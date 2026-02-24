using System;
using BirdsiteLive.Common.Interfaces;

namespace BirdsiteLive.Models
{
    public class DisplayPost
    {
        public string Text { get; set; }
        public string OriginalUrl { get; set; }
        public string AuthorProfileImage { get; set; }
        public string AuthorName { get; set; }
        public string AuthorHandle { get; set; }
        public string AuthorUrl { get; set; }
        public string ServiceName { get; set; }
        public DateTime CreatedAt { get; set; }
        public long LikeCount { get; set; }
        public long ShareCount { get; set; }
        public bool IsRepost { get; set; }
        public string RepostFromName { get; set; }
        public string RepostFromHandle { get; set; }
        public ExtractedMedia[] Media { get; set; } = Array.Empty<ExtractedMedia>();
        public DisplayPollOption[] PollOptions { get; set; } = Array.Empty<DisplayPollOption>();
    }

    public class DisplayPollOption
    {
        public string Label { get; set; }
        public long Votes { get; set; }
    }
}
