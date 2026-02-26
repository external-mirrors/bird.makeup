# Social networks tests instructions

Tests content are the ground truth of what is actually happening in a social network. They allow testing multiple strategies with the same data. 

Do not modify test files unless the user explicitly approves a targeted test-policy change. Fix upstream code, not assertions. If a test change is explicitly approved, keep it minimal and scoped: skip only the unavailable assertion for the affected implementation(s), and avoid marking the whole test inconclusive.

Keep implementations independent. Do not backfill one implementation/strategy from another. 

Do not add status-id/domain/account special-cases just to satisfy tests. Do not use fallback extractors as a "cheat" path for other implementations.

Timeline tests are mostly from dead users. This garantees that they won't post again and thus keep our tests valid in the long term.
