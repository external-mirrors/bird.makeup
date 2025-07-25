using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.DAL.Postgres.DataAccessLayers;
using BirdsiteLive.DAL.Postgres.Tests.DataAccessLayers.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BirdsiteLive.DAL.Postgres.Tests.DataAccessLayers
{
    [TestClass]
    public class HnUserPostgresDalTests : PostgresTestingBase
    {
        [TestInitialize]
        public async Task TestInit()
        {
            var dal = new DbInitializerPostgresDal(_settings, _tools);
            var init = new DatabaseInitializer(dal);
            await init.InitAndMigrateDbAsync();
        }

        [TestCleanup]
        public async Task CleanUp()
        {
            var dal = new DbInitializerPostgresDal(_settings, _tools);
            await dal.DeleteAllAsync();
        }

        [TestMethod]
        public async Task GetAllUsers_Ranked()
        {
            var facct = "myhandle";
            var host = "r.town";
            var inboxRoute = "/myhandle/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{facct}";

            var fdal = new FollowersPostgresDal(_settings);
            await fdal.CreateFollowerAsync(facct, host, inboxRoute, sharedInboxRoute, actorId, []);
            var follower = await fdal.GetFollowerAsync(facct, host);
            
            // Create accounts
            var dal = new HackerNewsUserPostgresDal(_settings);
            for (var i = 0; i < 100; i++)
            {
                var acct = $"myid{i}";

                await dal.CreateUserAsync(acct);


                var user = await dal.GetUserAsync(acct);
                await dal.AddFollower(follower.Id, user.Id);
            }

            for (var i = 0; i < 100; i++)
            {
                var acct = $"myid{i}";
                var user = await dal.GetUserAsync(acct);
                if (i != 42)
                    await dal.UpdateUserLastSyncAsync(user);
            }
            

            var result = await dal.GetNextUsersToCrawlAsync(2, 0, 10);

            SyncUser user0 = result.ElementAt(0);
            Assert.AreEqual(user0.Acct, "frontpage");
            SyncUser user1 = result.ElementAt(1);
            Assert.AreEqual(user1.Acct, "myid42");
        }

    }
}