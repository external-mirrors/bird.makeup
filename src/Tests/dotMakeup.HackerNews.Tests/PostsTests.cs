using BirdsiteLive.Common.Settings;
using Moq;

namespace dotMakeup.HackerNews.Tests;

[TestClass]
public class PostsTests
{
    private InstanceSettings _settings = new InstanceSettings();
    [TestMethod]
    public async Task Story1()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        var userService = new HnService(httpFactory.Object, null, _settings);
        var post = await userService.GetPostAsync("8863");
        
        Assert.AreEqual(post.Id, "8863");
        Assert.AreEqual(post.Author.Acct, "dhouston");
        Assert.AreEqual(post.MessageContent, "My YC app: Dropbox - Throw away your USB drive\n\nhttp://www.getdropbox.com/u/2/screencast.html");
        Assert.IsNull(post.InReplyToStatusId);
    }
    [TestMethod]
    public async Task Story2()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        var userService = new HnService(httpFactory.Object, null, _settings);
        var post = await userService.GetPostAsync("121003");
        
        Assert.AreEqual(post.Id, "121003");
        Assert.AreEqual(post.Author.Acct, "tel");
        Assert.AreEqual(post.MessageContent, "Ask HN: The Arc Effect\n\n<i>or</i> HN: the Next Iteration<p>I get the impression that with Arc being released a lot of people who never had time for HN before are suddenly dropping in more often. (PG: what are the numbers on this? I'm envisioning a spike.)<p>Not to say that isn't great, but I'm wary of Diggification. Between links comparing programming to sex and a flurry of gratuitous, ostentatious  adjectives in the headlines it's a bit concerning.<p>80% of the stuff that makes the front page is still pretty awesome, but what's in place to keep the signal/noise ratio high? Does the HN model still work as the community scales? What's in store for (++ HN)?");
        Assert.IsNull(post.InReplyToStatusId);
    }
    [TestMethod]
    public async Task Comment1()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        var userService = new HnService(httpFactory.Object, null, _settings);
        var post = await userService.GetPostAsync("2921983");
        
        Assert.AreEqual(post.Id, "2921983");
        Assert.AreEqual(post.Author.Acct, "norvig");
        Assert.AreEqual(post.InReplyToStatusId, 2921506);
        Assert.AreEqual(post.InReplyToAccount, "mayoff");
        Assert.AreEqual(post.MessageContent, "Aw shucks, guys ... you make me blush with your compliments.<p>Tell you what, Ill make a deal: I'll keep writing if you keep reading. K?");
    }
    [TestMethod]
    public async Task Comment2()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        var userService = new HnService(httpFactory.Object, null, _settings);
        var post = await userService.GetPostAsync("2921983");
        
        Assert.AreEqual(post.Id, "2921983");
        Assert.AreEqual(post.MessageContent, "Aw shucks, guys ... you make me blush with your compliments.<p>Tell you what, Ill make a deal: I'll keep writing if you keep reading. K?");
    }
    
}