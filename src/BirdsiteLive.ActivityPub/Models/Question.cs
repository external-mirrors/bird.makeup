using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace BirdsiteLive.ActivityPub.Models
{
    // useful doc: https://humberto.io/blog/mastodon_poll_in_activitypub/
    public class Question : Note
    {
        private static Dictionary<string, object> featuredContext = new Dictionary<string, object>()
        {
            ["toot"] = "http://joinmastodon.org/ns#",
            ["votersCount"] = "toot:votersCount",
            ["gts"] = "https://gotosocial.org/ns#",
            ["approvedBy"] = new Dictionary<string, object>()
                { ["@id"] = "gts:approvedBy", ["@type"] = "@id" },
        };
        [JsonPropertyName("@context")]
        public new object[] context { get; set; } = new object[] { "https://www.w3.org/ns/activitystreams", "https://w3id.org/security/v1", featuredContext};
        public new string type { get; } = "Question";
        public long votersCount { get; set; }
        public string endTime { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string closed { get; set; } = null;
        [JsonPropertyName("oneOf")]
        public QuestionAnswer[] answers { get; set; }
    }

    public class QuestionAnswer
    {
        public string type { get; } = "Note";
        public string name { get; set; }
        public Dictionary<string, object> replies { get; set; }
    }
}