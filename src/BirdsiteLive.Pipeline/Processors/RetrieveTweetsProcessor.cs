using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BirdsiteLive.Common.Exceptions;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.Pipeline.Contracts;
using BirdsiteLive.Pipeline.Models;
using BirdsiteLive.Twitter;
using BirdsiteLive.Twitter.Models;
using BirdsiteLive.Common.Settings;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Pipeline.Processors.SubTasks
{
    public class RetrieveTweetsProcessor : IRetrieveTweetsProcessor
    {
        private readonly ISocialMediaService _socialMediaService;
        private readonly ILogger<RetrieveTweetsProcessor> _logger;
        private readonly InstanceSettings _settings;

        #region Ctor
        public RetrieveTweetsProcessor(ISocialMediaService socialMediaService, InstanceSettings settings, ILogger<RetrieveTweetsProcessor> logger)
        {
            _socialMediaService = socialMediaService;
            _logger = logger;
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

                var t = Task.Run(async () => {
                    var user = userWtData.User;
                    var isVip = userWtData.Followers.ToList().Exists(x => x.Host == "r.town");
                    if (isVip)
                    {
                        user.Followers += 9999;
                    }
                    try 
                    {
                        var tweets = await _socialMediaService.GetNewPosts(user);
                        _logger.LogInformation(index + "/" + syncTwitterUsers.Count() + " Got " + tweets.Length + " posts from user " + user.Acct + " " );
                        if (tweets.Length > 0)
                        {
                            userWtData.Tweets = tweets;
                            usersWtTweets.Add(userWtData);
                            var latestPostDate = tweets.Max(x => x.CreatedAt);
                            await _socialMediaService.UserDal.UpdateUserExtradataAsync(user.Acct, "latest_post_date", latestPostDate);
                        }
                    } 
                    catch(RateLimitExceededException e)
                    {
                        await Task.Delay(_settings.SocialNetworkRequestJitter);
                        _logger.LogError(e.Message);
                    }
                    catch(Exception e)
                    {
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