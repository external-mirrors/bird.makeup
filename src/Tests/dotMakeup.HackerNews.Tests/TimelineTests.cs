using System.Text.Json;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using Moq;

namespace dotMakeup.HackerNews.Tests;

[TestClass]
public class TimelineTests
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
    public async Task Frontpage()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        var userDal = new Mock<IHackerNewsUserDal>();
        var userService = new HnService(httpFactory.Object, userDal.Object, _settings);
        var posts = await userService.GetNewPosts( new SyncUser() { Acct = "frontpage", ExtraData = JsonDocument.Parse("{}").RootElement});
        
        Assert.AreEqual(posts.Length, 10);
        foreach (var p in posts)
        {
            Assert.IsNotNull(p.Author);
        }
    }
}