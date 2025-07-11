using System;
using System.Threading.Tasks;
using BirdsiteLive.Common.Exceptions;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using Microsoft.Extensions.Caching.Memory;

namespace BirdsiteLive.Domain;

public class SocialNetworkCache
{
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
            catch (UserNotFoundException _)
            {
                return null;
            }
        }
        return null;
    }
    public void BackfillPostCache<T>(T post) where T : SocialMediaPost
    {
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
            catch (UserNotFoundException _)
            {
                return null;
            }
        }
        return null;
    }
}