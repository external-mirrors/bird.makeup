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
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        var settings = new InstanceSettings
        {
            Domain = "domain.name",
            SidecarURL = "http://localhost:5001"
        };

        _ipfsService = new DotmakeupIpfs(settings, httpFactory.Object);
        _instaService = new InstagramService(_ipfsService, userDal.Object, httpFactory.Object, settings, settingsDal.Object);
    }
    [TestMethod]
    public async Task user_kobe()
    {
        SocialMediaUser user;
        try
        {
            user = await _instaService.GetUserAsync("kobebryant");
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
    }
    [TestMethod]
    public async Task user_virgil()
    {
        SocialMediaUser user;
        try
        {
            user = await _instaService.GetUserAsync("virgilabloh");
        }
        catch (Exception _)
        {
            Assert.Inconclusive();
            return;
        }
        Assert.AreEqual(user.Description, "a 𝚜𝚎𝚖𝚒-𝚌𝚑𝚛𝚘𝚗𝚘𝚕𝚘𝚐𝚒𝚌𝚊𝚕 𝚍𝚘𝚌𝚞𝚖𝚎𝚗𝚝 𝚘𝚏 𝚒𝚍𝚎𝚊𝚜.");
        Assert.AreEqual(user.Name, "");
        Assert.AreEqual(user.Url, "http://virgilabloh.com/land_i_own/");
    }
    [TestMethod]
    public async Task user_lisam()
    {
        SocialMediaUser user;
        try
        {
            user = await _instaService.GetUserAsync("lisampresley");
        }
        catch (Exception _)
        {
            Assert.Inconclusive();
            return;
        }
        Assert.AreEqual(user.Description, "Official LMP Instagram!\nLisa Marie Presley is at heart a simple Southern girl. Singer, Songwriter, Philanthropist, Mother and Daughter of ‘the King’.");
        Assert.AreEqual(user.Name, "Lisa Marie Presley");
        Assert.AreEqual(user.Url, "https://people.com/music/lisa-marie-presley-was-destroyed-by-son-benjamins-death-grief-essay/");
    }
}