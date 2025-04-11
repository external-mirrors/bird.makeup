using System.Text.Json.Serialization;

namespace BirdsiteLive.ActivityPub
{
    public class ActivityFlag : Activity
    {
        [JsonPropertyName("object")]
        public string[] apObject { get; set; }
    }
}