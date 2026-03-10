# Special consideration for Graphql202X's strategy

This is the OG strategy, hitting twitter Graphql endpoints directly. The URL get rotated once in a while, but we keep them around as long as they work with the year in the name. We keep them in different strategy files since the answer structure changes when new graphql endpoints comes out.

The timeline endpoint upstream was modified to return only the top tweets per number of likes. This is way less useful in terms of coverage, but makes for a nice fallback for lower priority accounts (when we filter by date, we can still potentially get new posts if they are very popular). This strategy still works great for post and user lookups.

# Special consideration for Nitter' strategy

There is a special case to handle when crawling through Nitter. We are running multiple raspberry pies with Nitter installed. Those are trusted and we can fully rely on their results. 

We keep open the posibility of using remote Nitter instances provided by the community. Those are "low trust". We can only rely on them for the mapping of accounts -> candidate recent tweet IDs. We must not trust any parsed tweet object fields from low-trust endpoints (author, text, media, reply/quote/retweet metadata, counts, dates, etc.). The full tweet object must always be extracted from a trusted strategy/source using the discovered ID, and only that trusted extraction should be published/used. In this model, the worst thing a low-trust endpoint can do is censor by omitting IDs.

When using community Nitter instances, we must be as kind as possible. Minimizing the number of requests, using heuristic to figure out if they are actually new posts before making the requests and trying to hit their cache as much as possible.

This still means the Nitter strategy must work fully on its own in the normal case.
