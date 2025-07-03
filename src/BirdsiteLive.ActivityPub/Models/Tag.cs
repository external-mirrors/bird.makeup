using System.Text.Json.Serialization;

namespace BirdsiteLive.ActivityPub.Models
{
    public class Tag {
        public string type { get; set; } //Hashtag
        public string href { get; set; } //https://mastodon.social/tags/app
        public string name { get; set; } //#app
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string rel { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string mediaType { get; set; }
    }
}