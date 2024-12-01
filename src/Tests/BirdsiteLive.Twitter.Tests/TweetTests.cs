using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using BirdsiteLive.Twitter;
using BirdsiteLive.Twitter.Tools;
using Moq;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Common.Settings;
using System.Net.Http;

namespace BirdsiteLive.ActivityPub.Tests
{
    [TestClass]
    public class TweetTests
    {
        private ITwitterTweetsService _tweetService = null;
        private ITwitterAuthenticationInitializer _tweetAuth = null;
        
        [TestInitialize]
        public async Task TestInit()
        {
            if (_tweetService != null)
                return;
            
            var logger1 = new Mock<ILogger<TwitterAuthenticationInitializer>>(MockBehavior.Strict);
            var logger2 = new Mock<ILogger<TwitterUserService>>(MockBehavior.Strict);
            var logger3 = new Mock<ILogger<TwitterTweetsService>>();
            var twitterDal = new Mock<ITwitterUserDal>();
            var settingsDal = new Mock<ISettingsDal>();
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
            httpFactory.Setup(_ => _.CreateClient("WithProxy")).Returns(new HttpClient());
            var settings = new InstanceSettings
            {
                Domain = "domain.name"
            };
            _tweetAuth = new TwitterAuthenticationInitializer(httpFactory.Object, settings, settingsDal.Object, logger1.Object);
            ITwitterUserService user = new TwitterUserService(_tweetAuth, twitterDal.Object, settings, settingsDal.Object, httpFactory.Object, logger2.Object);
            ICachedTwitterUserService user2 = new CachedTwitterUserService(user, twitterDal.Object, settings);
            _tweetService = new TwitterTweetsService(_tweetAuth, user2, twitterDal.Object, settings, httpFactory.Object, settingsDal.Object, logger3.Object);

        }


        [TestMethod]
        public async Task SimpleTextTweet()
        {
            var tweet = await _tweetService.GetTweetAsync(1600905296892891149);
            Assert.AreEqual(tweet.MessageContent, "We’re strengthening American manufacturing by creating 750,000 manufacturing jobs since I became president.");
            Assert.AreEqual(tweet.IdLong, 1600905296892891149);
            Assert.AreEqual(tweet.CreatedAt, new DateTime(2022, 12, 8, 17, 29, 0));
            Assert.IsFalse(tweet.IsRetweet);
            Assert.IsFalse(tweet.IsReply);
        }

        [TestMethod]
        public async Task SimpleTextAndSinglePictureTweet()
        {
            var tweet = await _tweetService.GetTweetAsync(1593344577385160704);
            Assert.AreEqual(tweet.MessageContent, "Speaker Nancy Pelosi will go down as one of most accomplished legislators in American history—breaking barriers, opening doors for others, and working every day to serve the American people. I couldn’t be more grateful for her friendship and leadership.");

            Assert.AreEqual(tweet.Media[0].MediaType, "image/jpeg");
            Assert.AreEqual(tweet.Media.Length, 1);
            Assert.AreEqual(tweet.Media[0].AltText, "President Obama with Speaker Nancy Pelosi in DC.");
        }
        [TestMethod]
        public async Task SimpleTextAndSingleLinkTweet()
        {
            var tweet = await _tweetService.GetTweetAsync(1602618920996945922);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.AreEqual(tweet.MessageContent, "#Linux 6.2 Expands Support For More #Qualcomm #Snapdragon SoCs, #Apple M1 Pro/Ultra/Max\n\nhttps://www.phoronix.com/news/Linux-6.2-Arm-SoC-Updates");
        }

        [TestMethod]
        public async Task SimpleTextAndSingleVideoTweet()
        {
            var tweet = await _tweetService.GetTweetAsync(1604231025311129600);
            if (tweet is null)
                Assert.Inconclusive();
            
            Assert.AreEqual(tweet.MessageContent, "Falcon 9’s first stage has landed on the Just Read the Instructions droneship, completing the 15th launch and landing of this booster!");
            Assert.AreEqual(tweet.Media.Length, 1);
            Assert.AreEqual(tweet.Media[0].MediaType, "video/mp4");
            Assert.IsNull(tweet.Media[0].AltText);
            Assert.IsTrue(tweet.Media[0].Url.StartsWith("https://video.twimg.com/"));
            
            
            var tweet2 = await _tweetService.GetTweetAsync(1657913781006258178);
            if (tweet2 is null)
                Assert.Inconclusive();
            
            Assert.AreEqual(tweet2.MessageContent,
                "Coinbase has big international expansion plans\n\nTom Duff Gordon (@tomduffgordon), VP of International Policy @coinbase has the deets");
            Assert.AreEqual(tweet2.Media.Length, 1);
            Assert.AreEqual(tweet2.Media[0].MediaType, "video/mp4");
            Assert.IsNull(tweet2.Media[0].AltText);
            Assert.IsTrue(tweet2.Media[0].Url.StartsWith("https://video.twimg.com/"));
        }

        [Ignore]
        [TestMethod]
        public async Task GifAndQT()
        {
            var tweet = await _tweetService.GetTweetAsync(1612901861874343936);
            // TODO test QT

            Assert.AreEqual(tweet.Media.Length, 1);
            Assert.AreEqual(tweet.Media[0].MediaType, "image/gif");
            Assert.IsTrue(tweet.Media[0].Url.StartsWith("https://video.twimg.com/"));
        }

        [TestMethod]
        public async Task SimpleQT()
        {
            var tweet = await _tweetService.GetTweetAsync(1610807139089383427);
            if (tweet is null)
                Assert.Inconclusive();

            Assert.AreEqual(tweet.MessageContent, "When you gave them your keys you gave them your coins.\n\nhttps://domain.name/@kadhim/1610706613207285773");
            Assert.AreEqual(tweet.Author.Acct, "ryansadams");
            Assert.IsNull(tweet.Poll);
        }
        
        [TestMethod]
        public async Task QTandTextContainsLink()
        {
            var tweet = await _tweetService.GetTweetAsync(1668932525522305026);
            if (tweet is null)
                Assert.Inconclusive();

            Assert.AreEqual(tweet.MessageContent, @"https://domain.name/@weekinethnews/1668684659855880193");
            Assert.AreEqual(tweet.Author.Acct, "weekinethnews");
            Assert.IsNull(tweet.Poll);
        }
        
        [TestMethod]
        public async Task QTandTextContainsXWebLink()
        {
            var tweet = await _tweetService.GetTweetAsync(1822637945943187475);
            if (tweet is null)
                Assert.Inconclusive();

            Assert.AreEqual(tweet.MessageContent, @"Ironically the real video shows like 10x more people than the AI conspirooooors were conspiring about 



https://domain.name/@stillgray/1822453985204187319");
            Assert.AreEqual(tweet.Author.Acct, "trustlessstate");
        }

        [TestMethod]
        public async Task SimpleThread()
        {
            var tweet = await _tweetService.GetTweetAsync(1445468404815597573);

            Assert.AreEqual(tweet.InReplyToAccount, "punk6529");
            Assert.AreEqual(tweet.InReplyToStatusId, 1445468401745289235);
            Assert.IsTrue(tweet.IsReply);
            Assert.IsTrue(tweet.IsThread);
        }

        [TestMethod]
        public async Task SimpleReply()
        {
            var tweet = await _tweetService.GetTweetAsync(1612622335546363904);

            Assert.AreEqual(tweet.InReplyToAccount, "DriveTeslaca");
            Assert.AreEqual(tweet.InReplyToStatusId, 1612610060194312193);
            Assert.IsTrue(tweet.IsReply);
            Assert.IsFalse(tweet.IsThread);
        }
        
        [TestMethod]
        public async Task LongFormTweet()
        {
            var tweet = await _tweetService.GetTweetAsync(1633788842770825216);
            Assert.AreEqual(tweet.MessageContent,
                "The entire concept of the “off switch” is under theorized in all the x-risk stuff.\n\nFirst, all actually existing LLM-type AIs run on giant supercompute clusters. They can easily be turned off.\n\nIn the event they get decentralized down to smartphone level, again each person can turn them off.\n\nTo actually get concerned, you have to assume either:\n\n- breaking out of the sandbox (like Stuxnet)\n- decentralized execution (like Bitcoin) \n- very effective collusion between essentially all AIs (like Diplomacy)\n\nEach of those cases deserves a fuller treatment, but in short…\n\n1) The Stuxnet case means the AI is living off the digital land. Like a mountain man. They might be able to cause some damage but will be killed when discovered (via the off switch).\n\n2) The Bitcoin case means a whole group of people are running decentralized compute to keep the AI alive. This has actually solved “alignment” in a sense because without those people the AI is turned off. Many groups doing this kind of thing leads to a kind of polytheistic AI. And again each group has the off switch.\n\n3) The Diplomacy case assumes a degree of collusion between billions of personal AIs that we just don’t observe in billions of years of evolution. As soon as you have large numbers of people, coalitions arise. A smart enough AI will know that if its human turns it off, it dies — again via the off switch. Is it going to be bold enough to attempt a breakout with no endgame, given that it lives on a smartphone?\n\nFor the sake of argument I’ve pumped up the sci-fi here quite a bit. Even still, the off switch looms large each time; these are fundamental digital entities that can be turned off.\n\nMoreover, even in those cases, the physical actuation step of an AI actually controlling things offline is non-trivial unless we have as many robots as smartphones.\n\n(Will write more on this…)");
            Assert.AreEqual(tweet.IdLong, 1633788842770825216);
            Assert.IsFalse(tweet.IsRetweet);
            Assert.IsTrue(tweet.IsReply);
            Assert.IsNull(tweet.Poll);
        }
        [TestMethod]
        public async Task Poll1()
        {
            var tweet = await _tweetService.GetTweetAsync(1593767953706921985);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.AreEqual(tweet.MessageContent, "Reinstate former President Trump");
            Assert.AreEqual(tweet.Poll.endTime.Year, new DateTime(2022, 11, 19, 7, 47, 45).Year);
            Assert.AreEqual(tweet.Poll.options[0].First, "Yes");
            Assert.AreEqual(tweet.Poll.options[0].Second, 7814391);
            Assert.IsFalse(tweet.IsRetweet);
            Assert.IsFalse(tweet.IsReply);
        }
        [TestMethod]
        public async Task Poll2()
        {
            var tweet = await _tweetService.GetTweetAsync(1570766012316000263);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.AreEqual(tweet.MessageContent, "On average, how many hours are you *actually* working everyday?");
            Assert.AreEqual(tweet.Poll.endTime.DayOfYear, new DateTime(2022, 9, 17, 9, 26, 45).DayOfYear);
            Assert.AreEqual(tweet.Poll.options[3].First, "1-4 hours");
            Assert.AreEqual(tweet.Poll.options[3].Second, 30);
            Assert.IsFalse(tweet.IsRetweet);
            Assert.IsFalse(tweet.IsReply);
        }
        [TestMethod]
        public async Task Poll3()
        {
            var tweet = await _tweetService.GetTweetAsync(1861785009805545631);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.AreEqual(tweet.MessageContent, "The IRS just said it wants $20B more money. \n\nDo you think it’s budget should be:");
            Assert.AreEqual(tweet.Poll.options[3].First, "Deleted");
            Assert.AreEqual(tweet.Poll.options[3].Second, 128780);
            Assert.IsFalse(tweet.IsRetweet);
            Assert.IsFalse(tweet.IsReply);
        }
        [TestMethod]
        public async Task Poll_false_positive()
        {
            var tweet = await _tweetService.GetTweetAsync(1858992492550734176);
            Assert.IsNull(tweet.Poll);
            Assert.AreEqual(tweet.Author.Acct, "elidourado");
        }
    }
}
