using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using BirdsiteLive.Common.Exceptions;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Instagram.Models;
using dotMakeup.Instagram.Models;
using dotMakeup.ipfs;

namespace dotMakeup.Instagram.Strategies;

public class Sidecar : IUserExtractor, IPostExtractor
{
    static Meter _meter = new("DotMakeup", "1.0.0");
    static Counter<int> _apiCalled = _meter.CreateCounter<int>("dotmakeup_api_called_count");
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IInstagramUserDal _dal;
    private readonly ISettingsDal _settingsDal;
    private readonly InstanceSettings _settings;
    private readonly IIpfsService _ipfs;
    
    private readonly bool _isPremium;

    public Sidecar(IHttpClientFactory httpClientFactory, IInstagramUserDal userDal, ISettingsDal settingsDal, InstanceSettings instanceSettings, IIpfsService ipfsService, bool isPremium = false)
    {
        _httpClientFactory = httpClientFactory;
        _dal = userDal;
        _settingsDal = settingsDal;
        _settings = instanceSettings;
        _ipfs = ipfsService;
        _isPremium = isPremium;
    }
    
    public async Task<InstagramPost?> GetPostAsync(string id)
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

        var mirrored = await _ipfs.Mirror(post, true);
        await _dal.UpdatePostCacheAsync(mirrored);
        
        return (InstagramPost)mirrored;

    }
    public async Task<InstagramUser> GetUserAsync(string username)
    {
        string sidecarURL = await GetWebSidecar();
        int methodChoice = new Random().Next(2) + 1;
        if (_isPremium)
            methodChoice = 1;
        
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

        await _dal.UpdateUserCacheAsync(user);
        
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
    private async Task<string> GetWebSidecar()
    {
        if (_isPremium)
            return _settings.SidecarURL;
        
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