using dotMakeup.HackerNews;
using System.Threading.Tasks;
using BirdsiteLive.Common.Settings;
using Moq;

namespace dotMakeup.HackerNews.Tests;

[TestClass]
public class UsersTests
{
    private InstanceSettings _settings = new InstanceSettings();
    
    [TestMethod]
    public async Task TestMethod1()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        var userService = new HnService(httpFactory.Object, null, _settings);
        var user = await userService.GetUserAsync("dhouston");
        
        Assert.AreEqual(user.Description, "Founder/CEO of Dropbox (http://www.dropbox.com ; yc summer '07)");
    }
}