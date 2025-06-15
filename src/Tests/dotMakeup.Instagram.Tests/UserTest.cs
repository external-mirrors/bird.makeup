using System.Text.Json;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using dotMakeup.Instagram.Models;
using dotMakeup.ipfs;
using Moq;

namespace dotMakeup.Instagram.Tests;

[TestClass]
public class UserTest
{
    private ISocialMediaService _instaService;
    private IIpfsService _ipfsService;
    [TestInitialize]
    public async Task TestInit()
    {
        var userDal = new Mock<IInstagramUserDal>();
        var httpFactory = new Mock<IHttpClientFactory>();
        var settingsDal = new Mock<ISettingsDal>();
        settingsDal.Setup(_ => _.Get("ig_always_refresh"))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement);
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        httpFactory.Setup(_ => _.CreateClient("WithProxy")).Returns(new HttpClient());
        var settings = new InstanceSettings
        {
            Domain = "domain.name",
            SidecarURL = "http://localhost:5001"
        };

        var ipfsService = new Mock<IIpfsService>();
        ipfsService.Setup(a => a.Mirror(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync("abc");
        ipfsService.Setup(a => a.GetIpfsPublicLink(It.IsAny<string>())).Returns("http://abc.com");
        _instaService = new InstagramService(ipfsService.Object, userDal.Object, httpFactory.Object, settings, settingsDal.Object);
    }
    [TestMethod]
    public async Task user_kobe()
    {
        InstagramUser user;
        try
        {
            user = (InstagramUser)await _instaService.GetUserAsync("kobebryant");
        }
        catch (Exception _)
        {
            Assert.Inconclusive();
            return;
        }
        Assert.AreEqual(user.Description, "Writer. Producer. Investor @granity @bryantstibel @drinkbodyarmor @mambamambacitasports");
        Assert.AreEqual(user.Name, "Kobe Bryant");
        Assert.IsNotNull(user.ProfileImageUrl);
        Assert.IsNull(user.Url);
        
        Assert.AreEqual(user.RecentPosts.ElementAt(1).Media.Length, 2);
        Assert.AreEqual(user.RecentPosts.ElementAt(1).Media.First().MediaType, "video/mp4");
        Assert.AreEqual(user.RecentPosts.ElementAt(2).Media.First().MediaType, "image/jpeg");
    }
    [TestMethod]
    public async Task user_virgil()
    {
        InstagramUser user;
        try
        {
            user = (InstagramUser)await _instaService.GetUserAsync("virgilabloh");
        }
        catch (Exception _)
        {
            Assert.Inconclusive();
            return;
        }
        Assert.AreEqual(user.Description, "a 𝚜𝚎𝚖𝚒-𝚌𝚑𝚛𝚘𝚗𝚘𝚕𝚘𝚐𝚒𝚌𝚊𝚕 𝚍𝚘𝚌𝚞𝚖𝚎𝚗𝚝 𝚘𝚏 𝚒𝚍𝚎𝚊𝚜.");
        Assert.AreEqual(user.Name, "");
        Assert.AreEqual(user.Url, "http://virgilabloh.com/land_i_own/");
        var latest = user.RecentPosts.First();
        Assert.AreEqual(latest.Media.Length, 1);
        Assert.IsTrue(latest.MessageContent.StartsWith("We are devastated to announce the passing of our beloved Virgil Abloh, a "));
        
        Assert.AreEqual(user.RecentPosts.ElementAt(2).Media.Length, 1);
        Assert.AreEqual(user.RecentPosts.ElementAt(2).Media.First().MediaType, "video/mp4");
    }
    [TestMethod]
    public async Task user_lisam()
    {
        InstagramUser user;
        try
        {
            user = (InstagramUser)await _instaService.GetUserAsync("lisampresley");
        }
        catch (Exception _)
        {
            Assert.Inconclusive();
            return;
        }
        Assert.AreEqual(user.Description, "Official LMP Instagram!\nLisa Marie Presley is at heart a simple Southern girl. Singer, Songwriter, Philanthropist, Mother and Daughter of ‘the King’.");
        Assert.AreEqual(user.Name, "Lisa Marie Presley");
        Assert.AreEqual(user.Url, "https://people.com/music/lisa-marie-presley-was-destroyed-by-son-benjamins-death-grief-essay/");
        Assert.AreEqual(user.RecentPosts.First().Media.Length, 6);
    }
    [Ignore]
    [TestMethod]
    public async Task user_etymologynerd()
    {
        InstagramUser user;
        try
        {
            user = (InstagramUser)await _instaService.GetUserAsync("etymologynerd");
        }
        catch (Exception _)
        {
            Assert.Inconclusive();
            return;
        }
        Assert.IsTrue(user.RecentPosts.ToArray().Length > 1);
    }
}