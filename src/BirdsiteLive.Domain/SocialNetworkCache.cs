using System;
using System.Diagnostics.Metrics;
using System.Net.Http;
using System.Threading.Tasks;
using BirdsiteLive.Common.Exceptions;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using Microsoft.Extensions.Caching.Memory;

namespace BirdsiteLive.Domain;

public class SocialNetworkCache
{
    static Meter _meter = new("DotMakeup", "1.0.0");
    private ObservableGauge<long> _userCount; 
    private ObservableGauge<long> _userCacheHit; 
    private ObservableGauge<long> _userCacheMiss; 
    private ObservableGauge<long> _postCount; 
    private ObservableGauge<long> _postCacheHit; 
    private ObservableGauge<long> _postCacheMiss; 
    
    private readonly InstanceSettings _instanceSettings;
    private readonly MemoryCache _userCache;
    private readonly MemoryCache _postCache;
    private readonly MemoryCacheEntryOptions _cacheEntryOptions = new MemoryCacheEntryOptions()
        .SetSize(1)//Size amount
        //Priority on removing when reaching size limit (memory pressure)
        .SetPriority(CacheItemPriority.Low)
        // Keep in cache for this time, reset time if accessed.
        .SetSlidingExpiration(TimeSpan.FromHours(16))
        // Remove from cache after this time, regardless of sliding expiration
        .SetAbsoluteExpiration(TimeSpan.FromHours(24));

    private readonly MemoryCacheEntryOptions _cacheEntryOptionsError = new MemoryCacheEntryOptions()
        .SetSize(1)//Size amount
        //Priority on removing when reaching size limit (memory pressure)
        .SetPriority(CacheItemPriority.Low)
        // Keep in cache for this time, reset time if accessed.
        .SetSlidingExpiration(TimeSpan.FromHours(5))
        // Remove from cache after this time, regardless of sliding expiration
        .SetAbsoluteExpiration(TimeSpan.FromHours(30));
    public SocialNetworkCache(InstanceSettings instanceSettings)
    {
        _instanceSettings = instanceSettings;
        _userCache = new MemoryCache(new MemoryCacheOptions()
        {
            SizeLimit = instanceSettings.UserCacheCapacity,
            TrackStatistics = true
        });
        _postCache = new MemoryCache(new MemoryCacheOptions()
        {
            SizeLimit = instanceSettings.TweetCacheCapacity,
            TrackStatistics = true
        });
        _userCount = _meter.CreateObservableGauge<long>("dotmakeup_cache_user_count", () => _userCache.GetCurrentStatistics()!.CurrentEntryCount, "Number of entries in user cache" );
        _userCacheHit = _meter.CreateObservableGauge<long>("dotmakeup_cache_user_count", () => _userCache.GetCurrentStatistics()!.TotalHits, "Number of user cache hits" );
        _userCacheMiss = _meter.CreateObservableGauge<long>("dotmakeup_cache_user_count", () => _userCache.GetCurrentStatistics()!.TotalMisses, "Number of user cache misses" );
        _postCount = _meter.CreateObservableGauge<long>("dotmakeup_cache_post_count", () => _postCache.GetCurrentStatistics()!.CurrentEntryCount, "Number of entries in post cache" );
        _postCacheHit = _meter.CreateObservableGauge<long>("dotmakeup_cache_post_count", () => _postCache.GetCurrentStatistics()!.TotalHits, "Number of post cache hits" );
        _postCacheMiss = _meter.CreateObservableGauge<long>("dotmakeup_cache_post_count", () => _postCache.GetCurrentStatistics()!.TotalMisses, "Number of user cache misses" );
    }

    public void BackfillUserCache<T>(T user) where T : SocialMediaUser
    {
        _userCache.Set(user.Acct, Task.FromResult(user), _cacheEntryOptions);
    }

    public Task<T> GetUser<T>(string username, Func<Task<T>>[] getSocialMediaUser) where T : SocialMediaUser
    {
        if (_userCache.TryGetValue(username, out Task<T> cachedUser))
            return cachedUser; 
        
        var p = _processUser(getSocialMediaUser);
        _userCache.Set(username, p, _cacheEntryOptions);

        return p;
    }

    private async Task<T> _processUser<T>(Func<Task<T>>[] getSocialMediaUser) where T : SocialMediaUser
    {
        foreach (var getSocialMediaUserFunc in getSocialMediaUser)
        {
            try
            {
                var u = await getSocialMediaUserFunc();
                if (u is not null)
                    return u;

            }
            catch (RateLimitExceededException _)
            {
            }
            catch (HttpRequestException _)
            {
            }
            catch (UserNotFoundException _)
            {
                throw;
            }
        }
        return null;
    }

    public void BackfillPostCache<T>(T post) where T : SocialMediaPost
    {
        if (_userCache.TryGetValue(post.Author.Acct, out var cachedObj))
        {
            // Try to get the result from the cached task regardless of its generic type
            if (cachedObj is Task cachedTask && cachedTask.IsCompletedSuccessfully)
            {
                var result = cachedTask.GetType().GetProperty("Result")?.GetValue(cachedTask);
                if (result is SocialMediaUser cachedUser)
                {
                    post.Author = cachedUser;
                }
            }
        }
        _postCache.Set(post.Id, Task.FromResult(post), _cacheEntryOptions);
    }

    public Task<T> GetPost<T>(string id, Func<Task<T>>[] getSocialMediaPost) where T : class, SocialMediaPost
    {
        if (_postCache.TryGetValue(id, out Task<T> cachedPost))
            return cachedPost; 
        
        var p = _processPost(getSocialMediaPost);
        _postCache.Set(id, p, _cacheEntryOptions);

        return p;
    }

    private async Task<T> _processPost<T>(Func<Task<T>>[] getSocialMediaPost) where T : class, SocialMediaPost
    {
        foreach (var getSocialMediaPostFunc in getSocialMediaPost)
        {
            try
            {
                var u = await getSocialMediaPostFunc();
                if (u is not null)
                    return u;

            }
            catch (RateLimitExceededException _)
            {
            }
            catch (HttpRequestException _)
            {
            }
            catch (UserNotFoundException _)
            {
                throw;
            }
        }
        return null;
    }
}