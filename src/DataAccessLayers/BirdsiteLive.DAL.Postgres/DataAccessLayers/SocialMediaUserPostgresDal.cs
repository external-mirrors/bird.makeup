using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Models;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.DAL.Postgres.DataAccessLayers.Base;
using BirdsiteLive.DAL.Postgres.Settings;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace BirdsiteLive.DAL.Postgres.DataAccessLayers;

public abstract class SocialMediaUserPostgresDal : PostgresBase, SocialMediaUserDal
{
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            IgnoreReadOnlyProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    
        #region Ctor
        public SocialMediaUserPostgresDal(PostgresSettings settings) : base(settings)
        {
            
        }
        #endregion

        public abstract string tableName { get; set; }
        public abstract string PostCacheTableName { get; set; }
        public abstract string FollowingColumnName { get; set; }
        
        public abstract Task<SyncUser[]> GetNextUsersToCrawlAsync(int nStart, int nEnd, int m);

        public async Task<SyncUser> GetUserAsync(int id)
        {
            return await GetUserAsync(null, id);
        }
        public async Task<SyncUser> GetUserAsync(string acct)
        {
            return await GetUserAsync(acct, null);
        }

        public async Task<SyncUser[]> GetAllUsersAsync()
        {
            var query = $"SELECT id, acct, lastsync FROM {tableName}";

            using (var dbConnection = Connection)
            {
                var result = await dbConnection.QueryAsync<SyncUser>(query);
                return result.ToArray();
            }
        }

        private async Task<SyncUser> GetUserAsync(string acct, int? id)
        {
            var query = $"SELECT *, ( SELECT COUNT(*) FROM {_settings.FollowersTableName} WHERE {FollowingColumnName} @> ARRAY[{tableName}.id]) as followersCount FROM {tableName} WHERE acct = $1 OR id = $2";

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
            return new SyncUser
            {
                Id = reader["id"] as int? ?? default,
                Acct = reader["acct"] as string,
                Followers = reader["followersCount"] as long? ?? default,
                ExtraData = extradata,
            };

        }
        public async Task<string> GetUserCacheAsync(string username)
        {
            var query = $"SELECT cache FROM {tableName} WHERE acct = $1";

            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters =
                {
                    new() { Value = username },
                },
            };
            var reader = await command.ExecuteReaderAsync();
            if (!reader.HasRows)
                return null;
            await reader.ReadAsync();
            
            var cache = reader["cache"] as string;
            return cache;
        }

        public async Task<string> GetPostCacheAsync(string post)
        {
            var query = $"SELECT data FROM {PostCacheTableName} WHERE id = $1";

            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters =
                {
                    new() { Value = post },
                },
            };
            var reader = await command.ExecuteReaderAsync();
            if (!reader.HasRows)
                return null;
            await reader.ReadAsync();
            
            var cache = reader["data"] as string;
            return cache;
        }
        public async Task UpdatePostCacheAsync(SocialMediaPost post)
        {
            var query = $"""
                INSERT INTO {PostCacheTableName} (id, acct, data, lastsync)
                VALUES ($1, $2, $3, NOW())
                ON CONFLICT (id)
                DO NOTHING 
""";
            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = 
                { 
                    new() { Value = post.Id},
                    new() { Value = post.Author.Acct},
                    new() { Value = JsonSerializer.Serialize(post, _jsonOptions), NpgsqlDbType = NpgsqlDbType.Jsonb },
                }
            };

            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateUserLastSyncAsync(SyncUser user)
        {
            var query = $"UPDATE {tableName} SET lastsync = NOW() WHERE acct = $1";
            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = 
                { 
                    new() { Value = user.Acct},
                }
            };

            await command.ExecuteNonQueryAsync();
        }
        public async Task ClearUserCacheAsync(string user)
        {
            var query = $"UPDATE {tableName} SET cache = null WHERE acct = $1";
            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = 
                { 
                    new() { Value = user},
                }
            };

            await command.ExecuteNonQueryAsync();
        }
        public async Task UpdateUserCacheAsync(SocialMediaUser? cache)
        {
            var query = $"UPDATE {tableName} SET cache = $1 WHERE acct = $2";
            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = 
                { 
                    new() { Value = JsonSerializer.Serialize(cache, _jsonOptions), NpgsqlDbType = NpgsqlDbType.Jsonb },
                    new() { Value = cache.Acct},
                }
            };

            await command.ExecuteNonQueryAsync();
        }
        public async Task<long> GetFollowersCountAsync(int id)
        {
            var query = $"SELECT COUNT({FollowingColumnName}) FROM {_settings.FollowersTableName} WHERE {FollowingColumnName} @> ARRAY[$1]";

            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters =
                {
                    new() { Value = id },
                },
            };
            var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            
            var count = reader["count"] as long ? ?? default ;
            return count;
        }

        public async Task<Follower[]> GetFollowersAsync(int id)
        {
            if (id == default) throw new ArgumentException("followedUserId");

            var query = $"SELECT * FROM {_settings.FollowersTableName} WHERE {FollowingColumnName} @> ARRAY[$1]";

            await using var connection = await DataSource.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = { new() { Value = id}}
            };
            var reader = await command.ExecuteReaderAsync();

            var followers = new List<Follower>();
            while (await reader.ReadAsync())
            {
                followers.Add(new Follower
                {
                    Id = reader["id"] as int? ?? default,
                    Followings = (reader["followings"] as int[] ?? new int[0]).ToList(),
                    ActorId = reader["actorId"] as string,
                    Acct = reader["acct"] as string,
                    Host = reader["host"] as string,
                    InboxRoute = reader["inboxRoute"] as string,
                    SharedInboxRoute = reader["sharedInboxRoute"] as string,
                    PostingErrorCount = reader["postingErrorCount"] as int? ?? default,
                });
            }
            
            return followers.ToArray();
        }

        public async Task DeleteUserAsync(string acct)
        {
            if (string.IsNullOrWhiteSpace(acct)) throw new ArgumentException("acct");

            acct = acct.ToLowerInvariant();

            var query = $"DELETE FROM {tableName} WHERE acct = $1";

            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = 
                { 
                    new() { Value = acct },
                }
            };

            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteUserAsync(int id)
        {
            if (id == default) throw new ArgumentException("id");
            
            var query = $"DELETE FROM {tableName} WHERE id = $1";

            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = 
                { 
                    new() { Value = id },
                }
            };

            await command.ExecuteNonQueryAsync();
        }
        public async Task CreateUserAsync(string acct)
        {
            acct = acct.ToLowerInvariant();

            var query = $"INSERT INTO {tableName} (acct,lastTweetPostedId) VALUES($1,-1)";
            // TODO improve this
            if (tableName == _settings.InstagramUserTableName)
                query = $"INSERT INTO {tableName} (acct) VALUES($1)";
                
            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = 
                { 
                    new() { Value = acct },
                }
            };
            
            await command.ExecuteNonQueryAsync();
        }
        public async Task AddFollower(int follower, int followed)
        {

            var query = $"UPDATE {_settings.FollowersTableName} SET {FollowingColumnName} = array_append({FollowingColumnName}, $2) WHERE id = $1";
                
            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = 
                { 
                    new() { Value = follower },
                    new() { Value = followed },
                }
            };
            
            await command.ExecuteNonQueryAsync();
        }
        public async Task RemoveFollower(int follower, int followed)
        {

            var query = $"UPDATE {_settings.FollowersTableName} SET {FollowingColumnName} = array_remove({FollowingColumnName}, $2) WHERE id = $1";
                
            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = 
                { 
                    new() { Value = follower },
                    new() { Value = followed },
                }
            };
            
            await command.ExecuteNonQueryAsync();
        }
        public async Task UpdateUserExtradataAsync(string username, string key, string subkey, object value)
        {
            if(username == default) throw new ArgumentException("id");

            var query = $"UPDATE {tableName} SET extradata[$4][$1] = $2 WHERE acct = $3";
            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = 
                { 
                    new() { Value = subkey},
                    new() { Value = JsonSerializer.Serialize(value), NpgsqlDbType = NpgsqlDbType.Jsonb },
                    new() { Value = username},
                    new() { Value = key},
                    
                }
            };

            await command.ExecuteNonQueryAsync();
        }
        public async Task UpdateUserExtradataAsync(string username, string key, object value)
        {
            if(username == default) throw new ArgumentException("id");

            var query = $"UPDATE {tableName} SET extradata[$1] = $2 WHERE acct = $3";
            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters = 
                { 
                    new() { Value = key},
                    new() { Value = JsonSerializer.Serialize(value), NpgsqlDbType = NpgsqlDbType.Jsonb },
                    new() { Value = username},
                    
                }
            };

            await command.ExecuteNonQueryAsync();
        }
        public async Task UpdateUsersWikidataAsync(Dictionary<string, object> values)
        {
            
            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            await using var clearCommand = new NpgsqlCommand($"UPDATE {tableName} SET wikidata = null;", connection);
            await clearCommand.ExecuteNonQueryAsync();
            
            foreach ((string username, object value) in values)
            {
                if(username == default)
                    continue;
                Console.WriteLine($"\r writing {username}"); 
                var query = $"""
                    INSERT INTO {tableName} (acct, wikidata)
                    VALUES ($1, $2)
                    ON CONFLICT (acct)
                    DO UPDATE
                        SET wikidata = EXCLUDED.wikidata
                    """;
                await using var command = new NpgsqlCommand(query, connection) {
                    Parameters = 
                    { 
                        new() { Value = username},
                        new() { Value = JsonSerializer.Serialize(value), NpgsqlDbType = NpgsqlDbType.Jsonb },
                        
                    }
                };

                await command.ExecuteNonQueryAsync();
            }
            Console.WriteLine($"commiting for {tableName}"); 
            await transaction.CommitAsync();
        }
        public async Task<string> GetUserExtradataAsync(string acct, string key)
        {
            var query = $"SELECT extradata[$2] FROM {tableName} WHERE acct = $1";

            if (acct is not null)
                acct = acct.ToLowerInvariant();

            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters =
                {
                    new() { Value = acct },
                    new() { Value = key }
                },
            };
            var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            var extradata = reader["extradata"] as string ?? "{}";
            return extradata;
        }
        public async Task<string> GetUserWikidataAsync(string acct)
        {
            var query = $"SELECT wikidata FROM {tableName} WHERE acct = $1";

            if (acct is not null)
                acct = acct.ToLowerInvariant();

            await using var connection = DataSource.CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(query, connection) {
                Parameters =
                {
                    new() { Value = acct },
                },
            };
            var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            var extradata = reader["wikidata"] as string ?? "{}";
            return extradata;
        }
}