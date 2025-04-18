using System;
using System.Text.Json;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Domain.Repository;
using BirdsiteLive.Moderation.Processors;
using dotMakeup.ipfs;
using Microsoft.Extensions.Logging;

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
            var posts = await _socialMediaService.UserDal.GetAllPostsCacheIdAsync();
            foreach (var p in posts)
            {
                var postString = await _socialMediaService.UserDal.GetPostCacheAsync(p);
                var post = JsonSerializer.Deserialize<SocialMediaPost>(postString);

                if (post.CreatedAt > DateTime.Now.AddDays(-14) )
                {
                    foreach (var media in post.Media)
                    {
                        try
                        {
                            await _ipfs.Unpin(media.Url.Replace("https://ipfs.kilogram.makeup/ipfs/", ""));
                            _logger.LogWarning($"Unpined: {media.Url}");
                        }
                        catch (Exception e)
                        {
                            _logger.LogCritical(e, $"Error unpinning {media.Url}");
                        }
                    }
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
