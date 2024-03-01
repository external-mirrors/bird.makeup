﻿using System;
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
    public class IgUserPostgresDalTests : PostgresTestingBase
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
        public async Task GetIgUserAsync_NoUser()
        {
            var dal = new InstagramUserPostgresDal(_settings);
            var result = await dal.GetUserAsync("dontexist");
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task CreateAndGetUser()
        {
            var acct = "myid";

            var dal = new InstagramUserPostgresDal(_settings);

            await dal.CreateUserAsync(acct);
            var result = await dal.GetUserAsync(acct);

            Assert.AreEqual(acct, result.Acct);
            Assert.AreEqual(0, result.FetchingErrorCount);
            Assert.IsTrue(result.Id > 0);
        }

        [TestMethod]
        public async Task CreateAndGetUser_byId()
        {
            var acct = "myid";

            var dal = new InstagramUserPostgresDal(_settings);

            await dal.CreateUserAsync(acct);
            var result = await dal.GetUserAsync(acct);
            var resultById = await dal.GetUserAsync(result.Id);

            Assert.AreEqual(acct, resultById.Acct);
            Assert.AreEqual(result.Id, resultById.Id);
        }

        [Ignore]
        [TestMethod]
        public async Task CreateUpdateAndGetUser()
        {
            var acct = "myid";
            var lastTweetId = 1548L;

            var dal = new InstagramUserPostgresDal(_settings);

            await dal.CreateUserAsync(acct);
            var result = await dal.GetUserAsync(acct);


            var updatedLastTweetId = 1600L;
            var updatedLastSyncId = 1550L;
            var now = DateTime.Now;
            var errors = 15;
            //await dal.UpdateTwitterUserAsync(result.Id, updatedLastTweetId, errors, now);

            result = await dal.GetUserAsync(acct);

            Assert.AreEqual(acct, result.Acct);
            Assert.AreEqual(updatedLastTweetId, result.LastTweetPostedId);
            Assert.AreEqual(errors, result.FetchingErrorCount);
            Assert.IsTrue(Math.Abs((now.ToUniversalTime() - result.LastSync).Milliseconds) < 100);
        }

        [Ignore]
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

        [Ignore]
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

        [Ignore]
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task Update_NoId()
        {
            var dal = new TwitterUserPostgresDal(_settings);
            await dal.UpdateTwitterUserAsync(default, default, default, DateTime.UtcNow);
        }

        [Ignore]
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task Update_NoLastTweetPostedId()
        {
            var dal = new TwitterUserPostgresDal(_settings);
            await dal.UpdateTwitterUserAsync(12, default,  default, DateTime.UtcNow);
        }


        [Ignore]
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task Update_NoLastSync()
        {
            var dal = new TwitterUserPostgresDal(_settings);
            await dal.UpdateTwitterUserAsync(12, 9556, 65,  default);
        }

        [Ignore]
        [TestMethod]
        public async Task CreateAndDeleteUser()
        {
            var acct = "myacct";
            var lastTweetId = 1548L;

            var dal = new TwitterUserPostgresDal(_settings);

            await dal.CreateUserAsync(acct);
            var result = await dal.GetUserAsync(acct);
            Assert.IsNotNull(result);

            await dal.DeleteUserAsync(acct);
            result = await dal.GetUserAsync(acct);
            Assert.IsNull(result);
        }

        [Ignore]
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task DeleteUser_NotAcct()
        {
            var dal = new TwitterUserPostgresDal(_settings);
            await dal.DeleteUserAsync(string.Empty);
        }

        [Ignore]
        [TestMethod]
        public async Task CreateAndDeleteUser_byId()
        {
            var acct = "myacct";
            var lastTweetId = 1548L;

            var dal = new TwitterUserPostgresDal(_settings);

            await dal.CreateUserAsync(acct);
            var result = await dal.GetUserAsync(acct);
            Assert.IsNotNull(result);

            await dal.DeleteUserAsync(result.Id);
            result = await dal.GetUserAsync(acct);
            Assert.IsNull(result);
        }

        [Ignore]
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task DeleteUser_NotAcct_byId()
        {
            var dal = new TwitterUserPostgresDal(_settings);
            await dal.DeleteUserAsync(default(int));
        }

        [Ignore]
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

        [Ignore]
        [TestMethod]
        public async Task GetAllTwitterUsers_Top_NotInit()
        {
            // Create accounts
            var dal = new TwitterUserPostgresDal(_settings);
            for (var i = 0; i < 1000; i++)
            {
                var acct = $"myid{i}";
                var lastTweetId = i+10;

                await dal.CreateUserAsync(acct);
            }

            // Update accounts
            var now = DateTime.UtcNow;
            var allUsers = await dal.GetAllTwitterUsersAsync();
            foreach (var acc in allUsers)
            {
                var lastSync = now.AddDays(acc.LastTweetPostedId);
                acc.LastSync = lastSync;
                await dal.UpdateTwitterUserAsync(acc);
            }

            // Create a not init account
            await dal.CreateUserAsync("not_init");

            var result = await dal.GetAllTwitterUsersAsync(10);

            Assert.IsTrue(result.Any(x => x.Acct == "not_init"));
        }

        [Ignore]
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

        [Ignore]
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

        [Ignore]
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