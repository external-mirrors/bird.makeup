using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BirdsiteLive.Common.Models;

namespace BirdsiteLive.Common.Interfaces;

public interface SocialMediaUserDal
{ 
        Task<SyncUser> GetUserAsync(string acct);
        Task<SyncUser[]> GetAllUsersAsync();
        Task<string?> GetUserCacheAsync(string acct);
        Task UpdatePostCacheAsync(SocialMediaPost post);
        Task<string> GetPostCacheAsync(string post);
        Task UpdateUserCacheAsync(SocialMediaUser user);
        Task UpdateUserLastSyncAsync(SyncUser user);
        Task<SyncUser> GetUserAsync(int acct);
        Task<SyncUser[]> GetNextUsersToCrawlAsync(int nStart, int nEnd, int m);
        Task DeleteUserAsync(int id);
        Task DeleteUserAsync(string acct);
        Task CreateUserAsync(string acct);
        Task AddFollower(int follower, int followed);
        Task RemoveFollower(int follower, int followed);
        Task<long> GetFollowersCountAsync(int id);
        Task<Follower[]> GetFollowersAsync(int id);
        Task UpdateUserExtradataAsync(string username, string key, string subkey, object value);
        Task UpdateUserExtradataAsync(string username, string key, object value);
        Task UpdateUserWikidataAsync(string username, object value);
        Task<string> GetUserWikidataAsync(string username);
        Task<string> GetUserExtradataAsync(string username, string key);
}

public class SyncUser
{
        public int Id { get; set; }
        public long TwitterUserId { get; set; }
        public string Acct { get; set; }
        public string FediAcct { get; set; }

        public long LastTweetPostedId { get; set; }

        public DateTime LastSync { get; set; }

        public int FetchingErrorCount { get; set; } //TODO: update DAL
        public long Followers { get; set; } 
        public int StatusesCount { get; set; }
        public JsonElement ExtraData { get; set; }
        public string PreDescriptionHook
        {
            get
            {
                JsonElement hooks;
                if (!ExtraData.TryGetProperty("hooks", out hooks))
                    return "";

                JsonElement preDesc;
                if (!hooks.TryGetProperty("preDescription", out preDesc))
                    return "";

                return preDesc.GetString();
            }
        }
        public string PostDescriptionHook 
        {
            get
            {
                JsonElement hooks;
                if(!ExtraData.TryGetProperty("hooks", out hooks))
                    return "";
                
                JsonElement preDesc;
                if(!hooks.TryGetProperty("postDescription", out preDesc))
                    return "";

                return preDesc.GetString();
            }
        }
        public (string, string)[] AdditionnalAttachments 
        {
            get
            {
                JsonElement hooks;
                if(!ExtraData.TryGetProperty("hooks", out hooks))
                    return Array.Empty<(string, string)>();
                
                JsonElement attachements;
                if(!hooks.TryGetProperty("addAttachments", out attachements))
                    return Array.Empty<(string, string)>();

                var finalAtt = new List<(string, string)>();
                foreach (JsonProperty att in attachements.EnumerateObject())
                {
                    finalAtt = finalAtt.Append((att.Name, att.Value.GetString())).ToList();
                }
                return finalAtt.ToArray();
            }
        }
        public string DisclaimerReplacementHook
        {
            get
            {
                JsonElement hooks;
                if(!ExtraData.TryGetProperty("hooks", out hooks))
                    return "";
                
                JsonElement preDesc;
                if(!hooks.TryGetProperty("disclaimerReplacement", out preDesc))
                    return "";

                return preDesc.GetString();
            }
        }
}