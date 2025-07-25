using System.Text.Json;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Models;
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
                                       PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
                                       PREFIX wdt: <http://www.wikidata.org/prop/direct/>
                                       PREFIX schema: <http://schema.org/>
                                       SELECT ?item ?handleTwitter ?handleIG ?handleReddit ?handleFedi ?handleHN ?handleTT ?work ?itemLabel ?itemDescription
                                         WHERE
                                         {
                                          {?item wdt:P2002 ?handleTwitter } UNION {?item wdt:P2003 ?handleIG}
                                           OPTIONAL {?item wdt:P4033 ?handleFedi} 
                                           OPTIONAL {?item wdt:P4265 ?handleReddit}
                                           OPTIONAL {?item wdt:P7171 ?handleHN}
                                           OPTIONAL {?item wdt:P7085 ?handleTT}
                                           OPTIONAL {?item wdt:P2003 ?handleIG }
                                           OPTIONAL {?item wdt:P2002 ?handleTwitter }
                                           OPTIONAL {?item wdt:P800 ?work }
                                         
                                           OPTIONAL { ?item rdfs:label ?itemLabel . FILTER( LANG(?itemLabel) = "en" ) }
                                           OPTIONAL { ?item schema:description ?itemDescription . FILTER( LANG(?itemDescription) = "en" ) }
                                         } 
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

        string? key = Environment.GetEnvironmentVariable("semantic2");
        if (key is null)
        {
            _endpoint = "https://query.wikidata.org/sparql?query=";
            _endpoint = "https://qlever.cs.uni-freiburg.de/api/wikidata?query=";
        }
        else
        {
            _endpoint = "https://query.semantic.builders/sparql?query=";
            _client.DefaultRequestHeaders.Add("api-key", key);   
        }
        //_client.DefaultRequestHeaders.Add("Accept", "application/json");
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
        string query = _endpoint + Uri.EscapeDataString(HandleQuery);
        var response = await _client.GetAsync(_endpoint + Uri.EscapeDataString(HandleQuery));
        var content = await response.Content.ReadAsStringAsync();
        var res = JsonDocument.Parse(content);
        Console.WriteLine("Done with Wikidata Query");

        var qcodeUpdates = new Dictionary<string, WikidataEntry>();
        var twitterUpdates = new Dictionary<string, WikidataEntry>();
        var instagramUpdates = new Dictionary<string, WikidataEntry>();

        foreach (JsonElement n in res.RootElement.GetProperty("results").GetProperty("bindings").EnumerateArray())
        {
            

            var qcode = n.GetProperty("item").GetProperty("value").GetString()!.Replace("http://www.wikidata.org/entity/", "");
            string? handleTwitter = ExtractValue(n, "handleTwitter", true);
            var handleIg = ExtractValue(n, "handleIG", true);
            var handleReddit = ExtractValue(n, "handleReddit", true);
            var handleHn = ExtractValue(n, "handleHN", true);
            var handleTikTok = ExtractValue(n, "handleTT", true);
            var handleFedi = ExtractValue(n, "handleFedi", false);
            var work = ExtractValue(n, "work", false);

            // for any network
            bool isFollowed = (handleTwitter is not null && twitterUser.Contains(handleTwitter))
                              || (handleIg is not null && instagramUsers.Contains(handleIg));

            if (!isFollowed)
                continue;

            WikidataEntry entry;
            if (qcodeUpdates.ContainsKey(qcode))
            {
                entry = qcodeUpdates[qcode];
            }
            else
            {
                entry = new WikidataEntry()
                {
                    QCode = qcode,
                    Description = ExtractValue(n, "itemDescription", false),
                    Label = ExtractValue(n, "itemLabel", false),
                    HandleReddit = handleReddit,
                    HandleTikTok = handleTikTok,
                };
            }

            if (handleHn is not null)
            {
                if (entry.HandleHN is null)
                    entry.HandleHN = new HashSet<string>();
                entry.HandleHN.Add(handleHn);
            }
            if (handleFedi is not null)
            {
                if (entry.FediHandle is null)
                    entry.FediHandle = new HashSet<string>();
                entry.FediHandle.Add(handleFedi);
            }
            if (handleIg is not null)
            {
                if (entry.HandleIG is null)
                    entry.HandleIG = new HashSet<string>();
                entry.HandleIG.Add(handleIg);
            }
            if (handleTwitter is not null)
            {
                if (entry.HandleTwitter is null)
                    entry.HandleTwitter = new HashSet<string>();
                entry.HandleTwitter.Add(handleTwitter);
            }
            if (work is not null)
            {
                if (entry.NotableWorks is null)
                    entry.NotableWorks = new HashSet<string>();
                entry.NotableWorks.Add(work.Replace("http://www.wikidata.org/entity/", ""));
            }
            
            
            Console.Write($"\r {entry.Label} with {qcode}");
            qcodeUpdates[qcode] = entry;
        }

        foreach ((string qcode, WikidataEntry entry) in qcodeUpdates)
        {
            if (entry.HandleTwitter is not null)
            {
                foreach (var handleTwitter in entry.HandleTwitter)
                {
                    if (handleTwitter.Length < 21)
                        twitterUpdates[handleTwitter] = entry;
                }
            }
            if (entry.HandleIG is not null)
            {
                foreach (var handleIg in entry.HandleIG)
                {
                    if (handleIg.Length < 31)
                        instagramUpdates[handleIg] = entry;
                }
            }
        }
        Console.WriteLine($"Checkpointing Twitter");
        await _dal.UpdateUsersWikidataAsync(twitterUpdates);
        Console.WriteLine($"Checkpointing Instagram");
        await _dalIg.UpdateUsersWikidataAsync(instagramUpdates);
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

    public async Task SyncAttachments()
    {
        Console.WriteLine("Loading twitter users");
        var twitterUserQuery = await _dal.GetAllUsersAsync()!;
        Console.WriteLine($"Done loading {twitterUserQuery.Length} twitter users");

        foreach (SyncUser u in twitterUserQuery)
        {
            var s = await _dal.GetUserWikidataAsync(u.Acct);
            if (s is null)
                continue;
            var w = JsonSerializer.Deserialize<WikidataEntry>(s);
            var att = CreateAttachements(w, true, false);
            if (att.Count > 0)
            {
                await _dal.UpdateUserExtradataAsync(u.Acct, "hooks", "addAttachments", att);
            }
        }
        
        Console.WriteLine("Loading instagram users");
        var igUserQuery = await _dalIg.GetAllUsersAsync()!;
        Console.WriteLine($"Done loading {igUserQuery.Length} instagram users");

        foreach (SyncUser u in igUserQuery)
        {
            var s = await _dalIg.GetUserWikidataAsync(u.Acct);
            if (s is null)
                continue;
            var w = JsonSerializer.Deserialize<WikidataEntry>(s);
            var att = CreateAttachements(w, false, true);
            if (att.Count > 0)
            {
                await _dalIg.UpdateUserExtradataAsync(u.Acct, "hooks", "addAttachments", att);
            }
        }
    }

    private Dictionary<string, string> CreateAttachements(WikidataEntry w, bool skipTwitter, bool skipIg)
    {
        var results = new Dictionary<string, string>();
        
        if (w.FediHandle is not null)
        {
            var fediHandle = w.FediHandle.ElementAt(0);
            Console.WriteLine($"{w.QCode} - {fediHandle}");

            var input = fediHandle.TrimStart('@');
            string[] parts = input.Split('@');
            if (parts.Length == 2)
            {

                string username = parts[0];
                string domain = parts[1];

                string fediLink =
                    $"<span class=\"h-card\" translate=\"no\"><a href=\"https://{domain}/users/{username}\" class=\"u-url mention\">@<span>{username}@{domain}</span></a></span>";
                results.Add("fedi", fediLink);
            }
        }
        
        if (w.HandleTwitter is not null && w.HandleTwitter.Count == 1 && !skipTwitter)
        {
            var twitterHandle = w.HandleTwitter.ElementAt(0);

            string fediLink =
                $"<span class=\"h-card\" translate=\"no\"><a href=\"https://bird.makeup/users/{twitterHandle}\" class=\"u-url mention\">@<span>{twitterHandle}@bird.makeup</span></a></span>";
            results.Add("Twitter", fediLink);
        }
        
        if (w.HandleIG is not null && w.HandleIG.Count == 1 && !skipIg)
        {
            var handle = w.HandleIG.ElementAt(0);

            string fediLink =
                $"<span class=\"h-card\" translate=\"no\"><a href=\"https://kilogram.makeup/users/{handle}\" class=\"u-url mention\">@<span>{handle}@kilogram.makeup</span></a></span>";
            results.Add("Instagram", fediLink);
        }

        return results;
    }
}