using Bogus.DataSets;
using FreeAgencyAuctionAPI.Filters;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Authorization;
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
    [Authorize]
    [ApiController]
    [Route("dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly IOwnerService _oService;
        private readonly ILogger<DashboardController> _logger;
        private AuctionContext _db;
        private readonly IGMBot _gm;
        private ILeagueService _leagueService;
        private readonly IMflService _mfl;
        private readonly IPlayerRepo _pRepo;
        private readonly IOwnerRepo _oRepo;


        public DashboardController(ILeagueService leagueService, IMflService mfl, IOwnerService ownerServiceLayer, IPlayerRepo prepo, ILogger<DashboardController> logger, AuctionContext db, IGMBot gm, IOwnerRepo oRepo)
        {
            _leagueService = leagueService;
            _mfl = mfl;
            _pRepo = prepo;
            _oService = ownerServiceLayer;
            _logger = logger;
            _db = db;
            _gm = gm;
            _oRepo = oRepo;
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

        // get 5th year option candidates
        [HttpGet("league/{leagueId}/owners/{leagueOwnerId}/mfl/{mflFranchiseId}/fifth-year-option-candidates")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetFifthYearOptionCandidates([Path] int leagueId, [Path] int leagueOwnerId, [Path] int mflFranchiseId)
        {
            var candidates = await _mfl.GetFifthYearOptionCandidates(leagueId, leagueOwnerId, mflFranchiseId);
            return Ok(candidates);
        }

        // get league transactions and team dead caps
        [HttpGet("leagues/{leagueId}/league-caps")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetLeagueTransactionsAndCaps([Path] int leagueId)
        {
            var deadCapTask = _leagueService.GetDeadCapData(leagueId);
            var capRoomTask = _mfl.GetSalaryCapRoom(leagueId);
            await Task.WhenAll(deadCapTask, capRoomTask);
            var deadCapData = deadCapTask.Result;
            var capRoomList = capRoomTask.Result;
            foreach (var team in deadCapData.TeamDeadCapData)
                team.CapRoom = capRoomList.FirstOrDefault(o => o.Mflfranchiseid == team.FranchiseId)?.Caproom ?? 0;
            return Ok(deadCapData);
        }

        [HttpGet("leagues/{leagueId}/trade-bait")]
        [AllowAnonymous]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTradeBait([Path] int leagueId)
        {
            var tradeBait = await _mfl.GetTradeBaitForLeague(leagueId);
            return Ok(tradeBait);
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
            var alreadyExtended = await _db.WaiverExtensions
                .AnyAsync(w => w.LeagueOwnerId == body.leagueOwnerId && w.PlayerId == body.mflPlayerId && w.Year == DateTime.UtcNow.Year);
            if (alreadyExtended)
                return Conflict(new ErrorResponse("This player has already been extended this year."));

            var playerEntity = await _pRepo.GetPlayerById(body.mflPlayerId);
            var playerName = playerEntity?.Fullname ?? body.mflPlayerId.ToString();
            var waiver = new WaiverExtension
            {
                LeagueId = body.leagueId,
                LeagueOwnerId = body.leagueOwnerId,
                Year = DateTime.UtcNow.Year,
                PlayerId = body.mflPlayerId
            };
            await _pRepo.AddWaiverExtensionForTeam(waiver);
            await _mfl.AddPlayerToTeam(body.leagueId, body.mflPlayerId, body.mflFranchiseId, playerName);
            await _mfl.GiveNewContractToPlayer(body.leagueId, body.mflPlayerId, body.tagSalary, false, playerName);
            return NoContent();
        }

        [HttpPost("fifth-year-option")]
        public async Task<IActionResult> SignFifthYearOption([FromBody] FifthYearOptionRequestBody body)
        {
            var candidates = await _mfl.GetFifthYearOptionCandidates(body.leagueId, body.leagueOwnerId, body.mflFranchiseId);
            var match = candidates.FirstOrDefault(c => c.Player.MflId == body.mflPlayerId);
            if (match == null)
            {
                var botId = Utils.leagueBotDict.TryGetValue(body.leagueId, out var x) ? x : string.Empty;
                await _gm.NotifyMflError(new BotMessage($"5th yr option rejected — player {body.mflPlayerId} not eligible for franchise {body.mflFranchiseId} in league {body.leagueId}", botId));
                return BadRequest(new { friendlyMessage = "Player is no longer eligible for a 5th year option." });
            }

            var player = await _mfl.GetMflPlayerById(body.leagueId, body.mflPlayerId);
            var fullName = $"{player.first_name} {player.last_name}";
            await _mfl.AddPlayerToTeam(body.leagueId, body.mflPlayerId, body.mflFranchiseId, fullName);
            await _mfl.GiveNewContractToPlayer(body.leagueId, body.mflPlayerId, match.OptionSalary, 1,
                $"{fullName} signed to a 5th year option: 1 yr, ${match.OptionSalary}");

            var capSpace = await _mfl.GetSalaryCapRoom(body.leagueId);
            var capList = capSpace.OrderBy(c => c.Mflfranchiseid).Select(c => c.Caproom ?? 0).ToList();
            await _oRepo.UpdateCapRoomForAllOwners(capList, body.leagueId);

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
                Year = DateTime.UtcNow.Year,
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

        // HOLDOUT ENDPOINTS
        
        // 1. Generate holdouts for a league (admin endpoint)
        [AllowAnonymous]
        [AdminApiKey]
        [HttpPost("league/{leagueId}/year/{year}/generate-holdouts")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GenerateHoldouts([Path] int leagueId, [Path] int year)
        {
            try
            {
                var holdouts = await _mfl.GenerateAndSaveHoldouts(leagueId, year);
                
                if (!holdouts.Any())
                {
                    return BadRequest(new ErrorResponse($"Holdouts already exist for league {leagueId} year {year}, or no eligible players found."));
                }
                
                return Ok(holdouts);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error generating holdouts");
                return BadRequest(new ErrorResponse(e.Message));
            }
        }

        // 2. Get holdouts for a specific owner
        [HttpGet("league/{leagueId}/owners/{leagueOwnerId}/holdouts")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetOwnerHoldouts([Path] int leagueId, [Path] int leagueOwnerId)
        {
            try
            {
                var holdouts = await _pRepo.GetHoldoutsForOwner(leagueOwnerId, Utils.CurrentYear);
                
                var holdoutDTOs = holdouts.Select(h => new HoldoutDTO
                {
                    Id = h.Id,
                    LeagueId = h.LeagueId,
                    LeagueOwnerId = h.LeagueOwnerId,
                    Year = h.Year,
                    Player = new PlayerDTO
                    {
                        MflId = h.Player.Mflid,
                        FirstName = h.Player.Firstname,
                        LastName = h.Player.Lastname,
                        FullName = h.Player.Fullname,
                        Position = h.Player.Position,
                        Team = h.Player.Team,
                        Age = h.Player.Age,
                        Headshot = h.Player.Headshot,
                        ActionShot = h.Player.Actionshot
                    },
                    OriginalSalary = h.OriginalSalary,
                    HoldoutSalary = h.HoldoutSalary,
                    Status = h.Status,
                    ScoreTier = h.ScoreTier,
                    SalaryComparison = h.SalaryComparison,
                    YearsRemaining = h.YearsRemaining
                }).ToList();

                return Ok(holdoutDTOs);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error getting owner holdouts");
                return BadRequest(new ErrorResponse(e.Message));
            }
        }

        // 3. Respond to a holdout (Accept or Deny)
        [HttpPost("holdout-response")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RespondToHoldout([FromBody] HoldoutResponseBody body)
        {
            try
            {
                // Validate status
                if (body.status != "Accepted" && body.status != "Denied")
                {
                    return BadRequest(new ErrorResponse("Status must be either 'Accepted' or 'Denied'"));
                }

                // Get the holdout
                var holdout = await _pRepo.GetHoldoutById(body.holdoutId);
                if (holdout == null)
                {
                    return BadRequest(new ErrorResponse($"Holdout with id {body.holdoutId} not found"));
                }

                // Check if already processed
                if (holdout.Status != "Pending")
                {
                    return BadRequest(new ErrorResponse($"Holdout has already been {holdout.Status.ToLower()}"));
                }

                // If accepted, update MFL contract first — only persist to DB if MFL succeeds
                if (body.status == "Accepted")
                {
                    var player = await _mfl.GetMflPlayerById(body.leagueId, body.mflPlayerId);
                    var playerName = !string.IsNullOrWhiteSpace(player.first_name)
                        ? $"{player.first_name} {player.last_name}"
                        : player.name;

                    var rosters = await _mfl.GetMflRosters(body.leagueId);
                    var franchise = rosters.FirstOrDefault(f => int.Parse(f.id) == body.mflFranchiseId);
                    var playerRosterEntry = franchise?.player.FirstOrDefault(p => p.id == body.mflPlayerId.ToString());
                    var currentContractYears = playerRosterEntry != null && int.TryParse(playerRosterEntry.contractYear, out var cy)
                        ? cy
                        : holdout.YearsRemaining;

                    var holdoutMessage = $"{holdout.LeagueOwner.Teamname} accepted {playerName}'s holdout - contract updated from ${holdout.OriginalSalary} to ${holdout.HoldoutSalary}";
                    await _mfl.GiveNewContractToPlayer(body.leagueId, body.mflPlayerId, holdout.HoldoutSalary, currentContractYears, holdoutMessage);
                }

                await _pRepo.UpdateHoldoutStatus(body.holdoutId, body.status);

                return NoContent();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error responding to holdout");
                return BadRequest(new ErrorResponse(e.Message));
            }
        }
        
        [HttpPost("admin/leagues/{leagueId}/true-up-salary-caps")]
        public async Task<IActionResult> TrueUpSalaryCaps([FromRoute] int leagueId)
        {
            try
            {
                var capSpace = (await _mfl.GetSalaryCapRoom(leagueId))
                    .OrderBy(c => c.Mflfranchiseid)
                    .ToList();

                var owners = await _db.LeagueOwners.Where(l => l.Leagueid == leagueId).ToListAsync();

                var updates = new List<object>();
                foreach (var mflFranchise in capSpace)
                {
                    var owner = owners.FirstOrDefault(o => o.Mflfranchiseid == mflFranchise.Mflfranchiseid);
                    var newCap = mflFranchise.Caproom ?? 0;
                    var oldCap = owner?.Caproom;
                    if (owner != null) owner.Caproom = newCap;
                    updates.Add(new
                    {
                        mflFranchiseId = mflFranchise.Mflfranchiseid,
                        oldCap,
                        newCap,
                        matched = owner != null
                    });
                }

                var rowsSaved = await _db.SaveChangesAsync();

                return Ok(new
                {
                    leagueId,
                    mflFranchisesReturned = capSpace.Count,
                    ownersInDb = owners.Count,
                    rowsSaved,
                    updates
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error truing up salary caps for league {LeagueId}", leagueId);
                return BadRequest(new ErrorResponse(e.Message));
            }
        }

        [AllowAnonymous]
        [AdminApiKey]
        [HttpPost("admin/leagues/{leagueId}/years/{year}/generate-franchise-tag-values")]
        public async Task<IActionResult> GenerateFranchiseTagValues([FromRoute] int leagueId, [FromRoute] int year)
        {
            var result = await _mfl.GenerateFranchiseTagValues(leagueId, year);
            return Ok(result);
        }

        // need config for threshholds of position rankings
    }
}
