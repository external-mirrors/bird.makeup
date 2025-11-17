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
using AngleSharp.Io;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Twitter.Models;
using BirdsiteLive.Twitter.Tools;
using Microsoft.Extensions.Logging;
using HttpMethod = System.Net.Http.HttpMethod;

namespace BirdsiteLive.Twitter.Strategies;

public class Nitter : ITimelineExtractor
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
    
    public async Task<List<ExtractedTweet>> GetTimelineAsync(SyncUser user, long fromId, long a, bool withReplies)
    {
        // https://status.d420.de/
        var nitterSettings = await _settings.Get("nitter");
        if (nitterSettings is null)
            return new List<ExtractedTweet>();


        var requester = new DefaultHttpRequester();
        
        requester.Headers["User-Agent"] = Useragent;
        requester.Headers["Accept"] =
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8";
        requester.Headers["Accept-Encoding"] = "gzip, deflate";
        requester.Headers["Accept-Language"] = "en-US,en;q=0.5";
        var config = Configuration.Default.With(requester).WithDefaultLoader();
        var context = BrowsingContext.New(config);

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
        (var domain , bool lowtrust) = domains[randIndex];
        string address;
        if (withReplies)
            address = $"{(lowtrust?"https":"http")}://{domain}{(lowtrust?"":":8080")}/{user.Acct}/with_replies";
        else
            address = $"{(lowtrust?"https":"http")}://{domain}{(lowtrust?"":":8080")}/{user.Acct}";
        _logger.LogInformation($"Nitter: fetching {address}");
        var document = await context.OpenAsync(address);

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
    
}