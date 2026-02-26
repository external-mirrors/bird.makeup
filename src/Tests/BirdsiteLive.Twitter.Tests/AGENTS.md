# Twitter Tests Agent Notes

## User instructions (persistent)
- Scope: focus on Nitter behavior first when asked to make Twitter tests pass.

## Fast workflow for Nitter runs
- Run only Nitter tests:
  - `dotnet test Tests/BirdsiteLive.Twitter.Tests/BirdsiteLive.Twitter.Tests.csproj --filter "Name~Nitter" -v q --logger "trx;LogFileName=nitter_run.trx"`
- List available Nitter tests quickly:
  - `dotnet test Tests/BirdsiteLive.Twitter.Tests/BirdsiteLive.Twitter.Tests.csproj --list-tests --filter "Name~Nitter" -v q`
- Extract failures from TRX:
  - Parse `Tests/BirdsiteLive.Twitter.Tests/TestResults/*.trx` and print failed test names + messages.

## Field Availability Verification
- Before changing parser logic for a failing assertion, verify that the required field is actually present in that implementation's upstream payload/HTML.
- For Nitter status pages, check configured endpoints directly (e.g., `marci`, `medusa`) before coding:
  - `for d in marci medusa; do curl -sS "http://$d:8080/i/status/<id>" > /tmp/nitter_$d.html; rg -n "twitter:image:alt|og:image:alt|<img[^>]*alt=\"[^\"]+\"" /tmp/nitter_$d.html; done`
- For API-based implementations, inspect raw JSON responses and confirm the field/path exists before adding extraction logic.
- If the field is absent upstream across configured sources, ask for user approval before applying a minimal test-policy exception for the affected implementation(s).

## Nitter parser learnings
- Preserve timeline order from page traversal for timeline assertions; sorting by `CreatedAt` can break `TimelineGrant`.
- In timelines, skip non-author contextual thread tweets unless item has `.retweet-header`.
- For retweets in Nitter timeline, the retweeter lookup can fail; use a non-throwing fallback author object so parsing continues.
- On single-status pages, main tweet can appear under:
  - `.main-tweet .timeline-item`
  - `.main-tweet.timeline-item`
  - `.main-tweet`
- Video media may be rendered as card/video containers without `<video>` source tags:
  - detect `.video-container`, `.gallery-video`, `.video-overlay`
  - synthesize a `video/mp4` URL when needed.
- Reply context may be missing explicit header; infer from adjacent timeline items in thread views.
- Some Nitter templates do not expose media alt text for certain tweets even when alt text exists on X; verify availability in Nitter HTML before attempting extraction.
