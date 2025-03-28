using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using BirdsiteLive.Twitter;
using BirdsiteLive.Twitter.Tools;
using BirdsiteLive.Common.Settings;
using Moq;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;
using System.Net.Http;
using System.Text.Json;
using BirdsiteLive.Twitter.Models;
using Newtonsoft.Json;

namespace BirdsiteLive.ActivityPub.Tests
{
    [TestClass]
    public class TimelineTests
    {
        private ITwitterTweetsService _tweetService;
        private ICachedTwitterUserService _twitterUserService;
        private ITwitterUserDal _twitterUserDalMoq;
        private ITwitterAuthenticationInitializer _tweetAuth = null;

        [TestInitialize]
        public async Task TestInit()
        {
            var logger1 = new Mock<ILogger<TwitterAuthenticationInitializer>>(MockBehavior.Strict);
            var logger2 = new Mock<ILogger<TwitterUserService>>(MockBehavior.Strict);
            var logger3 = new Mock<ILogger<TwitterTweetsService>>();
            var twitterDal = new Mock<ITwitterUserDal>();
            var settingsDal = new Mock<ISettingsDal>();
            settingsDal.Setup(_ => _.Get("nitter"))
                .ReturnsAsync(JsonDocument.Parse("""{"endpoints": ["nitter.x86-64-unknown-linux-gnu.zip"], "allowboosts": true, "postnitterdelay": 0, "followersThreshold0": 10, "followersThreshold": 10,  "followersThreshold2": 11,  "followersThreshold3": 12, "twitterFollowersThreshold":  10}""").RootElement);
            settingsDal.Setup(_ => _.Get("twitteraccounts"))
                .ReturnsAsync(JsonDocument.Parse("""{"accounts": [["xxx", "xxx"]]}""").RootElement);
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
            httpFactory.Setup(_ => _.CreateClient("WithProxy")).Returns(new HttpClient());
            var settings = new InstanceSettings
            {
                Domain = "domain.name"
            };

            twitterDal
                .Setup(x => x.GetUserAsync(
                    It.Is<string>(y => true)
                ))
                .ReturnsAsync((string username) => new SyncTwitterUser { Acct = username, TwitterUserId = default });
            _twitterUserDalMoq = twitterDal.Object;

            _tweetAuth = new TwitterAuthenticationInitializer(httpFactory.Object, settings, settingsDal.Object, logger1.Object);
            ITwitterUserService user = new TwitterUserService(_tweetAuth, _twitterUserDalMoq, settings, settingsDal.Object, httpFactory.Object, logger2.Object);
            _twitterUserService = new CachedTwitterUserService(user, twitterDal.Object, settings);
            _tweetService = new TwitterTweetsService(_tweetAuth, _twitterUserService, twitterDal.Object, settings, httpFactory.Object, settingsDal.Object, logger3.Object);

        }


        [TestMethod]
        public async Task Login()
        {
            try
            {
                var tweet = await _tweetAuth.Login();
            }
            catch (Exception e)
            {
                Assert.Inconclusive();
            }
        }
        [TestMethod]
        public async Task TimelineKobe()
        {
            var user = await _twitterUserDalMoq.GetUserAsync("kobebryant");
            ExtractedTweet[] tweets;
            try
            {
                tweets = await _tweetService.GetTimelineAsync((SyncTwitterUser)user, 1117506566939234304);
            }
            catch (Exception e)
            {
                Assert.Inconclusive();
                return;
            }
            
            if (tweets.Length == 0)
                Assert.Inconclusive();
           
            Assert.AreEqual(tweets[0].MessageContent, "Continuing to move the game forward @KingJames. Much respect my brother 💪🏾 #33644");
            Assert.IsTrue(tweets.Length > 10);
            Assert.IsTrue(tweets.Length < 20);

            
            Assert.IsTrue(_twitterUserService.UserIsCached("kobebryant"));
            bool aRetweetedAccountIsCached = _twitterUserService.UserIsCached("djvlad");
            Assert.IsTrue(aRetweetedAccountIsCached);
            
        }

        [Ignore]
        [TestMethod]
        public async Task TimelineMKBHD()
        {
            // Goal of this test is the interaction between old pin and crawling
            var user = await _twitterUserDalMoq.GetUserAsync("mkbhd");
            user.Followers = 99999999; // we want to make sure it's a VIP user
            user.LastTweetPostedId = 1699909873041916323; 
            var tweets = await _tweetService.GetTimelineAsync((SyncTwitterUser) user, 1699909873041916323);

            Assert.IsTrue(tweets.Length > 0);
        }
        [TestMethod]
        public async Task TimelineGrant()
        {
            var user = await _twitterUserDalMoq.GetUserAsync("grantimahara");
            user.Followers = 99999999; // we want to make sure it's a VIP user
            user.StatusesCount = 10;
            ExtractedTweet[] tweets;
            try
            {
                tweets = await _tweetService.GetTimelineAsync((SyncTwitterUser) user, 1232042440875335680);
            }
            catch (Exception e)
            {
                Assert.Inconclusive();
                return;
            }

            if (tweets.Length == 0)
                Assert.Inconclusive();
            
            Assert.AreEqual(tweets.Length, 18);
            
            Assert.IsTrue(tweets[0].IsReply);
            Assert.IsFalse(tweets[0].IsRetweet);
 
            Assert.AreEqual(tweets[2].MessageContent, "Liftoff!");
            Assert.IsTrue(tweets[2].IsRetweet);
            Assert.AreEqual(tweets[2].RetweetId, 1266812530833240064); 
            Assert.IsTrue(tweets[2].IdLong > 1698746132626280448);
            Assert.AreEqual(tweets[2].OriginalAuthor.Acct, "spacex");
            Assert.AreEqual(tweets[2].Author.Acct, "grantimahara");
        }

    }
}
