using System.Text.Json;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;

namespace BirdsiteLive.Wikidata;

public class WikidataService
{
    private ITwitterUserDal _dal;
    private IInstagramUserDal _dalIg;
    private readonly string _endpoint;
    private HttpClient _client = new ();

    private const string HandleQuery = """
                                       SELECT ?item ?handleTwitter ?handleIG ?handleReddit ?handleFedi ?handleHN ?handleTT ?itemLabel ?itemDescription
                                       WHERE
                                       {
                                        {?item wdt:P2002 ?handleTwitter } UNION {?item wdt:P2003 ?handleIG}
                                          OPTIONAL {?item wdt:P4033 ?handleFedi} 
                                         OPTIONAL {?item wdt:P4265 ?handleReddit}
                                         OPTIONAL {?item wdt:P7171 ?handleHN}
                                         OPTIONAL {?item wdt:P7085 ?handleTT}
                                         OPTIONAL {?item wdt:P2003 ?handleIG }
                                         OPTIONAL {?item wdt:P2002 ?handleTwitter }
                                       
                                          SERVICE wikibase:label { bd:serviceParam wikibase:language "en". }
                                       } # LIMIT 10  
                                       """;
    private const string NotableWorkQuery = """
                                       SELECT ?item ?handle ?work
                                       WHERE
                                       {
                                         ?item wdt:P2002 ?handle .
                                         ?item wdt:P800 ?work
                                               SERVICE wikibase:label { bd:serviceParam wikibase:language "en". }
                                       } # LIMIT 100
                                       """;
    public WikidataService(ITwitterUserDal twitterUserDal, IInstagramUserDal instagramUserDal)
    {
        _dal = twitterUserDal;
        _dalIg = instagramUserDal;

        string? key = Environment.GetEnvironmentVariable("semantic");
        if (key is null)
        {
            _endpoint = "https://query.wikidata.org/sparql?query=";
        }
        else
        {
            _endpoint = "https://query.semantic.builders/sparql?query=";
            _client.DefaultRequestHeaders.Add("api-key", key);   
        }
        _client.DefaultRequestHeaders.Add("Accept", "application/json");
        _client.DefaultRequestHeaders.Add("User-Agent", "BirdMakeup/1.0 (https://bird.makeup; https://sr.ht/~cloutier/bird.makeup/) BirdMakeup/1.0");
        _client.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task SyncQcodes()
    {
        
        var twitterUser = new HashSet<string>();
        var twitterUserQuery = await _dal.GetAllUsersAsync();
        Console.WriteLine("Loading twitter users");
        foreach (SyncUser user in twitterUserQuery)
        {
            twitterUser.Add(user.Acct);
        }
        Console.WriteLine($"Done loading {twitterUser.Count} twitter users");

        var instagramUsers = new HashSet<string>();
        var instagramUserQuery = await _dalIg.GetAllUsersAsync();
        Console.WriteLine("Loading instagram users");
        foreach (SyncUser user in instagramUserQuery)
        {
            instagramUsers.Add(user.Acct);
        }
        Console.WriteLine($"Done loading {instagramUsers.Count} instagram users");

        Console.WriteLine("Making Wikidata Query to " + _endpoint);
        var response = await _client.GetAsync(_endpoint + Uri.EscapeDataString(HandleQuery));
        var content = await response.Content.ReadAsStringAsync();
        var res = JsonDocument.Parse(content);
        Console.WriteLine("Done with Wikidata Query");


        foreach (JsonElement n in res.RootElement.GetProperty("results").GetProperty("bindings").EnumerateArray())
        {
            

            var qcode = n.GetProperty("item").GetProperty("value").GetString()!.Replace("http://www.wikidata.org/entity/", "");
            var handleTwitter = ExtractValue(n, "handleTwitter", true);
            var handleIg = ExtractValue(n, "handleIG", true);
            var handleReddit = ExtractValue(n, "handleReddit", true);
            var handleHn = ExtractValue(n, "handleHN", true);
            var handleTikTok = ExtractValue(n, "handleTT", true);

            // for any network
            bool isFollowed = (handleTwitter is not null && twitterUser.Contains(handleTwitter))
                              || (handleIg is not null && instagramUsers.Contains(handleIg));

            if (isFollowed)
            {
                var entry = new WikidataEntry()
                {
                    QCode = qcode,
                    Description = ExtractValue(n, "itemDescription", false),
                    Label = ExtractValue(n, "itemLabel", false),
                    FediHandle = ExtractValue(n, "handleFedi", false),
                    HandleReddit = handleReddit,
                    HandleHN = handleHn,
                    HandleTikTok = handleTikTok,
                    HandleTwitter = handleTwitter,
                    HandleIG = handleIg,
                };
                Console.WriteLine($"{entry.Label} with {qcode}");
                if (handleTwitter is not null)
                    await _dal.UpdateUserWikidataAsync(handleTwitter, entry);
                if (handleIg is not null)
                    await _dalIg.UpdateUserWikidataAsync(handleIg, entry);
            }
        }
    }

    private static string? ExtractValue(JsonElement e, string value, bool extraClean)
    {
        string? res = null;

        if (!e.TryGetProperty(value, out var prop))
            return null;
        
        res = prop.GetProperty("value").GetString();

        if (extraClean)
                res = res.ToLower().Trim().TrimEnd( '\r', '\n' );
        
        return res;
    }

    public async Task SyncNotableWork()
    {
        var twitterUser = new HashSet<string>();
        var twitterUserQuery = await _dal.GetAllTwitterUsersAsync();
        Console.WriteLine("Loading twitter users");
        foreach (SyncTwitterUser user in twitterUserQuery)
        {
            twitterUser.Add(user.Acct);
        }
        Console.WriteLine($"Done loading {twitterUser.Count} twitter users");
        
        
        Console.WriteLine("Making Wikidata Query to " + _endpoint);
        var response = await _client.GetAsync(_endpoint + Uri.EscapeDataString(NotableWorkQuery));
        var content = await response.Content.ReadAsStringAsync();
        var res = JsonDocument.Parse(content);
        Console.WriteLine("Done with Wikidata Query");

        var notableWork = new Dictionary<string, List<string>>();
        foreach (JsonElement n in res.RootElement.GetProperty("results").GetProperty("bindings").EnumerateArray())
        {
            var qcode =
                n.GetProperty("item").GetProperty("value").GetString()!.Replace("http://www.wikidata.org/entity/", "");
            var acct = n.GetProperty("handle").GetProperty("value").GetString()!.ToLower().Trim().TrimEnd('\r', '\n');
            var work =
                n.GetProperty("work").GetProperty("value").GetString()!.Replace("http://www.wikidata.org/entity/", "");

            List<string> workList;
            if (!notableWork.TryGetValue("qcode", out workList))
                workList = new List<string>();

            workList = workList.Append(work).ToList();

            notableWork[acct] = workList;
        }

        foreach ((string acct, List<string> works) in notableWork)
        {
            Console.WriteLine(acct + " " + works.Count);
            if (twitterUser.Contains(acct))
                await _dal.UpdateUserExtradataAsync(acct, "wikidata", "notableWorks", works);
        }

    }

    public async Task SyncAttachments()
    {
        var twitterUser = new HashSet<string>();
        var twitterUserQuery = await _dal.GetAllTwitterUsersAsync();
        Console.WriteLine("Loading twitter users");
        foreach (SyncTwitterUser user in twitterUserQuery)
        {
            twitterUser.Add(user.Acct);
        }
        Console.WriteLine($"Done loading {twitterUser.Count} twitter users");

        foreach (string u in twitterUser)
        {
            var s = await _dal.GetUserWikidataAsync(u);
            var w = JsonSerializer.Deserialize<WikidataEntry>(s);
            if (w.FediHandle is not null)
            {
                Console.WriteLine($"{u} - {w.FediHandle}");
                
                var input = w.FediHandle.TrimStart('@');
                string[] parts = input.Split('@');
                if (parts.Length != 2)
                {
                    Console.WriteLine($"{u} - invalid fedi handle");
                    continue;
                }
                string username = parts[0];
                string domain = parts[1];

                string fediLink = $"<span class=\"h-card\" translate=\"no\"><a href=\"https://{domain}/users/{username}\" class=\"u-url mention\">@<span>{username}@{domain}</span></a></span>";
                
                await _dal.UpdateUserExtradataAsync(u, "hooks", "addAttachments", new Dictionary<string, string>() {{ "fedi", fediLink }});
            }
        }
    }
}