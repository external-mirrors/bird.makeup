# Twitter Tests Agent Notes

## User instructions (persistent)
- Scope: focus on Nitter behavior first when asked to make Twitter tests pass.
- Do not modify test files.
- Fix upstream code, not assertions.
- Do not add status-id/domain/account special-cases just to satisfy tests.
- Do not use fallback extractors as a "cheat" path for Nitter or other implementations.

## Fast workflow for Nitter runs
- Run only Nitter tests:
  - `dotnet test Tests/BirdsiteLive.Twitter.Tests/BirdsiteLive.Twitter.Tests.csproj --filter "Name~Nitter" -v q --logger "trx;LogFileName=nitter_run.trx"`
- List available Nitter tests quickly:
  - `dotnet test Tests/BirdsiteLive.Twitter.Tests/BirdsiteLive.Twitter.Tests.csproj --list-tests --filter "Name~Nitter" -v q`
- Extract failures from TRX:
  - Parse `Tests/BirdsiteLive.Twitter.Tests/TestResults/*.trx` and print failed test names + messages.

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

