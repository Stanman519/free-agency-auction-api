using Azure;
using Bogus.DataSets;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
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
        private readonly IPlayerRepo _pRepo;

        public DashboardController(ILeagueService leagueService, IMflService mfl, IOwnerService ownerServiceLayer, IPlayerRepo prepo, ILogger<DashboardController> logger, AuctionContext db)
        {
            _leagueService = leagueService;
            _mfl = mfl;
            _pRepo = prepo;
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
        [HttpPost("auth")]
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

            chosenLeagueId = chosenLeague?.League?.LeagueId ?? null;
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
            var player = await _mfl.GetMflPlayerById(body.leagueId, body.mflPlayerId);
            var waiver = new WaiverExtension
            {
                LeagueId = body.leagueId,
                LeagueOwnerId = body.leagueOwnerId,
                Year = Utils.ThisYear,
                PlayerId = body.mflPlayerId
            };
            await _pRepo.AddWaiverExtensionForTeam(waiver);
            await _mfl.AddPlayerToTeam(body.leagueId, body.mflPlayerId, body.mflFranchiseId);
            await _mfl.GiveNewContractToPlayer(body.leagueId, body.mflPlayerId, body.tagSalary, false, $"{player.first_name} {player.last_name}");
            return NoContent();
        }

        [HttpGet("leagues/{leagueId}/years/{year}/playerIds/{playerIds}")]
        public async Task<IActionResult> GetDetailsForPlayerIds([FromRoute] int leagueId, [FromRoute] int year, [FromRoute] string playerIds)
        {
            var players = await _mfl.GetMflPlayersByIds(leagueId, year, playerIds);

            return Ok(players);
        }

        [HttpGet("leagues/{leagueId}/years/{year}/franchises/{franchiseId}/full-mfl-league")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetBigLeagueObject([FromRoute] int leagueId, [FromRoute] int year, [FromRoute] int franchiseId)
        {
            return Ok(await _mfl.GetMflLeagueRootAndAssets(leagueId, year, franchiseId));
        }

        [HttpPost("tag-player")]
        public async Task<IActionResult> FranchiseTagPlayer([FromBody] FranchiseTagRequestBody body)
        {
            var player = await _mfl.GetMflPlayerById(body.leagueId, body.mflPlayerId);
            var tag = new FranchiseTagPlayer
            {
                Mflleagueid = body.leagueId,
                Leagueownerid = body.leagueOwnerId,
                Year = Utils.ThisYear,
                Tagprice = body.tagSalary,
                Position = player.position,
                Originalsalary = 0,
                Fullname = $"{player.first_name} {player.last_name}",
                Mflplayerid = body.mflPlayerId
            };
            await _pRepo.AddFranchiseTagForTeam(tag);
            await _mfl.AddPlayerToTeam(body.leagueId, body.mflPlayerId, body.mflFranchiseId);
            await _mfl.GiveNewContractToPlayer(body.leagueId, body.mflPlayerId, body.tagSalary, true, $"{player.first_name} {player.last_name}");
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

        [HttpGet("league/{leagueId}/trades/{tradeId}/{leagueOwnerId}/mfl/{mflFranchiseId}/reject-trade/comments/{comments}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RejectPendingTrade([Path] int leagueId, [Path] int tradeId, [Path] int leagueOwnerId, [Path] int mflFranchiseId, [Path] string comments)
        {
            var year = DateTime.UtcNow.Year;
            var response = "reject";
            await _mfl.ResponseToMflTrade(year, leagueId, tradeId, response, comments, mflFranchiseId.ToString("D4"));
            return Ok();
        }

        [HttpGet("league/{leagueId}/trades/{tradeId}/{leagueOwnerId}/mfl/{mflFranchiseId}/revoke-trade/comments/{comments}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Revoke([Path] int leagueId, [Path] int tradeId, [Path] int leagueOwnerId, [Path] int mflFranchiseId, [Path] string comments)
        {
            var year = DateTime.UtcNow.Year;
            var response = "revoke";
            await _mfl.ResponseToMflTrade(year, leagueId, tradeId, response, comments, mflFranchiseId.ToString("D4"));
            return Ok();
        }
        [HttpGet("league/{leagueId}/trades/{tradeId}/{leagueOwnerId}/mfl/{mflFranchiseId}/accept-trade")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Accept([Path] int leagueId, [Path] int tradeId, [Path] int leagueOwnerId, [Path] int mflFranchiseId)
        {
            var year = DateTime.UtcNow.Year;
            var response = "accept";
            await _mfl.ResponseToMflTrade(year, leagueId, tradeId, response, string.Empty, mflFranchiseId.ToString("D4"));
            return Ok();
        }

        [HttpGet("league/{leagueId}/owners/{leagueOwnerId}/mfl/{mflFranchiseId}/pending-trades")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyPendingTrades([Path] int leagueId, [Path] int leagueOwnerId, [Path] int mflFranchiseId)
        {
            var taxi = await _mfl.GetMyPendingTrades(leagueId, mflFranchiseId);
            return Ok(taxi);
        }
        [HttpPost("propose-trade")]
        public async Task<IActionResult> ProposeTrade([FromBody] TradeRequest body)
        {
            var now = DateTime.UtcNow;
            body.Expires = ((DateTimeOffset)now.AddDays(7)).ToUnixTimeSeconds();
            //do some verification
            // make guid
            body.CommentGuid = Guid.NewGuid();
            var currentTrades = await _mfl.GetMyPendingTrades(body.LeagueId, body.SenderId);
            //check if there are any trades out there where all the assets are the same to cancel them.
            var potentialDupeTrades = currentTrades.tradeRequests
                .Where(t => (t.SenderId == body.SenderId && t.ReceiverId == body.ReceiverId) ||
                (t.SenderId == body.ReceiverId && t.ReceiverId == body.SenderId)).ToList();
            var dupe = potentialDupeTrades.FirstOrDefault(p =>
            {
                var sendingInts = p.SendingAssets.Where(sa => !string.IsNullOrEmpty(sa.MflId)).Select(sa => sa.MflId);
                var receivingInts = p.ReceivingAssets.Where(sa => !string.IsNullOrEmpty(sa.MflId)).Select(sa => sa.MflId);
                var newSendings = body.SendingAssets.Where(sa => !string.IsNullOrEmpty(sa.MflId)).Select(sa => sa.MflId);
                var newRecevings = body.ReceivingAssets.Where(sa => !string.IsNullOrEmpty(sa.MflId)).Select(sa => sa.MflId);

                return
                    (sendingInts.SequenceEqual(newSendings) && receivingInts.SequenceEqual(newRecevings)) ||
                    (sendingInts.SequenceEqual(newRecevings) && receivingInts.SequenceEqual(newSendings));

            });

            if (dupe != null)
            {
                //revoke duplicate trade so new one doesnt fail.
                var response = dupe.SenderId == body.SenderId ? "revoke" : "reject";
                await _mfl.ResponseToMflTrade(now.Year, body.LeagueId, int.Parse(dupe.TradeId), response, "Revoked to prevent duplicate mfl trade.", body.SenderId.ToString("D4"));
            }

            

            var dbProp = new Proposal
            {
                CapEatCandidates = body.SendingAssets
                    .Where(_ => _.CapEats.Count > 0).ToList()
                    .Concat(body.ReceivingAssets.Where(r => r.CapEats.Count > 0))
                        .SelectMany(_ => _.CapEats)
                        .Select(_ => new CapEatCandidate
                        {
                            EaterId = _.EaterId,
                            LeagueId = body.LeagueId,
                            CapAdjustment = _.Amount,
                            MflPlayerId = _.MflId,
                            ReceiverId = _.ReceiverId,
                            Year = _.Year
                        }).ToList(),
                ReceiverId = body.ReceiverId,
                Expires = body.Expires,
                LeagueId = body.LeagueId,
                SenderId = body.SenderId
            };

            try
            {
                await _db.Proposals.AddAsync(dbProp);
                _db.SaveChanges();
            }
            catch (Exception ex)
            {
                return BadRequest(new ErrorResponse($"couldn't save to database: {ex.Message}"));
            }
            try
            {
                await _mfl.ProposeMflTrade(body);
                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest(new ErrorResponse(e.Message));
            }

            // post trade to mfl
            // retry?

            //

            return Ok();

        }
        // get all holdout players in league
        [HttpGet("league/{leagueId}/holdout-players")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllHoldoutPlayers([Path] int leagueId)
        {
            var x = await _mfl.GetHoldoutPlayers(leagueId);
            return Ok(x);
        }
        // need config for threshholds of position rankings
        // get all scores from mfl with YTD as W
        // get players from mfl
        // get contracts from mfl

        // join on id

        // get players who have > 1 year left on contract
        // make threshholds for paygrades of each position
        // make service method to get players who performed higher than threshholds above their paygrade




        // get buyout player - 



    }
}
