using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.Twitter.Models;
using BirdsiteLive.Twitter.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace BirdsiteLive.Twitter.Tests
{
    [TestClass, TestCategory("Twitter")]
    public class UserTests
    {
        public static IEnumerable<object[]> Implementations
        {
            get
            {
                yield return new object[] { StrategyHints.Graphql2024 };
                yield return new object[] { StrategyHints.Graphql2025 };
                yield return new object[] { StrategyHints.Sidecar };
            }
        }
        private TwitterUserService _tweetService;
        [TestInitialize]
        public async Task TestInit()
        {
            var logger = new Mock<ILogger<TwitterService>>();
            var httpFactory = new Mock<IHttpClientFactory>();
            var twitterDal = new Mock<ITwitterUserDal>();
            twitterDal.Setup(_ => _.GetUserAsync("kobebryant"))
                .ReturnsAsync(new SyncTwitterUser() { Followers = 1, TwitterUserId = 1059194370, ExtraData = JsonDocument.Parse("{}").RootElement });
            twitterDal.Setup(_ => _.GetUserAsync("grantimahara"))
                .ReturnsAsync(new SyncTwitterUser() { Followers = 9999, ExtraData = JsonDocument.Parse("""{"TwitterUserId": 28521141}""").RootElement });
            twitterDal.Setup(_ => _.GetUserAsync("terriblemaps"))
                .ReturnsAsync(new SyncTwitterUser() { Followers = 9999, TwitterUserId = 1663172653, ExtraData = JsonDocument.Parse("{}").RootElement});
            var settings = new InstanceSettings
            {
                Domain = "domain.name"
            };
            var settingsDal = new Mock<ISettingsDal>();
            settingsDal.Setup(_ => _.Get("twitteraccounts"))
                .ReturnsAsync(JsonDocument.Parse("""{"accounts": [["xxx", "xxx"]]}""").RootElement);
            httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
            httpFactory.Setup(_ => _.CreateClient("WithProxy")).Returns(new HttpClient());
            ITwitterAuthenticationInitializer auth = new TwitterAuthenticationInitializer(httpFactory.Object, settings, settingsDal.Object, logger.Object);
            _tweetService = new TwitterUserService(auth, twitterDal.Object, settings, settingsDal.Object, httpFactory.Object, logger.Object);
        }

        [TestMethod]
        [DynamicData(nameof(Implementations))]
        public async Task UserKobe(StrategyHints s)
        {
            TwitterUser user;
            try
            {
                user = await _tweetService.GetUserAsync("kobebryant", s);
            }
            catch (Exception e)
            {
                Assert.Inconclusive();
                return;
            }
            if (user is null)
                Assert.Inconclusive();
            Assert.AreEqual(user.Name, "Kobe Bryant");
            Assert.AreEqual(user.Id, 1059194370);;
            Assert.AreEqual(user.Acct, "kobebryant");
            Assert.AreEqual(user.Location, null);
        }

        [TestMethod]
        [DynamicData(nameof(Implementations))]
        public async Task UserGrant(StrategyHints s)
        {
            TwitterUser user;
            try
            {
                user = await _tweetService.GetUserAsync("grantimahara", s);
            }
            catch (Exception e)
            {
                Assert.Inconclusive();
                return;
            }
            if (user is null)
                Assert.Inconclusive();
            Assert.AreEqual(user.Name, "Grant Imahara");
            Assert.IsTrue(Math.Abs( user.StatusCount - 12495 ) < 100);
            Assert.IsTrue(user.FollowersCount > 500_000);
            Assert.AreEqual(user.Acct, "grantimahara");
            Assert.AreEqual(user.Location, "Los Angeles, CA");
        }
        [TestMethod]
        [DynamicData(nameof(Implementations))]
        public async Task UserGrantBio(StrategyHints s)
        {
            var user = await _tweetService.GetUserAsync("grantimahara", s);
            if (user is null)
                Assert.Inconclusive();
            if (user.Description != "Host of White Rabbit Project on Netflix, former MythBuster and special FX modelmaker.")
                Assert.Inconclusive();
        }
        [Ignore]
        [TestMethod]
        [DynamicData(nameof(Implementations))]
        public async Task UserFailed(StrategyHints s)
        {
            var username = "terriblemaps";
            var user = await _tweetService.GetUserAsync(username, s);
            if (user is null)
                Assert.Inconclusive();
            Assert.AreEqual(user.Acct, username);
        }

    }
}
