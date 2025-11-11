using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BirdsiteLive.ActivityPub;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Cryptography;
using BirdsiteLive.Domain.Factories;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace BirdsiteLive.Domain.Tests;

[TestClass]
public class CryptoServiceTest
{
    private readonly InstanceSettings _settings;
    private readonly CryptoService _cryptoService;
    private readonly ActivityPubService _activityPubService;
    private readonly MagicKey _key;
    
    public CryptoServiceTest()
    {
        _settings = new InstanceSettings
        {
            Domain = "domain.name"
        };
        var httpFactory = new Mock<IHttpClientFactory>();
        var log = new Mock<ILogger<ActivityPubService>>();
        var keyFactory = new Mock<IMagicKeyFactory>();
        _key = new MagicKey(
            """{"D":"Oar5IoCLbM2K82c2M3ljJJUAf7KFr6beFtlhFOj+4q1WnIReXylvoe3XBkotQ13jb1RQ5dNKQhI3oMUpHbLbG8mScHv48QtT6OR1HaDVDYwEdiSGN0JsmtBRigbMJyn2AX0OlwkxJe3xi6oo3eCV5CSjU/hiW9JXA9dKD3NP1VgLIGHRNgNcIxkOfVWDN01ECYu4t2OXnZCuyqunZ0lharUJ6e8lralqkZFoP6bMevsrTc3+hYXcjfYZkevet1yUxJqNfw7RWPKNbabheTtsAuS0jhXig8XoJKY8AIffVjchGgIUI4vq4nJMN2rUxz68CF/nFOuD8feER1byABSN4Q==", "P": "7pf+7JzVFRHCSFsaXsm+CnBR/pfwTv8GVp9kpmL7Baru9h7MHnxYJ0N/RTMpMyZvDVx0Sjobp+gLrKLiYD22QfxFAlTVjoDohKUsQugydA0wrDNJ5BBmWWxkapTInGutZYwWTkefWRC/hjyZlAvUhgW9ctSas8+/LEeuyA6ql6M=","Q":"2IaktklUUgI3gbk+7jXNOClm6rc6cjoetk4sS85EjoUJZs59uOAdpfmm0uIqNP0gKy4opxnsQFxybaEHwzuYWH+ZySNS9uRRjYKfDAU6OYCYEOFKlh6jjHUCdcd8VDFNSvA0MT5DZ8tpX2MjjahuGdlfXQZm1UKYB+h1g25caSc=","DP":"JEg83eJjjNasgrBH7E4ldhTqgxq70md5oUaP2bWHkq8Rs5+vTpt+FEpxWiaTh1G65X8/t+HqPrhMvi3u2s/HnXUtUVNxPkBgG3u6pVoGAhvXYPhTrjjIN6UCCCsj7pV5Qs3wvmqp0rN3TIR+nkLGSLMqwgGOnPVkjuk/rPB+BJ0=", "DQ": "wH2CdKNgGL/rxKGAtpiR5nm4CrX1eZL9tqhsbL/k5qaSoxizX+Wttd3pVtTFHPJi5MBWV6eOBfGpsJhVpFSYrSRS/SMwIFj9v0X+Stti1bfieC8w9aArWTS0iSxc9SQXSKWeYKCvn9iPxsMF2mt/5e7+/l4wkSpwqacYwU0dTkU=", "Modulus": "yc28Klfkn//jJKnZpzK4yDsfAv5u6mjzLJwMcW30IWk3k+N/cmH3MDDlC7en04kdYOKWLHSS0+G7XaxehZOj55GG+N7GjEBeUlls1jLHAP+zCyrtPh9UDmSOhYbrTdXAExHTcGn3rVCyYURopzk+gZ7GtNCEYIPrvpUhqgLVXE0FPTeBiyGq92VOmIEdDqQ9HdHgDnzd49oXsMxaqqSh6aJBIv8JgvDUR0OJD7xVrqd5ZvsoDcaKmkNfYL/TsFuQVH5DWC0emTlIgfYp246mUDoh1Z/3vvSglZjATXMx+zjnLbVQB8zcZSSdJSOLlpBPs8CeRjWZqMBFH1hdvML01Q==", "Exponent": "AQAB", "InverseQ": "G/edCYzS5R54fU2GDys37nkoq2rlHUG+uas3fJMKRr+2OMU6sBy26p76enXWEP2gtlqmugFvm4QeKYBUgxwNCPdfof+vNb1yh8wLiqWyG636+MYJK9NkUkAIpUjyVvI4rFWQX4+1cu7pqEqetfP0LafS+4Z+FPhBJK6Iz3YvJng="}""");
        keyFactory.Setup(_ => _.GetMagicKey()).ReturnsAsync(_key);
        _cryptoService = new CryptoService(keyFactory.Object);
        _activityPubService = new ActivityPubService(_cryptoService, _settings, httpFactory.Object, log.Object);
    }

    [TestMethod]
    public async Task SignGetUserAndValidate()
    {
        var actor = new Actor()
        {
            id = "https://domain.name/users/chuck",
            publicKey = new PublicKey()
            {
                publicKeyPem = _key.AsPEM,
            }
        };
        var req = await _activityPubService.BuildRequest((string)null, "domain.name", actor.id, HttpMethod.Get, "/users/bozo");
    }
    [TestMethod]
    public async Task SignMessageToInboxAndValidate()
    {
        var activity = new ActivityCreate()
        {

        };
        var actor = new Actor()
        {
            id = "https://domain.name/users/chuck",
            publicKey = new PublicKey()
            {
                publicKeyPem = _key.AsPEM,
            }
        };
        var req = await _activityPubService.BuildRequest(activity, "domain.name", actor.id, HttpMethod.Post, "/inbox");
        
        var res = CryptoService.ValidateSignature(actor, req.Headers.GetValues("Signature").First(), "post", "/inbox", "",req.Headers.ToDictionary(a => a.Key.ToLower(), a => a.Value.First()), await req.Content.ReadAsStringAsync());
        Assert.IsTrue(res.SignatureIsValidated);
    }

    [TestMethod]
    public void ValidBridgyFed()
    {

        // POST /users/id_aa_carmack/inbox HTTP/1.1
        // user-agent: Bridgy Fed (https://fed.brid.gy/)
        // accept-encoding: gzip, deflate
        // accept: */*
        // connection: keep-alive
        // date: Sun, 09 Nov 2025 15:54:04 GMT
        // host: bird.makeup
        // content-type: application/ld+json; profile="https://www.w3.org/ns/activitystreams"
        // digest: SHA-256=Ni6ysLKi5jiypZcCAGRG32xsXZihOpbr1jpdw1QflnE=
        // Content-Length: 494
        // signature: keyId="https://fed.brid.gy/snarfed.org#key",algorithm="rsa-sha256",signature="iqDJDgYdHOLZYLtYv2oC8fvFgzUvQARJXBE/IQfWWDKz1A48JkS1D1WOXSuNrV94vZN2MiY6LVqta6K+nZRACL1NPmARw9Le7wtg601eeqqoV/+l//KbB9j7Htl42P77IhLB7Vs9vbXISKNLOO9lULWon2ueeJiNc/laYZz+WeRMKXI1/28ERcU1nxqb/OvFnPGQ+ZNB1Xgia/6gonpVpbqn7aZ4tVDWMtdFbsIq476rIDTIOQ3Xi+b7rWP1t76/d/+Y89sEY1j83+7e0N7tik325mQ8tR0lU0niyjII65LTOQBS7hrIrQBGFE4FXqelwGM4DYcaUVf5jXR9p3htQA==",headers="date host digest (request-target)"
        // 
        // {"@context":["https://www.w3.org/ns/activitystreams","https://purl.archive.org/miscellany"],"type":"Follow","id":"https://fed.brid.gy/convert/ap/https://snarfed.org/#follow-2025-10-21T13:38:08-@id_aa_carmack@bird.makeup","actor":"https://fed.brid.gy/snarfed.org","object":"https://bird.makeup/users/id_aa_carmack","to":["https://www.w3.org/ns/activitystreams#Public"],"url":[{"type":"Link","rel":"canonical","href":"https://snarfed.org/#follow-2025-10-21T13:38:08-@id_aa_carmack@bird.makeup"}]}

        var actorJson = """
                        {
                        	"@context": [
                        		"https://www.w3.org/ns/activitystreams",
                        		"https://purl.archive.org/miscellany",
                        		{
                        			"discoverable": "http://joinmastodon.org/ns#discoverable",
                        			"indexable": "http://joinmastodon.org/ns#indexable"
                        		},
                        		{
                        			"PropertyValue": "http://schema.org#PropertyValue"
                        		},
                        		{
                        			"alsoKnownAs": {
                        				"@id": "as:alsoKnownAs",
                        				"@type": "@id"
                        			}
                        		},
                        		"https://w3id.org/security/v1"
                        	],
                        	"alsoKnownAs": [
                        		"https://snarfed.org/"
                        	],
                        	"attachment": [
                        		{
                        			"name": "Ryan Barrett",
                        			"type": "PropertyValue",
                        			"value": "<a rel=\"me\" href=\"https://snarfed.org/about\"><span class=\"invisible\">https://</span>snarfed.org/about</a>"
                        		}
                        	],
                        	"discoverable": true,
                        	"endpoints": {
                        		"sharedInbox": "https://fed.brid.gy/ap/sharedInbox"
                        	},
                        	"followers": "https://fed.brid.gy/snarfed.org/followers",
                        	"following": "https://fed.brid.gy/snarfed.org/following",
                        	"icon": {
                        		"name": "Ryan",
                        		"type": "Image",
                        		"url": "https://snarfed.org/ryan_profile_medium.jpg"
                        	},
                        	"id": "https://fed.brid.gy/snarfed.org",
                        	"image": [
                        		{
                        			"type": "Image",
                        			"url": "https://snarfed.org/w/wp-content/uploads/2013/06/copy-cropped-iraq_bar.jpg"
                        		},
                        		{
                        			"name": "Ryan",
                        			"type": "Image",
                        			"url": "https://snarfed.org/ryan_profile_medium.jpg"
                        		}
                        	],
                        	"inbox": "https://fed.brid.gy/snarfed.org/inbox",
                        	"indexable": true,
                        	"manuallyApprovesFollowers": false,
                        	"name": "Ryan Barrett",
                        	"outbox": "https://fed.brid.gy/snarfed.org/outbox",
                        	"preferredUsername": "snarfed.org",
                        	"publicKey": {
                        		"id": "https://fed.brid.gy/snarfed.org#key",
                        		"owner": "https://fed.brid.gy/snarfed.org",
                        		"publicKeyPem": "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAxi54qQrNQOcOnUftXBQm\n9BJoSCTOF6ffbVL+zXkEgktubVy5XD1Hli07CgvpDBz9jlKJNp1BJ2G0lpn0QBtW\nYvViPwlsgB1tsheChL3KtWTrnNl01+2lbYD2kgdTDXc/j/1BkL7ootHulE8jdjfK\ndNUUFoTmzF/5wgueNYMWNnWVSzGUVGyDo6TPYV6tmso3QRi0Cr5Sk28VqJ49oT5K\nbCv4eMrGJifbmVK4fhuTndQWeq1919pxnUByP5rsOBoj9b3YshygiWQAjaqV8W7W\n+kfybE4dGx4Cyj02HcgPlhgGexhSWo6Fpe9IvZGrKquql+kLyWRXvW5eQhb+l4bx\nlwIDAQAB\n-----END PUBLIC KEY-----"
                        	},
                        	"summary": "",
                        	"type": "Person",
                        	"url": "https://fed.brid.gy/r/https://snarfed.org/about"
                        }
                        """;
        
        var remote = JsonSerializer.Deserialize<Actor>(actorJson);
        var rawSig = "keyId=\"https://fed.brid.gy/snarfed.org#key\",algorithm=\"rsa-sha256\",signature=\"iqDJDgYdHOLZYLtYv2oC8fvFgzUvQARJXBE/IQfWWDKz1A48JkS1D1WOXSuNrV94vZN2MiY6LVqta6K+nZRACL1NPmARw9Le7wtg601eeqqoV/+l//KbB9j7Htl42P77IhLB7Vs9vbXISKNLOO9lULWon2ueeJiNc/laYZz+WeRMKXI1/28ERcU1nxqb/OvFnPGQ+ZNB1Xgia/6gonpVpbqn7aZ4tVDWMtdFbsIq476rIDTIOQ3Xi+b7rWP1t76/d/+Y89sEY1j83+7e0N7tik325mQ8tR0lU0niyjII65LTOQBS7hrIrQBGFE4FXqelwGM4DYcaUVf5jXR9p3htQA==\",headers=\"date host digest (request-target)\"";
        var method = "POST";
        var path = "/users/id_aa_carmack/inbox";
        var queryString = "";
        var body = "{\"@context\":[\"https://www.w3.org/ns/activitystreams\",\"https://purl.archive.org/miscellany\"],\"type\":\"Follow\",\"id\":\"https://fed.brid.gy/convert/ap/https://snarfed.org/#follow-2025-10-21T13:38:08-@id_aa_carmack@bird.makeup\",\"actor\":\"https://fed.brid.gy/snarfed.org\",\"object\":\"https://bird.makeup/users/id_aa_carmack\",\"to\":[\"https://www.w3.org/ns/activitystreams#Public\"],\"url\":[{\"type\":\"Link\",\"rel\":\"canonical\",\"href\":\"https://snarfed.org/#follow-2025-10-21T13:38:08-@id_aa_carmack@bird.makeup\"}]}";
        var headers = new Dictionary<string, string>()
        {
            { "date", "Sun, 09 Nov 2025 15:54:04 GMT" },
            { "digest", "SHA-256=Ni6ysLKi5jiypZcCAGRG32xsXZihOpbr1jpdw1QflnE=" },
            { "host", "bird.makeup"},
            { "content-type", "application/activity+json"}
        };

        var res = CryptoService.ValidateSignature(remote, rawSig, method, path, queryString, headers, body);
        Assert.IsTrue(res.SignatureIsValidated);
    }
    [TestMethod]
    public void ValidDelete()
    {
        // kilomakeup | trce: BirdsiteLive.Controllers.InboxController[0]
        // kilomakeup |       Inbox: {"@context":"https://www.w3.org/ns/activitystreams","id":"https://red.niboe.info/users/JotaLuis#delete","type":"Delete","actor":"https://red.niboe.info/users/JotaLuis","to":["https://www.w3.org/ns/activitystreams#Public"],"object":"https://red.niboe.info/users/JotaLuis","signature":{"type":"RsaSignature2017","creator":"https://red.niboe.info/users/JotaLuis#main-key","created":"2024-08-05T07:02:03Z","signatureValue":"FmHVziXqvga1RL2UWXT70cohfl1pDfhFAGNjIBPhUU2lRbHGiNWIvHxQil4oTxtLOam2kb8iWQDqOTl1DkeHgOU2LVd73BzxppCL83lGzyVQSkV2UjoF31ME5LSxEYbAuPy+jtZ7te1TL6OEk3ZSZkr+BEbLy02cqzYRGCVqfD5OsDsNusp18AcNEiA+4tFUtHs79wU1Dq8YWYPdj+tK0J3whaHLTcf5e7yckkkQe0fJtUlBaUXnN09OFzYK6+GoBXZteoQKrxVSNmQpa87Ofc1urxsjZQk7blj40nqXR4FO5ZkdIq6/QDg313D/t2b3OLo/IIj5ZGt2NN3/fq3J8A=="}}
        // kilomakeup | trce: BirdsiteLive.Controllers.InboxController[0]
        // kilomakeup |       Signature: keyId="https://red.niboe.info/users/JotaLuis#main-key",algorithm="rsa-sha256",headers="(request-target) host date digest content-type",signature="1tCzRmNA3Cmu1Wh+tdVc0FWrvuNKziAEtFaxZWZqUdN/4H0/uqRoN0M/nmqTKb5v3awZjDd1TH6a7Ht69O6ZHSRAF//OHLXhm7TKL+I+7M063v1n3j+mTzgDV3YMx3SGs5kG0V69jxto7KNxWuuhFReEoQY0o6GGL2Jgk5fBfhOpx+ltl5YXQy+discZP0LnFccOjp03nH3AOHpnYNHpxKvL/nPXqYm53p/tmTmT9/xr7dOFl4uMEY83KY5RDEETS8C5Y5YnXFZAb7I1WvwCKYpSD6M3YYMkB/uHcizLPEo5y7adxsEtd5p27RrZ6Sj96cM3pI0sOMHGhT9ZSmCc6w=="
        // kilomakeup | trce: BirdsiteLive.Controllers.InboxController[0]
        // kilomakeup |       Date: Mon, 05 Aug 2024 20:13:26 GMT
        // kilomakeup | trce: BirdsiteLive.Controllers.InboxController[0]
        // kilomakeup |       Digest: SHA-256=KM5J2Alu2SYGDBkhkln2p4zVYwjyGfvTxiRCibC/y6Q=
        //     kilomakeup | trce: BirdsiteLive.Controllers.InboxController[0]
        // kilomakeup |       Host: kilogram.makeup
        // kilomakeup | trce: BirdsiteLive.Controllers.InboxController[0]
        // kilomakeup |       Host: application/activity+json


        var actorJson =
            "{\n\t\"@context\": [\n\t\t\"https://www.w3.org/ns/activitystreams\",\n\t\t\"https://w3id.org/security/v1\",\n\t\t{\n\t\t\t\"manuallyApprovesFollowers\": \"as:manuallyApprovesFollowers\",\n\t\t\t\"toot\": \"http://joinmastodon.org/ns#\",\n\t\t\t\"featured\": {\n\t\t\t\t\"@id\": \"toot:featured\",\n\t\t\t\t\"@type\": \"@id\"\n\t\t\t},\n\t\t\t\"featuredTags\": {\n\t\t\t\t\"@id\": \"toot:featuredTags\",\n\t\t\t\t\"@type\": \"@id\"\n\t\t\t},\n\t\t\t\"alsoKnownAs\": {\n\t\t\t\t\"@id\": \"as:alsoKnownAs\",\n\t\t\t\t\"@type\": \"@id\"\n\t\t\t},\n\t\t\t\"movedTo\": {\n\t\t\t\t\"@id\": \"as:movedTo\",\n\t\t\t\t\"@type\": \"@id\"\n\t\t\t},\n\t\t\t\"schema\": \"http://schema.org#\",\n\t\t\t\"PropertyValue\": \"schema:PropertyValue\",\n\t\t\t\"value\": \"schema:value\",\n\t\t\t\"IdentityProof\": \"toot:IdentityProof\",\n\t\t\t\"discoverable\": \"toot:discoverable\",\n\t\t\t\"Device\": \"toot:Device\",\n\t\t\t\"Ed25519Signature\": \"toot:Ed25519Signature\",\n\t\t\t\"Ed25519Key\": \"toot:Ed25519Key\",\n\t\t\t\"Curve25519Key\": \"toot:Curve25519Key\",\n\t\t\t\"EncryptedMessage\": \"toot:EncryptedMessage\",\n\t\t\t\"publicKeyBase64\": \"toot:publicKeyBase64\",\n\t\t\t\"deviceId\": \"toot:deviceId\",\n\t\t\t\"claim\": {\n\t\t\t\t\"@type\": \"@id\",\n\t\t\t\t\"@id\": \"toot:claim\"\n\t\t\t},\n\t\t\t\"fingerprintKey\": {\n\t\t\t\t\"@type\": \"@id\",\n\t\t\t\t\"@id\": \"toot:fingerprintKey\"\n\t\t\t},\n\t\t\t\"identityKey\": {\n\t\t\t\t\"@type\": \"@id\",\n\t\t\t\t\"@id\": \"toot:identityKey\"\n\t\t\t},\n\t\t\t\"devices\": {\n\t\t\t\t\"@type\": \"@id\",\n\t\t\t\t\"@id\": \"toot:devices\"\n\t\t\t},\n\t\t\t\"messageFranking\": \"toot:messageFranking\",\n\t\t\t\"messageType\": \"toot:messageType\",\n\t\t\t\"cipherText\": \"toot:cipherText\",\n\t\t\t\"suspended\": \"toot:suspended\"\n\t\t}\n\t],\n\t\"id\": \"https://red.niboe.info/users/JotaLuis\",\n\t\"type\": \"Person\",\n\t\"following\": \"https://red.niboe.info/users/JotaLuis/following\",\n\t\"followers\": \"https://red.niboe.info/users/JotaLuis/followers\",\n\t\"inbox\": \"https://red.niboe.info/users/JotaLuis/inbox\",\n\t\"outbox\": \"https://red.niboe.info/users/JotaLuis/outbox\",\n\t\"featured\": \"https://red.niboe.info/users/JotaLuis/collections/featured\",\n\t\"featuredTags\": \"https://red.niboe.info/users/JotaLuis/collections/tags\",\n\t\"preferredUsername\": \"JotaLuis\",\n\t\"name\": \"\",\n\t\"summary\": \"\",\n\t\"url\": \"https://red.niboe.info/@JotaLuis\",\n\t\"manuallyApprovesFollowers\": false,\n\t\"discoverable\": false,\n\t\"published\": \"2022-10-02T00:00:00Z\",\n\t\"devices\": \"https://red.niboe.info/users/JotaLuis/collections/devices\",\n\t\"suspended\": true,\n\t\"publicKey\": {\n\t\t\"id\": \"https://red.niboe.info/users/JotaLuis#main-key\",\n\t\t\"owner\": \"https://red.niboe.info/users/JotaLuis\",\n\t\t\"publicKeyPem\": \"-----BEGIN PUBLIC KEY-----\\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA3T/I+q10jmoD8XFhVeqb\\neIl8uu740rQhxK7v5InHn9ItBjM0+La7SjvvE+WPWMtnbYnEX+JKERutz7AhLyjw\\nAGoMrAoRL+3zKi3WvTfSawYkkaAt8eYHz2+VkPXXCDF8ez2QEes+vNEpnUfNHwrW\\nWlMI2SWaajAws9uvXMsPnw2MQk4qWc3iocE11uaJ29kK69zrX+eQQ9iWMjv9LMaN\\numo2o0tApAsyX6RBs9si3NRiWIx5atK+bnK7CgN7Gczz5VYl0ALBqtYNNCSuVz9O\\no1fAT+dG3A8Y4riyWDMaWhCe+GUTrpP9KxOr4lmDZ4Hgjqt4n5Owx4Q+qpzuMS6u\\nSwIDAQAB\\n-----END PUBLIC KEY-----\\n\"\n\t},\n\t\"tag\": [],\n\t\"attachment\": [],\n\t\"endpoints\": {\n\t\t\"sharedInbox\": \"https://red.niboe.info/inbox\"\n\t}\n}";
        
        var remote = JsonSerializer.Deserialize<Actor>(actorJson);
        var rawSig =
            "keyId=\"https://red.niboe.info/users/JotaLuis#main-key\",algorithm=\"rsa-sha256\",headers=\"(request-target) host date digest content-type\",signature=\"1tCzRmNA3Cmu1Wh+tdVc0FWrvuNKziAEtFaxZWZqUdN/4H0/uqRoN0M/nmqTKb5v3awZjDd1TH6a7Ht69O6ZHSRAF//OHLXhm7TKL+I+7M063v1n3j+mTzgDV3YMx3SGs5kG0V69jxto7KNxWuuhFReEoQY0o6GGL2Jgk5fBfhOpx+ltl5YXQy+discZP0LnFccOjp03nH3AOHpnYNHpxKvL/nPXqYm53p/tmTmT9/xr7dOFl4uMEY83KY5RDEETS8C5Y5YnXFZAb7I1WvwCKYpSD6M3YYMkB/uHcizLPEo5y7adxsEtd5p27RrZ6Sj96cM3pI0sOMHGhT9ZSmCc6w==\"";
        var method = "POST";
        var path = "/inbox";
        var queryString = "";
        var body = "{\"@context\":\"https://www.w3.org/ns/activitystreams\",\"id\":\"https://red.niboe.info/users/JotaLuis#delete\",\"type\":\"Delete\",\"actor\":\"https://red.niboe.info/users/JotaLuis\",\"to\":[\"https://www.w3.org/ns/activitystreams#Public\"],\"object\":\"https://red.niboe.info/users/JotaLuis\",\"signature\":{\"type\":\"RsaSignature2017\",\"creator\":\"https://red.niboe.info/users/JotaLuis#main-key\",\"created\":\"2024-08-05T07:02:03Z\",\"signatureValue\":\"FmHVziXqvga1RL2UWXT70cohfl1pDfhFAGNjIBPhUU2lRbHGiNWIvHxQil4oTxtLOam2kb8iWQDqOTl1DkeHgOU2LVd73BzxppCL83lGzyVQSkV2UjoF31ME5LSxEYbAuPy+jtZ7te1TL6OEk3ZSZkr+BEbLy02cqzYRGCVqfD5OsDsNusp18AcNEiA+4tFUtHs79wU1Dq8YWYPdj+tK0J3whaHLTcf5e7yckkkQe0fJtUlBaUXnN09OFzYK6+GoBXZteoQKrxVSNmQpa87Ofc1urxsjZQk7blj40nqXR4FO5ZkdIq6/QDg313D/t2b3OLo/IIj5ZGt2NN3/fq3J8A==\"}}";
        var headers = new Dictionary<string, string>()
        {
            { "date", "Mon, 05 Aug 2024 20:13:26 GMT" },
            { "digest", "SHA-256=KM5J2Alu2SYGDBkhkln2p4zVYwjyGfvTxiRCibC/y6Q=" },
            { "host", "kilogram.makeup"},
            { "content-type", "application/activity+json"}
        };

        var res = CryptoService.ValidateSignature(remote, rawSig, method, path, queryString, headers, body);
        Assert.IsTrue(res.SignatureIsValidated);
    }
    [TestMethod]
    public void BadHost()
    {
        // kilomakeup | trce: BirdsiteLive.Controllers.InboxController[0]
        // kilomakeup |       Inbox: {"@context":"https://www.w3.org/ns/activitystreams","id":"https://red.niboe.info/users/JotaLuis#delete","type":"Delete","actor":"https://red.niboe.info/users/JotaLuis","to":["https://www.w3.org/ns/activitystreams#Public"],"object":"https://red.niboe.info/users/JotaLuis","signature":{"type":"RsaSignature2017","creator":"https://red.niboe.info/users/JotaLuis#main-key","created":"2024-08-05T07:02:03Z","signatureValue":"FmHVziXqvga1RL2UWXT70cohfl1pDfhFAGNjIBPhUU2lRbHGiNWIvHxQil4oTxtLOam2kb8iWQDqOTl1DkeHgOU2LVd73BzxppCL83lGzyVQSkV2UjoF31ME5LSxEYbAuPy+jtZ7te1TL6OEk3ZSZkr+BEbLy02cqzYRGCVqfD5OsDsNusp18AcNEiA+4tFUtHs79wU1Dq8YWYPdj+tK0J3whaHLTcf5e7yckkkQe0fJtUlBaUXnN09OFzYK6+GoBXZteoQKrxVSNmQpa87Ofc1urxsjZQk7blj40nqXR4FO5ZkdIq6/QDg313D/t2b3OLo/IIj5ZGt2NN3/fq3J8A=="}}
        // kilomakeup | trce: BirdsiteLive.Controllers.InboxController[0]
        // kilomakeup |       Signature: keyId="https://red.niboe.info/users/JotaLuis#main-key",algorithm="rsa-sha256",headers="(request-target) host date digest content-type",signature="1tCzRmNA3Cmu1Wh+tdVc0FWrvuNKziAEtFaxZWZqUdN/4H0/uqRoN0M/nmqTKb5v3awZjDd1TH6a7Ht69O6ZHSRAF//OHLXhm7TKL+I+7M063v1n3j+mTzgDV3YMx3SGs5kG0V69jxto7KNxWuuhFReEoQY0o6GGL2Jgk5fBfhOpx+ltl5YXQy+discZP0LnFccOjp03nH3AOHpnYNHpxKvL/nPXqYm53p/tmTmT9/xr7dOFl4uMEY83KY5RDEETS8C5Y5YnXFZAb7I1WvwCKYpSD6M3YYMkB/uHcizLPEo5y7adxsEtd5p27RrZ6Sj96cM3pI0sOMHGhT9ZSmCc6w=="
        // kilomakeup | trce: BirdsiteLive.Controllers.InboxController[0]
        // kilomakeup |       Date: Mon, 05 Aug 2024 20:13:26 GMT
        // kilomakeup | trce: BirdsiteLive.Controllers.InboxController[0]
        // kilomakeup |       Digest: SHA-256=KM5J2Alu2SYGDBkhkln2p4zVYwjyGfvTxiRCibC/y6Q=
        //     kilomakeup | trce: BirdsiteLive.Controllers.InboxController[0]
        // kilomakeup |       Host: kilogram.makeup
        // kilomakeup | trce: BirdsiteLive.Controllers.InboxController[0]
        // kilomakeup |       Host: application/activity+json


        var actorJson =
            "{\n\t\"@context\": [\n\t\t\"https://www.w3.org/ns/activitystreams\",\n\t\t\"https://w3id.org/security/v1\",\n\t\t{\n\t\t\t\"manuallyApprovesFollowers\": \"as:manuallyApprovesFollowers\",\n\t\t\t\"toot\": \"http://joinmastodon.org/ns#\",\n\t\t\t\"featured\": {\n\t\t\t\t\"@id\": \"toot:featured\",\n\t\t\t\t\"@type\": \"@id\"\n\t\t\t},\n\t\t\t\"featuredTags\": {\n\t\t\t\t\"@id\": \"toot:featuredTags\",\n\t\t\t\t\"@type\": \"@id\"\n\t\t\t},\n\t\t\t\"alsoKnownAs\": {\n\t\t\t\t\"@id\": \"as:alsoKnownAs\",\n\t\t\t\t\"@type\": \"@id\"\n\t\t\t},\n\t\t\t\"movedTo\": {\n\t\t\t\t\"@id\": \"as:movedTo\",\n\t\t\t\t\"@type\": \"@id\"\n\t\t\t},\n\t\t\t\"schema\": \"http://schema.org#\",\n\t\t\t\"PropertyValue\": \"schema:PropertyValue\",\n\t\t\t\"value\": \"schema:value\",\n\t\t\t\"IdentityProof\": \"toot:IdentityProof\",\n\t\t\t\"discoverable\": \"toot:discoverable\",\n\t\t\t\"Device\": \"toot:Device\",\n\t\t\t\"Ed25519Signature\": \"toot:Ed25519Signature\",\n\t\t\t\"Ed25519Key\": \"toot:Ed25519Key\",\n\t\t\t\"Curve25519Key\": \"toot:Curve25519Key\",\n\t\t\t\"EncryptedMessage\": \"toot:EncryptedMessage\",\n\t\t\t\"publicKeyBase64\": \"toot:publicKeyBase64\",\n\t\t\t\"deviceId\": \"toot:deviceId\",\n\t\t\t\"claim\": {\n\t\t\t\t\"@type\": \"@id\",\n\t\t\t\t\"@id\": \"toot:claim\"\n\t\t\t},\n\t\t\t\"fingerprintKey\": {\n\t\t\t\t\"@type\": \"@id\",\n\t\t\t\t\"@id\": \"toot:fingerprintKey\"\n\t\t\t},\n\t\t\t\"identityKey\": {\n\t\t\t\t\"@type\": \"@id\",\n\t\t\t\t\"@id\": \"toot:identityKey\"\n\t\t\t},\n\t\t\t\"devices\": {\n\t\t\t\t\"@type\": \"@id\",\n\t\t\t\t\"@id\": \"toot:devices\"\n\t\t\t},\n\t\t\t\"messageFranking\": \"toot:messageFranking\",\n\t\t\t\"messageType\": \"toot:messageType\",\n\t\t\t\"cipherText\": \"toot:cipherText\",\n\t\t\t\"suspended\": \"toot:suspended\"\n\t\t}\n\t],\n\t\"id\": \"https://red.niboe.info/users/JotaLuis\",\n\t\"type\": \"Person\",\n\t\"following\": \"https://red.niboe.info/users/JotaLuis/following\",\n\t\"followers\": \"https://red.niboe.info/users/JotaLuis/followers\",\n\t\"inbox\": \"https://red.niboe.info/users/JotaLuis/inbox\",\n\t\"outbox\": \"https://red.niboe.info/users/JotaLuis/outbox\",\n\t\"featured\": \"https://red.niboe.info/users/JotaLuis/collections/featured\",\n\t\"featuredTags\": \"https://red.niboe.info/users/JotaLuis/collections/tags\",\n\t\"preferredUsername\": \"JotaLuis\",\n\t\"name\": \"\",\n\t\"summary\": \"\",\n\t\"url\": \"https://red.niboe.info/@JotaLuis\",\n\t\"manuallyApprovesFollowers\": false,\n\t\"discoverable\": false,\n\t\"published\": \"2022-10-02T00:00:00Z\",\n\t\"devices\": \"https://red.niboe.info/users/JotaLuis/collections/devices\",\n\t\"suspended\": true,\n\t\"publicKey\": {\n\t\t\"id\": \"https://red.niboe.info/users/JotaLuis#main-key\",\n\t\t\"owner\": \"https://red.niboe.info/users/JotaLuis\",\n\t\t\"publicKeyPem\": \"-----BEGIN PUBLIC KEY-----\\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA3T/I+q10jmoD8XFhVeqb\\neIl8uu740rQhxK7v5InHn9ItBjM0+La7SjvvE+WPWMtnbYnEX+JKERutz7AhLyjw\\nAGoMrAoRL+3zKi3WvTfSawYkkaAt8eYHz2+VkPXXCDF8ez2QEes+vNEpnUfNHwrW\\nWlMI2SWaajAws9uvXMsPnw2MQk4qWc3iocE11uaJ29kK69zrX+eQQ9iWMjv9LMaN\\numo2o0tApAsyX6RBs9si3NRiWIx5atK+bnK7CgN7Gczz5VYl0ALBqtYNNCSuVz9O\\no1fAT+dG3A8Y4riyWDMaWhCe+GUTrpP9KxOr4lmDZ4Hgjqt4n5Owx4Q+qpzuMS6u\\nSwIDAQAB\\n-----END PUBLIC KEY-----\\n\"\n\t},\n\t\"tag\": [],\n\t\"attachment\": [],\n\t\"endpoints\": {\n\t\t\"sharedInbox\": \"https://red.niboe.info/inbox\"\n\t}\n}";
        
        var remote = JsonSerializer.Deserialize<Actor>(actorJson);
        var rawSig =
            "keyId=\"https://red.niboe.info/users/JotaLuis#main-key\",algorithm=\"rsa-sha256\",headers=\"(request-target) host date digest content-type\",signature=\"1tCzRmNA3Cmu1Wh+tdVc0FWrvuNKziAEtFaxZWZqUdN/4H0/uqRoN0M/nmqTKb5v3awZjDd1TH6a7Ht69O6ZHSRAF//OHLXhm7TKL+I+7M063v1n3j+mTzgDV3YMx3SGs5kG0V69jxto7KNxWuuhFReEoQY0o6GGL2Jgk5fBfhOpx+ltl5YXQy+discZP0LnFccOjp03nH3AOHpnYNHpxKvL/nPXqYm53p/tmTmT9/xr7dOFl4uMEY83KY5RDEETS8C5Y5YnXFZAb7I1WvwCKYpSD6M3YYMkB/uHcizLPEo5y7adxsEtd5p27RrZ6Sj96cM3pI0sOMHGhT9ZSmCc6w==\"";
        var method = "POST";
        var path = "/inbox";
        var queryString = "";
        var body = "{\"@context\":\"https://www.w3.org/ns/activitystreams\",\"id\":\"https://red.niboe.info/users/JotaLuis#delete\",\"type\":\"Delete\",\"actor\":\"https://red.niboe.info/users/JotaLuis\",\"to\":[\"https://www.w3.org/ns/activitystreams#Public\"],\"object\":\"https://red.niboe.info/users/JotaLuis\",\"signature\":{\"type\":\"RsaSignature2017\",\"creator\":\"https://red.niboe.info/users/JotaLuis#main-key\",\"created\":\"2024-08-05T07:02:03Z\",\"signatureValue\":\"FmHVziXqvga1RL2UWXT70cohfl1pDfhFAGNjIBPhUU2lRbHGiNWIvHxQil4oTxtLOam2kb8iWQDqOTl1DkeHgOU2LVd73BzxppCL83lGzyVQSkV2UjoF31ME5LSxEYbAuPy+jtZ7te1TL6OEk3ZSZkr+BEbLy02cqzYRGCVqfD5OsDsNusp18AcNEiA+4tFUtHs79wU1Dq8YWYPdj+tK0J3whaHLTcf5e7yckkkQe0fJtUlBaUXnN09OFzYK6+GoBXZteoQKrxVSNmQpa87Ofc1urxsjZQk7blj40nqXR4FO5ZkdIq6/QDg313D/t2b3OLo/IIj5ZGt2NN3/fq3J8A==\"}}";
        var headers = new Dictionary<string, string>()
        {
            { "date", "Mon, 05 Aug 2024 20:13:26 GMT" },
            { "digest", "SHA-256=KM5J2Alu2SYGDBkhkln2p4zVYwjyGfvTxiRCibC/y6Q=" },
            { "host", "fake.bad"},
            { "content-type", "application/activity+json"}
        };

        var res = CryptoService.ValidateSignature(remote, rawSig, method, path, queryString, headers, body);
        Assert.IsFalse(res.SignatureIsValidated);
    }
    
    [TestMethod]
    public void BadDigest()
    {
        var actorJson =
            "{\n\t\"@context\": [\n\t\t\"https://www.w3.org/ns/activitystreams\",\n\t\t\"https://w3id.org/security/v1\",\n\t\t{\n\t\t\t\"manuallyApprovesFollowers\": \"as:manuallyApprovesFollowers\",\n\t\t\t\"toot\": \"http://joinmastodon.org/ns#\",\n\t\t\t\"featured\": {\n\t\t\t\t\"@id\": \"toot:featured\",\n\t\t\t\t\"@type\": \"@id\"\n\t\t\t},\n\t\t\t\"featuredTags\": {\n\t\t\t\t\"@id\": \"toot:featuredTags\",\n\t\t\t\t\"@type\": \"@id\"\n\t\t\t},\n\t\t\t\"alsoKnownAs\": {\n\t\t\t\t\"@id\": \"as:alsoKnownAs\",\n\t\t\t\t\"@type\": \"@id\"\n\t\t\t},\n\t\t\t\"movedTo\": {\n\t\t\t\t\"@id\": \"as:movedTo\",\n\t\t\t\t\"@type\": \"@id\"\n\t\t\t},\n\t\t\t\"schema\": \"http://schema.org#\",\n\t\t\t\"PropertyValue\": \"schema:PropertyValue\",\n\t\t\t\"value\": \"schema:value\",\n\t\t\t\"IdentityProof\": \"toot:IdentityProof\",\n\t\t\t\"discoverable\": \"toot:discoverable\",\n\t\t\t\"Device\": \"toot:Device\",\n\t\t\t\"Ed25519Signature\": \"toot:Ed25519Signature\",\n\t\t\t\"Ed25519Key\": \"toot:Ed25519Key\",\n\t\t\t\"Curve25519Key\": \"toot:Curve25519Key\",\n\t\t\t\"EncryptedMessage\": \"toot:EncryptedMessage\",\n\t\t\t\"publicKeyBase64\": \"toot:publicKeyBase64\",\n\t\t\t\"deviceId\": \"toot:deviceId\",\n\t\t\t\"claim\": {\n\t\t\t\t\"@type\": \"@id\",\n\t\t\t\t\"@id\": \"toot:claim\"\n\t\t\t},\n\t\t\t\"fingerprintKey\": {\n\t\t\t\t\"@type\": \"@id\",\n\t\t\t\t\"@id\": \"toot:fingerprintKey\"\n\t\t\t},\n\t\t\t\"identityKey\": {\n\t\t\t\t\"@type\": \"@id\",\n\t\t\t\t\"@id\": \"toot:identityKey\"\n\t\t\t},\n\t\t\t\"devices\": {\n\t\t\t\t\"@type\": \"@id\",\n\t\t\t\t\"@id\": \"toot:devices\"\n\t\t\t},\n\t\t\t\"messageFranking\": \"toot:messageFranking\",\n\t\t\t\"messageType\": \"toot:messageType\",\n\t\t\t\"cipherText\": \"toot:cipherText\",\n\t\t\t\"suspended\": \"toot:suspended\"\n\t\t}\n\t],\n\t\"id\": \"https://red.niboe.info/users/JotaLuis\",\n\t\"type\": \"Person\",\n\t\"following\": \"https://red.niboe.info/users/JotaLuis/following\",\n\t\"followers\": \"https://red.niboe.info/users/JotaLuis/followers\",\n\t\"inbox\": \"https://red.niboe.info/users/JotaLuis/inbox\",\n\t\"outbox\": \"https://red.niboe.info/users/JotaLuis/outbox\",\n\t\"featured\": \"https://red.niboe.info/users/JotaLuis/collections/featured\",\n\t\"featuredTags\": \"https://red.niboe.info/users/JotaLuis/collections/tags\",\n\t\"preferredUsername\": \"JotaLuis\",\n\t\"name\": \"\",\n\t\"summary\": \"\",\n\t\"url\": \"https://red.niboe.info/@JotaLuis\",\n\t\"manuallyApprovesFollowers\": false,\n\t\"discoverable\": false,\n\t\"published\": \"2022-10-02T00:00:00Z\",\n\t\"devices\": \"https://red.niboe.info/users/JotaLuis/collections/devices\",\n\t\"suspended\": true,\n\t\"publicKey\": {\n\t\t\"id\": \"https://red.niboe.info/users/JotaLuis#main-key\",\n\t\t\"owner\": \"https://red.niboe.info/users/JotaLuis\",\n\t\t\"publicKeyPem\": \"-----BEGIN PUBLIC KEY-----\\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA3T/I+q10jmoD8XFhVeqb\\neIl8uu740rQhxK7v5InHn9ItBjM0+La7SjvvE+WPWMtnbYnEX+JKERutz7AhLyjw\\nAGoMrAoRL+3zKi3WvTfSawYkkaAt8eYHz2+VkPXXCDF8ez2QEes+vNEpnUfNHwrW\\nWlMI2SWaajAws9uvXMsPnw2MQk4qWc3iocE11uaJ29kK69zrX+eQQ9iWMjv9LMaN\\numo2o0tApAsyX6RBs9si3NRiWIx5atK+bnK7CgN7Gczz5VYl0ALBqtYNNCSuVz9O\\no1fAT+dG3A8Y4riyWDMaWhCe+GUTrpP9KxOr4lmDZ4Hgjqt4n5Owx4Q+qpzuMS6u\\nSwIDAQAB\\n-----END PUBLIC KEY-----\\n\"\n\t},\n\t\"tag\": [],\n\t\"attachment\": [],\n\t\"endpoints\": {\n\t\t\"sharedInbox\": \"https://red.niboe.info/inbox\"\n\t}\n}";
        
        var remote = JsonSerializer.Deserialize<Actor>(actorJson);
        var rawSig =
            "keyId=\"https://red.niboe.info/users/JotaLuis#main-key\",algorithm=\"rsa-sha256\",headers=\"(request-target) host date digest content-type\",signature=\"1tCzRmNA3Cmu1Wh+tdVc0FWrvuNKziAEtFaxZWZqUdN/4H0/uqRoN0M/nmqTKb5v3awZjDd1TH6a7Ht69O6ZHSRAF//OHLXhm7TKL+I+7M063v1n3j+mTzgDV3YMx3SGs5kG0V69jxto7KNxWuuhFReEoQY0o6GGL2Jgk5fBfhOpx+ltl5YXQy+discZP0LnFccOjp03nH3AOHpnYNHpxKvL/nPXqYm53p/tmTmT9/xr7dOFl4uMEY83KY5RDEETS8C5Y5YnXFZAb7I1WvwCKYpSD6M3YYMkB/uHcizLPEo5y7adxsEtd5p27RrZ6Sj96cM3pI0sOMHGhT9ZSmCc6w==\"";
        var method = "POST";
        var path = "/inbox";
        var queryString = "";
        var body = "{\"@context\":\"https://www.w3.org/ns/activitystreams\",\"id\":\"https://red.niboe.info/users/JotaLuis#delete\",\"type\":\"Delete\",\"actor\":\"https://red.niboe.info/users/JotaLuis\",\"to\":[\"https://www.w3.org/ns/activitystreams#Public\"],\"object\":\"https://red.niboe.info/users/JotaLuis\",\"signature\":{\"type\":\"RsaSignature2017\",\"creator\":\"https://red.niboe.info/users/JotaLuis#main-key\",\"created\":\"2024-08-05T07:02:03Z\",\"signatureValue\":\"FmHVziXqvga1RL2UWXT70cohfl1pDfhFAGNjIBPhUU2lRbHGiNWIvHxQil4oTxtLOam2kb8iWQDqOTl1DkeHgOU2LVd73BzxppCL83lGzyVQSkV2UjoF31ME5LSxEYbAuPy+jtZ7te1TL6OEk3ZSZkr+BEbLy02cqzYRGCVqfD5OsDsNusp18AcNEiA+4tFUtHs79wU1Dq8YWYPdj+tK0J3whaHLTcf5e7yckkkQe0fJtUlBaUXnN09OFzYK6+GoBXZteoQKrxVSNmQpa87Ofc1urxsjZQk7blj40nqXR4FO5ZkdIq6/QDg313D/t2b3OLo/IIj5ZGt2NN3/fq3J8A==\"}}";
        var headers = new Dictionary<string, string>()
        {
            { "date", "Mon, 05 Aug 2024 20:13:26 GMT" },
            { "digest", "SHA-256=KM5J2Alu2SYGDBkhkln2p4zVYwjygfvTxiRCibC/y6Q=" },
            { "host", "kilogram.makeup"},
            { "content-type", "application/activity+json"}
        };

        var res = CryptoService.ValidateSignature(remote, rawSig, method, path, queryString, headers, body);
        Assert.IsFalse(res.SignatureIsValidated);
    }
}