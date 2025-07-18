using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace BirdsiteLive.ActivityPub.Models
{
    // https://join-lemmy.org/docs/contributors/05-federation.html
    public class Page : Note
    {
        [JsonPropertyName("@context")]
        public new object[] context { get; set; } = new object[] { "https://www.w3.org/ns/activitystreams", "https://join-lemmy.org/context.jso", };
        public new string type { get; } = "Page";
        public string mediaType { get; set; } = "text/html";
        public string name { get; set; } 
    }

}