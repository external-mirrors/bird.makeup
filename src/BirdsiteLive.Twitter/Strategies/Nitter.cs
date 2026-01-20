using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
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
    private readonly ITweetExtractor _tweetExtractor;
    private readonly ITwitterUserDal _twitterUserDal;
    private string Useragent = "Bird.makeup ( https://git.sr.ht/~cloutier/bird.makeup ) Bot";
    static Meter _meter = new("DotMakeup", "1.0.0");
    static Counter<int> _nCalled = _meter.CreateCounter<int>("dotmakeup_nitter_called_count");

    public Nitter(ITweetExtractor tweetExtractor, IUserExtractor userExtractor, ISettingsDal settingsDal, ITwitterUserDal twitterUserDal,
        ILogger<TwitterService> logger)
    {
        _settings = settingsDal;
        _logger = logger;
        _userExtractor = userExtractor;
        _tweetExtractor = tweetExtractor;
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

    private async Task<(string, bool)> GetDomain()
    {

        // https://status.d420.de/
        var nitterSettings = await _settings.Get("nitter");
        if (nitterSettings is null)
            throw new Exception("Nitter settings not found");



        List<(string, bool)> domains = new List<(string, bool)>();
        foreach (var d in nitterSettings.Value.GetProperty("lowtrustendpoints").EnumerateArray())
        {
            domains.Add((d.GetString(), true));
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

    public async Task<List<ExtractedTweet>> GetTimelineAsync(SyncUser user, long fromId, long a, bool withReplies)
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
        string pattern = @".*\/([0-9]+)#m";
        Regex rg = new Regex(pattern);

        if (false && !lowtrust)
        {
            var timelineItems = document.QuerySelectorAll(".timeline-item");
            foreach (var item in timelineItems)
            {
                try
                {
                    var tweet = ParseTweetFromElement(item);
                    if (tweet == null) continue;

                    if (tweet.IdLong <= fromId) continue;

                    if (tweet.Author.Acct != user.Acct)
                    {
                        tweet.IsRetweet = true;
                        tweet.OriginalAuthor = tweet.Author;
                        tweet.Author = await _userExtractor.GetUserAsync(user.Acct);
                        tweet.RetweetId = tweet.IdLong;
                        // Sadly not given by Nitter UI
                        var gen = new TwitterSnowflakeGenerator(1, 1);
                        tweet.Id = gen.NextId().ToString();
                    }
                    tweets.Add(tweet);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Nitter: error parsing tweet from user {user.Acct}");
                }
            }
        }
        else
        {
            var cellSelector = ".tweet-link";
            var cells = document.QuerySelectorAll(cellSelector);
            var titles = cells.Select(m => m.GetAttribute("href"));

            foreach (string title in titles)
            {
                if (title == null) continue;
                MatchCollection matchedId = rg.Matches(title);
                if (matchedId.Count == 0) continue;
                var matchString = matchedId[0].Groups[1].Value;
                var match = Int64.Parse(matchString);

                if (match <= fromId)
                    continue;

                try
                {
                    var tweet = await _tweetExtractor.GetTweetAsync(match);
                    if (tweet.Author.Acct != user.Acct)
                    {
                        continue;
                    }

                    tweets.Add(tweet);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Nitter: error fetching tweet {match} from user {user.Acct}");
                }

                await Task.Delay(100);
            }
        }

        _nCalled.Add(1,
            new KeyValuePair<string, object>("source", domain),
            new KeyValuePair<string, object>("success", tweets.Count > 0)
        );

        return tweets;
    }

    public async Task<ExtractedTweet> GetTweetAsync(long statusId)
    {
        (var domain, bool lowtrust) = await GetDomain();

        // Use /i/status/<id> which usually redirects on Nitter
        string address = $"{(lowtrust ? "https" : "http")}://{domain}{(lowtrust ? "" : ":8080")}/i/status/{statusId}";

        var document = await GetDocument(address);
        
        var mainTweet = document.QuerySelector(".main-tweet .timeline-item");
        if (mainTweet != null)
        {
            var tweet = ParseTweetFromElement(mainTweet);
            if (tweet != null)
            {
                // Ensure ID is set correctly if it wasn't parsed (should be parsed by fallback though)
                if (string.IsNullOrEmpty(tweet.Id) || tweet.Id == "0") tweet.Id = statusId.ToString();
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

        string pattern = @".*\/([0-9]+)#m";
        Regex rg = new Regex(pattern);
        var matchId = rg.Match(link);
        if (!matchId.Success) return null;
        var id = matchId.Groups[1].Value;

        var content = item.QuerySelector(".tweet-content")?.TextContent;

        // Date
        var dateStr = item.QuerySelector(".tweet-date a")?.GetAttribute("title");
        DateTime createdAt = DateTime.UtcNow;
        if (dateStr != null)
        {
            var cleanDate = dateStr.Replace("UTC", "").Replace("·", "").Trim();
            if (DateTime.TryParse(cleanDate, out var dt)) createdAt = dt;
        }

        // Author
        var fullname = item.QuerySelector(".fullname")?.TextContent.Trim();
        var username = item.QuerySelector(".username")?.TextContent.Trim();
        var avatar = item.QuerySelector(".tweet-avatar img")?.GetAttribute("src");
        if (avatar != null && avatar.StartsWith("/pic/"))
            avatar = WebUtility.UrlDecode(avatar.Substring(5));

        var author = new TwitterUser
        {
            Acct = username?.TrimStart('@'),
            Name = fullname,
            ProfileImageUrl = avatar,
            ProfileUrl = "https://twitter.com/" + username?.TrimStart('@'),
            Id = 0
        };

        // Stats
        long likes = 0, shares = 0;
        var stats = item.QuerySelectorAll(".tweet-stats .tweet-stat");
        foreach (var stat in stats)
        {
            var num = stat.TextContent.Trim().Replace(",", "");
            long val = 0;
            long.TryParse(num, out val);

            if (stat.QuerySelector(".icon-heart") != null) likes = val;
            if (stat.QuerySelector(".icon-retweet") != null) shares = val;
        }

        // Media
        List<ExtractedMedia> media = new List<ExtractedMedia>();
        var attachments = item.QuerySelectorAll(".attachment");
        foreach (var att in attachments)
        {
            if (att.QuerySelector("video") != null)
            {
                var src = att.QuerySelector("video")?.GetAttribute("poster");
                var vidSrc = att.QuerySelector("source")?.GetAttribute("src");
                if (vidSrc != null)
                {
                    media.Add(new ExtractedMedia { MediaType = "video", Url = vidSrc });
                }
                else if (src != null)
                {
                    if (src.StartsWith("/pic/")) src = WebUtility.UrlDecode(src.Substring(5));
                    media.Add(new ExtractedMedia { MediaType = "image", Url = src });
                }
            }
            else if (att.QuerySelector("img") != null)
            {
                var src = att.QuerySelector("img")?.GetAttribute("src");
                if (src != null && src.StartsWith("/pic/")) src = WebUtility.UrlDecode(src.Substring(5));
                media.Add(new ExtractedMedia { MediaType = "image", Url = src });
            }
        }

        return new ExtractedTweet
        {
            Id = id,
            MessageContent = content,
            CreatedAt = createdAt,
            Author = author,
            LikeCount = likes,
            ShareCount = shares,
            Media = media.ToArray(),
            IsRetweet = false
        };
    }

    private string SimpleExtract(IDocument document, string cellSelector, string attribute)
    {
        var cells = document.QuerySelectorAll(cellSelector);
        return cells.Select(m => m.GetAttribute(attribute)).First();

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

        string bio = document.QuerySelector("div.profile-bio p")?.TextContent.Trim();

        // Extract location - get the second direct child span
        string location = null;
        var locationSpan = document.QuerySelector(".profile-location > span:nth-child(2)");
        if (locationSpan != null)
        {
            location = locationSpan.TextContent.Trim();
        }

        // Extract banner image
        string banner = null;
        var bannerElement = document.QuerySelector(".profile-banner img");
        if (bannerElement != null)
        {
            banner = bannerElement.GetAttribute("src");
            if (banner.StartsWith("/pic/"))
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
        string pattern = @".*\/([0-9]+)#m";
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
