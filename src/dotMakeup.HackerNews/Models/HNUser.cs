using BirdsiteLive.Common.Interfaces;

namespace dotMakeup.HackerNews.Models;

public class HNUser : SocialMediaUser
{
    public long[] Posts { get; set; } = Array.Empty<long>();
}