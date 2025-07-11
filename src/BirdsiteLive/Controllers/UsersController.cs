using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BirdsiteLive.ActivityPub;
using BirdsiteLive.ActivityPub.Converters;
using BirdsiteLive.ActivityPub.Models;
using BirdsiteLive.Common.Interfaces;
using BirdsiteLive.Common.Settings;
using BirdsiteLive.Common.Exceptions;
using BirdsiteLive.Common.Models;
using BirdsiteLive.DAL.Contracts;
using BirdsiteLive.Domain;
using BirdsiteLive.Domain.Statistics;
using BirdsiteLive.Models;
using BirdsiteLive.Tools;
using BirdsiteLive.Twitter;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Controllers
{
    public class UsersController : Controller
    {
        private readonly ICachedTwitterTweetsService _twitterTweetService;
        private readonly IUserService _userService;
        private readonly IStatusService _statusService;
        private readonly InstanceSettings _instanceSettings;
        private readonly IFollowersDal _followersDal;
        private readonly ITwitterUserDal _twitterUserDal;
        private readonly ISocialMediaService _socialMediaService;
        private readonly ILogger<UsersController> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IStatisticsHandler _statisticsHandler;

        #region Ctor
        public UsersController(IUserService userService, IStatusService statusService, InstanceSettings instanceSettings, ICachedTwitterTweetsService twitterTweetService, IFollowersDal followersDal, ITwitterUserDal twitterUserDal, ISocialMediaService socialMediaService, IStatisticsHandler statistic, ILogger<UsersController> logger)
        {
            _statusService = statusService;
            _userService = userService;
            _instanceSettings = instanceSettings;
            _twitterTweetService = twitterTweetService;
            _followersDal = followersDal;
            _twitterUserDal = twitterUserDal;
            _socialMediaService = socialMediaService;
            _logger = logger;
            _statisticsHandler = statistic;
            _jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
        }
        #endregion

        [Route("/users")]
        public IActionResult Index()
        {
            var acceptHeaders = Request.Headers["Accept"];
            if (acceptHeaders.Any())
            {
                var r = acceptHeaders.First();
                if (r.Contains("application/activity+json")) return NotFound();
            }
            return View("UserNotFound");
        }

        [Route("/@{id}")]
        [Route("/users/{id}")]
        [Route("/users/{id}/remote_follow")]
        public async Task<IActionResult> Index(string id)
        {
            if (id.EndsWith(".rss"))
                return NotFound();
            _logger.LogTrace("User Index: {Id}", id);

            id = _socialMediaService.MakeUserNameCanonical(id.Trim(new[] { ' ', '@' }));

            SocialMediaUser user = null;
            var isSaturated = false;
            var notFound = false;

            // Ensure valid username 
            if (!string.IsNullOrWhiteSpace(id) && _socialMediaService.ValidUsername.IsMatch(id))
            {
                try
                {
                    user = await _socialMediaService.GetUserAsync(id);
                    if (user is null)
                        isSaturated = true;
                }
                catch (UserNotFoundException)
                {
                    notFound = true;
                }
                catch (UserHasBeenSuspendedException)
                {
                    notFound = true;
                }
                catch (RateLimitExceededException)
                {
                    isSaturated = true;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception getting {Id}", id);
                    throw;
                }
            }
            else
            {
                isSaturated = true;
            }

            var acceptHeaders = Request.Headers["Accept"];
            if (acceptHeaders.Any())
            {
                var r = acceptHeaders.First();
                if (r.Contains("application/activity+json") || r.Contains("application/ld+json"))
                {
                    if (notFound) return NotFound();
                    if (isSaturated) return new ObjectResult("Too Many Requests") { StatusCode = 429 };
                    var apUser = await _userService.GetUser(user);
                    return Content(apUser, "application/activity+json; charset=utf-8");
                }
            }

            if (notFound) return View("UserNotFound");
            if (isSaturated) return View("ApiSaturated");

            Follower[] followers = new Follower[] { };

            string fediAccount = null;
            var userDal = await _socialMediaService.UserDal.GetUserAsync(user.Acct);
            if (userDal != null)
            {
                followers = await _socialMediaService.UserDal.GetFollowersAsync(userDal.Id);
                fediAccount = userDal.FediAcct;
            }


            var displayableUser = new DisplayTwitterUser
            {
                Name = user.Name,
                Description = user.Description,
                Acct = _socialMediaService.MakeUserNameCanonical(user.Acct),
                Url = user.Url,
                ProfileImageUrl = user.ProfileImageUrl,
                Protected = user.Protected,
                FollowerCount = followers.Length,
                MostPopularServer = followers.GroupBy(x => x.Host).OrderByDescending(x => x.Count()).Select(x => x.Key).FirstOrDefault("N/A"),
                FediverseAccount = fediAccount,
                InstanceHandle = $"@{_socialMediaService.MakeUserNameCanonical(user.Acct)}@{_instanceSettings.Domain}",
                ServiceName = _socialMediaService.ServiceName,
            };
            return View(displayableUser);
        }

        [Route("/@{id}/{statusId}")]
        [Route("/users/{id}/statuses/{statusId}")]
        public async Task<IActionResult> Tweet(string id, string statusId)
        {
            var acceptHeaders = Request.Headers["Accept"];

            var tweet = await _socialMediaService.GetPostAsync(statusId);
            if (tweet == null)
                return NotFound();

            if (tweet.Author.Acct != id)
                return NotFound();
            
            var status = await _statusService.GetStatus(id, tweet);

            if (acceptHeaders.Any())
            {
                var r = acceptHeaders.First();

                if (r.Contains("application/activity+json") || r.Contains("application/ld+json"))
                {
                    string jsonApUser;
                    if (status is Question)
                        jsonApUser = JsonSerializer.Serialize((Question)status);
                    else
                        jsonApUser = JsonSerializer.Serialize(status);
                    return Content(jsonApUser, "application/activity+json; charset=utf-8");
                }
            }

            //return Redirect($"https://twitter.com/{id}/status/{statusId}");
            var displayTweet = new DisplayTweet 
            {
                Text = tweet.MessageContent,
                OgUrl = $"https://twitter.com/{id}/status/{statusId}",
                UserProfileImage = tweet.Author.ProfileImageUrl,
                UserName = tweet.Author.Name,
            };
            return View(displayTweet);
        }

        // Mastodon API for QT in some apps
        [Route("/api/v1/statuses/{statusId}")]
        public async Task<IActionResult> mastoApi(string id, string statusId)
        {
            if (!long.TryParse(statusId, out var parsedStatusId))
                return NotFound();

            var tweet = await _twitterTweetService.GetTweetAsync(parsedStatusId);
            if (tweet == null)
                return NotFound();
            
            var user = await _socialMediaService.GetUserAsync(tweet.Author.Acct);
            var status = await _statusService.GetActivity(tweet.Author.Acct, tweet);
            var res = new MastodonPostApi()
            {
                id = parsedStatusId,
                content = status.apObject.content,
                created_at = status.published,
                uri = $"https://{_instanceSettings.Domain}/users/{tweet.Author.Acct.ToLower()}/statuses/{tweet.Id}",
                url = $"https://{_instanceSettings.Domain}/@{tweet.Author.Acct.ToLower()}/{tweet.Id}",
                account = new MastodonUserApi()
                {
                    id = user.Id,
                    username = user.Acct,
                    acct = user.Acct,
                    display_name = user.Name,
                    note = user.Description,
                    url = $"https://{_instanceSettings.Domain}/@{tweet.Author.Acct.ToLower()}",
                    avatar = user.ProfileImageUrl,
                    avatar_static = user.ProfileImageUrl,
                    header = user.ProfileBannerURL,
                    header_static = user.ProfileBannerURL,
                }
            };


            var jsonApUser = JsonSerializer.Serialize(res);
            return Content(jsonApUser, "application/activity+json; charset=utf-8");
        }
        [Route("/users/{userId}/stamps/{postId}/{remote}")]
        public async Task<IActionResult> Stamps(string userId, string postId, string remote)
        {

            var featured = new QuoteAuthorization()
            {
                id = $"https://{_instanceSettings.Domain}/users/{userId}/stamps/{postId}/{remote}",
                attributedTo = UrlFactory.GetActorUrl(_instanceSettings.Domain, userId),
                interactionTarget = UrlFactory.GetNoteUrl(_instanceSettings.Domain, userId, postId),
                interactingObject = UrlFactory.DecodeStampRemoteUrl(remote),
            };

            var jsonApUser = JsonSerializer.Serialize(featured);
            return Content(jsonApUser, "application/activity+json; charset=utf-8");
        }
        [Route("/users/{id}/collections/featured")]
        public async Task<IActionResult> Featured(string id)
        {
            var user = await _socialMediaService.UserDal.GetUserCacheAsync<SocialMediaUser>(id);
            if (user == null)
                user = new SocialMediaUser();

            var featured = new Featured()
            {
                id = $"https://{_instanceSettings.Domain}/users/{id}/collections/featured"
            };
            foreach (string postId in user.PinnedPosts)
            {
                var tweet = await _socialMediaService.GetPostAsync(postId);
                if (tweet is null)
                    continue;
                var status = await _statusService.GetActivity(id, tweet);
                status.apObject.context = null;

                featured.orderedItems.Add(status.apObject);
            }

            var jsonApUser = JsonSerializer.Serialize(featured);
            return Content(jsonApUser, "application/activity+json; charset=utf-8");
        }
        [Route("/users/{id}/statuses/{statusId}/activity")]
        public async Task<IActionResult> Activity(string id, string statusId)
        {
            if (!long.TryParse(statusId, out var parsedStatusId))
                return NotFound();

            var tweet = await _twitterTweetService.GetTweetAsync(parsedStatusId);
            if (tweet == null)
                return NotFound();
            
            var status = _statusService.GetActivity(id, tweet);

            var jsonApUser = JsonSerializer.Serialize(status);
            return Content(jsonApUser, "application/activity+json; charset=utf-8");
        }

        [Route("/users/{id}/inbox")]
        [HttpPost]
        public async Task<IActionResult> Inbox()
        {
            try
            {
                var r = Request;
                using (var reader = new StreamReader(Request.Body))
                {
                    var body = await reader.ReadToEndAsync();

                    _logger.LogTrace("User Inbox: {Body}", body);
                    //System.IO.File.WriteAllText($@"C:\apdebug\{Guid.NewGuid()}.json", body);

                    var activity = ApDeserializer.ProcessActivity(body);
                    _statisticsHandler.RegisterNewInboundActivity(activity);
                    var signature = r.Headers["Signature"].First();

                    switch (activity?.type)
                    {
                        case "Follow":
                        {
                            var succeeded = await _userService.FollowRequestedAsync(signature, r.Method, r.Path,
                                r.QueryString.ToString(), HeaderHandler.RequestHeaders(r.Headers),
                                activity as ActivityFollow, body);
                            if (succeeded) return Accepted();
                            else return Unauthorized();
                        }
                        case "Undo":
                            if (activity is ActivityUndoFollow)
                            {
                                var succeeded = await _userService.UndoFollowRequestedAsync(signature, r.Method, r.Path,
                                    r.QueryString.ToString(), HeaderHandler.RequestHeaders(r.Headers),
                                    activity as ActivityUndoFollow, body);
                                if (succeeded) return Accepted();
                                else return Unauthorized();
                            }

                            return Accepted();
                        case "Delete":
                        {
                            var succeeded = await _userService.DeleteRequestedAsync(signature, r.Method, r.Path,
                                r.QueryString.ToString(), HeaderHandler.RequestHeaders(r.Headers),
                                activity as ActivityDelete, body);
                            if (succeeded) return Accepted();
                            else return Unauthorized();
                        }
                        case "Announce":
                            return Accepted();
                        case "Like":
                            return Accepted();
                        case "Create":
                            return Accepted();
                        default:
                            return Accepted();
                    }
                }
            }
            catch (FollowerIsGoneException)  //TODO: check if user in DB
            {
                return Accepted();
            }
            catch (UserNotFoundException)
            {
                return NotFound();
            }
            catch (UserHasBeenSuspendedException)
            {
                return NotFound();
            }
            catch (RateLimitExceededException)
            {
                return new ObjectResult("Too Many Requests") { StatusCode = 429 };
            }
        }

        [Route("/users/{id}/followers")]
        [HttpGet]
        public IActionResult Followers(string id)
        {
            var r = Request.Headers["Accept"].First();
            if (!r.Contains("json")) return NotFound();

            var followers = new EmptyCollection
            {
                id = $"https://{_instanceSettings.Domain}/users/{id}/followers"
            };
            var jsonApUser = JsonSerializer.Serialize(followers);
            return Content(jsonApUser, "application/activity+json; charset=utf-8");
        }
        [Route("/users/{id}/outbox")]
        [HttpGet]
        public IActionResult Outbox(string id)
        {
            var r = Request.Headers["Accept"].First();
            if (!r.Contains("json")) return NotFound();

            var followers = new EmptyCollection
            {
                id = $"https://{_instanceSettings.Domain}/users/{id}/outbox"
            };
            var jsonApUser = JsonSerializer.Serialize(followers);
            return Content(jsonApUser, "application/activity+json; charset=utf-8");
        }
    }
}