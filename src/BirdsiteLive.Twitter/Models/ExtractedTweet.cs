using System;
using System.Collections.Generic;
using System.Net.Sockets;
using BirdsiteLive.Common.Interfaces;

namespace BirdsiteLive.Twitter.Models
{
    public class ExtractedTweet : SocialMediaPost
    {
        public string Id { get; set; } = null!;
        public long IdLong
        {
            get => long.Parse(Id);
        }
        public long? InReplyToStatusId { get; set; }
        public string QuotedStatusId { get; set; } = null!;
        public string MessageContent { get; set; } = null!;
        public ExtractedMedia[] Media { get; set; } = null!;
        public string QuotedAccount { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string InReplyToAccount { get; set; } = null!;
        public bool IsReply { get; set; }
        public bool IsThread { get; set; }
        public bool IsRetweet { get; set; }
        public string RetweetUrl { get; set; } = null!;
        public long RetweetId { get; set; }
        public SocialMediaUser OriginalAuthor { get; set; } = null!;
        public SocialMediaUser Author { get; set; } = null!;
        public Poll? Poll { get; set; }
        public long LikeCount { get; set; } = 0;
        public long ShareCount { get; set; } = 0;
    }

}
