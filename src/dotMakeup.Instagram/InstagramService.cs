using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Common.Exceptions;
using BirdsiteLive.Domain;
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
        private readonly SocialNetworkCache _socialNetworkCache;
        

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
            
            _socialNetworkCache = new SocialNetworkCache(settings);
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
            var v2 = await RefreshUserAsync(user.Acct);
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
                            var hash = await _ipfs.Mirror(m.Url, true);
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
            JsonElement? alwaysRefresh = await _settingsDal.Get("ig_always_refresh");
            if (alwaysRefresh is not null)
                return await CallDirect(username);
            
            var sidecar = await GetWebSidecar();
            var user = await _socialNetworkCache.GetUser(username, [
                () => _instagramUserDal.GetUserCacheAsync<InstagramUser>(username),
                () => CallSidecar(username, sidecar, new Random().Next(2) + 1),
                () => CallSidecar(username, _settings.SidecarURL, 1),
                () => CallDirect(username),
            ]);
            return user;
        }

        private async Task<InstagramUser?> RefreshUserAsync(string username)
        {
            InstagramUser user;
            
            user = await CallDirect(username);
                
            var profileUrlHash = await _ipfs.Mirror(user.ProfileImageUrl, true);
            user.ProfileImageUrl = _ipfs.GetIpfsPublicLink(profileUrlHash);
                
            await _instagramUserDal.UpdateUserCacheAsync(user);
            
            return user;
        }

        public async Task<SocialMediaPost?> GetPostAsync(string id)
        {
            var post = await _socialNetworkCache.GetPost(id, [
                () => _instagramUserDal.GetPostCacheAsync<InstagramPost>(id),
                () => CallSidecarForPost(id),
            ]);
            return post;
        }

        private async Task<InstagramPost?> CallSidecarForPost(string id)
        {
            var client = _httpClientFactory.CreateClient();
            string requestUrl;
            requestUrl = (await GetWebSidecar()) + "/instagram/post/" + id;
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            var httpResponse = await client.SendAsync(request);

            if (httpResponse.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }
            var c = await httpResponse.Content.ReadAsStringAsync();
            var postDoc = JsonDocument.Parse(c);
            var post = ParsePost(postDoc.RootElement);

            return post;

        }

        private async Task<InstagramUser> CallSidecar(string username, string sidecarURL, int methodChoice)
        {
            InstagramUser user = null;
            using var client = _httpClientFactory.CreateClient();
            string requestUrl;
            string method = "user";
            if (methodChoice == 2)
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
                throw new RateLimitExceededException();
            }

            var c = await httpResponse.Content.ReadAsStringAsync();
            var userDocument = JsonDocument.Parse(c);

            List<string> pinnedPost = new List<string>();
            List<InstagramPost> recentPost = new List<InstagramPost>();
            foreach (JsonElement postDoc in userDocument.RootElement.GetProperty("posts").EnumerateArray())
            {
                var post = ParsePost(postDoc);
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
                throw new UserNotFoundException();
            }

            await _instagramUserDal.UpdateUserCacheAsync(user);
            
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

                    var isPinned = false;
                    foreach (var pin in node.GetProperty("pinned_for_users").EnumerateArray())
                    {
                        var pinUser = pin.GetProperty("username").GetString();
                        if (pinUser == userResult.Acct)
                            isPinned = true;
                    }
                    var parsedPost = new InstagramPost()
                    {
                        Id = node.GetProperty("shortcode").GetString(),
                        MessageContent = caption,
                        Author = userResult,
                        CreatedAt = DateTimeOffset.FromUnixTimeSeconds(node.GetProperty("taken_at_timestamp").GetInt64()).UtcDateTime,
                        IsPinned = isPinned,
                        
                        Media = mediaItems.ToArray(),
                    };
                    userPosts.Add(parsedPost);
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"Error fetching post: {ex.Message}");
                }
            }

            userResult.RecentPosts = userPosts.Where(x => x.IsPinned == false);
            userResult.PinnedPosts = userPosts.Where(x => x.IsPinned == true).Select(x => x.Id).ToArray();
            
            if (user is not null)
                await _instagramUserDal.UpdateUserCacheAsync(user);
            
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