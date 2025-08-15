using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Common.Exceptions;
using BirdsiteLive.Domain;
using BirdsiteLive.Instagram.Models;
using dotMakeup.ipfs;
using dotMakeup.Instagram.Models;
using dotMakeup.Instagram.Strategies;
using Microsoft.Extensions.Caching.Memory;

namespace dotMakeup.Instagram;

public class InstagramService : ISocialMediaService
{
        static Meter _meter = new("DotMakeup", "1.0.0");
        static Counter<int> _apiCalled = _meter.CreateCounter<int>("dotmakeup_api_called_count");
        
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly InstanceSettings _settings;
        private readonly ISettingsDal _settingsDal;
        private readonly IInstagramUserDal _instagramUserDal;
        private readonly IIpfsService _ipfs;
        private readonly SocialNetworkCache _socialNetworkCache;

        private readonly Sidecar _sidecar;
        private readonly Sidecar _sidecarPremium;
        private readonly Direct _direct;

        private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions()
        {
            Converters = { new InstagramSocialMediaUserConverter() }
        };

        #region Ctor
        public InstagramService(IIpfsService ipfs, IInstagramUserDal userDal, IHttpClientFactory httpClientFactory, InstanceSettings settings, ISettingsDal settingsDal)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings;
            _settingsDal = settingsDal;
            _instagramUserDal = userDal;
            _ipfs = ipfs;
            UserDal = userDal;
            
            _socialNetworkCache = new SocialNetworkCache(settings);
            _sidecar = new Sidecar(httpClientFactory, userDal, settingsDal, settings, _ipfs);
            _sidecarPremium = new Sidecar(httpClientFactory, userDal, settingsDal, settings, _ipfs, true);
            _direct = new Direct(httpClientFactory, userDal, _ipfs);
        }
        #endregion


        public string MakeUserNameCanonical(string name)
        {
            return name.Trim().ToLowerInvariant();
        }

        public string ServiceName { get; } = "Instagram";
        public Regex ValidUsername { get;  } = new Regex(@"^[a-zA-Z0-9_\.]{1,30}(?<!\.)$");
        public Regex UserMention { get;  } = new Regex(@"(^|.?[ \n\.]+)@([a-zA-Z0-9_\.]+)(?<!\.)(?=\s|$|[\[\]<>,;:'\.’!?/—\|-]|(. ))");
        public SocialMediaUserDal UserDal { get; }
        
        public async Task<SocialMediaPost[]> GetNewPosts(SyncUser user)
        {
            await UserDal.UpdateUserLastSyncAsync(user);
            
            var newPosts = new List<SocialMediaPost>();
            var v2 = await RefreshUserAsync(user.Acct);
            _apiCalled.Add(1, new KeyValuePair<string, object?>("api", "instagram_sidecar_timeline"),
                new KeyValuePair<string, object?>("result", v2 != null ? "2xx": "5xx")
            );
            if (v2 == null)
                return [];
            
            foreach (var p in v2.RecentPosts)
            {
                if (p.CreatedAt > user.LastPost)
                {
                    var mirrored = await _ipfs.Mirror(p, true);
                    await _instagramUserDal.UpdatePostCacheAsync(mirrored);
                    
                    newPosts.Add(p);
                }
            }

            return newPosts.ToArray();
        }

        public async Task<SocialMediaUser?> GetUserAsync(string username)
        {
            var user = await _socialNetworkCache.GetUser(username, [
                () => _instagramUserDal.GetUserCacheAsync<InstagramUser>(username),
                () => _sidecar.GetUserAsync(username), 
                () => _sidecarPremium.GetUserAsync(username), 
                () => _direct.GetUserAsync(username),
            ]);
            return user;
        }

        private async Task<InstagramUser?> RefreshUserAsync(string username)
        {
            InstagramUser user;
            
            user = await _direct.GetUserAsync(username);
                
            var profileUrlHash = await _ipfs.Mirror(user.ProfileImageUrl, true);
            user.ProfileImageUrl = _ipfs.GetIpfsPublicLink(profileUrlHash);
                
            await _instagramUserDal.UpdateUserCacheAsync(user);
            
            return user;
        }

        public async Task<SocialMediaPost?> GetPostAsync(string id)
        {
            var post = await _socialNetworkCache.GetPost(id, [
                () => _instagramUserDal.GetPostCacheAsync<InstagramPost>(id),
                () => _sidecar.GetPostAsync(id),
            ]);
            return post;
        }

        
}
public class InstagramSocialMediaUserConverter : JsonConverter<SocialMediaUser>
{
    public override SocialMediaUser? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<InstagramUser>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, SocialMediaUser value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}