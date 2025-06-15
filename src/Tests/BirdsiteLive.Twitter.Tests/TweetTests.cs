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
            httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(() => new HttpClient());
            httpFactory.Setup(_ => _.CreateClient("WithProxy")).Returns(() => new HttpClient());
            var settings = new InstanceSettings
            {
                Domain = "domain.name"
            };
            _tweetAuth =
                new TwitterAuthenticationInitializer(httpFactory.Object, settings, settingsDal.Object, logger1.Object);
            ITwitterUserService user = new TwitterUserService(_tweetAuth, twitterDal.Object, settings,
                settingsDal.Object, httpFactory.Object, logger2.Object);
            ICachedTwitterUserService user2 = new CachedTwitterUserService(user, twitterDal.Object, settings);
            _tweetService = new TwitterTweetsService(_tweetAuth, user2, twitterDal.Object, settings, httpFactory.Object,
                settingsDal.Object, logger3.Object);

        }


        [TestMethod]
        public async Task SimpleTextTweet()
        {
            var tweet = await _tweetService.GetTweetAsync(1600905296892891149);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.AreEqual(tweet.MessageContent,
                "We’re strengthening American manufacturing by creating 750,000 manufacturing jobs since I became president.");
            Assert.AreEqual(tweet.IdLong, 1600905296892891149);
            Assert.AreEqual(tweet.CreatedAt, new DateTime(2022, 12, 8, 17, 29, 0));
            Assert.IsFalse(tweet.IsRetweet);
            Assert.IsFalse(tweet.IsReply);
        }

        [TestMethod]
        public async Task LeadingDotTextAndSinglePictureTweet_2()
        {
            var tweet = await _tweetService.GetTweetAsync(1908137050907558326);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.AreEqual(tweet.MessageContent,
                "@ETHGlobal Taipei is kicking off! \ud83d\udd25\n\nPowered by\ud83e\uddcb\nBuilt on Ethereum");

            Assert.AreEqual(tweet.Media[0].MediaType, "image/jpeg");
            Assert.AreEqual(tweet.Media.Length, 1);
            Assert.IsNull(tweet.Media[0].AltText);
        }
        [TestMethod]
        public async Task LeadingDotTextAndSinglePictureTweet()
        {
            var tweet = await _tweetService.GetTweetAsync(1905980906189254989);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.AreEqual(tweet.MessageContent,
                "@SBelangerCAQ voici ce qui se passe avec les aînés de ma région. \n\nC’est tout simplement scandaleux.");

            Assert.AreEqual(tweet.Media[0].MediaType, "image/jpeg");
            Assert.AreEqual(tweet.Media.Length, 1);
            Assert.IsNull(tweet.Media[0].AltText);
        }
        [TestMethod]
        public async Task SimpleTextAndSinglePictureTweet()
        {
            var tweet = await _tweetService.GetTweetAsync(1593344577385160704);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.AreEqual(tweet.Media[0].MediaType, "image/jpeg");
            Assert.AreEqual(tweet.Media.Length, 1);
            
            Assert.AreEqual(tweet.MessageContent,
                "Speaker Nancy Pelosi will go down as one of most accomplished legislators in American history—breaking barriers, opening doors for others, and working every day to serve the American people. I couldn’t be more grateful for her friendship and leadership.");

            Assert.AreEqual(tweet.Media[0].AltText, "President Obama with Speaker Nancy Pelosi in DC.");
        }

        [TestMethod]
        public async Task SimpleTextAndSingleLinkTweet()
        {
            var tweet = await _tweetService.GetTweetAsync(1602618920996945922);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.AreEqual(tweet.MessageContent,
                "#Linux 6.2 Expands Support For More #Qualcomm #Snapdragon SoCs, #Apple M1 Pro/Ultra/Max\n\nhttps://www.phoronix.com/news/Linux-6.2-Arm-SoC-Updates");
        }

        [TestMethod]
        public async Task SimpleTextAndSingleVideoTweet()
        {
            var tweet = await _tweetService.GetTweetAsync(1604231025311129600);
            if (tweet is null)
                Assert.Inconclusive();

            Assert.AreEqual(tweet.MessageContent,
                "Falcon 9’s first stage has landed on the Just Read the Instructions droneship, completing the 15th launch and landing of this booster!");
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

            Assert.AreEqual(tweet.MessageContent,
                "When you gave them your keys you gave them your coins.\n\nhttps://domain.name/@kadhim/1610706613207285773");
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

        [Ignore]
        [TestMethod]
        public async Task QTandTextContainsXWebLink_2()
        {
            var tweet = await _tweetService.GetTweetAsync(1906784491349377365);
            if (tweet is null)
                Assert.Inconclusive();

            Assert.AreEqual(tweet.MessageContent,
                @"If you want a detailed walkthrough of how Privacy Pools works, we got you covered 🤝

Still feeling unsure? Feel free to post your questions, and we’ll help you out! ✅

Note: pay attention when creating an account!



https://domain.name/@0xbowio/1906779676053209370");
            Assert.AreEqual(tweet.Author.Acct, "0xbowio");
        }
        [Ignore]
        [TestMethod]
        public async Task QTandTextContainsXWebLink()
        {
            var tweet = await _tweetService.GetTweetAsync(1822637945943187475);
            if (tweet is null)
                Assert.Inconclusive();

            Assert.AreEqual(tweet.MessageContent,
                @"Ironically the real video shows like 10x more people than the AI conspirooooors were conspiring about 



https://domain.name/@stillgray/1822453985204187319");
            Assert.AreEqual(tweet.Author.Acct, "trustlessstate");
        }

        [TestMethod]
        public async Task SimpleThread()
        {
            var tweet = await _tweetService.GetTweetAsync(1445468404815597573);
            if (tweet is null)
                Assert.Inconclusive();

            Assert.AreEqual(tweet.InReplyToAccount, "punk6529");
            Assert.AreEqual(tweet.InReplyToStatusId, 1445468401745289235);
            Assert.IsTrue(tweet.IsReply);
            Assert.IsTrue(tweet.IsThread);
        }

        [TestMethod]
        public async Task SimpleReply()
        {
            var tweet = await _tweetService.GetTweetAsync(1612622335546363904);
            if (tweet is null)
                Assert.Inconclusive();

            Assert.AreEqual(tweet.InReplyToAccount, "DriveTeslaca");
            Assert.AreEqual(tweet.InReplyToStatusId, 1612610060194312193);
            Assert.IsTrue(tweet.IsReply);
            Assert.IsFalse(tweet.IsThread);
        }

        [TestMethod]
        public async Task LongFormTweet()
        {
            var tweet = await _tweetService.GetTweetAsync(1633788842770825216);
            if (tweet is null)
                Assert.Inconclusive();
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
            Assert.AreEqual(tweet.MessageContent,
                "The IRS just said it wants $20B more money. \n\nDo you think it’s budget should be:");
            Assert.AreEqual(tweet.Poll.options[3].First, "Deleted");
            Assert.AreEqual(tweet.Poll.options[3].Second, 128780);
            Assert.IsFalse(tweet.IsRetweet);
            Assert.IsFalse(tweet.IsReply);
        }

        [TestMethod]
        public async Task Poll4()
        {
            var tweet = await _tweetService.GetTweetAsync(1872489920297910742);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.AreEqual(tweet.MessageContent, "Do you feel the immigration debates on X have been:");
            Assert.AreEqual(tweet.Poll.options[1].First, "Toxic");
            Assert.AreEqual(tweet.Poll.options[1].Second, 6323);
            Assert.IsFalse(tweet.IsRetweet);
            Assert.IsFalse(tweet.IsReply);
        }

        [TestMethod]
        public async Task Poll_false_positive()
        {
            var tweet = await _tweetService.GetTweetAsync(1858992492550734176);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.IsNull(tweet.Poll);
            Assert.AreEqual(tweet.Author.Acct, "elidourado");
        }

        [TestMethod]
        public async Task ShortLink_Expension_1()
        {
            var tweet = await _tweetService.GetTweetAsync(1884240424870568326);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.IsNull(tweet.Poll);
            Assert.AreEqual(tweet.Author.Acct, "spotifyartists");

            Assert.AreEqual(tweet.MessageContent,
                "For another year, Spotify set the record for the highest annual payment to the music industry from any single retailer: over $10 billion. That figure has grown tenfold over the past decade, bringing Spotify's total payouts since founding to $60B. \n\nFor more on how we got there, and what it means, hit the link below.\n\ud83d\udd17 https://newsroom.spotify.com/2025-01-28/on-our-10-billion-milestone-and-a-decade-of-getting-the-world-to-value-music/?utm_campaign=organic-social_all-social_trf_all-en_econ_crossdevice_none_general&utm_medium=organic-social&utm_source=all-socialorganic-social_all-social_trf_all-en_econ_crossdevice_none_general");
        }

        [Ignore]
        [TestMethod]
        public async Task ShortLink_Expension_2()
        {
            var tweet = await _tweetService.GetTweetAsync(1883636068647342141);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.IsNull(tweet.Poll);
            Assert.AreEqual(tweet.Author.Acct, "doge");

            Assert.AreEqual(tweet.MessageContent,
                "The DOGE Team is looking for world-class talent to work long hours identifying/eliminating waste, fraud, and abuse. These are full-time, salaried positions for software engineers, InfoSec engineers, financial analysts, HR professionals, and, in general, all competent/caring people.  Apply here!\n\nhttps://doge.gov/join/");
        }
        [TestMethod]
        public async Task ShortLink_Expension_3()
        {
            var tweet = await _tweetService.GetTweetAsync(1887282879925002660);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.IsNull(tweet.Poll);
            Assert.AreEqual(tweet.Author.Acct, "base");

            Assert.IsTrue(tweet.MessageContent.StartsWith(
                "Based community meetups are happening all over the world:\n\nDubai 2/11\nhttps://lu.ma/8tbivk8o\n\nSeoul 2/13\nhttps://lu.ma/ch9wy5gd"));
            //Assert.AreEqual(tweet.MessageContent,
            //    "Based community meetups are happening all over the world:\n\nDubai 2/11\nhttps://lu.ma/8tbivk8o\n\nSeoul 2/13\nhttps://lu.ma/ch9wy5gd\n\nAddis Ababa 2/14\nhttps://lu.ma/v2tnqtk8\n\nSydney 2/15\nhttps://lu.ma/s127mjn5\n\nHong Kong 2/18\nhttps://lu.ma/based-brunch\n\nZurich 2/20\nhttps://lu.ma/rvsd4s97\n\nArusha 2/20\nhttps://lu.ma/fkrh9jeh\n\nHong Kong 2/20\nhttps://lu.ma/wdvepo9r\n\nTaipei City 2/22\nhttps://lu.ma/ypuh65ad\n\nKabale 2/22\nhttps://lu.ma/i0ekoliq\n\nMalawi 2/26\nhttps://lu.ma/ouzen3rx\n\nDenver | @EthereumDenver  3/1\nhttps://lu.ma/l3cadx8j\n\nKampala 3/22\nhttps://lu.ma/g9yyct7s");
        }
        [TestMethod]
        public async Task ShortLink_Expension_4()
        {
            var tweet = await _tweetService.GetTweetAsync(1887592728621420875);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.IsNull(tweet.Poll);
            Assert.AreEqual(tweet.Author.Acct, "askvenice");

            Assert.AreEqual(tweet.MessageContent,
                "The last three days at Venice...\n\nFebruary 3rd, 2025\n**App UI**\n* Inference - Update error handling on document upload to gracefully handle display of invalid PDF errors.\n\n* Characters - Update the share URL within the character settings screen to use the character's public slug vs. UUID.\n\n* With Enter Submits Chat disabled, permit sending the chat with control-enter. Solves request from user in [Featurebase](https://veniceai.featurebase.app/p/use-ctrlenter-to-submit-prompt)\n\n* Remove Retiring Soon tag from Dolphin. Our intent was to retire this model and replace it with an upcoming Dolphin release but until we have a final ETA from Dolphin, the model will remain.\n\n* Add a spinner to the Thinking... block in Dolphin to make the UI more clear that the LLM is generating content behind the scenes.\n\n* Fixed a bug that made the Copy option on code blocks not possible to click until the entire message completed rendering.\n\n\u2800**Token Dashboard**\n* Add a key to the Network Utilization Graph\n\n* Fix rendering of VCU cards on mobile screens\n\n* Force wallets to connect to the Base network when executing transactions on-chain.\n\n* Add \"Claim and Restake\" button to facilitate claiming and immediately restaking rewards in a single transaction.\n\n* Create [Dune Analytics dashboard](https://dune.com/queries/4661260/7760387) showing network utilization over time as recorded on-chain.\n\n\u2800**API**\n* Fixed issue where the use of max_completion_tokens in combination with the llama-3.1-405b model would result in a 500 response.\n\n* Support light and dark mode, toggle-able in the top right corner.");
        }
        [TestMethod]
        public async Task ShortLink_Expension_5()
        {
            var tweet = await _tweetService.GetTweetAsync(1908169318828810274);
            if (tweet is null)
                Assert.Inconclusive();
            Assert.IsNull(tweet.Poll);
            Assert.AreEqual(tweet.Author.Acct, "val_plante");

            Assert.AreEqual(tweet.MessageContent,
                "Les horreurs de la guerre au Proche-Orient et la violence démesurée envers la population civile dans la bande de Gaza sont choquantes et éprouvantes, mais nous devons choisir le dialogue à Montréal. Les actes d’intimidation et de grabuge qui provoquent un sentiment d’insécurité dans une institution universitaire ne peuvent pas être tolérés. https://t.co/LRkcv7fsWM");
        }
    }

}