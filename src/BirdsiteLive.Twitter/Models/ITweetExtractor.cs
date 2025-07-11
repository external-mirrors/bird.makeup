using System.Threading.Tasks;

namespace BirdsiteLive.Twitter.Models;

public interface ITweetExtractor
{
    Task<ExtractedTweet> GetTweetAsync(long statusId);
}