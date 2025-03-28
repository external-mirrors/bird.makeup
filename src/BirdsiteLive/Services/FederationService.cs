using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Moderation;
using BirdsiteLive.Pipeline;
using Microsoft.Extensions.Hosting;

namespace BirdsiteLive.Services
{
    public class FederationService : BackgroundService
    {
        private readonly IDatabaseInitializer _databaseInitializer;
        private readonly IHousekeeping _housekeeping;
        private readonly IStatusPublicationPipeline _statusPublicationPipeline;
        private readonly InstanceSettings _instanceSettings;
        private readonly IHostApplicationLifetime _applicationLifetime;

        #region Ctor
        public FederationService(IDatabaseInitializer databaseInitializer, IHousekeeping housekeeping, IStatusPublicationPipeline statusPublicationPipeline, InstanceSettings instanceSettings, IHostApplicationLifetime applicationLifetime)
        {
            _databaseInitializer = databaseInitializer;
            _housekeeping = housekeeping;
            _statusPublicationPipeline = statusPublicationPipeline;
            _instanceSettings = instanceSettings;
            _applicationLifetime = applicationLifetime;
        }
        #endregion

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _databaseInitializer.InitAndMigrateDbAsync();
                if (_instanceSettings.Ordinal == 0)
                {
                    await Task.WhenAll(_housekeeping.ApplyModerationSettingsAsync(), _housekeeping.CleanCaches());
                }
                await _statusPublicationPipeline.ExecuteAsync(stoppingToken);
            }
            finally
            {
                await Task.Delay(1000 * 30);
                _applicationLifetime.StopApplication();
            }
        }
    }
}