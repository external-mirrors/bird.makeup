using BirdsiteLive.ActivityPub.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace BirdsiteLive.ActivityPub.Tests
{
    [TestClass]
    public class ApDeserializerTests
    {
	    [TestMethod]
	    public void FollowDeserializationTest_Lemmy()
	    {
		    var json = "{\n  \"actor\": \"http://ds9.lemmy.ml/u/lemmy_alpha\",\n  \"to\": [\"http://enterprise.lemmy.ml/c/main\"],\n  \"object\": \"http://enterprise.lemmy.ml/c/main\",\n  \"type\": \"Follow\",\n  \"id\": \"http://ds9.lemmy.ml/activities/follow/6abcd50b-b8ca-4952-86b0-a6dd8cc12866\"\n}";

		    var data = ApDeserializer.ProcessActivity(json) as ActivityFollow;

		    Assert.AreEqual("http://ds9.lemmy.ml/activities/follow/6abcd50b-b8ca-4952-86b0-a6dd8cc12866", data.id);
		    Assert.AreEqual("Follow", data.type);
		    Assert.AreEqual("http://enterprise.lemmy.ml/c/main", data.apObject);
	    }
        [TestMethod]
        public void FollowDeserializationTest()
        {
            var json = "{ \"@context\":\"https://www.w3.org/ns/activitystreams\",\"id\":\"https://mastodon.technology/c94567cf-1fda-42ba-82fc-a0f82f63ccbe\",\"type\":\"Follow\",\"actor\":\"https://mastodon.technology/users/testtest\",\"object\":\"https://4a120ca2680e.ngrok.io/users/manu\"}";

            var data = ApDeserializer.ProcessActivity(json) as ActivityFollow;

            Assert.AreEqual("https://mastodon.technology/c94567cf-1fda-42ba-82fc-a0f82f63ccbe", data.id);
            Assert.AreEqual("Follow", data.type);
            Assert.AreEqual("https://4a120ca2680e.ngrok.io/users/manu", data.apObject);
        }

        [TestMethod]
        public void UndoDeserializationTest()
        {
            var json =
                "{\"@context\":\"https://www.w3.org/ns/activitystreams\",\"id\":\"https://mastodon.technology/users/testtest#follows/225982/undo\",\"type\":\"Undo\",\"actor\":\"https://mastodon.technology/users/testtest\",\"object\":{\"id\":\"https://mastodon.technology/c94567cf-1fda-42ba-82fc-a0f82f63ccbe\",\"type\":\"Follow\",\"actor\":\"https://mastodon.technology/users/testtest\",\"object\":\"https://4a120ca2680e.ngrok.io/users/manu\"}}";

            var data = ApDeserializer.ProcessActivity(json) as ActivityUndoFollow;
            Assert.AreEqual("https://mastodon.technology/users/testtest#follows/225982/undo", data.id);
            Assert.AreEqual("Undo", data.type);
            Assert.AreEqual("Follow", data.apObject.type);
            Assert.AreEqual("https://mastodon.technology/users/testtest", data.apObject.actor);
            Assert.AreEqual("https://4a120ca2680e.ngrok.io/users/manu", data.apObject.apObject);
            Assert.AreEqual(null, data.apObject.context);
        }

        [TestMethod]
        public void AnnounceDeserializationTest_Lemmy()
        {
	        var json = "{\n  \"actor\": \"http://enterprise.lemmy.ml/c/main\",\n  \"to\": [\"https://www.w3.org/ns/activitystreams#Public\"],\n  \"object\": {\n    \"actor\": \"http://enterprise.lemmy.ml/u/lemmy_beta\",\n    \"to\": [\"https://www.w3.org/ns/activitystreams#Public\"],\n    \"object\": {\n      \"type\": \"Page\",\n      \"id\": \"http://enterprise.lemmy.ml/post/7\",\n      \"attributedTo\": \"http://enterprise.lemmy.ml/u/lemmy_beta\",\n      \"to\": [\n        \"http://enterprise.lemmy.ml/c/main\",\n        \"https://www.w3.org/ns/activitystreams#Public\"\n      ],\n      \"name\": \"post 4\",\n      \"mediaType\": \"text/html\",\n      \"commentsEnabled\": true,\n      \"sensitive\": false,\n      \"stickied\": false,\n      \"published\": \"2021-11-01T12:11:22.871846Z\"\n    },\n    \"cc\": [\"http://enterprise.lemmy.ml/c/main\"],\n    \"type\": \"Create\",\n    \"id\": \"http://enterprise.lemmy.ml/activities/create/2807c9ec-3ad8-4859-a9e0-28b59b6e499f\"\n  },\n  \"cc\": [\"http://enterprise.lemmy.ml/c/main/followers\"],\n  \"type\": \"Announce\",\n  \"id\": \"http://enterprise.lemmy.ml/activities/announce/8030b171-803a-4108-94b1-342688f375cf\"\n}";


	        var data = ApDeserializer.ProcessActivity(json) as Activity;
	        Assert.AreEqual("http://enterprise.lemmy.ml/c/main", data.actor);
	        Assert.AreEqual("Announce", data.type);
        }
        [TestMethod]
        public void AcceptDeserializationTest()
        {
            var json = "{\"@context\":\"https://www.w3.org/ns/activitystreams\",\"id\":\"https://mamot.fr/users/testtest#accepts/follows/333879\",\"type\":\"Accept\",\"actor\":\"https://mamot.fr/users/testtest\",\"object\":{\"id\":\"https://85da1577f778.ngrok.io/f89dfd87-f5ce-4603-83d9-405c0e229989\",\"type\":\"Follow\",\"actor\":\"https://85da1577f778.ngrok.io/users/gra\",\"object\":\"https://mamot.fr/users/testtest\"}}";


            var data = ApDeserializer.ProcessActivity(json) as ActivityAcceptFollow;
            Assert.AreEqual("https://mamot.fr/users/testtest#accepts/follows/333879", data.id);
            Assert.AreEqual("Accept", data.type);
            Assert.AreEqual("https://mamot.fr/users/testtest", data.actor);
            Assert.AreEqual("https://85da1577f778.ngrok.io/f89dfd87-f5ce-4603-83d9-405c0e229989", data.apObject.id);
            Assert.AreEqual("https://85da1577f778.ngrok.io/users/gra", data.apObject.actor);
            Assert.AreEqual("Follow", data.apObject.type);
            Assert.AreEqual("https://mamot.fr/users/testtest", data.apObject.apObject);
        }
        [TestMethod]
        public void LikeDeserializationTest()
        {
	        var json = "{\n  \"@context\": \"https://www.w3.org/ns/activitystreams\",\n  \"type\": \"Like\",\n  \"id\": \"https://mymath.rocks/activitypub/helge/like-c62dab9f-34fb-4940-bb72-98e8872f96be\",\n  \"actor\": \"https://mymath.rocks/activitypub/helge\",\n  \"content\": \"\ud83d\udc2e\",\n  \"object\": \"https://i.calckey.cloud/notes/9ajhcxg0lu\",\n  \"to\": [\"https://i.calckey.cloud/users/99is5hpneh\"]\n}";


            var data = ApDeserializer.ProcessActivity(json) as ActivityLike;
            Assert.AreEqual("https://mymath.rocks/activitypub/helge/like-c62dab9f-34fb-4940-bb72-98e8872f96be", data.id);
            Assert.AreEqual("Like", data.type);
            Assert.AreEqual("https://mymath.rocks/activitypub/helge", data.actor);
            Assert.AreEqual("https://i.calckey.cloud/notes/9ajhcxg0lu", data.apObject);
        }
        [TestMethod]
        public void FlagDeserializationTest()
        {
	        var json = "{\n  \"@context\": \"https://www.w3.org/ns/activitystreams\",\n  \"id\": \"https://mastodon.example/ccb4f39a-506a-490e-9a8c-71831c7713a4\",\n  \"type\": \"Flag\",\n  \"actor\": \"https://mastodon.example/actor\",\n  \"content\": \"Please take a look at this user and their posts\",\n  \"object\": [\n    \"https://example.com/users/1\",\n    \"https://example.com/posts/380590\",\n    \"https://example.com/posts/380591\"\n  ],\n  \"to\": \"https://example.com/users/1\"\n}";


            var data = ApDeserializer.ProcessActivity(json) as ActivityFlag; 
            Assert.AreEqual("https://mastodon.example/ccb4f39a-506a-490e-9a8c-71831c7713a4", data.id);
            Assert.AreEqual("Flag", data.type);
            Assert.AreEqual("https://mastodon.example/actor", data.actor);
            Assert.AreEqual("https://example.com/users/1", data.apObject[0]);
        }

        [TestMethod]
        public void DeleteDeserializationTest()
        {
            var json =
                "{\"@context\": \"https://www.w3.org/ns/activitystreams\", \"id\": \"https://mastodon.technology/users/deleteduser#delete\", \"type\": \"Delete\", \"actor\": \"https://mastodon.technology/users/deleteduser\", \"to\": [\"https://www.w3.org/ns/activitystreams#Public\"],\"object\": \"https://mastodon.technology/users/deleteduser\",\"signature\": {\"type\": \"RsaSignature2017\",\"creator\": \"https://mastodon.technology/users/deleteduser#main-key\",\"created\": \"2020-11-19T22:43:01Z\",\"signatureValue\": \"peksQao4v5N+sMZgHXZ6xZnGaZrd0s+LqZimu63cnp7O5NBJM6gY9AAu/vKUgrh4C50r66f9OQdHg5yChQhc4ViE+yLR/3/e59YQimelmXJPpcC99Nt0YLU/iTRLsBehY3cDdC6+ogJKgpkToQvB6tG2KrPdrkreYh4Il4eXLKMfiQhgdKluOvenLnl2erPWfE02hIu/jpuljyxSuvJunMdU4yQVSZHTtk/I8q3jjzIzhgyb7ICWU5Hkx0H/47Q24ztsvOgiTWNgO+v6l9vA7qIhztENiRPhzGP5RCCzUKRAe6bcSu1Wfa3NKWqB9BeJ7s+2y2bD7ubPbiEE1MQV7Q==\"}}";

            var data = ApDeserializer.ProcessActivity(json) as ActivityDelete;

            Assert.AreEqual("https://mastodon.technology/users/deleteduser#delete", data.id);
            Assert.AreEqual("Delete", data.type);
            Assert.AreEqual("https://mastodon.technology/users/deleteduser", data.actor);
            Assert.AreEqual("https://mastodon.technology/users/deleteduser", data.apObject);
        }
        // {"object":{"object":"https://bird.makeup/users/spectatorindex","id":"https://masto.ai/b89eb86e-c902-48bc-956f-94f081617f18","type":"Follow","actor":"https://masto.ai/users/singha"},"@context":"https://www.w3.org/ns/activitystreams","id":"https://bird.makeup/users/spectatorindex#accepts/follows/27363118-e61e-4710-a41c-75dd5d54912f","type":"Accept","actor":"https://bird.makeup/users/spectatorindex"}
        // {"object":{"object":"https://bird.makeup/users/moltke","id":"https://universeodon.com/81cddd78-d7d6-4665-aa21-7bcfbea82b6b","type":"Follow","actor":"https://universeodon.com/users/amhrasmussen"},"@context":"https://www.w3.org/ns/activitystreams","id":"https://bird.makeup/users/moltke#accepts/follows/d28146be-e884-4e91-8385-19fa004f35b3","type":"Accept","actor":"https://bird.makeup/users/moltke"}


    }
}