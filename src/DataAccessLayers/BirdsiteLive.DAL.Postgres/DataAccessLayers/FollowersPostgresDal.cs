#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8613, CS8618, CS8619, CS8620, CS8621, CS8625, CS8629, CS8631, CS8634
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.DAL.Postgres.DataAccessLayers.Base;
using BirdsiteLive.DAL.Postgres.Settings;
using Dapper;
using System.Text.Json;
using BirdsiteLive.Common.Models;
using Npgsql;

namespace BirdsiteLive.DAL.Postgres.DataAccessLayers
{
    public class FollowersPostgresDal : PostgresBase, IFollowersDal
    {
        #region Ctor
        public FollowersPostgresDal(PostgresSettings settings) : base(settings)
        {
            
        }
        #endregion

        public async Task CreateFollowerAsync(string acct, string host, string inboxRoute, string sharedInboxRoute, string actorId, int[] followings = null!)
        {
            if(followings == null) followings = new int[0];

            acct = acct.ToLowerInvariant();
            host = host.ToLowerInvariant();

            using (var dbConnection = Connection)
            {
                await dbConnection.ExecuteAsync(
                    $"INSERT INTO {_settings.FollowersTableName} (acct,host,inboxRoute,sharedInboxRoute,followings,actorId) VALUES(@acct,@host,@inboxRoute,@sharedInboxRoute,@followings,@actorId)",
                    new { acct, host, inboxRoute, sharedInboxRoute, followings, actorId });
            }
        }

        public async Task<int> GetFollowersCountAsync()
        {
            var query = $"SELECT COUNT(*) FROM {_settings.FollowersTableName}";

            using (var dbConnection = Connection)
            {
                var result = (await dbConnection.QueryAsync<int>(query)).FirstOrDefault();
                return result;
            }
        }

        public async Task<int> GetFailingFollowersCountAsync()
        {
            var query = $"SELECT COUNT(*) FROM {_settings.FollowersTableName} WHERE postingErrorCount > 0";

            using (var dbConnection = Connection)
            {
                var result = (await dbConnection.QueryAsync<int>(query)).FirstOrDefault();
                return result;
            }
        }

        public async Task<Follower> GetFollowerAsync(string acct, string host)
        {
            var query = $"SELECT * FROM {_settings.FollowersTableName} WHERE acct = $1 AND host = $2";

            acct = acct.ToLowerInvariant();
            host = host.ToLowerInvariant();

            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = { new() { Value = acct}, new() { Value = host }},
            };
            var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null!;

            var twitterFollowings = (reader["followings"] as int[] ?? new int[0]).ToList();
            var igFollowings = (reader["followings_instagram"] as int[] ?? new int[0]).ToList();
            
            return new Follower
            {
                Id = reader["id"] as int? ?? default,
                Followings = twitterFollowings,
                TotalFollowings = twitterFollowings.Count + igFollowings.Count,
                ActorId = reader["actorId"] as string,
                Acct = reader["acct"] as string,
                Host = reader["host"] as string,
                InboxRoute = reader["inboxRoute"] as string,
                SharedInboxRoute = reader["sharedInboxRoute"] as string,
                PostingErrorCount = reader["postingErrorCount"] as int? ?? default,
            };
            
        }


        public async Task<Follower[]> GetAllFollowersAsync()
        {
            var query = $"SELECT * FROM {_settings.FollowersTableName}";

            using (var dbConnection = Connection)
            {
                var result = await dbConnection.QueryAsync<SerializedFollower>(query);
                return result.Select(Convert).ToArray();
            }
        }

        public async Task UpdateFollowerErrorCountAsync(int followerid, int count)
        {
            if (followerid == default) throw new ArgumentException("id");

            var query = $"UPDATE {_settings.FollowersTableName} SET postingErrorCount = $1 WHERE id = $2";
            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = { 
                    new() { Value = count}, 
                    new() { Value = followerid}
                }
            };

            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteFollowerAsync(int id)
        {
            if (id == default) throw new ArgumentException("id");
            
            var query = $"DELETE FROM {_settings.FollowersTableName} WHERE id = @id";

            using (var dbConnection = Connection)
            {
                await dbConnection.QueryAsync(query, new { id });
            }
        }

        public async Task DeleteFollowerAsync(string acct, string host)
        {
            if (string.IsNullOrWhiteSpace(acct)) throw new ArgumentException("acct");
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("host");

            acct = acct.ToLowerInvariant();
            host = host.ToLowerInvariant();

            var query = $"DELETE FROM {_settings.FollowersTableName} WHERE acct = @acct AND host = @host";

            using (var dbConnection = Connection)
            {
                await dbConnection.QueryAsync(query, new { acct, host });
            }
        }

        private Follower Convert(SerializedFollower follower)
        {
            if (follower == null) return null;

            return new Follower()
            {
                Id = follower.Id,
                Acct = follower.Acct,
                Host = follower.Host,
                InboxRoute = follower.InboxRoute,
                ActorId = follower.ActorId,
                SharedInboxRoute = follower.SharedInboxRoute,
                Followings = follower.Followings.ToList(),
                PostingErrorCount = follower.PostingErrorCount
            };
        }
    }

    internal class SerializedFollower {
        public int Id { get; set; }
        public int[] Followings { get; set; } = null!;
        public string Acct { get; set; } = null!;
        public string Host { get; set; } = null!;
        public string InboxRoute { get; set; } = null!;
        public string SharedInboxRoute { get; set; } = null!;
        public string ActorId { get; set; } = null!;
        public int PostingErrorCount { get; set; }
    }
}
