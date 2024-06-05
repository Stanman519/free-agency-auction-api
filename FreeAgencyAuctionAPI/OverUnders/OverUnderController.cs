using AutoMapper;
using FreeAgencyAuctionAPI.Models.Confidence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestEase;
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

        public OverUnderController(AuctionContext db, IMapper mapper, ILogger<OverUnderController> logger)
        {
            _mapper = mapper;
            _db = db;
            _logger = logger;
        }

        [HttpGet("year/{year}/leagues/{league}/owners/{ownerId}/team-win-totals")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllNflTeams([Path] int year, [Path] string league, [Path] int ownerId)
        {
            var franchiseOvers = await _db.SeasonWins.Where(s => s.Year == year && s.Franchise.League == league).GroupJoin(_db.OverUnderPicks.Where(p => p.OwnerId == ownerId), (ln) => ln.Id, pk => pk.LineId,
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
                        OwnerId = pk.OwnerId
                    }).FirstOrDefault() ?? new OverUnderPickDTO
                    {
                        LineAdjustment = 0,
                        LineId = g.ln.Id,
                        OwnerId = ownerId
                    }
                }).ToListAsync();
            var retOvers = franchiseOvers.OrderByDescending(_ => _.OverUnder).ToList();
            return Ok(retOvers);
        }

        [HttpPost("year/{year}/leagues/{league}/team-win-totals")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpsertTeamWinTotals([Path] int year, [Path] string league, [FromBody] IEnumerable<OverUnderPickDTO> picks)
        {

            if (picks.Any(p => p.Id != null))
            {
                //update
                var dbPicks = _mapper.Map<List<OverUnderPick>>(picks);
                var strayPicks = new List<OverUnderPick>();
                dbPicks.ForEach(async p =>
                {
                    var entityToUpdate = await _db.OverUnderPicks.FirstOrDefaultAsync(e => e.Id == p.Id);
                    if (entityToUpdate != null)
                    {
                        entityToUpdate.LineAdjustment = p.LineAdjustment;
                        entityToUpdate.IsOver = p.IsOver;
                    } else
                    {
                        strayPicks.Add(p);
                    }
                });
                _db.OverUnderPicks.AddRange(strayPicks);
            } else
            {
                var dbPicks = _mapper.Map<List<OverUnderPick>>(picks);
                _db.OverUnderPicks.AddRange(dbPicks);
                //insert
            }
            _db.SaveChanges();
            return Ok(picks);
        }
        [HttpGet("year/{year}/leagues/{league}/league-picks")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllLeaguePicks([Path] int year, [Path] string league)
        {
            var franchiseOvers = await _db.OverUnderPicks.Where(s => s.WinLine.Year == year && s.WinLine.Franchise.League == league).ToListAsync();
            var retOvers = _mapper.Map<List<OverUnderPickDTO>>(franchiseOvers);
            return Ok(retOvers);
        }
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
            public int? Id { get; set; }
            public int? LineId { get; set; }
            public int OwnerId { get; set; }
            public bool? IsOver { get; set; }
            public int LineAdjustment { get; set; }
        
    }
}
