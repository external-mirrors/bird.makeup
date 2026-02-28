# Special consideration for Nitter

There is a special case to handle when crawling through Nitter. We are running multiple raspberry pies with Nitter installed. Those are trusted and we can fully rely on their results. 

We keep open the posibility of using remote Nitter instances provided by the community. Those are "low trust". We can only rely on them for the mapping of accounts -> most recent tweets' IDs. If we verify the tweets from their ID, we can get the full content trustlessly. We cannot rely on them for retweets. Any strategy could be used in this case to fetch the tweet and confirm they are actually from the account. 

When using community Nitter instances, we must be as kind as possible. Minimizing the number of requests, using heuristic to figure out if they are actually new posts before making the requests and trying to hit their cache as much as possible.

This still means the Nitter strategy must work fully on its own in the normal case.