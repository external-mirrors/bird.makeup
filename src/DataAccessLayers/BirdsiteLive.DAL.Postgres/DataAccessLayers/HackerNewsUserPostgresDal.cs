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
            string query = @$"
                SELECT id, acct, lastsync, extradata
                FROM {tableName}
                ORDER BY
                    CASE
                        WHEN acct = 'frontpage' THEN 0
                        ELSE 1
                    END,
                    lastsync ASC
                LIMIT 20
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
                var extradata = JsonDocument.Parse(reader["extradata"] as string ?? "{}").RootElement;
                results.Add(new SyncUser
                    {
                        Id = reader["id"] as int? ?? default,
                        Acct = reader["acct"] as string,
                        LastSync = reader["lastSync"] as DateTime? ?? default,
                        ExtraData = extradata,
                    }
                );

            }
            return results.ToArray();
        }
}