using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RestEase;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FreeAgencyAuctionAPI
{
    [ApiController]
    [Route("dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly IOwnerService _oService;
        private readonly ILogger<DashboardController> _logger;
        private ILeagueService _leagueService;


        public DashboardController(ILeagueService leagueService, IOwnerService ownerServiceLayer, ILogger<DashboardController> logger)
        {
            _leagueService = leagueService;
            _oService = ownerServiceLayer;
            _logger = logger;
        }


        [HttpGet("home")]
        public async Task<IActionResult> GetOnLoadInfo([Query] string loginInfo = "")
        {
            //steal login 

            OwnerDTO profile = null;
            int? defaultLeagueId = null;
            var hasCookies = !string.IsNullOrEmpty(loginInfo);

            if (!hasCookies) return new BadRequestResult();

            profile = await _oService.CookieLogin(loginInfo);
            defaultLeagueId = profile.Leagues.FirstOrDefault().League.LeagueId;
            
                
            //leagues ids and names only 
            //
            var dashboard = new DashboardConfessionalDTO();
            try
            {
                if (defaultLeagueId != null )
                {
                    dashboard.LeagueTransactions = _leagueService.GetAllTransactions((int)defaultLeagueId);
                    dashboard.LeagueDeadCap = await _leagueService.GetDeadCapData((int)defaultLeagueId);
                    dashboard.Leagues = profile.Leagues.Select(l => l.League).ToList();
                }
                return Ok(dashboard); 
            }
            catch (System.Exception e)
            {
                return BadRequest();
            }

        }
    }
}
