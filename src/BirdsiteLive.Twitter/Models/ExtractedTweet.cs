using System;
using System.Collections.Generic;
using System.Net.Sockets;
using BirdsiteLive.Common.Interfaces;

namespace BirdsiteLive.Twitter.Models
{
    public class ExtractedTweet : SocialMediaPost
    {
        public string Id { get; set; }
        public long IdLong
        {
            get => long.Parse(Id);
        }
        public long? InReplyToStatusId { get; set; }
        public string QuotedStatusId { get; set; }
        public string MessageContent { get; set; }
        public ExtractedMedia[] Media { get; set; }
        public string QuotedAccount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string InReplyToAccount { get; set; }
        public bool IsReply { get; set; }
        public bool IsThread { get; set; }
        public bool IsRetweet { get; set; }
        public string RetweetUrl { get; set; }
        public long RetweetId { get; set; }
        public SocialMediaUser OriginalAuthor { get; set; }
        public SocialMediaUser Author { get; set; }
        public Poll? Poll { get; set; }
    }

}