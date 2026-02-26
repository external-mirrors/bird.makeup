﻿using System;
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
    // LEARNED DEBUGGING STRATEGIES:
    // 1. Live Data Inspection: Use 'curl' and 'grep' to inspect the actual HTML output from Nitter (e.g., http://marci:8080/user/with_replies).
    //    This helps identify how Nitter's structure (like '.retweet-header' or '.timeline-item') has changed and if thread parents are included.
    // 2. Targeted Test Execution: Use 'run_test fqn:...' for specific test cases to quickly verify fixes without running the entire suite.
    // 3. Strategy Overlap: Check 'TwitterTweetsService.cs' to see which strategy (Nitter vs Vanilla) is actually being used based on mock settings (e.g., followersThreshold).
    
    [TestClass, TestCategory("Twitter")]
    public class TimelineTests
    {
        private sealed record GrantTimelineExpectation(
            string NonRetweetId,
            string CreatedAt,
            string MessageContent,
            bool IsReply,
            bool IsThread,
            bool IsRetweet,
            string AuthorAcct,
            string OriginalAuthorAcct,
            string InReplyToAccount,
            long? InReplyToStatusId,
            string QuotedAccount,
            string QuotedStatusId,
            int MediaCount);

        private static readonly GrantTimelineExpectation[] GrantTimelineGroundTruth = new[]
        {
            new GrantTimelineExpectation(NonRetweetId: "1276728375424413696", CreatedAt: "2020-06-27T04:05:00.0000000", MessageContent: "You drive a hard bargain, but no. #shouldhavethrownintheknights", IsReply: true, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: "annaakana", InReplyToStatusId: 1276226756429504512, QuotedAccount: null, QuotedStatusId: null, MediaCount: 0),
            new GrantTimelineExpectation(NonRetweetId: "1276184815784452096", CreatedAt: "2020-06-25T16:05:00.0000000", MessageContent: "Haha you know I can’t fam, but you can visit any time!", IsReply: true, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: "annaakana", InReplyToStatusId: 1275903980460109824, QuotedAccount: null, QuotedStatusId: null, MediaCount: 0),
            new GrantTimelineExpectation(NonRetweetId: null, CreatedAt: "2020-05-30T19:23:00.0000000", MessageContent: "Liftoff!", IsReply: false, IsThread: false, IsRetweet: true, AuthorAcct: "grantimahara", OriginalAuthorAcct: "spacex", InReplyToAccount: null, InReplyToStatusId: null, QuotedAccount: null, QuotedStatusId: null, MediaCount: 1),
            new GrantTimelineExpectation(NonRetweetId: "1266813611567005696", CreatedAt: "2020-05-30T19:27:00.0000000", MessageContent: "Congrats on a successful and historic liftoff @SpaceX @NASA #CrewDragon!!", IsReply: true, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: "SpaceX", InReplyToStatusId: 0, QuotedAccount: null, QuotedStatusId: null, MediaCount: 0),
            new GrantTimelineExpectation(NonRetweetId: "1266805131808354304", CreatedAt: "2020-05-30T18:54:00.0000000", MessageContent: "Half an hour to go until today’s launch attempt. @SpaceX @NASA Good luck #CrewDragon! Watch live here: https://youtu.be/bIZsnKGV8TE", IsReply: true, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: "SpaceX", InReplyToStatusId: 0, QuotedAccount: null, QuotedStatusId: null, MediaCount: 0),
            new GrantTimelineExpectation(NonRetweetId: "1265739709881348096", CreatedAt: "2020-05-27T20:20:00.0000000", MessageContent: "Launch scrubbed for today due to weather, which was the right call. Safety first, of course. Next window will be Saturday, I believe! Good effort @SpaceX @NASA #Dragon Team!", IsReply: true, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: "SpaceX", InReplyToStatusId: 0, QuotedAccount: null, QuotedStatusId: null, MediaCount: 0),
            new GrantTimelineExpectation(NonRetweetId: null, CreatedAt: "2020-05-25T23:23:00.0000000", MessageContent: "Strap yourself into the @SpaceX Dragon capsule that will take @NASA  astronauts to the @Space_Station Wednesday. \n\nDon't miss NASA & SpaceX: Journey to the Future tonight at 10pm ET!", IsReply: true, IsThread: false, IsRetweet: true, AuthorAcct: "grantimahara", OriginalAuthorAcct: "sciencechannel", InReplyToAccount: "SpaceX", InReplyToStatusId: 0, QuotedAccount: null, QuotedStatusId: null, MediaCount: 1),
            new GrantTimelineExpectation(NonRetweetId: "1257486604626612224", CreatedAt: "2020-05-05T01:45:00.0000000", MessageContent: "Happy #MayTheFourth from me and #BabyYoda! I was lucky enough to work at @Lucasfilm_Ltd and @ILMVFX on #StarWars projects from 1993-2005. I look back on those years fondly and look forward to their plans for the future of the franchise. #MayThe4thBeWithYou #TheMandalorian", IsReply: false, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: null, InReplyToStatusId: null, QuotedAccount: null, QuotedStatusId: null, MediaCount: 1),
            new GrantTimelineExpectation(NonRetweetId: "1243602462952448000", CreatedAt: "2020-03-27T18:15:00.0000000", MessageContent: "This gets funnier the longer it goes on. I didn’t know Alexa had an entire fart repertoire!", IsReply: true, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: "sethrogen", InReplyToStatusId: 1243377326890430464, QuotedAccount: null, QuotedStatusId: null, MediaCount: 0),
            new GrantTimelineExpectation(NonRetweetId: "1243601748230434816", CreatedAt: "2020-03-27T18:12:00.0000000", MessageContent: "@NathanFillion Happy birthday, good sir!", IsReply: true, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: "NathanFillion", InReplyToStatusId: 0, QuotedAccount: null, QuotedStatusId: null, MediaCount: 0),
            new GrantTimelineExpectation(NonRetweetId: "1243234010215530496", CreatedAt: "2020-03-26T17:51:00.0000000", MessageContent: "Show me your WFH space! Here’s mine: a bunch of electronics equipment on a foldout table. \ud83d\ude01", IsReply: false, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: null, InReplyToStatusId: null, QuotedAccount: null, QuotedStatusId: null, MediaCount: 1),
            new GrantTimelineExpectation(NonRetweetId: null, CreatedAt: "2020-03-20T08:33:00.0000000", MessageContent: "This custom-made Baby Yoda animatronic deserves all the chicky chicky nuggs  \ud83d\ude0d✨", IsReply: false, IsThread: false, IsRetweet: true, AuthorAcct: "grantimahara", OriginalAuthorAcct: "nowthisimpact", InReplyToAccount: null, InReplyToStatusId: null, QuotedAccount: null, QuotedStatusId: null, MediaCount: 1),
            new GrantTimelineExpectation(NonRetweetId: "1240772555843104774", CreatedAt: "2020-03-19T22:50:00.0000000", MessageContent: "And then, when I have to leave the house *for any reason* I feel like The Omega Man/I Am Legend.", IsReply: false, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: null, InReplyToStatusId: null, QuotedAccount: null, QuotedStatusId: null, MediaCount: 0),
            new GrantTimelineExpectation(NonRetweetId: "1240771067032907777", CreatedAt: "2020-03-19T22:44:00.0000000", MessageContent: "During self-quarantine, does anyone else feel like the guy in the bunker in LOST? Blending a breakfast shake, riding his stationary bike, and working on the computer in total isolation?", IsReply: false, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: null, InReplyToStatusId: null, QuotedAccount: null, QuotedStatusId: null, MediaCount: 0),
            new GrantTimelineExpectation(NonRetweetId: "1240771588946984961", CreatedAt: "2020-03-19T22:46:00.0000000", MessageContent: "Yes, but Desmond did not have any mystery burritos", IsReply: false, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: null, InReplyToStatusId: null, QuotedAccount: null, QuotedStatusId: null, MediaCount: 0),
            new GrantTimelineExpectation(NonRetweetId: "1237045404752990211", CreatedAt: "2020-03-09T15:59:00.0000000", MessageContent: "If you were wondering how I built my Baby Yoda, this article has more detailed info and exclusive behind the scenes photos. Enjoy! #StarWars #TheMandalorian #BabyYoda", IsReply: false, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: null, InReplyToStatusId: null, QuotedAccount: "cnet", QuotedStatusId: "1236864464273670145", MediaCount: 1),
            new GrantTimelineExpectation(NonRetweetId: "1237275290805530629", CreatedAt: "2020-03-10T07:13:00.0000000", MessageContent: "Absolutely Angel!", IsReply: true, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: "aannggeellll", InReplyToStatusId: 1237246455271673858, QuotedAccount: null, QuotedStatusId: null, MediaCount: 0),
            new GrantTimelineExpectation(NonRetweetId: "1236066383881859074", CreatedAt: "2020-03-06T23:09:00.0000000", MessageContent: "Pleased to present my newest creation: a fully animatronic Baby Yoda. Special thx to @SaltiestHime for silicone skin/paint/hair, @thelindsayjane for the coat and Project 842 for the digital model. Touring children’s hospitals starting in April! #BabyYoda #TheMandalorian #Starwars", IsReply: false, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: null, InReplyToStatusId: null, QuotedAccount: null, QuotedStatusId: null, MediaCount: 1),
            new GrantTimelineExpectation(NonRetweetId: "1232042440875335680", CreatedAt: "2020-02-24T20:39:00.0000000", MessageContent: "Used gaffer tape as a lint roller before a big pitch meeting. How’s your Monday going?", IsReply: false, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: null, InReplyToStatusId: null, QuotedAccount: null, QuotedStatusId: null, MediaCount: 0),
            new GrantTimelineExpectation(NonRetweetId: "1228936496490504192", CreatedAt: "2020-02-16T06:57:00.0000000", MessageContent: "Our definitions of nominal are somewhat different", IsReply: true, IsThread: false, IsRetweet: false, AuthorAcct: "grantimahara", OriginalAuthorAcct: null, InReplyToAccount: "tweetsoutloud", InReplyToStatusId: 1228777625734115328, QuotedAccount: null, QuotedStatusId: null, MediaCount: 0),
        };

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
                yield return new object[] { StrategyHints.Nitter };
            }
        }
        [TestInitialize]
        public async Task TestInit()
        {
            var logger = new Mock<ILogger<TwitterService>>();
            var twitterDal = new Mock<ITwitterUserDal>();
            var settingsDal = new Mock<ISettingsDal>();
            settingsDal.Setup(_ => _.Get("nitter"))
                .ReturnsAsync(JsonDocument.Parse("""{"endpoints": ["marci"], "lowtrustendpoints": [], "postnitterdelay": 0, "followersThreshold0": 10, "followersThreshold": 10,  "followersThreshold2": 11,  "followersThreshold3": 12, "twitterFollowersThreshold":  10}""").RootElement);
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

        private static string NormalizeOptional(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static void AssertGrantTimelineGroundTruth(ExtractedTweet[] tweets)
        {
            Assert.AreEqual(
                GrantTimelineGroundTruth.Length,
                tweets.Length,
                $"Grant timeline count mismatch. expected={GrantTimelineGroundTruth.Length} actual={tweets.Length}");

            for (var i = 0; i < GrantTimelineGroundTruth.Length; i++)
            {
                var expected = GrantTimelineGroundTruth[i];
                var actual = tweets[i];

                if (!expected.IsRetweet)
                    Assert.AreEqual(expected.NonRetweetId, actual.Id, $"Grant timeline id mismatch at index {i}");
                Assert.AreEqual(
                    expected.CreatedAt,
                    actual.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"),
                    $"Grant timeline createdAt mismatch at index {i}");
                Assert.AreEqual(expected.MessageContent, actual.MessageContent, $"Grant timeline message mismatch at index {i}");
                Assert.AreEqual(expected.IsReply, actual.IsReply, $"Grant timeline isReply mismatch at index {i}");
                Assert.AreEqual(expected.IsThread, actual.IsThread, $"Grant timeline isThread mismatch at index {i}");
                Assert.AreEqual(expected.IsRetweet, actual.IsRetweet, $"Grant timeline isRetweet mismatch at index {i}");
                Assert.AreEqual(expected.AuthorAcct, actual.Author?.Acct, $"Grant timeline author mismatch at index {i}");
                Assert.AreEqual(
                    expected.OriginalAuthorAcct,
                    NormalizeOptional(actual.OriginalAuthor?.Acct),
                    $"Grant timeline original author mismatch at index {i}");
                Assert.AreEqual(
                    expected.InReplyToAccount,
                    NormalizeOptional(actual.InReplyToAccount),
                    $"Grant timeline inReplyToAccount mismatch at index {i}");
                Assert.AreEqual(
                    expected.InReplyToStatusId,
                    actual.InReplyToStatusId,
                    $"Grant timeline inReplyToId mismatch at index {i}");
                Assert.AreEqual(
                    expected.QuotedAccount,
                    NormalizeOptional(actual.QuotedAccount),
                    $"Grant timeline quotedAccount mismatch at index {i}");
                Assert.AreEqual(
                    expected.QuotedStatusId,
                    NormalizeOptional(actual.QuotedStatusId),
                    $"Grant timeline quotedId mismatch at index {i}");
                Assert.AreEqual(
                    expected.MediaCount,
                    actual.Media?.Length ?? 0,
                    $"Grant timeline media count mismatch at index {i}");
            }
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
        // Vanilla now returns the top tweets, ordered by likes. We want them chronologically. Still useful to keep around as backup.
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
           
            Assert.IsTrue(Array.Exists(
                tweets,
                t => t.MessageContent == "Continuing to move the game forward @KingJames. Much respect my brother 💪🏾 #33644"));
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

            AssertGrantTimelineGroundTruth(tweets);
        }

    }
}
