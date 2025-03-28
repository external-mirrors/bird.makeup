using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using BirdsiteLive.Twitter;
using BirdsiteLive.Twitter.Tools;
using Moq;
using System.Net.Http;
using System.Text.Json;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;
using BirdsiteLive.Twitter.Models;

namespace BirdsiteLive.ActivityPub.Tests
{
    [TestClass]
    public class UserTests
    {
        private ITwitterUserService _tweetService;
        [TestInitialize]
        public async Task TestInit()
        {
            var logger1 = new Mock<ILogger<TwitterAuthenticationInitializer>>(MockBehavior.Strict);
            var logger2 = new Mock<ILogger<TwitterUserService>>(MockBehavior.Strict);
            var logger3 = new Mock<ILogger<TwitterUserService>>();
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
            ITwitterAuthenticationInitializer auth = new TwitterAuthenticationInitializer(httpFactory.Object, settings, settingsDal.Object, logger1.Object);
            _tweetService = new TwitterUserService(auth, twitterDal.Object, settings, settingsDal.Object, httpFactory.Object, logger2.Object);
        }

        [TestMethod]
        public async Task UserKobe()
        {
            SocialMediaUser user;
            try
            {
                user = await _tweetService.GetUserAsync("kobebryant");
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
        public async Task UserGrant()
        {
            TwitterUser user;
            try
            {
                user = await _tweetService.GetUserAsync("grantimahara");
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
        public async Task UserGrantBio()
        {
            var user = await _tweetService.GetUserAsync("grantimahara");
            if (user.Description != "Host of White Rabbit Project on Netflix, former MythBuster and special FX modelmaker.")
                Assert.Inconclusive();
        }

    }
}
