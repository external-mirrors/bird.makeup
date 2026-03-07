#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8613, CS8618, CS8619, CS8620, CS8621, CS8625, CS8629, CS8631, CS8634
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using BirdsiteLive.Common.Exceptions;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Domain;
using dotMakeup.HackerNews.Models;
using dotMakeup.HackerNews.Strategies;
using Microsoft.Extensions.Caching.Memory;

namespace dotMakeup.HackerNews;

// https://github.com/HackerNews/API
public class HnService : ISocialMediaService
{
    static Meter _meter = new("DotMakeup", "1.0.0");
    static Counter<int> _frontpagePosts = _meter.CreateCounter<int>("dotmakeup_hn_frontpage_posts");
    private IHttpClientFactory _httpClientFactory;
    private readonly SocialNetworkCache _socialNetworkCache;
    private readonly IHnUserStrategy[] _userStrategies;

    private readonly HNUser _frontpage = new HNUser()
    {
        SocialMediaUserType = SocialMediaUserTypes.Group,
        Acct = "frontpage",
        Name = "Hacker News",
        Description = "Hacker News' front page",
    };
    public HnService(IHttpClientFactory httpClientFactory, IHackerNewsUserDal hackerNewsUsersDal, InstanceSettings settings)
        : this(httpClientFactory, hackerNewsUsersDal, settings, CreateDefaultUserStrategies(httpClientFactory))
    {
    }

    public HnService(IHttpClientFactory httpClientFactory, IHackerNewsUserDal hackerNewsUsersDal, InstanceSettings settings,
        IEnumerable<IHnUserStrategy> userStrategies)
    {
            _httpClientFactory = httpClientFactory;
            UserDal = hackerNewsUsersDal;
            _userStrategies = userStrategies
                .Where(x => x is not null)
                .OrderBy(x => x.Priority)
                .ToArray();
            if (_userStrategies.Length == 0)
                _userStrategies = CreateDefaultUserStrategies(httpClientFactory);
            
            _socialNetworkCache = new SocialNetworkCache(settings);
    }

    public string ServiceName { get; } = "Hacker News";
    public SocialMediaUserDal UserDal { get; }
    public Regex ValidUsername { get;  } = new Regex(@"^[a-zA-Z0-9_]{1,15}$");
    public Regex UserMention { get; } = new Regex(@".^");
    public string MakeUserNameCanonical(string name)
    {
        return name.Trim().ToLowerInvariant();
    }

    public async Task<SocialMediaUser> GetUserAsync(string username)
    {
        var requestedUsername = username.Trim();
        var canonicalUsername = MakeUserNameCanonical(requestedUsername);
        if (canonicalUsername == "frontpage")
            return _frontpage;
        
        HNUser user;

        user = await _socialNetworkCache.GetUser(canonicalUsername, [
            () => _resolveUserAsync(requestedUsername),
        ]);
        if (user is not null)
            _socialNetworkCache.BackfillUserCache(user);
        return user;
    }
    private async Task<HNUser> _resolveUserAsync(string username)
    {
        var requestedUsername = username.Trim();
        foreach (var strategy in _userStrategies)
        {
            var user = await strategy.GetUserAsync(requestedUsername);
            if (user is not null)
                return user;
        }

        throw new UserNotFoundException();
    }

    public async Task<SocialMediaPost?> GetPostAsync(string id)
    {
        HNPost post;

        post = await _socialNetworkCache.GetPost(id, [() => _resolvePostAsync(id)]);
        if (post is not null)
            _socialNetworkCache.BackfillPostCache(post);
        
        return post;
        
    }
    private async Task<HNPost?> _resolvePostAsync(string id)
    {
        var post = await _getPostAsync(id);
        if (post is not null)
            return post;

        var legacySourceId = _tryConvertLegacyFrontpageRetweetId(id);
        if (legacySourceId is null)
            return null;

        return await _getPostAsync(legacySourceId);
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
        if (c == "null")
            return null;
        postDoc = JsonDocument.Parse(c).RootElement;

        var replyIds = new List<string>();
        if (postDoc.TryGetProperty("kids", out var kidsProperty) && kidsProperty.ValueKind == JsonValueKind.Array)
        {
            foreach (var kid in kidsProperty.EnumerateArray())
            {
                if (kid.ValueKind == JsonValueKind.Number && kid.TryGetInt64(out var kidId))
                    replyIds.Add(kidId.ToString());
                else if (kid.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(kid.GetString()))
                    replyIds.Add(kid.GetString()!);
            }
        }

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
            text = "";
            if (postDoc.TryGetProperty("text", out JsonElement textProperty))
            {
                text += textProperty.GetString();
            }
            long parentId = postDoc.GetProperty("parent").GetInt64();
            var parent = await GetPostAsync(parentId.ToString());
            if (parent is not null && long.TryParse(parent.Id, out var parsedParentId))
            {
                inReplyToId = parsedParentId;
                inReplyToaccount = parent.Author.Acct;
            }
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
                poll.options = poll.options.Append((opt!.MessageContent, opt.Score.Value)).ToList();
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
        
        var user = await GetUserAsync(postDoc.GetProperty("by").GetString()!);
        var post = new HNPost()
        {
            Id = postDoc.GetProperty("id").GetInt32().ToString(),
            MessageContent = text,
            Author = user,
            CreatedAt = time,
            InReplyToStatusId = inReplyToId,
            InReplyToAccount = inReplyToaccount,
            ReplyCount = replyIds.Count,
            Replies = replyIds.ToArray(),
            Score = score,
            LikeCount = score ?? 0,
            Poll = poll,
        };
        return post;
    }
    private static string? _tryConvertLegacyFrontpageRetweetId(string id)
    {
        if (!id.EndsWith("000", StringComparison.Ordinal))
            return null;
        if (!long.TryParse(id, out var parsedId) || parsedId <= 0 || parsedId % 1000 != 0)
            return null;

        return (parsedId / 1000).ToString();
    }

    public async Task<SocialMediaPost[]> GetNewPosts(SyncUser user)
    {
        await UserDal.UpdateUserLastSyncAsync(user);
        if (user.Acct == "frontpage")
            return await _getFrontpagePosts(user);
        
        List<SocialMediaPost> posts = new List<SocialMediaPost>();
        var userData = (HNUser) await GetUserAsync(user.Acct);
        foreach (var p in userData.Posts)
        {
            HNPost post;
            try
            {
                post = await _getPostAsync(p.ToString());
            }
            catch (Exception)
            {
                continue;
            }
            if (post == null)
                continue;
            
            if (post.CreatedAt <= user.LastPost)
                break;
            
            posts.Add(post);
            if (posts.Count > 20)
                break;
        }

        return posts.ToArray();
    }

    private async Task<SocialMediaPost[]> _getFrontpagePosts(SyncUser frontpageUser)
    {
        string reqURL = "https://hacker-news.firebaseio.com/v0/topstories.json";
        
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod("GET"), reqURL);
        
        JsonElement postDoc;
        var httpResponse = await client.SendAsync(request);
        httpResponse.EnsureSuccessStatusCode();
        var c = await httpResponse.Content.ReadAsStringAsync();
        postDoc = JsonDocument.Parse(c).RootElement;

        var posts = new List<SocialMediaPost>();
        foreach (var p in postDoc.EnumerateArray())
        {
            var ogPost = await _getPostAsync(p.ToString());
            if (ogPost == null)
                continue;

            ogPost.IsRetweet = true;
            ogPost.OriginalAuthor = ogPost.Author;
            ogPost.Author = _frontpage;
            ogPost.RetweetId = Int64.Parse(ogPost.Id);

            if (ogPost.CreatedAt <= frontpageUser.LastPost)
                continue;
            
            posts.Add(ogPost);
            
            if (posts.Count >= 10)
                break;
        }
        
        _frontpagePosts.Add(posts.Count);
        
        return posts.ToArray();
    }

    private static IHnUserStrategy[] CreateDefaultUserStrategies(IHttpClientFactory httpClientFactory)
    {
        return
        [
            new HnWebUserStrategy(httpClientFactory),
            new HnApiUserStrategy(httpClientFactory),
        ];
    }
}
