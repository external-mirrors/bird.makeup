using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Twitter.Models;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Twitter.Strategies;

public class Sidecar : ITweetExtractor, ITimelineExtractor
{
    private readonly ITwitterUserDal  _twitterUserDal;
    private readonly ITwitterTweetsService _tweetsService;
    private readonly InstanceSettings _instanceSettings;
    private readonly ILogger<TwitterService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions()
    {
        Converters = { new TwitterSocialMediaUserConverter() }
    };
    public Sidecar(ITwitterUserDal userDal, ITwitterTweetsService tweetsService, IHttpClientFactory httpClientFactory, InstanceSettings instanceSettings, ILogger<TwitterService> logger)
    {
        _twitterUserDal = userDal;
        _tweetsService = tweetsService;
        _httpClientFactory = httpClientFactory;
        _instanceSettings = instanceSettings;
        _logger = logger;
        
    }
    public async Task<ExtractedTweet> GetTweetAsync(long tweetid)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"http://localhost:5000/twitter/gettweet/{tweetid}");
                            
            var httpResponse = await client.SendAsync(request);
            
                                
            var c = await httpResponse.Content.ReadAsStringAsync();
            var tweet = JsonSerializer.Deserialize<ExtractedTweet>(c, _serializerOptions);
            
            tweet = await _tweetsService.ExpandShortLinks(tweet);
            tweet = _tweetsService.CleanupText(tweet);
                            
            return tweet;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }
    
    
    public async Task<List<ExtractedTweet>> GetTimelineAsync(SyncUser user, long userId, long fromId, bool withReplies)
    {
        try
        {
            var tweets = new List<ExtractedTweet>();
            string username = String.Empty;
            string password = String.Empty;

            var candidates = await _twitterUserDal.GetTwitterCrawlUsersAsync(_instanceSettings.MachineName);
            Random.Shared.Shuffle(candidates);
            foreach (var account in candidates)
            {
                username = account.Acct;
                password = account.Password;
            }


            using var client = _httpClientFactory.CreateClient();
            string endpoint;
            if (withReplies)
                endpoint = "postbyuserwithreplies";
            else
                endpoint = "postbyuser";
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"http://localhost:5000/twitter/{endpoint}2/{user.TwitterUserId}");
            request.Headers.TryAddWithoutValidation("dotmakeup-user", username);
            request.Headers.TryAddWithoutValidation("dotmakeup-password", password);
            
            var httpResponse = await client.SendAsync(request);

            if (httpResponse.StatusCode != HttpStatusCode.OK)
            {
                return new List<ExtractedTweet>();
            }
            
            var c = await httpResponse.Content.ReadAsStringAsync();
            tweets = JsonSerializer.Deserialize<List<ExtractedTweet>>(c, _serializerOptions);
            var tweetsDocument = JsonDocument.Parse(c);
            
            for (var i = 0; i < tweets.Count; i++)
            {
                tweets[i] = await _tweetsService.ExpandShortLinks(tweets[i]);
                tweets[i] = _tweetsService.CleanupText(tweets[i]);
            }
            
            return tweets;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return new List<ExtractedTweet>();
        }
    }
}