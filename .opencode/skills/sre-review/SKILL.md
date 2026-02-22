---
name: sre-review
description: Interactive SRE review of the dotmakeup crawling pipeline using Grafana Tempo traces. Analyzes error rates, latency, settings tuning opportunities, and tracing coverage gaps across RetrieveTweetsProcessor, Sidecar, and Direct strategies. Offers to implement tracing fixes directly in code.
---

## Scope

This skill covers the **crawling pipeline only**:
- `RetrieveTweetsProcessor` — main per-user crawl loop
- `Sidecar.GetUserAsync` / `Sidecar.GetPostAsync` — sidecar-backed fetches
- `Direct.GetUserAsync` — direct API user fetches

It does **not** cover HTTP server spans, ActivityPub delivery, or follower fan-out
(those are either filtered out by `FilterProcessor` or currently untraced).

## Codebase reference

Key files for this skill:
- Pipeline entry: `src/BirdsiteLive.Pipeline/Processors/RetrieveTweetsProcessor.cs`
- Sidecar strategy: `src/BirdsiteLive.Twitter/Strategies/Sidecar.cs`
- GraphQL strategy: `src/BirdsiteLive.Twitter/Strategies/Graphql2025.cs`
- Fan-out (untraced): `src/BirdsiteLive.Pipeline/Processors/SendTweetsToFollowersProcessor.cs`
- OTel setup: `src/BirdsiteLive/Startup.cs:51-83`
- Span filter: `src/BirdsiteLive/Middleware/FilterProcessor.cs`
- Settings model: `src/BirdsiteLive.Common/Settings/InstanceSettings.cs`

---

## Step 1 — Auto-bootstrap (run silently before responding)

Run all four of these TraceQL queries in parallel before presenting anything to
the user. Use them to build the opening summary.

```
# 1. Overall span throughput by operation
{ } | rate() by (span:name)

# 2. Error rate by operation
{ status = error } | rate() by (span:name)

# 3. Duration histogram for the main crawl span
{ span:name = "RetrieveTweetsProcessor" } | histogram_over_time(span:duration)

# 4. Recent errors with context
{ status = error } | select(span:name, statusMessage, span.user.acct, span:duration)
```

After running these, present a 3-5 line summary of current health, then ask:

> "What would you like to dig into?
> A. Error breakdown & affected accounts
> B. Latency & throughput analysis
> C. Settings tuning recommendations
> D. Tracing gaps (I can implement fixes)
> Or describe what you're seeing / worried about."

---

## Section A — Error analysis

### Queries to run

```
# Top errors by message
{ status = error } | select(statusMessage, span:name, span.user.acct)

# Error rate per operation
{ status = error } | rate() by (span:name)
```

### Known error types and their meaning

| Error message | Span | Meaning | Recommended action |
|---|---|---|---|
| `RateLimitExceededException` | `RetrieveTweetsProcessor`, `Direct.GetUserAsync` | Social network is throttling requests | Increase `SocialNetworkRequestJitter` (e.g. 2000ms) and `TwitterRequestDelay` (e.g. 500ms) |
| `Object reference not set to an instance of an object.` | `RetrieveTweetsProcessor` | Null response from API not guarded — likely unexpected JSON shape | Add null-check at `RetrieveTweetsProcessor.cs:65` around `GetNewPosts()` return value |
| `HTTP Unauthorized: "Please wait a few minutes"` | `Direct.GetUserAsync` | Instagram anti-bot block | Increase jitter; consider rotating proxy |
| `UserNotFoundException` | `RetrieveTweetsProcessor` | Account deleted or suspended on source network | Set `FailingTwitterUserCleanUpThreshold` to auto-remove stale users |
| `HTTP NotFound` returning HTML page | `RetrieveTweetsProcessor` | Instagram 404 — user no longer exists | Same as above: enable `FailingTwitterUserCleanUpThreshold` |
| Proxy `502` from `geo.iproyal.com` | `RetrieveTweetsProcessor` | Outbound proxy is intermittently failing | Check proxy health; consider increasing proxy pool or failover |

### Silent errors (not visible as span errors)

`Graphql2025.GetTimelineAsync` (`src/BirdsiteLive.Twitter/Strategies/Graphql2025.cs:74-78`)
returns an empty list `[]` on 401/403/429 instead of throwing. These show up as
`posts.count = 0` with `status = unset` — they are **not** tagged as errors in Tempo.

To surface them, run:
```
{ span:name = "RetrieveTweetsProcessor" && span.posts.count = 0 } | rate()
```
A high zero-post rate that doesn't match the error rate is a sign of silent failures.
Ask the user if this rate looks abnormal, and if so suggest adding error tagging in
`Graphql2025.cs:74-78`.

---

## Section B — Latency analysis

### Queries to run

```
# p95 duration by operation
{ } | quantile_over_time(span:duration, 0.95) by (span:name)

# Slowest individual crawls with account names
{ span:name = "RetrieveTweetsProcessor" } | select(span.user.acct, span:duration, span.posts.count, span.user.isVip)

# VIP vs non-VIP latency comparison
{ span:name = "RetrieveTweetsProcessor" && span.user.isVip = true } | quantile_over_time(span:duration, 0.95)
{ span:name = "RetrieveTweetsProcessor" && span.user.isVip = false } | quantile_over_time(span:duration, 0.95)
```

### Known latency pattern

The duration histogram shows a **bimodal distribution**:
- Short bucket (~67ms): empty/cached responses — account has no new posts
- Long bucket (~8-17s): live API fetches returning actual posts

This is expected behaviour. If the long bucket grows beyond ~20s consistently,
it may indicate upstream API slowness, proxy latency, or token exhaustion.

### VIP accounts

`user.isVip = true` is set when a follower from `r.town` follows that account
(`RetrieveTweetsProcessor.cs:55`). VIP accounts get `user.Followers += 9999`
to boost their crawl priority. If VIPs are consistently slower than non-VIPs,
it may mean they are active high-post accounts creating more parsing work.

---

## Section C — Settings tuning

All settings live in `src/BirdsiteLive.Common/Settings/InstanceSettings.cs` and
are overridden via environment variables in `k8s/dotmakeup.yaml`.

| Setting | Default | Signal to look for | Recommendation |
|---|---|---|---|
| `ParallelTwitterRequests` | 10 | High `RateLimitExceededException` rate on `Direct.GetUserAsync` (currently ~12% error rate) | Reduce to 5 if using Direct strategy to reduce ban risk |
| `SocialNetworkRequestJitter` | 0 | Any rate limit errors | Set to 1000-3000ms to randomise request spacing |
| `TwitterRequestDelay` | 0 | Tight burst pattern visible in span timestamps | Set to 500-1000ms between batches |
| `ParallelFediversePosts` | 10 | No traces available for fan-out, check logs | Leave unless log errors spike |
| `FailingTwitterUserCleanUpThreshold` | unset (0) | `UserNotFoundException` or 404 errors recurring for same accounts | Set to e.g. 5 to auto-remove dead accounts after 5 consecutive failures |
| `FailingFollowerCleanUpThreshold` | -1 (disabled!) | Dead followers accumulate silently — no trace signal | **This is a dangerous default.** Set to e.g. 10 to prevent unbounded dead-follower growth |
| `UserCacheCapacity` | 40,000 | No trace signal — monitor memory | Leave unless memory pressure observed |
| `TweetCacheCapacity` | 20,000 | No trace signal | Leave unless memory pressure observed |
| `PostCacheRetentionDays` | 28 | No trace signal | Fine for current usage |
| `PipelineStartupDelay` | 15 min | Slow-start after pod restart visible as gap in traces | Reduce to 5 min if restarts are frequent |

**Note on `FailingFollowerCleanUpThreshold = -1`:**
The check in `SendTweetsToFollowersProcessor.cs:152` is:
```csharp
if (follower.PostingErrorCount > _instanceSettings.FailingFollowerCleanUpThreshold
    && _instanceSettings.FailingFollowerCleanUpThreshold > 0 ...)
```
The `> 0` guard means `-1` effectively disables cleanup entirely. Dead followers
accumulate indefinitely, wasting AP delivery attempts every crawl cycle. This
has no trace signal — it is invisible until you query the database directly.

---

## Section D — Tracing gaps

For each gap below: describe the problem, show the exact fix, then ask the user
"Would you like me to implement this now?" before writing any code.

### Gap 1 — `posts.count` not set on error path

**File:** `src/BirdsiteLive.Pipeline/Processors/RetrieveTweetsProcessor.cs:60-88`

**Problem:** `posts.count` is only set after a successful fetch (line 66). On any
error path the tag is absent, making it impossible to distinguish "fetched 0 posts"
from "fetch failed" using `{ span.posts.count = 0 }`.

**Fix:** Move `activity?.SetTag("posts.count", 0)` to just before the `try` block,
then overwrite it with the real count on success.

```csharp
// Before try block:
activity?.SetTag("posts.count", 0);
try
{
    var tweets = await _socialMediaService.GetNewPosts(user);
    activity?.SetTag("posts.count", tweets.Length);  // overwrites 0 on success
    ...
}
```

### Gap 2 — Exception type not queryable

**File:** `src/BirdsiteLive.Pipeline/Processors/RetrieveTweetsProcessor.cs:78-88`

**Problem:** Both catch blocks set `statusMessage` to `e.Message` but never tag
the exception type. You cannot run `{ span.error.type = "RateLimitExceededException" }`
to isolate rate-limit errors from null-ref crashes.

**Fix:** Add `activity?.SetTag("error.type", e.GetType().Name)` in both catch blocks.

```csharp
catch (RateLimitExceededException e)
{
    activity?.SetTag("error.type", e.GetType().Name);
    activity?.SetStatus(ActivityStatusCode.Error, e.Message);
    ...
}
catch (Exception e)
{
    activity?.SetTag("error.type", e.GetType().Name);
    activity?.SetStatus(ActivityStatusCode.Error, e.Message);
    ...
}
```

### Gap 3 — `Sidecar.GetTimelineAsync` has no span

**File:** `src/BirdsiteLive.Twitter/Strategies/Sidecar.cs:71-134`

**Problem:** `Sidecar.cs` has OTel metric counters (`dotmakeup_api_called_count`)
but no `ActivitySource` spans. `Sidecar.GetUserAsync` and `Sidecar.GetPostAsync`
appear in Tempo (they must be instrumented elsewhere), but timeline fetches —
the hot path — are completely invisible.

**Fix:** Add an `ActivitySource` to `Sidecar.cs` (reuse the existing `"DotMakeup"`
source) and wrap `GetTimelineAsync` in a span tagged with `endpoint`, `backend`,
and `result` (success/rate-limit/error).

```csharp
private static readonly ActivitySource ActivitySource = new("DotMakeup");

public async Task<List<ExtractedTweet>> GetTimelineAsync(...)
{
    using var activity = ActivitySource.StartActivity("Sidecar.GetTimelineAsync", ActivityKind.Internal);
    activity?.SetTag("crawl.endpoint", withReplies ? "postbyuserwithreplies" : "postbyuser");
    activity?.SetTag("crawl.strategy", "Sidecar");
    try
    {
        ...
        if (httpResponse.StatusCode != HttpStatusCode.OK)
        {
            activity?.SetStatus(ActivityStatusCode.Error, httpResponse.StatusCode.ToString());
            activity?.SetTag("error.type", "HttpError");
            ...
        }
        activity?.SetTag("posts.count", tweets.Count);
        return tweets;
    }
    catch (Exception e)
    {
        activity?.SetStatus(ActivityStatusCode.Error, e.Message);
        activity?.SetTag("error.type", e.GetType().Name);
        throw;
    }
}
```

### Gap 4 — No `crawl.strategy` tag on `RetrieveTweetsProcessor`

**File:** `src/BirdsiteLive.Pipeline/Processors/RetrieveTweetsProcessor.cs:60`

**Problem:** When a crawl fails or is slow, there is no way to tell from the span
whether the active strategy was `Graphql2025`, `Sidecar`, `Syndication`, or `Direct`.
This makes it impossible to compare strategy performance or isolate strategy-specific
failures.

**Fix:** Surface the strategy name from `ISocialMediaService` and tag it:

```csharp
activity?.SetTag("crawl.strategy", _socialMediaService.GetType().Name);
```

Or if `ISocialMediaService` wraps a strategy via composition, expose a
`StrategyName` property on the interface.

### Gap 5 — `SendTweetsToFollowersProcessor` is completely uninstrumented

**File:** `src/BirdsiteLive.Pipeline/Processors/SendTweetsToFollowersProcessor.cs`

**Problem:** AP delivery — the second half of every pipeline cycle — has zero
traces and zero metrics. Follower delivery failures, dead instance removals, and
HTTP 403 responses are only visible in logs. There is no way to measure delivery
latency, per-instance failure rates, or overall fan-out throughput from Tempo.

**Fix:** Add spans at two levels:
1. A parent span per user: `SendTweetsToFollowersProcessor` tagged with
   `user.acct`, `posts.count`, `followers.count`
2. Child spans per target instance: `SendTweetsToInstance` tagged with
   `target.host`, `inbox.type` (shared/individual), `result` (ok/error/removed)

Note: This would meaningfully increase trace volume. At current usage (~500MB/month
out of a 50GB Grafana free-tier limit, ~1% used), there is substantial headroom.
Adding fan-out spans may increase volume 2-3x but would still remain well under budget.

### Gap 6 — Token refresh events not traced (`Graphql2025`)

**File:** `src/BirdsiteLive.Twitter/Strategies/Graphql2025.cs:44-48`, `74-78`

**Problem:** When a guest token is rejected (401/403), `RefreshClient()` is called
silently. There is no trace event, no metric, and no counter for how often this
happens. Frequent token refreshes are a leading indicator of impending rate-limit
bans but are currently invisible.

**Fix:** Add a metric counter to `StatisticsHandler` (or directly in the strategy):
```csharp
_tokenRefreshCounter.Add(1, new KeyValuePair<string, object>("strategy", "Graphql2025"));
```
Or tag the parent `RetrieveTweetsProcessor` span with `token.refreshed = true`
when a refresh occurs during that crawl cycle.

---

## Trace volume budget

- **Current usage:** ~500MB/month
- **Grafana free tier limit:** 50GB/month
- **Usage:** ~1% of budget
- **Span rate:** ~0.37 spans/sec (~960K spans/month at ~520 bytes/span)

Adding all gaps above (Gaps 1-6) would increase span count roughly 3-4x, to
~3-4M spans/month — still well under 1GB/month, far below the 50GB limit.
**It is safe to add significantly more tracing without any cost concern.**

---

## Updating this skill

This skill is a living document. After every investigation session, propose
changes in **both directions** — adding new knowledge and removing stale content.

### Adding new content

When you discover something not yet documented:

1. Note it internally during the investigation
2. At the end of the session, say:
   > "I found something worth adding to the skill: [describe it]. Want me to
   > append it to the Learned Notes section?"
3. If the user agrees, append a dated entry to `## Learned Notes` below using
   the Edit tool.

Things worth adding:
- A new error message seen in traces not listed in Section A
- A TraceQL query that proved useful during investigation
- A setting interaction or edge case discovered
- A code path found to be misbehaving in a non-obvious way
- Any correlation discovered between two signals (e.g. "high jitter correlates with lower error rate")
- A gap from Section D that has since been implemented (move it to a "Fixed" note)

### Removing stale content

Equally important: flag content that is no longer accurate or useful.

At the end of a session, scan for staleness and say:
> "I also think [section/item] may no longer be relevant because [reason].
> Want me to remove or update it?"

Things to watch for:
- A tracing gap in Section D that has been implemented — the gap entry should be
  removed (or replaced with a brief "Fixed on [date]" note in Learned Notes)
- An error type in Section A that hasn't appeared in traces for several sessions —
  may be resolved; flag for removal if confirmed
- A settings recommendation that contradicts what is now deployed in `k8s/dotmakeup.yaml`
- A code reference (file:line) that no longer matches the current code after a refactor
- A Learned Note that has been superseded by a newer finding on the same topic
- The trace volume budget numbers if usage has grown significantly

When proposing a deletion, always quote the exact text to be removed so the user
can confirm before you use the Edit tool.

---

## Learned Notes

_No notes yet. Notes are added here during investigations._
