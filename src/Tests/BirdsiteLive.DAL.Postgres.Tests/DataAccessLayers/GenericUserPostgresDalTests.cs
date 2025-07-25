using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Models;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.DAL.Postgres.DataAccessLayers;
using BirdsiteLive.DAL.Postgres.Tests.DataAccessLayers.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BirdsiteLive.DAL.Postgres.Tests.DataAccessLayers
{
    [TestClass]
    public class GenericUserPostgresDalTests : PostgresTestingBase
    {
        public static IEnumerable<object[]> Implementations
        {
            get
            {
                yield return new object[] { new HackerNewsUserPostgresDal(_settings) };
                yield return new object[] { new InstagramUserPostgresDal(_settings) };
                yield return new object[] { new TwitterUserPostgresDal(_settings) };
            }
        }
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
        [DynamicData(nameof(Implementations))]
        public async Task GetIgUserAsync_NoUser(SocialMediaUserPostgresDal dal)
        {
            var result = await dal.GetUserAsync("dontexist");
            Assert.IsNull(result);
        }

        [TestMethod]
        [DynamicData(nameof(Implementations))]
        public async Task CreateAndGetUser(SocialMediaUserPostgresDal dal)
        {
            var acct = "myid";

            await dal.CreateUserAsync(acct);
            var result = await dal.GetUserAsync(acct);

            Assert.AreEqual(acct, result.Acct);
            Assert.AreEqual(0, result.FetchingErrorCount);
            Assert.IsTrue(result.Id > 0);
        }

        [TestMethod]
        [DynamicData(nameof(Implementations))]
        public async Task CreateAndGetUser_byId(SocialMediaUserPostgresDal dal)
        {
            var acct = "myid";

            await dal.CreateUserAsync(acct);
            var result = await dal.GetUserAsync(acct);
            var resultById = await dal.GetUserAsync(result.Id);

            Assert.AreEqual(acct, resultById.Acct);
            Assert.AreEqual(result.Id, resultById.Id);
        }

        [TestMethod]
        [DynamicData(nameof(Implementations))]
        public async Task CreateAndDeleteUser(SocialMediaUserPostgresDal dal)
        {
            var acct = "myacct";
            var lastTweetId = 1548L;

            await dal.CreateUserAsync(acct);
            var result = await dal.GetUserAsync(acct);
            Assert.IsNotNull(result);

            await dal.DeleteUserAsync(acct);
            result = await dal.GetUserAsync(acct);
            Assert.IsNull(result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        [DynamicData(nameof(Implementations))]
        public async Task DeleteUser_NotAcct(SocialMediaUserPostgresDal dal)
        {
            await dal.DeleteUserAsync(string.Empty);
        }

        [TestMethod]
        [DynamicData(nameof(Implementations))]
        public async Task CreateAndDeleteUser_byId(SocialMediaUserPostgresDal dal)
        {
            var acct = "myacct";
            var lastTweetId = 1548L;

            await dal.CreateUserAsync(acct);
            var result = await dal.GetUserAsync(acct);
            Assert.IsNotNull(result);

            await dal.DeleteUserAsync(result.Id);
            result = await dal.GetUserAsync(acct);
            Assert.IsNull(result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        [DynamicData(nameof(Implementations))]
        public async Task DeleteUser_NotAcct_byId(SocialMediaUserPostgresDal dal)
        {
            await dal.DeleteUserAsync(default(int));
        }
        
        [TestMethod]
        [DynamicData(nameof(Implementations))]
        public async Task Wikidata_Insert(SocialMediaUserPostgresDal dal)
        {
            var acct = "myacct";
            var lastTweetId = 1548L;
            var wiki = new WikidataEntry()
            {
                QCode = "Q123"
            };

            await dal.CreateUserAsync(acct);
            var result = await dal.GetUserAsync(acct);
            Assert.IsNotNull(result);

            var update = new Dictionary<string, WikidataEntry>();
            update[acct] = wiki;
            await dal.UpdateUsersWikidataAsync(update);
            result = await dal.GetUserAsync(acct);
            Assert.AreEqual(result.Wikidata.QCode, "Q123");
        }
        [TestMethod]
        [DynamicData(nameof(Implementations))]
        public async Task Wikidata_Empty(SocialMediaUserPostgresDal dal)
        {
            var acct = "myacct";
            var lastTweetId = 1548L;

            await dal.CreateUserAsync(acct);
            var result = await dal.GetUserAsync(acct);
            Assert.IsNotNull(result);

            result = await dal.GetUserAsync(acct);
            Assert.IsNull(result.Wikidata);
        }
        
    }
}