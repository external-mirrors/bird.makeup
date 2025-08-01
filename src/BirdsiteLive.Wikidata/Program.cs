using BirdsiteLive.Common.Models;
using BirdsiteLive.DAL.Postgres.DataAccessLayers;
using BirdsiteLive.DAL.Postgres.Settings;
using BirdsiteLive.Wikidata;

var settings = new PostgresSettings()
{
    ConnString = Environment.GetEnvironmentVariable("ConnString"),
};
var dal = new TwitterUserPostgresDal(settings);
var dalIg = new InstagramUserPostgresDal(settings);
var dalHn = new HackerNewsUserPostgresDal(settings);

var wikiService = new WikidataService();

await wikiService.SyncQcodes([
    (dal, (WikidataEntry entry) => { return entry.HandleTwitter; }),
    (dalIg, (WikidataEntry entry) => { return entry.HandleIG; }),
    (dalHn, (WikidataEntry entry) => { return entry.HandleHN; }),
]);
