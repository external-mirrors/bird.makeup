using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Twitter.Models;
using BirdsiteLive.Twitter.Tools;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Twitter.Strategies;

public class Graphql2024 : ITweetExtractor, ITimelineExtractor, IUserExtractor
{
    private readonly ITwitterAuthenticationInitializer _twitterAuthenticationInitializer;
    private readonly ITwitterTweetsService _tweetsService;
    private readonly InstanceSettings _instanceSettings;
    private readonly ILogger<TwitterService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string endpoint =
        "https://twitter.com/i/api/graphql/SAMkL5y_N9pmahSw8yy6gw/UserByScreenName?variables=%7B%22screen_name%22%3A%22elonmusk%22%2C%22withSafetyModeUserFields%22%3Atrue%7D&features=%7B%22hidden_profile_likes_enabled%22%3Afalse%2C%22hidden_profile_subscriptions_enabled%22%3Atrue%2C%22responsive_web_graphql_exclude_directive_enabled%22%3Atrue%2C%22verified_phone_label_enabled%22%3Afalse%2C%22subscriptions_verification_info_is_identity_verified_enabled%22%3Afalse%2C%22subscriptions_verification_info_verified_since_enabled%22%3Atrue%2C%22highlights_tweets_tab_ui_enabled%22%3Atrue%2C%22creator_subscriptions_tweet_preview_api_enabled%22%3Atrue%2C%22responsive_web_graphql_skip_user_profile_image_extensions_enabled%22%3Afalse%2C%22responsive_web_graphql_timeline_navigation_enabled%22%3Atrue%7D&fieldToggles=%7B%22withAuxiliaryUserLabels%22%3Afalse%7D";

    private static string gqlFeatures = """
    { 
      "android_graphql_skip_api_media_color_palette": false,
      "blue_business_profile_image_shape_enabled": false,
      "creator_subscriptions_subscription_count_enabled": false,
      "creator_subscriptions_tweet_preview_api_enabled": true,
      "freedom_of_speech_not_reach_fetch_enabled": false,
      "graphql_is_translatable_rweb_tweet_is_translatable_enabled": false,
      "hidden_profile_likes_enabled": false,
      "highlights_tweets_tab_ui_enabled": false,
      "interactive_text_enabled": false,
      "longform_notetweets_consumption_enabled": true,
      "longform_notetweets_inline_media_enabled": false,
      "longform_notetweets_richtext_consumption_enabled": true,
      "longform_notetweets_rich_text_read_enabled": false,
      "responsive_web_edit_tweet_api_enabled": false,
      "responsive_web_enhance_cards_enabled": false,
      "responsive_web_graphql_exclude_directive_enabled": true,
      "responsive_web_graphql_skip_user_profile_image_extensions_enabled": false,
      "responsive_web_graphql_timeline_navigation_enabled": false,
      "responsive_web_media_download_video_enabled": false,
      "responsive_web_text_conversations_enabled": false,
      "responsive_web_twitter_article_tweet_consumption_enabled": false,
      "responsive_web_twitter_blue_verified_badge_is_enabled": true,
      "rweb_lists_timeline_redesign_enabled": true,
      "spaces_2022_h2_clipping": true,
      "spaces_2022_h2_spaces_communities": true,
      "standardized_nudges_misinfo": false,
      "subscriptions_verification_info_enabled": true,
      "subscriptions_verification_info_reason_enabled": true,
      "subscriptions_verification_info_verified_since_enabled": true,
      "super_follow_badge_privacy_enabled": false,
      "super_follow_exclusive_tweet_notifications_enabled": false,
      "super_follow_tweet_api_enabled": false,
      "super_follow_user_api_enabled": false,
      "tweet_awards_web_tipping_enabled": false,
      "tweet_with_visibility_results_prefer_gql_limited_actions_policy_enabled": false,
      "tweetypie_unmention_optimization_enabled": false,
      "unified_cards_ad_metadata_container_dynamic_card_content_query_enabled": false,
      "verified_phone_label_enabled": false,
      "vibe_api_enabled": false,
      "view_counts_everywhere_api_enabled": false
    }
    """.Replace(" ", "").Replace("\n", "");
    public Graphql2024(ITwitterAuthenticationInitializer twitterAuthenticationInitializer, ITwitterTweetsService tweetsService, IHttpClientFactory httpClientFactory, InstanceSettings instanceSettings, ILogger<TwitterService> logger)
    {
        _twitterAuthenticationInitializer =  twitterAuthenticationInitializer;
        _tweetsService = tweetsService;
        _httpClientFactory = httpClientFactory;
        _instanceSettings = instanceSettings;
        _logger = logger;
        
    }
    public async Task<TwitterUser> GetUserAsync(string username)
    {

        JsonDocument res;
        var client = await _twitterAuthenticationInitializer.MakeHttpClient();
        using var request = _twitterAuthenticationInitializer.MakeHttpRequest(new HttpMethod("GET"), endpoint.Replace("elonmusk", username), true);

        var httpResponse = await client.SendAsync(request);
        if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogError("Error retrieving user {Username}, Refreshing client", username);
            await _twitterAuthenticationInitializer.RefreshClient(request);
            return null;
        }
        httpResponse.EnsureSuccessStatusCode();

        var c = await httpResponse.Content.ReadAsStringAsync();
        res = JsonDocument.Parse(c);
        var result = res.RootElement.GetProperty("data").GetProperty("user").GetProperty("result");
        var user = ExtractUser(result);
        return user;

    }

    public async Task<List<ExtractedTweet>> GetTimelineAsync(SyncUser user, long userId, long fromTweetId)
    {
        var client = await _twitterAuthenticationInitializer.MakeHttpClient();
        string username = user.Acct;
        
        //reqURL =
        //    """https://twitter.com/i/api/graphql/rIIwMe1ObkGh_ByBtTCtRQ/UserTweets?variables={"userId":"44196397","count":20,"includePromotedContent":true,"withQuickPromoteEligibilityTweetFields":true,"withVoice":true,"withV2Timeline":true}&features={"rweb_lists_timeline_redesign_enabled":true,"responsive_web_graphql_exclude_directive_enabled":true,"verified_phone_label_enabled":false,"creator_subscriptions_tweet_preview_api_enabled":true,"responsive_web_graphql_timeline_navigation_enabled":true,"responsive_web_graphql_skip_user_profile_image_extensions_enabled":false,"tweetypie_unmention_optimization_enabled":true,"responsive_web_edit_tweet_api_enabled":true,"graphql_is_translatable_rweb_tweet_is_translatable_enabled":true,"view_counts_everywhere_api_enabled":true,"longform_notetweets_consumption_enabled":true,"responsive_web_twitter_article_tweet_consumption_enabled":false,"tweet_awards_web_tipping_enabled":false,"freedom_of_speech_not_reach_fetch_enabled":true,"standardized_nudges_misinfo":true,"tweet_with_visibility_results_prefer_gql_limited_actions_policy_enabled":true,"longform_notetweets_rich_text_read_enabled":true,"longform_notetweets_inline_media_enabled":true,"responsive_web_media_download_video_enabled":false,"responsive_web_enhance_cards_enabled":false}""";
        //reqURL = reqURL.Replace("44196397", userId.ToString());
        string reqURL =
            """https://twitter.com/i/api/graphql/XicnWRbyQ3WgVY__VataBQ/UserTweets?variables={"userId":""" + '"' + userId + '"' + ""","count":20,"includePromotedContent":true,"withQuickPromoteEligibilityTweetFields":true,"withVoice":true,"withV2Timeline":true}&features={"rweb_lists_timeline_redesign_enabled":true,"responsive_web_graphql_exclude_directive_enabled":true,"verified_phone_label_enabled":false,"creator_subscriptions_tweet_preview_api_enabled":true,"responsive_web_graphql_timeline_navigation_enabled":true,"responsive_web_graphql_skip_user_profile_image_extensions_enabled":false,"tweetypie_unmention_optimization_enabled":true,"responsive_web_edit_tweet_api_enabled":true,"graphql_is_translatable_rweb_tweet_is_translatable_enabled":true,"view_counts_everywhere_api_enabled":true,"longform_notetweets_consumption_enabled":true,"responsive_web_twitter_article_tweet_consumption_enabled":false,"tweet_awards_web_tipping_enabled":false,"freedom_of_speech_not_reach_fetch_enabled":true,"standardized_nudges_misinfo":true,"tweet_with_visibility_results_prefer_gql_limited_actions_policy_enabled":true,"longform_notetweets_rich_text_read_enabled":true,"longform_notetweets_inline_media_enabled":true,"responsive_web_media_download_video_enabled":false,"responsive_web_enhance_cards_enabled":false}""";
        JsonDocument results;
        List<ExtractedTweet> extractedTweets = new List<ExtractedTweet>();
        using var request = _twitterAuthenticationInitializer.MakeHttpRequest(new HttpMethod("GET"), reqURL, true);
        try
        {

            var httpResponse = await client.SendAsync(request);
            var c = await httpResponse.Content.ReadAsStringAsync();
            if (httpResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                _logger.LogError("Error retrieving timeline of {Username}; refreshing client", username);
                await _twitterAuthenticationInitializer.RefreshClient(request);
                return [];
            }
            httpResponse.EnsureSuccessStatusCode();
            results = JsonDocument.Parse(c);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error retrieving timeline ", username);
            return null;
        }

        var timeline = results.RootElement.GetProperty("data").GetProperty("user").GetProperty("result")
            .GetProperty("timeline_v2").GetProperty("timeline").GetProperty("instructions").EnumerateArray();

        foreach (JsonElement timelineElement in timeline) 
        {
            if (timelineElement.GetProperty("type").GetString() != "TimelineAddEntries")
                continue;

            
            foreach (JsonElement tweet in timelineElement.GetProperty("entries").EnumerateArray())
            {
                if (tweet.GetProperty("content").GetProperty("__typename").GetString() != "TimelineTimelineItem")
                    continue;
                

                try 
                {   
                    JsonElement tweetRes = tweet.GetProperty("content").GetProperty("itemContent")
                        .GetProperty("tweet_results").GetProperty("result");
                    
                    // this reduce error logs if we can't parse old tweets
                    JsonElement restId;
                    if (!tweetRes.TryGetProperty("rest_id", out restId))
                        continue;
                    if (Int64.Parse(restId.GetString()) < fromTweetId)
                        continue;
                    
                    var extractedTweet = await Extract(tweetRes);

                    extractedTweets.Add(extractedTweet);
                }
                catch (Exception e)
                {
                    _logger.LogError("Tried getting timeline from user " + username + ", but got error: \n" +
                                     e.Message + e.StackTrace + e.Source);

                }

            }
        }
        extractedTweets = extractedTweets.OrderByDescending(x => x.Id).Where(x => x.IdLong > fromTweetId).ToList();

        return extractedTweets;
    }

    public async Task<ExtractedTweet> GetTweetAsync(long statusId)
    {
        var client = await _twitterAuthenticationInitializer.MakeHttpClient();


        string reqURL =
            "https://twitter.com/i/api/graphql/0hWvDhmW8YQ-S_ib3azIrw/TweetResultByRestId?variables=%7B%22tweetId%22%3A%221519480761749016577%22%2C%22withCommunity%22%3Afalse%2C%22includePromotedContent%22%3Afalse%2C%22withVoice%22%3Afalse%7D&features=%7B%22creator_subscriptions_tweet_preview_api_enabled%22%3Atrue%2C%22tweetypie_unmention_optimization_enabled%22%3Atrue%2C%22responsive_web_edit_tweet_api_enabled%22%3Atrue%2C%22graphql_is_translatable_rweb_tweet_is_translatable_enabled%22%3Atrue%2C%22view_counts_everywhere_api_enabled%22%3Atrue%2C%22longform_notetweets_consumption_enabled%22%3Atrue%2C%22responsive_web_twitter_article_tweet_consumption_enabled%22%3Afalse%2C%22tweet_awards_web_tipping_enabled%22%3Afalse%2C%22freedom_of_speech_not_reach_fetch_enabled%22%3Atrue%2C%22standardized_nudges_misinfo%22%3Atrue%2C%22tweet_with_visibility_results_prefer_gql_limited_actions_policy_enabled%22%3Atrue%2C%22longform_notetweets_rich_text_read_enabled%22%3Atrue%2C%22longform_notetweets_inline_media_enabled%22%3Atrue%2C%22responsive_web_graphql_exclude_directive_enabled%22%3Atrue%2C%22verified_phone_label_enabled%22%3Afalse%2C%22responsive_web_media_download_video_enabled%22%3Afalse%2C%22responsive_web_graphql_skip_user_profile_image_extensions_enabled%22%3Afalse%2C%22responsive_web_graphql_timeline_navigation_enabled%22%3Atrue%2C%22responsive_web_enhance_cards_enabled%22%3Afalse%7D";
        reqURL = reqURL.Replace("1519480761749016577", statusId.ToString());
        using var request = _twitterAuthenticationInitializer.MakeHttpRequest(new HttpMethod("GET"), reqURL, true);
        JsonDocument tweet;
        var httpResponse = await client.SendAsync(request);
        httpResponse.EnsureSuccessStatusCode();
        var c = await httpResponse.Content.ReadAsStringAsync();
        tweet = JsonDocument.Parse(c);


        var tweetInDoc = tweet.RootElement.GetProperty("data").GetProperty("tweetResult")
            .GetProperty("result");

        var extract = await Extract(tweetInDoc);
        return extract;
    }

    private async Task<ExtractedTweet> Extract(JsonElement tweetRes)
    {

        JsonElement retweet;
        TwitterUser OriginalAuthor;
        TwitterUser author = null;
        JsonElement inReplyToPostIdElement;
        JsonElement inReplyToUserElement;
        string inReplyToUser = null;
        long? inReplyToPostId = null;
        long retweetId = default;

        JsonElement userDoc = tweetRes.GetProperty("core")
                .GetProperty("user_results").GetProperty("result");


        author = ExtractUser(userDoc);
        string userName = author.Acct;
        
        bool isReply = tweetRes.GetProperty("legacy")
                .TryGetProperty("in_reply_to_status_id_str", out inReplyToPostIdElement);
        tweetRes.GetProperty("legacy")
                .TryGetProperty("in_reply_to_screen_name", out inReplyToUserElement);
        if (isReply) 
        {
            inReplyToPostId = Int64.Parse(inReplyToPostIdElement.GetString());
            inReplyToUser = inReplyToUserElement.GetString();
        }
        bool isRetweet = tweetRes.GetProperty("legacy")
                .TryGetProperty("retweeted_status_result", out retweet);
        string MessageContent;
        if (!isRetweet)
        {
            MessageContent = tweetRes.GetProperty("legacy")
                .GetProperty("full_text").GetString();
            bool isNote = tweetRes.TryGetProperty("note_tweet", out var note);
            if (isNote)
            {
                MessageContent = note.GetProperty("note_tweet_results").GetProperty("result")
                    .GetProperty("text").GetString();
            }
            OriginalAuthor = null;
            
        }
        else 
        {
            MessageContent = tweetRes.GetProperty("legacy")
                .GetProperty("retweeted_status_result").GetProperty("result")
                .GetProperty("legacy").GetProperty("full_text").GetString();
            bool isNote = tweetRes.GetProperty("legacy")
                .GetProperty("retweeted_status_result").GetProperty("result")
                .TryGetProperty("note_tweet", out var note);
            if (isNote)
            {
                MessageContent = note.GetProperty("note_tweet_results").GetProperty("result")
                    .GetProperty("text").GetString();
            }
            JsonElement OriginalAuthorDoc = tweetRes.GetProperty("legacy")
                .GetProperty("retweeted_status_result").GetProperty("result")
                .GetProperty("core").GetProperty("user_result").GetProperty("result");
            OriginalAuthor = ExtractUser(OriginalAuthorDoc); 
            //OriginalAuthor = await _twitterUserService.GetUserAsync(OriginalAuthorUsername);
            retweetId = Int64.Parse(tweetRes.GetProperty("legacy")
                .GetProperty("retweeted_status_result").GetProperty("result")
                .GetProperty("rest_id").GetString());
        }
        
        if (MessageContent.StartsWith(".@"))
            MessageContent = MessageContent.Remove(0, 1);
            

        string creationTime = tweetRes.GetProperty("legacy")
                .GetProperty("created_at").GetString().Replace(" +0000", "");

        JsonElement extendedEntities;
        bool hasMedia = tweetRes.GetProperty("legacy")
                .TryGetProperty("extended_entities", out extendedEntities);

        JsonElement.ArrayEnumerator urls = tweetRes.GetProperty("legacy")
                .GetProperty("entities").GetProperty("urls").EnumerateArray();
        foreach (JsonElement url in urls)
        {
            string tco = url.GetProperty("url").GetString();
            string goodUrl = url.GetProperty("expanded_url").GetString();
            MessageContent = MessageContent.Replace(tco, goodUrl);
        }
        
        List<ExtractedMedia> Media = new List<ExtractedMedia>();
        if (hasMedia) 
        {
            foreach (JsonElement media in extendedEntities.GetProperty("media").EnumerateArray())
            {
                var type = media.GetProperty("type").GetString();
                string url = "";
                string altText = null;
                if (media.TryGetProperty("video_info", out _))
                {
                    var bitrate = -1;
                    foreach (JsonElement v in media.GetProperty("video_info").GetProperty("variants").EnumerateArray())
                    {
                        if (v.GetProperty("content_type").GetString() !=  "video/mp4")
                            continue;
                        int vBitrate = v.GetProperty("bitrate").GetInt32();
                        if (vBitrate > bitrate)
                        {
                            bitrate = vBitrate;
                            url = v.GetProperty("url").GetString();
                        }
                    }
                }
                else 
                {
                    url = media.GetProperty("media_url_https").GetString();
                }

                if (media.TryGetProperty("ext_alt_text", out JsonElement altNode))
                {
                    altText = altNode.GetString();
                }
                var m = new ExtractedMedia
                {
                    MediaType = GetMediaType(type, url),
                    Url = url,
                    AltText = altText
                };
                Media.Add(m);

                MessageContent = MessageContent.Replace(media.GetProperty("url").GetString(), "");
            }
        }

        bool isQuoteTweet = tweetRes.GetProperty("legacy")
                .GetProperty("is_quote_status").GetBoolean();

        string quoteTweetId = null;
        string quoteTweetAcct = null;
        if (isQuoteTweet)
        {

            quoteTweetId = tweetRes.GetProperty("legacy")
                    .GetProperty("quoted_status_id_str").GetString();
            JsonElement quoteTweetAcctDoc = tweetRes
                .GetProperty("quoted_status_result").GetProperty("result")
                .GetProperty("core").GetProperty("user_results").GetProperty("result");
            TwitterUser QTauthor = ExtractUser(quoteTweetAcctDoc);
            quoteTweetAcct = QTauthor.Acct;
            //Uri test = new Uri(quoteTweetLink);
            //string quoteTweetAcct = test.Segments[1].Replace("/", "");
            //string quoteTweetId = test.Segments[3];
            
            string quoteTweetLink = $"https://{_instanceSettings.Domain}/@{quoteTweetAcct}/{quoteTweetId}";

            //MessageContent.Replace($"https://twitter.com/i/web/status/{}", "");
           // MessageContent = MessageContent.Replace($"https://twitter.com/{quoteTweetAcct}/status/{quoteTweetId}", "");
            MessageContent = MessageContent.Replace($"https://twitter.com/{quoteTweetAcct}/status/{quoteTweetId}", "", StringComparison.OrdinalIgnoreCase);
            MessageContent = MessageContent.Replace($"https://x.com/{quoteTweetAcct}/status/{quoteTweetId}", "", StringComparison.OrdinalIgnoreCase);
            
            //MessageContent = Regex.Replace(MessageContent, Regex.Escape($"https://twitter.com/{quoteTweetAcct}/status/{quoteTweetId}"), "", RegexOptions.IgnoreCase);
            // MessageContent = MessageContent + "\n\n" + quoteTweetLink;
        }

        Poll poll = null;
        JsonElement cardDoc;
        if (tweetRes.TryGetProperty("card", out cardDoc))
        {
            DateTime endDate = DateTime.Now;
            Dictionary<char, string> labels = new Dictionary<char, string>();
            Dictionary<char, long> counts = new Dictionary<char, long>();
            string type = cardDoc.GetProperty("legacy").GetProperty("name").GetString();
            foreach (JsonElement val in cardDoc.GetProperty("legacy").GetProperty("binding_values")
                         .EnumerateArray())
            {
                var key = val.GetProperty("key").GetString();
                if (key == "end_datetime_utc")
                {
                    var endDateString = val.GetProperty("value").GetProperty("string_value").GetString();
                    endDate = DateTime.Parse(endDateString);
                }
                else if (key.StartsWith("choice") && key.EndsWith("label"))
                {
                    var entryLabel = val.GetProperty("value").GetProperty("string_value").GetString();
                    char entryNumber = key[6];
                    labels.Add(entryNumber, entryLabel);
                }
                else if (key.StartsWith("choice") && key.EndsWith("count"))
                {
                    var entryLabel = val.GetProperty("value").GetProperty("string_value").GetString();
                    char entryNumber = key[6];
                    counts.Add(entryNumber, long.Parse(entryLabel));
                }
            }

            var c = counts.OrderBy(x => x.Key).Select(x => x.Value);
            poll = new Poll()
            {
                endTime = endDate,
                options = labels.OrderBy(x => x.Key).Select(x => x.Value).Zip(c).ToList(),
            };
            if (!type.StartsWith("poll"))
                poll = null;
        }

        
        var extractedTweet = new ExtractedTweet
        {
            Id = tweetRes.GetProperty("rest_id").GetString(),
            InReplyToStatusId = inReplyToPostId,
            InReplyToAccount = inReplyToUser,
            MessageContent = MessageContent.Trim(),
            CreatedAt = DateTime.ParseExact(creationTime, "ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture),
            IsReply = isReply,
            IsThread = userName == inReplyToUser,
            IsRetweet = isRetweet,
            Media = Media.Count() == 0 ? null : Media.ToArray(),
            RetweetUrl = "https://t.co/123",
            RetweetId = retweetId,
            OriginalAuthor = OriginalAuthor,
            Author = author,
            Poll = poll,
            QuotedAccount = quoteTweetAcct,
            QuotedStatusId = quoteTweetId,
        };
        extractedTweet = await _tweetsService.ExpandShortLinks(extractedTweet);
   
        return extractedTweet;
     
    }
    public TwitterUser ExtractUser(JsonElement result)
    {
        string profileBannerURL = null;
        JsonElement profileBannerURLObject;
        if (result.GetProperty("legacy").TryGetProperty("profile_banner_url", out profileBannerURLObject))
        {
            profileBannerURL = profileBannerURLObject.GetString();
        }

        List<string> pinnedTweets = new();
        JsonElement pinnedDoc;
        if (result.GetProperty("legacy").TryGetProperty("pinned_tweet_ids_str", out pinnedDoc))
        {
            foreach (JsonElement id in pinnedDoc.EnumerateArray())
            {
                pinnedTweets.Add(id.GetString());
            }
        }

        string location = null;
        JsonElement locationDoc;
        if (result.GetProperty("legacy").TryGetProperty("location", out locationDoc))
        {
            location = locationDoc.GetString();
            if (location == "")
                location = null;
        } 

        return new TwitterUser
        {
            Id = long.Parse(result.GetProperty("rest_id").GetString()),
            Acct = result.GetProperty("legacy").GetProperty("screen_name").GetString().ToLower(), 
            Name =  result.GetProperty("legacy").GetProperty("name").GetString(), //res.RootElement.GetProperty("data").GetProperty("name").GetString(),
            Description =  "", //res.RootElement.GetProperty("data").GetProperty("description").GetString(),
            Url =  "", //res.RootElement.GetProperty("data").GetProperty("url").GetString(),
            ProfileImageUrl =  result.GetProperty("legacy").GetProperty("profile_image_url_https").GetString().Replace("normal", "400x400"), 
            ProfileBannerURL = profileBannerURL,
            Protected = false, //res.RootElement.GetProperty("data").GetProperty("protected").GetBoolean(), 
            PinnedPosts = pinnedTweets,
            StatusCount = result.GetProperty("legacy").GetProperty("statuses_count").GetInt32(),
            FollowersCount = result.GetProperty("legacy").GetProperty("followers_count").GetInt32(),
            Location = location,
            ProfileUrl = "twitter.com/" + result.GetProperty("legacy").GetProperty("screen_name").GetString().ToLower(), 
        };

    }
    private string GetMediaType(string mediaType, string mediaUrl)
    {
        switch (mediaType)
        {
            case "photo":
                var pExt = Path.GetExtension(mediaUrl);
                switch (pExt)
                {
                    case ".jpg":
                    case ".jpeg":
                        return "image/jpeg";
                    case ".png":
                        return "image/png";
                }
                return null;

            case "animated_gif":
                var vExt = Path.GetExtension(mediaUrl);
                switch (vExt)
                {
                    case ".gif":
                        return "image/gif";
                    case ".mp4":
                        return "video/mp4";
                }
                return "image/gif";
            case "video":
                return "video/mp4";
        }
        return null;
    }
}