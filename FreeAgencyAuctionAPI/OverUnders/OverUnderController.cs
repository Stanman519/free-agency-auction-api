using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Models.Confidence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private readonly int DEFAULT_POOL_ID_TEMP = 1;

        public OverUnderController(AuctionContext db, IMapper mapper, ILogger<OverUnderController> logger)
        {
            _mapper = mapper;
            _db = db;
            _logger = logger;
        }

        [HttpGet("pools/{poolId}/year/{year}/leagues/{league}/owners/{ownerId}/team-win-totals")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllNflTeams([Path] int poolId, [Path] int year, [Path] string league, [Path] int ownerId)
        {
            var franchiseOvers = await _db.SeasonWins.Where(s => s.Year == year && s.Franchise.League == league).GroupJoin(_db.OverUnderPicks.Where(p => p.OwnerId == ownerId && p.PoolId == poolId), (ln) => ln.Id, pk => pk.LineId,
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
                        OwnerId = pk.OwnerId
                    }).FirstOrDefault() ?? new OverUnderPickDTO
                    {
                        LineAdjustment = 0,
                        LineId = g.ln.Id,
                        OwnerId = ownerId,
                        PoolId = DEFAULT_POOL_ID_TEMP
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

        [HttpPost("pools/{poolId}/ou-save-picks")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpsertTeamWinTotals([Path] int poolId, [FromBody] IEnumerable<OverUnderPickDTO> picks)
        {
            List<OverUnderPick> dbPicks;
            picks.ToList().ForEach(p => p.PoolId = poolId);
            if (picks.Any(p => p.Id != null && p.Id != 0))
            {
                //updateg
                dbPicks = _mapper.Map<List<OverUnderPick>>(picks);
                var strayPicks = new List<OverUnderPick>();
                dbPicks.ForEach(async p =>
                {
                    var entityToUpdate = await _db.OverUnderPicks.FirstOrDefaultAsync(e => e.Id == p.Id);
                    if (entityToUpdate != null)
                    {
                        entityToUpdate.LineAdjustment = p.LineAdjustment;
                        entityToUpdate.IsOver = p.IsOver;
                    }
                    else
                    {
                        strayPicks.Add(p);
                    }
                });
                _db.OverUnderPicks.AddRange(strayPicks);
            }
            else
            {
                dbPicks = _mapper.Map<List<OverUnderPick>>(picks);
                _db.OverUnderPicks.AddRange(dbPicks);
                //insert
            }
            _db.SaveChanges();
            return Ok(_mapper.Map<List<OverUnderPickDTO>>(dbPicks));
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


        /*        [HttpGet("year/{year}/leagues/{league}/make-demo-picks")]
                [Produces("application/json")]
                [ProducesResponseType(StatusCodes.Status200OK)]
                [ProducesResponseType(StatusCodes.Status400BadRequest)]
                public async Task<IActionResult> MakeDemoPicks([Path] int year, [Path] string league)
                {
                    var demoUsers = await _db.Owners.Where(o => o.istest).Select(o => o.Ownerid).ToListAsync();
                    var existingPicks = await _db.OverUnderPicks.Where(p => p.Owner.istest).ToListAsync();
                    var relevantLines = await _db.SeasonWins.Where(w => w.Year == year && w.Franchise.League == league).ToListAsync();
                    var rnd = new Random();

                    var newPicksToPush = new List<OverUnderPick>();

                    demoUsers.ForEach(u =>
                    {
                        if (!existingPicks.Select(ep => ep.OwnerId).Contains(u))
                        {

                            var randomSortLines = relevantLines.OrderBy(_ => rnd.Next()).ToList();
                            for (int i = 0; i < randomSortLines.Count; i++)
                            {
                                var temp = rnd.Next(1, 3);
                                var isOver = temp == 1;
                                var newPick = new OverUnderPick
                                {
                                    IsOver = i < 24 ? isOver : null,
                                    LineAdjustment = i < 3 ? (isOver ? 1 : -1) : 0,
                                    LineId = randomSortLines[i].Id,
                                    OwnerId = u

                                };
                                newPicksToPush.Add(newPick);
                            }

                        }

                    });
                    await _db.OverUnderPicks.AddRangeAsync(newPicksToPush);
                    await _db.SaveChangesAsync();
                    return Ok();
                }*/


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

        public class OverUnderPickDTO
        {
            public int? Id { get; set; } = 0;
            public int? LineId { get; set; }
            public int OwnerId { get; set; }
            public bool? IsOver { get; set; }
            public int LineAdjustment { get; set; }
            public int PoolId { get; set; }

        }
    }
}
