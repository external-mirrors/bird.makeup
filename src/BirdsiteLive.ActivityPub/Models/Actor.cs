using System.Collections.Generic;
using System.Text.Json.Serialization;
using BirdsiteLive.ActivityPub.Converters;

namespace BirdsiteLive.ActivityPub
{
    public class Actor
    {
        [JsonPropertyName("@context")]
        public object[] context { get; set; } = new object[] { "https://www.w3.org/ns/activitystreams", "https://w3id.org/security/v1", featuredContext};
        public string id { get; set; }
        public string type { get; set; }
        public string followers { get; set; }
        public string outbox { get; set; }
        public string preferredUsername { get; set; }
        public string name { get; set; }
        public string summary { get; set; }
        public string url { get; set; }
        public bool manuallyApprovesFollowers { get; set; }
        public string inbox { get; set; }
        public bool? discoverable { get; set; } = true;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? featured { get; set; }
        public PublicKey publicKey { get; set; }
        public Image icon { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(ArrayOrSingleConverter<Image>))]
        public Image image { get; set; }
        public EndPoints endpoints { get; set; }
        public UserAttachment[] attachment { get; set; }

        private static Dictionary<string, object> featuredContext = new Dictionary<string, object>()
        {
            ["toot"] = "http://joinmastodon.org/ns#",
            ["featured"] = new Dictionary<string, object>()
                { ["@id"] = "toot:featured", ["@type"] = "@id" },
            ["discoverable"] = "toot:discoverable",
        };
    }
}
