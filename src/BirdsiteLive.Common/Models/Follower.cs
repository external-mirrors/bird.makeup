using System.Collections.Generic;

namespace BirdsiteLive.Common.Models
{
    public class Follower
    {
        public int Id { get; set; }
        
        public List<int> Followings { get; set; } = null!;
        public int TotalFollowings { get; set; }

        public string ActorId { get; set; } = null!;
        public string Acct { get; set; } = null!;
        public string Host { get; set; } = null!;
        public string InboxRoute { get; set; } = null!;
        public string SharedInboxRoute { get; set; } = null!;

        public int PostingErrorCount { get; set; }
    }
}
