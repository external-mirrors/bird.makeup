using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Models;

namespace BirdsiteLive.Pipeline.Models
{
    public class UserWithDataToSync
    {
        public SyncUser User { get; set; } = null!;
        public SocialMediaPost[] Tweets { get; set; } = null!;
        public Follower[] Followers { get; set; } = null!;
    }
}
