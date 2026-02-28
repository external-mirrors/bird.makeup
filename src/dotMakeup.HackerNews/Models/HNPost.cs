using BirdsiteLive.Common.Interfaces;

namespace dotMakeup.HackerNews.Models;

public class HNPost : SocialMediaPost
{
    public string Id { get; set; } = null!;
    public SocialMediaUser Author { get; set; } = null!;
    public SocialMediaUser OriginalAuthor { get; set; } = null!;
    public string MessageContent { get; set; } = null!;
    public bool IsRetweet { get; set; }
    public long RetweetId { get; set; }
    public long? InReplyToStatusId { get; set; }
    public string QuotedStatusId { get; set; } = null!;
    public string InReplyToAccount { get; set; } = null!;
    public string QuotedAccount { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public ExtractedMedia[] Media { get; set; } = null!;
    public Poll? Poll { get; set; }
    public long? Score { get; set; }
    public long LikeCount { get; set; } = 0;
    public long ShareCount { get; set; } = 0;
}
