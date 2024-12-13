using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BirdsiteLive.Domain.Repository;
using BirdsiteLive.Services;

namespace BirdsiteLive.Controllers
{
    public class AboutController : Controller
    {
        private readonly IModerationRepository _moderationRepository;
        private readonly ICachedStatisticsService _cachedStatisticsService;

        #region Ctor
        public AboutController(IModerationRepository moderationRepository, ICachedStatisticsService cachedStatisticsService)
        {
            _moderationRepository = moderationRepository;
            _cachedStatisticsService = cachedStatisticsService;
        }
        #endregion

        public async Task<IActionResult> Index()
        {
            var stats = await _cachedStatisticsService.GetStatisticsAsync();
            return View(stats);
        }

    }
}
