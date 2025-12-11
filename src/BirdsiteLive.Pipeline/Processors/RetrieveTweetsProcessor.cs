using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using BirdsiteLive.Common.Exceptions;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Pipeline.Contracts;
using BirdsiteLive.Pipeline.Models;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Domain.Statistics;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Pipeline.Processors
{
    public class RetrieveTweetsProcessor : IRetrieveTweetsProcessor
    {
        private readonly ISocialMediaService _socialMediaService;
        private readonly ILogger<RetrieveTweetsProcessor> _logger;
        private readonly IStatisticsHandler _statisticsHandler;
        private readonly InstanceSettings _settings;
        private static readonly ActivitySource ActivitySource = new("DotMakeup");

        #region Ctor
        public RetrieveTweetsProcessor(ISocialMediaService socialMediaService, IStatisticsHandler statisticsHandler, InstanceSettings settings, ILogger<RetrieveTweetsProcessor> logger)
        {
            _socialMediaService = socialMediaService;
            _logger = logger;
            _statisticsHandler = statisticsHandler;
            _settings = settings;
        }
        #endregion

        public async Task<UserWithDataToSync[]> ProcessAsync(UserWithDataToSync[] syncTwitterUsers, CancellationToken ct)
        {

            if (_settings.ParallelTwitterRequests == 0)
            {
                while(true)
                    await Task.Delay(1000);
            }

            var usersWtTweets = new ConcurrentBag<UserWithDataToSync>();
            List<Task> todo = new List<Task>();
            int index = 0;
            foreach (var userWtData in syncTwitterUsers)
            {
                index++;
                var requestIndex = index;

                var t = Task.Run(async () => {
                    var user = userWtData.User;
                    var isVip = userWtData.Followers.ToList().Exists(x => x.Host == "r.town");
                    if (isVip)
                    {
                        user.Followers += 9999;
                    }
                    using var activity = ActivitySource.StartActivity("RetrieveTweetsProcessor", ActivityKind.Internal);
                    activity?.SetTag("user.acct", user.Acct);
                    activity?.SetTag("user.isVip", isVip);
                    try 
                    {
                        var tweets = await _socialMediaService.GetNewPosts(user);
                        activity?.SetTag("posts.count", tweets.Length);
                        _logger.LogInformation(requestIndex + "/" + syncTwitterUsers.Count() + " Got " + tweets.Length + " posts from user " + user.Acct + " " );
                        if (tweets.Length > 0)
                        {
                            userWtData.Tweets = tweets;
                            usersWtTweets.Add(userWtData);
                            var latestPostDate = tweets.Max(x => x.CreatedAt);
                            activity?.SetTag("latest_post_date", latestPostDate);
                            await _socialMediaService.UserDal.UpdateUserExtradataAsync(user.Acct, "latest_post_date", latestPostDate);
                        }
                        _statisticsHandler.RegisterNewPosts(tweets);
                    } 
                    catch(RateLimitExceededException e)
                    {
                        activity?.SetStatus(ActivityStatusCode.Error, e.Message);
                        await Task.Delay(_settings.SocialNetworkRequestJitter);
                        _logger.LogError(e.Message);
                    }
                    catch(Exception e)
                    {
                        activity?.SetStatus(ActivityStatusCode.Error, e.Message);
                        _logger.LogError(e.Message);
                    }
                });
                todo.Add(t);
                if (todo.Count >= _settings.ParallelTwitterRequests)
                {
                    await Task.WhenAll(todo);
                    await Task.Delay(_settings.TwitterRequestDelay, ct);
                    todo.Clear();
                }
                await Task.Delay((int)Math.Round(Random.Shared.NextSingle() * _settings.SocialNetworkRequestJitter), ct);
                
            }

            await Task.WhenAll(todo);
            return usersWtTweets.ToArray();
        }
    }
}