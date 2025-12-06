using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Twitter.Models;
using BirdsiteLive.Twitter.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HttpMethod = System.Net.Http.HttpMethod;

namespace BirdsiteLive.Twitter.Strategies;

public class Nitter : ITimelineExtractor, IUserExtractor
{
    private readonly ISettingsDal _settings;
    private readonly ILogger<TwitterService> _logger;
    private readonly IBrowsingContext _context;
    private readonly IUserExtractor _userExtractor;
    private readonly ITweetExtractor _tweetExtractor;
    private string Useragent = "Bird.makeup ( https://git.sr.ht/~cloutier/bird.makeup ) Bot";
    static Meter _meter = new("DotMakeup", "1.0.0");
    static Counter<int> _nCalled = _meter.CreateCounter<int>("dotmakeup_nitter_called_count");
    public Nitter(ITweetExtractor tweetExtractor, IUserExtractor userExtractor, ISettingsDal settingsDal, ILogger<TwitterService> logger)
    {
        _settings = settingsDal;
        _logger = logger;
        _userExtractor = userExtractor;
        _tweetExtractor = tweetExtractor;
        
        var requester = new DefaultHttpRequester();
        requester.Headers["User-Agent"] = Useragent;
        requester.Headers["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8";
        requester.Headers["Accept-Encoding"] = "gzip, deflate";
        requester.Headers["Accept-Language"] = "en-US,en;q=0.5";
        var config = Configuration.Default.With(requester).WithDefaultLoader();
        _context = BrowsingContext.New(config);
    }

    private async Task<(string, bool)> GetDomain()
    {
        
        // https://status.d420.de/
        var nitterSettings = await _settings.Get("nitter");
        if (nitterSettings is null)
            throw new Exception("Nitter settings not found");



        List<(string, bool)> domains = new List<(string, bool)>() { };
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
        (var domain , bool lowtrust) = await GetDomain();
        
        string address = $"{(lowtrust?"https":"http")}://{domain}{(lowtrust?"":":8080")}/{user.Acct}{(withReplies?"/with_replies":"")}";
        
        var document = await GetDocument(address);

        var cellSelector = ".tweet-link";
        var cells = document.QuerySelectorAll(cellSelector);
        var titles = cells.Select(m => m.GetAttribute("href"));


        List<ExtractedTweet> tweets = new List<ExtractedTweet>();
        string pattern = @".*\/([0-9]+)#m";
        Regex rg = new Regex(pattern);
        
        foreach (string title in titles)
        {
            MatchCollection matchedId = rg.Matches(title);
            var matchString = matchedId[0].Groups[1].Value;
            var match = Int64.Parse(matchString);

            if (match <= fromId)
                continue;

            try
            {
                var tweet = await _tweetExtractor.GetTweetAsync(match);
                if (tweet.Author.Acct != user.Acct)
                {
                    if (lowtrust)
                        continue;
                    
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
                _logger.LogError($"Nitter: error fetching tweet {match} from user {user.Acct}");
            }
            await Task.Delay(100);
        }

        _nCalled.Add(1,
            new KeyValuePair<string, object>("source", domain),
            new KeyValuePair<string, object>("success", tweets.Count > 0)
        );
        
        return tweets;
    }

    private string SimpleExtract(IDocument document, string cellSelector, string attribute)
    {
        var cells = document.QuerySelectorAll(cellSelector);
        return cells.Select(m => m.GetAttribute(attribute)).First();
        
    }

    public async Task<TwitterUser> GetUserAsync(string username)
    {
        (var domain , bool lowtrust) = await GetDomain();
        
        string address = $"{(lowtrust?"https":"http")}://{domain}{(lowtrust?"":":8080")}/{username}";
        
        var document = await GetDocument(address);

        var name = SimpleExtract(document, ".profile-card-fullname", "title");
        var profile = SimpleExtract(document, ".profile-card-avatar", "href");
        string url;
        var canonicalLink = document.QuerySelector("link[rel='canonical']");
        if (canonicalLink is IElement element)
            url = element.GetAttribute("href");
        else
            url = null;
        
        string bio = document.QuerySelector("div.profile-bio p")?.TextContent.Trim();
        
        // Extract banner image
        string banner = "";
        var bannerElement = document.QuerySelector(".profile-banner img");
        if (bannerElement != null)
        {
            banner = bannerElement.GetAttribute("src");
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
            PinnedPosts = [],
            StatusCount = statusCount,
            FollowersCount = followersCount,
            Location = "",
            ProfileUrl = "",
        };
    }
}