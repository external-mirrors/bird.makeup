using System.Text.Json.Serialization;

namespace BirdsiteLive.ActivityPub
{
    public class ActivityLike : Activity
    {
        [JsonPropertyName("object")]
        public string apObject { get; set; }
    }
}