# Agent Instructions

For Twitter test work, always load and follow:
- `Tests/BirdsiteLive.Twitter.Tests/AGENTS.md`

This repository is a fork of BirdsiteLive, renamed to dotMakeup because it now supports more than Twitter.
As components are added or refactored, progressively move naming and structure toward the new dotMakeup names.

In this codebase, a "strategy" is the concrete crawl/fetch mechanism (for example `Graphql2025`, `Sidecar`, `Direct`). Each social network should have its own independant strategies. Different strategies can be used in different situation to face rate limits and data completeness issues. Keep strategies' implementations independent. Do not backfill one implementation from another (e.g., Nitter from Syndication/GraphQL, GraphQL from Nitter, etc.). Do not use fallback extractors from other implementations. All social networks should have their own "Strategies" directory. Strategies should be part of tracing/metrics/logs to gage performance and reliability. 
