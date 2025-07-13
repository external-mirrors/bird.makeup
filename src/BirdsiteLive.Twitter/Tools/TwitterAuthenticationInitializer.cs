using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using BirdsiteLive.Common.Settings;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using BirdsiteLive.DAL.Contracts;

namespace BirdsiteLive.Twitter.Tools
{
    public interface ITwitterAuthenticationInitializer
    {
        Task<HttpClient> MakeHttpClient();
        HttpRequestMessage MakeHttpRequest(HttpMethod m, string endpoint, bool addToken);
        Task RefreshClient(HttpRequestMessage client);
        Task<JsonDocument> Login();
    }

    public class TwitterAuthenticationInitializer : ITwitterAuthenticationInitializer
    {
        private readonly ILogger<TwitterService> _logger;
        private static bool _initialized;
        private readonly IHttpClientFactory _httpClientFactory;
        private ConcurrentDictionary<String, String> _token2 = new ConcurrentDictionary<string, string>();
        static Random rnd = new Random();
        private RateLimiter _rateLimiter;
        private const int _targetClients = 10;
        private InstanceSettings _instanceSettings;
        private ISettingsDal _settingsDal;
        private WebProxy _proxy;

        private readonly (string, string)[] _apiKeys = new[]
        {
            ("IQKbtAYlXLripLGPWd0HUA", "GgDYlkSvaPxGxC4X8liwpUoqKwwr3lCADbz8A7ADU"), // iPhone
            ("3nVuSoBZnx6U4vzUxf5w", "Bcs59EFbbsdF6Sl9Ng71smgStWEGwXXKSjYvPVt7qys"), // Android
            ("CjulERsDeqhhjSme66ECg", "IQWdVyqFxghAtURHGeGiWAsmCAGmdW3WmbEx6Hck"), // iPad
            ("3rJOl1ODzm9yZy63FACdg", "5jPoQ5kQvMJFDYRNE8bQ4rHuds4xJqhvgNJM4awaE8"), // Mac
        };

        private readonly string[] _bTokens = new[]
        {
            // developer.twitter.com
            "AAAAAAAAAAAAAAAAAAAAACHguwAAAAAAaSlT0G31NDEyg%2BSnBN5JuyKjMCU%3Dlhg0gv0nE7KKyiJNEAojQbn8Y3wJm1xidDK7VnKGBP4ByJwHPb",
            // tweetdeck new
            "AAAAAAAAAAAAAAAAAAAAAFQODgEAAAAAVHTp76lzh3rFzcHbmHVvQxYYpTw%3DckAlMINMjmCwxUcaXbAN4XqJVdgMJaHqNOFgPMK0zN1qLqLQCF",
            // ipad -- TimelineSearch returns data in a different format, making nitter return empty results. on the other hand, it has high rate limits. build separate token pools per endpoint?
            "AAAAAAAAAAAAAAAAAAAAAGHtAgAAAAAA%2Bx7ILXNILCqkSGIzy6faIHZ9s3Q%3DQy97w6SIrzE7lQwPJEYQBsArEE2fC25caFwRBvAGi456G09vGR",
        };

        #region Ctor

        public TwitterAuthenticationInitializer(IHttpClientFactory httpClientFactory, InstanceSettings settings, ISettingsDal settingsDal,
            ILogger<TwitterService> logger)
        {
            _logger = logger;
            _instanceSettings = settings;
            _settingsDal = settingsDal;
            _httpClientFactory = httpClientFactory;

            var concuOpt = new ConcurrencyLimiterOptions();
            concuOpt.PermitLimit = 1;
            _rateLimiter = new ConcurrencyLimiter(concuOpt);
        }

        #endregion

        private async Task<string> GenerateBearerToken()
        {
            var httpClient = _httpClientFactory.CreateClient();
            using (var request = new HttpRequestMessage(new HttpMethod("POST"),
                       "https://api.twitter.com/oauth2/token?grant_type=client_credentials"))
            {
                int r1 = rnd.Next(_bTokens.Length);
                return _bTokens[r1];

                int r = rnd.Next(_apiKeys.Length);
                var (login, password) = _apiKeys[r];
                var authValue = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{login}:{password}")));
                request.Headers.Authorization = authValue;

                var httpResponse = await httpClient.SendAsync(request);

                var c = await httpResponse.Content.ReadAsStringAsync();
                httpResponse.EnsureSuccessStatusCode();
                var doc = JsonDocument.Parse(c);
                var token = doc.RootElement.GetProperty("access_token").GetString();
                return token;
            }

        }


        public async Task RefreshClient(HttpRequestMessage req)
        {
            string token = req.Headers.GetValues("x-guest-token").First();

            _token2.TryRemove(token, out _);

            await RefreshCred();
            await Task.Delay(1000);
        }

        private async Task RefreshCred()
        {
            (string bearer, string guest) = await GetCred();
            _token2.TryAdd(guest, bearer);
        }

        private async Task<(string, string)> GetCred()
        {
            string token;
            var httpClient = _httpClientFactory.CreateClient("WithProxy");
            string bearer = await GenerateBearerToken();
            using RateLimitLease lease = await _rateLimiter.AcquireAsync(permitCount: 1);
            using var request = new HttpRequestMessage(new HttpMethod("POST"),
                "https://api.twitter.com/1.1/guest/activate.json");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer " + bearer);
            //request.Headers.Add("User-Agent",
            //    "Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Googlebot/2.1; +http://www.google.com/bot.html) Chrome/113.0.5672.127 Safari/537.36");

            HttpResponseMessage httpResponse;
            do
            {
                httpResponse = await httpClient.SendAsync(request);

                var c = await httpResponse.Content.ReadAsStringAsync();
                if (httpResponse.StatusCode == HttpStatusCode.TooManyRequests)
                    await Task.Delay(10 * 1000);
                httpResponse.EnsureSuccessStatusCode();
                var doc = JsonDocument.Parse(c);
                token = doc.RootElement.GetProperty("guest_token").GetString();

            } while (httpResponse.StatusCode != HttpStatusCode.OK);

            return (bearer, token);

        }

        public async Task<HttpClient> MakeHttpClient()
        {
            if (_token2.Count < _targetClients)
                await RefreshCred();
            return _httpClientFactory.CreateClient();
        }

        public HttpRequestMessage MakeHttpRequest(HttpMethod m, string endpoint, bool addToken)
        {
            var request = new HttpRequestMessage(m, endpoint);
            (string token, string bearer) = _token2.MaxBy(x => rnd.Next());
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer " + bearer);
            request.Headers.TryAddWithoutValidation("Referer", "https://twitter.com/");
            request.Headers.TryAddWithoutValidation("x-twitter-active-user", "yes");
            //request.Headers.Add("User-Agent",
            //    "Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Googlebot/2.1; +http://www.google.com/bot.html) Chrome/113.0.5672.127 Safari/537.36");
            if (addToken)
                request.Headers.TryAddWithoutValidation("x-guest-token", token);
            //request.Headers.TryAddWithoutValidation("Referer", "https://twitter.com/");
            //request.Headers.TryAddWithoutValidation("x-twitter-active-user", "yes");
            return request;
        }

        public async Task<JsonDocument> Login()
        {
            var cred = await _settingsDal.Get("twitteraccounts");
            string username = String.Empty;
            string password = String.Empty;
            
            foreach (JsonElement account in cred.Value.GetProperty("accounts").EnumerateArray())
            {
                username = account.EnumerateArray().First().GetString();
                password = account.EnumerateArray().Last().GetString();
            }

            
            string TW_CONSUMER_KEY = "3nVuSoBZnx6U4vzUxf5w";
            string TW_CONSUMER_SECRET = "Bcs59EFbbsdF6Sl9Ng71smgStWEGwXXKSjYvPVt7qys";
            string TW_ANDROID_BASIC_TOKEN = "Basic " +
                                            Convert.ToBase64String(
                                                Encoding.ASCII.GetBytes($"{TW_CONSUMER_KEY}:{TW_CONSUMER_SECRET}"));

            HttpClient client = new HttpClient();

            // Getting bearer token
            using var request = new HttpRequestMessage(new HttpMethod("POST"),
                "https://api.twitter.com/oauth2/token");
            request.Headers.TryAddWithoutValidation("Authorization", TW_ANDROID_BASIC_TOKEN);
            request.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
            request.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8,
                "application/x-www-form-urlencoded");
            var tokenRequest = await client.SendAsync( request );
            var tokenResponse =
                await JsonSerializer.DeserializeAsync<TokenResponse>(await tokenRequest.Content.ReadAsStreamAsync());
            string bearerToken = tokenResponse.access_token;

            // Activating guest token
            var guestTokenRequest =
                new HttpRequestMessage(HttpMethod.Post, "https://api.twitter.com/1.1/guest/activate.json");
            guestTokenRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            var guestTokenResponse = await client.SendAsync(guestTokenRequest);
            var guestTokenJson =
                await JsonSerializer.DeserializeAsync<GuestTokenResponse>(await guestTokenResponse.Content
                    .ReadAsStreamAsync());
            string guestToken = guestTokenJson.guest_token;

            // Sending requests
            var session = new HttpClient();

            using var request2 = new HttpRequestMessage(new HttpMethod("POST"),
                "https://api.twitter.com/1.1/onboarding/task.json?flow_name=login&api_version=1&known_device_token=&sim_country_code=us");
            request2.Content =
                new StringContent(
                    "{\"flow_token\":null,\"input_flow_data\":{\"country_code\":null,\"flow_context\":{\"referrer_context\":{\"referral_details\":\"utm_source=google-play&utm_medium=organic\",\"referrer_url\":\"\"},\"start_location\":{\"location\":\"deeplink\"}},\"requested_variant\":null,\"target_user_id\":0}}",
                    Encoding.UTF8, "application/json");
            request2.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearerToken);
            request2.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            request2.Headers.TryAddWithoutValidation("User-Agent",
                "TwitterAndroid/9.95.0-release.0 (29950000-r-0) ONEPLUS+A3010/9 (OnePlus;ONEPLUS+A3010;OnePlus;OnePlus3;0;;1;2016)");
            request2.Headers.TryAddWithoutValidation("x-twitter-api-version", "5");
            request2.Headers.TryAddWithoutValidation("x-twitter-client", "TwitterAndroid");
            request2.Headers.TryAddWithoutValidation("x-twitter-client-version", "9.95.0-release.0");
            request2.Headers.TryAddWithoutValidation("os-version", "28");
            request2.Headers.TryAddWithoutValidation("system-user-agent", "Dalvik/2.1.0 (Linux; U; Android 9; ONEPLUS A3010 Build/PKQ1.181203.001)");
            request2.Headers.TryAddWithoutValidation("x-twitter-active-user", "yes");
            request2.Headers.TryAddWithoutValidation("x-guest-token", guestToken);
            var task2 = await session.SendAsync(request2);
            var att = task2.Headers.GetValues("att").First().ToString();

            string flowToken = JsonDocument.Parse( await task2.Content.ReadAsStringAsync() ).RootElement.GetProperty("flow_token").GetString();

            using var request3 = new HttpRequestMessage(new HttpMethod("POST"),
                "https://api.twitter.com/1.1/onboarding/task.json");
            request3.Content =
                new StringContent(
                    $"{{\"flow_token\": \"{flowToken}\",\"subtask_inputs\": [{{\"enter_text\": {{\"suggestion_id\": null,\"text\": \"{username}\",\"link\": \"next_link\" }},\"subtask_id\": \"LoginEnterUserIdentifier\"}}]}}",
                    Encoding.UTF8, "application/json");
            request3.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearerToken);
            request3.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            request3.Headers.TryAddWithoutValidation("User-Agent",
                "TwitterAndroid/9.95.0-release.0 (29950000-r-0) ONEPLUS+A3010/9 (OnePlus;ONEPLUS+A3010;OnePlus;OnePlus3;0;;1;2016)");
            request3.Headers.TryAddWithoutValidation("x-twitter-api-version", "5");
            request3.Headers.TryAddWithoutValidation("x-twitter-client", "TwitterAndroid");
            request3.Headers.TryAddWithoutValidation("x-twitter-client-version", "9.95.0-release.0");
            request3.Headers.TryAddWithoutValidation("os-version", "28");
            request3.Headers.TryAddWithoutValidation("system-user-agent", "Dalvik/2.1.0 (Linux; U; Android 9; ONEPLUS A3010 Build/PKQ1.181203.001)");
            request3.Headers.TryAddWithoutValidation("x-twitter-active-user", "yes");
            request3.Headers.TryAddWithoutValidation("x-guest-token", guestToken);
            request3.Headers.TryAddWithoutValidation("att", att);
            var task3 = await session.SendAsync(request3);
            
            flowToken = JsonDocument.Parse( await task3.Content.ReadAsStringAsync() ).RootElement.GetProperty("flow_token").GetString();
            
            using var request4 = new HttpRequestMessage(new HttpMethod("POST"),
                "https://api.twitter.com/1.1/onboarding/task.json");
            request4.Content =
                new StringContent(
                    $"{{\"flow_token\": \"{flowToken}\",\"subtask_inputs\": [{{\"enter_password\": {{\"password\": \"{password}\",\"link\": \"next_link\" }},\"subtask_id\": \"LoginEnterPassword\"}}]}}",
                    Encoding.UTF8, "application/json");
            request4.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearerToken);
            request4.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            request4.Headers.TryAddWithoutValidation("User-Agent",
                "TwitterAndroid/9.95.0-release.0 (29950000-r-0) ONEPLUS+A3010/9 (OnePlus;ONEPLUS+A3010;OnePlus;OnePlus3;0;;1;2016)");
            request4.Headers.TryAddWithoutValidation("x-twitter-api-version", "5");
            request4.Headers.TryAddWithoutValidation("x-twitter-client", "TwitterAndroid");
            request4.Headers.TryAddWithoutValidation("x-twitter-client-version", "9.95.0-release.0");
            request4.Headers.TryAddWithoutValidation("os-version", "28");
            request4.Headers.TryAddWithoutValidation("system-user-agent", "Dalvik/2.1.0 (Linux; U; Android 9; ONEPLUS A3010 Build/PKQ1.181203.001)");
            request4.Headers.TryAddWithoutValidation("x-twitter-active-user", "yes");
            request4.Headers.TryAddWithoutValidation("x-guest-token", guestToken);
            request4.Headers.TryAddWithoutValidation("att", att);
            var task4 = await session.SendAsync(request4);
            
            flowToken = JsonDocument.Parse( await task4.Content.ReadAsStringAsync() ).RootElement.GetProperty("flow_token").GetString();
            
            using var request5 = new HttpRequestMessage(new HttpMethod("POST"),
                "https://api.twitter.com/1.1/onboarding/task.json");
            request5.Content =
                new StringContent(
                    $"{{\"flow_token\": \"{flowToken}\",\"subtask_inputs\": [{{\"check_logged_in_account\": {{\"link\": \"AccountDuplicationCheck_false\" }},\"subtask_id\": \"AccountDuplicationCheck\"}}]}}",
                    Encoding.UTF8, "application/json");
            request5.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearerToken);
            request5.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            request5.Headers.TryAddWithoutValidation("User-Agent",
                "TwitterAndroid/9.95.0-release.0 (29950000-r-0) ONEPLUS+A3010/9 (OnePlus;ONEPLUS+A3010;OnePlus;OnePlus3;0;;1;2016)");
            request5.Headers.TryAddWithoutValidation("x-twitter-api-version", "5");
            request5.Headers.TryAddWithoutValidation("x-twitter-client", "TwitterAndroid");
            request5.Headers.TryAddWithoutValidation("x-twitter-client-version", "9.95.0-release.0");
            request5.Headers.TryAddWithoutValidation("os-version", "28");
            request5.Headers.TryAddWithoutValidation("system-user-agent", "Dalvik/2.1.0 (Linux; U; Android 9; ONEPLUS A3010 Build/PKQ1.181203.001)");
            request5.Headers.TryAddWithoutValidation("x-twitter-active-user", "yes");
            request5.Headers.TryAddWithoutValidation("x-guest-token", guestToken);
            request5.Headers.TryAddWithoutValidation("att", att);
            var task5 = await session.SendAsync(request5);
                
            var task5Json = JsonDocument.Parse(await task5.Content.ReadAsStreamAsync());
            
            return task5Json;
        }
    }

    public class TokenResponse
    {
        public string token_type { get; set; }
        public string access_token { get; set; }
    }

    public class GuestTokenResponse
    {
        public string guest_token { get; set; }
    }

    public class Subtask
    {
        public string enter_text { get; set; }
        public string subtask_id { get; set; }
    }

}