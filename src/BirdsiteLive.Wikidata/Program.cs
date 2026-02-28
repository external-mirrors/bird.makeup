#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8613, CS8618, CS8619, CS8620, CS8621, CS8625, CS8629, CS8631, CS8634
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
