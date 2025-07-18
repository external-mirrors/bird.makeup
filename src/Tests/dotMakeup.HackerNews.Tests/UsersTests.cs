using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using dotMakeup.HackerNews;
using System.Threading.Tasks;
using BirdsiteLive.Common.Exceptions;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using Moq;

namespace dotMakeup.HackerNews.Tests;

[TestClass]
public class UsersTests
{
    private InstanceSettings _settings = new InstanceSettings();
    
    [TestMethod]
    public async Task frontpage_new_posts()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        var dal = new Mock<IHackerNewsUserDal>();
        var userService = new HnService(httpFactory.Object, dal.Object, _settings);
        SyncUser user = new SyncUser() { Acct = "frontpage", ExtraData = JsonDocument.Parse("{\"latest_post_date\": \"2012-01-01T12:12:12\"}").RootElement };
        var posts = await userService.GetNewPosts(user);
        
        Assert.AreEqual(posts.Length, 10);
        Assert.IsTrue(posts.Last().CreatedAt > new DateTime(2012, 1, 1, 12, 0, 0) );
    }
    [TestMethod]
    public async Task User_new_posts()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        var dal = new Mock<IHackerNewsUserDal>();
        //dal.Setup(_ => _.GetUserAsync("aaronsw")).Returns(new SyncUser() {})
        var userService = new HnService(httpFactory.Object, dal.Object, _settings);
        SyncUser user = new SyncUser() { Acct = "aaronsw", ExtraData = JsonDocument.Parse("{\"latest_post_date\": \"2012-01-01T12:12:12\"}").RootElement };
        var posts = await userService.GetNewPosts(user);
        
        Assert.AreEqual(posts.Length, 18);
        Assert.AreEqual(posts[0].MessageContent, "<a href=\"https://github.com/Aaronius/Stupid-Table-Plugin/commit/fbf3dcab2ec8e4381529dc23b0e2727bbac1d18b?w=1\" rel=\"nofollow\">https://github.com/Aaronius/Stupid-Table-Plugin/commit/fbf3d...</a> people.");
        Assert.IsTrue(posts.Last().CreatedAt > new DateTime(2012, 1, 1, 12, 0, 0) );
    }
    [TestMethod]
    public async Task User_marcan_42()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        var userService = new HnService(httpFactory.Object, null, _settings);
        var user = await userService.GetUserAsync("marcan_42");
        
        Assert.AreEqual(user.Description, "");
    }
    [TestMethod]
    public async Task User_gargron()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        var userService = new HnService(httpFactory.Object, null, _settings);
        var user = await userService.GetUserAsync("gargron");
        
        Assert.AreEqual(user.Description, "");
    }
    [TestMethod]
    public async Task User_dhouston()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        var userService = new HnService(httpFactory.Object, null, _settings);
        var user = await userService.GetUserAsync("dhouston");
        
        Assert.AreEqual(user.Description, "Founder/CEO of Dropbox (http://www.dropbox.com ; yc summer '07)");
    }
    [Ignore]
    [TestMethod]
    [ExpectedException(typeof(UserNotFoundException))]
    public async Task User_missing()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        var userService = new HnService(httpFactory.Object, null, _settings);
        
        await userService.GetUserAsync("sdhciuh38dhuh");
    }
}