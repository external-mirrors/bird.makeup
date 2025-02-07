using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using dotMakeup.HackerNews.Models;
using Microsoft.Extensions.Caching.Memory;

namespace dotMakeup.HackerNews;

// https://github.com/HackerNews/API
public class HnService : ISocialMediaService
{
    private IHttpClientFactory _httpClientFactory;
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

    private readonly HNUser _frontpage = new HNUser()
    {
        SocialMediaUserType = SocialMediaUserTypes.Group,
        Acct = "frontpage",
        Description = "Front page",
    };
    public HnService(IHttpClientFactory httpClientFactory, IHackerNewsUserDal hackerNewsUsersDal, InstanceSettings settings)
    {
            _httpClientFactory = httpClientFactory;
            UserDal = hackerNewsUsersDal;
            
            _userCache = new MemoryCache(new MemoryCacheOptions()
            {
                SizeLimit = settings.UserCacheCapacity
            });
            _postCache = new MemoryCache(new MemoryCacheOptions()
            {
                SizeLimit = settings.TweetCacheCapacity
            });
    }

    public string ServiceName { get; } = "Hacker News";
    public SocialMediaUserDal UserDal { get; }
    public Regex ValidUsername { get;  } = new Regex(@"^[a-zA-Z0-9_]{1,15}$");
    public Regex UserMention { get; } = new Regex(@".^");

    public async Task<SocialMediaUser> GetUserAsync(string username)
    {
        if (username == "frontpage")
            return _frontpage;
        
        HNUser user;

        if (!_userCache.TryGetValue(username, out user))
        {
            user = await _getUserAsync(username);
            _userCache.Set(username, user, _cacheEntryOptions);
        }
        
        return user;
    }
    private async Task<HNUser> _getUserAsync(string username)
    {
        string reqURL = "https://hacker-news.firebaseio.com/v0/user/dhouston.json";
        reqURL = reqURL.Replace("dhouston", username);
        
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod("GET"), reqURL);
        
        JsonDocument userDoc;
        var httpResponse = await client.SendAsync(request);
        httpResponse.EnsureSuccessStatusCode();
        var c = await httpResponse.Content.ReadAsStringAsync();
        userDoc = JsonDocument.Parse(c);

        string about =
            HttpUtility.HtmlDecode(userDoc.RootElement.GetProperty("about").GetString());
        
        var user = new HNUser()
        {
            SocialMediaUserType = SocialMediaUserTypes.User,
            Acct = username,
            Name = username,
            Description = about,
        };
        return user;
    }

    public async Task<SocialMediaPost?> GetPostAsync(string id)
    {
        HNPost post;

        if (!_postCache.TryGetValue(id, out post))
        {
            post = await _getPostAsync(id);
            _userCache.Set(id, post, _cacheEntryOptions);
        }
        
        return post;
        
    }
    private async Task<HNPost?> _getPostAsync(string id)
    {
        string reqURL = "https://hacker-news.firebaseio.com/v0/item/2921983.json";
        reqURL = reqURL.Replace("2921983", id);
        
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod("GET"), reqURL);
        
        JsonElement userDoc;
        var httpResponse = await client.SendAsync(request);
        httpResponse.EnsureSuccessStatusCode();
        var c = await httpResponse.Content.ReadAsStringAsync();
        userDoc = JsonDocument.Parse(c).RootElement;

        string type =
            HttpUtility.HtmlDecode(userDoc.GetProperty("type").GetString());

        string text;
        long? inReplyToId = null;
        string? inReplyToaccount = null;
        if (type == "story")
        {
            text =
                HttpUtility.HtmlDecode(userDoc.GetProperty("title").GetString());
            if (userDoc.TryGetProperty("text", out JsonElement textProperty))
            {
                text += "\n\n";
                text += textProperty.GetString();
            }
            if (userDoc.TryGetProperty("url", out JsonElement urlProperty))
            {
                text += "\n\n";
                text += urlProperty.GetString();
            }
        }
        else if (type == "comment")
        {
            text =
                HttpUtility.HtmlDecode(userDoc.GetProperty("text").GetString());
            long parentId = userDoc.GetProperty("parent").GetInt64();
            var parent = await GetPostAsync(parentId.ToString());
            inReplyToId = Int64.Parse(parent?.Id);
            inReplyToaccount = parent?.Author.Acct;
        }
        else if (type == "job")
        {
            text =
                HttpUtility.HtmlDecode(userDoc.GetProperty("text").GetString());
        }
        else if (type == "poll")
        {
            text =
                HttpUtility.HtmlDecode(userDoc.GetProperty("text").GetString());
        }
        else if (type == "pollopt")
        {
            text =
                HttpUtility.HtmlDecode(userDoc.GetProperty("text").GetString());
        }
        else
        {
            throw new NotImplementedException();
        }
        
        var user = await GetUserAsync(userDoc.GetProperty("by").GetString());
        var post = new HNPost()
        {
            Id = userDoc.GetProperty("id").GetInt32().ToString(),
            MessageContent = text,
            Author = user,
            InReplyToStatusId = inReplyToId,
            InReplyToAccount = inReplyToaccount,
        };
        return post;
    }

    public Task<SocialMediaPost[]> GetNewPosts(SyncUser user)
    {
        throw new NotImplementedException();
    }

}