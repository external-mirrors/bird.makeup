using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Models;
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
                WITH followings AS (SELECT unnest({FollowingColumnName}) as fid, count(*), bool_or(host = 'r.town') as vip FROM {_settings.FollowersTableName} GROUP BY fid)
                SELECT id, acct, lastsync, extradata, wikidata
                FROM {_settings.InstagramUserTableName}
                WHERE id IN (SELECT fid FROM followings WHERE vip = true)
                OR (
                        id IN (SELECT fid FROM followings WHERE vip = false and followings.count > 2)
                        AND {_settings.InstagramUserTableName}.wikidata is not null
                    )
                OR ( extract(dow from now()) = 0
                     AND id IN (SELECT fid FROM followings WHERE followings.count > 0)
                   )
                ORDER BY lastsync ASC
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
                WikidataEntry wikidata = null;
                if ((reader["wikidata"] as string) is not null)
                    wikidata = JsonSerializer.Deserialize<WikidataEntry>(reader["wikidata"] as string);
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