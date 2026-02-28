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
            ["QuoteAuthorization"] = "https://w3id.org/fep/044f#QuoteAuthorization",
            ["gts"] = "https://gotosocial.org/ns#",
            ["interactingObject"] = new Dictionary<string, object>()
                { ["@id"] = "gts:interactingObject", ["@type"] = "@id" },
            ["interactionTarget"] = new Dictionary<string, object>()
            { ["@id"] = "gts:interactionTarget", ["@type"] = "@id" },
        };
        [JsonPropertyName("@context")]
        public object[] context { get; set; } = new object[] { "https://www.w3.org/ns/activitystreams", extraContext};
        public string id { get; set; } = null!;
        public string type { get; set; } = "QuoteAuthorization";
        public string attributedTo { get; set; } = null!;
        public string interactingObject { get; set; } = null!;
        public string interactionTarget { get; set; } = null!;
    }
}
