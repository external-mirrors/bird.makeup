using BirdsiteLive.DAL.Postgres.DataAccessLayers;
using BirdsiteLive.DAL.Postgres.Settings;
using BirdsiteLive.Wikidata;

var settings = new PostgresSettings()
{
    ConnString = Environment.GetEnvironmentVariable("ConnString"),
};
var dal = new TwitterUserPostgresDal(settings);
var dalIg = new InstagramUserPostgresDal(settings);

var wikiService = new WikidataService(dal, dalIg);

await wikiService.SyncQcodes();
await wikiService.SyncAttachments();
