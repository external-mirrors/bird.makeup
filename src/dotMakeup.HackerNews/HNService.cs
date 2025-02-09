using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using BirdsiteLive.Common.Exceptions;
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
        Name = "Hacker News",
        Description = "Hacker News' front page",
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
        if (c == "null")
            throw new UserNotFoundException();
        
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
        
        JsonElement postDoc;
        var httpResponse = await client.SendAsync(request);
        httpResponse.EnsureSuccessStatusCode();
        var c = await httpResponse.Content.ReadAsStringAsync();
        postDoc = JsonDocument.Parse(c).RootElement;

        string type =
            HttpUtility.HtmlDecode(postDoc.GetProperty("type").GetString());

        string text;
        long? inReplyToId = null;
        string? inReplyToaccount = null;
        Poll? poll = null;
        long? score = null;
        if (type == "story" || type == "job")
        {
            text =
                HttpUtility.HtmlDecode(postDoc.GetProperty("title").GetString());
            if (postDoc.TryGetProperty("text", out JsonElement textProperty))
            {
                text += "\n\n";
                text += textProperty.GetString();
            }
            if (postDoc.TryGetProperty("url", out JsonElement urlProperty))
            {
                text += "\n\n";
                text += urlProperty.GetString();
            }

            score = postDoc.GetProperty("score").GetInt32();
        }
        else if (type == "comment")
        {
            text =
                HttpUtility.HtmlDecode(postDoc.GetProperty("text").GetString());
            long parentId = postDoc.GetProperty("parent").GetInt64();
            var parent = await GetPostAsync(parentId.ToString());
            inReplyToId = Int64.Parse(parent?.Id);
            inReplyToaccount = parent?.Author.Acct;
        }
        else if (type == "poll")
        {
            poll = new Poll();
            text =
                HttpUtility.HtmlDecode(postDoc.GetProperty("title").GetString());
            foreach (var part in postDoc.GetProperty("parts").EnumerateArray())
            {
                var partNumber = part.GetInt32();
                var opt = await _getPostAsync(partNumber.ToString());
                poll.options = poll.options.Append((opt.MessageContent, opt.Score.Value)).ToList();
            }
            score = postDoc.GetProperty("score").GetInt32();
        }
        else if (type == "pollopt")
        {
            text =
                HttpUtility.HtmlDecode(postDoc.GetProperty("text").GetString());
            score = postDoc.GetProperty("score").GetInt32();
        }
        else
        {
            throw new NotImplementedException();
        }
        DateTime time = DateTimeOffset.FromUnixTimeSeconds(postDoc.GetProperty("time").GetInt64()).UtcDateTime;
        
        var user = await GetUserAsync(postDoc.GetProperty("by").GetString());
        var post = new HNPost()
        {
            Id = postDoc.GetProperty("id").GetInt32().ToString(),
            MessageContent = text,
            Author = user,
            CreatedAt = time,
            InReplyToStatusId = inReplyToId,
            InReplyToAccount = inReplyToaccount,
            Score = score,
            Poll = poll,
        };
        return post;
    }

    public Task<SocialMediaPost[]> GetNewPosts(SyncUser user)
    {
        throw new NotImplementedException();
    }

}