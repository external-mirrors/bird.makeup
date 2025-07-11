using System.Collections.Generic;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;

namespace BirdsiteLive.Twitter.Models;

public interface ITimelineExtractor
{
    Task<List<ExtractedTweet>> GetTimelineAsync(SyncUser user, long userId, long fromTweetId, bool withReplies);
}