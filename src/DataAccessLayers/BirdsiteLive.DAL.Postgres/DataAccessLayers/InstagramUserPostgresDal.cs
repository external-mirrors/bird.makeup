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

public class InstagramUserPostgresDal : SocialMediaUserPostgresDal, IInstagramUserDal
{
    
        #region Ctor
        public InstagramUserPostgresDal(PostgresSettings settings) : base(settings)
        {
            PostCacheTableName = _settings.CachedInstaPostsTableName;
            tableName = _settings.InstagramUserTableName;
        }
        #endregion

        public sealed override string tableName { get; set; }
        public sealed override string PostCacheTableName { get; set; }
        public override string FollowingColumnName { get; set; } = "followings_instagram";
        public override async Task<SyncUser[]> GetNextUsersToCrawlAsync(int nStart, int nEnd, int m)
        {
            string query = @$"
                SELECT id, acct, lastsync 
                FROM {tableName} 
                WHERE id IN (SELECT unnest({FollowingColumnName}) as fid FROM {_settings.FollowersTableName} WHERE host = 'r.town' GROUP BY fid)
                ORDER BY lastsync ASC;
                ";

            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters =
                {
                    new() { Value = m},
                    new() { Value = nStart},
                    new() { Value = nEnd}
                }
            };
            var reader = await command.ExecuteReaderAsync();
            var results = new List<SyncUser>();
            while (await reader.ReadAsync())
            {
                results.Add(new SyncUser
                    {
                        Id = reader["id"] as int? ?? default,
                        Acct = reader["acct"] as string,
                        LastSync = reader["lastSync"] as DateTime? ?? default,
                    }
                );

            }
            return results.ToArray();
        }
}