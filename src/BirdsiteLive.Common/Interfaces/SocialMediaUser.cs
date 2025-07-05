using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BirdsiteLive.Common.Interfaces;

public enum SocialMediaUserTypes
{
        User,
        Group
}
public class SocialMediaUser
{
        public SocialMediaUserTypes SocialMediaUserType { get; set; }
        public long Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string Acct { get; set; }
        public string ProfileUrl { get; set; }
        public string ProfileImageUrl { get; set; }
        public string ProfileBannerURL{ get; set; }
        public bool Protected { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public IEnumerable<string> PinnedPosts { get; set; } = [];
}