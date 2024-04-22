using FreeAgencyAuctionAPI.Models.Confidence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace FreeAgencyAuctionAPI.OverUnders
{
    [ApiController]
    [Route("games")]
    public class OverUnderController : ControllerBase
    {
        private readonly AuctionContext _db;
        private readonly ILogger<OverUnderController> _logger;

        public OverUnderController(AuctionContext db, ILogger<OverUnderController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet("year/{year}/leagues/{league}/team-win-totals")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllNflTeams(int year, string league)
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
}
