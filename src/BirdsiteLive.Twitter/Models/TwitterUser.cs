using BirdsiteLive.Common.Interfaces;

namespace BirdsiteLive.Twitter.Models
{
    public class TwitterUser : SocialMediaUser
    {
        
        public int StatusCount { get; set; }
        public int FollowersCount { get; set; }
    }
}