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
        public async Task<IActionResult> GetOnLoadInfo([Body] AuthUser user, [Query] string leagueId)
        {           
            var dashboard = new DashboardConfessionalDTO();
            OwnerDTO profile = null;
            int? chosenLeagueId = null;
            var hasLogin = !string.IsNullOrEmpty(user.Sub);
            var safeLeagueId = 0;
            var queryLeague = false;
            if (!string.IsNullOrEmpty(leagueId))
            {
                queryLeague = true;
                int.TryParse(leagueId, out safeLeagueId);
            }
            if (!hasLogin) return new BadRequestResult();
            
            profile = await _oService.SynchronizeAuthorizedUser(user);
            dashboard.Profile = profile;
            var chosenLeague = (queryLeague && safeLeagueId != 0 && profile.Leagues.Any(l => l.League.LeagueId == safeLeagueId)) ? profile.Leagues.FirstOrDefault(l => l.League.LeagueId == safeLeagueId) : profile.Leagues.FirstOrDefault();
            chosenLeagueId =  chosenLeague.League.LeagueId;
            if (profile != null && chosenLeagueId != null)
            {
                var ownerOffseasonData = await _mfl.GetTagAndTaxiInfos((int)chosenLeagueId, chosenLeague);
                chosenLeague.TagCandidates = ownerOffseasonData.TagCandidates;
                chosenLeague.TaxiPlayers = ownerOffseasonData.TaxiPlayers;
                chosenLeague.CutCandidates = ownerOffseasonData.CutCandidates; 
            }
            
            try
            {
                if (chosenLeagueId != null )
                {
                    var deadCapData = await _leagueService.GetDeadCapData((int)chosenLeagueId);
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
        public async Task<IActionResult> CutTaxiPlayer([FromBody] CutRequestBody body)
        {
            await _mfl.FreeDropTaxiPlayer(body);
            return NoContent();
            
        }
        [HttpPost("buyout")]
        public async Task<IActionResult> BuyoutPlayer([FromBody] CutRequestBody body)
        {
            await _mfl.BuyoutPlayer(body);
            return NoContent();

        }
    }
}
