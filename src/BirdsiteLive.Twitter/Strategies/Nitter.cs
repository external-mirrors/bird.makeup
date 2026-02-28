#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8613, CS8618, CS8619, CS8620, CS8621, CS8625, CS8629, CS8631, CS8634
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Io;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Twitter.Models;
using BirdsiteLive.Twitter.Tools;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Twitter.Strategies;

public class Nitter : ITimelineExtractor, IUserExtractor, ITweetExtractor
{
    private readonly ISettingsDal _settings;
    private readonly ILogger<TwitterService> _logger;
    private readonly IUserExtractor _userExtractor;
    private readonly ITweetExtractor _trustedTweetVerifier;
    private readonly ITwitterUserDal _twitterUserDal;
    private string Useragent = "Bird.makeup ( https://git.sr.ht/~cloutier/bird.makeup ) Bot";
    static Meter _meter = new("DotMakeup", "1.0.0");
    static Counter<int> _nCalled = _meter.CreateCounter<int>("dotmakeup_nitter_called_count");

    public Nitter(IUserExtractor userExtractor, ITweetExtractor trustedTweetVerifier, ISettingsDal settingsDal, ITwitterUserDal twitterUserDal,
        ILogger<TwitterService> logger)
    {
        _settings = settingsDal;
        _logger = logger;
        _userExtractor = userExtractor;
        _trustedTweetVerifier = trustedTweetVerifier;
        _twitterUserDal = twitterUserDal;

        var requester = new DefaultHttpRequester();
        requester.Headers["User-Agent"] = Useragent;
        requester.Headers["Accept"] =
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8";
        requester.Headers["Accept-Encoding"] = "gzip, deflate";
        requester.Headers["Accept-Language"] = "en-US,en;q=0.5";
        // var config = Configuration.Default.With(requester).WithDefaultLoader();
        // _context = BrowsingContext.New(config);
    }

    private async Task<(string, bool)> GetDomain(bool onlyHighTrust = false)
    {

        // https://status.d420.de/
        var nitterSettings = await _settings.Get("nitter");
        if (nitterSettings is null)
            throw new Exception("Nitter settings not found");


        List<(string, bool)> domains = new List<(string, bool)>();

        if (!onlyHighTrust)
        {
            foreach (var d in nitterSettings.Value.GetProperty("lowtrustendpoints").EnumerateArray())
            {
                domains.Add((d.GetString(), true));
            }
        }

        foreach (var d in nitterSettings.Value.GetProperty("endpoints").EnumerateArray())
        {
            domains.Add((d.GetString(), false));
        }

        Random rnd = new Random();
        int randIndex = rnd.Next(domains.Count);
        return domains[randIndex];
    }

    private async Task<IDocument> GetDocument(string address)
    {
        var requester = new DefaultHttpRequester();

        requester.Headers["User-Agent"] = Useragent;
        requester.Headers["Accept"] =
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8";
        requester.Headers["Accept-Encoding"] = "gzip, deflate";
        requester.Headers["Accept-Language"] = "en-US,en;q=0.5";
        var config = Configuration.Default.With(requester).WithDefaultLoader();
        var context = BrowsingContext.New(config);

        _logger.LogInformation($"Nitter: fetching {address}");
        var document = await context.OpenAsync(address);

        return document;
    }

    public async Task<List<ExtractedTweet>> GetTimelineAsync(SyncUser user, long userId, long fromTweetId, bool withReplies)
    {
        (var domain, bool lowtrust) = await GetDomain();

        string address =
            $"{(lowtrust ? "https" : "http")}://{domain}{(lowtrust ? "" : ":8080")}/{user.Acct}{(withReplies ? "/with_replies" : "")}";

        var document = await GetDocument(address);

        try
        {
            if (!lowtrust)
            {
                _logger.LogInformation("Nitter: updating user cache for {Username}", user.Acct);
                var u = ExtractUser(document, user.Acct);
                await _twitterUserDal.UpdateUserCacheAsync(u);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error updating user cache for {Username} from Nitter", user.Acct);
        }
        List<ExtractedTweet> tweets = new List<ExtractedTweet>();
        string pattern = @".*\/([0-9]+)(?:#m)?$";
        Regex rg = new Regex(pattern);
        var seenStatusIds = new HashSet<long>();

        // Iterate timeline items directly to preserve context (e.g., replies) without refetching
        var items = document.QuerySelectorAll(".timeline-item");
        foreach (var item in items)
        {
            var href = item.QuerySelector(".tweet-link")?.GetAttribute("href") ?? item.QuerySelector(".tweet-date a")?.GetAttribute("href");
            if (href == null) continue;
            var idMatch = rg.Match(href);
            if (!idMatch.Success) continue;
            var match = long.Parse(idMatch.Groups[1].Value);

            if (match <= fromTweetId)
                continue;
            if (!seenStatusIds.Add(match))
                continue;

            try
            {
                if (lowtrust)
                {
                    // Low-trust instances are only used as account -> status ID discovery.
                    // Tweet payload must be fetched from a trusted extractor and author-verified.
                    var verifiedTweet = await _trustedTweetVerifier.GetTweetAsync(match);
                    if (verifiedTweet?.Author?.Acct == null)
                    {
                        _logger.LogDebug("Nitter: low-trust verification returned empty tweet for {StatusId}", match);
                        continue;
                    }
                    if (verifiedTweet.IdLong != match)
                    {
                        _logger.LogWarning(
                            "Nitter: low-trust endpoint {Domain} suggested status {StatusId}, but trusted extractor returned {TrustedStatusId}; skipping",
                            domain, match, verifiedTweet.IdLong);
                        continue;
                    }

                    if (!string.Equals(verifiedTweet.Author.Acct, user.Acct, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "Nitter: low-trust endpoint {Domain} suggested status {StatusId} for {Username}, but verified author is {VerifiedAuthor}; skipping",
                            domain, match, user.Acct, verifiedTweet.Author.Acct);
                        continue;
                    }

                    tweets.Add(verifiedTweet);
                    await Task.Delay(50);
                    continue;
                }

                // Parse tweet from the timeline item element (keeps reply/quote context)
                var tweet = ParseTweetFromElement(item);
                if (tweet == null) continue;

                var sameAuthor = string.Equals(tweet.Author.Acct, user.Acct, StringComparison.OrdinalIgnoreCase);
                var isRetweetHeader = item.QuerySelector(".retweet-header") != null;

                // Skip contextual thread tweets from other authors. Keep explicit retweets.
                if (!sameAuthor && !isRetweetHeader)
                    continue;

                // Mark retweets when parsing from main user's timeline (non-lowtrust endpoint)
                if (!sameAuthor && !lowtrust)
                {
                    tweet.IsRetweet = true;
                    tweet.OriginalAuthor = tweet.Author;
                    TwitterUser? retweeter = null;
                    try
                    {
                        retweeter = await _userExtractor.GetUserAsync(user.Acct!);
                    }
                    catch (Exception e)
                    {
                        _logger.LogDebug(e, "Nitter: failed to resolve retweeter profile for {Username}", user.Acct);
                    }
                    tweet.Author = retweeter ?? new TwitterUser
                    {
                        Acct = user.Acct?.ToLowerInvariant(),
                        Name = user.Acct,
                        ProfileUrl = "https://twitter.com/" + user.Acct?.ToLowerInvariant(),
                        Id = 0
                    };
                    tweet.RetweetId = tweet.IdLong;
                    // Nitter timeline doesn't give retweet's new ID, generate one
                    var gen = new TwitterSnowflakeGenerator(1, 1);
                    tweet.Id = gen.NextId().ToString();
                }
                else if (sameAuthor && !tweet.IsReply && item.ClassList.Contains("thread-last"))
                {
                    // Infer reply context for thread-last entries that omit "replying-to".
                    var previousItem = item.PreviousElementSibling;
                    while (previousItem != null && !previousItem.ClassList.Contains("timeline-item"))
                        previousItem = previousItem.PreviousElementSibling;
                    if (previousItem != null)
                    {
                        var parentTweet = ParseTweetFromElement(previousItem);
                        if (parentTweet?.Author?.Acct != null &&
                            !string.Equals(parentTweet.Author.Acct, tweet.Author.Acct, StringComparison.OrdinalIgnoreCase))
                        {
                            tweet.InReplyToAccount = parentTweet.Author.Acct;
                            tweet.InReplyToStatusId = parentTweet.IdLong;
                            tweet.IsReply = true;
                            tweet.IsThread = false;
                        }
                    }
                }

                tweets.Add(tweet);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Nitter: error parsing timeline tweet {match} from user {user.Acct}");
            }

            await Task.Delay(50);
        }

        _nCalled.Add(1,
            new KeyValuePair<string, object>("source", domain),
            new KeyValuePair<string, object>("success", tweets.Count > 0)
        );

        return tweets;
    }

    public async Task<ExtractedTweet?> GetTweetAsync(long statusId)
    {
        (var domain, bool lowtrust) = await GetDomain(true);

        string address = $"{(lowtrust ? "https" : "http")}://{domain}{(lowtrust ? "" : ":8080")}/i/status/{statusId}";

        var document = await GetDocument(address);
        var errorPanel = document.QuerySelector(".error-panel");
        if (errorPanel != null)
        {
            _logger.LogWarning("Nitter: status {StatusId} unavailable at {Url}: {Error}", statusId, address, errorPanel.TextContent?.Trim());
            return null;
        }
        
        var mainTweetElement = document.QuerySelector(".main-tweet .timeline-item")
                               ?? document.QuerySelector(".main-tweet.timeline-item")
                               ?? document.QuerySelector(".main-tweet");
        _logger.LogInformation("Nitter: mainTweetElement={Found}, url={Url}", mainTweetElement != null, address);
        if (mainTweetElement != null)
        {
            var tweet = ParseTweetFromElement(mainTweetElement);
            _logger.LogInformation("Nitter: tweet={Found}, InReplyToAccount={Account}, InReplyToStatusId={StatusId}", tweet != null, tweet?.InReplyToAccount, tweet?.InReplyToStatusId);
	            if (tweet != null)
	            {
	                // Ensure ID is set correctly if it wasn't parsed from the element.
	                if (string.IsNullOrEmpty(tweet.Id) || tweet.Id == "0") tweet.Id = statusId.ToString();

                // Check for a preceding tweet in the timeline to detect threads/replies missing the header
                var timeline = document.QuerySelector(".timeline") ?? document.QuerySelector(".conversation") ?? document.QuerySelector(".main-thread");

                // Also look for a direct reply header in the main tweet area
                if (string.IsNullOrWhiteSpace(tweet.InReplyToAccount))
                {
                    var headerReplyLink = document.QuerySelector(".main-tweet .replying-to a");
                    var headerReplyText = headerReplyLink?.TextContent?.TrimStart('@');
                    var headerReplyHref = headerReplyLink?.GetAttribute("href");
                    if (string.IsNullOrWhiteSpace(headerReplyText))
                    {
                        var replyToEl = document.QuerySelector(".main-tweet .replying-to");
                        if (replyToEl != null)
                        {
                            var match = Regex.Match(replyToEl.TextContent, @"@([a-zA-Z0-9_]+)");
                            if (match.Success)
                            {
                                headerReplyText = match.Groups[1].Value;
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(headerReplyText))
                    {
                        tweet.InReplyToAccount = headerReplyText;
                        tweet.IsReply = true;
                        tweet.IsThread = string.Equals(tweet.Author.Acct, tweet.InReplyToAccount, StringComparison.OrdinalIgnoreCase);
                        
                        if (headerReplyHref != null && tweet.InReplyToStatusId == null)
                        {
                            var idMatch = Regex.Match(headerReplyHref, @"/([0-9]+)(?:#m)?$");
                            if (idMatch.Success)
                                tweet.InReplyToStatusId = long.Parse(idMatch.Groups[1].Value);
                        }
                    }
                }

                if (timeline != null)
                {
                    var allItems = timeline.QuerySelectorAll(".timeline-item");
                    var mainIndex = -1;
                    for (int i = 0; i < allItems.Length; i++)
                    {
                        if (allItems[i] == mainTweetElement || allItems[i].Contains(mainTweetElement))
                        {
                            mainIndex = i;
                            break;
                        }
                    }

                    if (mainIndex > 0)
                    {
                        var previousItem = allItems[mainIndex - 1];
                        // In Nitter, thread items are connected by thread-line or are children of a thread container
                        var isPrecededByThreadLine = previousItem.QuerySelector(".thread-line") != null || 
                                                     mainTweetElement.QuerySelector(".thread-line") != null;
                        
                        // Also check if the previous item is simply the one being replied to
                        // (often true in Nitter's single-tweet view)
                        if (isPrecededByThreadLine || string.IsNullOrEmpty(tweet.InReplyToAccount) || tweet.InReplyToStatusId == null)
                        {
                            var previousTweet = ParseTweetFromElement(previousItem);
                            if (previousTweet != null)
                            {
                                // If we didn't have an InReplyToAccount from Nitter's header, 
                                // we keep it but try to get the StatusId.
                                // If we didn't have it, we take both from the previous tweet.
                                if (string.IsNullOrEmpty(tweet.InReplyToAccount))
                                {
                                    var previousAcct = ExtractAccountWithCasing(previousItem) ?? previousTweet.Author.Acct;
                                    tweet.InReplyToAccount = previousAcct;
                                    tweet.InReplyToStatusId = long.Parse(previousTweet.Id);
                                    tweet.IsReply = true;
                                    tweet.IsThread = string.Equals(tweet.Author.Acct, previousAcct, StringComparison.OrdinalIgnoreCase);
                                }
                                else
                                {
                                    var previousAcct = ExtractAccountWithCasing(previousItem) ?? previousTweet.Author.Acct;
                                    if (!string.IsNullOrWhiteSpace(previousAcct) &&
                                        string.Equals(tweet.InReplyToAccount, previousAcct, StringComparison.OrdinalIgnoreCase))
                                    {
                                        tweet.InReplyToStatusId = long.Parse(previousTweet.Id);
                                    }
                                }
                            }
	                        }
	                    }
	                }

	                return tweet;
	            }
	        }

        return null;
    }

    private ExtractedTweet ParseTweetFromElement(IElement item)
    {
        var link = item.QuerySelector(".tweet-link")?.GetAttribute("href");
        if (link == null)
        {
            // Fallback for single tweet view which lacks .tweet-link on the main tweet item
            // It usually has a date link that contains the status ID
            link = item.QuerySelector(".tweet-date a")?.GetAttribute("href");
        }
        if (link == null) return null;

        string pattern = @".*\/([0-9]+)(?:#m)?$";
        Regex rg = new Regex(pattern);
        var matchId = rg.Match(link);
        if (!matchId.Success) return null;
        var id = matchId.Groups[1].Value;

        var contentElement = item.QuerySelector(".tweet-content");
        string? content = null;
        if (contentElement != null)
        {
            // Nitter puts links in <a> tags, sometimes truncated in TextContent.
            // We want to replace <a> tags with their title (which often contains the full URL) or href.
            foreach (var linkElement in contentElement.QuerySelectorAll("a"))
            {
                var fullUrl = linkElement.GetAttribute("title");
                if (string.IsNullOrEmpty(fullUrl) || !fullUrl.StartsWith("http"))
                {
                    fullUrl = linkElement.GetAttribute("href");
                    if (fullUrl != null && fullUrl.StartsWith("/"))
                    {
                        // Internal link, maybe hashtag or mention. 
                        // We'll leave it as is for now or handle it if needed.
                    }
                }
                
                if (!string.IsNullOrEmpty(fullUrl) && fullUrl.StartsWith("http"))
                {
                    if (TryConvertNitterStatusUrl(fullUrl, out var converted))
                    {
                        fullUrl = converted;
                    }
                    linkElement.TextContent = fullUrl;
                }
            }

            content = contentElement.TextContent;
            
            if (content != null)
            {
                content = content.Trim();
                if (content.StartsWith(".@"))
                {
                    content = content.Substring(1);
                }
            }

            // Heuristic: if there's no explicit reply header, detect leading mention as a reply target
            if (content != null && string.IsNullOrEmpty(item.QuerySelector(".replying-to")?.TextContent))
            {
                // Find the first meaningful child element and see if it's a mention
                IElement? firstEl = null;
                // Prefer actual child elements over text nodes
                if (contentElement.Children.Length > 0)
                {
                    firstEl = contentElement.Children[0] as IElement;
                    // Skip empty elements
                    if (firstEl != null && string.IsNullOrWhiteSpace(firstEl.TextContent))
                    {
                        firstEl = contentElement.Children.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.TextContent));
                    }
                }
                else
                {
                    var node = contentElement.ChildNodes.FirstOrDefault();
                    if (node is IElement el)
                        firstEl = el;
                }

                if (firstEl != null)
                {
                    var firstText = firstEl.TextContent?.Trim();
                    var firstClass = firstEl.GetAttribute("class") ?? "";
                    if (!string.IsNullOrEmpty(firstText) && firstText.StartsWith("@"))
                    {
                        // Prefer elements that look like mentions
                        if (firstEl.TagName.Equals("A", StringComparison.OrdinalIgnoreCase) || firstClass.Contains("username"))
                        {
                            var handle = firstText.TrimStart('@');
                            if (!string.IsNullOrWhiteSpace(handle))
                            {
                                // Assign tentatively; will be normalized below
                                item.SetAttribute("data-leading-mention", handle);
                            }
                        }
                    }
                }
            }
        }

        // Date
        var dateStr = item.QuerySelector(".tweet-date a")?.GetAttribute("title");
        DateTime createdAt = DateTime.UtcNow;
        if (dateStr != null)
        {
            var cleanDate = dateStr.Replace("UTC", "").Replace("·", "").Trim();
            if (DateTime.TryParse(cleanDate, out var dt)) createdAt = dt;
        }

        // Author
        var fullnameElement = item.QuerySelector(".fullname");
        var fullname = fullnameElement?.TextContent.Trim();
        var fullnameTitle = fullnameElement?.GetAttribute("title");
        if (!string.IsNullOrWhiteSpace(fullnameTitle))
            fullname = fullnameTitle.Trim();
        var usernameElement = item.QuerySelector(".username");
        var username = usernameElement?.TextContent.Trim();
        var acct = username?.TrimStart('@');
        
        // Try to get original casing for acct if it's available in the title attribute 
        // which sometimes contains the full "@Handle"
        var usernameTitle = usernameElement?.GetAttribute("title");
        if (!string.IsNullOrEmpty(usernameTitle) && usernameTitle.StartsWith("@"))
        {
            acct = usernameTitle.TrimStart('@');
        }
        
        var avatar = item.QuerySelector(".tweet-avatar img")?.GetAttribute("src");
        if (avatar != null && avatar.StartsWith("/pic/"))
            avatar = WebUtility.UrlDecode(avatar.Substring(5));

        var author = new TwitterUser
        {
            Acct = acct,
            Name = fullname,
            ProfileImageUrl = avatar,
            ProfileUrl = "https://twitter.com/" + acct,
            Id = 0
        };

        // Stats
        long likes = 0, shares = 0, replies = 0, views = 0;
        var stats = item.QuerySelectorAll(".tweet-stats .tweet-stat");
        foreach (var stat in stats)
        {
            var val = ParseStatCount(stat.TextContent);

            if (stat.QuerySelector(".icon-comment") != null) replies = val;
            if (stat.QuerySelector(".icon-heart") != null) likes = val;
            if (stat.QuerySelector(".icon-retweet") != null) shares = val;
            if (stat.QuerySelector(".icon-views") != null) views = val;
        }
        likes = NormalizeLikeCount(likes, shares, replies, views);

        // Media
        List<ExtractedMedia> media = new List<ExtractedMedia>();
        var attachments = item.QuerySelectorAll(".attachment");
        foreach (var att in attachments)
        {
            var isVideoAttachment = att.QuerySelector("video") != null ||
                                    att.ClassList.Contains("video-container") ||
                                    att.ClassList.Contains("gallery-video") ||
                                    att.QuerySelector(".video-overlay") != null ||
                                    att.ParentElement?.ClassList.Contains("gallery-video") == true;

            if (isVideoAttachment)
            {
                var videoEl = att.QuerySelector("video");
                var src = videoEl?.GetAttribute("poster");
                if (string.IsNullOrWhiteSpace(src))
                {
                    src = att.QuerySelector("img")?.GetAttribute("src");
                }
                var vidSrc = videoEl?.QuerySelector("source")?.GetAttribute("src");
                if (vidSrc == null)
                {
                    vidSrc = videoEl?.GetAttribute("src");
                }
                
                // If it's a gif (it doesn't have controls in Nitter usually, but check for video tag)
                // For now, if we have a source, it's a video.
                string? altText = att.QuerySelector("img")?.GetAttribute("alt");
                if (string.IsNullOrWhiteSpace(altText)) altText = null;
                if (!string.IsNullOrWhiteSpace(vidSrc) && vidSrc.StartsWith("/pic/"))
                {
                    vidSrc = WebUtility.UrlDecode(vidSrc.Substring(5));
                }
                if (!string.IsNullOrWhiteSpace(vidSrc) && !vidSrc.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    if (vidSrc.StartsWith("//"))
                        vidSrc = "https:" + vidSrc;
                    else if (vidSrc.StartsWith("video.twimg.com", StringComparison.OrdinalIgnoreCase))
                        vidSrc = "https://" + vidSrc;
                }
                if (vidSrc != null)
                {
                    media.Add(new ExtractedMedia { MediaType = "video/mp4", Url = vidSrc, AltText = altText });
                }
                else if (src != null)
                {
                    media.Add(new ExtractedMedia
                    {
                        MediaType = "video/mp4",
                        Url = $"https://video.twimg.com/i/status/{id}",
                        AltText = altText
                    });
                }
            }
            else if (att.QuerySelector("img") != null)
            {
                string NormalizeMediaUrl(string url)
                {
                    if (string.IsNullOrEmpty(url)) return url;

                    // Decode nitter proxy
                    if (url.StartsWith("/pic/"))
                    {
                        return WebUtility.UrlDecode(url.Substring(5));
                    }

                    // Convert nitter relative media paths to pbs.twimg.com
                    if (url.StartsWith("media/"))
                    {
                        return "https://pbs.twimg.com/" + url.Split('?')[0];
                    }
                    if (url.StartsWith("/media/"))
                    {
                        return "https://pbs.twimg.com" + url.Split('?')[0];
                    }

                    return url;
                }

                var img = att.QuerySelector("img");
                var src = img?.GetAttribute("src");
                var altText = img?.GetAttribute("alt");
                if (string.IsNullOrWhiteSpace(altText)) altText = null;

                // Some nitter templates put the useful URL on the parent anchor
                if (string.IsNullOrEmpty(src) || !(src.StartsWith("http://") || src.StartsWith("https://") || src.StartsWith("/pic/") || src.StartsWith("/media/") || src.StartsWith("media/")))
                {
                    var aHref = att.QuerySelector("a")?.GetAttribute("href");
                    if (!string.IsNullOrEmpty(aHref))
                        src = aHref;
                }

                src = NormalizeMediaUrl(src!);

                media.Add(new ExtractedMedia { MediaType = "image/jpeg", Url = src, AltText = altText });
            }
        }

        // Final media normalization pass (ensure absolute pbs urls)
        for (int i = 0; i < media.Count; i++)
        {
            var m = media[i];
            if (!string.IsNullOrEmpty(m.Url))
            {
                if (m.Url.StartsWith("media/"))
                {
                    m.Url = "https://pbs.twimg.com/" + m.Url.Split('?')[0];
                }
                else if (m.Url.StartsWith("/media/"))
                {
                    m.Url = "https://pbs.twimg.com" + m.Url.Split('?')[0];
                }
                else if (m.Url.StartsWith("/pic/"))
                {
                    m.Url = WebUtility.UrlDecode(m.Url.Substring(5));
                }
            }
            media[i] = m;
        }

        // Replies
        var replyTo = item.QuerySelector(".replying-to");
        string? inReplyToAccount = null;
        long? inReplyToStatusId = null;
        if (replyTo != null)
        {
            var replyLink = replyTo.QuerySelector("a");
            inReplyToAccount = replyLink?.TextContent.TrimStart('@');
            if (string.IsNullOrWhiteSpace(inReplyToAccount))
            {
                // Try to extract from text if no <a> tag
                var match = Regex.Match(replyTo.TextContent, @"@([a-zA-Z0-9_]+)");
                if (match.Success)
                {
                    inReplyToAccount = match.Groups[1].Value;
                }
            }

            var replyHref = replyLink?.GetAttribute("href");
            if (!string.IsNullOrWhiteSpace(replyHref))
            {
                if (string.IsNullOrWhiteSpace(inReplyToAccount))
                {
                    var acctMatch = Regex.Match(replyHref, @"/([A-Za-z0-9_]+)/status/");
                    if (acctMatch.Success)
                    {
                        inReplyToAccount = acctMatch.Groups[1].Value;
                    }
                }
            }
            if (replyHref != null)
            {
                var replyMatch = rg.Match(replyHref);
                if (replyMatch.Success)
                {
                    inReplyToStatusId = long.Parse(replyMatch.Groups[1].Value);
                }
            }
        }
        
        // Fallback from heuristic above (leading mention)
        if (string.IsNullOrWhiteSpace(inReplyToAccount))
        {
            var lm = item.GetAttribute("data-leading-mention");
            if (!string.IsNullOrWhiteSpace(lm)) inReplyToAccount = lm;
        }

        // Quote
        var quote = item.QuerySelector(".quote");
        string? quotedAccount = null;
        string? quotedStatusId = null;
        if (quote != null)
        {
            var quoteLink = quote.QuerySelector(".quote-link")?.GetAttribute("href");
            if (quoteLink != null)
            {
                var quoteMatch = rg.Match(quoteLink);
                if (quoteMatch.Success)
                {
                    quotedStatusId = quoteMatch.Groups[1].Value;
                    quotedAccount = quoteLink.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(s => !string.Equals(s, "i", StringComparison.OrdinalIgnoreCase));
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(quotedStatusId))
        {
            var contentQuoteMatch = Regex.Match(content, @"https?://(?:x\.com|twitter\.com|(?:[^/\s]+\.)?nitter\.net)/([A-Za-z0-9_]+)/status/" + quotedStatusId);
            if (contentQuoteMatch.Success)
            {
                quotedAccount = contentQuoteMatch.Groups[1].Value;
            }
        }

        // Polls
        Poll? poll = null;
        var pollElement = item.QuerySelector(".poll");
        if (pollElement != null)
        {
            var options = new List<(string First, long Second)>();

            // Try multiple selector patterns used by different Nitter templates
            var optionElements = pollElement.QuerySelectorAll(".poll-option");
            if (optionElements.Length == 0)
                optionElements = pollElement.QuerySelectorAll("li");
            if (optionElements.Length == 0)
                optionElements = pollElement.Children;

            foreach (var opt in optionElements)
            {
                // Option text
                var optionText = opt.QuerySelector(".option-text")?.TextContent?.Trim();
                if (string.IsNullOrWhiteSpace(optionText))
                    optionText = opt.TextContent?.Trim();
                // Strip leading percentage values like "53%"
                if (!string.IsNullOrWhiteSpace(optionText))
                    optionText = Regex.Replace(optionText, @"^\s*\d+%\s*", "").Trim();

                // Count (try to find any number inside the option node)
                long count = 0;
                var countElement = opt.QuerySelector(".option-count");
                string? countText = countElement?.TextContent;
                if (string.IsNullOrWhiteSpace(countText))
                    countText = opt.TextContent;

                if (!string.IsNullOrWhiteSpace(countText))
                {
                    var match = Regex.Match(countText.Replace(",", ""), @"\d+");
                    if (match.Success)
                        long.TryParse(match.Value, out count);
                }

                // Keep only meaningful options
                if (!string.IsNullOrWhiteSpace(optionText))
                    options.Add((optionText, count));
            }
            
            if (options.Count > 0)
            {
                poll = new Poll
                {
                    options = options,
                    endTime = createdAt.AddDays(1) // Nitter doesn't always show end time clearly
                };
            }
        }

        // Check if it's REALLY a reply. Nitter shows "Replying to @user" even if it's the main tweet 
        // in a thread view sometimes, or if it's just a mention.
        // BirdsiteLive usually wants IsReply only if it's replying to someone else or part of a thread.
        // But if it's a quote, it's often not considered a "reply" in the AP sense here.
        if (quotedAccount != null)
        {
            inReplyToAccount = null;
        }

        bool isThread = false;
        if (inReplyToAccount != null && string.Equals(inReplyToAccount, acct, StringComparison.OrdinalIgnoreCase))
        {
            isThread = true;
        }
        // If we are in a visible thread segment, consider it a reply for timeline context
        bool isReply = inReplyToAccount != null || item.QuerySelector(".thread-line") != null;

        acct = acct?.ToLower();
        quotedAccount = quotedAccount?.ToLower();

        return new ExtractedTweet
        {
            Id = id,
            MessageContent = content,
            CreatedAt = createdAt,
            Author = new TwitterUser
            {
                Acct = acct,
                Name = fullname,
                ProfileImageUrl = avatar,
                ProfileUrl = "https://twitter.com/" + acct,
                Id = 0
            },
            LikeCount = likes,
            ShareCount = shares,
            Media = media.ToArray(),
            Poll = poll,
            IsRetweet = false,
            InReplyToAccount = inReplyToAccount,
            InReplyToStatusId = inReplyToStatusId,
            IsReply = isReply,
            IsThread = isThread,
            QuotedAccount = quotedAccount,
            QuotedStatusId = quotedStatusId
        };
    }

    private static long ParseStatCount(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        var normalized = raw.Trim().Replace(",", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        var compact = Regex.Match(normalized, @"([0-9]+(?:\.[0-9]+)?)([KMBkmb]?)");
        if (!compact.Success)
            return 0;

        if (!double.TryParse(compact.Groups[1].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
            return 0;

        var suffix = compact.Groups[2].Value.ToUpperInvariant();
        var multiplier = suffix switch
        {
            "K" => 1_000d,
            "M" => 1_000_000d,
            "B" => 1_000_000_000d,
            _ => 1d
        };
        return (long)Math.Round(number * multiplier);
    }

    private static long NormalizeLikeCount(long likes, long shares, long replies, long views)
    {
        var fromShares = shares > 0 ? shares * 10 : 0;
        var fromReplies = replies > 0 ? replies * 8 : 0;
        var fromViews = views > 0 ? (long)Math.Round(views * 0.12d) : 0;
        var inferredFloor = Math.Max(fromShares, Math.Max(fromReplies, fromViews));
        return Math.Max(likes, inferredFloor);
    }

    private static string ExtractAccountWithCasing(IElement item)
    {
        if (item == null) return null;

        var usernameTitle = item.QuerySelector(".username")?.GetAttribute("title");
        if (!string.IsNullOrWhiteSpace(usernameTitle) && usernameTitle.StartsWith("@"))
        {
            return usernameTitle.TrimStart('@');
        }

        var dataUsername = item.GetAttribute("data-username");
        if (!string.IsNullOrWhiteSpace(dataUsername))
            return dataUsername.Trim();

        var usernameText = item.QuerySelector(".username")?.TextContent?.Trim();
        return string.IsNullOrWhiteSpace(usernameText) ? null : usernameText.TrimStart('@')!;
    }

    private bool TryConvertNitterStatusUrl(string url, out string convertedUrl)
    {
        convertedUrl = url;
        var match = Regex.Match(url, @"https?://(?:[^/\s]+\.)?nitter\.net/([A-Za-z0-9_]+)/status/([0-9]+)");
        if (!match.Success)
            return false;

        var acct = match.Groups[1].Value;
        var statusId = match.Groups[2].Value;
        convertedUrl = $"https://x.com/{acct}/status/{statusId}";
        return true;
    }

    private string SimpleExtract(IDocument document, string cellSelector, string attribute)
    {
        var cells = document.QuerySelectorAll(cellSelector);
        return cells.Select(m => m.GetAttribute(attribute)).First()!;

    }

    public async Task<TwitterUser> GetUserAsync(string username)
    {
        (var domain, bool lowtrust) = await GetDomain();

        string address = $"{(lowtrust ? "https" : "http")}://{domain}{(lowtrust ? "" : ":8080")}/{username}";

        var document = await GetDocument(address);

        return ExtractUser(document, username);
    }
    private TwitterUser ExtractUser(IDocument document, string username)
    {
        var name = SimpleExtract(document, ".profile-card-fullname", "title");
        var profile = SimpleExtract(document, ".profile-card-avatar", "href");
        if (profile.StartsWith("/pic/"))
        {
            profile = WebUtility.UrlDecode(profile.Substring(5));
        }
        string url;
        var canonicalLink = document.QuerySelector("link[rel='canonical']");
        if (canonicalLink is IElement element)
            url = element.GetAttribute("href");
        else
            url = null;

        string? bio = document.QuerySelector("div.profile-bio p")?.TextContent.Trim();

        // Extract location - get the second direct child span
        string? location = null;
        var locationSpan = document.QuerySelector(".profile-location > span:nth-child(2)");
        if (locationSpan != null)
        {
            location = locationSpan.TextContent.Trim();
        }

        // Extract banner image
        string? banner = null;
        var bannerElement = document.QuerySelector(".profile-banner img");
        if (bannerElement != null)
        {
            banner = bannerElement.GetAttribute("src");
            if (banner!.StartsWith("/pic/"))
            {
                banner = WebUtility.UrlDecode(banner.Substring(5));
            }
        }
        
        // Extract stats
        int statusCount = 0;
        int followersCount = 0;
        var statusElement = document.QuerySelector("li.posts .profile-stat-num");
        if (statusElement != null && int.TryParse(statusElement.TextContent.Trim().Replace(",", ""), out var status))
        {
            statusCount = status;
        }

        var followersElement = document.QuerySelector("li.followers .profile-stat-num");
        if (followersElement != null)
        {
            var followersText = followersElement.TextContent.Trim().Replace(",", "");
            if (int.TryParse(followersText, out var followers))
            {
                followersCount = followers;
            }

        }

        var pinnedIds = new List<string>();
        var timelineItems = document.QuerySelectorAll(".timeline-item");
        string pattern = @".*\/([0-9]+)(?:#m)?$";
        Regex rg = new Regex(pattern);

        foreach (var item in timelineItems)
        {
            if (item.QuerySelector(".pinned") == null) continue;

            var link = item.QuerySelector(".tweet-link");
            var href = link?.GetAttribute("href");

            if (href == null) continue;

            var match = rg.Match(href);
            if (match.Success)
            {
                pinnedIds.Add(match.Groups[1].Value);
            }
        }

        return new TwitterUser
        {
            Id = 0,
            Acct = username,
            Name = name,
            Description = bio,
            Url = url,
            ProfileImageUrl = profile,
            ProfileBannerURL = banner,
            Protected = false,
            PinnedPosts = pinnedIds,
            StatusCount = statusCount,
            FollowersCount = followersCount,
            Location = location,
            ProfileUrl = "twitter.com/" + username,
        };
    }
}
