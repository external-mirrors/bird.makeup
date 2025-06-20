using System;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;

namespace BirdsiteLive.Services
{
    public interface ICachedStatisticsService
    {
        Task<CachedStatistics> GetStatisticsAsync();
    }

    public class CachedStatisticsService : ICachedStatisticsService
    {
        private readonly IFollowersDal _followersDal;

        private readonly ISocialMediaService _socialMediaService;
        private static Task<CachedStatistics> _cachedStatistics;
        private readonly InstanceSettings _instanceSettings;

        #region Ctor
        public CachedStatisticsService(ISocialMediaService socialMediaService, IFollowersDal followersDal, InstanceSettings instanceSettings)
        {
            _socialMediaService = socialMediaService;
            _instanceSettings = instanceSettings;
            _followersDal = followersDal;
            _cachedStatistics = CreateStats();
        }
        #endregion

        public async Task<CachedStatistics> GetStatisticsAsync()
        {
            var stats = await _cachedStatistics;
            if ((DateTime.UtcNow - stats.RefreshedTime).TotalMinutes > 5)
            {
                _cachedStatistics = CreateStats();
            }

            return stats;
        }

        private async Task<CachedStatistics> CreateStats()
        {
            var crawlingSpeed = await _socialMediaService.UserDal.GetCrawlingSpeed();
            var fediverseUsers = await _followersDal.GetFollowersCountAsync();

            var stats = new CachedStatistics
            {
                RefreshedTime = DateTime.UtcNow,
                CrawlingSpeed = crawlingSpeed,
                FediverseUsers = fediverseUsers
            };
        
            return stats;
        }
    }

    public class CachedStatistics
    {
        public DateTime RefreshedTime { get; set; }
        public decimal CrawlingSpeed { get; set; }
        public int FediverseUsers { get; set; }
    }
}