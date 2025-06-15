using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Domain.Repository;
using BirdsiteLive.Moderation.Processors;
using dotMakeup.ipfs;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Utilities.Collections;

namespace BirdsiteLive.Moderation
{
    public interface IHousekeeping
    {
        Task ApplyModerationSettingsAsync();
        Task CleanCaches();
    }

    public class Housekeeping : IHousekeeping
    {
        private readonly IModerationRepository _moderationRepository;
        private readonly IFollowerModerationProcessor _followerModerationProcessor;
        private readonly ITwitterAccountModerationProcessor _twitterAccountModerationProcessor;
        private readonly IIpfsService _ipfs;
        private readonly ISocialMediaService _socialMediaService;
        private readonly InstanceSettings _settings;

        private readonly ILogger<Housekeeping> _logger;

        #region Ctor
        public Housekeeping(IModerationRepository moderationRepository, IFollowerModerationProcessor followerModerationProcessor, ITwitterAccountModerationProcessor twitterAccountModerationProcessor, IIpfsService ipfsService, ISocialMediaService socialMediaService, InstanceSettings settings, ILogger<Housekeeping> logger)
        {
            _moderationRepository = moderationRepository;
            _followerModerationProcessor = followerModerationProcessor;
            _twitterAccountModerationProcessor = twitterAccountModerationProcessor;
            _ipfs = ipfsService;
            _socialMediaService = socialMediaService;
            _settings = settings;
            _logger = logger;
        }
        #endregion

        public async Task ApplyModerationSettingsAsync()
        {
            _logger.LogWarning("ModerationPipeline started.");
            try
            {
                await CheckFollowerModerationPolicyAsync();
                await CheckTwitterAccountModerationPolicyAsync();
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "ModerationPipeline execution failed.");
            }
        }

        public async Task CleanCaches()
        {
            if (_settings.IpfsApi == null)
                return;

            var hashesP = _ipfs.AllPinnedHashes();
            var desiredPins = new HashSet<string>();
            var posts = await _socialMediaService.UserDal.GetAllPostsCacheIdAsync();
            foreach (var p in posts)
            {
                var post = await _socialMediaService.GetPostAsync(p);
                if (post is null) continue;

                if (post.CreatedAt > DateTime.Now.AddDays(-14) )
                {
                    foreach (var media in post.Media)
                    {
                        var h = media.Url.Replace("https://ipfs.kilogram.makeup/ipfs/", "");
                        desiredPins.Add(h);
                    }

                    await _socialMediaService.UserDal.DeletePostCacheAsync(p);
                }
            }

            foreach (var u in await _socialMediaService.UserDal.GetAllUsersAsync())
            {
                var userDoc = await _socialMediaService.UserDal.GetUserCacheAsync(u.Acct);
                if (userDoc is null) continue;
                var user = JsonDocument.Parse(userDoc).RootElement;
                var media = user.GetProperty("ProfileImageUrl").GetString();
                var h = media.Replace("https://ipfs.kilogram.makeup/ipfs/", "");
                desiredPins.Add(h);
            }

            var hashes = new HashSet<string>( await hashesP );
            var totalPins = hashes.Count;
            hashes.ExceptWith(desiredPins);
            _logger.LogInformation($"Unpinning from ipfs {hashes.Count} pins from {totalPins} total pins, leaving {desiredPins.Count} pins");
            
            foreach (var h in hashes)
            {
                try
                {
                    await _ipfs.Unpin(h);
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, $"Error unpinning {h}");
                }
            }
            if (_settings.IpfsApi != null)
                await _ipfs.GarbageCollection();
        }

        private async Task CheckFollowerModerationPolicyAsync()
        {
            var followerPolicy = _moderationRepository.GetModerationType(ModerationEntityTypeEnum.Follower);
            if (followerPolicy == ModerationTypeEnum.None) return;

            await _followerModerationProcessor.ProcessAsync(followerPolicy);
        }

        private async Task CheckTwitterAccountModerationPolicyAsync()
        {
            var twitterAccountPolicy = _moderationRepository.GetModerationType(ModerationEntityTypeEnum.TwitterAccount);
            if (twitterAccountPolicy == ModerationTypeEnum.None) return;

            await _twitterAccountModerationProcessor.ProcessAsync(twitterAccountPolicy);
        }
    }
}
