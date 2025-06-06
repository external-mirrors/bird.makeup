using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
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
using HttpMethod = System.Net.Http.HttpMethod;

namespace BirdsiteLive.Twitter
{
    public interface ITwitterTweetsService
    {
        Task<ExtractedTweet> GetTweetAsync(long statusId);
        Task<ExtractedTweet[]> GetTimelineAsync(SyncUser user, long fromTweetId = -1);
    }

    public class TwitterTweetsService : ITwitterTweetsService
    {
        static Meter _meter = new("DotMakeup", "1.0.0");
        static Counter<int> _apiCalled = _meter.CreateCounter<int>("dotmakeup_api_called_count");
        static Counter<int> _newTweets = _meter.CreateCounter<int>("dotmakeup_twitter_new_tweets_count");
        
        private readonly ITwitterAuthenticationInitializer _twitterAuthenticationInitializer;
        private readonly ICachedTwitterUserService _twitterUserService;
        private readonly ITwitterUserDal _twitterUserDal;
        private readonly ILogger<TwitterTweetsService> _logger;
        private readonly InstanceSettings _instanceSettings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IBrowsingContext _context;
        private readonly ISettingsDal _settings;
        private string Useragent = "Bird.makeup ( https://git.sr.ht/~cloutier/bird.makeup ) Bot";

        #region Ctor
        public TwitterTweetsService(ITwitterAuthenticationInitializer twitterAuthenticationInitializer, ICachedTwitterUserService twitterUserService, ITwitterUserDal twitterUserDal, InstanceSettings instanceSettings, IHttpClientFactory httpClientFactory, ISettingsDal settings, ILogger<TwitterTweetsService> logger)
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
        }
        #endregion


        public async Task<ExtractedTweet> GetTweetAsync(long statusId)
        {
            //return await TweetFromSyndication(statusId);

            var client = await _twitterAuthenticationInitializer.MakeHttpClient();


            string reqURL =
                "https://twitter.com/i/api/graphql/0hWvDhmW8YQ-S_ib3azIrw/TweetResultByRestId?variables=%7B%22tweetId%22%3A%221519480761749016577%22%2C%22withCommunity%22%3Afalse%2C%22includePromotedContent%22%3Afalse%2C%22withVoice%22%3Afalse%7D&features=%7B%22creator_subscriptions_tweet_preview_api_enabled%22%3Atrue%2C%22tweetypie_unmention_optimization_enabled%22%3Atrue%2C%22responsive_web_edit_tweet_api_enabled%22%3Atrue%2C%22graphql_is_translatable_rweb_tweet_is_translatable_enabled%22%3Atrue%2C%22view_counts_everywhere_api_enabled%22%3Atrue%2C%22longform_notetweets_consumption_enabled%22%3Atrue%2C%22responsive_web_twitter_article_tweet_consumption_enabled%22%3Afalse%2C%22tweet_awards_web_tipping_enabled%22%3Afalse%2C%22freedom_of_speech_not_reach_fetch_enabled%22%3Atrue%2C%22standardized_nudges_misinfo%22%3Atrue%2C%22tweet_with_visibility_results_prefer_gql_limited_actions_policy_enabled%22%3Atrue%2C%22longform_notetweets_rich_text_read_enabled%22%3Atrue%2C%22longform_notetweets_inline_media_enabled%22%3Atrue%2C%22responsive_web_graphql_exclude_directive_enabled%22%3Atrue%2C%22verified_phone_label_enabled%22%3Afalse%2C%22responsive_web_media_download_video_enabled%22%3Afalse%2C%22responsive_web_graphql_skip_user_profile_image_extensions_enabled%22%3Afalse%2C%22responsive_web_graphql_timeline_navigation_enabled%22%3Atrue%2C%22responsive_web_enhance_cards_enabled%22%3Afalse%7D";
            reqURL = reqURL.Replace("1519480761749016577", statusId.ToString());
            using var request = _twitterAuthenticationInitializer.MakeHttpRequest(new HttpMethod("GET"), reqURL, true);
            try
            {
                JsonDocument tweet;
                var httpResponse = await client.SendAsync(request);
                httpResponse.EnsureSuccessStatusCode();
                var c = await httpResponse.Content.ReadAsStringAsync();
                tweet = JsonDocument.Parse(c);


                var tweetInDoc = tweet.RootElement.GetProperty("data").GetProperty("tweetResult")
                    .GetProperty("result");
                
                var extract = await Extract( tweetInDoc );
                _apiCalled.Add(1, new KeyValuePair<string, object>("api", "twitter_tweet"),
                    new KeyValuePair<string, object>("result", "2xx")
                );
                return extract;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error retrieving tweet {TweetId}", statusId);
                _apiCalled.Add(1, new KeyValuePair<string, object>("api", "twitter_tweet"),
                    new KeyValuePair<string, object>("result", "5xx")
                );
                return await TweetFromSyndication(statusId);
            }
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
                extractedTweets = await TweetFromVanilla(user, userId, fromTweetId);
                source = "Vanilla";
            }
            else if (user.Followers > followersThreshold0)
            {
                extractedTweets = await TweetFromSidecar(user, fromTweetId, true);
                source = "Sidecar (with replies)";
                await Task.Delay(postNitterDelay);
            }
            else if (user.StatusesCount != twitterUser.StatusCount && user.Followers > followersThreshold3)
            {
                extractedTweets = await TweetFromSidecar(user, fromTweetId, true);
                source = "Sidecar (with replies)";
                await Task.Delay(postNitterDelay);
            }
            else if (user.StatusesCount != twitterUser.StatusCount && user.Followers > followersThreshold2)
            {
                extractedTweets = await TweetFromSidecar(user, fromTweetId, false);
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
                extractedTweets = await TweetFromVanilla(user, userId, fromTweetId);
                source = "Vanilla";
            }

            await _twitterUserDal.UpdateTwitterStatusesCountAsync(username, twitterUser.StatusCount);
            await _twitterUserDal.UpdateUserExtradataAsync(username, "statusesCount", twitterUser.StatusCount);
            _newTweets.Add(extractedTweets.Count,
                new KeyValuePair<string, object>("source", source)
            );
            return extractedTweets.ToArray();
        }

        private async Task<List<ExtractedTweet>> TweetFromVanilla(SyncUser user, long userId, long fromTweetId)
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

        private async Task<List<ExtractedTweet>> TweetFromSidecar(SyncUser user, long fromId, bool withReplies)
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
                    $"http://localhost:5000/twitter/{endpoint}/{user.TwitterUserId}");
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
                var tweetsDocument = JsonDocument.Parse(c);
                
                foreach (JsonElement title in tweetsDocument.RootElement.EnumerateArray())
                {
                    if (title.GetInt64() <= fromId)
                        continue;

                    try
                    {
                        //var tweet = await TweetFromSyndication(match);
                        var tweet = await GetTweetAsync(title.GetInt64());
                        if (tweet.Author.Acct != user.Acct)
                        {
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
                        _logger.LogError($"error fetching tweet {title.GetInt64()} from user {user.Acct}");
                    }
                    await Task.Delay(100);
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
        private async Task<ExtractedTweet> TweetFromSyndication(long statusId)
        {
            JsonDocument tweet;
            var client = _httpClientFactory.CreateClient();
            
            using var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"https://cdn.syndication.twimg.com/tweet-result?id={statusId}&lang=en&token=3ykp5xr72qv"),
            };
            //request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36");
            request.Headers.Add("User-Agent", "farts");
            //using var request = new HttpRequestMessage(new HttpMethod("GET"), reqURL);
            //using var request = _twitterAuthenticationInitializer.MakeHttpRequest(HttpMethod.Get, reqURL, false);
            
            using var httpResponse = await client.SendAsync(request);
            if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogError("Error retrieving tweet {statusId}; refreshing client", statusId);
            }

            httpResponse.EnsureSuccessStatusCode();
            var c = await httpResponse.Content.ReadAsStringAsync();
            tweet = JsonDocument.Parse(c);

            
            string messageContent = tweet.RootElement.GetProperty("text").GetString();
            string username = tweet.RootElement.GetProperty("user").GetProperty("screen_name").GetString().ToLower();
            List<ExtractedMedia> Media = new();

            JsonElement replyTo;
            bool isReply = tweet.RootElement.TryGetProperty("parent", out replyTo);
            string inReplyTo = null;
            long? inReplyToId = null;
            bool isThread = false;
            if (isReply)
            {
                inReplyTo = tweet.RootElement.GetProperty("in_reply_to_screen_name").GetString();
                inReplyToId = Int64.Parse(tweet.RootElement.GetProperty("in_reply_to_status_id_str").GetString());

                isThread = username == inReplyTo;
            }

            JsonElement entities;
            if (tweet.RootElement.TryGetProperty("entities", out entities))
            {
                JsonElement urls;
                if (entities.TryGetProperty("urls", out urls))
                {
                    foreach (JsonElement url in urls.EnumerateArray())
                    {
                        var urlTCO = url.GetProperty("url").GetString();
                        var urlOriginal = url.GetProperty("expanded_url").GetString();

                        messageContent = messageContent.Replace(urlTCO, urlOriginal);
                    }
                }
                
                JsonElement mediaEntity;
                if (entities.TryGetProperty("media", out mediaEntity))
                {
                    foreach (JsonElement media in mediaEntity.EnumerateArray())
                    {
                        var urlTCO = media.GetProperty("url").GetString();

                        messageContent = messageContent.Replace(urlTCO, String.Empty);
                    }
                }
            }
            
            JsonElement mediaDetails;
            if (tweet.RootElement.TryGetProperty("mediaDetails", out mediaDetails))
            {
                foreach (var media in mediaDetails.EnumerateArray())
                {
                        var url = media.GetProperty("media_url_https").GetString();
                        var type = media.GetProperty("type").GetString();
                        string altText = null;
                        if (media.TryGetProperty("ext_alt_text", out _))
                            altText = media.GetProperty("ext_alt_text").GetString();
                        string returnType = null;

                        if (type == "photo")
                        {
                            returnType = "image/jpeg";
                        }
                        else if (type == "video")
                        {
                            returnType = "video/mp4";
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
                        
                        var m = new ExtractedMedia()
                        {
                            Url = url,
                            MediaType = returnType,
                            AltText = altText,
                        };
                        Media.Add(m);
                }
            }

            JsonElement qt;
            bool isQT = tweet.RootElement.TryGetProperty("quoted_tweet", out qt);
            if (isQT)
            {
                string quoteTweetId = qt.GetProperty("id_str").GetString();
                string quoteTweetAcct = qt.GetProperty("user").GetProperty("screen_name").GetString();
                
                string quoteTweetLink = $"https://{_instanceSettings.Domain}/@{quoteTweetAcct.ToLower()}/{quoteTweetId}";

                messageContent = Regex.Replace(messageContent, Regex.Escape($"https://twitter.com/{quoteTweetAcct}/status/{quoteTweetId}"), "", RegexOptions.IgnoreCase);
                messageContent = Regex.Replace(messageContent, Regex.Escape($"https://x.com/{quoteTweetAcct}/status/{quoteTweetId}"), "", RegexOptions.IgnoreCase);
                messageContent = messageContent + "\n\n" + quoteTweetLink;
                
            }

            var author = new TwitterUser()
            {
                Acct = username,
            };

            var createdaAt = DateTime.Parse(tweet.RootElement.GetProperty("created_at").GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind);
            
            if (messageContent.StartsWith(".@"))
                messageContent = messageContent.Remove(0, 1);

            messageContent = await ExpandShortLinks(messageContent);
            
            return new ExtractedTweet()
            {
                MessageContent = messageContent.Trim(),
                Id = statusId.ToString(),
                IsReply = isReply,
                IsThread = isThread,
                IsRetweet = false,
                InReplyToAccount = inReplyTo,
                InReplyToStatusId = inReplyToId,
                Author = author,
                CreatedAt = createdaAt,
                Media = Media.Count() == 0 ? null : Media.ToArray(),
            };

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


            author = _twitterUserService.Extract(userDoc);
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
                OriginalAuthor = _twitterUserService.Extract(OriginalAuthorDoc); 
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

            MessageContent = await ExpandShortLinks(MessageContent);
            bool isQuoteTweet = tweetRes.GetProperty("legacy")
                    .GetProperty("is_quote_status").GetBoolean();

            if (isQuoteTweet)
            {

                string quoteTweetId = tweetRes.GetProperty("legacy")
                        .GetProperty("quoted_status_id_str").GetString();
                JsonElement quoteTweetAcctDoc = tweetRes
                    .GetProperty("quoted_status_result").GetProperty("result")
                    .GetProperty("core").GetProperty("user_results").GetProperty("result");
                TwitterUser QTauthor = _twitterUserService.Extract(quoteTweetAcctDoc);
                string quoteTweetAcct = QTauthor.Acct;
                //Uri test = new Uri(quoteTweetLink);
                //string quoteTweetAcct = test.Segments[1].Replace("/", "");
                //string quoteTweetId = test.Segments[3];
                
                string quoteTweetLink = $"https://{_instanceSettings.Domain}/@{quoteTweetAcct}/{quoteTweetId}";

                //MessageContent.Replace($"https://twitter.com/i/web/status/{}", "");
               // MessageContent = MessageContent.Replace($"https://twitter.com/{quoteTweetAcct}/status/{quoteTweetId}", "");
                MessageContent = MessageContent.Replace($"https://twitter.com/{quoteTweetAcct}/status/{quoteTweetId}", "", StringComparison.OrdinalIgnoreCase);
                MessageContent = MessageContent.Replace($"https://x.com/{quoteTweetAcct}/status/{quoteTweetId}", "", StringComparison.OrdinalIgnoreCase);
                
                //MessageContent = Regex.Replace(MessageContent, Regex.Escape($"https://twitter.com/{quoteTweetAcct}/status/{quoteTweetId}"), "", RegexOptions.IgnoreCase);
                MessageContent = MessageContent + "\n\n" + quoteTweetLink;
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
            };
       
            return extractedTweet;
         
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
        
        public async Task<string> ExpandShortLinks(string input)
        {
            try
            {
                // Regular expression to match t.co short links
                string pattern = @"https?://t\.co/[a-zA-Z0-9]+";
                Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
                
                MatchCollection matches = regex.Matches(input);

                using var client = _httpClientFactory.CreateClient();
                
                foreach (Match match in matches)
                {
                    HttpResponseMessage response = await client.GetAsync(match.ToString(), HttpCompletionOption.ResponseHeadersRead);
                    var longlink = response.RequestMessage.RequestUri.ToString();
                    input = input.Replace(match.ToString(), longlink);
                }
            } catch (Exception _) {}
            
            return input;
        }
    }
}
