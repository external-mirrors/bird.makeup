#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8613, CS8618, CS8619, CS8620, CS8621, CS8625, CS8629, CS8631, CS8634
using System.Text.Json;
using System.Web;
using BirdsiteLive.Common.Interfaces;
using dotMakeup.HackerNews.Models;

namespace dotMakeup.HackerNews.Strategies;

public class HnApiUserStrategy : IHnUserStrategy
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HnApiUserStrategy(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string Name => "api";
    public int Priority => 0;

    public async Task<HNUser?> GetUserAsync(string username)
    {
        var reqUrl = $"https://hacker-news.firebaseio.com/v0/user/{Uri.EscapeDataString(username)}.json";

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, reqUrl);
        using var httpResponse = await client.SendAsync(request);
        httpResponse.EnsureSuccessStatusCode();

        var content = await httpResponse.Content.ReadAsStringAsync();
        if (content == "null")
            return null;

        using var userDoc = JsonDocument.Parse(content);
        var userRoot = userDoc.RootElement;

        var acct = username;
        if (userRoot.TryGetProperty("id", out var idProperty) && idProperty.ValueKind == JsonValueKind.String)
            acct = idProperty.GetString() ?? username;

        var about = string.Empty;
        if (userRoot.TryGetProperty("about", out var aboutProperty) && aboutProperty.ValueKind == JsonValueKind.String)
            about = HttpUtility.HtmlDecode(aboutProperty.GetString());

        var submissions = new List<long>();
        if (userRoot.TryGetProperty("submitted", out var submittedProperty) && submittedProperty.ValueKind == JsonValueKind.Array)
        {
            foreach (var submission in submittedProperty.EnumerateArray())
            {
                if (submission.ValueKind == JsonValueKind.Number && submission.TryGetInt64(out var submissionId))
                    submissions.Add(submissionId);
            }
        }

        return new HNUser
        {
            SocialMediaUserType = SocialMediaUserTypes.User,
            Acct = acct,
            Name = acct,
            Description = about,
            Posts = submissions.ToArray(),
        };
    }
}
