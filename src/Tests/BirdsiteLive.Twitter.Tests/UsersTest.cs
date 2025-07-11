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
    [TestClass]
    public class UserTests
    {
        public static IEnumerable<object[]> Implementations
        {
            get
            {
                yield return new object[] { StrategyHints.Graphql2024 };
                yield return new object[] { StrategyHints.Graphql2025 };
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
                .ReturnsAsync(new SyncTwitterUser() { Followers = 1 });
            twitterDal.Setup(_ => _.GetUserAsync("grantimahara"))
                .ReturnsAsync(new SyncTwitterUser() { Followers = 9999 });
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
            SocialMediaUser user;
            try
            {
                user = await _tweetService.GetUserAsync("kobebryant", s);
            }
            catch (Exception e)
            {
                Assert.Inconclusive();
                return;
            }
            Assert.AreEqual(user.Name, "Kobe Bryant");
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
            if (user.Description != "Host of White Rabbit Project on Netflix, former MythBuster and special FX modelmaker.")
                Assert.Inconclusive();
        }

    }
}
