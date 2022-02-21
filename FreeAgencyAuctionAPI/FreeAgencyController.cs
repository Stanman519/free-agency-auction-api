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

        public FreeAgencyController(IPlayerServiceLayer pService, IOwnerServiceLayer ownerServiceLayer,
            IBidLotService bService, IMflService mfl, IHubContext<AuctionHub> auctionHub, IGMBot bot,
            IHeadshotLoadingService headshot)
        {
            _pService = pService;
            _oService = ownerServiceLayer;
            _bService = bService;
            _mfl = mfl;
            _auctionHub = auctionHub;
            _bot = bot;
            _headshot = headshot;
        }
        
        /// <summary>
        /// get data for page load
        /// </summary>
        /// <returns></returns>
        [HttpGet("page-load")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetDataForPageLoad()
        {
            
            var rightNowUTC = DateTime.UtcNow;  // NEED TO CHECK IF any times expired 
            var owners = await _oService.GetAllOwners();
            // if (ret != null) return Ok(ret);
            // return BadRequest();
            //var allLots = await _bService.GetAllLots();
            
             // TEST DATA FOR DEVELOPMENT
             var ltest1 = new LotDTO
            {
                LotId = 6,
                Bid = new BidDTO
                {
                    BidId = 1,
                    BidLength = 1,
                    BidSalary = 43,
                    Expires = new DateTime(2022, 8, 8, 8, 8, 8, 8),
                    LotId = 6,
                    Ownername = "Ryan Stanley",
                    Player = new PlayerDTO
                    {
                        Age = 44,
                        FirstName = "Tom",
                        LastName = "Brady",
                        Headshot = "https://a.espncdn.com/i/headshots/nfl/players/full/2330.png",
                        MflId = "5848",
                        Team = "TBB",
                        Position = "QB"
                    }
                }
            };
            var ltest2 = new LotDTO
            {
                LotId = 2,
                Bid = new BidDTO
                {
                    BidId = 1,
                    BidLength = 2,
                    BidSalary = 14,
                    Expires = new DateTime(2022, 8, 8, 1, 8, 8, 8),
                    LotId = 2,
                    Ownername = "Bob C",
                    Player = new PlayerDTO
                    {
                        Age = 26,
                        FirstName = "Ezekiel",
                        LastName = "Elliott",
                        Headshot = "https://a.espncdn.com/i/headshots/nfl/players/full/3051392.png",
                        MflId = "12625",
                        Team = "DAL",
                        Position = "RB"
                    }
                }
            };
            
            var ltest3 = new LotDTO
            {
                LotId = 3,
                Bid = new BidDTO
                {
                    BidId = 1,
                    BidLength = 1,
                    BidSalary = 3,
                    Expires = new DateTime(2022, 8, 8, 14, 8, 8, 8),
                    LotId = 3,
                    Ownername = "Jason",
                    Player = new PlayerDTO
                    {
                        Age = 27,
                        FirstName = "Dallas",
                        LastName = "Goedert",
                        Headshot = "https://a.espncdn.com/i/headshots/nfl/players/full/3121023.png",
                        MflId = "13674",
                        Team = "PHI",
                        Position = "TE"
                    }
                }
            };

            var ltest4 = new LotDTO
            {
                LotId = 4,
                Bid = new BidDTO
                {
                    BidId = 1,
                    BidLength = 1,
                    BidSalary = 4,
                    Expires = new DateTime(2022, 8, 8, 18, 8, 8, 8),
                    LotId = 4,
                    Ownername = "Mike",
                    Player = new PlayerDTO
                    {
                        Age = 29,
                        FirstName = "James",
                        LastName = "Hall",
                        Headshot = "https://a.espncdn.com/i/headshots/nfl/players/full/4252364.png",
                        MflId = "123121",
                        Team = "NYJ",
                        Position = "WR"
                    }
                }
            };
            
            
            var ltest5 = new LotDTO
            {
                LotId = 5,
                Bid = new BidDTO
                {
                    BidId = 1,
                    BidLength = 2,
                    BidSalary = 31,
                    Expires = new DateTime(2022, 8, 8, 0, 8, 8, 8),
                    LotId = 5,
                    Ownername = "Ted",
                    Player = new PlayerDTO
                    {
                        Age = 22,
                        FirstName = "Jimmy",
                        LastName = "Clausen",
                        Headshot = "https://a.espncdn.com/i/headshots/nfl/players/full/3045138.png",
                        MflId = "123121",
                        Team = "LAC",
                        Position = "QB"
                    }
                }
            };
            var ltest6 = new LotDTO {LotId = 1};
            var ltest7 = new LotDTO {LotId = 7};
            var ltest8 = new LotDTO {LotId = 8};
            var ltest9 = new LotDTO {LotId = 9};
            var ltest10 = new LotDTO {LotId = 10};
            var ltest11 = new LotDTO {LotId = 11};
            var ltest12 = new LotDTO {LotId = 12};

            var lots = new List<LotDTO>();
            lots.Add(ltest1);
            lots.Add(ltest2);
            lots.Add(ltest3);
            lots.Add(ltest4);
            lots.Add(ltest5);
            lots.Add(ltest6);
            lots.Add(ltest7);
            lots.Add(ltest8);
            lots.Add(ltest9);
            lots.Add(ltest10);
            lots.Add(ltest11);
            lots.Add(ltest12);
            
            
            
            var freeAgents = await _pService.GetAllFreeAgents();
            // if (freeAgents != null) return Ok(ret);
            // return BadRequest();
            
            return Ok(new
            {
                owners,
                lots,
                freeAgents
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
        public async Task<IActionResult> GetMflBioAndScoreInfo([FromRoute] int lastYear, [FromRoute] string id, [FromRoute] string firstName, [FromRoute] string lastName, [FromRoute] string position)
        {
            var ret = await _mfl.GetMflPlayerBioDetails(lastYear, id, firstName, lastName, position);
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
            // check if latest bid for player first
            if (!await _bService.IsLatestBid(bid))
                return BadRequest();
            var addPlayerResp = await _mfl.AddPlayerToTeam(bid);
            var ret = await _pService.WinPlayer(bid);
            var lotRet = await _bService.ClearThisLot((int) bid.LotId);
            var contractResponse = await _mfl.GiveNewContractToPlayer(bid);
            if (addPlayerResp == null || contractResponse == null || addPlayerResp?.Length > 0 ||
                contractResponse?.Length > 0)
            {
                try
                {
                    await _bot.NotifyMflError(
                        new ErrorMessage($"there was an error syncing player {bid.Player.MflId} to mfl"));
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
        /// A NEW BID
        /// </summary>
        /// <returns></returns>
        [HttpPost("bid")]
        [Produces("application/json", Type = typeof(BidDTO))]
        [ProducesResponseType(typeof(BidDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PostNewBid([FromBody] BidDTO newBid)

        {
            newBid.Expires = DateTime.UtcNow;
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
            var ret = await _bService.Nominate(nomination);
            var lotToUpdate = new LotDTO
            {
                LotId = (int) nomination.LotId,
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

        
        //TODO: This needs to be incorporated to page load.  this is a weird call to mfl. necessary with db data?
        [HttpGet("salaryCap")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(OwnerDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSalaryCap()
        {
            return Ok(await _mfl.GetSalaryCapRoom());
        }

        [HttpGet("players/{playerId}/bid-history")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetBidHisotry([FromRoute] string playerId)
        {
            return Ok(await _bService.GetBidHistory(playerId));
        }

        [HttpGet("inventory")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> LoadFreeAgentsToDb()
        {
            var mflFreeAgentsTask = _mfl.GetAllMflFreeAgents();
            var headshotsTask = _headshot.ParseHeadshots();
            await Task.WhenAll(mflFreeAgentsTask, headshotsTask);
            var finalList = mflFreeAgentsTask.Result.ToList()
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
                        height = Int32.Parse(mfl.height),
                        weight = Int32.Parse(mfl.weight),
                        position = mfl.position,
                        team = mfl.team
                    }
                ).ToList();
            await _pService.LoadAllFreeAgentsIntoDb(finalList);
            return Ok();
        }
    }
}

// /// <summary>
        // /// active bids for all lots - is this still needed? Page load sent agian via Signal R?
        // /// </summary>
        // /// <returns></returns>
        // [HttpGet("lots")]
        // [Produces("application/json", Type = typeof(PlayerDTO))]
        // [ProducesResponseType(typeof(List<LotDTO>), StatusCodes.Status200OK)]
        // [ProducesResponseType(StatusCodes.Status400BadRequest)]
        // public async Task<IActionResult> GetBidsForAllLots()
        //
        // {
        //     var ret = await _bService.GetAllLots();
        //     if (ret != null) return Ok(ret);
        //     return BadRequest();
        // }

        // /// <summary>
        // /// clear this lot after auction ends - THIS SHOULD JUST BE DONE IN THE WIN CALL...
        // /// </summary>
        // /// <returns></returns>
        // [HttpPut("lots/clear/{lotId}")]
        // [Produces("application/json", Type = typeof(LotDTO))]
        // [ProducesResponseType(typeof(LotDTO), StatusCodes.Status200OK)]
        // [ProducesResponseType(StatusCodes.Status400BadRequest)]
        // public async Task<IActionResult> ClearThisLot(int lotId)
        //
        // {
        //     var ret = await _bService.ClearThisLot(lotId);
        //     if (ret != null) return Ok(ret);
        //     return BadRequest();
        // }

        // /// <summary>
        // /// get all owners for budget scoreboard - THIS SHOULD JUST BE DONE IN THE PAGE LOAD CALL NOW
        // /// </summary>
        // /// <returns></returns>
        // [HttpGet("owners")]
        // [Produces("application/json", Type = typeof(List<OwnerDTO>))]
        // [ProducesResponseType(typeof(List<OwnerDTO>), StatusCodes.Status200OK)]
        // [ProducesResponseType(StatusCodes.Status400BadRequest)]
        // public async Task<IActionResult> GetAllOwners()
        //
        // {
        //     var ret = await _oService.GetAllOwners();
        //     if (ret != null) return Ok(ret);
        //     return BadRequest();
        // }
        
// /// <summary>
// /// get all players who don't have owners or nominations - for nomination THIS IS IN PAGE LOAD NOW.
// /// Do i still need?  PAGE LOAD SENT AGAIN VIA SignalR??
// /// </summary>
// /// <returns></returns>
// [HttpGet("players/nominate")]
// [Produces("application/json", Type = typeof(PlayerDTO))]
// [ProducesResponseType(typeof(List<PlayerDTO>), StatusCodes.Status200OK)]
// [ProducesResponseType(StatusCodes.Status400BadRequest)]
// public async Task<IActionResult> GetAllFreeAgents()
// {
//     var ret = await _pService.GetAllFreeAgents();
//     if (ret != null) return Ok(ret);
//     return BadRequest();
// }
// /// <summary>
// /// get player by id - What was this used for???????
// /// </summary>
// /// <returns></returns>
// [HttpGet("players/{playerId}")]
// [Produces("application/json", Type = typeof(PlayerDTO))]
// [ProducesResponseType(typeof(PlayerDTO), StatusCodes.Status200OK)]
// [ProducesResponseType(StatusCodes.Status400BadRequest)]
// public async Task<IActionResult> GetPlayerById(string playerId)
// {
//     var ret = await _pService.GetPlayerById(playerId);
//     if (ret != null) return Ok(ret);
//     return BadRequest();
// }

// /// <summary>
// /// get all players who have owners - for rosters pages
// /// </summary>
// /// <returns></returns>
// [HttpGet("players/rostered")]
// [Produces("application/json", Type = typeof(PlayerDTO))]
// [ProducesResponseType(typeof(List<PlayerDTO>), StatusCodes.Status200OK)]
// [ProducesResponseType(StatusCodes.Status400BadRequest)]
// public async Task<IActionResult> GetRosteredPlayers()
// {
//     var ret = await _pService.GetRosteredPlayers();
//     if (ret != null) return Ok(ret);
//     return BadRequest();
// }