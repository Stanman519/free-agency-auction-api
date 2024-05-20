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

        [HttpGet("year/{year}/leagues/{league}/team-win-totals")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllNflTeams([Path] int year, [Path] string league)
        {
            var franchiseOvers = await _db.SeasonWins.Where(s => s.Year == year && s.Franchise.League == league).ToListAsync();
            var retOvers = franchiseOvers.Select(_ => new TeamWinTotalsDTO
            {
                Id = _.Id,
                OverUnder = _.BaseOverUnder,
                Year = _.Year,
                RealWins = _.RealWins, 
                GamesRemaining  = _.GamesRemaining,
                Franchise = new NflTeamDTO
                {
                    City = _.Franchise.City,
                    Logo = _.Franchise.Logo,
                    Name = _.Franchise.Name,
                    Primary = _.Franchise.Primary,
                    Secondary = _.Franchise.Secondary,
                    SecondaryLogo = _.Franchise.SecondaryLogo,
                    Tertiary = _.Franchise.Tertiary,
                    Tricode = _.Franchise.Tricode,
                    Id = _.FranchiseId
                }
            }).OrderByDescending(_ => _.OverUnder).ToList();
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

    }

    public class TeamWinTotalsDTO
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public decimal OverUnder { get; set; }
        public int RealWins { get; set; }
        public int? GamesRemaining { get; set; }
        public NflTeamDTO Franchise { get; set; }
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
