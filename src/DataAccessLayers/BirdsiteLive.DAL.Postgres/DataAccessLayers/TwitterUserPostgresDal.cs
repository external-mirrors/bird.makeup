using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Models;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.DAL.Postgres.Settings;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace BirdsiteLive.DAL.Postgres.DataAccessLayers
{
    public class TwitterUserPostgresDal : SocialMediaUserPostgresDal, ITwitterUserDal
    {
        #region Ctor
        public TwitterUserPostgresDal(PostgresSettings settings) : base(settings)
        {
            PostCacheTableName = _settings.CachedTweetsTableName;
            tableName = _settings.TwitterUserTableName;
        }
        #endregion

        public sealed override string tableName { get; set; }
        public sealed override string PostCacheTableName { get; set; }
        public override string FollowingColumnName { get; set; } = "followings";

        public async override Task<SyncUser[]> GetNextUsersToCrawlAsync(int nStart, int nEnd, int m)
        {
            SyncUser[] users = await GetAllTwitterUsersWithFollowersAsync(2000, nStart, nEnd, m);
            return users;
        }

        public async Task<SyncTwitterUser> GetUserAsync(int id)
        {
            return await GetUserAsync(null, id);
        }
        public async Task<SyncTwitterUser> GetUserAsync(string acct)
        {
            return await GetUserAsync(acct, null);
        }
        private async Task<SyncTwitterUser> GetUserAsync(string acct, int? id)
        {
            var query = $@"SELECT id, acct, twitterUserId, lastTweetPostedId, lastSync, fetchingErrorCount, statusescount, extradata, wikidata,
                ( SELECT COUNT(*) FROM {_settings.FollowersTableName} WHERE followings @> ARRAY[{tableName}.id]) as followersCount 
                FROM {tableName} WHERE acct = $1 OR id = $2";

            if (acct is not null)
                acct = acct.ToLowerInvariant();

            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters =
                {
                    new() { Value = (object) acct ?? DBNull.Value},
                    new() { Value = (object) id ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Integer}
                },
            };
            var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;
            
            var extradata = JsonDocument.Parse(reader["extradata"] as string ?? "{}").RootElement;
            WikidataEntry wikidata = null;
            if ((reader["wikidata"] as string) is not null)
                wikidata = JsonSerializer.Deserialize<WikidataEntry>(reader["wikidata"] as string);
            return new SyncTwitterUser
            {
                Id = reader["id"] as int? ?? default,
                Acct = reader["acct"] as string,
                TwitterUserId = reader["twitterUserId"] as long? ?? default,
                LastTweetPostedId = reader["lastTweetPostedId"] as long? ?? default,
                LastSync = reader["lastSync"] as DateTime? ?? default,
                FetchingErrorCount = reader["fetchingErrorCount"] as int? ?? default,
                StatusesCount = reader["statusescount"] as int? ?? -1,
                Followers = reader["followersCount"] as long? ?? default,
                ExtraData = extradata,
                Wikidata = wikidata,
            };

        }

        public async Task<int> GetTwitterUsersCountAsync()
        {
            var query = $"SELECT COUNT(*) FROM (SELECT unnest(followings) as follow FROM {_settings.FollowersTableName} GROUP BY follow) AS f INNER JOIN {_settings.TwitterUserTableName} ON f.follow={_settings.TwitterUserTableName}.id";

            using (var dbConnection = Connection)
            {
                var result = (await dbConnection.QueryAsync<int>(query)).FirstOrDefault();
                return result;
            }
        }

        public async Task<int> GetFailingTwitterUsersCountAsync()
        {
            var query = $"SELECT COUNT(*) FROM {_settings.TwitterUserTableName} WHERE fetchingErrorCount > 0";

            using (var dbConnection = Connection)
            {
                var result = (await dbConnection.QueryAsync<int>(query)).FirstOrDefault();
                return result;
            }
        }

        public async Task<SyncTwitterUser[]> GetAllTwitterUsersWithFollowersAsync(int maxNumber, int nStart, int nEnd, int m)
        {
            string query = @$"
                WITH following AS (
                    SELECT unnest(followings) as follow FROM {_settings.FollowersTableName}
                ),
                following2 AS (
                    SELECT id, lastsync, count(*) as followers FROM following
                    INNER JOIN {_settings.TwitterUserTableName} ON following.follow={_settings.TwitterUserTableName}.id
                    WHERE mod(id, $2) >= $3 AND mod(id, $2) <= $4
                        AND (extract(dow from now()) = 1 OR (NOW() - extradata['latest_post_date']::TEXT::date) < INTERVAL '6 weeks')
                    GROUP BY id
                    ORDER BY lastSync ASC NULLS FIRST
                    LIMIT $1
                )
                UPDATE {_settings.TwitterUserTableName}
                SET lastsync = NOW()
                FROM following2
                WHERE following2.id = {_settings.TwitterUserTableName}.id
                RETURNING {_settings.TwitterUserTableName}.id, acct, twitterUserId, lastTweetPostedId, {_settings.TwitterUserTableName}.lastSync, fetchingErrorCount, statusescount, followers, extradata, wikidata
                ";

            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters =
                {
                    new() { Value = maxNumber},
                    new() { Value = m},
                    new() { Value = nStart},
                    new() { Value = nEnd}
                }
            };
            var reader = await command.ExecuteReaderAsync();
            var results = new List<SyncTwitterUser>();
            while (await reader.ReadAsync())
            {
                WikidataEntry wikidata = null;
                if ((reader["wikidata"] as string) is not null)
                    wikidata = JsonSerializer.Deserialize<WikidataEntry>(reader["wikidata"] as string);
                results.Add(new SyncTwitterUser
                    {
                        Id = reader["id"] as int? ?? default,
                        Acct = reader["acct"] as string,
                        TwitterUserId = reader["twitterUserId"] as long? ?? default,
                        LastTweetPostedId = reader["lastTweetPostedId"] as long? ?? default,
                        LastSync = reader["lastSync"] as DateTime? ?? default,
                        FetchingErrorCount = reader["fetchingErrorCount"] as int? ?? default,
                        Followers = reader["followers"] as long? ?? default,
                        StatusesCount = reader["statusescount"] as int? ?? -1,
                        ExtraData = JsonDocument.Parse(reader["extradata"] as string ?? "{}").RootElement,
                        Wikidata = wikidata,
                    }
                );

            }
            return results.ToArray();
        }

        public async Task<SyncTwitterUser[]> GetAllTwitterUsersAsync(int maxNumber)
        {
            var query = $"SELECT * FROM {_settings.TwitterUserTableName} ORDER BY lastSync ASC NULLS FIRST LIMIT @maxNumber";

            using (var dbConnection = Connection)
            {
                var result = await dbConnection.QueryAsync<SyncTwitterUser>(query, new { maxNumber });
                return result.ToArray();
            }
        }

        public async Task<SyncTwitterUser[]> GetAllTwitterUsersAsync()
        {
            var query = $"SELECT id, acct, lasttweetpostedid, lastsync, fetchingerrorcount, twitteruserid, statusescount FROM {_settings.TwitterUserTableName}";

            using (var dbConnection = Connection)
            {
                var result = await dbConnection.QueryAsync<SyncTwitterUser>(query);
                return result.ToArray();
            }
        }

        public async Task<CrawlAccount[]> GetTwitterCrawlUsersAsync(string worker)
        {
            var query = $"SELECT *, extradata->>'backend' FROM {_settings.TwitterCrawlingUserTableName} WHERE reservedto = @worker";

            using (var dbConnection = Connection)
            {
                var result = await dbConnection.QueryAsync<CrawlAccount>(query, new { worker });
                return result.ToArray();
            }
        }


        public async Task UpdateTwitterUserIdAsync(string username, long twitterUserId)
        {
            if(username == default) throw new ArgumentException("id");
            if(twitterUserId == default) throw new ArgumentException("twitterUserId");

            var query = $"UPDATE {_settings.TwitterUserTableName} SET twitterUserId = $1 WHERE acct = $2";
            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = { new() { Value = twitterUserId}, new() { Value = username}}
            };

            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateTwitterStatusesCountAsync(string username, long StatusesCount)
        {
            if(username == default) throw new ArgumentException("id");
            if(StatusesCount == default) throw new ArgumentException("statuses count");

            var query = $"UPDATE {_settings.TwitterUserTableName} SET statusescount = $1 WHERE acct = $2";
            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = { new() { Value = StatusesCount}, new() { Value = username}}
            };

            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateTwitterUserAsync(int id, long lastTweetPostedId, int fetchingErrorCount, DateTime lastSync)
        {
            if(id == default) throw new ArgumentException("id");
            if(lastTweetPostedId == default) throw new ArgumentException("lastTweetPostedId");
            if(lastSync == default) throw new ArgumentException("lastSync");

            var query = $"UPDATE {_settings.TwitterUserTableName} SET lastTweetPostedId = $1, fetchingErrorCount = $2, lastSync = $3 WHERE id = $4";

            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = { 
                    new() { Value = lastTweetPostedId}, 
                    new() { Value = fetchingErrorCount},
                    new() { Value = lastSync.ToUniversalTime()},
                    new() { Value = id},
                }
            };

            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateTwitterUserAsync(SyncTwitterUser user)
        {
            await UpdateTwitterUserAsync(user.Id, user.LastTweetPostedId, user.FetchingErrorCount, user.LastSync);
        }

    }
}