using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Models;

namespace BirdsiteLive.Pipeline.Models
{
    public class UserWithDataToSync
    {
        public SyncUser User { get; set; }
        public SocialMediaPost[] Tweets { get; set; }
        public Follower[] Followers { get; set; }
    }
}