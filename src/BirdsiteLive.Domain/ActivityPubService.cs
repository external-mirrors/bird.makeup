using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BirdsiteLive.ActivityPub;
using BirdsiteLive.ActivityPub.Converters;
using BirdsiteLive.ActivityPub.Models;
using BirdsiteLive.Common.Settings;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Domain
{
    public interface IActivityPubService
    {
        Task<Actor> GetUser(string actor, string objectId);
        Task<HttpStatusCode> PostDataAsync<T>(T data, string targetHost, string actorUrl, string inbox = null) where T : Activity;
        Task PostNewActivity(ActivityCreateNote note, string username, string noteId, string targetHost,
            string targetInbox);

        ActivityAcceptFollow BuildAcceptFollow(ActivityFollow activity);
    }

    public class ActivityPubService : IActivityPubService
    {
        private readonly InstanceSettings _instanceSettings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ICryptoService _cryptoService;
        private readonly ILogger<ActivityPubService> _logger;

        #region Ctor
        public ActivityPubService(ICryptoService cryptoService, InstanceSettings instanceSettings, IHttpClientFactory httpClientFactory, ILogger<ActivityPubService> logger)
        {
            _cryptoService = cryptoService;
            _instanceSettings = instanceSettings;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }
        #endregion

        public async Task<Actor> GetUser(string actor, string objectId)
        {
            var userUri = new System.Uri(objectId);
            var httpDate = DateTime.Now.ToString("r");
            var sig64 = await _cryptoService.SignString($"(request-target): get {userUri.AbsolutePath}\nhost: {userUri.Host}\ndate: {httpDate}");
            var sigHeader = "keyId=\"" + actor + "#main-key\",algorithm=\"rsa-sha256\",headers=\"(request-target) host date\",signature=\"" + sig64 + "\"";

            var httpClient = _httpClientFactory.CreateClient();
            var httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = userUri,
                Headers =
                {
                    { "Host", userUri.Host },
                    { "Date", httpDate },
                    { "Accept", "application/activity+json" },
                    { "User-Agent", "System.Net (Bird.MakeUp; +" + _instanceSettings.Domain + ")" },
                    { "Signature", sigHeader  }
                }
            };

            var result = await httpClient.SendAsync(httpRequestMessage);

            if (result.StatusCode == HttpStatusCode.Gone)
                throw new FollowerIsGoneException();

            result.EnsureSuccessStatusCode();

            var content = await result.Content.ReadAsStringAsync();

            var resultingActor = JsonSerializer.Deserialize<Actor>(content);
            if (string.IsNullOrWhiteSpace(resultingActor.url)) resultingActor.url = objectId;
            return resultingActor;
        }

        public async Task PostNewActivity(ActivityCreateNote noteActivity, string username, string noteId, string targetHost, string targetInbox)
        {
            try
            {
                var actor = UrlFactory.GetActorUrl(_instanceSettings.Domain, username);

                await PostDataAsync(noteActivity, targetHost, actor, targetInbox);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error sending {Username} post ({NoteId}) to {Host}{Inbox}", username, noteId, targetHost, targetInbox);
                throw;
            }
        }

        public ActivityAcceptFollow BuildAcceptFollow(ActivityFollow activity)
        {
            var acceptFollow = new ActivityAcceptFollow()
            {
                context = "https://www.w3.org/ns/activitystreams",
                id = $"{activity.apObject}#accepts/follows/{Guid.NewGuid()}",
                type = "Accept",
                to = [ activity.actor ],
                actor = activity.apObject,
                apObject = new ActivityFollow()
                {
                    id = activity.id,
                    type = activity.type,
                    actor = activity.actor,
                    to = activity.to,
                    apObject = activity.apObject
                }
            };
            return acceptFollow;
        }
        public async Task<HttpRequestMessage> BuildRequest<T>(T data, string targetHost, string actorUrl, HttpMethod method,
            string inbox = null)
        {
            var usedInbox = $"/inbox";
            if (!string.IsNullOrWhiteSpace(inbox))
                usedInbox = inbox;

            var json = JsonSerializer.Serialize(data);

            var date = DateTime.UtcNow.ToUniversalTime();
            var httpDate = date.ToString("r");

            var digest = CryptoService.ComputeSha256Hash(json);

            var signature = await _cryptoService.SignAndGetSignatureHeader(date, actorUrl, targetHost, digest, usedInbox);

            var httpRequestMessage = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri($"https://{targetHost}{usedInbox}"),
                Headers =
                {
                    { "Host", targetHost },
                    { "Date", httpDate },
                    { "Signature", signature },
                    { "Digest", $"SHA-256={digest}" },
                    { "User-Agent", "System.Net (Bird.MakeUp; +" + _instanceSettings.Domain + ")"}
                },
            };
            if (data is not null)
            {
                httpRequestMessage.Content = new StringContent(json, Encoding.UTF8, "application/ld+json");
                httpRequestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/activity+json");
            }

            return httpRequestMessage;
        }

        public async Task<HttpStatusCode> PostDataAsync<T>(T data, string targetHost, string actorUrl, string inbox = null) where T : Activity
        {
            var httpRequestMessage = await BuildRequest(data, targetHost, actorUrl, HttpMethod.Post, inbox);

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                var content = await httpRequestMessage.Content.ReadAsStringAsync();
                _logger.LogTrace("Sending AP message to actor {TargetHost}. Content: {Content}", 
                    targetHost, content);
            }
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(2);
            
            var response = await client.SendAsync(httpRequestMessage);
            response.EnsureSuccessStatusCode();

            return response.StatusCode;
        }
    }
}