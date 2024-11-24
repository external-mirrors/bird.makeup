using System;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.DAL.Models;

namespace BirdsiteLive.DAL.Contracts
{
    public interface ITwitterUserDal : SocialMediaUserDal
    {
        Task<SyncTwitterUser[]> GetAllTwitterUsersWithFollowersAsync(int maxNumber, int nStart, int nEnd, int m);
        Task<SyncTwitterUser[]> GetAllTwitterUsersAsync();
        Task<CrawlAccount[]> GetTwitterCrawlUsersAsync(string worker);
        Task UpdateTwitterUserAsync(int id, long lastTweetPostedId, int fetchingErrorCount, DateTime lastSync);
        Task UpdateTwitterUserIdAsync(string username, long twitterUserId);
        Task UpdateTwitterStatusesCountAsync(string username, long StatusesCount);
        Task<int> GetTwitterUsersCountAsync();
        Task<TimeSpan> GetTwitterSyncLag();
        Task<int> GetFailingTwitterUsersCountAsync();
    }
}