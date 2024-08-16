﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
                    userWtData.Followers = await _socialMediaService.UserDal.GetFollowersAsync(user.Id);
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
                            userWtData.Tweets = (ExtractedTweet[])tweets;
                            usersWtTweets.Add(userWtData);
                        }
                    } 
                    catch(Exception e)
                    {
                        _logger.LogError(e.Message);
                    }
                });
                todo.Add(t);
                if (todo.Count > _settings.ParallelTwitterRequests)
                {
                    await Task.WhenAll(todo);
                    await Task.Delay(_settings.TwitterRequestDelay);
                    todo.Clear();
                }
                
            }

            await Task.WhenAll(todo);
            return usersWtTweets.ToArray();
        }
    }
}