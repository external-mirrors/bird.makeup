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
        Assert.AreEqual(post.CreatedAt, new DateTime(2007, 04, 04, 19, 16, 40));
        Assert.IsNull(post.InReplyToStatusId);
        Assert.IsNull(post.Poll);
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
        Assert.AreEqual(post.CreatedAt, new DateTime(2008, 02, 22, 2, 33, 40));
        Assert.IsNull(post.InReplyToStatusId);
    }
    [TestMethod]
    public async Task Job1()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        var userService = new HnService(httpFactory.Object, null, _settings);
        var post = await userService.GetPostAsync("192327");
        
        Assert.AreEqual(post.Id, "192327");
        Assert.AreEqual(post.Author.Acct, "justin");
        Assert.AreEqual(post.MessageContent, "Justin.tv is looking for a Lead Flash Engineer!\n\nJustin.tv is the biggest live video site online. We serve hundreds of thousands of video streams a day, and have supported up to 50k live concurrent viewers. Our site is growing every week, and we just added a 10 gbps line to our colo. Our unique visitors are up 900% since January.<p>There are a lot of pieces that fit together to make Justin.tv work: our video cluster, IRC server, our web app, and our monitoring and search services, to name a few. A lot of our website is dependent on Flash, and we're looking for talented Flash Engineers who know AS2 and AS3 very well who want to be leaders in the development of our Flash.<p>Responsibilities<p><pre><code>    * Contribute to product design and implementation discussions\n    * Implement projects from the idea phase to production\n    * Test and iterate code before and after production release \n</code></pre>\nQualifications<p><pre><code>    * You should know AS2, AS3, and maybe a little be of Flex.\n    * Experience building web applications.\n    * A strong desire to work on website with passionate users and ideas for how to improve it.\n    * Experience hacking video streams, python, Twisted or rails all a plus.\n</code></pre>\nWhile we're growing rapidly, Justin.tv is still a small, technology focused company, built by hackers for hackers. Seven of our ten person team are engineers or designers. We believe in rapid development, and push out new code releases every week. We're based in a beautiful office in the SOMA district of SF, one block from the caltrain station. If you want a fun job hacking on code that will touch a lot of people, JTV is for you.<p>Note: You must be physically present in SF to work for JTV. Completing the technical problem at <a href=\"http://www.justin.tv/problems/bml\" rel=\"nofollow\">http://www.justin.tv/problems/bml</a> will go a long way with us. Cheers!\n\n");
        Assert.AreEqual(post.CreatedAt, new DateTime(2008, 05, 16, 23, 40, 17));
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
        Assert.AreEqual(post.CreatedAt, new DateTime(2011, 8, 24, 18, 38, 47));
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
    [TestMethod]
    public async Task Poll1()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(_ => _.CreateClient(string.Empty)).Returns(new HttpClient());
        var userService = new HnService(httpFactory.Object, null, _settings);
        var post = await userService.GetPostAsync("126809");
        
        Assert.AreEqual(post.Id, "126809");
        Assert.AreEqual(post.MessageContent, "Poll: What would happen if News.YC had explicit support for polls?");
        Assert.IsNotNull(post.Poll);
        Assert.AreEqual(post.Poll.options.Count, 3);
        Assert.AreEqual(post.Poll.options[2].First, "We'd have the same number of polls, but they wouldn't look as ugly.");
        Assert.IsTrue(post.Poll.options[2].Second > 170);
    }
    
}