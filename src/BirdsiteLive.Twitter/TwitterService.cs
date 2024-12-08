using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.Twitter.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BirdsiteLive.Twitter
{

    public class TwitterService : ISocialMediaService
    {
        private readonly ITwitterTweetsService _twitterTweetsService;
        private readonly ICachedTwitterUserService _twitterUserService;
        private readonly ITwitterUserDal _userDal;

        #region Ctor
        public TwitterService(ICachedTwitterTweetsService twitterService, ICachedTwitterUserService twitterUserService, ITwitterUserDal userDal, InstanceSettings settings)
        {
            _twitterTweetsService = twitterService;
            _twitterUserService = twitterUserService;
            _userDal = userDal;
            UserDal = userDal;
        }
        #endregion

        public async Task<SocialMediaPost> GetPostAsync(string id)
        {
            if (!long.TryParse(id, out var parsedStatusId))
                return null;
            var post = await _twitterTweetsService.GetTweetAsync(parsedStatusId);
            return post;
        }

        public async Task<SocialMediaPost[]> GetNewPosts(SyncUser user)
        {
            var tweets = new ExtractedTweet[0];
            
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
                var tweetId = tweets.Last().Id;
                await _userDal.UpdateTwitterUserAsync(user.Id, long.Parse(tweetId), 0, user.LastSync);
            }

            if (user.Followers > 60)
                await _twitterUserService.UpdateUserCache(user);

            return tweets;
        }

        public string ServiceName { get; } = "Twitter";
        
        // https://help.twitter.com/en/managing-your-account/twitter-username-rules
        public Regex ValidUsername { get;  } = new Regex(@"^[a-zA-Z0-9_]{1,15}$");
        public Regex UserMention { get;  } = new Regex(@"(^|.?[ \n\.]+)@([a-zA-Z0-9_]+)(?=\s|$|[\[\]<>,;:'\.’!?/—\|-]|(. ))");
        public SocialMediaUserDal UserDal { get; }
        public async Task<SocialMediaUser> GetUserAsync(string user)
        {
            var res = await _twitterUserService.GetUserAsync(user);
            return res;
        }

    }
}