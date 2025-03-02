using System;
using System.Threading.Tasks;
using BirdsiteLive.Domain.Repository;
using BirdsiteLive.Moderation.Processors;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Moderation
{
    public interface IHousekeeping
    {
        Task ApplyModerationSettingsAsync();
        Task CleanCaches();
    }

    public class HousekeepingPipelines : IHousekeeping
    {
        private readonly IModerationRepository _moderationRepository;
        private readonly IFollowerModerationProcessor _followerModerationProcessor;
        private readonly ITwitterAccountModerationProcessor _twitterAccountModerationProcessor;

        private readonly ILogger<HousekeepingPipelines> _logger;

        #region Ctor
        public HousekeepingPipelines(IModerationRepository moderationRepository, IFollowerModerationProcessor followerModerationProcessor, ITwitterAccountModerationProcessor twitterAccountModerationProcessor, ILogger<HousekeepingPipelines> logger)
        {
            _moderationRepository = moderationRepository;
            _followerModerationProcessor = followerModerationProcessor;
            _twitterAccountModerationProcessor = twitterAccountModerationProcessor;
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

        public Task CleanCaches()
        {
            return Task.CompletedTask;
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
