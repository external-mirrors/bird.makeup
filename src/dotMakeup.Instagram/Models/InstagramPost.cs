using BirdsiteLive.Common.Interfaces;
using Newtonsoft.Json;

namespace BirdsiteLive.Instagram.Models
{
    public class InstagramPost : SocialMediaPost
    {
        public string Id { get; set; }
        [JsonIgnore]
        public long? InReplyToStatusId { get; set; } = null;
        [JsonIgnore]
        public string QuotedStatusId { get; set; }
        public string MessageContent { get; set; }
        public ExtractedMedia[] Media { get; set; }
        [JsonIgnore]
        public string QuotedAccount { get; set; }
        public DateTime CreatedAt { get; set; }
        [JsonIgnore]
        public string InReplyToAccount { get; set; } = null;
        [JsonIgnore]
        public bool IsRetweet { get; set; } = false;
        public bool IsPinned { get; set; } = false;
        [JsonIgnore]
        public long RetweetId { get; set; }
        [JsonIgnore]
        public SocialMediaUser OriginalAuthor { get; set; } = null;
        public SocialMediaUser Author { get; set; }
        [JsonIgnore]
        public Poll? Poll { get; set; } = null;
        public long LikeCount { get; set; } = 0;
        public long ShareCount { get; set; } = 0;
    }
}