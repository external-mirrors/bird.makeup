using System.Collections.Generic;
using System.Threading.Tasks;
using BirdsiteLive.Common.Models;
using BirdsiteLive.DAL.Models;

namespace BirdsiteLive.DAL.Contracts
{
    public interface IFollowersDal
    {
        Task<Follower> GetFollowerAsync(string acct, string host);
        Task CreateFollowerAsync(string acct, string host, string inboxRoute, string sharedInboxRoute, string actorId, int[] followings = null!);
        Task<Follower[]> GetAllFollowersAsync();
        Task UpdateFollowerErrorCountAsync(int followerId, int count);
        Task DeleteFollowerAsync(int id);
        Task DeleteFollowerAsync(string acct, string host);
        Task<int> GetFollowersCountAsync();
        Task<int> GetFailingFollowersCountAsync();
    }
}
