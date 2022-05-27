using System;
using System.Collections.Generic;
using System.Dynamic;
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
        private readonly IPlayerServiceLayer _pService;
        private readonly IOwnerServiceLayer _oService;
        private readonly IBidLotService _bService;
        private readonly IMflService _mfl;
        private readonly IHubContext<AuctionHub> _auctionHub;
        private readonly IGMBot _bot;
        private readonly IHeadshotLoadingService _headshot;
        private readonly ILogger<FreeAgencyController> _logger;

        public FreeAgencyController(IPlayerServiceLayer pService, IOwnerServiceLayer ownerServiceLayer,
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
        [HttpGet("page-load")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetDataForPageLoad([Query] string loginInfo = "")
        {
            OwnerDTO profile = null;
            var hasCookies = !string.IsNullOrEmpty(loginInfo);

            if (hasCookies) profile = await _oService.CookieLogin(loginInfo);
            
            var owners = await _oService.GetAllOwners();
            var lotsQuery = await _bService.GetAllLots();
            var freeAgents = await _pService.GetAllFreeAgents();
            
            var lots = lotsQuery.OrderBy(_ => _.LotId).Take(12).ToList();
            
            return Ok( new LoadData
            {
                owners = owners,
                lots = lots,
                freeAgents = freeAgents,
                profile = hasCookies ? profile : null
            });
            return BadRequest(new ErrorResponse("Initial page load failed."));
        }
        
        /// <summary>
        /// get all mfl bio info for player bio
        /// </summary>
        /// <returns></returns>
        [HttpGet("year/{lastYear}/playerId/{id}/position/{position}/firstName/{firstName}/lastName/{lastName}")]
        [Produces("application/json", Type = typeof(PlayerDTO))]
        [ProducesResponseType(typeof(List<PlayerDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMflBioAndScoreInfo([FromRoute] int lastYear, [FromRoute] string id, [FromRoute] string firstName, [FromRoute] string lastName, [FromRoute] string position, [Query("hasAction")] bool hasAction)
        {
            var ret = await _mfl.GetMflPlayerBioDetails(lastYear, id, firstName, lastName, position, hasAction);
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
                // if necessary, could send Rabbit message here instead
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
            if (newBid.LotId == null) return BadRequest(new ErrorResponse("Cannot complete bid. The entered lot ID is null."));
            if (!await _bService.ValidateBidForDbEntry(newBid))
                return BadRequest(new ErrorResponse("This entry does not actually beat the latest bid for this player. Try reloading your page."));
            newBid.Expires = DateTime.UtcNow.AddDays(1);
            var ret = await _bService.PostNewBid(newBid);
            var lotToUpdate = new LotDTO
            {
                LotId = (int) newBid.LotId,
                Bid = ret
            };
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
            if (nomination.LotId == null)
            {
                _logger.LogCritical("Somehow a null lotId was entered with bid {bid}", nomination.BidId);
                return BadRequest(new ErrorResponse("Cannot complete bid. The entered lot ID is null."));
            }
            nomination.Expires = DateTime.UtcNow.AddDays(1);
            var ret = await _bService.Nominate(nomination);
            var lotToUpdate = new LotDTO
            {
                LotId = (int) nomination.LotId,
                Bid = ret
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
        
        [HttpGet("salaryCap")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(OwnerDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSalaryCap()
        {
            var capSpace = await _mfl.GetSalaryCapRoom();
            await _oService.UpdateCapSpaceForOwners(capSpace.OrderBy(_ => _.ownerid).Select(_ => _.caproom)
                .ToList());
            return Ok();
        }

        [HttpGet("players/{playerId}/bid-history")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetBidHisotry([FromRoute] string playerId)
        {
            return Ok(await _bService.GetBidHistory(playerId));
        }
        
        [HttpPost("tip")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetBidSuggestion([FromBody] PlayerTipRequestDTO tipRequestRequest)
        {
            
            return Ok(await _pService.GetSuggestedSalary(tipRequestRequest));
        }

        [HttpGet("inventory")]
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
                    dbPlayer.team == mfl.team && dbPlayer.mflId == mfl.id) == null) return true;
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
                        mflid = mfl.id,
                        age = _mfl.GetAgeInt(mfl.birthdate),
                        firstname = mfl.first_name,
                        lastname = mfl.last_name,
                        fullname = mfl.name,
                        headshot = h.Count() > 1 ? h.FirstOrDefault(_ => _.FirstName == mfl.first_name)?.Headshot : h.FirstOrDefault()?.Headshot,
                        height = Int32.TryParse(mfl.height, out var outHeight) ? outHeight : 0,
                        weight = Int32.TryParse(mfl.weight, out var outWeight) ? outWeight : 0 ,
                        position = mfl.position,
                        team = mfl.team,
                        mflidint = Int32.Parse(mfl.id)
                    }
                ).ToList();
            var mflIdsInDb = playerIdsAndTeamsInDb.Select(p => p.mflId).ToList();
            var teamChangeList = new List<PlayerEntity>();
            var newPlayerList = new List<PlayerEntity>();
            playersToAddWithHeadshots.ForEach(player =>
            {
                if (mflIdsInDb.Contains(player.mflid))
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
        }
    }
}