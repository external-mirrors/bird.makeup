using BirdsiteLive.Common.Interfaces;

namespace dotMakeup.HackerNews.Models;

public class HNUser : SocialMediaUser
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public string Acct { get; set; }
    public string ProfileUrl { get; set; }
    public string ProfileImageUrl { get; set; }
    public string ProfileBannerURL { get; set; }
    public bool Protected { get; set; }
    public string Description { get; set; }
    public string Location { get; set; }
    public IEnumerable<string> PinnedPosts { get; set; } = Array.Empty<string>();
}