using System.Text.Json.Serialization;

namespace BirdsiteLive.ActivityPub.Models
{
    public class Tag {
        public string type { get; set; } = null!; //Hashtag
        public string href { get; set; } = null!; //https://mastodon.social/tags/app
        public string name { get; set; } = null!; //#app
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string rel { get; set; } = null!;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string mediaType { get; set; } = null!;
    }
}
