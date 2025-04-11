using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Models;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Moderation.Actions;
using BirdsiteLive.Pipeline.Contracts;
using BirdsiteLive.Pipeline.Models;
using BirdsiteLive.Pipeline.Processors.SubTasks;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Pipeline.Processors
{
    public class SendTweetsToFollowersProcessor : ISendTweetsToFollowersProcessor
    {
        private readonly ISendTweetsToInboxTask _sendTweetsToInboxTask;
        private readonly ISendTweetsToSharedInboxTask _sendTweetsToSharedInbox;
        private readonly SocialMediaUserDal _userDal;
        private readonly IFollowersDal _followersDal;
        private readonly ISocialMediaService _socialMediaService;
        private readonly InstanceSettings _instanceSettings;
        private readonly ILogger<SendTweetsToFollowersProcessor> _logger;
        private readonly IRemoveFollowerAction _removeFollowerAction;
        private List<Task> _todo = new List<Task>();

        #region Ctor
        public SendTweetsToFollowersProcessor(ISendTweetsToInboxTask sendTweetsToInboxTask, ISendTweetsToSharedInboxTask sendTweetsToSharedInbox, IFollowersDal followersDal, ISocialMediaService socialMediaService, ILogger<SendTweetsToFollowersProcessor> logger, InstanceSettings instanceSettings, IRemoveFollowerAction removeFollowerAction)
        {
            _sendTweetsToInboxTask = sendTweetsToInboxTask;
            _sendTweetsToSharedInbox = sendTweetsToSharedInbox;
            _logger = logger;
            _instanceSettings = instanceSettings;
            _removeFollowerAction = removeFollowerAction;
            _socialMediaService = socialMediaService;
            _userDal = socialMediaService.UserDal;
            _followersDal = followersDal;
        }
        #endregion

        public async Task ProcessAsync(UserWithDataToSync[] usersWithTweetsToSync, CancellationToken ct)
        {
            foreach (var userWithTweetsToSync in usersWithTweetsToSync)
            {
                var user = userWithTweetsToSync.User;
                userWithTweetsToSync.Followers = await _socialMediaService.UserDal.GetFollowersAsync(user.Id);

                _todo = _todo.Where(x => !x.IsCompleted).ToList();
                
                var t = Task.Run( async () => 
                {
                    // Process Shared Inbox
                    var followersWtSharedInbox = userWithTweetsToSync.Followers
                        .Where(x => !string.IsNullOrWhiteSpace(x.SharedInboxRoute))
                        .ToList();
                    await ProcessFollowersWithSharedInboxAsync(userWithTweetsToSync.Tweets, followersWtSharedInbox, user);

                    // Process Inbox
                    var followerWtInbox = userWithTweetsToSync.Followers
                        .Where(x => string.IsNullOrWhiteSpace(x.SharedInboxRoute))
                        .ToList();
                    await ProcessFollowersWithInboxAsync(userWithTweetsToSync.Tweets, followerWtInbox, user);
                    
                    _logger.LogInformation("Done sending " + userWithTweetsToSync.Tweets.Length + " tweets for "
                        + userWithTweetsToSync.Followers.Length + " followers for user " + userWithTweetsToSync.User.Acct);
                }, ct);
                _todo.Add(t);

                if (_todo.Count >= _instanceSettings.ParallelFediversePosts)
                {
                    await Task.WhenAny(_todo);
                }
                
                
            }

        }

        private async Task ProcessFollowersWithSharedInboxAsync(SocialMediaPost[] tweets, List<Follower> followers, SyncUser user)
        {
            var followersPerInstances = followers.GroupBy(x => x.Host);

            foreach (var followersPerInstance in followersPerInstances)
            {
                try
                {
                    _logger.LogDebug("Sending " + tweets.Length + " tweets from user " + user.Acct + " to instance " + followersPerInstance.Key);
                    await _sendTweetsToSharedInbox.ExecuteAsync(tweets, user, followersPerInstance.Key, followersPerInstance.ToArray());

                    foreach (var f in followersPerInstance)
                        await ProcessWorkingUserAsync(f);
                }
                catch (HttpRequestException e)
                {
                    var follower = followersPerInstance.First();
                    _logger.LogError(e, "Posting to {Host}{Route} failed (forbidden). Removing following relation", follower.Host, follower.SharedInboxRoute);

                    if (e.StatusCode == HttpStatusCode.Forbidden)
                    {
                        foreach (var f in followersPerInstance)
                        {
                            await _socialMediaService.UserDal.RemoveFollower(f.Id, user.Id);
                        }
                    }
                }
                catch (Exception e)
                {
                    var follower = followersPerInstance.First();
                    _logger.LogError(e, "Posting to {Host}{Route} failed", follower.Host, follower.SharedInboxRoute);

                    foreach (var f in followersPerInstance)
                        await ProcessFailingUserAsync(f, user);
                }
            }
        }
        
        private async Task ProcessFollowersWithInboxAsync(SocialMediaPost[] tweets, List<Follower> followerWtInbox, SyncUser user)
        {
            foreach (var follower in followerWtInbox)
            {
                try
                {
                    await _sendTweetsToInboxTask.ExecuteAsync(tweets, follower, user);
                    await ProcessWorkingUserAsync(follower);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Posting to {Host}{Route} failed", follower.Host, follower.InboxRoute);
                    await ProcessFailingUserAsync(follower, user);
                }
            }
        }

        private async Task ProcessWorkingUserAsync(Follower follower)
        {
            if (follower.PostingErrorCount > 0)
            {
                await _followersDal.UpdateFollowerErrorCountAsync(follower.Id, 0);
            }
        }

        private async Task ProcessFailingUserAsync(Follower follower, SyncUser user)
        {
            follower.PostingErrorCount++;

            if (follower.PostingErrorCount > _instanceSettings.FailingFollowerCleanUpThreshold 
                && _instanceSettings.FailingFollowerCleanUpThreshold > 0
                || follower.PostingErrorCount > 2147483600)
            {
                await _removeFollowerAction.ProcessAsync(follower);
            }
            else
            {
                await _followersDal.UpdateFollowerErrorCountAsync(follower.Id, follower.PostingErrorCount++);
            }
        }
    }
}