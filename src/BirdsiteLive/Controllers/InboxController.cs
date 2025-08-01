using System.Diagnostics.Metrics;
using System.IO;
using System.Threading.Tasks;
using BirdsiteLive.ActivityPub;
using BirdsiteLive.ActivityPub.Models;
using BirdsiteLive.Domain;
using BirdsiteLive.Domain.Statistics;
using BirdsiteLive.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BirdsiteLive.Controllers
{
    [ApiController]
    public class InboxController : ControllerBase
    {
        private readonly ILogger<InboxController> _logger;
        private readonly IUserService _userService;
        private readonly IStatisticsHandler _statisticsHandler;

        #region Ctor
        public InboxController(ILogger<InboxController> logger, IUserService userService, IStatisticsHandler statisticsHandler)
        {
            _logger = logger;
            _userService = userService;
            _statisticsHandler = statisticsHandler;
        }
        #endregion

        [Route("/inbox")]
        [HttpPost]
        public async Task<IActionResult> Inbox()
        {
            try
            {
                var r = Request;
                using (var reader = new StreamReader(Request.Body))
                {
                    var body = await reader.ReadToEndAsync();

                    _logger.LogTrace("Inbox: {Body}", body);

                    var activity = ApDeserializer.ProcessActivity(body);
                    _statisticsHandler.RegisterNewInboundActivity(activity);
                    var signature = HeaderHandler.RequestHeaders(r.Headers)["signature"];
                    
                    switch (activity?.type)
                    {
                        case "Follow":
                            {
                                var succeeded = await _userService.FollowRequestedAsync(signature, r.Method, r.Path,
                                    r.QueryString.ToString(), HeaderHandler.RequestHeaders(r.Headers), activity as ActivityFollow, body);
                                if (succeeded) return Accepted();
                                else return Unauthorized();
                            }
                        case "Undo":
                            if (activity is ActivityUndoFollow)
                            {
                                var succeeded = await _userService.UndoFollowRequestedAsync(signature, r.Method, r.Path,
                                    r.QueryString.ToString(), HeaderHandler.RequestHeaders(r.Headers), activity as ActivityUndoFollow, body);
                                if (succeeded) return Accepted();
                                else return Unauthorized();
                            }

                            return Accepted();
                        case "Delete":
                            {
                                var succeeded = await _userService.DeleteRequestedAsync(signature, r.Method, r.Path,
                                    r.QueryString.ToString(), HeaderHandler.RequestHeaders(r.Headers), activity as ActivityDelete, body);
                                if (succeeded) return Accepted();
                                else return Unauthorized();
                            }
                        case "Announce":
                            return Accepted();
                        case "Like":
                            return Accepted();
                        case "Create":
                            return Accepted();

                    }
                }
            }
            catch (FollowerIsGoneException) { } //TODO: check if user in DB

            return Unauthorized();
        }
    }
}