using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
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
    public class TimelineTests
    {
        private TwitterTweetsService _tweetService;
        private ITwitterUserService _twitterUserService;
        private ITwitterUserDal _twitterUserDalMoq;
        private ITwitterAuthenticationInitializer _tweetAuth = null;

        public static IEnumerable<object[]> Implementations
        {
            get
            {
                yield return new object[] { StrategyHints.Graphql2024 };
                yield return new object[] { StrategyHints.Graphql2025 };
                yield return new object[] { StrategyHints.Sidecar };
            }
        }
        [TestInitialize]
        public async Task TestInit()
        {
            var logger = new Mock<ILogger<TwitterService>>();
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
                    It.Is<string>(y => y == "kobebryant")
                ))
                .ReturnsAsync((string username) => new SyncTwitterUser { Acct = username, Followers = 1059194370, ExtraData = JsonDocument.Parse("{\"TwitterUserIde\":1059194370}").RootElement});
            twitterDal
                .Setup(x => x.GetUserAsync(
                    It.Is<string>(y => y == "grantimahara")
                ))
                .ReturnsAsync((string username) => new SyncTwitterUser { Acct = username, Followers = 99999, ExtraData = JsonDocument.Parse("""{}""").RootElement});
            twitterDal
                .Setup(x => x.GetUserAsync(
                    It.Is<string>(y => y == "mkbhd")
                ))
                .ReturnsAsync((string username) => new SyncTwitterUser { Acct = username, TwitterUserId = 29873662, ExtraData = JsonDocument.Parse("""{}""").RootElement});
            twitterDal
                .Setup(x => x.GetUserAsync(
                    It.Is<string>(y => y == "askvenice")
                ))
                .ReturnsAsync((string username) => new SyncTwitterUser { Acct = username, TwitterUserId = 1764736490515685376, ExtraData = JsonDocument.Parse("{}").RootElement});
            _twitterUserDalMoq = twitterDal.Object;

            _tweetAuth = new TwitterAuthenticationInitializer(httpFactory.Object, settings, settingsDal.Object, logger.Object);
            ITwitterUserService user = new TwitterUserService(_tweetAuth, _twitterUserDalMoq, settings, settingsDal.Object, httpFactory.Object, logger.Object);
            _tweetService = new TwitterTweetsService(_tweetAuth, user, twitterDal.Object, settings, httpFactory.Object, settingsDal.Object, logger.Object);

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
        public async Task TimelineKobeVanilla()
        {
            var user = await _twitterUserDalMoq.GetUserAsync("kobebryant");
            ExtractedTweet[] tweets;
            try
            {
                tweets = await _tweetService.GetTimelineAsync((SyncTwitterUser)user);
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
        }
        [TestMethod]
        [DynamicData(nameof(Implementations))]
        public async Task TimelineKobe(StrategyHints s)
        {
            var user = await _twitterUserDalMoq.GetUserAsync("kobebryant");
            ExtractedTweet[] tweets;
            try
            {
                tweets = await _tweetService.GetTimelineAsync((SyncTwitterUser)user, s);
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
        }

        [Ignore]
        [TestMethod]
        [DynamicData(nameof(Implementations))]
        public async Task TimelineMKBHD(StrategyHints s)
        {
            // Goal of this test is the interaction between old pin and crawling
            var user = await _twitterUserDalMoq.GetUserAsync("mkbhd");
            user.Followers = 99999999; // we want to make sure it's a VIP user
            user.LastTweetPostedId = 1699909873041916323; 
            var tweets = await _tweetService.GetTimelineAsync((SyncTwitterUser) user, s);

            Assert.IsTrue(tweets.Length > 0);
        }
        [Ignore]
        [TestMethod]
        [DynamicData(nameof(Implementations))]
        public async Task TimelineGrant(StrategyHints s)
        {
            var user = await _twitterUserDalMoq.GetUserAsync("grantimahara");
            user.Followers = 99999999; // we want to make sure it's a VIP user
            user.StatusesCount = 10;
            ExtractedTweet[] tweets;
            try
            {
                tweets = await _tweetService.GetTimelineAsync((SyncTwitterUser) user, s);
            }
            catch (Exception e)
            {
                Assert.Inconclusive();
                return;
            }

            if (tweets.Length == 0)
                Assert.Inconclusive();
            
            //Assert.AreEqual(tweets.Length, 18);
            
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
