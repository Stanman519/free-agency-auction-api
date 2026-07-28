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
            NormalizeOwners(owners);

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
            NormalizeOwners(owners);
            var freeAgents = await _pService.GetAllFreeAgents(LeagueId);

            var pool = freeAgents
                .OrderByDescending(p => p.LastSeasonPts ?? 0)
                .ToList();

            // The board should show a variety of positions, not the top-8 scorers (which
            // are all QBs). Draw best-first from each position bucket and round-robin them.
            Queue<PlayerDTO> Bucket(params string[] pos) => new Queue<PlayerDTO>(
                pool.Where(p => pos.Contains((p.Position ?? string.Empty).ToUpperInvariant())));
            var byPos = new[] { Bucket("WR"), Bucket("RB", "FB", "HB"), Bucket("QB"), Bucket("TE") };

            var now = DateTime.UtcNow;
            var rng = new Random();
            var activeCount = Math.Min(8, pool.Count);

            var lots = new List<LotDTO>();
            for (int i = 0, cursor = 0; i < activeCount; i++)
            {
                // Advance to the next non-empty position bucket for variety.
                PlayerDTO player = null;
                for (var tries = 0; tries < byPos.Length && player == null; tries++, cursor++)
                {
                    var q = byPos[cursor % byPos.Length];
                    if (q.Count > 0) player = q.Dequeue();
                }
                if (player == null) break; // pool exhausted

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
                        // A real nomination starts an 18h clock; a bid resets it to 18h. So
                        // keep every demo lot under 18h out (staggered ~1h–17h) — the timer
                        // UI is sized for hours, and nothing races to zero while browsing.
                        Expires = now.AddMinutes(60 + i * 130 + rng.Next(0, 40)),
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
            NormalizeOwners(owners);
            var pool = (await _pService.GetAllFreeAgents(LeagueId)).ToList();

            // Draw each franchise a believable position mix (not a same-position slice of
            // the pool). Position buckets are best-first queues, so no player lands twice.
            Queue<PlayerDTO> Bucket(params string[] pos) => new Queue<PlayerDTO>(
                pool.Where(p => pos.Contains((p.Position ?? string.Empty).ToUpperInvariant()))
                    .OrderByDescending(p => p.LastSeasonPts ?? 0));
            var qbs = Bucket("QB");
            var rbs = Bucket("RB", "FB", "HB");
            var wrs = Bucket("WR");
            var tes = Bucket("TE");

            var rng = new Random(32); // stable-ish across reloads

            var rosters = owners.Select(o =>
            {
                var players = new List<PlayerDTO>();

                // salaries[k] = contract for the k-th best player drawn at this slot.
                void Draw(Queue<PlayerDTO> q, int count, int[] salaries, string status)
                {
                    for (var k = 0; k < count && q.Count > 0; k++)
                    {
                        var p = q.Dequeue();
                        p.MflFranchiseId = o.Mflfranchiseid;
                        p.Salary = salaries[Math.Min(k, salaries.Length - 1)];
                        p.Length = 1 + rng.Next(0, 4);
                        p.RosterStatus = status;
                        players.Add(p);
                    }
                }

                // Active roster — a real dynasty mix of QB/RB/WR/TE.
                Draw(qbs, 2, new[] { 45, 18 }, "ROSTER");
                Draw(rbs, 6, new[] { 34, 22, 14, 8, 5, 3 }, "ROSTER");
                Draw(wrs, 7, new[] { 40, 28, 20, 13, 9, 5, 4 }, "ROSTER");
                Draw(tes, 3, new[] { 23, 12, 6 }, "ROSTER");
                // A couple banged-up starters on IR (50% cap hit).
                Draw(rbs, 1, new[] { 16 }, "INJURED_RESERVE");
                Draw(wrs, 1, new[] { 11 }, "INJURED_RESERVE");
                // Rookies stashed on the taxi squad (20% cap hit).
                Draw(rbs, 2, new[] { 14, 8 }, "TAXI_SQUAD");
                Draw(wrs, 2, new[] { 12, 6 }, "TAXI_SQUAD");
                Draw(tes, 1, new[] { 7 }, "TAXI_SQUAD");

                return new OpposingFranchiseWithRoster
                {
                    Mflfranchiseid = o.Mflfranchiseid,
                    CapRoom = o.CapRoom,
                    YearsLeft = o.YearsLeft,
                    Leagueownerid = o.Leagueownerid,
                    TeamName = string.IsNullOrWhiteSpace(o.TeamName) ? o.OwnerName : o.TeamName,
                    OwnerName = o.OwnerName,
                    Avatar = o.Avatar,
                    Players = players
                        .OrderBy(x => PositionOrder(x.Position))
                        .ThenByDescending(x => x.Salary)
                        .ToList(),
                    DraftPicks = new List<FutureDraftPickDTO>()
                };
            }).ToList();

            return Ok(rosters);
        }

        /// <summary>Rough descending salary curve so top lots cost more. Demo-only cosmetic.</summary>
        private static int EstimateSalary(int rank) => Math.Max(3, 58 - rank * 7);

        /// <summary>
        /// The demo league's owners have colliding/unset MFL franchise ids, which breaks
        /// the roster picker (React key collisions) and the "my team" match. Sort by the
        /// always-unique league-owner id and hand each franchise a unique id (keeping a
        /// real one when it's positive and unique, else falling back to the owner id).
        /// Deterministic, so bootstrap and rosters agree on which franchise is "me".
        /// </summary>
        private static void NormalizeOwners(List<OpposingFranchiseDTO> owners)
        {
            owners.Sort((a, b) => a.Leagueownerid.CompareTo(b.Leagueownerid));
            var used = new HashSet<int>();
            foreach (var o in owners)
            {
                var id = o.Mflfranchiseid;
                if (id <= 0 || !used.Add(id))
                {
                    id = o.Leagueownerid;
                    while (!used.Add(id)) id++;
                }
                o.Mflfranchiseid = id;
            }
        }

        /// <summary>Groups a roster QB → RB → WR → TE for display ordering.</summary>
        private static int PositionOrder(string position) => (position ?? string.Empty).ToUpperInvariant() switch
        {
            "QB" => 0,
            "RB" or "FB" or "HB" => 1,
            "WR" => 2,
            "TE" => 3,
            _ => 4
        };

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
