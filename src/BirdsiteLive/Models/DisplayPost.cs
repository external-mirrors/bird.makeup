using System;
using BirdsiteLive.Common.Interfaces;

namespace BirdsiteLive.Models
{
    public class DisplayPost
    {
        public string Text { get; set; } = null!;
        public string OriginalUrl { get; set; } = null!;
        public string AuthorProfileImage { get; set; } = null!;
        public string AuthorName { get; set; } = null!;
        public string AuthorHandle { get; set; } = null!;
        public string AuthorUrl { get; set; } = null!;
        public string ServiceName { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public long LikeCount { get; set; }
        public long ShareCount { get; set; }
        public bool IsRepost { get; set; }
        public string RepostFromName { get; set; } = null!;
        public string RepostFromHandle { get; set; } = null!;
        public ExtractedMedia[] Media { get; set; } = Array.Empty<ExtractedMedia>();
        public DisplayPollOption[] PollOptions { get; set; } = Array.Empty<DisplayPollOption>();
    }

    public class DisplayPollOption
    {
        public string Label { get; set; } = null!;
        public long Votes { get; set; }
    }
}
