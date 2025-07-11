using System.Threading.Tasks;

namespace BirdsiteLive.Twitter.Models;

public interface IUserExtractor
{
    Task<TwitterUser> GetUserAsync(string username);

}