namespace dotMakeup.Instagram.Models;

public interface IUserExtractor
{
    Task<InstagramUser> GetUserAsync(string username);

}