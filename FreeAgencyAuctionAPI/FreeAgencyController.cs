using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Hub;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RestEase;


namespace FreeAgencyAuctionAPI
{
    [ApiController]
    [Route("[controller]")]
    public class FreeAgencyController : ControllerBase
    {
        private readonly IPlayerService _pService;
        private readonly IOwnerServiceLayer _oService;
        private readonly IBidLotService _bService;
        private readonly IMflService _mfl;
        private readonly IHubContext<AuctionHub> _auctionHub;
        private readonly IGMBot _bot;
        private readonly IHeadshotLoadingService _headshot;
        private readonly ILogger<FreeAgencyController> _logger;


        public FreeAgencyController(IPlayerService pService, IOwnerServiceLayer ownerServiceLayer,
            IBidLotService bService, IMflService mfl, IHubContext<AuctionHub> auctionHub, IGMBot bot,
            IHeadshotLoadingService headshot, ILogger<FreeAgencyController> logger)
        {
            _pService = pService;
            _oService = ownerServiceLayer;
            _bService = bService;
            _mfl = mfl;
            _auctionHub = auctionHub;
            _bot = bot;
            _headshot = headshot;
            _logger = logger;
        }
        
        /// <summary>
        /// get data for page load
        /// </summary>
        /// <returns></returns>
        [HttpGet("leagues/{leagueId}/page-load")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetDataForPageLoad([Path] int leagueId, [Query] string loginInfo = "")
        {
            //TODO: THIS NEEDS BETTER ERROR HANDLING AND WIN MESSAGES
            OwnerDTO profile = null;
            var hasCookies = !string.IsNullOrEmpty(loginInfo);

            if (hasCookies) profile = await _oService.CookieLogin(loginInfo);
            
            var owners = await _oService.GetAllOwners();
            var lotsQuery = await _bService.GetAllLots(leagueId);
            var freeAgents = await _pService.GetAllFreeAgents(leagueId);
            
            var lots = lotsQuery.OrderBy(_ => _.LotId).Take(12).ToList();
            var filterOutAuctionPlayers = freeAgents.Where(f => !lots.Select(l => l.Bid?.Player?.MflId).Contains(f.MflId));



            return Ok( new LoadData
            {
                owners = owners,
                lots = lots,
                freeAgents = filterOutAuctionPlayers.ToList(),
                profile = hasCookies ? profile : null
            });
            return BadRequest(new ErrorResponse("Initial page load failed."));
        }
        
        /// <summary>
        /// get all mfl bio info for player bio
        /// </summary>
        /// <returns></returns>
        [HttpGet("leagues/{leagueId}/year/{lastYear}/playerId/{id}/position/{position}/firstName/{firstName}/lastName/{lastName}")]
        [Produces("application/json", Type = typeof(PlayerDTO))]
        [ProducesResponseType(typeof(List<PlayerDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMflBioAndScoreInfo([FromRoute] int leagueId, [FromRoute] int lastYear, [FromRoute] string id, [FromRoute] string firstName, [FromRoute] string lastName, [FromRoute] string position, [Query("hasAction")] bool hasAction)
        {
            var ret = await _mfl.GetMflPlayerBioDetails(leagueId, lastYear, id, firstName, lastName, position, hasAction);
            if (ret != null) return Ok(ret);
            return BadRequest();
        }

        /// <summary>
        /// add info to player after WIN
        /// </summary>
        /// <returns></returns>
        [HttpPut("win")]
        [Produces("application/json", Type = typeof(PlayerDTO))]
        [ProducesResponseType(typeof(PlayerDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> WinPlayer([FromBody] BidDTO bid)
        {
            var rightNow = DateTime.UtcNow;
            if (bid.Expires > rightNow) return BadRequest(new ErrorResponse("Bid has still not expired."));
            // check if latest bid for player first
            if (!await _bService.IsLatestBid(bid))
                return BadRequest(new ErrorResponse("There has been a more recent bid for this player. Try reloading the page."));
            try
            {
                await _bService.HandleWinningTasks(bid);
            }
            catch (Exception e)
            {
                return BadRequest(new ErrorResponse(e.Message));
            }
            return Ok();
        }
        
        /// <summary>
        /// A NEW BID
        /// </summary>
        /// <returns></returns>
        [HttpPost("bid")]
        [Produces("application/json", Type = typeof(BidDTO))]
        [ProducesResponseType(typeof(BidDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PostNewBid([FromBody] BidDTO newBid)

        {
            if (newBid.LotId == null || newBid.LeagueId == null) return BadRequest(new ErrorResponse("Cannot complete bid. The entered lot ID or league ID is null."));
            if (!await _bService.ValidateBidForDbEntry(newBid))
                return BadRequest(new ErrorResponse("This entry does not actually beat the latest bid for this player. Try reloading your page."));
            newBid.Expires = DateTime.UtcNow.AddDays(200);
            var ret = await _bService.PostNewBid(newBid);
            var lotToUpdate = new LotDTO
            {
                LotId = (int) newBid.LotId,
                Bid = ret,
                LeagueId = newBid.LeagueId
            };
            ret.LotId = newBid.LotId;
            var updatedLot = await _bService.UpdateLotWithBid(lotToUpdate);
            if (updatedLot != null)
            {
                await _auctionHub.Clients.All.SendAsync("FreshBid", ret);
                return Ok(ret);
            }
            return BadRequest();
        }

        /// <summary>
        /// A NEW NOMINATION
        /// </summary>
        /// <returns></returns>
        [HttpPost("nominate")]
        [Produces("application/json", Type = typeof(BidDTO))]
        [ProducesResponseType(typeof(BidDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PostNomination([FromBody] BidDTO nomination)

        {
            if (nomination.LotId == null || nomination.LeagueId == null)
            {
                _logger.LogCritical("Somehow a null lotId or leagueId was entered with bid {bid}", nomination.BidId);
                return BadRequest(new ErrorResponse("Cannot complete bid. The entered lot ID or league ID is null."));
            }
            nomination.Expires = DateTime.UtcNow.AddDays(200);
            var ret = await _bService.Nominate(nomination);
            ret.LotId = nomination.LotId;
            var lotToUpdate = new LotDTO
            {
                LotId = (int) nomination.LotId,
                Bid = ret,
                LeagueId = nomination.LeagueId
            };
            var updatedLot = await _bService.UpdateLotWithBid(lotToUpdate);
            if (updatedLot == null) return BadRequest();
            
            try
            {
                await _auctionHub.Clients.All.SendAsync("FreshBid", ret);
            }
            catch (Exception e)
            {
                _logger.LogError("nomination signalR message failed. bid: {bid}", ret.BidId);
            }
            return Ok(ret);
            
            
        }


        /// <summary>
        /// LOG IN
        /// </summary>
        /// <returns></returns>
        [HttpPost("login")]
        [Produces("application/json", Type = typeof(OwnerDTO))]
        [ProducesResponseType(typeof(OwnerDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse),StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] OwnerDTO loginAttempt)

        {
            var ret = await _oService.Login(loginAttempt);
            if (ret == null) return BadRequest(new ErrorResponse {FriendlyMessage = "Incorrect login info"});
            return Ok(ret);
        }
        
        /// <summary>
        /// REGISTER NEW USER
        /// </summary>
        /// <returns></returns>
        [HttpPost("register")]
        [Produces("application/json", Type = typeof(OwnerDTO))]
        [ProducesResponseType(typeof(OwnerDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RegisterUser([FromBody] OwnerDTO newUser)
        {
            var ret = await _oService.Register(newUser);
            // if (ret != null)
            return Ok(ret);
        }
        
        [HttpGet("leagues/{leagueId}/salaryCap")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSalaryCap([FromRoute] int leagueId)
        {
            var capSpace = await _mfl.GetSalaryCapRoom(leagueId);
            await _oService.UpdateCapSpaceForOwners(capSpace.OrderBy(_ => _.Ownerid).Select(_ => _.Caproom ?? 0)
                .ToList());
            return Ok();
        }

        [HttpGet("leagues/{leagueId}/players/{playerId}/bid-history")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetBidHisotry([FromRoute] int leagueId, [FromRoute] string playerId)
        {
            return Ok(await _bService.GetBidHistory(leagueId, playerId));
        }
        
        [HttpPost("leagues/{leagueId}/tip")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetBidSuggestion([FromRoute] int leagueId, [FromBody] PlayerTipRequestDTO tipRequestRequest)
        {
            await _pService.GetSuggestedSalary(tipRequestRequest);
            return Ok(await _bService.GetBidHistory(leagueId, tipRequestRequest.MflId));
        }

        /*[HttpGet("inventory")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> LoadFreeAgentsToDb()
        {
            var mflFreeAgentsTask = _mfl.GetAllMflFreeAgents();
            var headshotsTask = _headshot.ParseHeadshots();
            var dbFreeAgentsTask = _pService.GetAllPlayers();
            await Task.WhenAll(mflFreeAgentsTask, headshotsTask, dbFreeAgentsTask);
            var playerIdsAndTeamsInDb = dbFreeAgentsTask.Result.Select(p => new { mflId = p.MflId, team = p.Team}).ToList();
            // how do we check if team changed?
            var playersToAddToDb = mflFreeAgentsTask.Result.ToList().Where(mfl =>
            {
                if (playerIdsAndTeamsInDb.FirstOrDefault(dbPlayer =>
                    dbPlayer.team == mfl.team && dbPlayer.mflId == int.Parse(mfl.id)) == null) return true;
                return false;
            });
            // if a players team changed, they should be in playersToAddToDb.
            
            // go though players to add to db, either add them to the teamChangeList or newPlayerList
            
            var playersToAddWithHeadshots = playersToAddToDb
                .GroupJoin(headshotsTask.Result,
                    mfl => mfl.last_name,
                    h => h.LastName,
                    (mfl, h) => new PlayerEntity
                    {
                        Mflid = int.Parse(mfl.id),
                        Age = _mfl.GetAgeInt(mfl.birthdate),
                        Firstname = mfl.first_name,
                        Lastname = mfl.last_name,
                        Fullname = mfl.name,
                        Headshot = h.Count() > 1 ? h.FirstOrDefault(_ => _.FirstName == mfl.first_name)?.Headshot : h.FirstOrDefault()?.Headshot,
                        Height = Int32.Parse(mfl.height),
                        Weight = Int32.Parse(mfl.weight),
                        Position = mfl.position,
                        Team = mfl.team,
                    }
                ).ToList();
            var mflIdsInDb = playerIdsAndTeamsInDb.Select(p => p.mflId).ToList();
            var teamChangeList = new List<PlayerEntity>();
            var newPlayerList = new List<PlayerEntity>();
            playersToAddWithHeadshots.ForEach(player =>
            {
                if (mflIdsInDb.Contains(player.Mflid))
                {
                    teamChangeList.Add(player);
                }
                else
                {
                    newPlayerList.Add(player);
                }
            });
            await _pService.UpdateTeamsAndHeadshotsInDb(teamChangeList);
            await _pService.LoadAllFreeAgentsIntoDb(newPlayerList);
            return Ok();
        }*/
    }
}