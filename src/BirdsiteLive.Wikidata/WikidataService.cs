using System.Text.Json;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Models;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;

namespace BirdsiteLive.Wikidata;

public class WikidataService
{
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
    public WikidataService()
    {

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

    public async Task<Dictionary<string, WikidataEntry>> ImportQcodes()
    {
        Console.WriteLine("Making Wikidata Query to " + _endpoint);
        var response = await _client.GetAsync(_endpoint + Uri.EscapeDataString(HandleQuery));
        var content = await response.Content.ReadAsStringAsync();
        var res = JsonDocument.Parse(content);
        Console.WriteLine("Done with Wikidata Query");

        var qcodeUpdates = new Dictionary<string, WikidataEntry>();

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
                if (handleIg.Length < 31)
                    entry.HandleIG.Add(handleIg);
            }
            if (handleTwitter is not null)
            {
                if (entry.HandleTwitter is null)
                    entry.HandleTwitter = new HashSet<string>();
                if (handleTwitter.Length < 21)
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

        Console.WriteLine($"There are {qcodeUpdates.Count} Wikidata objects");
        return qcodeUpdates;
    }
    public async Task SyncQcodes(List<(SocialMediaUserDal, Func<WikidataEntry, HashSet<string>?>)> networks)
    {
        var qcodeUpdatesCandidates = await ImportQcodes();
        var qcodeUpdates = new Dictionary<string, WikidataEntry>();

        foreach (var (dal, propDelegate) in networks)
        {
            var users = new HashSet<string>();
            var userQuery = await dal.GetAllUsersAsync();
            Console.WriteLine("Loading users");

            foreach (SyncUser user in userQuery)
            {
                users.Add(user.Acct);
            }

            Console.WriteLine($"Done loading {users.Count} users");

            var emptyHSets = new HashSet<string>();
            foreach (var (qcode, entry) in qcodeUpdatesCandidates)
            {
                if (users.Overlaps(propDelegate(entry) ?? emptyHSets))
                    qcodeUpdates[qcode] = entry;
            }
        }

        Console.WriteLine($"There are {qcodeUpdates.Count} Wikidata objects to import");

        foreach (var (dal, propDelegate) in networks)
        {
            var updates = new Dictionary<string, WikidataEntry>();
            foreach (var (qcode, entry) in qcodeUpdates)
            {
                var accounts = propDelegate(entry);
                if (accounts is null)
                    continue;

                foreach (var account in accounts)
                {
                    updates[account] = entry;
                }
            }
            Console.WriteLine($"Checkpointing {updates.Count} users");
            await dal.UpdateUsersWikidataAsync(updates);
            Console.WriteLine($"Done checkpointing");
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
}