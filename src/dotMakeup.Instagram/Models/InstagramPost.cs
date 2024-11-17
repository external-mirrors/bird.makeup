using BirdsiteLive.Common.Interfaces;

namespace BirdsiteLive.Instagram.Models
{
    public class InstagramPost : SocialMediaPost
    {
        public string Id { get; set; }
        public long? InReplyToStatusId { get; set; } = null;
        public string MessageContent { get; set; }
        public ExtractedMedia[] Media { get; set; }
        public DateTime CreatedAt { get; set; }
        public string InReplyToAccount { get; set; } = null;
        public bool IsRetweet { get; set; } = false;
        public bool IsPinned { get; set; } = false;
        public long RetweetId { get; set; }
        public SocialMediaUser OriginalAuthor { get; set; } = null;
        public SocialMediaUser Author { get; set; }
        public Poll? Poll { get; set; } = null;
    }
}