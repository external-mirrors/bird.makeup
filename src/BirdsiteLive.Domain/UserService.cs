using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BirdsiteLive.ActivityPub;
using BirdsiteLive.ActivityPub.Converters;
using BirdsiteLive.ActivityPub.Models;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Domain.BusinessUseCases;
using BirdsiteLive.Domain.Repository;
using BirdsiteLive.Domain.Tools;

namespace BirdsiteLive.Domain
{
    public interface IUserService
    {
        Task<string> GetUser(SocialMediaUser twitterUser);
        Task<bool> FollowRequestedAsync(string signature, string method, string path, string queryString, Dictionary<string, string> requestHeaders, ActivityFollow activity, string body);
        Task<bool> UndoFollowRequestedAsync(string signature, string method, string path, string queryString, Dictionary<string, string> requestHeaders, ActivityUndoFollow activity, string body);

        Task<bool> SendRejectFollowAsync(ActivityFollow activity, string followerHost);
        Task<bool> DeleteRequestedAsync(string signature, string method, string path, string queryString, Dictionary<string, string> requestHeaders, ActivityDelete activity, string body);
    }

    public class UserService : IUserService
    {
        private readonly IProcessDeleteUser _processDeleteUser;
        private readonly IProcessFollowUser _processFollowUser;
        private readonly IProcessUndoFollowUser _processUndoFollowUser;

        private readonly InstanceSettings _instanceSettings;
        private readonly ICryptoService _cryptoService;
        private readonly IActivityPubService _activityPubService;
        private readonly IStatusExtractor _statusExtractor;

        private readonly ISocialMediaService _socialMediaService;

        private readonly IModerationRepository _moderationRepository;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

        #region Ctor
        public UserService(InstanceSettings instanceSettings, ICryptoService cryptoService, IActivityPubService activityPubService, IProcessFollowUser processFollowUser, IProcessUndoFollowUser processUndoFollowUser, IStatusExtractor statusExtractor, IModerationRepository moderationRepository, IProcessDeleteUser processDeleteUser, ITwitterUserDal twitterUserDal, ISocialMediaService socialMediaService)
        {
            _instanceSettings = instanceSettings;
            _cryptoService = cryptoService;
            _activityPubService = activityPubService;
            _processFollowUser = processFollowUser;
            _processUndoFollowUser = processUndoFollowUser;
            _statusExtractor = statusExtractor;
            _moderationRepository = moderationRepository;
            _processDeleteUser = processDeleteUser;
            _socialMediaService = socialMediaService;
        }
        #endregion

        public async Task<string> GetUser(SocialMediaUser twitterUser)
        {
            var actorUrl = UrlFactory.GetActorUrl(_instanceSettings.Domain, twitterUser.Acct);
            var acct = _socialMediaService.MakeUserNameCanonical(twitterUser.Acct);

            // Extract links, mentions, etc
            var description = twitterUser.Description;
            if (!string.IsNullOrWhiteSpace(description))
            {
                var extracted = await _statusExtractor.Extract(description, _instanceSettings.ResolveMentionsInProfiles ? "all" : "none");
                description = extracted.content;
            }
            
            string featured = null;
            if (twitterUser.PinnedPosts.Count() > 0)
            {
                featured = $"https://{_instanceSettings.Domain}/users/{twitterUser.Acct}/collections/featured";
            }

            List<UserAttachment> attachment = new List<UserAttachment>()
            {
                new UserAttachment
                {
                    type = "PropertyValue",
                    name = "Official",
                    value =
                        $"<a href=\"https://{twitterUser.ProfileUrl}\" rel=\"me nofollow noopener noreferrer\" target=\"_blank\"><span class=\"invisible\">https://</span><span class=\"ellipsis\">{twitterUser.ProfileUrl}</span></a>"
                },
                new UserAttachment
                {
                    type = "PropertyValue",
                    name = "Support this service",
                    value =
                        $"<a href=\"https://www.patreon.com/birddotmakeup\" rel=\"me nofollow noopener noreferrer\" target=\"_blank\"><span class=\"invisible\">https://</span><span class=\"ellipsis\">www.patreon.com/birddotmakeup</span></a>"
                }
            };

            if (twitterUser.Location is not null)
            {
                var locationAttachment = new UserAttachment()
                {
                    type = "PropertyValue",
                    name = "Location",
                    value = twitterUser.Location,
                };
                attachment.Insert(0, locationAttachment);
            }
            
            if (twitterUser.Url is not null)
            {
                var locationAttachment = new UserAttachment()
                {
                    type = "PropertyValue",
                    name = "🔗",
                    value = twitterUser.Url,
                };
                attachment.Insert(0, locationAttachment);
            }

            var userDal = await _socialMediaService.UserDal.GetUserAsync(twitterUser.Acct);
            if (userDal is not null)
            {
                description = userDal.PreDescriptionHook + description + userDal.PostDescriptionHook;
                foreach ((string name, string value) in userDal.AdditionnalAttachments)
                {
                    
                    var locationAttachment = new UserAttachment()
                    {
                        type = "PropertyValue",
                        name = name,
                        value = value,
                    };
                    attachment = attachment.Append( locationAttachment).ToList();
                }
            }

            if (twitterUser.SocialMediaUserType == SocialMediaUserTypes.Group)
            {
                var group = new Group
                {
                    id = actorUrl,
                    followers = $"{actorUrl}/followers",
                    outbox = $"{actorUrl}/outbox",
                    preferredUsername = acct,
                    name = twitterUser.Name,
                    inbox = $"{actorUrl}/inbox",
                    summary = description + $"<br>This account is a replica from {_socialMediaService.ServiceName}. Its author can't see your replies. If you find this service useful, please consider supporting us via our Patreon. <br>",
                    url = actorUrl,
                    publicKey = new PublicKey()
                    {
                        id = $"{actorUrl}#main-key",
                        owner = actorUrl,
                        publicKeyPem =  await _cryptoService.GetUserPem(acct)
                    },
                    endpoints = new EndPoints
                    {
                        sharedInbox = $"https://{_instanceSettings.Domain}/inbox"
                    }
                };
                return System.Text.Json.JsonSerializer.Serialize(group, _jsonOptions);

            }

            var user = new Actor
            {
                id = actorUrl,
                type = "Service", 
                followers = $"{actorUrl}/followers",
                outbox = $"{actorUrl}/outbox",
                preferredUsername = acct,
                name = twitterUser.Name,
                inbox = $"{actorUrl}/inbox",
                summary = description + $"<br>This account is a replica from {_socialMediaService.ServiceName}. Its author can't see your replies. If you find this service useful, please consider supporting us via our Patreon. <br>",
                url = actorUrl,
                featured = featured,
                manuallyApprovesFollowers = twitterUser.Protected,
                publicKey = new PublicKey()
                {
                    id = $"{actorUrl}#main-key",
                    owner = actorUrl,
                    publicKeyPem =  await _cryptoService.GetUserPem(acct)
                },
                attachment = attachment.ToArray(),
                endpoints = new EndPoints
                {
                    sharedInbox = $"https://{_instanceSettings.Domain}/inbox"
                }
            };
            if (twitterUser.ProfileBannerURL != null)
            {
                user.image = new Image
                {
                    mediaType = "image/jpeg",
                    url = twitterUser.ProfileBannerURL
                };
            }
            if (twitterUser.ProfileImageUrl != null)
            {
                user.icon = new Image
                {
                    mediaType = "image/jpeg",
                    url = twitterUser.ProfileImageUrl
                };
            }
            return System.Text.Json.JsonSerializer.Serialize(user, _jsonOptions);
        }

        public async Task<bool> FollowRequestedAsync(string signature, string method, string path, string queryString, Dictionary<string, string> requestHeaders, ActivityFollow activity, string body)
        {

            // Validate
            var sigValidation = await ValidateSignature(activity.apObject, activity.actor, signature, method, path, queryString, requestHeaders, body);
            if (!sigValidation.SignatureIsValidated) return false;

            // Prepare data
            var followerUserName = SigValidationResultExtractor.GetUserName(sigValidation);
            var followerHost = SigValidationResultExtractor.GetHost(sigValidation);
            var followerInbox = sigValidation.User.inbox;
            var followerSharedInbox = SigValidationResultExtractor.GetSharedInbox(sigValidation);
            var twitterUser = _socialMediaService.MakeUserNameCanonical(activity.apObject.Split('/').Last().Replace("@", string.Empty)).Trim();

            // Make sure to only keep routes
            followerInbox = OnlyKeepRoute(followerInbox, followerHost);
            followerSharedInbox = OnlyKeepRoute(followerSharedInbox, followerHost);
            
            // Validate Moderation status
            var followerModPolicy = _moderationRepository.GetModerationType(ModerationEntityTypeEnum.Follower);
            if (followerModPolicy != ModerationTypeEnum.None)
            {
                var followerStatus = _moderationRepository.CheckStatus(ModerationEntityTypeEnum.Follower, $"@{followerUserName}@{followerHost}");
                
                if(followerModPolicy == ModerationTypeEnum.WhiteListing && followerStatus != ModeratedTypeEnum.WhiteListed || 
                   followerModPolicy == ModerationTypeEnum.BlackListing && followerStatus == ModeratedTypeEnum.BlackListed)
                    return await SendRejectFollowAsync(activity, followerHost);
            }

            // Validate TwitterAccount status
            var twitterAccountModPolicy = _moderationRepository.GetModerationType(ModerationEntityTypeEnum.TwitterAccount);
            if (twitterAccountModPolicy != ModerationTypeEnum.None)
            {
                var twitterUserStatus = _moderationRepository.CheckStatus(ModerationEntityTypeEnum.TwitterAccount, twitterUser);
                if (twitterAccountModPolicy == ModerationTypeEnum.WhiteListing && twitterUserStatus != ModeratedTypeEnum.WhiteListed ||
                    twitterAccountModPolicy == ModerationTypeEnum.BlackListing && twitterUserStatus == ModeratedTypeEnum.BlackListed)
                    return await SendRejectFollowAsync(activity, followerHost);
            }

            // Validate User 
            var userDal = await _socialMediaService.UserDal.GetUserAsync(twitterUser);
            if (userDal is null)
            {
                // this will fail if the username doesn't exist
                var user = await _socialMediaService.GetUserAsync(twitterUser);
                if (user.Protected)
                {

                    return await SendRejectFollowAsync(activity, followerHost);
                }
            }
            // Execute
            await _processFollowUser.ExecuteAsync(followerUserName, followerHost, twitterUser, followerInbox,
                followerSharedInbox, activity.actor);
            return await SendAcceptFollowAsync(activity, followerHost, followerInbox);
        }
        
        private async Task<bool> SendAcceptFollowAsync(ActivityFollow activity, string followerHost, string followerInbox)
        {
            var acceptFollow = _activityPubService.BuildAcceptFollow(activity);
            var result = await _activityPubService.PostDataAsync(acceptFollow, followerHost, activity.apObject, followerInbox);
            return result == HttpStatusCode.Accepted ||
                   result == HttpStatusCode.OK; //TODO: revamp this for better error handling

        }

        public async Task<bool> SendRejectFollowAsync(ActivityFollow activity, string followerHost)
        {
            var acceptFollow = new ActivityRejectFollow()
            {
                context = "https://www.w3.org/ns/activitystreams",
                id = $"{activity.apObject}#rejects/follows/{Guid.NewGuid()}",
                type = "Reject",
                actor = activity.apObject,
                apObject = new ActivityFollow()
                {
                    id = activity.id,
                    type = activity.type,
                    actor = activity.actor,
                    apObject = activity.apObject
                }
            };
            var result = await _activityPubService.PostDataAsync(acceptFollow, followerHost, activity.apObject);
            return result == HttpStatusCode.Accepted ||
                   result == HttpStatusCode.OK; //TODO: revamp this for better error handling
        }
        
        private string OnlyKeepRoute(string inbox, string host)
        {
            if (string.IsNullOrWhiteSpace(inbox)) 
                return null;

            if (inbox.Contains(host))
                inbox = inbox.Split(new[] { host }, StringSplitOptions.RemoveEmptyEntries).Last();

            return inbox;
        }

        public async Task<bool> UndoFollowRequestedAsync(string signature, string method, string path, string queryString,
            Dictionary<string, string> requestHeaders, ActivityUndoFollow activity, string body)
        {
            // Validate
            var sigValidation = await ValidateSignature(activity.apObject.apObject, activity.actor, signature, method, path, queryString, requestHeaders, body);
            if (!sigValidation.SignatureIsValidated) return false;

            // Save Follow in DB
            var followerUserName = _socialMediaService.MakeUserNameCanonical(sigValidation.User.preferredUsername);
            var followerHost = sigValidation.User.url.Replace("https://", string.Empty).Split('/').First();
            //var followerInbox = sigValidation.User.inbox;
            var twitterUser = activity.apObject.apObject.Split('/').Last().Replace("@", string.Empty);
            await _processUndoFollowUser.ExecuteAsync(followerUserName, followerHost, twitterUser);
            return true;
        }

        public async Task<bool> DeleteRequestedAsync(string signature, string method, string path, string queryString, Dictionary<string, string> requestHeaders,
            ActivityDelete activity, string body)
        {
            // Validate
            var sigValidation = await ValidateSignature(null, activity.actor, signature, method, path, queryString, requestHeaders, body);
            if (!sigValidation.SignatureIsValidated) return false;

            // Remove user and followings
            var followerUserName = SigValidationResultExtractor.GetUserName(sigValidation);
            var followerHost = SigValidationResultExtractor.GetHost(sigValidation);

            await _processDeleteUser.ExecuteAsync(followerUserName, followerHost);

            return true;
        }

        private async Task<SignatureValidationResult> ValidateSignature(string localActor, string actor, string rawSig, string method, string path, string queryString, Dictionary<string, string> requestHeaders, string body)
        {
            var remoteUser2 = await _activityPubService.GetUser(localActor, actor);
            return CryptoService.ValidateSignature(remoteUser2, rawSig, method, path, queryString, requestHeaders,
                body);
        }
    }

    public class SignatureValidationResult 
    {
        public bool SignatureIsValidated { get; set; }
        public Actor User { get; set; }
    }
}
