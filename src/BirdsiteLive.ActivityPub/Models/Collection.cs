using BirdsiteLive.ActivityPub.Converters;
using System.Text.Json.Serialization;

namespace BirdsiteLive.ActivityPub.Models
{
    public class Collection : Activity
    {
        public Collection()
        {
            context = "https://www.w3.org/ns/activitystreams";
            type = "Collection";
        }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? totalItems { get; set; }
    }
}