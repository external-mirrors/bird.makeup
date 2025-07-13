using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.DAL.Postgres.DataAccessLayers;
using BirdsiteLive.DAL.Postgres.Tests.DataAccessLayers.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BirdsiteLive.DAL.Postgres.Tests.DataAccessLayers
{
    [TestClass]
    [DoNotParallelize]
    public class TwitterUserPostgresDalTests : PostgresTestingBase
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

        // Twitter specific
        [TestMethod]
        public async Task CreateUpdateAndGetUser()
        {
            var acct = "myid";
            var lastTweetId = 1548L;

            var dal = new TwitterUserPostgresDal(_settings);

            await dal.CreateUserAsync(acct);
            var result = await dal.GetUserAsync(acct);


            var updatedLastTweetId = 1600L;
            var updatedLastSyncId = 1550L;
            var now = DateTime.Now;
            var errors = 15;
            await dal.UpdateTwitterUserAsync(result.Id, updatedLastTweetId, errors, now);

            result = await dal.GetUserAsync(acct);

            Assert.AreEqual(acct, result.Acct);
            Assert.AreEqual(updatedLastTweetId, result.LastTweetPostedId);
            Assert.AreEqual(errors, result.FetchingErrorCount);
            Assert.IsTrue(Math.Abs((now.ToUniversalTime() - result.LastSync).Milliseconds) < 100);
        }

        [TestMethod]
        public async Task CreateUpdate2AndGetUser()
        {
            var acct = "myid";
            var lastTweetId = 1548L;

            var dal = new TwitterUserPostgresDal(_settings);

            await dal.CreateUserAsync(acct);
            var result = await dal.GetUserAsync(acct);


            var updatedLastTweetId = 1600L;
            var updatedLastSyncId = 1550L;
            var now = DateTime.Now;
            var errors = 15;

            result.LastTweetPostedId = updatedLastTweetId;
            result.FetchingErrorCount = errors;
            result.LastSync = now;
            await dal.UpdateTwitterUserAsync((SyncTwitterUser) result);

            result = await dal.GetUserAsync(acct);

            Assert.AreEqual(acct, result.Acct);
            Assert.AreEqual(updatedLastTweetId, result.LastTweetPostedId);
            Assert.AreEqual(errors, result.FetchingErrorCount);
            Assert.IsTrue(Math.Abs((now.ToUniversalTime() - result.LastSync).Milliseconds) < 100);
        }

        [TestMethod]
        public async Task CreateUpdate3AndGetUser()
        {
            var acct = "myid";
            var lastTweetId = 1548L;

            var dal = new TwitterUserPostgresDal(_settings);

            await dal.CreateUserAsync(acct);
            var result = await dal.GetUserAsync(acct);


            var updatedLastTweetId = 1600L;
            var updatedLastSyncId = 1550L;
            var now = DateTime.Now;
            var errors = 32768;

            result.LastTweetPostedId = updatedLastTweetId;
            result.FetchingErrorCount = errors;
            result.LastSync = now;
            await dal.UpdateTwitterUserAsync((SyncTwitterUser) result);

            result = (SyncTwitterUser) await dal.GetUserAsync(acct);

            Assert.AreEqual(acct, result.Acct);
            Assert.AreEqual(updatedLastTweetId, result.LastTweetPostedId);
            Assert.AreEqual(errors, result.FetchingErrorCount);
            Assert.IsTrue(Math.Abs((now.ToUniversalTime() - result.LastSync).Milliseconds) < 100);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task Update_NoId()
        {
            var dal = new TwitterUserPostgresDal(_settings);
            await dal.UpdateTwitterUserAsync(default, default, default, DateTime.UtcNow);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task Update_NoLastTweetPostedId()
        {
            var dal = new TwitterUserPostgresDal(_settings);
            await dal.UpdateTwitterUserAsync(12, default,  default, DateTime.UtcNow);
        }


        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task Update_NoLastSync()
        {
            var dal = new TwitterUserPostgresDal(_settings);
            await dal.UpdateTwitterUserAsync(12, 9556, 65,  default);
        }

        [TestMethod]
        public async Task GetAllTwitterUsers_Top()
        {
            var dal = new TwitterUserPostgresDal(_settings);
            for (var i = 0; i < 1000; i++)
            {
                var acct = $"myid{i}";
                var lastTweetId = 1548L;

                await dal.CreateUserAsync(acct);
            }

            var result = await dal.GetAllTwitterUsersAsync(1000);
            Assert.AreEqual(1000, result.Length);
            Assert.IsFalse(result[0].Id == default);
            Assert.IsFalse(result[0].Acct == default);
            Assert.IsFalse(result[0].LastTweetPostedId == default);
        }

        [TestMethod]
        public async Task GetAllTwitterUsers_Ranked()
        {
            // Create accounts
            var dal = new TwitterUserPostgresDal(_settings);
            for (var i = 0; i < 100; i++)
            {
                var acct = $"myid{i}";

                await dal.CreateUserAsync(acct);

                var user = await dal.GetUserAsync(acct);
                user.LastTweetPostedId = i+10;
                user.FetchingErrorCount = i+200;
                if (i == 42)
                    user.LastSync = DateTime.Now.AddYears(-1);
                else
                    user.LastSync = DateTime.Now.AddMonths(-1).AddSeconds(i);
                await dal.UpdateTwitterUserAsync(user);
                //await dal.UpdateTwitterUserIdAsync(acct, i+1);
                await dal.UpdateUserExtradataAsync(acct, "TwitterUserId", i+1);
                
                await dal.UpdateTwitterStatusesCountAsync(acct, i+3000);
                await dal.UpdateUserExtradataAsync(user.Acct, "latest_post_date", DateTime.Now);
            }
            
            var facct = "myhandle";
            var host = "domain.ext";
            var following = Enumerable.Range(0, 100).ToArray();
            var inboxRoute = "/myhandle/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{facct}";

            var fdal = new FollowersPostgresDal(_settings);
            await fdal.CreateFollowerAsync(facct, host, inboxRoute, sharedInboxRoute, actorId, following);

            var result = await dal.GetAllTwitterUsersWithFollowersAsync(1, 0, 10, 10);

            SyncTwitterUser user1 = result.ElementAt(0);
            Assert.AreEqual(user1.Acct, "myid42");
            Assert.AreEqual(user1.LastTweetPostedId, 52);

            SyncTwitterUser user2 = await dal.GetUserAsync(result.ElementAt(0).Id);
            Assert.AreEqual(user1.FetchingErrorCount, user2.FetchingErrorCount);
            Assert.AreEqual(user1.StatusesCount, user2.StatusesCount);
            Assert.AreEqual(user1.Followers, 1);
            Assert.AreEqual(user1.Followers, user2.Followers);
            Assert.AreEqual(user1.LastTweetPostedId, user2.LastTweetPostedId);
            Assert.AreEqual(user1.FetchingErrorCount, user2.FetchingErrorCount);
            Assert.AreEqual(user1.TwitterUserId, user2.TwitterUserId);

        }

        [TestMethod]
        public async Task GetAllTwitterUsers_Limited()
        {
            var now = DateTime.Now;
            var oldest = now.AddDays(-3);
            var newest = now.AddDays(-2);

            var dal = new TwitterUserPostgresDal(_settings);
            for (var i = 0; i < 20; i++)
            {
                var acct = $"myid{i}";
                var lastTweetId = 1548L;

                await dal.CreateUserAsync(acct);
            }

            var allUsers = await dal.GetAllTwitterUsersAsync(100);
            for (var i = 0; i < 20; i++)
            {
                var user = allUsers[i];
                var date = i % 2 == 0 ? oldest : newest;
                await dal.UpdateTwitterUserAsync(user.Id, user.LastTweetPostedId, 0, date);
            }

            var result = await dal.GetAllTwitterUsersAsync(10);
            Assert.AreEqual(10, result.Length);
            Assert.IsFalse(result[0].Id == default);
            Assert.IsFalse(result[0].Acct == default);
            Assert.IsFalse(result[0].LastTweetPostedId == default);

            foreach (var acc in result)
                Assert.IsTrue(Math.Abs((acc.LastSync - oldest.ToUniversalTime()).TotalMilliseconds) < 1000);
        }

        [TestMethod]
        public async Task GetAllTwitterUsers()
        {
            var dal = new TwitterUserPostgresDal(_settings);
            for (var i = 0; i < 1000; i++)
            {
                var acct = $"myid{i}";
                var lastTweetId = 1548L;

                await dal.CreateUserAsync(acct);
            }

            var result = await dal.GetAllTwitterUsersAsync();
            Assert.AreEqual(1000, result.Length);
            Assert.IsFalse(result[0].Id == default);
            Assert.IsFalse(result[0].Acct == default);
            Assert.IsFalse(result[0].LastTweetPostedId == default);
        }

        [TestMethod]
        public async Task CountTwitterUsers()
        {
            var dal = new TwitterUserPostgresDal(_settings);
            for (var i = 0; i < 10; i++)
            {
                var acct = $"myid{i}";
                var lastTweetId = 1548L;

                await dal.CreateUserAsync(acct);
            }

            var result = await dal.GetTwitterUsersCountAsync();
            Assert.AreEqual(0, result);
        }

        [Ignore]
        [TestMethod]
        public async Task CountFailingTwitterUsers()
        {
            await Task.Delay(1000);
            var dal = new TwitterUserPostgresDal(_settings);
            for (var i = 0; i < 10; i++)
            {
                var acct = $"myid{i}";
                var lastTweetId = 1548L;

                await dal.CreateUserAsync(acct);

                if (i == 0 || i == 2 || i == 3)
                {
                    var t = await dal.GetUserAsync(acct);
                    await dal.UpdateTwitterUserAsync(t.Id ,1L, 50+i*2, DateTime.Now);
                }
            }

            var result = await dal.GetFailingTwitterUsersCountAsync();
            Assert.AreEqual(3, result);
        }
    }
}