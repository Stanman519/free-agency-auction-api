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

        [HttpPost("home")]
        public async Task<IActionResult> GetOnLoadInfo([Body] AuthUser user)
        {
            // assuming we have auth login info:

            // new method,
            // check db first for owner, with this userid, return it or create one if it doesnt exist.

            // if it doesn't exist they wont be tied to any league or team, so that's an issue for another day. (but, we want the auth user to be in the body so we can take name and image, and email).
            // get all big league objects, and check the emails/usernames to see if there is a match with this user, assign them to that team/league

            // if it does exist, continue this flow as is?

             
            var dashboard = new DashboardConfessionalDTO();
            OwnerDTO profile = null;
            int? defaultLeagueId = null;
            var hasLogin = !string.IsNullOrEmpty(user.Sub);

            if (!hasLogin) return new BadRequestResult();

            //profile = await _oService.CookieLogin(loginInfo);
            profile = await _oService.SynchronizeAuthorizedUser(user);
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

        [HttpPost("tag-player")]
        public async Task<IActionResult> FranchiseTagPlayer([FromBody] FranchiseTagRequestBody body)
        {
            await _mfl.AddPlayerToTeam(body.leagueId, body.mflPlayerId, body.mflFranchiseId);
            await _mfl.GiveNewContractToPlayer(body.leagueId, body.mflPlayerId, body.tagSalary);
            return NoContent();
        }
        [HttpPost("taxi-cut")]
        public async Task<IActionResult> CutTaxiPlayer([FromBody] TaxiCutRequestBody body)
        {
            await _mfl.FreeDropTaxiPlayer(body);
            return NoContent();
            
        }
    }
}
