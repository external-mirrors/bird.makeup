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
        public string Name { get; set; } = null!;
        public string Url { get; set; } = null!;
        public string Acct { get; set; } = null!;
        public string ProfileUrl { get; set; } = null!;
        public string ProfileImageUrl { get; set; } = null!;
        public string ProfileBannerURL{ get; set; } = null!;
        public bool Protected { get; set; }
        public string Description { get; set; } = null!;
        public string Location { get; set; } = null!;
        public IEnumerable<string> PinnedPosts { get; set; } = [];
}
