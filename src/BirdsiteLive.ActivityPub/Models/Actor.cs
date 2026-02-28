using System.Collections.Generic;
using System.Text.Json.Serialization;
using BirdsiteLive.ActivityPub.Converters;

namespace BirdsiteLive.ActivityPub
{
    public class Actor
    {
        [JsonPropertyName("@context")]
        public object[] context { get; set; } = new object[] { "https://www.w3.org/ns/activitystreams", "https://w3id.org/security/v1", featuredContext};
        public string id { get; set; } = null!;
        public string type { get; set; } = null!;
        public string followers { get; set; } = null!;
        public string outbox { get; set; } = null!;
        public string preferredUsername { get; set; } = null!;
        public string name { get; set; } = null!;
        public string summary { get; set; } = null!;
        public string url { get; set; } = null!;
        public bool manuallyApprovesFollowers { get; set; }
        public string inbox { get; set; } = null!;
        public bool? discoverable { get; set; } = true;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? featured { get; set; }
        public PublicKey publicKey { get; set; } = null!;
        public Image icon { get; set; } = null!;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(ArrayOrSingleConverter<Image>))]
        public Image image { get; set; } = null!;
        public EndPoints endpoints { get; set; } = null!;
        public UserAttachment[] attachment { get; set; } = null!;

        private static Dictionary<string, object> featuredContext = new Dictionary<string, object>()
        {
            ["toot"] = "http://joinmastodon.org/ns#",
            ["featured"] = new Dictionary<string, object>()
                { ["@id"] = "toot:featured", ["@type"] = "@id" },
            ["discoverable"] = "toot:discoverable",
        };
    }
}
