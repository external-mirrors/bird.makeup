using System.Diagnostics.CodeAnalysis;
using BirdsiteLive.ActivityPub.Models;
using System.Text.Json.Serialization;

namespace BirdsiteLive.ActivityPub
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class ActivityCreateNote : Activity
    {
        public string published { get; set; }
        public string[] cc { get; set; }

        [JsonPropertyName("object")]
        public Note apObject { get; set; }
    }
}