using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Models.Confidence;
using FreeAgencyAuctionAPI.Repos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestEase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FreeAgencyAuctionAPI.OverUnders
{
    [ApiController]
    [Route("games")]
    public class OverUnderController : ControllerBase
    {
        private readonly IMapper _mapper;
        private readonly AuctionContext _db;
        private readonly ILogger<OverUnderController> _logger;
        private readonly ISportsDataApi _sportsDataApi;
        private readonly IOptionsSnapshot<AppConfig> _options;
        private readonly int DEFAULT_POOL_ID_TEMP = 1;

        public OverUnderController(AuctionContext db, IMapper mapper, ILogger<OverUnderController> logger, ISportsDataApi sportsDateApi, IOptionsSnapshot<AppConfig> options)
        {
            _mapper = mapper;
            _db = db;
            _logger = logger;
            _sportsDataApi = sportsDateApi;
            _options = options;
        }

        [HttpGet("pools/{poolId}/year/{year}/leagues/{league}/owners/{ownerId}/team-win-totals")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllNflTeams([Path] int poolId, [Path] int year, [Path] string league, [Path] int ownerId)
        {
            var franchiseOvers = await _db.SeasonWins
                .Where(s => s.Year == year && s.Franchise.League == league)
                .GroupJoin(_db.OverUnderPicks
                    .Where(p => p.PoolUser.OwnerId == ownerId && p.PoolId == poolId), (ln) => ln.Id, pk => pk.LineId,
                (ln, pk) => new { ln = ln, pk = pk })
                .Select(g => new TeamWinTotalsDTO
                {
                    Id = g.ln.Id,
                    OverUnder = g.ln.BaseOverUnder,
                    Year = g.ln.Year,
                    RealWins = g.ln.RealWins,
                    GamesRemaining = g.ln.GamesRemaining,
                    Franchise = new NflTeamDTO
                    {
                        City = g.ln.Franchise.City,
                        Logo = g.ln.Franchise.Logo,
                        Name = g.ln.Franchise.Name,
                        Primary = g.ln.Franchise.Primary,
                        Secondary = g.ln.Franchise.Secondary,
                        SecondaryLogo = g.ln.Franchise.SecondaryLogo,
                        Tertiary = g.ln.Franchise.Tertiary,
                        Tricode = g.ln.Franchise.Tricode,
                        Id = g.ln.FranchiseId
                    },
                    UserPick = g.pk.Select(pk => new OverUnderPickDTO
                    {
                        Id = pk.Id,
                        IsOver = pk.IsOver,
                        LineAdjustment = pk.LineAdjustment,
                        LineId = pk.LineId,
                        PoolId = pk.PoolId,
                        UserId = pk.UserId
                    }).FirstOrDefault() ?? new OverUnderPickDTO
                    {
                        LineAdjustment = 0,
                        LineId = g.ln.Id,
                        PoolId = poolId
                    }
                }).ToListAsync();
            var retOvers = franchiseOvers.OrderByDescending(_ => _.OverUnder).ToList();
            var otherUsers = await _db.PoolUsers.Where(u => u.PoolId == poolId).Select(u => u.Owner.Displayname).ToListAsync();
            var ret = new OverUnderLoadBody
            {
                OtherUsers = otherUsers,
                WinLines = retOvers
            };
            return Ok(ret);
        }

        [HttpPost("pools/{poolId}/owners/{ownerId}/ou-save-picks")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpsertTeamWinTotals([Path] int poolId, [Path] int ownerId, [FromBody] IEnumerable<OverUnderPickDTO> picks)
        {
            List<OverUnderPick> newDbPicks = new List<OverUnderPick>();
            picks.ToList().ForEach(p => p.PoolId = poolId);

            if (picks.Any(p => p.Id != null && p.Id != 0))
            {
                var strayPicks = new List<OverUnderPick>();
                var existingPicks = await _db.OverUnderPicks
                    .Where(p => picks.Select(x => x.Id).Contains(p.Id))
                    .ToListAsync();

                var existingPicksDict = existingPicks.ToDictionary(p => p.Id);


                foreach (var p in picks)
                {
                    var pick = _mapper.Map<OverUnderPick>(p);
                    if (existingPicksDict.TryGetValue(pick.Id, out var existingPick))
                    {
                        // Update existing pick
                        existingPick.LineAdjustment = pick.LineAdjustment;
                        existingPick.IsOver = pick.IsOver;
                    }
                    else
                    {
                        // New pick
                        strayPicks.Add(pick);
                    }
                }
                // Update existing records
                if (strayPicks.Any())
                {
                    _db.OverUnderPicks.AddRange(strayPicks);
                    newDbPicks.AddRange(strayPicks);
                }
            }
            else
            {
                newDbPicks = _mapper.Map<List<OverUnderPick>>(picks);

                // Insert new pool user
                if (ownerId != -1)
                {
                    var newPoolUser = new PoolUser
                    {
                        OwnerId = ownerId,
                        PoolId = poolId
                    };
                    _db.PoolUsers.Add(newPoolUser);
                    await _db.SaveChangesAsync(); // Save to generate the PoolUser ID

                    // Now set the UserId for OverUnderPick records
                    newDbPicks.ForEach(p =>
                    {
                        p.UserId = newPoolUser.Id;
                    });

                    _db.OverUnderPicks.AddRange(newDbPicks);
                }
            }
            await _db.SaveChangesAsync();
            return Ok(_mapper.Map<List<OverUnderPickDTO>>(newDbPicks));
        }

        [HttpGet("pools/{poolId}/ou-users-picks")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllUsersAndPicksForPool([Path] int poolId)
        {
            var pool = await _db.Pools.FirstOrDefaultAsync(p => p.Id == poolId);
            if (pool.StartDate < DateTime.UtcNow)
            {
                // give all the picks of all the owners
                var usersAndPicks = await _db.PoolUsers.Where(p => p.PoolId == poolId).Select(u => new PoolUserDTO
                {
                    Id = u.Id,
                    Owner = new OwnerDTO()
                    {
                        DisplayName = u.Owner.Displayname,
                        Avatar = u.Owner.Avatar,
                        Ownername = u.Owner.Ownername,
                        OwnerId = u.Owner.Ownerid
                    },
                    IsPaid = u.IsPaid,
                    Picks = u.OverUnderPicks.Select(p => new OverUnderPickDTO
                    {
                        Id = p.Id,
                        IsOver = p.IsOver,
                        LineAdjustment = p.LineAdjustment,
                        LineId = p.LineId,
                        UserId = p.UserId,
                        PoolId = p.PoolId
                    })
                }).ToListAsync();
                return Ok(usersAndPicks);
            } else
            {
                // give all the owners with empty pick arrays
                var users = await _db.PoolUsers.Where(p => p.PoolId == poolId).Select(u => new PoolUserDTO
                {
                    Id = u.Id,
                    IsPaid = u.IsPaid,
                    Owner = _mapper.Map<OwnerDTO>(u.Owner),
                    Picks = new List<OverUnderPickDTO>()
                }).ToListAsync();
                return Ok(users);
            }
            
        }

        [HttpGet("pools/{poolId}/ou-league-picks")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllLeaguePicks([Path] int poolId)
        {
            var franchiseOvers = await _db.OverUnderPicks.Where(s => s.PoolId == poolId).ToListAsync();
            var retOvers = _mapper.Map<List<OverUnderPickDTO>>(franchiseOvers);
            return Ok(retOvers);
        }

        [HttpGet("pools/{poolId}/ou-users")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllParticipatingUsers([Path] int poolId)
        {
            var owners = await _db.PoolUsers
                .Where(p => p.PoolId == poolId)
                .Select(u => u.Owner)
                .ToListAsync();
            var ownerDTOs = owners.Select(o => new OwnerDTO
            {
                DisplayName = o.Displayname,
                OwnerId = o.Ownerid,
                Avatar = o.Avatar
            });

            return Ok(ownerDTOs);
        }

        [HttpPost("update-wins")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateNFLTeamWins()
        {
            var thisYear = DateTime.UtcNow.Month < 4 ? DateTime.UtcNow.Year - 1 : DateTime.UtcNow.Year; //dealing with games after the new year

            var key = _options.Value.SportsDataConfig.SportsDataApiKey;


            try
            {
                var teams = await _sportsDataApi.GetNflStandingsByYear(thisYear, key);
                var dbTeams = _db.SeasonWins.Where(_ => _.Year == thisYear).ToList();
                teams.ToList().ForEach(t =>
                {
                    var foundDbTeam = dbTeams.Find(db => db.Franchise.SportsDataId == t.TeamID);
                    if (foundDbTeam != null)
                    {
                        foundDbTeam.RealWins = t.Wins;
                        foundDbTeam.GamesRemaining = 17 - (t.Wins + t.Losses + t.Ties);
                    }
                });
                await _db.SaveChangesAsync();
                return Ok();
            } 
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.Message);
            }




        }

        [HttpGet("pools/{poolId}/ou-users-picks-testing")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllUsersAndPicksForPoolTest([Path] int poolId)
        {
            var pool = await _db.Pools.FirstOrDefaultAsync(p => p.Id == poolId);
            if (true)
            {
                // give all the picks of all the owners
                var usersAndPicks = await _db.PoolUsers.Where(p => p.PoolId == poolId).Select(u => new PoolUserDTO
                {
                    Id = u.Id,
                    Owner = new OwnerDTO()
                    {
                        DisplayName = u.Owner.Displayname,
                        Avatar = u.Owner.Avatar,
                        Ownername = u.Owner.Ownername,
                        OwnerId = u.Owner.Ownerid
                    },
                    IsPaid = u.IsPaid,
                    Picks = u.OverUnderPicks.Select(p => new OverUnderPickDTO
                    {
                        Id = p.Id,
                        IsOver = p.IsOver,
                        LineAdjustment = p.LineAdjustment,
                        LineId = p.LineId,
                        UserId = p.UserId,
                        PoolId = p.PoolId
                    })
                }).ToListAsync();
                return Ok(usersAndPicks);
            }
            else
            {
                // give all the owners with empty pick arrays
                var users = await _db.PoolUsers.Where(p => p.PoolId == poolId).Select(u => new PoolUserDTO
                {
                    Id = u.Id,
                    IsPaid = u.IsPaid,
                    Owner = _mapper.Map<OwnerDTO>(u.Owner),
                    Picks = new List<OverUnderPickDTO>()
                }).ToListAsync();
                return Ok(users);
            }

        }
        /*
                [HttpGet("year/{year}/leagues/{league}/make-demo-picks")]
                [Produces("application/json")]
                [ProducesResponseType(StatusCodes.Status200OK)]
                [ProducesResponseType(StatusCodes.Status400BadRequest)]
                public async Task<IActionResult> MakeDemoPicks([Path] int year, [Path] string league)
                {
                    var demoUsers = await _db.Owners.Where(o => o.istest).Select(o => o.Ownerid).ToListAsync();
                    var poolUsers = await _db.PoolUsers.ToListAsync();
                    var existingPicks = await _db.OverUnderPicks.Where(p => p.PoolUser.Owner.istest).ToListAsync();
                    var relevantLines = await _db.SeasonWins.Where(w => w.Year == year && w.Franchise.League == league).ToListAsync();
                    var rnd = new Random();

                    var newPicksToPush = new List<OverUnderPick>();

                    demoUsers.ForEach(u =>
                    {
                        var poolUser = poolUsers.FirstOrDefault(pu => pu.OwnerId == u);
                        if (!existingPicks.Select(ep => ep.PoolUser.Owner.Ownerid).Contains(u))
                        {

                            var randomSortLines = relevantLines.OrderBy(_ => rnd.Next()).ToList();
                            for (int i = 0; i < randomSortLines.Count; i++)
                            {


                                if (poolUser != null)
                                {
                                    var temp = rnd.Next(1, 3);
                                    var isOver = temp == 1;
                                    var newPick = new OverUnderPick
                                    {
                                        IsOver = i < 24 ? isOver : null,
                                        LineAdjustment = i < 3 ? (isOver ? 1 : -1) : 0,
                                        LineId = randomSortLines[i].Id,
                                        UserId = poolUser.Id,
                                        PoolId = poolUser.PoolId

                                    };
                                    newPicksToPush.Add(newPick);
                                }
                            }

                        }

                    });
                    await _db.OverUnderPicks.AddRangeAsync(newPicksToPush);
                    await _db.SaveChangesAsync();
                    return Ok();
                }
        */

        public class OverUnderLoadBody
        {
            public IEnumerable<TeamWinTotalsDTO> WinLines { get; set; }
            public IEnumerable<string> OtherUsers { get; set; }
        }


        public class TeamWinTotalsDTO
        {
            public int Id { get; set; }
            public int Year { get; set; }
            public decimal OverUnder { get; set; }
            public int RealWins { get; set; }
            public int? GamesRemaining { get; set; }
            public NflTeamDTO Franchise { get; set; }
            public OverUnderPickDTO UserPick { get; set; }
        }


        public class PoolUserDTO
        {
            public int Id { get; set; }
            public bool IsPaid { get; set; }
            public OwnerDTO Owner { get; set; }
            public IEnumerable<OverUnderPickDTO> Picks { get; set;}

        }
    }
}
