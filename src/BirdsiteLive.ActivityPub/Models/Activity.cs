using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using BirdsiteLive.ActivityPub.Converters;

namespace BirdsiteLive.ActivityPub
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Activity
    {
        [JsonIgnore]
        public object context { get; set; }
        // Avoids deserialization of @context, as it may be a JSON object instead of a string (e.g., on Firefish)
        [JsonPropertyName("@context")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        //[JsonPropertyOrder(1)]
        public object SerializedContext => context;
        public string id { get; set; }
        //[JsonPropertyOrder(2)]
        public string type { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string actor { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> to { get; set; }


        //[JsonPropertyName("object")]
        //public string apObject { get; set; }
    }
}