using System;
using BirdsiteLive.Twitter.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BirdsiteLive.Twitter.Tests
{
    internal static class MediaUrlAssertions
    {
        public static void AssertValidMediaUrls(ExtractedTweet tweet, string context)
        {
            if (tweet.Media == null)
                return;

            for (var i = 0; i < tweet.Media.Length; i++)
            {
                AssertValidMediaUrl(tweet.Media[i].Url, $"{context} media[{i}]");
            }
        }

        public static void AssertValidMediaUrls(ExtractedTweet[] tweets, string context)
        {
            for (var i = 0; i < tweets.Length; i++)
            {
                AssertValidMediaUrls(tweets[i], $"{context} tweet[{i}]");
            }
        }

        private static void AssertValidMediaUrl(string url, string context)
        {
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
        }
    }
}
