using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BirdsiteLive.ActivityPub.Tests
{
    [TestClass]
    public class ActorTests
    {
        [TestMethod]
        public void ActorDeserializationTest_Lemmy_User()
        {
            var json = """
				{
				  "id": "https://enterprise.lemmy.ml/u/picard",
				  "type": "Person",
				  "preferredUsername": "picard",
				  "name": "Jean-Luc Picard",
				  "summary": "<p>Captain of the starship <strong>Enterprise</strong>.</p>\n",
				  "source": {
				    "content": "Captain of the starship **Enterprise**.",
				    "mediaType": "text/markdown"
				  },
				  "icon": {
				    "type": "Image",
				    "url": "https://enterprise.lemmy.ml/pictrs/image/ed9ej7.jpg"
				  },
				  "image": {
				    "type": "Image",
				    "url": "https://enterprise.lemmy.ml/pictrs/image/XenaYI5hTn.png"
				  },
				  "matrixUserId": "@picard:matrix.org",
				  "inbox": "https://enterprise.lemmy.ml/u/picard/inbox",
				  "outbox": "https://enterprise.lemmy.ml/u/picard/outbox",
				  "endpoints": {
				    "sharedInbox": "https://enterprise.lemmy.ml/inbox"
				  },
				  "published": "2020-01-17T01:38:22.348392Z",
				  "updated": "2021-08-13T00:11:15.941990Z",
				  "publicKey": {
				    "id": "https://enterprise.lemmy.ml/u/picard#main-key",
				    "owner": "https://enterprise.lemmy.ml/u/picard",
				    "publicKeyPem": "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0lP99/s5Vv+XbPdkeqIJ\nwoD4GFnHmBnBHdEKChEUWfWj1TtioC/rGNoXFQeXQA3Amhy4nxSceiDnUgwkkuQY\nv0MtIW58NzgknEavtllxL+LSds5pg3gANaDIk8UiWTkqXTg0GnlJMpCK1Chen0l/\nszL6DEvUyTSuS5ZYDXFgewF89Pe7U0S15V5U2Harv7AgJYDyxmUL0D1pGuUCRqcE\nl5MTHJjrXeNnH1w2g8aly8YlO/Cr0L51rFg/lBF23vni7ZLv8HbmWh6YpaAf1R8h\nE45zKR7OHqymdjzrg1ITBwovefpwMkVgnJ+Wdr4HPnFlBSkXPoZeM11+Z8L0anzA\nXwIDAQAB\n-----END PUBLIC KEY-----\n"
				  }
				}
				""";

            var actor = JsonSerializer.Deserialize<Actor>(json);
            Assert.IsNotNull(actor);
            Assert.AreEqual(actor.id, "https://enterprise.lemmy.ml/u/picard");;
        }
        [TestMethod]
        public void ActorDeserializationTest_Lemmy_Community()
        {
            var json = """
				{
				  "id": "https://enterprise.lemmy.ml/c/tenforward",
				  "type": "Group",
				  "preferredUsername": "tenforward",
				  "name": "Ten Forward",
				  "summary": "<p>Lounge and recreation facility</p>\n<hr />\n<p>Welcome to the <a href=\"https://memory-alpha.fandom.com/wiki/USS_Enterprise_(NCC-1701-D)\">Enterprise</a>!.</p>\n",
				  "source": {
				    "content": "Lounge and recreation facility\n\n---\n\nWelcome to the [Enterprise](https://memory-alpha.fandom.com/wiki/USS_Enterprise_(NCC-1701-D))!.",
				    "mediaType": "text/markdown"
				  },
				  "sensitive": false,
				  "icon": {
				    "type": "Image",
				    "url": "https://enterprise.lemmy.ml/pictrs/image/waqyZwLAy4.webp"
				  },
				  "image": {
				    "type": "Image",
				    "url": "https://enterprise.lemmy.ml/pictrs/image/Wt8zoMcCmE.jpg"
				  },
				  "inbox": "https://enterprise.lemmy.ml/c/tenforward/inbox",
				  "followers": "https://enterprise.lemmy.ml/c/tenforward/followers",
				  "attributedTo": "https://enterprise.lemmy.ml/c/tenforward/moderators",
				  "featured": "https://enterprise.lemmy.ml/c/tenforward//featured",
				  "postingRestrictedToMods": false,
				  "endpoints": {
				    "sharedInbox": "https://enterprise.lemmy.ml/inbox"
				  },
				  "outbox": "https://enterprise.lemmy.ml/c/tenforward/outbox",
				  "publicKey": {
				    "id": "https://enterprise.lemmy.ml/c/tenforward#main-key",
				    "owner": "https://enterprise.lemmy.ml/c/tenforward",
				    "publicKeyPem": "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAzRjKTNtvDCmugplwEh+g\nx1bhKm6BHUZfXfpscgMMm7tXFswSDzUQirMgfkxa9ubfr1PDFKffA2vQ9x6CyuO/\n70xTafdOHyV1tSqzgKz0ZvFZ/VCOo6qy1mYWVkrtBm/fKzM+87MdkKYB/zI4VyEJ\nLfLQgjwxBAEYUH3CBG71U0gO0TwbimWNN0vqlfp0QfThNe1WYObF88ZVzMLgFbr7\nRHBItZjlZ/d8foPDidlIR3l2dJjy0EsD8F9JM340jtX7LXqFmU4j1AQKNHTDLnUF\nwYVhzuQGNJ504l5LZkFG54XfIFT7dx2QwuuM9bSnfPv/98RYrq1Si6tCkxEt1cVe\n4wIDAQAB\n-----END PUBLIC KEY-----\n"
				  },
				  "language": [
				    {
				      "identifier": "fr",
				      "name": "Français"
				    },
				    {
				      "identifier": "de",
				      "name": "Deutsch"
				    }
				  ],
				  "published": "2019-06-02T16:43:50.799554Z",
				  "updated": "2021-03-10T17:18:10.498868Z"
				}
				""";

            var actor = JsonSerializer.Deserialize<Actor>(json);
            Assert.IsNotNull(actor);
            Assert.AreEqual(actor.id, "https://enterprise.lemmy.ml/c/tenforward");;
        }
        [TestMethod]
        public void ActorDeserializationTest()
        {
            var json = """
					{
						"@context": [
							"https://www.w3.org/ns/activitystreams",
							"https://w3id.org/security/v1",
							{
								"manuallyApprovesFollowers": "as:manuallyApprovesFollowers",
								"toot": "http://joinmastodon.org/ns#",
								"featured": {
									"@id": "toot:featured",
									"@type": "@id"
								},
								"featuredTags": {
									"@id": "toot:featuredTags",
									"@type": "@id"
								},
								"alsoKnownAs": {
									"@id": "as:alsoKnownAs",
									"@type": "@id"
								},
								"movedTo": {
									"@id": "as:movedTo",
									"@type": "@id"
								},
								"schema": "http://schema.org#",
								"PropertyValue": "schema:PropertyValue",
								"value": "schema:value",
								"discoverable": "toot:discoverable",
								"Device": "toot:Device",
								"Ed25519Signature": "toot:Ed25519Signature",
								"Ed25519Key": "toot:Ed25519Key",
								"Curve25519Key": "toot:Curve25519Key",
								"EncryptedMessage": "toot:EncryptedMessage",
								"publicKeyBase64": "toot:publicKeyBase64",
								"deviceId": "toot:deviceId",
								"claim": {
									"@type": "@id",
									"@id": "toot:claim"
								},
								"fingerprintKey": {
									"@type": "@id",
									"@id": "toot:fingerprintKey"
								},
								"identityKey": {
									"@type": "@id",
									"@id": "toot:identityKey"
								},
								"devices": {
									"@type": "@id",
									"@id": "toot:devices"
								},
								"messageFranking": "toot:messageFranking",
								"messageType": "toot:messageType",
								"cipherText": "toot:cipherText",
								"suspended": "toot:suspended"
							}
						],
						"id": "https://mastodon.online/users/devvincent",
						"type": "Person",
						"following": "https://mastodon.online/users/devvincent/following",
						"followers": "https://mastodon.online/users/devvincent/followers",
						"inbox": "https://mastodon.online/users/devvincent/inbox",
						"outbox": "https://mastodon.online/users/devvincent/outbox",
						"featured": "https://mastodon.online/users/devvincent/collections/featured",
						"featuredTags": "https://mastodon.online/users/devvincent/collections/tags",
						"preferredUsername": "devvincent",
						"name": "",
						"summary": "",
						"url": "https://mastodon.online/@devvincent",
						"manuallyApprovesFollowers": false,
						"discoverable": false,
						"published": "2022-05-08T00:00:00Z",
						"devices": "https://mastodon.online/users/devvincent/collections/devices",
						"publicKey": {
							"id": "https://mastodon.online/users/devvincent#main-key",
							"owner": "https://mastodon.online/users/devvincent",
							"publicKeyPem": "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA7U07uS4zu5jeZSBVZ072\naXcTeVQc0baM8BBUvJkpX+mV2vh+V4yfqN44KzFxlkk8XcAoidt8HBAvpQ/5yCwZ\neGS2ySCxC+sqvErIbaYadWVHGJhZjLYPVa0n8wvkqRQ0aUJ8K17/wY+/YYfukgeC\nTGHGoyzDDZZxrR1Z8LTvImSEkYooTvvzaaFaTUnFwCKepxftKLdJAfp4sP4l1Zom\nUZGwaYimuJmN1bfhet/2v0S7M7/XPlmVRpfUluE2vYE0RtJt3BVDZfoWEGJPk9us\nN/JHu6UBUh6UM6ASFy5MlDLh36OxyO9sVx1WgQlNDmu2qcGUIkIgqTKppDCIP3Xk\nVQIDAQAB\n-----END PUBLIC KEY-----\n"
						},
						"tag": [],
						"attachment": [],
						"endpoints": {
							"sharedInbox": "https://mastodon.online/inbox"
						}
					}
				""";

            var actor = JsonSerializer.Deserialize<Actor>(json);
            Assert.IsNotNull(actor);
        }

        [TestMethod]
        public void Serialize()
        {
            var obj = new Actor
            {
                type = "Person",
                id = "id"
            };

            var json = JsonSerializer.Serialize(obj);


        }
    }
}