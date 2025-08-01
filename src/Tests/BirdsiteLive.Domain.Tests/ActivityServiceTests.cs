using System.Net.Http;
using System.Threading.Tasks;
using BirdsiteLive.ActivityPub;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Domain.Factories;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Text.Json;
using BirdsiteLive.Cryptography;


namespace BirdsiteLive.Domain.Tests
{
    [TestClass]
    public class ActivityServiceTests
    {
        private readonly InstanceSettings _settings;

        #region Ctor
        public ActivityServiceTests()
        {
            _settings = new InstanceSettings
            {
                Domain = "domain.name"
            };
        }
        #endregion

        [TestMethod]
        public async Task ActivityTest()
        {
            var logger1 = new Mock<ILogger<ActivityPubService>>();
            var httpFactory = new Mock<IHttpClientFactory>();
            var keyFactory = new Mock<IMagicKeyFactory>();
            keyFactory.Setup(_ => _.GetMagicKey()).ReturnsAsync(new MagicKey(
                """{"D":"Oar5IoCLbM2K82c2M3ljJJUAf7KFr6beFtlhFOj+4q1WnIReXylvoe3XBkotQ13jb1RQ5dNKQhI3oMUpHbLbG8mScHv48QtT6OR1HaDVDYwEdiSGN0JsmtBRigbMJyn2AX0OlwkxJe3xi6oo3eCV5CSjU/hiW9JXA9dKD3NP1VgLIGHRNgNcIxkOfVWDN01ECYu4t2OXnZCuyqunZ0lharUJ6e8lralqkZFoP6bMevsrTc3+hYXcjfYZkevet1yUxJqNfw7RWPKNbabheTtsAuS0jhXig8XoJKY8AIffVjchGgIUI4vq4nJMN2rUxz68CF/nFOuD8feER1byABSN4Q==", "P": "7pf+7JzVFRHCSFsaXsm+CnBR/pfwTv8GVp9kpmL7Baru9h7MHnxYJ0N/RTMpMyZvDVx0Sjobp+gLrKLiYD22QfxFAlTVjoDohKUsQugydA0wrDNJ5BBmWWxkapTInGutZYwWTkefWRC/hjyZlAvUhgW9ctSas8+/LEeuyA6ql6M=","Q":"2IaktklUUgI3gbk+7jXNOClm6rc6cjoetk4sS85EjoUJZs59uOAdpfmm0uIqNP0gKy4opxnsQFxybaEHwzuYWH+ZySNS9uRRjYKfDAU6OYCYEOFKlh6jjHUCdcd8VDFNSvA0MT5DZ8tpX2MjjahuGdlfXQZm1UKYB+h1g25caSc=","DP":"JEg83eJjjNasgrBH7E4ldhTqgxq70md5oUaP2bWHkq8Rs5+vTpt+FEpxWiaTh1G65X8/t+HqPrhMvi3u2s/HnXUtUVNxPkBgG3u6pVoGAhvXYPhTrjjIN6UCCCsj7pV5Qs3wvmqp0rN3TIR+nkLGSLMqwgGOnPVkjuk/rPB+BJ0=", "DQ": "wH2CdKNgGL/rxKGAtpiR5nm4CrX1eZL9tqhsbL/k5qaSoxizX+Wttd3pVtTFHPJi5MBWV6eOBfGpsJhVpFSYrSRS/SMwIFj9v0X+Stti1bfieC8w9aArWTS0iSxc9SQXSKWeYKCvn9iPxsMF2mt/5e7+/l4wkSpwqacYwU0dTkU=", "Modulus": "yc28Klfkn//jJKnZpzK4yDsfAv5u6mjzLJwMcW30IWk3k+N/cmH3MDDlC7en04kdYOKWLHSS0+G7XaxehZOj55GG+N7GjEBeUlls1jLHAP+zCyrtPh9UDmSOhYbrTdXAExHTcGn3rVCyYURopzk+gZ7GtNCEYIPrvpUhqgLVXE0FPTeBiyGq92VOmIEdDqQ9HdHgDnzd49oXsMxaqqSh6aJBIv8JgvDUR0OJD7xVrqd5ZvsoDcaKmkNfYL/TsFuQVH5DWC0emTlIgfYp246mUDoh1Z/3vvSglZjATXMx+zjnLbVQB8zcZSSdJSOLlpBPs8CeRjWZqMBFH1hdvML01Q==", "Exponent": "AQAB", "InverseQ": "G/edCYzS5R54fU2GDys37nkoq2rlHUG+uas3fJMKRr+2OMU6sBy26p76enXWEP2gtlqmugFvm4QeKYBUgxwNCPdfof+vNb1yh8wLiqWyG636+MYJK9NkUkAIpUjyVvI4rFWQX4+1cu7pqEqetfP0LafS+4Z+FPhBJK6Iz3YvJng="}"""));
            var cryptoService = new CryptoService(keyFactory.Object);
            httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
            var service = new ActivityPubService(cryptoService, _settings, httpFactory.Object, logger1.Object);

            var activity = new ActivityAcceptFollow()
            {
                id = "awef",
                context = "https://www.w3.org/ns/activitystreams",
                type = "Accept",
                actor = "https://mastodon.technology/users/testtest",
                apObject = new ActivityFollow()
                {
                    context = "https://www.w3.org/ns/activitystreams",
                    id = "abc",
                    type = "Follow",
                    actor = "https://mastodon.technology/users/testtest2",
                    apObject = "https://mastodon.technology/users/testtest3",
                }
                
            };
            var json =
                """{"object":{"object":"https://mastodon.technology/users/testtest3","@context":"https://www.w3.org/ns/activitystreams","id":"abc","type":"Follow","actor":"https://mastodon.technology/users/testtest2"},"@context":"https://www.w3.org/ns/activitystreams","id":"awef","type":"Accept","actor":"https://mastodon.technology/users/testtest"}""";
            #region Validations

            var req = await service.BuildRequest(activity, "google.com", "tata", HttpMethod.Post, "awef");
            
            Assert.AreEqual(await req.Content.ReadAsStringAsync(), json);

            #endregion
        }
        [TestMethod]
        public async Task AcceptFollow()
        {
 

            var logger1 = new Mock<ILogger<ActivityPubService>>();
            var httpFactory = new Mock<IHttpClientFactory>();
            var keyFactory = new Mock<IMagicKeyFactory>();
            var cryptoService = new CryptoService(keyFactory.Object);
            httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
            var service = new ActivityPubService(cryptoService, _settings, httpFactory.Object, logger1.Object);

            var json = "{ \"@context\":\"https://www.w3.org/ns/activitystreams\",\"id\":\"https://mastodon.technology/c94567cf-1fda-42ba-82fc-a0f82f63ccbe\",\"type\":\"Follow\",\"actor\":\"https://mastodon.technology/users/testtest\",\"object\":\"https://4a120ca2680e.ngrok.io/users/manu\"}";
            var activity = ApDeserializer.ProcessActivity(json) as ActivityFollow;

            var jsonres =
                "{\"object\":{\"id\":\"https://mastodon.technology/c94567cf-1fda-42ba-82fc-a0f82f63ccbe\",\"type\":\"Follow\",\"actor\":\"https://mastodon.technology/users/testtest\",\"object\":\"https://4a120ca2680e.ngrok.io/users/manu\"},\"@context\":\"https://www.w3.org/ns/activitystreams\",\"id\":\"https://4a120ca2680e.ngrok.io/users/manu#accepts/follows/32e5fbda-9159-4ede-8249-9d008092d26f\",\"type\":\"Accept\",\"actor\":\"https://4a120ca2680e.ngrok.io/users/manu\"}";
            var activityRes = ApDeserializer.ProcessActivity(jsonres) as ActivityAcceptFollow;
            #region Validations

            var req = service.BuildAcceptFollow(activity);
            
            string s = JsonSerializer.Serialize(req);
            
            Assert.AreEqual(req.actor, activityRes.actor);
            Assert.IsNull(req.apObject.to);

            #endregion
        }
        [TestMethod]
        public async Task AcceptFollow_Lemmy()
        {
 

            var logger1 = new Mock<ILogger<ActivityPubService>>();
            var httpFactory = new Mock<IHttpClientFactory>();
            var keyFactory = new Mock<IMagicKeyFactory>();
            var cryptoService = new CryptoService(keyFactory.Object);
            httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
            var service = new ActivityPubService(cryptoService, _settings, httpFactory.Object, logger1.Object);

            var json = """
                       {
                         "actor": "http://ds9.lemmy.ml/u/lemmy_alpha",
                         "to": ["http://enterprise.lemmy.ml/c/main"],
                         "object": "http://enterprise.lemmy.ml/c/main",
                         "type": "Follow",
                         "id": "http://ds9.lemmy.ml/activities/follow/6abcd50b-b8ca-4952-86b0-a6dd8cc12866"
                       }
                       """;
            var activity = ApDeserializer.ProcessActivity(json) as ActivityFollow;

            var jsonres =
                """
                {
                  "object" : {
                    "object" : "http://enterprise.lemmy.ml/c/main",
                    "id" : "http://ds9.lemmy.ml/activities/follow/6abcd50b-b8ca-4952-86b0-a6dd8cc12866",
                    "type" : "Follow",
                    "actor" : "http://ds9.lemmy.ml/u/lemmy_alpha",
                    "to" : [ "http://enterprise.lemmy.ml/c/main" ]
                  },
                  "@context" : "https://www.w3.org/ns/activitystreams",
                  "id" : "http://enterprise.lemmy.ml/c/main#accepts/follows/3cdeacb3-f026-40ff-a3a6-2c520d242f57",
                  "type" : "Accept",
                  "actor" : "http://enterprise.lemmy.ml/c/main",
                  "to" : [ "http://ds9.lemmy.ml/u/lemmy_alpha" ]
                }
                """;
            var activityRes = ApDeserializer.ProcessActivity(jsonres) as ActivityAcceptFollow;
            #region Validations

            var req = service.BuildAcceptFollow(activity);
            
            string s = JsonSerializer.Serialize(req);
            
            Assert.AreEqual(req.actor, activityRes.actor);
            Assert.AreEqual(req.apObject.to[0], "http://enterprise.lemmy.ml/c/main");

            #endregion
        }


    }
}
