using System;
using System.Linq;
using System.Threading.Tasks;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Postgres.DataAccessLayers.Base;
using BirdsiteLive.DAL.Postgres.Settings;
using BirdsiteLive.DAL.Postgres.Tools;
using Dapper;
using Npgsql;

namespace BirdsiteLive.DAL.Postgres.DataAccessLayers
{
    internal class DbVersion
    {
        public string Type { get; set; }
        public int Major { get; set; }
        public int Minor { get; set; }
    }

    public class DbInitializerPostgresDal : PostgresBase, IDbInitializerDal
    {
        private readonly PostgresTools _tools;
        private readonly Version _currentVersion = new Version(4, 0);
        private const string DbVersionType = "db-version";

        #region Ctor
        public DbInitializerPostgresDal(PostgresSettings settings, PostgresTools tools) : base(settings)
        {
            _tools = tools;
        }
        #endregion

        public async Task<Version> GetCurrentDbVersionAsync()
        {
            var query = $"SELECT * FROM {_settings.DbVersionTableName} WHERE type = @type";

            try
            {
                await using var dbConnection = Connection;

                var result = (await dbConnection.QueryAsync<DbVersion>(query, new { type = DbVersionType })).FirstOrDefault();

                if (result == default)
                    return null;

                return new Version(result.Major, result.Minor);
            }
            catch (PostgresException e)
            {
                if (e.Message.Contains("42P01"))
                    return null;

                throw;
            }
        }

        public Version GetMandatoryDbVersion()
        {
            return _currentVersion;
        }

        public async Task<Version> InitDbAsync()
        {
            // Create version table 
            var createVersion = $@"CREATE TABLE {_settings.DbVersionTableName}
		    (
                type VARCHAR(128) PRIMARY KEY,

                major SMALLINT NOT NULL,
                minor SMALLINT NOT NULL
		    );";
            await _tools.ExecuteRequestAsync(createVersion);

            // Create Twitter User table
            var createTwitter = $@"CREATE TABLE {_settings.TwitterUserTableName}
		    (
                id SERIAL PRIMARY KEY,
                acct VARCHAR(20) UNIQUE, 

                lastTweetPostedId BIGINT,
                lastTweetSynchronizedForAllFollowersId BIGINT
		    );";
            await _tools.ExecuteRequestAsync(createTwitter);

            // Create Followers table
            var createFollowers = $@"CREATE TABLE {_settings.FollowersTableName}
		    (
                id SERIAL PRIMARY KEY,
                
                followings INTEGER[],
                followingsSyncStatus JSONB,

                acct VARCHAR(50) NOT NULL, 
                host VARCHAR(253) NOT NULL,
                inboxRoute VARCHAR(2048) NOT NULL,
                sharedInboxRoute VARCHAR(2048),
                UNIQUE (acct, host)
		    );";
            await _tools.ExecuteRequestAsync(createFollowers);

            // Create Cached Tweet table 
            var createCachedTweets = $@"CREATE TABLE {_settings.CachedTweetsTableName}
		    (
                id BIGINT PRIMARY KEY,
                twitterUserId INTEGER,
                data JSONB
		    );";
            await _tools.ExecuteRequestAsync(createCachedTweets);

            // Insert version to db
            var firstVersion = new Version(1, 0);
            using (var dbConnection = Connection)
            {
                dbConnection.Open();

                await dbConnection.ExecuteAsync(
                    $"INSERT INTO {_settings.DbVersionTableName} (type,major,minor) VALUES(@type,@major,@minor)",
                    new { type = DbVersionType, major = firstVersion.Major, minor = firstVersion.Minor });
            }

            return firstVersion;
        }

        public Tuple<Version, Version>[] GetMigrationPatterns()
        {
            return new[]
            {
                new Tuple<Version, Version>(new Version(1,0), new Version(2,0)),
                new Tuple<Version, Version>(new Version(2,0), new Version(2,1)),
                new Tuple<Version, Version>(new Version(2,1), new Version(2,2)),
                new Tuple<Version, Version>(new Version(2,2), new Version(2,3)),
                new Tuple<Version, Version>(new Version(2,3), new Version(2,4)),
                new Tuple<Version, Version>(new Version(2,4), new Version(2,5)),
                new Tuple<Version, Version>(new Version(2,5), new Version(3,0)),
                new Tuple<Version, Version>(new Version(3,0), new Version(3,1)),
                new Tuple<Version, Version>(new Version(3,1), new Version(3,2)),
                new Tuple<Version, Version>(new Version(3,2), new Version(3,3)),
                new Tuple<Version, Version>(new Version(3,3), new Version(3,4)),
                new Tuple<Version, Version>(new Version(3,4), new Version(3,5)),
                new Tuple<Version, Version>(new Version(3,5), new Version(4,0)),
            };
        }

        public async Task<Version> MigrateDbAsync(Version from, Version to)
        {
            if (from == new Version(1, 0) && to == new Version(2, 0))
            {
                var addLastSync = $@"ALTER TABLE {_settings.TwitterUserTableName} ADD lastSync TIMESTAMP (2) WITHOUT TIME ZONE";
                await _tools.ExecuteRequestAsync(addLastSync);

                var addIndex = $@"CREATE INDEX IF NOT EXISTS lastsync_twitteruser ON {_settings.TwitterUserTableName}(lastSync)";
                await _tools.ExecuteRequestAsync(addIndex);
            }
            else if (from == new Version(2, 0) && to == new Version(2, 1))
            {
                var addActorId = $@"ALTER TABLE {_settings.FollowersTableName} ADD actorId VARCHAR(2048)";
                await _tools.ExecuteRequestAsync(addActorId);
            }
            else if (from == new Version(2, 1) && to == new Version(2, 2))
            {
                var addLastSync = $@"ALTER TABLE {_settings.TwitterUserTableName} ADD fetchingErrorCount SMALLINT";
                await _tools.ExecuteRequestAsync(addLastSync);
            }
            else if (from == new Version(2, 2) && to == new Version(2, 3))
            {
                var addPostingError = $@"ALTER TABLE {_settings.FollowersTableName} ADD postingErrorCount SMALLINT";
                await _tools.ExecuteRequestAsync(addPostingError);
            }
            else if (from == new Version(2, 3) && to == new Version(2, 4))
            {
                var alterLastSync = $@"ALTER TABLE {_settings.TwitterUserTableName} ALTER COLUMN fetchingErrorCount TYPE INTEGER";
                await _tools.ExecuteRequestAsync(alterLastSync);

                var alterPostingError = $@"ALTER TABLE {_settings.FollowersTableName} ALTER COLUMN postingErrorCount TYPE INTEGER";
                await _tools.ExecuteRequestAsync(alterPostingError);
            }
            else if (from == new Version(2, 4) && to == new Version(2, 5))
            {
                var alterTwitterUserId = $@"ALTER TABLE {_settings.TwitterUserTableName} ADD twitterUserId BIGINT";
                await _tools.ExecuteRequestAsync(alterTwitterUserId);

            }
            else if (from == new Version(2, 5) && to == new Version(3, 0))
            {
                var dropFollowingSyncStatus = $@"ALTER TABLE {_settings.FollowersTableName} DROP COLUMN followingssyncstatus";
                await _tools.ExecuteRequestAsync(dropFollowingSyncStatus);

                var dropLastTweet = $@"ALTER TABLE {_settings.TwitterUserTableName} DROP COLUMN lasttweetsynchronizedforallfollowersid";
                await _tools.ExecuteRequestAsync(dropLastTweet);
                
                var addFediverseEquivalent = $@"ALTER TABLE {_settings.TwitterUserTableName} ADD fediverseaccount text";
                await _tools.ExecuteRequestAsync(addFediverseEquivalent);
                
                var createWorkers = $@"CREATE TABLE {_settings.WorkersTableName}
                (
                    id BIGINT PRIMARY KEY,
                    rangeStart INTEGER,
                    rangeEnd INTEGER,
                    lastSeen TIMESTAMP (2) WITHOUT TIME ZONE,
                    name text
                );";
                await _tools.ExecuteRequestAsync(createWorkers);
                
                var createWorkerInstance = $@" INSERT INTO {_settings.WorkersTableName} (id,rangeStart,rangeEnd) VALUES(0,0, 100) ";
                await _tools.ExecuteRequestAsync(createWorkerInstance);
                
                var createInstagram = $@"CREATE TABLE {_settings.InstagramUserTableName}
                (
                    id SERIAL PRIMARY KEY,
                    acct VARCHAR(20) UNIQUE, 
                    
                    data JSONB
                );";
                await _tools.ExecuteRequestAsync(createInstagram);
                
                var createInstagramPost = $@"CREATE TABLE {_settings.CachedInstaPostsTableName}
                (
                    id VARCHAR(30) PRIMARY KEY,
                    acct VARCHAR(20) UNIQUE, 
                    
                    data JSONB
                );";
                await _tools.ExecuteRequestAsync(createInstagramPost);
            }
            else if (from == new Version(3, 0) && to == new Version(3, 1))
            {
                var addStatusCount = $@"ALTER TABLE {_settings.TwitterUserTableName} ADD statusescount integer";
                await _tools.ExecuteRequestAsync(addStatusCount);
                
                var createSettings = $@"CREATE TABLE {_settings.SettingTableName}
                (
                    setting_key TEXT PRIMARY KEY,
                    setting_value JSONB
                );";
                await _tools.ExecuteRequestAsync(createSettings);
            }
            else if (from == new Version(3, 1) && to == new Version(3, 2))
            {
                var alterTwitterExtradata = $@"ALTER TABLE {_settings.TwitterUserTableName} ADD extradata JSONB";
                await _tools.ExecuteRequestAsync(alterTwitterExtradata);

                var alterInstagramExtradata = $@"ALTER TABLE {_settings.InstagramUserTableName} ADD extradata JSONB";
                await _tools.ExecuteRequestAsync(alterInstagramExtradata);
            }
            else if (from == new Version(3, 2) && to == new Version(3, 3))
            {
                var alterFollowersAddIg = $@"ALTER TABLE {_settings.FollowersTableName} ADD followings_instagram INTEGER[]";
                await _tools.ExecuteRequestAsync(alterFollowersAddIg);
            }
            else if (from == new Version(3, 3) && to == new Version(3, 4))
            {
                var alterTwittterAddWikidata = $@"ALTER TABLE {_settings.TwitterUserTableName} ADD wikidata JSONB";
                var alterTwittterAddCache = $@"ALTER TABLE {_settings.TwitterUserTableName} ADD cache JSONB";
                var alterIgAddWikidata = $@"ALTER TABLE {_settings.InstagramUserTableName} ADD wikidata JSONB";
                var alterIgAddCache = $@"ALTER TABLE {_settings.InstagramUserTableName} ADD cache JSONB";
                await _tools.ExecuteRequestAsync(alterTwittterAddWikidata);
                await _tools.ExecuteRequestAsync(alterTwittterAddCache);
                await _tools.ExecuteRequestAsync(alterIgAddWikidata);
                await _tools.ExecuteRequestAsync(alterIgAddCache);
            }
            else if (from == new Version(3, 4) && to == new Version(3, 5))
            {
                var alterFollowerExtradata = $@"ALTER TABLE {_settings.FollowersTableName} ADD extradata JSONB";
                var addLastSync1 = $@"ALTER TABLE {_settings.InstagramUserTableName} ADD lastSync TIMESTAMP (2) WITHOUT TIME ZONE DEFAULT NOW()";
                var addLastSync2 = $@"ALTER TABLE {_settings.CachedInstaPostsTableName} ADD lastSync TIMESTAMP (2) WITHOUT TIME ZONE DEFAULT NOW()";
                var defaultTweetId = $@"ALTER TABLE {_settings.TwitterUserTableName} ALTER COLUMN lasttweetpostedid SET DEFAULT -1";
                await _tools.ExecuteRequestAsync(alterFollowerExtradata);
                await _tools.ExecuteRequestAsync(addLastSync1);
                await _tools.ExecuteRequestAsync(addLastSync2);
                await _tools.ExecuteRequestAsync(defaultTweetId);
            }
            else if (from == new Version(3, 5) && to == new Version(4, 0))
            {
                var createTwitterCrawling = $@"CREATE TABLE {_settings.TwitterCrawlingUserTableName}
                (
                    acct VARCHAR(20) primary key, 
                    password VARCHAR(30) NOT NULL, 
                    email VARCHAR(30), 
                    phone VARCHAR(30), 
                    reservedto VARCHAR(30), 
                    extradata JSONB
                );";
                var createHnUsers = $@"CREATE TABLE {_settings.HackerNewsUserTableName}
                (
                    id SERIAL PRIMARY KEY,
                    acct VARCHAR(20) UNIQUE, 
                    lastsync TIMESTAMP (2) WITHOUT TIME ZONE DEFAULT NOW(),
                    type char NOT NULL,
                    wikidata JSONB
                );
                INSERT INTO {_settings.HackerNewsUserTableName} (acct, type)
                VALUES ('frontpage', 'g');";
                var dropConstraint =
                    $@"ALTER TABLE {_settings.CachedInstaPostsTableName} DROP CONSTRAINT IF EXISTS cached_insta_posts_acct_key;";
                var addFollowersIndex =
                    $@"create index {_settings.CachedInstaPostsTableName}_acct_index on {_settings.CachedInstaPostsTableName} (acct);
                       DROP INDEX IF EXISTS following_test;
                       create index followings_twitter_gin_index on {_settings.FollowersTableName} using gin (followings);
                       create index followings_instagram_gin_index on {_settings.FollowersTableName} using gin (followings_instagram);
                       create index followings_hn_gin_index on {_settings.FollowersTableName} using gin (followings_hn);
                    ";
                var alterFollowersAddHn = $@"ALTER TABLE {_settings.FollowersTableName} ADD followings_hn INTEGER[]";
                await _tools.ExecuteRequestAsync(alterFollowersAddHn);
                await _tools.ExecuteRequestAsync(dropConstraint);
                await _tools.ExecuteRequestAsync(addFollowersIndex);
                await _tools.ExecuteRequestAsync(createHnUsers);
                await _tools.ExecuteRequestAsync(createTwitterCrawling);
            }
            else
            {
                throw new NotImplementedException();
            }

            await UpdateDbVersionAsync(to);
            return to;
        }

        private async Task UpdateDbVersionAsync(Version newVersion)
        {
            using (var dbConnection = Connection)
            {
                dbConnection.Open();

                await dbConnection.ExecuteAsync(
                    $"UPDATE {_settings.DbVersionTableName} SET major = @major, minor = @minor WHERE type = @type",
                    new { type = DbVersionType, major = newVersion.Major, minor = newVersion.Minor });
            }
        }

        public async Task DeleteAllAsync()
        {
            var dropsRequests = new[]
            {
                $@"DROP TABLE {_settings.DbVersionTableName};",
                $@"DROP TABLE {_settings.TwitterUserTableName};",
                $@"DROP TABLE {_settings.FollowersTableName};",
                $@"DROP TABLE {_settings.CachedTweetsTableName};",
                $@"DROP TABLE {_settings.InstagramUserTableName};",
                $@"DROP TABLE {_settings.CachedInstaPostsTableName};",
                $@"DROP TABLE {_settings.SettingTableName};",
                $@"DROP TABLE {_settings.WorkersTableName};",
                $@"DROP TABLE {_settings.HackerNewsUserTableName};",
                $@"DROP TABLE {_settings.TwitterCrawlingUserTableName};",
            };

            foreach (var r in dropsRequests)
            {
                await _tools.ExecuteRequestAsync(r);
            }
        }
    }
}