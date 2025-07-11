using System;
using System.Collections.Generic;
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
        var lowtrust = false;
        // https://status.d420.de/
        var nitterSettings = await _settings.Get("nitter");
        if (nitterSettings is null)
            return new List<ExtractedTweet>();


        var requester = new DefaultHttpRequester();
        string useragent;
        if (lowtrust && nitterSettings.Value.TryGetProperty("useragent", out JsonElement useragentElement))
            useragent = useragentElement.GetString();
        else
            useragent = Useragent;
        requester.Headers["User-Agent"] = useragent;
        requester.Headers["Accept"] =
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8";
        requester.Headers["Accept-Encoding"] = "gzip, deflate";
        requester.Headers["Accept-Language"] = "en-US,en;q=0.5";
        var config = Configuration.Default.With(requester).WithDefaultLoader();
        var context = BrowsingContext.New(config);

        List<string> domains = new List<string>() { };
        var prop = (lowtrust) ? "lowtrustendpoints" : "endpoints";
        foreach (var d in nitterSettings.Value.GetProperty(prop).EnumerateArray())
        {
            domains.Add(d.GetString());
        }

        Random rnd = new Random();
        int randIndex = rnd.Next(domains.Count);
        var domain = domains[randIndex];
        string address;
        if (withReplies)
            address = $"https://{domain}/{user.Acct}/with_replies";
        else
            address = $"https://{domain}/{user.Acct}";
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
                //var tweet = await TweetFromSyndication(match);
                var tweet = await _tweetExtractor.GetTweetAsync(match);
                if (tweet.Author.Acct != user.Acct)
                {
                    if (!nitterSettings.Value.GetProperty("allowboosts").GetBoolean() || lowtrust)
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
                _logger.LogError($"error fetching tweet {match} from user {user.Acct}");
            }
            await Task.Delay(100);
        }
        
        return tweets;
    }
    
}