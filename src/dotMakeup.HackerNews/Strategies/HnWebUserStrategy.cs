#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8613, CS8618, CS8619, CS8620, CS8621, CS8625, CS8629, CS8631, CS8634
using System.Web;
using AngleSharp;
using AngleSharp.Dom;
using BirdsiteLive.Common.Interfaces;
using dotMakeup.HackerNews.Models;

namespace dotMakeup.HackerNews.Strategies;

public class HnWebUserStrategy : IHnUserStrategy
{
    private const string UserAgent = "Bird.makeup ( https://git.sr.ht/~cloutier/bird.makeup ) Bot";
    private static readonly Uri BaseUri = new("https://news.ycombinator.com/");
    private readonly IHttpClientFactory _httpClientFactory;

    public HnWebUserStrategy(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string Name => "web";
    public int Priority => 10;

    public async Task<HNUser?> GetUserAsync(string username)
    {
        var profileDocument = await GetDocumentAsync($"https://news.ycombinator.com/user?id={Uri.EscapeDataString(username)}");
        if (profileDocument is null)
            return null;

        var canonicalUsername = GetCanonicalUsername(profileDocument);
        if (string.IsNullOrWhiteSpace(canonicalUsername))
            return null;

        var submittedDocument =
            await GetDocumentAsync($"https://news.ycombinator.com/submitted?id={Uri.EscapeDataString(canonicalUsername)}");

        return new HNUser
        {
            SocialMediaUserType = SocialMediaUserTypes.User,
            Acct = canonicalUsername,
            Name = canonicalUsername,
            Description = GetAbout(profileDocument),
            Posts = ExtractSubmissionIds(submittedDocument).ToArray(),
        };
    }

    private async Task<IDocument?> GetDocumentAsync(string address)
    {
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content) ||
            string.Equals(content.Trim(), "No such user.", StringComparison.Ordinal))
            return null;

        var context = BrowsingContext.New(Configuration.Default);
        return await context.OpenAsync(req => req.Content(content).Address(address));
    }

    private static string? GetCanonicalUsername(IDocument document)
    {
        var profileLink = document.QuerySelector("a.hnuser[href^='user?id=']");
        var canonicalUsername = GetIdFromHref(profileLink?.GetAttribute("href"));
        if (!string.IsNullOrWhiteSpace(canonicalUsername))
            return canonicalUsername;

        canonicalUsername = GetIdFromHref(document.QuerySelector("link[rel='canonical']")?.GetAttribute("href"));
        if (!string.IsNullOrWhiteSpace(canonicalUsername))
            return canonicalUsername;

        return null;
    }

    private static string GetAbout(IDocument document)
    {
        var aboutValue = GetProfileFieldValue(document, "about:");
        return aboutValue is null ? string.Empty : HttpUtility.HtmlDecode(aboutValue.InnerHtml.Trim());
    }

    private static IElement? GetProfileFieldValue(IDocument document, string label)
    {
        foreach (var row in document.QuerySelectorAll("#bigbox tr"))
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length < 2)
                continue;

            if (string.Equals(cells[0].TextContent.Trim(), label, StringComparison.OrdinalIgnoreCase))
                return cells[1];
        }

        return null;
    }

    private static IEnumerable<long> ExtractSubmissionIds(IDocument? document)
    {
        if (document is null)
            yield break;

        var seen = new HashSet<long>();
        foreach (var story in document.QuerySelectorAll("tr.athing.submission[id]"))
        {
            if (!long.TryParse(story.Id, out var storyId) || !seen.Add(storyId))
                continue;

            yield return storyId;
        }
    }

    private static string? GetIdFromHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return null;

        if (!Uri.TryCreate(BaseUri, href, out var uri))
            return null;

        return HttpUtility.ParseQueryString(uri.Query)["id"];
    }
}
