using BirdsiteLive.Instagram.Models;

namespace dotMakeup.Instagram.Models;

public interface IPostExtractor
{
    Task<InstagramPost> GetPostAsync(string statusId);
}