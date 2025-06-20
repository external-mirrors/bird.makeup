using System.Text.Json;
using System.Text.Json.Serialization;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Instagram.Models;

namespace dotMakeup.Instagram.Models;

public class InstagramUser : SocialMediaUser
{
        public IEnumerable<InstagramPost> RecentPosts { get; set; } = [];
        public long FollowersCount { get; set; }
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