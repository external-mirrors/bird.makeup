using System;
using System.Text.Json;
using System.Threading.Tasks;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Postgres.DataAccessLayers.Base;
using BirdsiteLive.DAL.Postgres.Settings;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using NpgsqlTypes;

namespace BirdsiteLive.DAL.Postgres.DataAccessLayers;

public class SettingsPostgresDal : PostgresBase, ISettingsDal
{
    public SettingsPostgresDal(PostgresSettings settings) : base(settings)
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

        private readonly MemoryCache _cache;
        private readonly MemoryCacheEntryOptions _cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSize(1)//Size amount
            //Priority on removing when reaching size limit (memory pressure)
            .SetPriority(CacheItemPriority.High)
            // Keep in cache for this time, reset time if accessed.
            .SetSlidingExpiration(TimeSpan.FromMinutes(3))
            // Remove from cache after this time, regardless of sliding expiration
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
    public async Task<JsonElement?> Get(string key)
    {
        if (_cache.TryGetValue(key, out JsonElement cachedRes))
            return cachedRes;
        
        var query = $"SELECT setting_key, setting_value FROM {_settings.SettingTableName} WHERE setting_key = $1";

        await using var connection = DataSource.CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(query, connection) {
            Parameters = { new() { Value = key}}
        };
        var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        if (reader["setting_value"] as string is null)
            return null;
        
        var res = JsonDocument.Parse((reader["setting_value"] as string)!).RootElement;
        _cache.Set(key, res, _cacheEntryOptions);
        
        await reader.CloseAsync();
        
        return res;
    }
    public async Task Set(string key, JsonElement? value)
    {
        var query =
            $"INSERT INTO {_settings.SettingTableName} VALUES ($1,$2) ON CONFLICT (setting_key) DO UPDATE SET setting_value = EXCLUDED.setting_value;";

        await using var connection = DataSource.CreateConnection();
        await connection.OpenAsync();

        if (value is null)
            value = JsonDocument.Parse("{}").RootElement;
        
        await using var command = new NpgsqlCommand(query, connection) {
            Parameters =
            {
                new() { Value = key},
                new() { Value = value.ToString(), NpgsqlDbType = NpgsqlDbType.Jsonb}
            }
        };
        var reader = await command.ExecuteReaderAsync();
    }
}
