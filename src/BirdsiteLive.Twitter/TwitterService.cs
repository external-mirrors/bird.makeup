using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.Domain;
using BirdsiteLive.Twitter.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BirdsiteLive.Twitter
{
    public enum StrategyHints { Syndication, Graphql2024, Graphql2025, Sidecar, Nitter }
    public class TwitterService : ISocialMediaService
    {
        private readonly ITwitterTweetsService _twitterTweetsService;
        private readonly ITwitterUserService _twitterUserService;
        private readonly ITwitterUserDal _userDal;
        private readonly ISettingsDal _settings;
        private readonly SocialNetworkCache _socialNetworkCache;

        #region Ctor
        public TwitterService(ITwitterTweetsService twitterService, ITwitterUserService twitterUserService, ITwitterUserDal userDal, InstanceSettings settings, ISettingsDal settingsDal)
        {
            _twitterTweetsService = twitterService;
            _twitterUserService = twitterUserService;
            _userDal = userDal;
            UserDal = userDal;
            _settings = settingsDal;
            _socialNetworkCache = new SocialNetworkCache(settings);
        }
        #endregion

        public string MakeUserNameCanonical(string name)
        {
            return name.Trim().ToLowerInvariant();
        }
        public async Task<SocialMediaPost> GetPostAsync(string id)
        {
            if (!long.TryParse(id, out var parsedStatusId))
                return null;
            var post = await _socialNetworkCache.GetPost(id, [() => _twitterTweetsService.GetTweetAsync(parsedStatusId)]);
            return post;
        }

        public async Task<SocialMediaPost[]> GetNewPosts(SyncUser user)
        {
            var tweets = Array.Empty<ExtractedTweet>();
            
            try
            {
                if (user.LastTweetPostedId == -1)
                    tweets = await _twitterTweetsService.GetTimelineAsync(user);
                else
                    tweets = await _twitterTweetsService.GetTimelineAsync(user, user.LastTweetPostedId);
            }
            catch (Exception e)
            {
                await _userDal.UpdateTwitterUserAsync(user.Id, user.LastTweetPostedId, user.FetchingErrorCount++, user.LastSync);
            }
            if (tweets.Length > 0)
            {
                var mostRecentTweet = tweets.MaxBy(t => t.IdLong).IdLong;
                await _userDal.UpdateTwitterUserAsync(user.Id, mostRecentTweet, 0, user.LastSync);
            }

            var cacheThreshold = 100;
            var cacheSettings = await _settings.Get("twitter_user_cache");
            if (cacheSettings is not null)
            {
                if (cacheSettings.Value.TryGetProperty("threshold", out var threshold))
                {
                    cacheThreshold = threshold.GetInt32();
                }
            }
            if (user.Followers > cacheThreshold)
                await _twitterUserService.UpdateUserCache(user);
            
            foreach (var tweet in tweets)
                _socialNetworkCache.BackfillPostCache(tweet);

            return tweets;
        }

        public string ServiceName { get; } = "Twitter";
        
        // https://help.twitter.com/en/managing-your-account/twitter-username-rules
        public Regex ValidUsername { get;  } = new Regex(@"^[a-zA-Z0-9_]{1,15}$");
        public Regex UserMention { get;  } = new Regex(@"(^|.?[ \n\.]+)@([a-zA-Z0-9_]+)(?=\s|$|[\[\]<>,;:'\.’!?/—\|-]|(. ))");
        public SocialMediaUserDal UserDal { get; }
        public async Task<SocialMediaUser> GetUserAsync(string user)
        {
            var res = await _socialNetworkCache.GetUser(user, [
                () => _userDal.GetUserCacheAsync<TwitterUser>(user),
                () => _twitterUserService.GetUserAsync(user),
            ]);
            return res;
        }

    }
}