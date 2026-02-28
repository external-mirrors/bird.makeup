using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BirdsiteLive.Common.Models;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.DAL.Postgres.DataAccessLayers;
using BirdsiteLive.DAL.Postgres.Tests.DataAccessLayers.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BirdsiteLive.DAL.Postgres.Tests.DataAccessLayers
{
    [TestClass]
    public class FollowersPostgresDalTests : PostgresTestingBase
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
        public async Task CreateAndGetFollower()
        {
            var acct = "myhandle";
            var host = "domain.ext";
            var following = new[] { 12, 19, 23 };
            var followingSync = new Dictionary<int, long>()
            {
                {12, 165L},
                {19, 166L},
                {23, 167L}
            };
            var inboxRoute = "/myhandle/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{acct}";

            var dal = new FollowersPostgresDal(_settings);
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);

            var result = await dal.GetFollowerAsync(acct, host);

            Assert.IsNotNull(result);
            Assert.AreEqual(acct, result.Acct);
            Assert.AreEqual(host, result.Host);
            Assert.AreEqual(inboxRoute, result.InboxRoute);
            Assert.AreEqual(sharedInboxRoute, result.SharedInboxRoute);
            Assert.AreEqual(actorId, result.ActorId);
            Assert.AreEqual(0, result.PostingErrorCount);
            Assert.AreEqual(following.Length, result.Followings.Count);
            Assert.AreEqual(following[0], result.Followings[0]);
        }
        [TestMethod]
        public async Task CreateAndGetFollower_multinetwork()
        {
            var acct = "myhandle";
            var host = "domain.ext";
            var following = new[] { 12, 19, 23 };
            var inboxRoute = "/myhandle/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{acct}";

            var dal = new FollowersPostgresDal(_settings);
            var dalIg = new InstagramUserPostgresDal(_settings);
            var dalTwitter = new TwitterUserPostgresDal(_settings);
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);


            var result = await dal.GetFollowerAsync(acct, host);

            Assert.IsNotNull(result);
            Assert.AreEqual(acct, result.Acct);
            Assert.AreEqual(host, result.Host);
            Assert.AreEqual(inboxRoute, result.InboxRoute);
            Assert.AreEqual(sharedInboxRoute, result.SharedInboxRoute);
            Assert.AreEqual(actorId, result.ActorId);
            Assert.AreEqual(0, result.PostingErrorCount);
            Assert.AreEqual(following.Length, result.Followings.Count);
            Assert.AreEqual(following[0], result.Followings[0]);
            
            await dalIg.AddFollower(result.Id, 99);
            await dalTwitter.AddFollower(result.Id, 420);
            
            var result2 = await dal.GetFollowerAsync(acct, host);
            Assert.AreEqual(5, result2.TotalFollowings);
        }
        [TestMethod]
        public async Task CreateGetAndRemoveFollower_Abstract()
        {
            var acct = "myhandle";
            var host = "domain.ext";
            var following = new[] { 12, 19, 23 };
            var inboxRoute = "/myhandle/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{acct}";

            var dalF = new FollowersPostgresDal(_settings);
            var dalT = new TwitterUserPostgresDal(_settings);
            await dalF.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, Array.Empty<int>());

            var result1 = await dalF.GetFollowerAsync(acct, host);
            foreach (int f in following)
            {
                await dalT.AddFollower(result1.Id, f);
            }
            var result = await dalF.GetFollowerAsync(acct, host);

            Assert.IsNotNull(result);
            Assert.AreEqual(acct, result.Acct);
            Assert.AreEqual(host, result.Host);
            Assert.AreEqual(inboxRoute, result.InboxRoute);
            Assert.AreEqual(sharedInboxRoute, result.SharedInboxRoute);
            Assert.AreEqual(actorId, result.ActorId);
            Assert.AreEqual(0, result.PostingErrorCount);
            Assert.AreEqual(following.Length, result.Followings.Count);
            Assert.AreEqual(following[0], result.Followings[0]);

            var count1 = await dalT.GetFollowersCountAsync(19);
            Assert.AreEqual(count1, 1);
            
            await dalT.RemoveFollower(result1.Id, 19);
            var result3 = await dalF.GetFollowerAsync(acct, host);
            Assert.AreEqual(12, result3.Followings[0]);
            Assert.AreEqual(23, result3.Followings[1]);
            Assert.AreEqual(2, result3.Followings.Count);
            
            var count2 = await dalT.GetFollowersCountAsync(19);
            Assert.AreEqual(count2, 0);
        }

        [TestMethod]
        public async Task CreateAndGetFollower_NoFollowings()
        {
            var acct = "myhandle";
            var host = "domain.ext";
            var inboxRoute = "/myhandle/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{acct}";

            var dal = new FollowersPostgresDal(_settings);
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, null!);

            var result = await dal.GetFollowerAsync(acct, host);

            Assert.IsNotNull(result);
            Assert.AreEqual(acct, result.Acct);
            Assert.AreEqual(host, result.Host);
            Assert.AreEqual(actorId, result.ActorId);
            Assert.AreEqual(inboxRoute, result.InboxRoute);
            Assert.AreEqual(sharedInboxRoute, result.SharedInboxRoute);
            Assert.AreEqual(0, result.Followings.Count);
            Assert.AreEqual(0, result.PostingErrorCount);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task GetFollowers_NoId()
        {
            var dal = new TwitterUserPostgresDal(_settings);
            await dal.GetFollowersAsync(default);
        }

        [TestMethod]
        public async Task CreateAndGetFollower_NoSharedInbox()
        {
            var acct = "myhandle";
            var host = "domain.ext";
            var following = new[] { 12, 19, 23 };
            var followingSync = new Dictionary<int, long>()
            {
                {12, 165L},
                {19, 166L},
                {23, 167L}
            };
            var inboxRoute = "/myhandle/inbox";
            string? sharedInboxRoute = null;
            var actorId = $"https://{host}/{acct}";

            var dal = new FollowersPostgresDal(_settings);
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute!, actorId, following);

            var result = await dal.GetFollowerAsync(acct, host);

            Assert.IsNotNull(result);
            Assert.AreEqual(acct, result.Acct);
            Assert.AreEqual(host, result.Host);
            Assert.AreEqual(inboxRoute, result.InboxRoute);
            Assert.AreEqual(actorId, result.ActorId);
            Assert.AreEqual(sharedInboxRoute, result.SharedInboxRoute);
            Assert.AreEqual(following.Length, result.Followings.Count);
            Assert.AreEqual(following[0], result.Followings[0]);
            Assert.AreEqual(0, result.PostingErrorCount);
        }

        [TestMethod]
        public async Task GetFollowersAsync()
        {
            var dal = new FollowersPostgresDal(_settings);
            var dalTwitter = new TwitterUserPostgresDal(_settings);

            //User 1 
            var acct = "myhandle1";
            var host = "domain.ext";
            var following = new[] { 1, 2, 3 };
            var followingSync = new Dictionary<int, long>();
            var inboxRoute = "/myhandle1/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{acct}";
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);

            //User 2 
            acct = "myhandle2";
            host = "domain.ext";
            following = new[] { 2, 4, 5 };
            inboxRoute = "/myhandle2/inbox";
            sharedInboxRoute = "/inbox2";
            actorId = $"https://{host}/{acct}";
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);

            //User 2 
            acct = "myhandle3";
            host = "domain.ext";
            following = new[] { 1 };
            inboxRoute = "/myhandle3/inbox";
            sharedInboxRoute = "/inbox3";
            actorId = $"https://{host}/{acct}";
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);

            var result = await dalTwitter.GetFollowersAsync(2);
            Assert.AreEqual(2, result.Length);

            result = await dalTwitter.GetFollowersAsync(5);
            Assert.AreEqual(1, result.Length);

            result = await dalTwitter.GetFollowersAsync(24);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public async Task GetAllFollowersAsync()
        {
            var dal = new FollowersPostgresDal(_settings);

            //User 1 
            var acct = "myhandle1";
            var host = "domain.ext";
            var following = new[] { 1, 2, 3 };
            var followingSync = new Dictionary<int, long>();
            var inboxRoute = "/myhandle1/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{acct}";
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);

            //User 2 
            acct = "myhandle2";
            host = "domain.ext";
            following = new[] { 2, 4, 5 };
            inboxRoute = "/myhandle2/inbox";
            sharedInboxRoute = "/inbox2";
            actorId = $"https://{host}/{acct}";
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);

            //User 2 
            acct = "myhandle3";
            host = "domain.ext";
            following = new[] { 1 };
            inboxRoute = "/myhandle3/inbox";
            sharedInboxRoute = "/inbox3";
            actorId = $"https://{host}/{acct}";
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);

            var result = await dal.GetAllFollowersAsync();
            Assert.AreEqual(3, result.Length);
        }

        [TestMethod]
        public async Task CountFollowersAsync()
        {
            var dal = new FollowersPostgresDal(_settings);

            var result = await dal.GetFollowersCountAsync();
            Assert.AreEqual(0, result);

            //User 1 
            var acct = "myhandle1";
            var host = "domain.ext";
            var following = new[] { 1, 2, 3 };
            var followingSync = new Dictionary<int, long>();
            var inboxRoute = "/myhandle1/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{acct}";
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);

            //User 2 
            acct = "myhandle2";
            host = "domain.ext";
            following = new[] { 2, 4, 5 };
            inboxRoute = "/myhandle2/inbox";
            sharedInboxRoute = "/inbox2";
            actorId = $"https://{host}/{acct}";
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);

            //User 3
            acct = "myhandle3";
            host = "domain.ext";
            following = new[] { 1 };
            inboxRoute = "/myhandle3/inbox";
            sharedInboxRoute = "/inbox3";
            actorId = $"https://{host}/{acct}";
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);

            result = await dal.GetFollowersCountAsync();
            Assert.AreEqual(3, result);
        }

        [TestMethod]
        public async Task CountFailingFollowersAsync()
        {
            var dal = new FollowersPostgresDal(_settings);

            var result = await dal.GetFailingFollowersCountAsync();
            Assert.AreEqual(0, result);

            //User 1 
            var acct = "myhandle1";
            var host = "domain.ext";
            var following = new[] { 1, 2, 3 };
            var followingSync = new Dictionary<int, long>();
            var inboxRoute = "/myhandle1/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{acct}";
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);

            //User 2 
            acct = "myhandle2";
            host = "domain.ext";
            following = new[] { 2, 4, 5 };
            inboxRoute = "/myhandle2/inbox";
            sharedInboxRoute = "/inbox2";
            actorId = $"https://{host}/{acct}";
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);

            var follower = await dal.GetFollowerAsync(acct, host);
            await dal.UpdateFollowerErrorCountAsync(follower.Id, 1);

            //User 3
            acct = "myhandle3";
            host = "domain.ext";
            following = new[] { 1 };
            inboxRoute = "/myhandle3/inbox";
            sharedInboxRoute = "/inbox3";
            actorId = $"https://{host}/{acct}";
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);

            follower = await dal.GetFollowerAsync(acct, host);
            await dal.UpdateFollowerErrorCountAsync(follower.Id, 50);

            result = await dal.GetFailingFollowersCountAsync();
            Assert.AreEqual(2, result);
        }

        [TestMethod]
        public async Task CreateUpdateAndGetFollower_Add()
        {
            var acct = "myhandle";
            var host = "domain.ext";
            var following = new[] { 12, 19, 23 };
            var inboxRoute = "/myhandle/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{acct}";

            var dal = new FollowersPostgresDal(_settings);
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);
            var result = await dal.GetFollowerAsync(acct, host);

            var updatedFollowing = new List<int> { 12, 19, 23, 24 };
            
            var dalTwitter = new TwitterUserPostgresDal(_settings);
            foreach (int f in updatedFollowing)
                await dalTwitter.AddFollower(result.Id, f);
            
            await dal.UpdateFollowerErrorCountAsync(result.Id, 10);
            result = await dal.GetFollowerAsync(acct, host);

            Assert.AreEqual(updatedFollowing.Count + following.Length, result.Followings.Count);
            Assert.AreEqual(updatedFollowing[0], result.Followings[0]);
            Assert.AreEqual(10, result.PostingErrorCount);
        }

        [TestMethod]
        public async Task CreateUpdateAndGetFollower_Integer()
        {
            var acct = "myhandle";
            var host = "domain.ext";
            var following = new[] { 12, 19, 23 };
            var inboxRoute = "/myhandle/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{acct}";

            var dal = new FollowersPostgresDal(_settings);
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);
            var result = await dal.GetFollowerAsync(acct, host);

            var updatedFollowing = new List<int> { 12, 19, 23, 24 };

            var dalTwitter = new TwitterUserPostgresDal(_settings);
            foreach (int f in updatedFollowing)
                await dalTwitter.AddFollower(result.Id, f);
            await dal.UpdateFollowerErrorCountAsync(result.Id, 32768);
            result = await dal.GetFollowerAsync(acct, host);

            Assert.AreEqual(updatedFollowing.Count + following.Length, result.Followings.Count);
            Assert.AreEqual(updatedFollowing[0], result.Followings[0]);
            Assert.AreEqual(32768, result.PostingErrorCount);
        }

        [TestMethod]
        public async Task CreateUpdateAndGetFollower_Remove()
        {
            var acct = "myhandle";
            var host = "domain.ext";
            var following = new[] { 12, 19, 23 };
            var inboxRoute = "/myhandle/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{acct}";

            var dal = new FollowersPostgresDal(_settings);
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);
            var result = await dal.GetFollowerAsync(acct, host);

            var updatedFollowing = new[] { 12, 19 };

            var dalTwitter = new TwitterUserPostgresDal(_settings);
            foreach (int f in updatedFollowing)
                await dalTwitter.AddFollower(result.Id, f);
            await dal.UpdateFollowerErrorCountAsync(result.Id, 5);
            result = await dal.GetFollowerAsync(acct, host);

            Assert.AreEqual(updatedFollowing.Length + following.Length, result.Followings.Count);
            Assert.AreEqual(updatedFollowing[0], result.Followings[0]);
            Assert.AreEqual(5, result.PostingErrorCount);
        }

        [TestMethod]
        public async Task CreateUpdateAndGetFollower_ResetErrorCount()
        {
            var acct = "myhandle";
            var host = "domain.ext";
            var following = new[] { 12, 19, 23 };
            var inboxRoute = "/myhandle/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{acct}";

            var dal = new FollowersPostgresDal(_settings);
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);
            var result = await dal.GetFollowerAsync(acct, host);
            Assert.AreEqual(0, result.PostingErrorCount);

            result.PostingErrorCount = 5;

            await dal.UpdateFollowerErrorCountAsync(result.Id, 5);
            result = await dal.GetFollowerAsync(acct, host);
            Assert.AreEqual(5, result.PostingErrorCount);

            result.PostingErrorCount = 0;

            await dal.UpdateFollowerErrorCountAsync(result.Id, 0);
            result = await dal.GetFollowerAsync(acct, host);
            Assert.AreEqual(0, result.PostingErrorCount);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task Update_NoFollowerId()
        {
            var follower = new Follower
            {
                Id = default
            };

            var dal = new FollowersPostgresDal(_settings);
            await dal.UpdateFollowerErrorCountAsync(follower.Id, 0);
        }

        [TestMethod]
        public async Task CreateAndDeleteFollower_ById()
        {
            var acct = "myhandle";
            var host = "domain.ext";
            var following = new[] { 12, 19, 23 };
            var followingSync = new Dictionary<int, long>()
            {
                {12, 165L},
                {19, 166L},
                {23, 167L}
            };
            var inboxRoute = "/myhandle/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{acct}";

            var dal = new FollowersPostgresDal(_settings);
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);
            var result = await dal.GetFollowerAsync(acct, host);
            Assert.IsNotNull(result);

            await dal.DeleteFollowerAsync(result.Id);

            result = await dal.GetFollowerAsync(acct, host);
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task CreateAndDeleteFollower_ByHandle()
        {
            var acct = "myhandle";
            var host = "domain.ext";
            var following = new[] { 12, 19, 23 };
            var followingSync = new Dictionary<int, long>()
            {
                {12, 165L},
                {19, 166L},
                {23, 167L}
            };
            var inboxRoute = "/myhandle/inbox";
            var sharedInboxRoute = "/inbox";
            var actorId = $"https://{host}/{acct}";

            var dal = new FollowersPostgresDal(_settings);
            await dal.CreateFollowerAsync(acct, host, inboxRoute, sharedInboxRoute, actorId, following);
            var result = await dal.GetFollowerAsync(acct, host);
            Assert.IsNotNull(result);

            await dal.DeleteFollowerAsync(acct, host);

            result = await dal.GetFollowerAsync(acct, host);
            Assert.IsNull(result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task Delete_NoFollowerId()
        {
            var dal = new FollowersPostgresDal(_settings);
            await dal.DeleteFollowerAsync(default);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task Delete_NoAcct()
        {
            var dal = new FollowersPostgresDal(_settings);
            await dal.DeleteFollowerAsync(string.Empty, string.Empty);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task Delete_NoHost()
        {
            var dal = new FollowersPostgresDal(_settings);
            await dal.DeleteFollowerAsync("acct", string.Empty);
        }
    }
}
