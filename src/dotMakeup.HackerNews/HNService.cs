using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using BirdsiteLive.Common.Interfaces;
using dotMakeup.HackerNews.Models;

namespace dotMakeup.HackerNews;

public class HnService : ISocialMediaService
{
    private IHttpClientFactory _httpClientFactory;
    public HnService(IHttpClientFactory httpClientFactory)
    {
            _httpClientFactory = httpClientFactory;
        
    }

    public string ServiceName { get; } = "Hacker News";
    public SocialMediaUserDal UserDal { get; }
    public Regex ValidUsername { get;  } = new Regex(@"^[a-zA-Z0-9_]{1,15}$");
    public Regex UserMention { get; } = new Regex(@".^");
    
    public async Task<SocialMediaUser> GetUserAsync(string username)
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
            Id = 0,
            Acct = username,
            Name = username,
            Description = about,
        };
        return user;
    }

    public async Task<SocialMediaPost?> GetPostAsync(string id)
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

        string text =
            HttpUtility.HtmlDecode(userDoc.GetProperty("text").GetString());
        
        var user = new HNPost()
        {
            Id = userDoc.GetProperty("id").GetInt32().ToString(),
            MessageContent = text,
        };
        return user;
    }

    public Task<SocialMediaPost[]> GetNewPosts(SyncUser user)
    {
        throw new NotImplementedException();
    }

}