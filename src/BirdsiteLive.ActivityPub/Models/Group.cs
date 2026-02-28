using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BirdsiteLive.ActivityPub
{
    public class Group
    {
        [JsonPropertyName("@context")]
        public object[] context { get; set; } = new object[] { "https://www.w3.org/ns/activitystreams", "https://w3id.org/security/v1"};
        public string id { get; set; } = null!;
        public string type { get; set; } = "Group";
        public string followers { get; set; } = null!;
        public string outbox { get; set; } = null!;
        public string preferredUsername { get; set; } = null!;
        public string name { get; set; } = null!;
        public string summary { get; set; } = null!;
        public string url { get; set; } = null!;
        public string inbox { get; set; } = null!;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? featured { get; set; }
        public PublicKey publicKey { get; set; } = null!;
        public Image icon { get; set; } = null!;
        public Image image { get; set; } = null!;
        public EndPoints endpoints { get; set; } = null!;
        public bool postingRestrictedToMods { get; set; } = true;
        public UserAttachment[] attachment { get; set; } = null!;
    }
}
