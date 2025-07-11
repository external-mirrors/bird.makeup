using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Twitter.Models;
using BirdsiteLive.Twitter.Tools;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.DAL.Models;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Io;
using BirdsiteLive.Twitter.Strategies;
using HttpMethod = System.Net.Http.HttpMethod;

namespace BirdsiteLive.Twitter
{
    public interface ITwitterTweetsService
    {
        Task<ExtractedTweet> GetTweetAsync(long statusId);
        Task<ExtractedTweet[]> GetTimelineAsync(SyncUser user, long fromTweetId = -1);
        Task<ExtractedTweet> ExpandShortLinks(ExtractedTweet tweet);
    }
    
    public class TwitterTweetsService : ITwitterTweetsService
    {
        static Meter _meter = new("DotMakeup", "1.0.0");
        static Counter<int> _apiCalled = _meter.CreateCounter<int>("dotmakeup_api_called_count");
        static Counter<int> _newTweets = _meter.CreateCounter<int>("dotmakeup_twitter_new_tweets_count");
        
        private readonly ITwitterAuthenticationInitializer _twitterAuthenticationInitializer;
        private readonly ICachedTwitterUserService _twitterUserService;
        private readonly ITwitterUserDal _twitterUserDal;
        private readonly ILogger<TwitterService> _logger;
        private readonly InstanceSettings _instanceSettings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IBrowsingContext _context;
        private readonly ISettingsDal _settings;
        private string Useragent = "Bird.makeup ( https://git.sr.ht/~cloutier/bird.makeup ) Bot";
        private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions()
        {
            Converters = { new TwitterSocialMediaUserConverter() }
        };

        private readonly ITweetExtractor _tweetFromSyndication;
        private readonly Graphql2024 _tweetFromGraphql2024;

        #region Ctor
        public TwitterTweetsService(ITwitterAuthenticationInitializer twitterAuthenticationInitializer, ICachedTwitterUserService twitterUserService, ITwitterUserDal twitterUserDal, InstanceSettings instanceSettings, IHttpClientFactory httpClientFactory, ISettingsDal settings, ILogger<TwitterService> logger)
        {
            _twitterAuthenticationInitializer = twitterAuthenticationInitializer;
            _twitterUserService = twitterUserService;
            _twitterUserDal = twitterUserDal;
            _instanceSettings = instanceSettings;
            _httpClientFactory = httpClientFactory;
            _settings = settings;
            _logger = logger;
            
            var requester = new DefaultHttpRequester();
            requester.Headers["User-Agent"] = Useragent;
            requester.Headers["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8";
            requester.Headers["Accept-Encoding"] = "gzip, deflate";
            requester.Headers["Accept-Language"] = "en-US,en;q=0.5";
            var config = Configuration.Default.With(requester).WithDefaultLoader();
            _context = BrowsingContext.New(config);

            _tweetFromSyndication = new Syndication(this, httpClientFactory, instanceSettings, logger);
            _tweetFromGraphql2024 = new Graphql2024(_twitterAuthenticationInitializer, this, httpClientFactory, instanceSettings, logger);
        }
        #endregion


        public async Task<ExtractedTweet> GetTweetAsync(long statusId, StrategyHints s)
        {
            if (s == StrategyHints.Syndication)
                return await _tweetFromSyndication.GetTweetAsync(statusId);

            if (s == StrategyHints.Graphql2024)
                return await _tweetFromGraphql2024.GetTweetAsync(statusId);
            return null;
        }
        public async Task<ExtractedTweet> GetTweetAsync(long statusId)
        {
            //return await TweetFromSidecar(statusId);
            try
            {
                var extract = await  _tweetFromGraphql2024.GetTweetAsync(statusId);
                
                _apiCalled.Add(1, new KeyValuePair<string, object>("api", "twitter_tweet"),
                    new KeyValuePair<string, object>("result", "2xx")
                );
                return extract;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error retrieving tweet {TweetId}", statusId);
                ExtractedTweet backupTweet = null;
                try
                {
                    backupTweet = await _tweetFromSyndication.GetTweetAsync(statusId);
                }
                catch (Exception _) {}
                _apiCalled.Add(1, new KeyValuePair<string, object>("api", "twitter_tweet"),
                    new KeyValuePair<string, object>("result", backupTweet is null ? "5xx" : "2xx_backup")
                );
                return backupTweet;
            }
        }
        public async Task<ExtractedTweet> GetTweetAsync2(long statusId)
        {
            //return await TweetFromSyndication(statusId);
            //return await TweetFromSidecar(statusId);

            var client = await _twitterAuthenticationInitializer.MakeHttpClient();


            string reqURL =
                "https://api.x.com/graphql/evQ359cb1YUxGLO2r3aLTA/TweetResultByRestId?variables=%7B%22tweetId%22%3A%221691152661331021825%22%2C%22withCommunity%22%3Afalse%2C%22includePromotedContent%22%3Afalse%2C%22withVoice%22%3Afalse%7D&features=%7B%22creator_subscriptions_tweet_preview_api_enabled%22%3Atrue%2C%22premium_content_api_read_enabled%22%3Afalse%2C%22communities_web_enable_tweet_community_results_fetch%22%3Atrue%2C%22c9s_tweet_anatomy_moderator_badge_enabled%22%3Atrue%2C%22responsive_web_grok_analyze_button_fetch_trends_enabled%22%3Afalse%2C%22responsive_web_grok_analyze_post_followups_enabled%22%3Afalse%2C%22responsive_web_jetfuel_frame%22%3Atrue%2C%22responsive_web_grok_share_attachment_enabled%22%3Atrue%2C%22articles_preview_enabled%22%3Atrue%2C%22responsive_web_edit_tweet_api_enabled%22%3Atrue%2C%22graphql_is_translatable_rweb_tweet_is_translatable_enabled%22%3Atrue%2C%22view_counts_everywhere_api_enabled%22%3Atrue%2C%22longform_notetweets_consumption_enabled%22%3Atrue%2C%22responsive_web_twitter_article_tweet_consumption_enabled%22%3Atrue%2C%22tweet_awards_web_tipping_enabled%22%3Afalse%2C%22responsive_web_grok_show_grok_translated_post%22%3Afalse%2C%22responsive_web_grok_analysis_button_from_backend%22%3Atrue%2C%22creator_subscriptions_quote_tweet_preview_enabled%22%3Afalse%2C%22freedom_of_speech_not_reach_fetch_enabled%22%3Atrue%2C%22standardized_nudges_misinfo%22%3Atrue%2C%22tweet_with_visibility_results_prefer_gql_limited_actions_policy_enabled%22%3Atrue%2C%22longform_notetweets_rich_text_read_enabled%22%3Atrue%2C%22longform_notetweets_inline_media_enabled%22%3Atrue%2C%22payments_enabled%22%3Afalse%2C%22profile_label_improvements_pcf_label_in_post_enabled%22%3Atrue%2C%22rweb_tipjar_consumption_enabled%22%3Atrue%2C%22verified_phone_label_enabled%22%3Afalse%2C%22responsive_web_grok_image_annotation_enabled%22%3Atrue%2C%22responsive_web_grok_community_note_auto_translation_is_enabled%22%3Afalse%2C%22responsive_web_graphql_skip_user_profile_image_extensions_enabled%22%3Afalse%2C%22responsive_web_graphql_timeline_navigation_enabled%22%3Atrue%2C%22responsive_web_enhance_cards_enabled%22%3Afalse%7D&fieldToggles=%7B%22withArticleRichContentState%22%3Atrue%2C%22withArticlePlainText%22%3Afalse%2C%22withGrokAnalyze%22%3Afalse%2C%22withDisallowedReplyControls%22%3Afalse%7D";
            reqURL = reqURL.Replace("1691152661331021825", statusId.ToString());
                return null;
        }

        public async Task<ExtractedTweet[]> GetTimelineAsync(SyncUser user, long fromTweetId = -1)
        {
            long userId;
            string username = user.Acct;
            if (user.TwitterUserId == default) 
            {
                var user2 = await _twitterUserService.GetUserAsync(username);
                userId = user2.Id;
                await _twitterUserDal.UpdateTwitterUserIdAsync(username, user2.Id);
                user.TwitterUserId = userId;
            }
            else 
            {
                userId = user.TwitterUserId;
            }

            List<ExtractedTweet> extractedTweets;

            int followersThreshold0 = 9999999;
            int followersThreshold = 9999999;
            int followersThreshold2 = 9999999;
            int followersThreshold3 = 9999999;
            int twitterFollowersThreshold = 9999999;
            int postNitterDelay = 500;
            
            string source;
            var nitterSettings = await _settings.Get("nitter");
            if (nitterSettings is not null)
            {
                followersThreshold0 = nitterSettings.Value.GetProperty("followersThreshold0").GetInt32();
                followersThreshold = nitterSettings.Value.GetProperty("followersThreshold").GetInt32();
                followersThreshold2 = nitterSettings.Value.GetProperty("followersThreshold2").GetInt32();
                followersThreshold3 = nitterSettings.Value.GetProperty("followersThreshold3").GetInt32();
                twitterFollowersThreshold = nitterSettings.Value.GetProperty("twitterFollowersThreshold").GetInt32();
                postNitterDelay = nitterSettings.Value.GetProperty("postnitterdelay").GetInt32();
            }
            var twitterUser = await _twitterUserService.GetUserAsync(username);
            if (user.StatusesCount == -1)
            {
                extractedTweets = await _tweetFromGraphql2024.GetTimelineAsync(user, userId, fromTweetId);
                source = "Vanilla";
            }
            else if (user.Followers > followersThreshold0)
            {
                extractedTweets = await TweetsFromSidecar2(user, fromTweetId, true);
                source = "Sidecar (with replies)";
                await Task.Delay(postNitterDelay);
            }
            else if (user.StatusesCount != twitterUser.StatusCount && user.Followers > followersThreshold3)
            {
                extractedTweets = await TweetsFromSidecar2(user, fromTweetId, true);
                source = "Sidecar (with replies)";
                await Task.Delay(postNitterDelay);
            }
            else if (user.StatusesCount != twitterUser.StatusCount && user.Followers > followersThreshold2)
            {
                extractedTweets = await TweetsFromSidecar2(user, fromTweetId, false);
                source = "Sidecar (without replies)";
                await Task.Delay(postNitterDelay);
            }
            else if (user.StatusesCount != twitterUser.StatusCount && user.Followers > followersThreshold && twitterUser.FollowersCount > twitterFollowersThreshold)
            {
                extractedTweets = await TweetFromNitter(user, fromTweetId, false, false);
                source = "Nitter";
                await Task.Delay(postNitterDelay);
            }
            else
            {
                extractedTweets = await _tweetFromGraphql2024.GetTimelineAsync(user, userId, fromTweetId);
                source = "Vanilla";
            }
            
            extractedTweets = extractedTweets.OrderByDescending(x => x.Id).Where(x => x.IdLong > fromTweetId).ToList();

            await _twitterUserDal.UpdateTwitterStatusesCountAsync(username, twitterUser.StatusCount);
            await _twitterUserDal.UpdateUserExtradataAsync(username, "statusesCount", twitterUser.StatusCount);
            _newTweets.Add(extractedTweets.Count,
                new KeyValuePair<string, object>("source", source)
            );
            return extractedTweets.ToArray();
        }
        
        private async Task<List<ExtractedTweet>> TweetsFromSidecar2(SyncUser user, long fromId, bool withReplies)
        {
            try
            {
                var tweets = new List<ExtractedTweet>();
                string username = String.Empty;
                string password = String.Empty;

                var candidates = await _twitterUserDal.GetTwitterCrawlUsersAsync(_instanceSettings.MachineName);
                Random.Shared.Shuffle(candidates);
                foreach (var account in candidates)
                {
                    username = account.Acct;
                    password = account.Password;
                }


                using var client = _httpClientFactory.CreateClient();
                string endpoint;
                if (withReplies)
                    endpoint = "postbyuserwithreplies";
                else
                    endpoint = "postbyuser";
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    $"http://localhost:5000/twitter/{endpoint}2/{user.TwitterUserId}");
                request.Headers.TryAddWithoutValidation("dotmakeup-user", username);
                request.Headers.TryAddWithoutValidation("dotmakeup-password", password);
                
                var httpResponse = await client.SendAsync(request);

                if (httpResponse.StatusCode != HttpStatusCode.OK)
                {
                    _apiCalled.Add(1, new KeyValuePair<string, object>("api", "twitter_sidecar_timeline"),
                        new KeyValuePair<string, object>("result", "5xx"),
                        new KeyValuePair<string, object>("endpoint", endpoint)
                    );
                    return new List<ExtractedTweet>();
                }
                _apiCalled.Add(1, new KeyValuePair<string, object>("api", "twitter_sidecar_timeline"),
                    new KeyValuePair<string, object>("result", "2xx"),
                    new KeyValuePair<string, object>("endpoint", endpoint)
                );
                
                var c = await httpResponse.Content.ReadAsStringAsync();
                tweets = JsonSerializer.Deserialize<List<ExtractedTweet>>(c, _serializerOptions);
                var tweetsDocument = JsonDocument.Parse(c);
                
                for (var i = 0; i < tweets.Count; i++)
                {
                    tweets[i] = await ExpandShortLinks(tweets[i]);
                    tweets[i] = CleanupText(tweets[i]);
                }
                
                return tweets;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new List<ExtractedTweet>();
            }
        }

        private async Task<List<ExtractedTweet>> TweetFromNitter(SyncUser user, long fromId, bool withReplies,
            bool lowtrust)
        {
            // https://status.d420.de/
            var nitterSettings = await _settings.Get("nitter");
            if (nitterSettings is null)
                return new List<ExtractedTweet>();


            var requester = new DefaultHttpRequester();
            string useragent;
            if (lowtrust && nitterSettings.Value.TryGetProperty("useragent", out JsonElement useragentElement))
                useragent = useragentElement.GetString();
            else
                useragent = Useragent;
            requester.Headers["User-Agent"] = useragent;
            requester.Headers["Accept"] =
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8";
            requester.Headers["Accept-Encoding"] = "gzip, deflate";
            requester.Headers["Accept-Language"] = "en-US,en;q=0.5";
            var config = Configuration.Default.With(requester).WithDefaultLoader();
            var context = BrowsingContext.New(config);

            List<string> domains = new List<string>() { };
            var prop = (lowtrust) ? "lowtrustendpoints" : "endpoints";
            foreach (var d in nitterSettings.Value.GetProperty(prop).EnumerateArray())
            {
                domains.Add(d.GetString());
            }

            Random rnd = new Random();
            int randIndex = rnd.Next(domains.Count);
            var domain = domains[randIndex];
            string address;
            if (withReplies)
                address = $"https://{domain}/{user.Acct}/with_replies";
            else
                address = $"https://{domain}/{user.Acct}";
            var document = await context.OpenAsync(address);

            var cellSelector = ".tweet-link";
            var cells = document.QuerySelectorAll(cellSelector);
            var titles = cells.Select(m => m.GetAttribute("href"));

            _apiCalled.Add(1, new KeyValuePair<string, object>("api", "nitter"),
                new KeyValuePair<string, object>("success", titles.Any()),
                new KeyValuePair<string, object>("domain", domain)
            );
        

        List<ExtractedTweet> tweets = new List<ExtractedTweet>();
            string pattern = @".*\/([0-9]+)#m";
            Regex rg = new Regex(pattern);
            
            foreach (string title in titles)
            {
                MatchCollection matchedId = rg.Matches(title);
                var matchString = matchedId[0].Groups[1].Value;
                var match = Int64.Parse(matchString);

                if (match <= fromId)
                    continue;

                try
                {
                    //var tweet = await TweetFromSyndication(match);
                    var tweet = await GetTweetAsync(match);
                    if (tweet.Author.Acct != user.Acct)
                    {
                        if (!nitterSettings.Value.GetProperty("allowboosts").GetBoolean() || lowtrust)
                            continue;
                        
                        tweet.IsRetweet = true;
                        tweet.OriginalAuthor = tweet.Author;
                        tweet.Author = await _twitterUserService.GetUserAsync(user.Acct);
                        tweet.RetweetId = tweet.IdLong;
                        // Sadly not given by Nitter UI
                        var gen = new TwitterSnowflakeGenerator(1, 1);
                        tweet.Id = gen.NextId().ToString();
                    }
                    tweets.Add(tweet);
                }
                catch (Exception e)
                {
                    _logger.LogError($"error fetching tweet {match} from user {user.Acct}");
                }
                await Task.Delay(100);
            }
            
            return tweets;
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
        
        public async Task<ExtractedTweet> ExpandShortLinks(ExtractedTweet input)
        {
            try
            {
                // Regular expression to match t.co short links
                string pattern = @"https?://t\.co/[a-zA-Z0-9]+";
                Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
                
                MatchCollection matches = regex.Matches(input.MessageContent);

                using var client = _httpClientFactory.CreateClient();
                
                foreach (Match match in matches)
                {
                    HttpResponseMessage response = await client.GetAsync(match.ToString(), HttpCompletionOption.ResponseHeadersRead);
                    var longlink = response.RequestMessage.RequestUri.ToString();
                    input.MessageContent = input.MessageContent.Replace(match.ToString(), longlink);
                }
            } catch (Exception _) {}
            
            return input;
        }
        public ExtractedTweet CleanupText(ExtractedTweet input)
        {
            if (input.MessageContent.StartsWith(".@"))
                input.MessageContent = input.MessageContent.Remove(0, 1);
            
            // Regular expression to match media links
            string pattern = @" https?://x\.com/[a-zA-Z0-9]+/status/[0-9]+/(video|photo)/[0-9]+";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
            
            MatchCollection matches = regex.Matches(input.MessageContent);

            foreach (Match match in matches)
            {
                input.MessageContent = input.MessageContent.Replace(match.ToString(), "");
            }

            if (input.QuotedAccount is not null && input.QuotedStatusId is not null)
            {
                input.MessageContent = Regex.Replace(input.MessageContent, Regex.Escape($"https://twitter.com/{input.QuotedAccount}/status/{input.QuotedStatusId}"), "", RegexOptions.IgnoreCase);
                input.MessageContent = Regex.Replace(input.MessageContent, Regex.Escape($"https://x.com/{input.QuotedAccount}/status/{input.QuotedStatusId}"), "", RegexOptions.IgnoreCase);
            }
            
            return input;
        }
    }
}

public class TwitterSocialMediaUserConverter : JsonConverter<SocialMediaUser>
{
    public override SocialMediaUser? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<TwitterUser>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, SocialMediaUser value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
