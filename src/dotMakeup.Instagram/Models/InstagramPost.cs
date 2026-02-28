using BirdsiteLive.Common.Interfaces;
using Newtonsoft.Json;

namespace BirdsiteLive.Instagram.Models
{
    public class InstagramPost : SocialMediaPost
    {
        public string Id { get; set; } = null!;
        [JsonIgnore]
        public long? InReplyToStatusId { get; set; } = null;
        [JsonIgnore]
        public string QuotedStatusId { get; set; } = null!;
        public string MessageContent { get; set; } = null!;
        public ExtractedMedia[] Media { get; set; } = null!;
        [JsonIgnore]
        public string QuotedAccount { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        [JsonIgnore]
        public string InReplyToAccount { get; set; } = null!;
        [JsonIgnore]
        public bool IsRetweet { get; set; } = false;
        public bool IsPinned { get; set; } = false;
        [JsonIgnore]
        public long RetweetId { get; set; }
        [JsonIgnore]
        public SocialMediaUser OriginalAuthor { get; set; } = null!;
        public SocialMediaUser Author { get; set; } = null!;
        [JsonIgnore]
        public Poll? Poll { get; set; } = null;
        public long LikeCount { get; set; } = 0;
        public long ShareCount { get; set; } = 0;
    }
}
