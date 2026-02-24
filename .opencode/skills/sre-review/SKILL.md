---
name: sre-review
description: Interactive SRE review of the dotmakeup crawling pipeline using Grafana Tempo traces. Analyzes error rates, latency, settings tuning opportunities, and tracing coverage gaps across RetrieveTweetsProcessor, Sidecar, and Direct strategies. Offers to implement tracing fixes directly in code.
---

## Scope

This skill covers the **crawling pipeline only**:
- `RetrieveTweetsProcessor` — main per-user crawl loop
- `Sidecar.GetUserAsync` / `Sidecar.GetPostAsync` — sidecar-backed fetches
- `Direct.GetUserAsync` — direct API user fetches

It does **not** cover HTTP server spans, ActivityPub delivery to remote servers,
or follower fan-out. Delivery to other servers (`SendTweetsToFollowersProcessor`,
remote inbox POSTs, per-instance failure tracking) is out of scope — those paths
are either filtered out by `FilterProcessor` or currently untraced, and
investigating them is not part of an SRE review session with this skill.

## MCP / tooling limitations

The Grafana MCP integration currently exposes **Tempo only** (traces). Logs (Loki)
and metrics (Prometheus/Mimir) are still not queryable via MCP tools.

However, logs/metrics can be queried in-session via Grafana Cloud HTTP APIs when
the user provides read-only credentials as environment variables.

Required env vars:
- `GRAFANA_TOKEN` (Grafana Cloud Access Policy token)
- `LOKI_URL`, `LOKI_USER`
- `MIMIR_URL`, `MIMIR_USER`

Auth pattern:
- Loki: `curl -u "$LOKI_USER:$GRAFANA_TOKEN" "$LOKI_URL/loki/api/v1/..."`
- Mimir: `curl -u "$MIMIR_USER:$GRAFANA_TOKEN" "$MIMIR_URL/api/v1/..."`

Important URL note:
- `MIMIR_URL` may already include `/api/prom` (e.g.
  `https://prometheus-<region>.grafana.net/api/prom`); in that case use
  `/api/v1/...` after it (not `/api/prom/api/v1/...`).

If those env vars are missing or auth fails, fall back to Tempo-only analysis and
ask the user to verify Loki/Mimir manually.

Observability budget constraints:
- Traces budget: 50GB/month.
- Logs budget: 50GB/month (counted separately from traces).
- Metrics budget: 10k metric-series limit.
- When suggesting new labels/tags, keep cardinality low (avoid per-user/per-post
  labels in metrics) so the 10k metrics limit is not exhausted.

Even when logs/metrics are unavailable, improvements to logs and metrics **can still
be suggested**. Suggestions worth making:

**Logs:**
- Structured log fields (e.g. `acct`, `strategy`, `postCount`) on key events in
  `RetrieveTweetsProcessor` and `Sidecar` would make Loki queries far more useful.
  Currently most log lines are unstructured strings.
- `Console.WriteLine` calls (e.g. `Sidecar.cs:65`, `Sidecar.cs:131`) should be
  replaced with `_logger.LogError` so they appear in Loki at the correct severity.

**Metrics:**
- A `dotmakeup_crawl_errors_total` counter broken down by `error_type` and
  `strategy` would give a durable rate signal independent of trace sampling.
- A `dotmakeup_token_refresh_total` counter in `Graphql2025.cs` would surface
  token churn as a Grafana alertable metric (see Gap 6 in Section D).
- Gauge for active follower count per instance host would make dead-follower
  accumulation visible without a direct DB query.

## Codebase reference

Key files for this skill:
- Pipeline entry: `src/BirdsiteLive.Pipeline/Processors/RetrieveTweetsProcessor.cs`
- Sidecar strategy: `src/BirdsiteLive.Twitter/Strategies/Sidecar.cs`
- GraphQL strategy: `src/BirdsiteLive.Twitter/Strategies/Graphql2025.cs`
- Fan-out (untraced): `src/BirdsiteLive.Pipeline/Processors/SendTweetsToFollowersProcessor.cs`
- OTel setup: `src/BirdsiteLive/Startup.cs:51-83`
- Span filter: `src/BirdsiteLive/Middleware/FilterProcessor.cs`
- Settings model: `src/BirdsiteLive.Common/Settings/InstanceSettings.cs`

## Interaction rule

When this skill asks the user a question, always use the OpenCode native
`question` tool instead of plain-text prompts. This applies to all prompts,
including the Step 2 "What would you like to dig into?" menu and Section D
"Would you like me to implement this now?" confirmations.

---

## Step 1 — Per-instance baseline (run silently before responding)

Before anything else, assess how each instance is doing over the last few days:
`bird`, `kilogram`, and `hacker`.

- Default lookback window: last 72 hours.
- Use all three signal types: traces (Tempo), metrics (Mimir), logs (Loki).
- If Loki/Mimir credentials are unavailable or fail, do a traces-only baseline and
  explicitly state that logs/metrics were unavailable.

### 1A. Traces (Tempo)

Run per-instance trace checks for each of `bird`, `kilogram`, and `hacker`.
Use whichever attribute currently identifies instance in spans (prefer
`span.instance`, otherwise use a resource attribute such as
`resource.service.name` / `resource.k8s.namespace.name`).

For each instance, gather at least:

```
# throughput by operation
{ <instance-attr> = "bird" } | rate() by (span:name)

# error rate by operation
{ <instance-attr> = "bird" && status = error } | rate() by (span:name)

# crawl latency distribution
{ <instance-attr> = "bird" && span:name = "RetrieveTweetsProcessor" } | histogram_over_time(span:duration)
```

Repeat for `kilogram` and `hacker`.

### 1B. Metrics (Mimir, when available)

Use Mimir to compare instances over the same window. Prefer these queries (or
equivalent label names in your deployment):

```promql
sum by (instance) (rate(dotmakeup_api_called_count[15m]))
sum by (instance, error_type, strategy) (increase(dotmakeup_crawl_errors_total[72h]))
sum by (instance, strategy) (increase(dotmakeup_token_refresh_total[72h]))
```

### 1C. Logs (Loki, when available)

Use Loki to identify recurring error patterns by instance over the same window.
Prefer structured filters if labels/fields exist; otherwise use best-effort text
filters and note the limitation.

Minimum checks:
- Error log volume by instance
- Top recurring crawl-related error messages per instance
- Any repeated rate-limit/proxy/auth patterns per instance

### 1D. Opening response requirement

Start every session with a short per-instance health block (3 lines minimum):
- `bird`: status, dominant failure mode (if any), and latency note
- `kilogram`: status, dominant failure mode (if any), and latency note
- `hacker`: status, dominant failure mode (if any), and latency note

Then continue with the cross-instance summary and guided question menu.

## Step 2 — Auto-bootstrap (run silently before responding)

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

### Optional preflight — Loki/Mimir access (when env vars are present)

If `GRAFANA_TOKEN`, `LOKI_URL`, `LOKI_USER`, `MIMIR_URL`, and `MIMIR_USER` are set,
run these smoke tests (via Bash) and include pass/fail in your opening context:

```bash
# Loki labels endpoint
curl -sS -u "$LOKI_USER:$GRAFANA_TOKEN" "$LOKI_URL/loki/api/v1/labels"

# Mimir metric names endpoint
curl -sS -u "$MIMIR_USER:$GRAFANA_TOKEN" "$MIMIR_URL/api/v1/label/__name__/values"

# Mimir basic query (may return empty result vector but should be HTTP 200)
curl -sS -G -u "$MIMIR_USER:$GRAFANA_TOKEN" "$MIMIR_URL/api/v1/query" \
  --data-urlencode "query=up"
```

If available, use Loki/Mimir evidence to support Section A-C conclusions.

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

## Section D — Tracing gaps (audit then log)

Do not treat this section as a static backlog. At the start of each investigation:

1. Re-check every previously known gap against current code.
2. If a gap is fixed, remove it from active gap tracking and record a short
   "Fixed on <date>" note in `## Learned Notes`.
3. If a gap is still open, keep it in `## Learned Notes` under a single current
   "Open tracing gaps" entry (problem + exact fix), instead of keeping a long
   duplicate list here.

When presenting an open gap to the user, describe the problem and exact fix, then
ask via the OpenCode native `question` tool whether to implement it now.

Current open gaps are tracked in `## Learned Notes`.

---

## Observability budget

- **Traces:** ~500MB/month today vs 50GB/month limit (~1% used).
- **Logs:** 50GB/month limit, tracked separately from traces.
- **Metrics:** 10k metric-series limit.
- **Current span rate:** ~0.37 spans/sec (~960K spans/month at ~520 bytes/span).

### Pre-change impact check (required)

Before proposing or implementing any change that adds/removes observability
(new spans, new log fields, new metrics, or label changes), run a quick impact
baseline first:

- Metrics usage (direct): `count({__name__=~".+"})` via Mimir query API.
- Logs usage (direct for time window): Loki `index/volume_range` over a recent
  window (default 72h), then project monthly roughly.
- Traces usage: no single direct usage API in current tooling; estimate from
  span rate and expected span cardinality/size change.

Include that estimate before making the change and call out risk against:
- traces 50GB/month
- logs 50GB/month
- metrics 10k series

Instrumenting the currently open tracing gaps would still increase span volume by
only a few multiples, which remains well below 1GB/month and far below the traces
budget. It is safe to add crawl-pipeline tracing. For logs/metrics changes, prefer
low-cardinality dimensions (`strategy`, `instance`, `result`, `error_type`) and avoid
high-cardinality labels (e.g., raw account names) on metrics.

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
- A tracing gap that has since been implemented (move it to a "Fixed" note)

### Removing stale content

Equally important: flag content that is no longer accurate or useful.

At the end of a session, scan for staleness and say:
> "I also think [section/item] may no longer be relevant because [reason].
> Want me to remove or update it?"

Things to watch for:
- A tracing gap that has been implemented — remove it from the active "Open tracing
  gaps" note and add a brief "Fixed on [date]" note in Learned Notes
- An error type in Section A that hasn't appeared in traces for several sessions —
  may be resolved; flag for removal if confirmed
- A settings recommendation that contradicts what is now deployed in `k8s/dotmakeup.yaml`
- A code reference (file:line) that no longer matches the current code after a refactor
- A Learned Note that has been superseded by a newer finding on the same topic
- The trace volume budget numbers if usage has grown significantly

When proposing a deletion, always quote the exact text to be removed so the user
can confirm before you use the Edit tool.

### Work log retention

Treat `## Learned Notes` as the session work log and keep at most 5 entries.

- When adding a new note and there are already 5 entries, remove one first.
- Prefer removing the oldest or most stale/superseded note.
- Keep the most recent 5 notes that are still useful for current investigations.

---

## Learned Notes

### 2026-02-23 — Loki/Mimir access works via Grafana Cloud API credentials

Logs and metrics are still unavailable through MCP tools, but are queryable during
sessions via `curl` when these env vars are set:
`GRAFANA_TOKEN`, `LOKI_URL`, `LOKI_USER`, `MIMIR_URL`, `MIMIR_USER`.

Validated smoke tests:
- `GET $LOKI_URL/loki/api/v1/labels` returns HTTP 200 with valid credentials.
- `GET $MIMIR_URL/api/v1/label/__name__/values` returns HTTP 200 and includes
  `dotmakeup_*` metrics.
- `GET $MIMIR_URL/api/v1/query?query=up` can return an empty vector with HTTP 200;
  this still confirms auth and endpoint wiring.
- `sum(rate(dotmakeup_api_called_count[15m]))` returned a non-empty vector in-session.

Token/scoping note:
- Grafana UI service-account tokens may fail against Loki/Mimir with `invalid token`.
  Prefer Grafana Cloud Access Policy tokens scoped for `logs:read` and `metrics:read`
  (optionally `traces:read`).

---

### 2026-02-22 — Scope clarification: delivery to other servers is out of scope

AP delivery to remote servers (`SendTweetsToFollowersProcessor` fan-out, HTTP POST
to remote inboxes, per-instance failure tracking) is explicitly **out of scope** for
this skill. The open-gaps work log documents what instrumentation _could_ be added, but
investigating or tuning delivery behaviour is not part of an SRE review session here.

---

### 2026-02-22 — `ISocialMediaService.ServiceName` vs `GetType().Name` for `crawl.strategy`

`ISocialMediaService` has a `ServiceName` string property, but it returns a
human-readable display name (e.g. `"Twitter"`). For the `crawl.strategy` span tag,
`_socialMediaService.GetType().Name` is more useful because it distinguishes concrete
implementations (`TwitterService`, `InstagramService`, `HnService`) without requiring
interface changes. Use `GetType().Name` when tagging strategy in processor spans.

---

### 2026-02-23 — Open tracing gaps (after code audit)

Code re-check against current repo state:
- Fixed: former gaps 1-4 are already implemented in code (`posts.count` default on
  error path, `error.type` tagging, `Sidecar.GetTimelineAsync` span, and
  `crawl.strategy` tag on `RetrieveTweetsProcessor`).
- Open gap: `SendTweetsToFollowersProcessor` remains uninstrumented. Suggested fix:
  add one parent span per user fan-out and child spans per target host/inbox route.
- Open gap: `Graphql2025` token refresh events are still untraced. Suggested fix:
  add `dotmakeup_token_refresh_total` counter and/or `token.refreshed=true` span tag
  when `RefreshClient()` runs.

---

### 2026-02-22 — Dominant error pattern: NullReferenceException in RetrieveTweetsProcessor

7 of 11 error traces in the past hour were `NullReferenceException` in
`RetrieveTweetsProcessor`, all with durations of ~14–17 seconds. Affected accounts
were unrelated (`duckduckgo`, `synacktiv`, `sg_posters`, `slpng_giants_fr`, etc.),
ruling out account-specific causes. The consistent long duration before crash
suggests `GetNewPosts()` completes a full upstream fetch but returns `null` (rather
than an empty array), which then hits `.Length` without a null guard. This is now
queryable via `{ span.error.type = "NullReferenceException" }`.
