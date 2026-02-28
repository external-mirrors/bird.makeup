using System.Text.Json;
using System.Text.Json.Serialization;

namespace BirdsiteLive.ActivityPub
{
    public class Attachment
    {
        public string type { get; set; } = null!;
        public string mediaType { get; set; } = null!;
        public string url { get; set; } = null!;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string name { get; set; } = null!;
    }
}
