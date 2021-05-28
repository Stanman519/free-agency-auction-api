using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Hub;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;


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

        public FreeAgencyController(IPlayerServiceLayer pService, IOwnerServiceLayer ownerServiceLayer,
            IBidLotService bService, IMflService mfl, IHubContext<AuctionHub> auctionHub, IGMBot bot)
        {
            _pService = pService;
            _oService = ownerServiceLayer;
            _bService = bService;
            _mfl = mfl;
            _auctionHub = auctionHub;
            _bot = bot;
        }

        /// <summary>
        /// get player by id
        /// </summary>
        /// <returns></returns>
        [HttpGet("players/{playerId}")]
        [Produces("application/json", Type = typeof(PlayerDTO))]
        [ProducesResponseType(typeof(PlayerDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPlayerById(int playerId)
        {
            var ret = await _pService.GetPlayerById(playerId);
            if (ret != null) return Ok(ret);
            return BadRequest();
        }

        /// <summary>
        /// get all players who have owners - for rosters pages
        /// </summary>
        /// <returns></returns>
        [HttpGet("players/rostered")]
        [Produces("application/json", Type = typeof(PlayerDTO))]
        [ProducesResponseType(typeof(List<PlayerDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetRosteredPlayers()
        {
            var ret = await _pService.GetRosteredPlayers();
            if (ret != null) return Ok(ret);
            return BadRequest();
        }

        /// <summary>
        /// get all players who don't have owners or nominations - for nomination
        /// </summary>
        /// <returns></returns>
        [HttpGet("players/nominate")]
        [Produces("application/json", Type = typeof(PlayerDTO))]
        [ProducesResponseType(typeof(List<PlayerDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllFreeAgents()
        {
            var ret = await _pService.GetAllFreeAgents();
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
            var addPlayerResp = await _mfl.AddPlayerToTeam(bid);
            var ret = await _pService.WinPlayer(bid);
            var lotRet = await _bService.ClearThisLot((int) bid.LotId);
            var contractResponse = await _mfl.GiveNewContractToPlayer(bid);
            if (addPlayerResp == null || contractResponse == null || addPlayerResp?.Length > 0 || contractResponse?.Length > 0)
            {
                try
                {
                    await _bot.NotifyMflError(new ErrorMessage($"there was an error syncing player {bid.PlayerId} to mfl"));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            var updatedCapSpace = await _mfl.GetSalaryCapRoom();
            await _oService.WinPlayer(updatedCapSpace);
            if (ret != null && lotRet != null) return Ok(ret);
            return BadRequest();
        }
        
        /// <summary>
        /// active bids for all lots
        /// </summary>
        /// <returns></returns>
        [HttpGet("lots")]
        [Produces("application/json", Type = typeof(PlayerDTO))]
        [ProducesResponseType(typeof(List<BidDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetBidsForAllLots()

        {
            var ret = await _bService.GetActiveBids();
            if (ret != null) return Ok(ret);
            return BadRequest();
        }
        
        /// <summary>
        /// clear this lot after auction ends
        /// </summary>
        /// <returns></returns>
        [HttpPut("lots/clear/{lotId}")]
        [Produces("application/json", Type = typeof(LotDTO))]
        [ProducesResponseType(typeof(LotDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ClearThisLot(int lotId)

        {
            var ret = await _bService.ClearThisLot(lotId);
            if (ret != null) return Ok(ret);
            return BadRequest();
        }
        
        /// <summary>
        /// get all owners for budget scoreboard
        /// </summary>
        /// <returns></returns>
        [HttpGet("owners")]
        [Produces("application/json", Type = typeof(List<OwnerDTO>))]
        [ProducesResponseType(typeof(List<OwnerDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllOwners()

        {
            var ret = await _oService.GetAllOwners();
            if (ret != null) return Ok(ret);
            return BadRequest();
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
            var ret = await _bService.PostNewBid(newBid);
            var lotToUpdate = new LotDTO
            {
                LotId = (int) newBid.LotId,
                BidId = ret.BidId
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
            var ret = await _bService.Nominate(nomination);
            var lotToUpdate = new LotDTO
            {
                LotId = (int) nomination.LotId,
                BidId = ret.BidId
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
        /// LOG IN
        /// </summary>
        /// <returns></returns>
        [HttpPost("login")]
        [Produces("application/json", Type = typeof(OwnerDTO))]
        [ProducesResponseType(typeof(OwnerDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] OwnerDTO loginAttempt)

        {
            Console.WriteLine("hitting controller for login");
            var ret = await _oService.Login(loginAttempt);
            if (ret == null) return BadRequest();
            return Ok(ret);
        }
        /// <summary>
        /// persisted login with cookie token
        /// </summary>
        /// <returns></returns>
        [HttpPost("login/persist")]
        [Produces("application/json", Type = typeof(OwnerDTO))]
        [ProducesResponseType(typeof(OwnerDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PersistedLogin([FromHeader] string authorization)

        {
            var ret = await _oService.CookieLogin(authorization);
            if (ret == null) return BadRequest();
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
            return Ok(await _mfl.GetSalaryCapRoom());
        }
    }
}