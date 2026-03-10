---
name: sre-review
description: Interactive SRE review of dotmakeup crawl coverage/freshness and federation read-query reliability using Tempo traces, Mimir metrics, Grafana dashboard definitions, and the production PostgreSQL database. Analyzes crawl errors, crawl duration shape, settings tuning opportunities, tracing coverage gaps across RetrieveTweetsProcessor plus Graphql2025, Sidecar, and Direct strategy spans, and suggests dashboard/query/panel improvements. Uses DB ground-truth data for crawl staleness, error accumulation, strategy policy checks, and shared-budget recommendations for remote user/post lookups.
---

## Scope

This skill covers two operational surfaces:
- `RetrieveTweetsProcessor` — main per-user crawl loop
- `Graphql2025.GetUserAsync` / `Graphql2025.GetTimelineAsync` — GraphQL-backed Twitter fetches
- `Sidecar.GetUserAsync` / `Sidecar.GetPostAsync` / `Sidecar.GetTimelineAsync` — sidecar-backed fetches
- `Direct.GetUserAsync` — direct API user fetches
- Federation read-query serving for remote lookups of users/posts (for example `/.well-known/webfinger`, `/users/{id}`, and `/users/{id}/statuses/{statusId}`)

It does **not** cover ActivityPub delivery to remote servers or follower fan-out.
Delivery paths (`SendTweetsToFollowersProcessor`, remote inbox POSTs,
per-instance delivery failure tracking) remain out of scope for this skill.
HTTP latency optimization is also out of scope; federation read-path review here
focuses on success rate and upstream-cost efficiency.
Because HTTP server spans are filtered in tracing, evaluate read-path health via
metrics/logs rather than Tempo traces.

## Operator priorities

For this operator, optimize in strict order:

1. **Crawl coverage/freshness first** (how many accounts are effectively checked
   under rate limits and crawl budget).
2. **Federation read-query success second** (how reliably remote servers can
   resolve users/posts), while minimizing upstream fetch cost.

- Shared upstream budget rule: upstream fetches triggered to answer read queries
  consume the same external rate-limit budget as crawling.
- Tie-break rule: if goals conflict, preserve crawl coverage/freshness first.
- Priority #2 is metrics-first: optimize success rate and upstream-cost
  efficiency, not HTTP latency.
- In summaries and proposal ordering, present #1 outcomes first, then #2 outcomes.

## MCP / tooling

Two MCP integrations are available: **Tempo** (traces) and **PostgreSQL**
(production database). Grafana dashboards are reviewed from local dashboard JSON
files and, when credentials are provided, from the live Grafana Dashboard HTTP
API. Logs (Loki) and metrics (Prometheus/Mimir) are accessible via Grafana Cloud
HTTP APIs when credentials are provided.

### Tempo MCP (traces — primary signal for crawl)

Tempo is the primary signal for crawl runtime behaviour: error rates, crawl
duration shape, span throughput, and per-account outcomes. All `tempo_*` tools
are available.

Tempo/Loki signal notes:
- Tempo metric-style aggregations can fail when the time range is too large.
  Range limits depend on backend/service settings and can change. For multi-day
  baselines, split into smaller windows and aggregate, or fall back to a recent
  window and state the limitation explicitly.

### PostgreSQL MCP (production DB — secondary signal)

The production database is accessible via the `postgres_*` MCP tools. Use it as
a **secondary signal** that provides ground-truth coverage, freshness, error
accumulation, and strategy policy data that traces cannot easily show.

Connection details:
- Role: `opencode_sre` (read-only)
- Database: `dotmakeup`

Accessible tables (SELECT only):

| Table | Key columns for SRE | Purpose |
|---|---|---|
| `twitter_users` | `acct`, `lastsync`, `fetchingerrorcount`, `wikidata`, `extradata` | Twitter account inventory, crawl freshness, error accumulation |
| `instagram_users` | `acct`, `lastsync`, `wikidata`, `extradata` | Instagram account inventory and crawl freshness |
| `hn_users` | `acct`, `lastsync`, `type`, `wikidata` | HackerNews account inventory and crawl freshness |
| `followers` | `acct`, `host`, `followings`, `followings_instagram`, `followings_hn`, `postingerrorcount` | Follower demand, VIP detection (r.town), delivery error tracking |
| `settings` | `setting_key`, `setting_value` (JSONB) | Strategy policy settings (`nitter`, `ig_crawling`, etc.) |

Inaccessible tables (permission denied):
- `workers`, `twitter_crawling_users`, `cached_tweets`, `cached_insta_posts`, `db_version`

Usage guidelines:
- Use `postgres_execute_sql` for custom queries. All other `postgres_list_*`
  tools are also available but less useful for SRE-specific queries.
- DB queries have **zero impact** on observability budgets (traces/logs/metrics).
- Prefer DB for ground-truth counts and freshness checks; prefer Tempo for
  runtime error patterns and crawl execution behaviour.
- Do not query for individual user credentials or sensitive data — focus on
  aggregate statistics and account-level crawl metadata.

### Grafana dashboards (local JSON + HTTP API)

Dashboard review is required for this skill. Inspect both dashboards:
- `grafana/infra.json`
- `grafana/audience.json`

Preferred source order:
1. Live dashboard API (when `GRAFANA_URL` + `GRAFANA_TOKEN` are set and
   dashboard read access is available).
2. Local JSON files as fallback (always available in repo).

API access notes:
- Use `https://$GRAFANA_URL/apis/dashboard.grafana.app/v1beta1/namespaces/:namespace/dashboards/:name`.
- In this API path, use `metadata.name` as `:name` (not `metadata.uid`).
- Derive `:namespace` and `:name` from each dashboard file's metadata.
- If API read fails, continue with local file analysis and state the limitation.
- Important deploy note: live dashboard reads work from the `dashboard.grafana.app`
  API, but writing those CRD-style objects back with `PUT` can produce dashboards
  that exist yet render with zero legacy `panels`. For deployment, prefer the
  legacy dashboard HTTP API (`POST /api/dashboards/db`) with a complete dashboard
  model, and use dashboard version history for rollback if needed.

Deployment expectation for approved dashboard changes:
- Requires `dashboards:write` (and folder scope) on `GRAFANA_TOKEN`.
- When the currently approved proposal is a dashboard change and write access is
  available, deploy it in the same session without waiting for a separate
  deployment request.
- Apply only the currently approved dashboard changes.
- After deployment, wait for the operator's `Done` / `Needs changes` review gate
  before moving to the next proposal.
- Live dashboard changes may only use telemetry that is already available and
  queryable in Grafana at deployment time. Do not add panels that depend on
  metrics/logs/traces which were only introduced in code during the same run but
  are not yet flowing into Grafana.
- For telemetry that has been added in code but is not yet visible in Grafana,
  record a note for a future run and update the dashboard only after that
  telemetry is confirmed live.
- Every non-trivial panel should have a clear description that explains what it
  measures, why it matters, and any important caveat (for example ratio
  denominator, cache-vs-upstream interpretation, or whether it is demand- or
  VIP-weighted).
- After update, re-GET dashboard and confirm generation/resourceVersion changed.
- Keep local `grafana/*.json` aligned with deployed content.

### Loki / Mimir (optional — via HTTP APIs)

Logs/metrics can be queried in-session via Grafana Cloud HTTP APIs when
the user provides read-only credentials as environment variables.

Required env vars:
- `GRAFANA_TOKEN` (Grafana Cloud Access Policy token for dashboards and Loki)
- `LOKI_URL`, `LOKI_USER`
- `MIMIR_URL`, `MIMIR_USER`, `MIMIR_TOKEN`

For live dashboard review/deploy, also provide:
- `GRAFANA_URL` (for example `cloutier.grafana.net`)

Auth pattern:
- Loki: `curl -u "$LOKI_USER:$GRAFANA_TOKEN" "$LOKI_URL/loki/api/v1/..."`
- Mimir: `curl -u "$MIMIR_USER:$MIMIR_TOKEN" "$MIMIR_URL/api/v1/..."`

Important URL note:
- `MIMIR_URL` may already include `/api/prom` (e.g.
  `https://prometheus-<region>.grafana.net/api/prom`); in that case use
  `/api/v1/...` after it (not `/api/prom/api/v1/...`).

Loki signal notes:
- In Loki for this stack, fields such as `detected_level`, `scope_name`,
  `exception_type`, and `exception_message` may be parsed metadata rather than
  indexed stream labels. Prefer filtering that evaluates parsed metadata fields
  (pipeline mode) over strict label-only matching.

If those env vars are missing or auth fails, fall back to Tempo + DB analysis and
ask the user to verify Loki/Mimir manually.

Federation read-path monitoring rule:
- For priority #2, use Mimir metrics as the primary signal.
- Use metrics to track endpoint success/failure mix and upstream call pressure.
- Use DB only as context for cache capabilities and policy state.
- Do not optimize HTTP latency in this skill.

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
  token churn as a Grafana alertable metric.
- Gauge for active follower count per instance host would make dead-follower
  accumulation visible without a direct DB query (though the `followers` table
  can now be queried directly via PostgreSQL MCP for ground-truth counts).
- Low-cardinality read-serving counters by endpoint family + result class would
  make priority #2 success/cost tracking durable without relying on traces.

## Codebase reference

Key files for this skill:
- Pipeline entry: `src/BirdsiteLive.Pipeline/Processors/RetrieveTweetsProcessor.cs`
- Twitter Sidecar strategy: `src/BirdsiteLive.Twitter/Strategies/Sidecar.cs`
- GraphQL strategy: `src/BirdsiteLive.Twitter/Strategies/Graphql2025.cs`
- Instagram Direct strategy: `src/dotMakeup.Instagram/Strategies/Direct.cs`
- Instagram Sidecar strategy: `src/dotMakeup.Instagram/Strategies/Sidecar.cs`
- Federation read endpoints: `src/BirdsiteLive/Controllers/WellKnownController.cs`, `src/BirdsiteLive/Controllers/UsersController.cs`
- Fan-out (untraced): `src/BirdsiteLive.Pipeline/Processors/SendTweetsToFollowersProcessor.cs`
- Cache path: `src/BirdsiteLive.Domain/SocialNetworkCache.cs`, `src/DataAccessLayers/BirdsiteLive.DAL.Postgres/DataAccessLayers/SocialMediaUserPostgresDal.cs`
- OTel setup: `src/BirdsiteLive/Startup.cs:51-83`
- Span filter: `src/BirdsiteLive/Middleware/FilterProcessor.cs`
- Settings model: `src/BirdsiteLive.Common/Settings/InstanceSettings.cs`

### Database schema (accessible tables)

| Table | Key columns | Notes |
|---|---|---|
| `twitter_users` | `id` (PK), `acct` (unique), `lastsync` (timestamp), `fetchingerrorcount` (int), `lasttweetpostedid`, `twitteruserid`, `statusescount`, `wikidata` (JSONB), `extradata` (JSONB), `cache` (JSONB) | ~200k rows. `fetchingerrorcount` tracks consecutive crawl failures. `lastsync` is the ground-truth crawl freshness timestamp. |
| `instagram_users` | `id` (PK), `acct` (unique), `lastsync` (timestamp), `data` (JSONB), `wikidata` (JSONB), `extradata` (JSONB), `cache` (JSONB) | ~21k rows. No `fetchingerrorcount` column — error tracking is trace-only for Instagram. |
| `hn_users` | `id` (PK), `acct` (unique), `lastsync` (timestamp), `type` (char: `u`=user, `s`=story), `wikidata` (JSONB), `extradata` (JSONB) | ~87 rows. Small table. |
| `followers` | `id` (PK), `acct`+`host` (unique), `followings` (int[]), `followings_instagram` (int[]), `followings_hn` (int[]), `postingerrorcount` (int), `inboxroute`, `sharedinboxroute` | ~124k rows across ~5.7k unique hosts. `followings` arrays reference user IDs in the corresponding user tables. `host` includes `r.town` for VIP detection. |
| `settings` | `setting_key` (PK, text), `setting_value` (JSONB) | Strategy policy settings. Known keys: `nitter`, `ig_crawling`, `twitter_user_cache`, `key.json`. |

## Interaction rule

When this skill asks the user a question, always use the OpenCode native
`question` tool instead of plain-text prompts.
- Proposal approvals must be asked as explicit yes/no choices for the single
  current proposal.
- After an approved proposal is implemented, ask a separate review gate for
  that same proposal before suggesting the next one.
- Use `Done` / `Needs changes` for the post-implementation review gate.
- For dashboard proposals, auto-deploy immediately after approval when write
  access allows so the operator can review the live dashboard before answering
  the review gate.

## Proposal types

After analysis, turn findings into concrete proposals. Useful proposal types
include:
- Error remediation and reliability fixes
- Coverage/freshness and throughput improvements
- Federation read-query success and cache-efficiency improvements
- Settings/scheduler/strategy tuning
- Tracing/logging/metrics instrumentation improvements
- Integration/regression test additions for production-discovered parser failures
- Grafana dashboard/query/panel improvements
- Grafana dashboard deployment of approved changes
- Code-level fixes for identified root causes

When building proposals, prioritize impact in this order:
1. Crawl coverage/freshness impact
2. Federation read-query success + upstream-cost efficiency impact
3. Reliability/risk reduction
For dashboard/metrics suggestions, prefer low-cardinality dimensions.

When a finding involves parser correctness, payload-shape handling, or silent
parse degradation, include a `Suggested tests` output. Anchor those suggestions
to the existing integration/regression suites:
- Twitter: `src/Tests/BirdsiteLive.Twitter.Tests/TweetTests.cs` and
  `src/Tests/BirdsiteLive.Twitter.Tests/TimelineTests.cs`
- HackerNews: `src/Tests/dotMakeup.HackerNews.Tests/UsersTests.cs`,
  `src/Tests/dotMakeup.HackerNews.Tests/PostsTests.cs`, and
  `src/Tests/dotMakeup.HackerNews.Tests/TimelineTests.cs`
- Instagram: `src/Tests/dotMakeup.Instagram.Tests/UserTest.cs`

Prefer integration/regression tests over generic unit tests, use real stable
accounts/posts/timelines when possible, assert exact parsed fields instead of
only `does not throw`, and keep any implementation-specific exception minimal
and scoped rather than weakening the whole test.

---

## Step 1 — Per-instance baseline (run silently before responding)

Before anything else, assess how each instance is doing over the last few days:
`bird`, `kilogram`, and `hacker`.

- Naming note: operator-facing `kilogram` often appears as `kilo` in telemetry
  labels/pod IDs (for example `dotmakeup-kilo-*`). Treat them as the same instance.

- Default lookback window: last 72 hours.
- Use traces + DB for priority #1 (crawl coverage/freshness).
- Use metrics (Mimir) as primary for priority #2 (federation read-query
  success/cost). Logs (Loki) are optional supporting evidence.
- For dashboard analysis, prefer live API when available; otherwise use
  `grafana/infra.json` and `grafana/audience.json` from the repo.
- If Mimir is unavailable, complete priority #1 and explicitly state that
  priority #2 could not be fully measured.
- For Tempo metric-style checks over 72h, chunk into smaller windows if
  range limits are hit. If chunking is not practical, use a recent window and
  call it out.

### 1A. Traces (Tempo)

Run per-instance trace checks for each of `bird`, `kilogram`, and `hacker`.
Use whichever attribute currently identifies instance in spans (prefer
`span.instance`, otherwise use a resource attribute such as
`resource.service.name` / `resource.k8s.namespace.name`).

If instance values are pod-style (for example `resource.service.instance.id`),
use regex filters like `dotmakeup-bird-.*`, `dotmakeup-kilo-.*`,
`dotmakeup-hacker-.*` for per-instance views.

For each instance, gather at least:
- Root insight: operation throughput mix shows where crawl budget is spent; recommendation: tune scheduler fairness/strategy mix if one path dominates unexpectedly.
- Root insight: error concentration by operation shows the main budget leak; recommendation: fix the top failing path before global tuning.
- Root insight: main crawl latency shape shows coverage risk; recommendation: lower pressure and check proxy/token quality if long-tail latency reduces recrawl frequency.

Repeat for `kilogram` and `hacker`.

If Tempo returns a range-limit error, rerun throughput/error-rate/latency
checks in smaller windows and aggregate by operation for the per-instance view.

### 1A.1. Strategy tag semantics (important)

`RetrieveTweetsProcessor` sets `crawl.strategy` to the service class name
(`TwitterService`, `InstagramService`, `HnService`). That tag is useful for
service-level splits but **does not** split extractor behavior (Direct vs Sidecar
vs Graphql).

For extractor-level strategy success/failure, use child-span outcomes split by
extractor operation + success state:
- Instagram: compare `Direct.GetUserAsync` vs `Sidecar.GetUserAsync` /
  `Sidecar.GetPostAsync`.
- Twitter: compare `Graphql2025.GetUserAsync` /
  `Graphql2025.GetTimelineAsync` vs `Sidecar.GetTimelineAsync`.
- Root insight: pick the failing extractor first; recommendation: demote/cool down failing extractor paths before changing global limits.

### 1B. Metrics (Mimir, when available)

Use Mimir to compare instances over the same window:
- Root insight: API call rate per instance shows effective crawl throughput.
- Root insight: crawl error deltas by error type/strategy show where budget is lost to retries/failures.
- Root insight: token-refresh churn by strategy highlights auth instability.

### 1C. Logs (Loki, when available)

Use Loki to identify recurring error patterns by instance over the same window.
Prefer structured filters if labels/fields exist; otherwise use best-effort text
filters and note the limitation.

When error fields are parsed metadata, group error volume by instance using
those parsed fields (not only stream labels).
- Root insight: repeated error spikes by instance identify localized failure domains; recommendation: apply per-instance mitigation before global changes.

Minimum checks:
- Error log volume by instance
- Top recurring crawl-related error messages per instance
- Any repeated rate-limit/proxy/auth patterns per instance

### 1D. Database (PostgreSQL MCP)

Use the production database to get ground-truth coverage and freshness data that
traces can only approximate. Run these checks using `postgres_execute_sql`:
- Root insight: freshness distribution by service is the true coverage baseline; recommendation: bias scheduling toward services with rising stale share.
- Root insight: high `fetchingerrorcount` accounts are persistent loops; recommendation: clean up or deprioritize above threshold.
- Root insight: live `settings` policy values (`nitter`, `ig_crawling`) are the tuning baseline; recommendation: propose deltas from current values, not defaults.
- Root insight: distinct followed-user counts quantify demand; recommendation: prioritize high-demand pools before the long tail.
- Root insight: `r.town` coverage tracks VIP obligations; recommendation: preserve VIP freshness when trading off fairness.

Use these results to:
- Compare "users followed" vs "total users" to find orphan accounts nobody follows
- Identify the stalest high-demand accounts (followed by many but rarely synced)
- Cross-reference `fetchingerrorcount` hotspots with trace error patterns
- Read current strategy policy values before making tuning recommendations

### 1E. Federation read-query baseline (metrics-first)

Assess remote-server read traffic for user/post lookups (WebFinger, actor, and
status/activity documents):
- Root insight: per-instance success rate by endpoint family shows remote
  resolvability quality.
- Root insight: error mix split (`429`, `5xx`, `404`) distinguishes saturation,
  internal faults, and content-miss outcomes.
- Root insight: upstream API calls per successful read response show shared
  budget efficiency.
- Root insight: cache-backed services should keep upstream calls per successful
  read lower and more stable.
- Recommendation: optimize read-serving for higher success with fewer upstream
  calls per successful response.

Shared budget reminder for recommendations:
- Upstream fetches for read-query serving consume the same rate-limit bucket as
  crawling.
- If a read-serving optimization risks crawl freshness, prefer crawl-preserving
  options.

### 1F. Opening response requirement

Start every session with a short per-instance health block (3 lines minimum):
- `bird`: status, dominant failure mode (if any), and coverage/freshness note
- `kilogram` (`kilo` in labels): status, dominant failure mode (if any), and coverage/freshness note
- `hacker`: status, dominant failure mode (if any), and coverage/freshness note

Then include a DB-sourced coverage summary:
- Per-service crawl freshness (what % synced in last 1d / 7d, how many stale >30d)
- Top error-accumulation accounts if `fetchingerrorcount` hotspots exist
- Follower demand vs coverage gaps (users followed but rarely synced)

Then include a metrics-sourced federation read-query summary:
- Success trend by endpoint family (user/post read paths)
- `429`/`5xx`/`404` split (`404` reported separately, not mixed into reliability failures)
- Upstream call pressure per successful read and cache-efficiency direction

Then include a dashboard-quality summary:
- Most important panel/query mismatches affecting priority #1 and #2 decisions
- Missing or weak dashboard coverage areas (if any)
- 1-3 highest-impact dashboard fixes to consider

Then continue with the cross-instance summary and the current top-ranked
proposal.

### 1G. Grafana dashboard baseline

Build a dashboard baseline for both `infra` and `audience` dashboards before
proposal ranking.

Minimum checks:
- Root insight: panel intent vs query semantics (title, query, unit,
  thresholds) must match. Recommendation: correct mismatched titles/queries and
  ensure ratio panels are true ratios.
- Root insight: success-ratio panels should use numerator/denominator formulas,
  not raw counts named as "success rate". Recommendation: convert to ratio and
  use percent units.
- Root insight: operator-priority coverage in dashboards should match this
  skill's priorities. Recommendation: ensure both crawl freshness coverage and
  federation read success/cost coverage are visible.
- Root insight: query dimensions should stay low-cardinality.
  Recommendation: avoid account-level metric labels in panel queries.
- Root insight: dashboard usability should support fast per-instance diagnosis.
  Recommendation: add/fix variables (instance/service/strategy/result), naming,
  and row organization where missing.

Use this baseline to feed Section F and the iterative next-proposal ranking.

## Step 2 — Full parallel exploration (run silently before responding)

Before asking the user any proposal question, run all analysis packs in
parallel and use their outputs to build the initial ranked proposal set. After
each approved change, do only a targeted refresh of the affected evidence
before re-ranking the remaining proposals.

For context-window efficiency, prefer using subagents for parallel packs so the
main agent only keeps condensed findings and proposal candidates.

### 2.0 Execution model (subagents preferred)

When available, use the OpenCode `Task` tool to run packs 2A-2F concurrently.

- Recommended mapping:
  - `general` subagent: 2A bootstrap, 2B error pack, 2C coverage + federation pack
  - `general` subagent: 2D settings/strategy evidence and Loki/Mimir preflight
  - `explore` subagent: 2E tracing-gap code audit and 2F dashboard audit
- Keep each subagent prompt tightly scoped to its pack, lookback window, and
  required outputs only.
- Require each subagent to return a compact payload:
  - pack id (`2A`/`2B`/...)
  - up to 5 key findings
  - "interesting findings" to bubble up to the main conversation
    (unexpected regressions, cross-signal contradictions, or high-impact wins)
  - proposal candidates with: title, expected coverage/freshness impact,
    expected federation success/cost impact, supporting evidence,
    implementation effort, and suggested tests
- Merge only those compact payloads into the parent context; do not paste large
  raw query/result dumps unless needed for a specific follow-up.
- If subagents are unavailable, run equivalent direct tool calls in parallel.

### 2A. Bootstrap pack (Tempo)

Run four bootstrap signal checks in parallel. Use them for opening health
context and proposal ranking.

If metric-style checks fail due time-range limits, rerun in smaller windows (or
a recent-window fallback) and keep full-lookback error exemplars when possible.

- Root insight: operation throughput balance shows where crawl budget is spent.
  Recommendation: if main crawl share drops or helper spans dominate, tune scheduler fairness and strategy selection.
- Root insight: error concentration by operation identifies the top budget leak.
  Recommendation: fix the highest failing operation first.
- Root insight: main crawl latency shape shows whether long-tail work is
  expanding. Recommendation: if long-tail duration grows materially, reduce load pressure and improve endpoint/proxy quality.
- Root insight: recent error exemplars with account context separate persistent
  loops from transient spikes. Recommendation: clean/deprioritize persistent loops; monitor transient spikes.

### 2B. Error pack (Section A inputs)

Run the Section A signal checks in parallel to precompute error proposals, then
add a silent-failure detector:
- Root insight: rising zero-post share with flat explicit errors indicates silent failures; recommendation: prioritize silent-failure tagging/fixes.

Also run DB error-accumulation checks to cross-reference with trace errors:
- Root insight: top accounts by `fetchingerrorcount` identify persistent loops.
- Root insight: bucketed error-count distribution shows whether issues are broad or concentrated; recommendation: concentrated tails favor targeted cleanup, broad shifts favor global tuning.

Cross-reference: if an account appears in both trace errors and DB
`fetchingerrorcount > 5`, it is a confirmed persistent failure. If it only
appears in traces but has `fetchingerrorcount = 0` in DB, the error may be
recent/transient.

Reuse this pack's outputs in Section A by default; rerun only when narrowing
filters/time range.

### 2C. Coverage/throughput + federation pack (Section B + Section E inputs)

Run the Section B signal set in parallel to precompute coverage/freshness
proposals.

Also run DB coverage queries to provide ground-truth freshness data:

- Root insight: per-service freshness distribution is the true coverage state.
  Recommendation: shift scheduling toward services with the worst stale ratios.
- Root insight: orphan-account volume shows crawl budget that can be reduced.
  Recommendation: lower depth for orphan tails when capacity is constrained.
- Root insight: stalest high-demand accounts expose fairness failures.
  Recommendation: increase priority for heavily followed stale accounts.

Cross-reference DB freshness with Tempo throughput: if Tempo shows healthy span
rates but DB shows many stale accounts, the crawl is cycling through the same
active accounts while neglecting the long tail.

Reuse this pack's outputs in Section B by default; rerun only when narrowing
filters/time range.

Also run priority #2 federation read-query checks (metrics-first):
- Root insight: successful read responses by endpoint family show whether remote
  servers can resolve users/posts reliably.
- Root insight: error mix (`429`, `5xx`, `404`) distinguishes budget saturation,
  internal service faults, and expected content-miss outcomes.
- Root insight: upstream API calls per successful read response measure shared
  budget efficiency.
- Root insight: cache-backed paths should show lower upstream cost per success
  than non-cached paths.
- Recommendation: improve cache-hit behaviour and cheap negative reuse before
  adding upstream fetch pressure.

Treat `404` as its own content-miss signal (not the same as reliability failure).
If read-serving gains raise upstream pressure enough to hurt crawl freshness,
prefer crawl-preserving options.

### 2D. Settings/strategy tuning pack (Section C inputs)

Run Section C4 evidence checks in parallel to precompute tuning proposals.

Also read current strategy policy settings directly from the database:

- Root insight: live policy values in `settings` are authoritative for tuning.
  Recommendation: read current policy first, then propose targeted deltas.

Use the DB policy values as ground-truth when making tuning recommendations.
For example, compare the `nitter.endpoints` list against Tempo error rates per
endpoint, or check `ig_crawling.WebSidecars` against sidecar span success rates.

Reuse this pack's outputs in Section C by default; rerun only when narrowing
filters/time range.

### 2E. Tracing-gap audit pack (Section D inputs)

Re-check all known gaps against current code and current signals during the same
parallel exploration pass. Convert each still-open gap into a proposal candidate
with problem, exact fix, and expected impact.

### 2F. Grafana dashboard audit pack (Section F inputs)

Run dashboard inspection in parallel with other packs.

Inputs:
- Live API dashboards when `GRAFANA_URL` + `GRAFANA_TOKEN` permit read access.
- Otherwise local files `grafana/infra.json` and `grafana/audience.json`.

Checks:
- Root insight: panel title/query/unit/threshold consistency ensures correct
  operator decisions. Recommendation: fix semantic mismatches first.
- Root insight: "success rate" panels must be true ratios with percent units.
  Recommendation: replace raw counts with ratio expressions where needed.
- Root insight: panel coverage must represent priority #1 and #2 decision loops.
  Recommendation: add missing panels for freshness, error-class split, and
  shared-budget efficiency.
- Root insight: dashboard panels are only actionable when their backing
  telemetry already exists in Grafana. Recommendation: separate immediate
  dashboard fixes from follow-up panels that depend on not-yet-live telemetry.
- Root insight: panels without descriptions slow operator review and make query
  intent hard to trust. Recommendation: add concise descriptions to every panel,
  especially stats, ratios, and panels with non-obvious SQL/PromQL semantics.
- Root insight: query and label choices affect cardinality/cost.
  Recommendation: keep dimensions low-cardinality and avoid user-level splits.
- Root insight: dashboard navigation/variables affect triage speed.
  Recommendation: add or clean up variables and row naming for per-instance
  drilldown.

Output requirements for this pack:
- Panel inventory by dashboard row and objective
- Top mismatches/anti-patterns
- Gap list for missing decision signals
- Concrete patch candidates (panel edits/new panels/variable edits)
- Future-run notes for panels that should be added later once newly proposed
  telemetry is confirmed present in Grafana
- Missing-description inventory, prioritizing high-risk panels first

### Optional preflight — Grafana/Loki/Mimir access (when env vars are present)

If `GRAFANA_TOKEN`, `GRAFANA_URL`, `LOKI_URL`, `LOKI_USER`, `MIMIR_URL`,
`MIMIR_USER`, and `MIMIR_TOKEN` are set, run these smoke tests (via Bash)
during Step 2 and
include pass/fail in your opening context:

- Dashboard API auth smoke check endpoint responds successfully.
- Dashboard read for both tracked dashboards succeeds (or explicit fallback to local JSON).
- Optional: permission endpoint confirms dashboard write scope when approved dashboard changes may be deployed.

- Loki auth smoke check endpoint responds successfully.
- Mimir auth smoke check endpoint responds successfully.
- A basic Mimir instant check returns HTTP 200 (empty vector is acceptable).

If available, use Grafana/Loki/Mimir evidence to support Section A-F
conclusions and proposal ranking.

### 2G. Proposal synthesis and yes/no flow

After all parallel packs complete:

1. Present a concise 3-5 line cross-instance summary.
2. Present interesting findings bubbled up from subtask outputs before asking
   any proposal question (no hard cap; prioritize highest-impact first).
3. Build one combined proposal list from all packs.
4. Rank proposals by (a) crawl coverage/freshness impact, (b) federation
   read-query success + upstream-cost efficiency impact, then (c)
   reliability/risk reduction.
5. Offer exactly one proposal at a time: the current top-ranked proposal,
   including supporting evidence and `Suggested tests` when parser/integration
   coverage is relevant, then use the OpenCode native `question` tool with
   explicit yes/no choices.
6. If the answer is `No`, defer that proposal for the current session,
   re-rank the remaining proposals, and offer the next best proposal.
7. If the answer is `Yes`, implement that proposal immediately so the diff
   stays atomic.
8. If the approved proposal is a dashboard change, deploy it automatically in
   the same session when credentials/scope allow; do not ask for a separate
   deployment request.
9. After implementation (and deployment for dashboards when applicable),
   summarize what changed, include a `Suggested tests` block with 1-3 concrete
   follow-up integration/regression tests (or `none` if nothing meaningful
   follows), and ask a review gate for that same proposal using the OpenCode
   native `question` tool with `Done` / `Needs changes`.
10. If the review answer is `Needs changes`, keep working on that same
    proposal, redeploy again for dashboard changes when needed, and repeat the
    review gate until the operator answers `Done`.
11. Only after `Done`, run a targeted refresh of the affected signals,
    re-rank the remaining proposals, and offer the next single best proposal.
12. Keep parent-context retention minimal by carrying forward only the
    remaining ranked proposals, supporting evidence snippets, and current
    proposal review state.

---

## Section A — Error analysis

### Signals to validate

These checks are executed during Step 2B by default; rerun only for focused
drilldowns.

- Root insight: top recurring error messages + affected operations reveal root
  failure classes. Recommendation: fix the dominant class first.
- Root insight: error share by operation shows where crawl capacity is lost.
  Recommendation: prioritize operation-specific mitigation before global changes.

### Known error types and their meaning

| Error message | Span | Meaning | Recommended action |
|---|---|---|---|
| `RateLimitExceededException` | `RetrieveTweetsProcessor`, `Direct.GetUserAsync` | Social network is throttling requests | Increase `SocialNetworkRequestJitter` (e.g. 2000ms) and `TwitterRequestDelay` (e.g. 500ms) |
| `Object reference not set to an instance of an object.` | `RetrieveTweetsProcessor` | Null response from API not guarded — likely unexpected JSON shape | Add null-check at `RetrieveTweetsProcessor.cs:65` around `GetNewPosts()` return value |
| `HTTP Unauthorized: "Please wait a few minutes"` | `Direct.GetUserAsync` | Instagram anti-bot block | Increase jitter; consider rotating proxy |
| `UserNotFoundException` | `RetrieveTweetsProcessor` | Account deleted or suspended on source network | Set `FailingTwitterUserCleanUpThreshold` to auto-remove stale users |
| `HTTP NotFound` returning HTML page | `RetrieveTweetsProcessor` | Instagram 404 — user no longer exists | Same as above: enable `FailingTwitterUserCleanUpThreshold` |
| Proxy `502` from `geo.iproyal.com` | `RetrieveTweetsProcessor` | Outbound proxy is intermittently failing | Check proxy health; consider increasing proxy pool or failover |

### DB error correlation

Cross-reference trace errors with the `fetchingerrorcount` column in
`twitter_users` to distinguish persistent vs transient failures:

- Root insight: accounts with high `fetchingerrorcount` + high staleness are
  confirmed persistent loops, not noise.
- Recommendation: compare those accounts with
  `FailingTwitterUserCleanUpThreshold`; if they persist above threshold,
  investigate cleanup-path execution.

When an account appears in trace errors **and** has `fetchingerrorcount > 5` in
the DB, recommend it for cleanup via `FailingTwitterUserCleanUpThreshold`.
When an account appears in traces but has `fetchingerrorcount = 0`, treat it as a
recent/transient issue and monitor rather than clean up.

### Silent errors (not visible as span errors)

`Graphql2025.GetTimelineAsync` (`src/BirdsiteLive.Twitter/Strategies/Graphql2025.cs:74-78`)
returns an empty list `[]` on 401/403/429 instead of throwing. These show up as
`posts.count = 0` with `status = unset` — they are **not** tagged as errors in Tempo.

To surface them, track each instance's zero-post crawl share over time.
A high zero-post rate that doesn't match the error rate is a sign of silent failures.
Do not ask the user to judge this manually. Compare each instance's zero-post ratio
against the trailing 72h baseline (chunked windows if needed). If an instance rises materially above its own
baseline while error-rate stays low, flag likely silent failures and suggest adding
error tagging in `Graphql2025.cs:74-78`.

When production evidence reveals a pathological parsing case, malformed/null
payload shape, unexpected HTML/JSON structure, or silent zero-post outcome,
include concrete regression-test suggestions in the same style as the existing
social-network suites. Good suggestions assert exact parsed semantics from a
real stable exemplar rather than a generic smoke check:
- Twitter: author identity, message content, reply/thread/quote linkage, poll
  extraction, media counts/types/URLs, alt text, short-link expansion, and
  timeline ordering across relevant strategies.
- HackerNews: post kind, author, createdAt, reply linkage, poll structure,
  frontpage normalization, and new-post filtering.
- Instagram: profile fields, pinned posts, recent-post ordering, caption
  extraction, and media counts/types.

---

## Section B — Coverage and throughput analysis

Coverage is the primary objective. Use crawl-latency analysis only when it helps
explain coverage loss (for example, high timeout rates or very long retries).

### Signals to validate

These checks are executed during Step 2C by default; rerun only for focused
drilldowns.

- Root insight: high-percentile duration by operation shows where latency is
  coverage-relevant.
- Root insight: slowest crawl exemplars (acct, duration, post count, VIP flag)
  separate naturally heavy users from pathological delays.
- Root insight: VIP vs non-VIP latency gap validates whether priority handling
  is delivering expected freshness.

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

### DB coverage ground truth

Tempo span rates approximate throughput but cannot show accounts that are
**never reached** by the crawl. Use the DB to find coverage gaps:

- Root insight: recently-synced percentages per service show real freshness.
- Root insight: stalest followed accounts expose demand-weighted coverage gaps.
- Root insight: stalest VIP (`r.town`) accounts show priority misses.
- Root insight: orphan-account count reveals low-value crawl budget.
- Recommendation: prioritize stale followed/VIP users and deprioritize orphan tails when capacity is constrained.

When DB freshness shows a service has many stale accounts but Tempo throughput
looks healthy, the crawl may be cycling through the same active accounts. This
is a scheduling fairness issue — recommend tuning the scheduler SQL (Section C2)
to improve long-tail coverage.

---

## Section E — Federation read-query success and shared-budget efficiency

This section covers remote-server reads for user/post documents (for example
`/.well-known/webfinger`, `/users/{id}`, `/users/{id}/statuses/{statusId}`, and
related ActivityPub read documents). Focus on success rate and upstream-cost
efficiency, not HTTP latency.

### Signals to validate (metrics-first)

- Root insight: per-instance success rate by endpoint family (webfinger, actor,
  status/activity docs) shows read-serving quality.
- Root insight: error mix by class (`429`, `5xx`, `404`) separates saturation,
  service faults, and expected content-miss outcomes.
- Root insight: upstream API call pressure per successful read response shows
  shared-budget cost.
- Recommendation: favour changes that improve read success while reducing
  upstream calls per successful response.

### Cache-shape context by service

- Instagram read paths can serve users/posts from DB cache before upstream fetch
  (`instagram_users.cache`, `cached_insta_posts`).
- Twitter user reads can use DB cache (`twitter_users.cache`), while post reads
  rely mainly on in-memory cache plus upstream fetch path.
- HackerNews reads rely on in-memory cache and upstream fetches (no DB post
  cache table in active read path).

Shared upstream bucket rule:
- Upstream fetches used to answer read queries consume the same external
  rate-limit bucket as crawling.
- If tradeoffs appear, preserve crawl coverage/freshness first.

---

## Section F — Grafana dashboard audit and improvement proposals

This section ensures dashboard quality matches operator priorities and avoids
misleading decisions.

Dashboard sources:
- Preferred: live API dashboards (`GRAFANA_URL` + `GRAFANA_TOKEN` with dashboard read).
- Fallback: `grafana/infra.json` and `grafana/audience.json` in repo.

### Signals to validate

- Root insight: panel semantics are correct only when title/query/unit/
  thresholds agree.
- Root insight: ratio panels are actionable only when based on explicit
  numerator/denominator formulas.
- Root insight: decision dashboards must cover both priorities:
  crawl coverage/freshness and federation read success/cost.
- Root insight: dashboard variables should support per-instance/service/
  strategy drilldown.
- Root insight: panel descriptions are part of dashboard correctness because
  operators need to understand panel meaning quickly. Recommendation: require
  descriptions for all panels and expand weak descriptions when query intent is
  not obvious from the title alone.
- Recommendation: prioritize fixes that reduce operator decision risk first,
  then cosmetic consistency.

### Anti-patterns to detect

- Panel title says "success rate" but query is a raw `increase(...)` count.
- Panel title/network label does not match query filter (for example title says
  one network but query filters another).
- Percent-like panels missing percent units or using incompatible thresholds.
- Duplicate/near-duplicate panels that create confusion without adding new
  decision signal.
- High-cardinality query dimensions in dashboard panels that inflate metric cost.
- Panels missing descriptions, or descriptions that do not explain query
  semantics/caveats.

### Proposal format for dashboard improvements

For each dashboard proposal include:
- dashboard + panel reference
- current issue and why it is decision-risky
- exact query/unit/threshold/variable change
- exact description text change when adding or improving panel explanations
- expected impact on priority #1 and/or #2
- cardinality/cost risk note (if any)
- whether the required telemetry already exists in Grafana now, or must be
  tracked as a future-run dashboard follow-up after instrumentation ships

### Deployment workflow for approved dashboard changes

When the currently approved proposal is a dashboard change, deploy it
automatically when API write scope is available so the operator can review the
live dashboard before moving to the next proposal:
1. Confirm dashboard API read access and write scope.
2. Fetch latest live dashboard objects first, and fetch the legacy dashboard
   model via `GET /api/dashboards/uid/:name` when preparing a live write.
3. Confirm every new/changed panel query uses telemetry already available in
   Grafana. If a proposed panel depends on telemetry that is not live yet, do
   not deploy that panel now; add a future-run note instead.
4. Add or update panel descriptions for every panel touched in the deployment,
   and prefer filling obvious missing descriptions in the same row while the
   dashboard is already being edited.
5. Apply only the currently approved dashboard changes to the complete
   dashboard model.
6. Deploy with `POST /api/dashboards/db` using `folderUid`, `overwrite: true`,
   and a commit message.
7. Re-GET the dashboard via `GET /api/dashboards/uid/:name` and verify panel
   count / title / version changed as expected.
8. Re-GET the `dashboard.grafana.app` object and keep repo `grafana/*.json`
   synchronized with the deployed version.
9. If a deployment accidentally produces an empty dashboard, immediately restore
   the prior working version from `GET /api/dashboards/uid/:name/versions/:n`
   using `POST /api/dashboards/db`.
10. If deployment is blocked by auth/scope/API failure, keep the local JSON
    updated and state clearly that live Grafana was not updated.
11. After deployment or local-only fallback, show what changed and wait for the
    operator's `Done` / `Needs changes` review gate before moving to the next
    proposal.

---

## Section C — Settings, scheduler, and strategy tuning

Tuning is not just `InstanceSettings`. In practice, crawl behaviour is shaped by
three layers:
1. `InstanceSettings` env vars (`k8s/*.yaml`)
2. Per-service scheduler SQL (`GetNextUsersToCrawlAsync` in DALs)
3. Strategy policy settings from DB (`nitter`, `ig_crawling`, `twitteraccounts`)

### C1. InstanceSettings knobs (env vars)

Core model: `src/BirdsiteLive.Common/Settings/InstanceSettings.cs`

| Setting | Default | Signal to look for | Recommendation |
|---|---|---|---|
| `ParallelTwitterRequests` | 10 | Sustained `RateLimitExceededException` / auth-throttle errors while crawls overlap heavily | For Direct-heavy instances start at 1-2; increase only if rate limits stay low |
| `SocialNetworkRequestJitter` | 0 | Any rate limit errors | Set to 1000-3000ms to randomise request spacing |
| `TwitterRequestDelay` | 0 | Tight burst pattern visible in span timestamps | Set to 500-1000ms between batches |
| `FailingTwitterUserCleanUpThreshold` | unset (0) | `UserNotFoundException` or 404 errors recurring for same accounts | Set to e.g. 5 to auto-remove dead accounts after 5 consecutive failures |
| `UserCacheCapacity` | 40,000 | No trace signal — monitor memory | Leave unless memory pressure observed |
| `TweetCacheCapacity` | 20,000 | No trace signal | Leave unless memory pressure observed |
| `PostCacheRetentionDays` | 28 | No trace signal | Fine for current usage |
| `PipelineStartupDelay` | 15 min | Slow-start after pod restart visible as gap in traces | Reduce to 5 min if restarts are frequent |

### C2. Per-service scheduler SQL knobs (high impact, currently hardcoded)

These are often the largest real-world tuning levers because they decide *which*
accounts get crawled next.

| Service | File / method | Current behaviour | Tuning direction |
|---|---|---|---|
| Twitter | `TwitterUserPostgresDal.GetNextUsersToCrawlAsync` | `maxNumber=2000`, shard gate (`n_start/n_end/m`), and recency filter with Monday exception | Externalize max batch size + recency horizon as settings; tune for fairness vs freshness |
| Instagram | `InstagramUserPostgresDal.GetNextUsersToCrawlAsync` | VIP/wikidata/day-of-week predicates, `LIMIT 20` | Externalize limit and predicate thresholds; tune VIP share vs broad coverage |
| HackerNews | `HackerNewsUserPostgresDal.GetNextUsersToCrawlAsync` | `frontpage` forced priority, `LIMIT 20` | Externalize limit/frontpage weight if backlog grows |

### C3. Strategy policy + endpoint quality settings (DB-backed)

These settings are stored in the `settings` table and can be read directly via
the PostgreSQL MCP. **Always read current values before making recommendations.**

Known policy structure:

| Policy key | Known JSONB fields | Used by | Why it matters |
|---|---|---|---|
| `nitter` | `endpoints` (array), `lowtrustendpoints` (array), `postnitterdelay` (int ms), `followersThreshold` / `followersThreshold0` / `followersThreshold2` / `followersThreshold3` (int), `twitterFollowersThreshold` (int) | `TwitterTweetsService`, `Nitter` | Controls endpoint pool, pacing, and follower-count thresholds for strategy selection. Compare per-endpoint success in traces; remove endpoints with persistent failures. |
| `ig_crawling` | `WebSidecars` (array of `host:port`), `WebSidecars2` (array), `non_vip_threshold` (int) | Instagram `Sidecar.GetWebSidecar` | Controls sidecar endpoint rotation. Track sidecar span success rates; demote endpoints with persistent 5xx. `non_vip_threshold` controls how aggressively non-VIP accounts are crawled. |
| `twitteraccounts` | (not readable — table `twitter_crawling_users` is inaccessible) | `TwitterAuthenticationInitializer` | Affects credential/token refresh behaviour and fallback quality. Monitor auth/throttle errors and token refresh churn via Tempo; recommend rotate/replenish when churn is high. |

### C4. Evidence checks (used by Step 2D)

These checks are executed during Step 2D by default; rerun only for focused
drilldowns.

- Root insight: extractor success/failure split for Twitter and Instagram shows
  which path is failing by instance.
- Recommendation: demote/cooldown failing extractor paths and preserve healthy ones.

When Mimir is available, also run:

- Root insight: per-instance API throughput and error trends validate whether a
  tuning change improved coverage.
- Root insight: token-refresh churn, nitter source mix, and sidecar domain success show strategy-level quality.

When giving recommendations, combine trace symptoms (errors/crawl duration) with DB
policy values and (when available) Mimir metrics so advice is strategy-specific,
not only global env-var changes.

---

## Section D — Tracing gaps (audit and propose)

Do not treat this section as a static backlog. At the start of each investigation:

1. Re-check every previously known gap against current code.
2. If a gap is fixed, remove it from active gap tracking.
3. If a gap is still open, keep a single current "Open tracing gaps" list in
   this section (problem + exact fix) and avoid long duplicate backlogs.

When an open gap is found, describe the problem and exact fix, then convert it
into a proposal candidate. Rank it with all other proposals from Step 2; if it
becomes the current top-ranked proposal, ask it as its own yes/no question via
the OpenCode native `question` tool and keep it in the same
approve-implement-review loop as other proposals.

---

## Observability budget

- **Traces:** ~500MB/month today vs 50GB/month limit (~1% used).
- **Logs:** 50GB/month limit, tracked separately from traces.
- **Metrics:** 10k metric-series limit.

### Pre-change impact check (required)

Before proposing or implementing any change that adds/removes observability
(new spans, new log fields, new metrics, or label changes), run a quick impact
baseline first:

- Metrics usage (direct): count all active series via the Mimir API.
- Logs usage (direct for time window): Loki `index/volume_range` over a recent
  window (default 72h), then project monthly roughly.
- Traces usage: no single direct usage API in current tooling; estimate from
  span rate and expected span cardinality/size change.
- Dashboard-only visual/layout edits do not add telemetry cost by themselves.
  Query, label, and metric-definition changes can increase logs/metrics usage.

Include that estimate before making the change and call out risk against:
- traces 50GB/month
- logs 50GB/month
- metrics 10k series

Instrumenting the currently open tracing gaps would still increase span volume by
only a few multiples, which remains well below 1GB/month and far below the traces
budget. It is safe to add crawl-pipeline tracing. For logs/metrics changes, prefer
low-cardinality dimensions (`strategy`, `instance`, `result`, `error_type`) and avoid
high-cardinality labels (e.g., raw account names) on metrics.

## Updating this skill

This skill is a living document. After every investigation session, propose
changes in **both directions** — adding new knowledge and removing stale content.
