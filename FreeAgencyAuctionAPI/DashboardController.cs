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
        private readonly IMflService _mfl;

        public DashboardController(ILeagueService leagueService, IMflService mfl, IOwnerService ownerServiceLayer, ILogger<DashboardController> logger)
        {
            _leagueService = leagueService;
            _mfl = mfl;
            _oService = ownerServiceLayer;
            _logger = logger;
        }
/*        [HttpGet("test")]
        public async Task<IActionResult> Test()
        {
            return Ok(await _oService.getTest());
        }*/
            [HttpGet("home")]
        public async Task<IActionResult> GetOnLoadInfo([Query] string loginInfo = "")
        {
            //steal login 
            var dashboard = new DashboardConfessionalDTO();
            OwnerDTO profile = null;
            int? defaultLeagueId = null;
            var hasCookies = !string.IsNullOrEmpty(loginInfo);

            if (!hasCookies) return new BadRequestResult();

            profile = await _oService.CookieLogin(loginInfo);
            dashboard.Profile = profile;
            var defaultLeague = profile.Leagues.FirstOrDefault();
            defaultLeagueId = defaultLeague.League.LeagueId;
            if (profile != null && defaultLeagueId != null)
            {
                var ownerOffseasonData = await _mfl.GetTagAndTaxiInfos((int)defaultLeagueId, defaultLeague.Mflfranchiseid);
                defaultLeague.TagCandidates = ownerOffseasonData.TagCandidates;
                defaultLeague.TaxiPlayers = ownerOffseasonData.TaxiPlayers;
            }
            
            try
            {
                if (defaultLeagueId != null )
                {
                    var deadCapData = await _leagueService.GetDeadCapData((int)defaultLeagueId);
                    dashboard.LeagueTransactions = deadCapData.LeagueTransactions;
                    dashboard.TeamDeadCaps = deadCapData.TeamDeadCapData;
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
