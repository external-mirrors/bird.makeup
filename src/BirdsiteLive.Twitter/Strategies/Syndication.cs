using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Twitter.Models;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Twitter.Strategies;

public class Syndication : ITweetExtractor
{
    private readonly ITwitterTweetsService _tweetsService;
    private readonly InstanceSettings _instanceSettings;
    private readonly ILogger<TwitterService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    public Syndication(ITwitterTweetsService tweetsService, IHttpClientFactory httpClientFactory, InstanceSettings instanceSettings, ILogger<TwitterService> logger)
    {
        _tweetsService = tweetsService;
        _httpClientFactory = httpClientFactory;
        _instanceSettings = instanceSettings;
        _logger = logger;
        
    }
    
    
    public async Task<ExtractedTweet> GetTweetAsync(long statusId)
    {
        JsonDocument tweet;
        var client = _httpClientFactory.CreateClient();
        
        using var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"https://cdn.syndication.twimg.com/tweet-result?features=tfw_legacy_timeline_sunset:true&id={statusId}&lang=en&token=3ykp5xr72qv"),
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
        var responseContent = await httpResponse.Content.ReadAsStringAsync();
        tweet = JsonDocument.Parse(responseContent);

        long favoriteCount = 0;
        if (tweet.RootElement.TryGetProperty("favorite_count", out var favoriteCountElement))
        {
            favoriteCount = favoriteCountElement.GetInt64();
        }

        string username;
        string name = null;
        string messageContent = tweet.RootElement.GetProperty("text").GetString();
        
        JsonElement noteTweet;
        if (tweet.RootElement.TryGetProperty("note_tweet", out noteTweet))
        {
            if (noteTweet.TryGetProperty("text", out var noteText))
            {
                messageContent = noteText.GetString();
            }
        }

        try
        {
            username = tweet.RootElement.GetProperty("user").GetProperty("screen_name").GetString().ToLower();
            if (tweet.RootElement.GetProperty("user").TryGetProperty("name", out var nameElement))
            {
                name = nameElement.GetString();
            }
        }
        catch (KeyNotFoundException _)
        {
            return null;
        }
        
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
                foreach (JsonElement mediaElement in mediaEntity.EnumerateArray())
                {
                    var urlTCO = mediaElement.GetProperty("url").GetString();

                    messageContent = messageContent.Replace(urlTCO, String.Empty);
                }
            }
        }
        
        JsonElement mediaDetails;
        if (tweet.RootElement.TryGetProperty("mediaDetails", out mediaDetails))
        {
            foreach (var mediaElement in mediaDetails.EnumerateArray())
            {
                    var url = mediaElement.GetProperty("media_url_https").GetString();
                    var type = mediaElement.GetProperty("type").GetString();
                    string altText = null;
                    if (mediaElement.TryGetProperty("ext_alt_text", out _))
                        altText = mediaElement.GetProperty("ext_alt_text").GetString();
                    string returnType = null;

                    if (type == "photo")
                    {
                        returnType = "image/jpeg";
                    }
                    else if (type == "video" || type == "animated_gif")
                    {
                        returnType = "video/mp4";
                        var bitrate = -1;
                        foreach (JsonElement v in mediaElement.GetProperty("video_info").GetProperty("variants").EnumerateArray())
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
        string quoteTweetId = null;
        string quoteTweetAcct = null;
        if (isQT)
        {
            quoteTweetId = qt.GetProperty("id_str").GetString();
            quoteTweetAcct = qt.GetProperty("user").GetProperty("screen_name").GetString().ToLower();
            
            string quoteTweetLink = $"https://{_instanceSettings.Domain}/@{quoteTweetAcct}/{quoteTweetId}";

            messageContent = Regex.Replace(messageContent, Regex.Escape($"https://twitter.com/{quoteTweetAcct}/status/{quoteTweetId}") + "$", "", RegexOptions.IgnoreCase);
            messageContent = Regex.Replace(messageContent, Regex.Escape($"https://x.com/{quoteTweetAcct}/status/{quoteTweetId}") + "$", "", RegexOptions.IgnoreCase);
            // messageContent = messageContent + "\n\n" + quoteTweetLink;
            
        }

        messageContent = Regex.Replace(messageContent, @" ?https?://(twitter|x)\.com/[a-zA-Z0-9_]+/status/[0-9]+/(video|photo)/[0-9]+$", "", RegexOptions.IgnoreCase);

        var author = new TwitterUser()
        {
            Acct = username,
            Name = name,
        };

        var createdaAt = DateTime.Parse(tweet.RootElement.GetProperty("created_at").GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind);
        
        Poll poll = null;
        JsonElement cardDoc;
        if (tweet.RootElement.TryGetProperty("card", out cardDoc))
        {
            DateTime endDate = DateTime.Now;
            Dictionary<char, string> labels = new Dictionary<char, string>();
            Dictionary<char, long> counts = new Dictionary<char, long>();
            string type = cardDoc.GetProperty("name").GetString();
            foreach (JsonProperty val in cardDoc.GetProperty("binding_values")
                         .EnumerateObject())
            {
                var key = val.Name;
                if (key == "end_datetime_utc")
                {
                    var endDateString = val.Value.GetProperty("string_value").GetString();
                    endDate = DateTime.Parse(endDateString);
                }
                else if (key.StartsWith("choice") && key.EndsWith("_label"))
                {
                    var entryLabel = val.Value.GetProperty("string_value").GetString();
                    char entryNumber = key[6];
                    labels.TryAdd(entryNumber, entryLabel);
                }
                else if (key.StartsWith("choice") && key.EndsWith("_count"))
                {
                    var entryLabel = val.Value.GetProperty("string_value").GetString();
                    char entryNumber = key[6];
                    counts.TryAdd(entryNumber, long.Parse(entryLabel));
                }
            }

            if (labels.Count > 0)
            {
                var optionsList = labels.OrderBy(x => x.Key).Select(x => x.Value).Zip(counts.OrderBy(x => x.Key).Select(x => x.Value)).ToList();
                poll = new Poll()
                {
                    endTime = endDate,
                    options = optionsList,
                };
            }

            if (!type.StartsWith("poll"))
                poll = null;
        }

        if (messageContent.StartsWith(".@"))
            messageContent = messageContent.Remove(0, 1);

        
        var extractedTweet = new ExtractedTweet()
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
            QuotedAccount = quoteTweetAcct,
            QuotedStatusId = quoteTweetId,
            LikeCount = favoriteCount,
            Poll = poll,
        };
        
        extractedTweet = await _tweetsService.ExpandShortLinks(extractedTweet);
        
        return extractedTweet;
    }
}