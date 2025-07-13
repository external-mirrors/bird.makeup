using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BirdsiteLive.Common.Exceptions;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Twitter.Models;
using BirdsiteLive.Twitter.Strategies;
using BirdsiteLive.Twitter.Tools;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Twitter
{
    public interface ITwitterUserService
    {
        Task<TwitterUser> GetUserAsync(string username);
        bool IsUserApiRateLimited();
        Task UpdateUserCache(SyncUser user);
    }

    public class TwitterUserService : ITwitterUserService
    {
        static Meter _meter = new("DotMakeup", "1.0.0");
        static Counter<int> _apiCalled = _meter.CreateCounter<int>("dotmakeup_api_called_count");
        
        private readonly ITwitterAuthenticationInitializer _twitterAuthenticationInitializer;
        private readonly ILogger<TwitterService> _logger;
        private readonly ITwitterUserDal _twitterUserDal;
        private readonly InstanceSettings _instanceSettings;
        private readonly ISettingsDal _settings;
        private readonly IHttpClientFactory _httpClientFactory;

        private readonly Graphql2024 _tweetFromGraphql2024;
        private readonly Graphql2025 _tweetFromGraphql2025;
        private readonly Sidecar _tweetFromSidecar;
        private readonly IUserExtractor[] _userExtractors;

        #region Ctor
        public TwitterUserService(ITwitterAuthenticationInitializer twitterAuthenticationInitializer, ITwitterUserDal twitterUserDal, InstanceSettings instanceSettings, ISettingsDal settingsDal, IHttpClientFactory httpClientFactory, ILogger<TwitterService> logger)
        {
            _twitterAuthenticationInitializer = twitterAuthenticationInitializer;
            _twitterUserDal = twitterUserDal;
            _instanceSettings = instanceSettings;
            _settings = settingsDal;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            
            _tweetFromGraphql2024 = new Graphql2024(_twitterAuthenticationInitializer, null, httpClientFactory, instanceSettings, logger);
            _tweetFromGraphql2025 = new Graphql2025(_twitterAuthenticationInitializer, null, httpClientFactory, instanceSettings, logger);
            _tweetFromSidecar = new Sidecar(_twitterUserDal, null, httpClientFactory, instanceSettings, logger);
            _userExtractors = [_tweetFromGraphql2024, _tweetFromGraphql2025];
        }
        #endregion

        public async Task<TwitterUser> GetUserAsync(string username, StrategyHints s)
        {
            if (s == StrategyHints.Graphql2024)
                return await _tweetFromGraphql2024.GetUserAsync(username);
            
            if (s == StrategyHints.Graphql2025)
                return await _tweetFromGraphql2025.GetUserAsync(username);
            
            if (s == StrategyHints.Sidecar)
                return await _tweetFromSidecar.GetUserAsync(username);
            
            return null;
        }
        public async Task<TwitterUser> GetUserAsync(string username)
        {
            var rnd = new Random();
            var u = _userExtractors[rnd.Next(_userExtractors.Length)];
            try
            {
                var user = await u.GetUserAsync(username);
                _apiCalled.Add(1, new KeyValuePair<string, object>("api", "twitter_account_" + u.GetType().Name)
                , new KeyValuePair<string, object>("result", "2xx") );
                return user;
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                _apiCalled.Add(1, new KeyValuePair<string, object>("api", "twitter_account_" + u.GetType().Name)
                , new KeyValuePair<string, object>("result", "4xx") );
                throw new UserNotFoundException();
            }
            catch (Exception e)
            {
                _apiCalled.Add(1, new KeyValuePair<string, object>("api", "twitter_account_" + u.GetType().Name)
                , new KeyValuePair<string, object>("result", "5xx") );
                _logger.LogError(e, "Error retrieving user {Username}", username);
                throw;
            }

        }

        async public Task UpdateUserCache(SyncUser user)
        {
            TwitterUser updatedUser = null;
            try
            {
                updatedUser = await _tweetFromSidecar.GetUserAsync(user.Acct);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error updating user cache for {Username}", user.Acct);
            }

            if (updatedUser is not null)
            {
                await _twitterUserDal.UpdateUserCacheAsync(updatedUser);
            }
            else
            {
                _logger.LogError("Error updating user cache for {Username}", user.Acct);
            }

        }

        public bool IsUserApiRateLimited()
        {
            return false;
        }
    }
}