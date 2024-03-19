using Azure;
using Bogus.DataSets;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestEase;
using System;
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
        private AuctionContext _db;
        private ILeagueService _leagueService;
        private readonly IMflService _mfl;

        public DashboardController(ILeagueService leagueService, IMflService mfl, IOwnerService ownerServiceLayer, ILogger<DashboardController> logger, AuctionContext db)
        {
            _leagueService = leagueService;
            _mfl = mfl;
            _oService = ownerServiceLayer;
            _logger = logger;
            _db = db;
        }

        [HttpGet("test-stuff")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AddNflTeams()
        {


            return Ok(new
            {
                matchups = await _db.NflTeamMatchups.ToListAsync(),
                teams = await _db.NflTeams.ToListAsync(),
                picks = await _db.NflPicks.ToListAsync()

            });

        }

        /*        [HttpPost("add-nfl-teams")]
                [Produces("application/json")]
                [ProducesResponseType(StatusCodes.Status200OK)]
                [ProducesResponseType(StatusCodes.Status400BadRequest)]
                public async Task<IActionResult> AddNflTeams([Body] List<NflTeam> teams)
                {
                    await _db.NflTeams.AddRangeAsync(teams);
                    await _db.SaveChangesAsync();
                    return Ok();

                }*/

        /*        [HttpPost("games-home")] 
                public Task<IActionResult> GetGamesMenu([Body] AuthUser user)
                {
                    if (string.IsNullOrEmpty(user.Sub)) return new BadRequestResult();
                }*/




        // THESE ARE THE NEW CALLS TO SPLIT INTO
        [HttpPost("auth")] //NEED TO CHANGE NAME ON CLIENT
        public async Task<IActionResult> SynchronizeAuth0ToDbOwner([Body] AuthUser user, [Query] string leagueId)
        {
            var hasLogin = !string.IsNullOrEmpty(user.Sub);
            if (!hasLogin) return new BadRequestResult();
            return Ok(await _oService.SynchronizeAuthorizedUser(user));
        }



        //get tag candidates
        [HttpGet("league/{leagueId}/owners/{leagueOwnerId}/mfl/{mflFranchiseId}/tag-candidates")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetFranchiseTagCandidates([Path] int leagueId, [Path] int leagueOwnerId, [Path] int mflFranchiseId)
        {
            var tags = await _mfl.GetFranchiseTagCandidates(leagueId, leagueOwnerId, mflFranchiseId);
            return Ok(tags);
        }

        //get taxi players
        [HttpGet("league/{leagueId}/owners/{leagueOwnerId}/mfl/{mflFranchiseId}/taxi-squad")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetTaxiSqauadPlayers([Path] int leagueId, [Path] int leagueOwnerId, [Path] int mflFranchiseId)
        {
            var taxi = await _mfl.GetTaxiSquadPlayers(leagueId, leagueOwnerId, mflFranchiseId);
            return Ok(taxi);
        }

        // get cut candidates
        [HttpGet("league/{leagueId}/owners/{leagueOwnerId}/mfl/{mflFranchiseId}/buyout-candidates")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetBuyoutCandidates([Path] int leagueId, [Path] int leagueOwnerId, [Path] int mflFranchiseId)
        {
            var candidates = await _mfl.GetBuyoutCandidates(leagueId, leagueOwnerId, mflFranchiseId);
            return Ok(candidates);
        }
        // get waiver extension candidates
        [HttpGet("league/{leagueId}/owners/{leagueOwnerId}/mfl/{mflFranchiseId}/waiver-extensions")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetWaiverExtensionCandidates([Path] int leagueId, [Path] int leagueOwnerId, [Path] int mflFranchiseId)
        {
            var alreadyUsedExtension = await _db.WaiverExtensions.FirstOrDefaultAsync(w => w.Year == DateTime.Now.Year && leagueId == w.LeagueId && leagueOwnerId == w.LeagueOwnerId);
            if (alreadyUsedExtension != null) return Ok(new List<PlayerDTO>());
            var candidates = await _mfl.GetWaiverExtensionCandidates(leagueId, leagueOwnerId, mflFranchiseId);
            return Ok(candidates);
        }


        // get league transactions and team dead caps
        [HttpGet("leagues/{leagueId}/league-caps")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetLeagueTransactionsAndCaps([Path] int leagueId)
        {
            var deadCapData = await _leagueService.GetDeadCapData(leagueId);
            return Ok(deadCapData);
        }



        // if multi leagues ever becomes a thing, need a get leagues endpoint. but not necessary rn



        [HttpPost("league-home")] //NEED TO CHANGE NAME ON CLIENT
        public async Task<IActionResult> GetOnLoadInfo([Body] AuthUser user, [Query] string leagueId)
        {           
            // this is fucking disgusting. separate into multiple calls.
            var dashboard = new LeagueDashboardDTO();
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

            if (profile.Leagues.Any())
            {

            }
            var chosenLeague = (queryLeague && safeLeagueId != 0 && profile.Leagues.Any(l => l.League.LeagueId == safeLeagueId)) ? 
                                    profile.Leagues.FirstOrDefault(l => l.League.LeagueId == safeLeagueId) : 
                                    profile.Leagues.FirstOrDefault();

            chosenLeagueId =  chosenLeague?.League?.LeagueId ?? null;
            if (profile != null && chosenLeagueId != null)
            {
                var ownerOffseasonData = await _mfl.GetTagAndTaxiInfos((int)chosenLeagueId, chosenLeague);
                chosenLeague.TagCandidates = ownerOffseasonData.TagCandidates;
                chosenLeague.TaxiPlayers = ownerOffseasonData.TaxiPlayers;
                chosenLeague.CutCandidates = ownerOffseasonData.CutCandidates; 
            }
            
            try
            {
                if (chosenLeagueId != null)
                {
                    dashboard.Leagues = profile.Leagues.Select(l => l.League).ToList();
                }
                return Ok(dashboard); 
            }
            catch (Exception e)
            {
                return BadRequest();
            }

        }
        [HttpPost("waiver-extension")]
        public async Task<IActionResult> SubmitWaiverExtension([FromBody] FranchiseTagRequestBody body)
        {
            await _mfl.AddPlayerToTeam(body.leagueId, body.mflPlayerId, body.mflFranchiseId);
            await _mfl.GiveNewContractToPlayer(body.leagueId, body.mflPlayerId, body.tagSalary);
            return NoContent();
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
