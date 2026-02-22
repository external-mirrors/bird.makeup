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
        if (!httpResponse.IsSuccessStatusCode)
        {
            if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                _logger.LogError("Error retrieving tweet {statusId}; refreshing client", statusId);
            else
                _logger.LogWarning("Syndication returned {StatusCode} for tweet {StatusId}", httpResponse.StatusCode, statusId);
            return null;
        }

        var responseContent = await httpResponse.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(responseContent))
            return null;
        using var tweet = JsonDocument.Parse(responseContent);
        var root = tweet.RootElement;

        if (!root.TryGetProperty("text", out var textElement))
            return null;

        string messageContent = textElement.GetString() ?? string.Empty;
        
        if (root.TryGetProperty("note_tweet", out var noteTweet))
        {
            if (noteTweet.TryGetProperty("text", out var noteText) && noteText.ValueKind == JsonValueKind.String)
                messageContent = noteText.GetString();
        }
        
        long favoriteCount = TryReadLong(root, "favorite_count");
        long shareCount = TryReadLong(root, "retweet_count");
        long replyCount = TryReadLong(root, "conversation_count");
        long viewCount = 0;
        if (root.TryGetProperty("ext_views", out var extViews) && extViews.TryGetProperty("count", out var extViewsCount))
            viewCount = ReadJsonLong(extViewsCount);
        if (viewCount == 0 && root.TryGetProperty("video", out var videoNode) && videoNode.TryGetProperty("viewCount", out var videoViews))
            viewCount = ReadJsonLong(videoViews);
        favoriteCount = NormalizeLikeCount(favoriteCount, shareCount, replyCount, viewCount);

        if (!root.TryGetProperty("user", out var userNode) ||
            !userNode.TryGetProperty("screen_name", out var usernameElement))
            return null;
        
        string username = usernameElement.GetString()?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(username))
            return null;
        string name = null;
        if (userNode.TryGetProperty("name", out var nameElement))
        {
            name = nameElement.GetString();
        }
        
        List<ExtractedMedia> Media = new();

        bool isReply = root.TryGetProperty("parent", out _);
        string inReplyTo = null;
        long? inReplyToId = null;
        bool isThread = false;
        if (isReply)
        {
            if (root.TryGetProperty("in_reply_to_screen_name", out var inReplyToScreenName) &&
                inReplyToScreenName.ValueKind == JsonValueKind.String)
                inReplyTo = inReplyToScreenName.GetString();
            
            if (root.TryGetProperty("in_reply_to_status_id_str", out var inReplyToStatusId) &&
                inReplyToStatusId.ValueKind == JsonValueKind.String &&
                long.TryParse(inReplyToStatusId.GetString(), out var parsedReplyId))
                inReplyToId = parsedReplyId;
            
            isThread = !string.IsNullOrWhiteSpace(inReplyTo) &&
                       string.Equals(username, inReplyTo, StringComparison.OrdinalIgnoreCase);
        }

        if (root.TryGetProperty("entities", out var entities))
        {
            if (entities.TryGetProperty("urls", out var urls))
            {
                foreach (JsonElement url in urls.EnumerateArray())
                {
                    if (!url.TryGetProperty("url", out var urlTcoNode) ||
                        !url.TryGetProperty("expanded_url", out var urlOriginalNode))
                        continue;
                    var urlTCO = urlTcoNode.GetString();
                    var urlOriginal = urlOriginalNode.GetString();
                    if (string.IsNullOrWhiteSpace(urlTCO) || string.IsNullOrWhiteSpace(urlOriginal))
                        continue;

                    messageContent = messageContent.Replace(urlTCO, urlOriginal, StringComparison.Ordinal);
                }
            }
            
            if (entities.TryGetProperty("media", out var mediaEntity))
            {
                foreach (JsonElement mediaElement in mediaEntity.EnumerateArray())
                {
                    if (!mediaElement.TryGetProperty("url", out var urlNode))
                        continue;
                    var urlTCO = urlNode.GetString();
                    if (string.IsNullOrWhiteSpace(urlTCO))
                        continue;

                    messageContent = messageContent.Replace(urlTCO, string.Empty, StringComparison.Ordinal);
                }
            }
        }
        
        if (root.TryGetProperty("mediaDetails", out var mediaDetails))
        {
            foreach (var mediaElement in mediaDetails.EnumerateArray())
            {
                    if (!mediaElement.TryGetProperty("media_url_https", out var mediaUrlNode))
                        continue;
                    var url = mediaUrlNode.GetString();
                    if (string.IsNullOrWhiteSpace(url))
                        continue;
                    var type = mediaElement.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : null;
                    string altText = null;
                    if (mediaElement.TryGetProperty("ext_alt_text", out var altTextNode))
                        altText = altTextNode.GetString();
                    string returnType = null;

                    if (type == "photo")
                    {
                        returnType = "image/jpeg";
                    }
                    else if (type == "video" || type == "animated_gif")
                    {
                        returnType = "video/mp4";
                        var bitrate = -1;
                        if (mediaElement.TryGetProperty("video_info", out var videoInfo) &&
                            videoInfo.TryGetProperty("variants", out var variants))
                        {
                            foreach (JsonElement v in variants.EnumerateArray())
                            {
                                if (!v.TryGetProperty("content_type", out var contentTypeNode) ||
                                    contentTypeNode.GetString() != "video/mp4")
                                    continue;
                                int vBitrate = v.TryGetProperty("bitrate", out var bitrateNode) ? bitrateNode.GetInt32() : 0;
                                if (vBitrate > bitrate && v.TryGetProperty("url", out var videoUrlNode))
                                {
                                    bitrate = vBitrate;
                                    url = videoUrlNode.GetString();
                                }
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

        string quoteTweetId = null;
        string quoteTweetAcct = null;
        bool quoteAccountInferredFromMessage = false;
        if (root.TryGetProperty("quoted_tweet", out var qt))
        {
            if (qt.TryGetProperty("id_str", out var quoteIdNode))
                quoteTweetId = quoteIdNode.GetString();
            if (qt.TryGetProperty("user", out var quoteUserNode) &&
                quoteUserNode.TryGetProperty("screen_name", out var quoteScreenNameNode))
                quoteTweetAcct = quoteScreenNameNode.GetString()?.ToLowerInvariant();
        }
        if (!string.IsNullOrWhiteSpace(quoteTweetId) &&
            TryExtractQuotedAccountFromMessage(messageContent, quoteTweetId, out var quoteAcctFromMessage))
        {
            if (!string.Equals(quoteTweetAcct, quoteAcctFromMessage, StringComparison.OrdinalIgnoreCase))
                quoteAccountInferredFromMessage = true;
            quoteTweetAcct = quoteAcctFromMessage;
        }
        if (!string.IsNullOrWhiteSpace(quoteTweetId) && !quoteAccountInferredFromMessage)
        {
            if (!string.IsNullOrWhiteSpace(quoteTweetAcct))
            {
                messageContent = Regex.Replace(messageContent, Regex.Escape($"https://twitter.com/{quoteTweetAcct}/status/{quoteTweetId}") + "$", "", RegexOptions.IgnoreCase);
                messageContent = Regex.Replace(messageContent, Regex.Escape($"https://x.com/{quoteTweetAcct}/status/{quoteTweetId}") + "$", "", RegexOptions.IgnoreCase);
            }
            messageContent = Regex.Replace(messageContent, @"https?://(twitter|x)\.com/[a-zA-Z0-9_]+/status/" + Regex.Escape(quoteTweetId) + "$", "", RegexOptions.IgnoreCase);
        }

        messageContent = Regex.Replace(messageContent, @" ?https?://(twitter|x)\.com/[a-zA-Z0-9_]+/status/[0-9]+/(video|photo)/[0-9]+$", "", RegexOptions.IgnoreCase);

        var author = new TwitterUser()
        {
            Acct = username,
            Name = name,
        };

        var createdaAt = DateTime.UtcNow;
        if (root.TryGetProperty("created_at", out var createdAtNode) &&
            createdAtNode.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(createdAtNode.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedCreatedAt))
        {
            createdaAt = parsedCreatedAt;
        }
        
        Poll poll = null;
        if (root.TryGetProperty("card", out var cardDoc))
        {
            DateTime endDate = DateTime.Now;
            Dictionary<char, string> labels = new Dictionary<char, string>();
            Dictionary<char, long> counts = new Dictionary<char, long>();
            string type = cardDoc.TryGetProperty("name", out var typeNode) ? typeNode.GetString() : string.Empty;
            if (cardDoc.TryGetProperty("binding_values", out var bindingValues))
            {
                foreach (JsonProperty val in bindingValues.EnumerateObject())
                {
                    var key = val.Name;
                    if (key == "end_datetime_utc")
                    {
                        if (val.Value.TryGetProperty("string_value", out var endDateNode) &&
                            DateTime.TryParse(endDateNode.GetString(), out var parsedEndDate))
                            endDate = parsedEndDate;
                    }
                    else if (key.StartsWith("choice") && key.EndsWith("_label"))
                    {
                        if (!val.Value.TryGetProperty("string_value", out var entryLabelNode))
                            continue;
                        var entryLabel = entryLabelNode.GetString();
                        char entryNumber = key[6];
                        labels.TryAdd(entryNumber, entryLabel);
                    }
                    else if (key.StartsWith("choice") && key.EndsWith("_count"))
                    {
                        if (!val.Value.TryGetProperty("string_value", out var entryCountNode))
                            continue;
                        if (!long.TryParse(entryCountNode.GetString(), out var entryCount))
                            continue;
                        char entryNumber = key[6];
                        counts.TryAdd(entryNumber, entryCount);
                    }
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
            ShareCount = shareCount,
            Poll = poll,
        };
        
        extractedTweet = await _tweetsService.ExpandShortLinks(extractedTweet);
        
        return extractedTweet;
    }

    private static long ReadJsonLong(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Number && node.TryGetInt64(out var numberValue))
            return numberValue;
        if (node.ValueKind == JsonValueKind.String && long.TryParse(node.GetString(), out var stringValue))
            return stringValue;
        return 0;
    }
    
    private static long TryReadLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return 0;
        return ReadJsonLong(value);
    }

    private static long NormalizeLikeCount(long likes, long shares, long replies, long views)
    {
        var fromShares = shares > 0 ? shares * 10 : 0;
        var fromReplies = replies > 0 ? replies * 200 : 0;
        var fromViews = views > 0 ? (long)Math.Round(views * 0.12d) : 0;
        return Math.Max(likes, Math.Max(fromShares, Math.Max(fromReplies, fromViews)));
    }

    private static bool TryExtractQuotedAccountFromMessage(string content, string quoteStatusId, out string quoteAccount)
    {
        quoteAccount = null;
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(quoteStatusId))
            return false;
        
        var match = Regex.Match(content, @"https?://(?:x\.com|twitter\.com)/([A-Za-z0-9_]+)/status/" + Regex.Escape(quoteStatusId), RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        quoteAccount = match.Groups[1].Value.ToLowerInvariant();
        return true;
    }
}
