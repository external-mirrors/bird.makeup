using System.Text.Json.Serialization;

namespace BirdsiteLive.ActivityPub
{
    public class NestedActivity
    {
        [JsonPropertyName("@context")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object context { get; set; } = null!;
        public string id { get; set; } = null!;
        public string type { get; set; } = null!;
        public string actor { get; set; } = null!;

        [JsonPropertyName("object")]
        public string apObject { get; set; } = null!;
    }
}
