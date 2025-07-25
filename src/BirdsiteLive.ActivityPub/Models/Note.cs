using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace BirdsiteLive.ActivityPub.Models
{
    public class Note : Activity
    {
        public Note()
        {
            context  = new object[] { "https://www.w3.org/ns/activitystreams", extraContext};
            type = "Note";
        }
        private static Dictionary<string, object> extraContext = new Dictionary<string, object>()
        {
            ["quoteUrl"] = "as:quoteUrl",
            ["toot"] = "http://joinmastodon.org/ns#",
            ["quote"] = new Dictionary<string, object>()
                { ["@id"] = "https://w3id.org/fep/044f#quote", ["@type"] = "@id" },
            ["quoteAuthorization"] = new Dictionary<string, object>()
                { ["@id"] = "https://w3id.org/fep/044f#quoteAuthorization", ["@type"] = "@id" },
        };

        public string announceId { get; set; }
        public string summary { get; set; }
        public string inReplyTo { get; set; }
        public string published { get; set; }
        public string url { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string quoteUrl { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string quote { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string quoteAuthorization { get; set; }
        public string attributedTo { get; set; }
        public string[] cc { get; set; }
        public bool sensitive { get; set; }
        //public string conversation { get; set; }
        public string content { get; set; }
        //public Dictionary<string,string> contentMap { get; set; }
        public Attachment[] attachment { get; set; }
        public Tag[] tag { get; set; }
        //public Dictionary<string, string> replies;
    }
}