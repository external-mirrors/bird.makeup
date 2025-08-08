using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using BirdsiteLive.Common.Exceptions;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Instagram.Models;
using dotMakeup.Instagram.Models;

namespace dotMakeup.Instagram.Strategies;

public class Direct : IUserExtractor
{
    static Meter _meter = new("DotMakeup", "1.0.0");
    static Counter<int> _apiCalled = _meter.CreateCounter<int>("dotmakeup_api_called_count");
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IInstagramUserDal _dal;
    public Direct(IHttpClientFactory httpClientFactory, IInstagramUserDal userDal)
    {
        _httpClientFactory = httpClientFactory;
        _dal = userDal;
    }
    public async Task<InstagramUser> GetUserAsync(string username)
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

                long likes = 0;
                try
                {
                    likes = node.GetProperty("edge_liked_by").GetProperty("count").GetInt64();
                }
                catch (Exception _) { }

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
                    LikeCount = likes,
                    
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
            await _dal.UpdateUserCacheAsync(user);
        
        return userResult;
    }

}