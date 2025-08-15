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
    private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions()
    {
        Converters = { new InstagramSocialMediaUserConverter() }
    };
    
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
        requestUrl = (await GetWebSidecar()) + "/instagram/post2/" + id;
        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var httpResponse = await client.SendAsync(request);

        if (httpResponse.StatusCode != HttpStatusCode.OK)
        {
            return null;
        }
        var c = await httpResponse.Content.ReadAsStringAsync();
        InstagramPost? post = JsonSerializer.Deserialize<InstagramPost>(c, _serializerOptions);
        if (post == null)
        {
            return null;
        }

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
        
        using var client = _httpClientFactory.CreateClient();
        string requestUrl;
        string method = "user2";
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
        InstagramUser user = JsonSerializer.Deserialize<InstagramUser>(c, _serializerOptions);

        if (user == null)
        {
            throw new UserNotFoundException();
        }

        await _dal.UpdateUserCacheAsync(user);
        
        return user;
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