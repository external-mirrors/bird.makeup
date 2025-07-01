using System.Collections.Generic;
using BirdsiteLive.ActivityPub.Converters;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace BirdsiteLive.ActivityPub.Models
{
    public class QuoteAuthorization
    {
        private static Dictionary<string, object> extraContext = new Dictionary<string, object>()
        {
            ["toot"] = "http://joinmastodon.org/ns#",
            ["QuoteAuthorization"] = "toot:QuoteAuthorization",
            ["gts"] = "https://gotosocial.org/ns#",
            ["interactingObject"] = new Dictionary<string, object>()
                { ["@id"] = "gts:interactingObject", ["@type"] = "@id" },
            ["interactionTarget"] = new Dictionary<string, object>()
            { ["@id"] = "gts:interactionTarget", ["@type"] = "@id" },
        };
        [JsonPropertyName("@context")]
        public new object[] context { get; set; } = new object[] { "https://www.w3.org/ns/activitystreams", extraContext};
        public string id { get; set; }
        public string type { get; set; } = "QuoteAuthorization";
        public string attributedTo { get; set; }
        public string interactingObject { get; set; }
        public string interactionTarget { get; set; }
    }
}