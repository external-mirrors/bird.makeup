#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8613, CS8618, CS8619, CS8620, CS8621, CS8625, CS8629, CS8631, CS8634
using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace BirdsiteLive.ActivityPub.Models
{
    // useful doc: https://humberto.io/blog/mastodon_poll_in_activitypub/
    public class Question : Note
    {
        public Question()
        {
           context = new object[] { "https://www.w3.org/ns/activitystreams", "https://w3id.org/security/v1", featuredContext};
           type = "Question";
        }
        private static Dictionary<string, object> featuredContext = new Dictionary<string, object>()
        {
            ["toot"] = "http://joinmastodon.org/ns#",
            ["votersCount"] = "toot:votersCount",
        };
        public long votersCount { get; set; }
        public string endTime { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string closed { get; set; } = null!;
        [JsonPropertyName("oneOf")]
        public QuestionAnswer[] answers { get; set; }
    }

    public class QuestionAnswer
    {
        public string type { get; } = "Note";
        public string name { get; set; } = null!;
        public Dictionary<string, object> replies { get; set; } = null!;
    }
}
