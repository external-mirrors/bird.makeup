using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Common.Exceptions;
using BirdsiteLive.Instagram.Models;
using dotMakeup.ipfs;
using dotMakeup.Instagram.Models;
using Microsoft.Extensions.Caching.Memory;

namespace dotMakeup.Instagram;

public class InstagramService : ISocialMediaService
{
        static Meter _meter = new("DotMakeup", "1.0.0");
        static Counter<int> _newPosts = _meter.CreateCounter<int>("dotmakeup_ig_new_posts_count");
        
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly InstanceSettings _settings;
        private readonly ISettingsDal _settingsDal;
        private readonly IInstagramUserDal _instagramUserDal;
        private readonly IIpfsService _ipfs;
        
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
            .SetSlidingExpiration(TimeSpan.FromMinutes(5))
            // Remove from cache after this time, regardless of sliding expiration
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

        private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions()
        {
            Converters = { new InstagramSocialMediaUserConverter() }
        };

        #region Ctor
        public InstagramService(IIpfsService ipfs, IInstagramUserDal userDal, IHttpClientFactory httpClientFactory, InstanceSettings settings, ISettingsDal settingsDal)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings;
            _settingsDal = settingsDal;
            _instagramUserDal = userDal;
            _ipfs = ipfs;
            UserDal = userDal;
            
            _userCache = new MemoryCache(new MemoryCacheOptions()
            {
                SizeLimit = settings.UserCacheCapacity
            });
            _postCache = new MemoryCache(new MemoryCacheOptions()
            {
                SizeLimit = settings.TweetCacheCapacity
            });
        }
        #endregion


        public string ServiceName { get; } = "Instagram";
        public Regex ValidUsername { get;  } = new Regex(@"^[a-zA-Z0-9_\.]{1,30}$");
        public Regex UserMention { get;  } = new Regex(@"(^|.?[ \n\.]+)@([a-zA-Z0-9_\.]+)(?=\s|$|[\[\]<>,;:'\.’!?/—\|-]|(. ))");
        public SocialMediaUserDal UserDal { get; }
        
        public async Task<SocialMediaPost[]> GetNewPosts(SyncUser user)
        {
            var newPosts = new List<SocialMediaPost>();
            var v2 = await GetUserAsync(user.Acct, true);
            if (v2 == null)
                return Array.Empty<SocialMediaPost>();
            
            var profileUrlHash = await _ipfs.Mirror(v2.ProfileUrl, false);
            v2.ProfileUrl = _ipfs.GetIpfsPublicLink(profileUrlHash);
            foreach (var p in v2.RecentPosts)
            {
                if (p.CreatedAt > user.LastSync)
                {
                    if (_settings.IpfsApi is not null)
                    {
                        foreach (ExtractedMedia m in p.Media)
                        {
                            var hash = await _ipfs.Mirror(m.Url, _settings.InstagramCrawlingGroup == "Priority");
                            m.Url = _ipfs.GetIpfsPublicLink(hash);
                        }

                        await _instagramUserDal.UpdatePostCacheAsync(p);
                    }
                    
                    newPosts.Add(p);
                }
            }

            await UserDal.UpdateUserLastSyncAsync(user);
            
            _newPosts.Add(newPosts.Count);
            
            return newPosts.ToArray();
        }

        public async Task<SocialMediaUser?> GetUserAsync(string username)
        {
            return await GetUserAsync(username, false);
        }

        private async Task<InstagramUser?> GetUserAsync(string username, bool forceRefresh)
        {
            JsonElement? accounts = await _settingsDal.Get("ig_allow_list");
            if (accounts is not null && !accounts.Value.EnumerateArray().Any(user => user.GetString() == username))
                throw new UserNotFoundException();

            InstagramUser user;
            
            if (forceRefresh)
            {
                if (_settings.CrawlingSidecarURL is not null)
                    user = await CallSidecar(username, _settings.CrawlingSidecarURL);
                else
                    user = await CallSidecar(username, _settings.SidecarURL);
                await _instagramUserDal.UpdateUserCacheAsync(user);
            }
            else if (!_userCache.TryGetValue(username, out user))
            {
                var userCache = await _instagramUserDal.GetUserCacheAsync(username);
                if (userCache is not null)
                {
                    user = JsonSerializer.Deserialize<InstagramUser>(userCache);
                }
                else
                {
                    user = await CallSidecar(username, _settings.SidecarURL);
                    await _instagramUserDal.UpdateUserCacheAsync(user);
                }
            }

            return user;
        }

        public async Task<SocialMediaPost?> GetPostAsync(string id)
        {
            if (!_postCache.TryGetValue(id, out InstagramPost post))
            {
                var dbCache = await _instagramUserDal.GetPostCacheAsync(id);
                if (dbCache is not null)
                {
                    var x = JsonSerializer.Deserialize<InstagramPost>(dbCache, _serializerOptions);
                    _postCache.Set(id, x, _cacheEntryOptions);
                    return x;
                }
                
                
                var client = _httpClientFactory.CreateClient();
                string requestUrl;
                requestUrl = _settings.SidecarURL + "/instagram/post/" + id;
                var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                var httpResponse = await client.SendAsync(request);

                if (httpResponse.StatusCode != HttpStatusCode.OK)
                {
                    _postCache.Set(id, post, _cacheEntryOptionsError);
                    return null;
                }
                var c = await httpResponse.Content.ReadAsStringAsync();
                var postDoc = JsonDocument.Parse(c);
                post = ParsePost(postDoc.RootElement);
            }

            
            _postCache.Set(id, post, _cacheEntryOptions);
            return post;
        }

        private async Task<InstagramUser> CallSidecar(string username, string sidecarURL)
        {
            InstagramUser user = null;
            var client = _httpClientFactory.CreateClient();
            string requestUrl;
            requestUrl = sidecarURL + "/instagram/user/" + username;
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            var httpResponse = await client.SendAsync(request);

            if (httpResponse.StatusCode != HttpStatusCode.OK)
            {
                _userCache.Set(username, user, _cacheEntryOptionsError);
                throw new RateLimitExceededException();
            }

            var c = await httpResponse.Content.ReadAsStringAsync();
            var userDocument = JsonDocument.Parse(c);

            List<string> pinnedPost = new List<string>();
            List<InstagramPost> recentPost = new List<InstagramPost>();
            foreach (JsonElement postDoc in userDocument.RootElement.GetProperty("posts").EnumerateArray())
            {
                var post = ParsePost(postDoc);
                _postCache.Set(post.Id, post, _cacheEntryOptions);
                if (post.IsPinned)
                    pinnedPost.Add(post.Id);
                else
                    recentPost.Add(post);
            }


            try
            {
                user = new InstagramUser()
                {
                    Description = userDocument.RootElement.GetProperty("bio").GetString(),
                    Acct = username,
                    ProfileImageUrl = userDocument.RootElement.GetProperty("profilePic").GetString(),
                    Name = userDocument.RootElement.GetProperty("name").GetString(),
                    PinnedPosts = pinnedPost,
                    RecentPosts = recentPost,
                    ProfileUrl = "www.instagram.com/" + username,
                };

            }
            catch (KeyNotFoundException _)
            {
                _userCache.Set(username, user, _cacheEntryOptionsError);
                throw new UserNotFoundException();
            }
            _userCache.Set(username, user, _cacheEntryOptions);

            return user;
        }

        private InstagramPost ParsePost(JsonElement postDoc)
        {
                List<ExtractedMedia> media = new List<ExtractedMedia>();
                foreach (JsonElement m in postDoc.GetProperty("media").EnumerateArray())
                {
                    bool isVideo = m.GetProperty("is_video").GetBoolean();
                    if (!isVideo)
                    {
                        media.Add(new ExtractedMedia()
                        {
                            Url = m.GetProperty("url").GetString(),
                            MediaType = "image/jpeg"

                        });

                    }
                    else
                    {
                        media.Add(new ExtractedMedia()
                        {
                            Url = m.GetProperty("video_url").GetString(),
                            MediaType = "video/mp4"

                        });
                        
                    }

                }
                var createdAt = DateTime.Parse(postDoc.GetProperty("date").GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind);
                var post = new InstagramPost()
                    {
                        Id = postDoc.GetProperty("id").GetString(),
                        MessageContent = postDoc.GetProperty("caption").GetString(),
                        Author = new InstagramUser()
                        {
                            Acct = postDoc.GetProperty("user").GetString(),
                        },
                        CreatedAt = createdAt,
                        IsPinned = postDoc.GetProperty("pinned").GetBoolean(),
                        
                        Media = media.ToArray(),
                    };
                return post;
            
        }
        
}