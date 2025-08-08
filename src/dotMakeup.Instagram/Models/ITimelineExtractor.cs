using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Instagram.Models;

namespace dotMakeup.Instagram.Models;

public interface ITimelineExtractor
{
    Task<List<InstagramPost>> GetTimelineAsync(SyncUser user, long userId, long fromTweetId, bool withReplies);
}