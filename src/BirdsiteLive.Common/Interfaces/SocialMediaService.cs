using System.Collections;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BirdsiteLive.Common.Interfaces;

public interface ISocialMediaService
{
        Task<SocialMediaUser> GetUserAsync(string username);
        Task<SocialMediaPost?> GetPostAsync(string id);
        Task<SocialMediaPost[]> GetNewPosts(SyncUser user);
        string MakeUserNameCanonical(string name);
        string ServiceName { get;  }
        SocialMediaUserDal UserDal { get; }
        Regex ValidUsername { get;  }
        Regex UserMention { get;  }
}