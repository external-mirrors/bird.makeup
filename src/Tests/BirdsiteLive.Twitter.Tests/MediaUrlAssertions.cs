using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Twitter.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BirdsiteLive.Twitter.Tests
{
    internal static class MediaUrlAssertions
    {
        private static readonly HttpClient ProbeClient = CreateProbeClient();
        private static readonly ConcurrentDictionary<string, Task<ResolvedMediaUrl>> ProbeCache =
            new(StringComparer.Ordinal);

        public static async Task AssertValidMediaUrls(ExtractedTweet tweet, string context)
        {
            if (tweet.Media == null)
                return;

            for (var i = 0; i < tweet.Media.Length; i++)
            {
                await AssertValidMediaUrl(tweet.Media[i], $"{context} media[{i}]");
            }
        }

        public static async Task AssertValidMediaUrls(ExtractedTweet[] tweets, string context)
        {
            for (var i = 0; i < tweets.Length; i++)
            {
                await AssertValidMediaUrls(tweets[i], $"{context} tweet[{i}]");
            }
        }

        private static HttpClient CreateProbeClient()
        {
            var client = new HttpClient(new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.All
            })
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("bird.makeup-twitter-tests/1.0");
            return client;
        }

        private static async Task AssertValidMediaUrl(ExtractedMedia media, string context)
        {
            var url = media.Url;

            Assert.IsFalse(string.IsNullOrWhiteSpace(url), $"{context} url should not be empty");
            Assert.IsTrue(
                Uri.TryCreate(url, UriKind.Absolute, out var uri),
                $"{context} url should be an absolute URI. actual={url}");
            Assert.AreEqual(
                Uri.UriSchemeHttps,
                uri.Scheme,
                $"{context} url should use https. actual={url}");
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(uri.Host),
                $"{context} url host should not be empty. actual={url}");
            Assert.IsTrue(
                string.Equals(uri.Host, "twimg.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(".twimg.com", StringComparison.OrdinalIgnoreCase),
                $"{context} url host should be a twimg.com host. actual={url}");
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(uri.AbsolutePath) || uri.AbsolutePath == "/",
                $"{context} url path should not be empty. actual={url}");

            var resolvedUrl = await ProbeMediaUrlAsync(url);

            Assert.IsTrue(
                resolvedUrl.StatusCode == HttpStatusCode.OK || resolvedUrl.StatusCode == HttpStatusCode.PartialContent,
                $"{context} url should resolve successfully. status={(int)resolvedUrl.StatusCode} contentType={resolvedUrl.ContentType ?? "<missing>"} actual={url}");
            Assert.IsTrue(
                IsCompatibleMediaType(media.MediaType, resolvedUrl.ContentType),
                $"{context} url should resolve to media compatible with {media.MediaType ?? "<missing>"}. contentType={resolvedUrl.ContentType ?? "<missing>"} actual={url}");
        }

        private static async Task<ResolvedMediaUrl> ProbeMediaUrlAsync(string url)
        {
            var probeTask = ProbeCache.GetOrAdd(url, ProbeMediaUrlCoreAsync);

            try
            {
                return await probeTask.ConfigureAwait(false);
            }
            catch
            {
                ProbeCache.TryRemove(url, out _);
                throw;
            }
        }

        private static async Task<ResolvedMediaUrl> ProbeMediaUrlCoreAsync(string url)
        {
            Exception? lastError = null;

            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Range = new RangeHeaderValue(0, 0);

                    using var response = await ProbeClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                        .ConfigureAwait(false);

                    return new ResolvedMediaUrl(
                        response.StatusCode,
                        response.Content.Headers.ContentType?.MediaType);
                }
                catch (HttpRequestException ex)
                {
                    lastError = ex;
                }
                catch (TaskCanceledException ex)
                {
                    lastError = ex;
                }
            }

            Assert.Fail($"{url} probe failed: {lastError?.Message ?? "unknown error"}");
            throw new InvalidOperationException("Unreachable");
        }

        private static bool IsCompatibleMediaType(string expectedMediaType, string? actualContentType)
        {
            if (string.IsNullOrWhiteSpace(actualContentType))
                return false;

            if (IsHlsPlaylist(actualContentType))
            {
                return !string.IsNullOrWhiteSpace(expectedMediaType) &&
                       (expectedMediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
                        expectedMediaType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase));
            }

            if (string.IsNullOrWhiteSpace(expectedMediaType))
            {
                return actualContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                       actualContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
                       actualContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);
            }

            var slashIndex = expectedMediaType.IndexOf('/');
            if (slashIndex <= 0)
                return false;

            var expectedPrefix = expectedMediaType[..(slashIndex + 1)];
            return actualContentType.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHlsPlaylist(string contentType)
        {
            return string.Equals(contentType, "application/x-mpegURL", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(contentType, "application/vnd.apple.mpegurl", StringComparison.OrdinalIgnoreCase);
        }

        private sealed record ResolvedMediaUrl(HttpStatusCode StatusCode, string? ContentType);
    }
}
