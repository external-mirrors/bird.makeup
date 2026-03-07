using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BirdsiteLive.ActivityPub.Models
{
    public class OrderedCollection : Collection
    {
        public OrderedCollection()
        {
            type = "OrderedCollection";
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string attributedTo { get; set; } = null!;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> items { get; set; } = null!;
    }
}
