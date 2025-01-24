using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.DAL.Postgres.Settings;
using Npgsql;

namespace BirdsiteLive.DAL.Postgres.DataAccessLayers;

public class HackerNewsUserPostgresDal : SocialMediaUserPostgresDal, IHackerNewsUserDal
{
    
        #region Ctor
        public HackerNewsUserPostgresDal(PostgresSettings settings) : base(settings)
        {
            PostCacheTableName = null;
            tableName = _settings.HackerNewsUserTableName;
        }
        #endregion

        public sealed override string tableName { get; set; }
        public sealed override string PostCacheTableName { get; set; }
        public override string FollowingColumnName { get; set; } = "followings_hn";
        public override async Task<SyncUser[]> GetNextUsersToCrawlAsync(int nStart, int nEnd, int m)
        {
            throw new NotImplementedException();
        }
}