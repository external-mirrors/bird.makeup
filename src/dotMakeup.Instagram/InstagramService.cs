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
        static Counter<int> _apiCalled = _meter.CreateCounter<int>("dotmakeup_api_called_count");
        
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
            .SetSlidingExpiration(TimeSpan.FromHours(5))
            // Remove from cache after this time, regardless of sliding expiration
            .SetAbsoluteExpiration(TimeSpan.FromHours(30));

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


        public string MakeUserNameCanonical(string name)
        {
            return name.Trim().ToLowerInvariant();
        }

        public string ServiceName { get; } = "Instagram";
        public Regex ValidUsername { get;  } = new Regex(@"^[a-zA-Z0-9_\.]{1,30}(?<!\.)$");
        public Regex UserMention { get;  } = new Regex(@"(^|.?[ \n\.]+)@([a-zA-Z0-9_\.]+)(?<!\.)(?=\s|$|[\[\]<>,;:'\.’!?/—\|-]|(. ))");
        public SocialMediaUserDal UserDal { get; }
        
        public async Task<SocialMediaPost[]> GetNewPosts(SyncUser user)
        {
            await UserDal.UpdateUserLastSyncAsync(user);
            
            var newPosts = new List<SocialMediaPost>();
            var v2 = await GetUserAsync(user.Acct, true);
            _apiCalled.Add(1, new KeyValuePair<string, object?>("api", "instagram_sidecar_timeline"),
                new KeyValuePair<string, object?>("result", v2 != null ? "2xx": "5xx")
            );
            if (v2 == null)
                return Array.Empty<SocialMediaPost>();
            
            foreach (var p in v2.RecentPosts)
            {
                if (p.CreatedAt > user.LastPost)
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

            return newPosts.ToArray();
        }

        public async Task<SocialMediaUser?> GetUserAsync(string username)
        {
            var user = await GetUserAsync(username, false);
            return user;
        }

        private async Task<InstagramUser?> GetUserAsync(string username, bool forceRefresh)
        {
            JsonElement? accounts = await _settingsDal.Get("ig_allow_list");
            if (accounts is not null && !accounts.Value.EnumerateArray().Any(user => user.GetString() == username))
                throw new UserNotFoundException();
            
            JsonElement? alwaysRefresh = await _settingsDal.Get("ig_always_refresh");
            if (alwaysRefresh is not null)
                forceRefresh = true;

            InstagramUser user;
            
            if (forceRefresh)
            {
                user = await CallDirect(username);
                
                var profileUrlHash = await _ipfs.Mirror(user.ProfileImageUrl, true);
                user.ProfileImageUrl = _ipfs.GetIpfsPublicLink(profileUrlHash);
                
                await _instagramUserDal.UpdateUserCacheAsync(user);
            }
            else if (!_userCache.TryGetValue(username, out user))
            {
                user = await _instagramUserDal.GetUserCacheAsync<InstagramUser>(username);
                if (user is null)
                {
                    user = await CallSidecar(username, await GetWebSidecar());
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
                requestUrl = (await GetWebSidecar()) + "/instagram/post/" + id;
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
            using var client = _httpClientFactory.CreateClient();
            string requestUrl;
            string method = "user";
            if (sidecarURL == "http://localhost:5000")
                method = "user_2";
            requestUrl = $"{sidecarURL}/instagram/{method}/{username}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            var httpResponse = await client.SendAsync(request);

            _apiCalled.Add(1, new KeyValuePair<string, object>("sidecar", $"ig_{method}"),
                new KeyValuePair<string, object>("status", httpResponse.StatusCode),
                new KeyValuePair<string, object>("domain", sidecarURL)
            );
            
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
                    Url = userDocument.RootElement.GetProperty("Url").GetString(),
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
        private async Task<InstagramUser> CallDirect(string username)
        {
            InstagramUser user = null;
            using var client = _httpClientFactory.CreateClient("WithProxy");
            string requestUrl = $"https://i.instagram.com/api/v1/users/web_profile_info/?username={username}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            request.Headers.Add("User-Agent", 
                "Mozilla/5.0 (X11; Linux x86_64; rv:135.0) Gecko/20100101 Firefox/135.0");
            request.Headers.Add("Accept", "*/*");
            request.Headers.Add("X-IG-App-ID", "936619743392459");

            var response = await client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                _userCache.Set(username, user, _cacheEntryOptionsError);
                throw new RateLimitExceededException();
            }
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();

            using var jsonDocument = JsonDocument.Parse(jsonString);
            var root = jsonDocument.RootElement;

            var userJson = root.GetProperty("data").GetProperty("user");
            string profileUrl = null;
            foreach (var l in userJson.GetProperty("bio_links").EnumerateArray())
            {
                profileUrl = l.GetProperty("url").GetString();
            }
            
            InstagramUser userResult = new InstagramUser()
            {
                Description = userJson.GetProperty("biography_with_entities").GetProperty("raw_text").GetString(),
                Acct = username,
                ProfileImageUrl = userJson.GetProperty("profile_pic_url_hd").GetString(),
                Name = userJson.GetProperty("full_name").GetString(),
                FollowersCount = userJson.GetProperty("edge_followed_by").GetProperty("count").GetInt32(),
                ProfileUrl = "www.instagram.com/" + username,
                Url = profileUrl,
            };

            var postsJson = userJson.GetProperty("edge_owner_to_timeline_media").GetProperty("edges");

            List<InstagramPost> userPosts = new List<InstagramPost>();
            _apiCalled.Add(1, new KeyValuePair<string, object>("sidecar", $"ig_direct"),
                new KeyValuePair<string, object>("status", response.StatusCode)
            );
            
            foreach (var post in postsJson.EnumerateArray())
            {
                try
                {
                    var node = post.GetProperty("node");
                    var mediaItems = new List<ExtractedMedia>();

                    if (node.GetProperty("is_video").GetBoolean())
                    {
                        mediaItems.Add(new ExtractedMedia()
                        {
                            Url =  node.GetProperty("video_url").GetString(),
                            MediaType = "video/mp4"

                        });
                    }
                    else
                    {
                        mediaItems.Add(new ExtractedMedia()
                        {
                            Url =  node.GetProperty("display_url").GetString(),
                            MediaType = "image/jpeg"
                        });
                    }

                    // carrousel sidecar of images/videos
                    if (node.TryGetProperty("edge_sidecar_to_children", out JsonElement sidecar))
                    {
                        mediaItems.Clear();
                        foreach (var sideElem in sidecar.GetProperty("edges").EnumerateArray())
                        {
                            var sideNode = sideElem.GetProperty("node");
                            string url = sideNode.TryGetProperty("video_url", out var videoUrlElem)
                                ? videoUrlElem.GetString()
                                : sideNode.GetProperty("display_url").GetString();
                            string type = sideNode.TryGetProperty("video_url", out var _)
                                ? "video/mp4"
                                : "image/jpeg";
                            mediaItems.Add(new ExtractedMedia()
                            {
                                Url = url,
                                MediaType = type

                            });
                        }
                    }

                    string caption = "";
                    var captionEdges = node.GetProperty("edge_media_to_caption").GetProperty("edges");
                    if (captionEdges.GetArrayLength() > 0)
                        caption = captionEdges[0].GetProperty("node").GetProperty("text").GetString();

                    var parsedPost = new InstagramPost()
                    {
                        Id = node.GetProperty("shortcode").GetString(),
                        MessageContent = caption,
                        Author = userResult,
                        CreatedAt = DateTimeOffset.FromUnixTimeSeconds(node.GetProperty("taken_at_timestamp").GetInt64()).UtcDateTime,
                        IsPinned = false,
                        
                        Media = mediaItems.ToArray(),
                    };
                    userPosts.Add(parsedPost);
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"Error fetching post: {ex.Message}");
                }
            }

            userResult.RecentPosts = userPosts;
            
            _userCache.Set(username, user, _cacheEntryOptions);

            return userResult;
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

        private async Task<string> GetWebSidecar()
        {
            var settings = await _settingsDal.Get("ig_crawling");
            if (settings == null)
                return _settings.SidecarURL;

            JsonElement sidecars;
            if (!settings.Value.TryGetProperty("WebSidecars", out sidecars))
                return _settings.SidecarURL;

            List<string> sidecarsURL = new List<string>();
            foreach (var s in sidecars.EnumerateArray())
            {
                sidecarsURL.Add(s.GetString());
            }
            var day1 = (int)DateTime.Now.DayOfWeek;

            return "http://" + sidecarsURL[day1 % sidecarsURL.Count];
        }
        
}