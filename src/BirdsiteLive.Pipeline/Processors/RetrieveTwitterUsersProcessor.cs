using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.Pipeline.Models;
using BirdsiteLive.Pipeline.Contracts;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Pipeline.Processors
{
    public class RetrieveTwitterUsersProcessor : IRetrieveTwitterUsersProcessor
    {
        private readonly ISocialMediaService _socialMediaService;
        private readonly InstanceSettings _instanceSettings;
        private readonly ILogger<RetrieveTwitterUsersProcessor> _logger;
        private static Random rng = new Random();

        private readonly int _n_start;
        private readonly int _n_end;

        #region Ctor
        public RetrieveTwitterUsersProcessor(ISocialMediaService socialMediaService, InstanceSettings instanceSettings, ILogger<RetrieveTwitterUsersProcessor> logger)
        {
            _socialMediaService = socialMediaService;
            _instanceSettings = instanceSettings;
            _logger = logger;

            if (_instanceSettings.MultiplyNByOrdinal)
            {
                var ordinal = _instanceSettings.Ordinal;
                var range = _instanceSettings.n_end - _instanceSettings.n_start;
                _n_start = _instanceSettings.n_start + (range * ordinal);
                _n_end = _instanceSettings.n_end + (range * ordinal) - 1;
            }
            else
            {
                _n_start = _instanceSettings.n_start;
                _n_end = _instanceSettings.n_end;
            }
        }
        #endregion

        public async Task GetTwitterUsersAsync(BufferBlock<UserWithDataToSync[]> twitterUsersBufferBlock, CancellationToken ct)
        {
            for (; ; )
            {
                ct.ThrowIfCancellationRequested();

                if (_instanceSettings.ParallelTwitterRequests == 0)
                {
                    while (true)
                        await Task.Delay(10000);
                }
                
                SyncUser[] usersDal = await _socialMediaService.UserDal.GetNextUsersToCrawlAsync(_n_start, _n_end, _instanceSettings.m);

                var userCount = usersDal.Any() ? Math.Min(usersDal.Length, 5) : 1;
                var splitUsers = usersDal.OrderBy(a => rng.Next()).ToArray().Chunk(userCount).ToList();

                foreach (var users in splitUsers)
                {
                    ct.ThrowIfCancellationRequested();
                    List<UserWithDataToSync> toSync = new List<UserWithDataToSync>();
                    foreach (var u in users)
                    {
                        var followers = await _socialMediaService.UserDal.GetFollowersAsync(u.Id);
                        toSync.Add( new UserWithDataToSync()
                        {
                            User = u,
                            Followers = followers
                        });
                        
                    }

                    await twitterUsersBufferBlock.SendAsync(toSync.ToArray(), ct);

                }
                
                await Task.Delay(10, ct); // this is somehow necessary
            }
        }
    }
}
