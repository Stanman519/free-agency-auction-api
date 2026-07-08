using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreeAgencyAuctionAPI
{
    /// <summary>
    /// Anonymous, read-only endpoints backing the public demo (fanpools.net/demo).
    /// Everything is scoped to the demo league (<see cref="Utils.DemoLeagueId"/>) and
    /// returns the same DTO shapes the authenticated screens use, so the frontend can
    /// reuse its existing components. No endpoint here writes; the global
    /// <see cref="Filters.DemoWriteGuardFilter"/> additionally forbids any mutation of a demo league.
    /// </summary>
    [AllowAnonymous]
    [ApiController]
    [Route("demo")]
    public class DemoController : ControllerBase
    {
        private const int LeagueId = Utils.DemoLeagueId;

        private readonly IOwnerService _oService;
        private readonly IPlayerService _pService;
        private readonly AuctionContext _db;
        private readonly ILogger<DemoController> _logger;

        public DemoController(
            IOwnerService oService,
            IPlayerService pService,
            AuctionContext db,
            ILogger<DemoController> logger)
        {
            _oService = oService;
            _pService = pService;
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Synthetic demo identity. The frontend seeds its Redux profile from this
        /// instead of running the Auth0 login, so demo visitors need no account.
        /// </summary>
        [HttpGet("bootstrap")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Bootstrap()
        {
            var league = await GetDemoLeagueDto();
            if (league == null) return NotFound(new ErrorResponse("Demo league not configured."));

            var owners = await _oService.GetAllOwners(LeagueId);
            if (owners.Count == 0) return NotFound(new ErrorResponse("Demo league has no owners."));

            return Ok(BuildDemoProfile(owners, league));
        }

        /// <summary>
        /// A lively auction generated in-memory on every load: the top demo free agents
        /// are placed on lots owned by demo franchises with fresh, staggered countdowns,
        /// plus a couple of open lots so the nominate UI is visible. Nothing is persisted,
        /// so it always looks mid-auction regardless of the calendar.
        /// </summary>
        [HttpGet("auction-bundle")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> AuctionBundle()
        {
            var league = await GetDemoLeagueDto();
            var owners = await _oService.GetAllOwners(LeagueId);
            var freeAgents = await _pService.GetAllFreeAgents(LeagueId);

            var pool = freeAgents
                .OrderByDescending(p => p.LastSeasonPts ?? 0)
                .ToList();

            var now = DateTime.UtcNow;
            var rng = new Random();
            var activeCount = Math.Min(8, pool.Count);

            var lots = new List<LotDTO>();
            for (var i = 0; i < activeCount; i++)
            {
                var player = pool[i];
                var owner = owners.Count > 0 ? owners[i % owners.Count] : null;
                lots.Add(new LotDTO
                {
                    LotId = i + 1,
                    LeagueId = LeagueId,
                    NominatedBy = owner?.Leagueownerid ?? 0,
                    Bid = new BidDTO
                    {
                        BidId = i + 1,
                        LotId = i + 1,
                        LeagueId = LeagueId,
                        OwnerId = owner?.Leagueownerid ?? 0,
                        Ownername = owner?.OwnerName ?? string.Empty,
                        BidSalary = EstimateSalary(i),
                        BidLength = 1 + (i % 4),
                        Player = player,
                        // Stagger expiries so timers tick down at different moments.
                        Expires = now.AddSeconds(25 + i * 12 + rng.Next(0, 8)),
                    }
                });
            }

            // Open (un-nominated) lots so the "nominate a player" flow is visible in the demo.
            lots.Add(new LotDTO { LotId = activeCount + 1, LeagueId = LeagueId, NominatedBy = 0, Bid = null });
            lots.Add(new LotDTO { LotId = activeCount + 2, LeagueId = LeagueId, NominatedBy = 0, Bid = null });

            var lottedMflIds = lots
                .Where(l => l.Bid?.Player != null)
                .Select(l => l.Bid.Player.MflId)
                .ToHashSet();
            var remainingFreeAgents = pool.Where(p => !lottedMflIds.Contains(p.MflId)).ToList();

            var profile = owners.Count > 0 ? BuildDemoProfile(owners, league) : null;

            return Ok(new LoadData
            {
                profile = profile,
                owners = owners,
                lots = lots,
                freeAgents = remainingFreeAgents
            });
        }

        /// <summary>
        /// Fabricated rosters for the demo dashboard (ROSTER + CAP OUTLOOK tabs), which
        /// read the rosters slice. Demo free agents are distributed across demo franchises
        /// with cosmetic salaries/lengths. In-memory only; nothing is persisted.
        /// </summary>
        [HttpGet("rosters")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Rosters()
        {
            var owners = await _oService.GetAllOwners(LeagueId);
            var pool = (await _pService.GetAllFreeAgents(LeagueId)).ToList();

            var perTeam = owners.Count > 0 ? Math.Max(1, pool.Count / owners.Count) : 0;
            var idx = 0;

            var rosters = owners.Select(o =>
            {
                var players = new List<PlayerDTO>();
                for (var k = 0; k < perTeam && idx < pool.Count; k++, idx++)
                {
                    var p = pool[idx];
                    p.Salary = EstimateContractSalary(k);
                    p.Length = 1 + (k % 4);
                    p.MflFranchiseId = o.Mflfranchiseid;
                    players.Add(p);
                }

                return new OpposingFranchiseWithRoster
                {
                    Mflfranchiseid = o.Mflfranchiseid,
                    CapRoom = o.CapRoom,
                    YearsLeft = o.YearsLeft,
                    Leagueownerid = o.Leagueownerid,
                    TeamName = string.IsNullOrWhiteSpace(o.TeamName) ? o.OwnerName : o.TeamName,
                    OwnerName = o.OwnerName,
                    Avatar = o.Avatar,
                    Players = players.OrderBy(x => x.Position).ThenByDescending(x => x.Salary).ToList(),
                    DraftPicks = new List<FutureDraftPickDTO>()
                };
            }).ToList();

            return Ok(rosters);
        }

        /// <summary>Rough descending salary curve so top lots cost more. Demo-only cosmetic.</summary>
        private static int EstimateSalary(int rank) => Math.Max(3, 58 - rank * 7);

        /// <summary>Descending per-roster contract salary, demo-only cosmetic.</summary>
        private static int EstimateContractSalary(int slot) => Math.Max(1, 45 - slot * 3);

        private OwnerDTO BuildDemoProfile(List<OpposingFranchiseDTO> owners, LeagueDTO league)
        {
            // "You" = the seeded test team if present, else the first demo franchise.
            var me = owners.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.TeamName)) ?? owners[0];

            return new OwnerDTO
            {
                OwnerId = me.Leagueownerid,
                Ownername = "demo|fanpools",
                DisplayName = string.IsNullOrWhiteSpace(me.OwnerName) ? "Demo GM" : me.OwnerName,
                Premium = false,
                Avatar = me.Avatar,
                ConfidencePaid = true,
                ConfidenceTitles = Array.Empty<int>(),
                Pools = new List<PoolDTO>(),
                Leagues = new List<LeagueOwnerDTO>
                {
                    new LeagueOwnerDTO
                    {
                        CapRoom = me.CapRoom,
                        YearsLeft = me.YearsLeft,
                        Mflfranchiseid = me.Mflfranchiseid,
                        Leagueownerid = me.Leagueownerid,
                        TeamName = string.IsNullOrWhiteSpace(me.TeamName) ? "Demo Dynasty" : me.TeamName,
                        Ownername = me.OwnerName,
                        League = league,
                        TagCandidates = new List<TagCandidate>(),
                        TaxiPlayers = new List<PlayerDTO>(),
                        CutCandidates = new List<PlayerDTO>()
                    }
                }
            };
        }

        private async Task<LeagueDTO> GetDemoLeagueDto()
        {
            var l = await _db.Leagues.AsNoTracking().FirstOrDefaultAsync(x => x.Mflid == LeagueId);
            if (l == null) return null;

            return new LeagueDTO
            {
                LeagueId = l.Mflid,
                Name = l.Name,
                FirstYear = l.FirstYear,
                // Force auctioning on so the demo always presents the live auction UI,
                // regardless of the stored flag (real demo league has it off).
                IsAuctioning = true,
                IsFranchiseTagSzn = l.IsFranchiseTagSzn,
                IsTaxiCutSzn = l.IsTaxiSzn,
                IsBuyoutSzn = l.IsBuyoutSzn
            };
        }
    }
}
