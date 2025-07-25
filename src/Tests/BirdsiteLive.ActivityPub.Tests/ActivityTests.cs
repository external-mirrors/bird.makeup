using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace BirdsiteLive.ActivityPub.Tests
{
    [TestClass]
    public class ActivityTests
    {
        [TestMethod]
        public void Serialize()
        {
            var obj = new ActivityAcceptFollow()
            {
                context = "https://www.w3.org/ns/activitystreams",
                id = $"#accepts/follows/",
                to = ["https://mastodon.technology/users/testtest"],
                type = "Accept",
                actor = "actor",
                apObject = new ActivityFollow()
                {
                    id = "https://mastodon.technology/users/testtest3",
                    type = "Follow",
                    actor = "https://test.com",
                    apObject = "test"
                }
            };

            var json = JsonConvert.SerializeObject(obj);


        }
    }
}
