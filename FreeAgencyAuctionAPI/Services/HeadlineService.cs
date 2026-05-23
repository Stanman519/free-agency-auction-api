using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Hub;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IHeadlineService
    {
        Task RecomputeAllForLeague(int leagueId);
        Task OnWin(BidDTO win);
        Task OnBidPosted(BidDTO bid);
        Task<List<HeadlineDTO>> GetActive(int leagueId);
    }

    public class HeadlineService : IHeadlineService
    {
        private readonly AuctionContext _db;
        private readonly IHeadlineRepo _repo;
        private readonly IHubContext<AuctionHub> _hub;
        private readonly IMflApi _mflApi;
        private readonly IOptions<AppConfig> _options;
        private readonly ILogger<HeadlineService> _logger;
        private static readonly ConcurrentDictionary<(int leagueId, int mflId), DateTime> _bidThrottle = new();
        private static readonly TimeSpan ThrottleWindow = TimeSpan.FromSeconds(10);
        private static readonly HashSet<string> SkillPositions = new() { "QB", "RB", "WR", "TE" };

        public HeadlineService(AuctionContext db, IHeadlineRepo repo, IHubContext<AuctionHub> hub, IMflApi mflApi, IOptions<AppConfig> options, ILogger<HeadlineService> logger)
        {
            _db = db;
            _repo = repo;
            _hub = hub;
            _mflApi = mflApi;
            _options = options;
            _logger = logger;
        }

        public async Task<List<HeadlineDTO>> GetActive(int leagueId)
        {
            var rows = await _repo.GetActive(leagueId);
            return rows.Select(ToDto).ToList();
        }

        public async Task RecomputeAllForLeague(int leagueId)
        {
            var league = await _db.Leagues.FirstOrDefaultAsync(l => l.Mflid == leagueId);
            if (league == null || !league.Isauctioning)
            {
                _logger.LogDebug("Skipping headline recompute for league {leagueId} (not auctioning)", leagueId);
                return;
            }

            await _repo.DeleteExpired(leagueId);

            // Cuts first — any active lot headline computed below will supersede a cut headline for the same player.
            await EmitRecentCutHeadlines(leagueId);

            var lots = await _db.Lots
                .Where(l => l.Leagueid == leagueId && l.Bidid != null)
                .ToListAsync();

            var activeMflIds = new HashSet<int>();
            foreach (var lot in lots)
            {
                if (lot.Bid?.Mflid is int mflId)
                {
                    activeMflIds.Add(mflId);
                    await ComposeAndUpsertPlayer(leagueId, mflId, win: null);
                }
            }

            var owners = await _db.LeagueOwners.Where(lo => lo.Leagueid == leagueId).ToListAsync();
            var yearStart = new DateTime(Utils.CurrentYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var allBids = await _db.Bids.Where(b => b.Leagueid == leagueId && b.Expires >= yearStart).ToListAsync();
            var ctx = await BuildOwnerContext(leagueId, owners, allBids);

            foreach (var owner in owners)
            {
                await ComposeAndUpsertOwner(leagueId, owner, ctx, justSignedWin: null);
            }
        }

        public async Task OnWin(BidDTO win)
        {
            var league = await _db.Leagues.FirstOrDefaultAsync(l => l.Mflid == win.LeagueId);
            if (league == null || !league.Isauctioning) return;

            try
            {
                await ComposeAndUpsertPlayer(win.LeagueId, win.Player.MflId, win);

                var owner = await _db.LeagueOwners.FirstOrDefaultAsync(lo => lo.Leagueownerid == win.OwnerId);
                if (owner != null)
                {
                    var yearStart = new DateTime(Utils.CurrentYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var allBids = await _db.Bids.Where(b => b.Leagueid == win.LeagueId && b.Expires >= yearStart).ToListAsync();
                    var ctx = await BuildOwnerContext(win.LeagueId, new List<LeagueOwnerEntity> { owner }, allBids);
                    await ComposeAndUpsertOwner(win.LeagueId, owner, ctx, win);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "OnWin headline compose failed for {mflId} in league {leagueId}", win.Player.MflId, win.LeagueId);
            }
        }

        public async Task OnBidPosted(BidDTO bid)
        {
            var key = (bid.LeagueId, bid.Player.MflId);
            var now = DateTime.UtcNow;
            if (_bidThrottle.TryGetValue(key, out var last) && (now - last) < ThrottleWindow) return;
            _bidThrottle[key] = now;

            var league = await _db.Leagues.FirstOrDefaultAsync(l => l.Mflid == bid.LeagueId);
            if (league == null || !league.Isauctioning) return;

            try
            {
                await ComposeAndUpsertPlayer(bid.LeagueId, bid.Player.MflId, win: null);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "OnBidPosted headline compose failed for {mflId} in league {leagueId}", bid.Player.MflId, bid.LeagueId);
            }
        }

        private async Task EmitRecentCutHeadlines(int leagueId)
        {
            try
            {
                var apiKeyConfig = _options.Value.Mfl?.MflApiKey?.FirstOrDefault(k => k.id == leagueId);
                if (apiKeyConfig == null) return;

                var resp = await _mflApi.GetLastYearWaiverTransactions(leagueId, apiKeyConfig.key, Utils.CurrentYear);
                var transactions = resp?.transactions?.transaction;
                if (transactions == null || transactions.Count == 0) return;

                var cutoffEpoch = DateTimeOffset.UtcNow.AddHours(-24).ToUnixTimeSeconds();

                var cuts = transactions
                    .Where(t => t.type == "FREE_AGENT" &&
                                long.TryParse(t.timestamp, out var ts) && ts >= cutoffEpoch)
                    .ToList();
                if (cuts.Count == 0) return;

                var owners = await _db.LeagueOwners.Where(lo => lo.Leagueid == leagueId).ToListAsync();

                foreach (var cut in cuts)
                {
                    // MFL transaction format: "{added}|{dropped}" — player IDs right of | are the cuts
                    var parts = cut.transaction?.Split('|') ?? Array.Empty<string>();
                    if (parts.Length < 2) continue;
                    var droppedIds = parts[1]
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => int.TryParse(s, out _))
                        .Select(int.Parse);

                    var franchiseId = int.TryParse(cut.franchise, out var fid) ? fid : 0;
                    var cuttingOwner = owners.FirstOrDefault(o => o.Mflfranchiseid == franchiseId);

                    foreach (var mflId in droppedIds)
                    {
                        var player = await _db.Players.FirstOrDefaultAsync(p => p.Mflid == mflId);
                        if (player == null || string.IsNullOrEmpty(player.Position)) continue;
                        if (!SkillPositions.Contains(player.Position)) continue;

                        var input = new PlayerHeadlineInput
                        {
                            RefId = mflId,
                            PlayerName = $"{player.Firstname} {player.Lastname}".Trim(),
                            Position = player.Position,
                            Cut = true,
                            CutBy = cuttingOwner?.Owner?.Ownername ?? cuttingOwner?.Teamname ?? "previous team",
                        };
                        var composed = HeadlineComposer.ComposePlayer(input);
                        if (composed == null) continue;
                        await UpsertIfChanged(leagueId, HeadlineRefKind.Player, mflId, composed, expiresAt: DateTime.UtcNow.AddHours(24));
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Cut headline detection failed for league {leagueId}", leagueId);
            }
        }

        private async Task ComposeAndUpsertPlayer(int leagueId, int mflId, BidDTO? win)
        {
            var input = await BuildPlayerInput(leagueId, mflId, win);
            if (input == null) return;
            var composed = HeadlineComposer.ComposePlayer(input);
            if (composed == null) return;

            await UpsertIfChanged(leagueId, HeadlineRefKind.Player, mflId, composed,
                expiresAt: win != null ? DateTime.UtcNow.AddHours(24) : (DateTime?)null);
        }

        private async Task ComposeAndUpsertOwner(int leagueId, LeagueOwnerEntity owner, OwnerContext ctx, BidDTO? justSignedWin)
        {
            var input = BuildOwnerInput(owner, ctx, justSignedWin);
            var composed = HeadlineComposer.ComposeOwner(input);
            if (composed == null)
            {
                var existing = await _repo.GetActiveByRef(leagueId, HeadlineRefKind.Owner, owner.Leagueownerid);
                if (existing != null)
                {
                    existing.IsActive = false;
                    await _db.SaveChangesAsync();
                }
                return;
            }
            await UpsertIfChanged(leagueId, HeadlineRefKind.Owner, owner.Leagueownerid, composed, expiresAt: null);
        }

        private async Task UpsertIfChanged(int leagueId, string refKind, int refId, ComposedHeadline composed, DateTime? expiresAt)
        {
            var existing = await _repo.GetActiveByRef(leagueId, refKind, refId);
            if (existing != null && existing.Text == composed.Text) return;

            var fresh = await _repo.Upsert(leagueId, refKind, refId, composed.Text, composed.Tags, expiresAt);
            try
            {
                await _hub.Clients.All.SendAsync("NewHeadline", ToDto(fresh));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "headline broadcast failed for {refKind}:{refId}", refKind, refId);
            }
        }

        private async Task<PlayerHeadlineInput?> BuildPlayerInput(int leagueId, int mflId, BidDTO? win)
        {
            var yearStart = new DateTime(Utils.CurrentYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var bids = await _db.Bids
                .Where(b => b.Leagueid == leagueId && b.Mflid == mflId && b.Expires >= yearStart)
                .OrderBy(b => b.Bidid)
                .Select(b => new
                {
                    b.Bidid,
                    b.Bidsalary,
                    b.Bidlength,
                    b.Expires,
                    b.Ownerid,
                    OwnerName = b.LeagueOwner.Owner.Ownername
                })
                .ToListAsync();
            if (bids.Count == 0) return null;

            var player = await _db.Players.FirstOrDefaultAsync(p => p.Mflid == mflId);
            if (player == null) return null;

            var sagaDays = (int)Math.Floor((DateTime.UtcNow - bids.First().Expires.AddHours(-18)).TotalDays);
            if (sagaDays < 0) sagaDays = 0;

            var top = bids.Last();
            var deadlineMin = (int)(top.Expires - DateTime.UtcNow).TotalMinutes;

            var analysis = BidAnalyzer.Analyze(
                bids.Select(b => new BidAnalyzer.BidRow(b.Bidid, b.Ownerid, b.Bidsalary, b.OwnerName)));
            var handoffs = analysis.HandoffCount;
            var distinctSerious = analysis.SeriousBidderCount;
            var warOpponents = analysis.WarOpponentLastNames;

            int topMoneyRank = 0;
            if (win != null && !string.IsNullOrEmpty(player.Position))
            {
                var posRoster = await _db.Contracts
                    .Where(c => c.Leagueid == leagueId && c.Player.Position == player.Position)
                    .OrderByDescending(c => c.Salary)
                    .Take(5)
                    .Select(c => c.Salary)
                    .ToListAsync();
                var rank = posRoster.Count(s => s >= win.BidSalary) + 1;
                if (rank <= 3) topMoneyRank = rank;
            }

            return new PlayerHeadlineInput
            {
                RefId = mflId,
                PlayerName = $"{player.Firstname} {player.Lastname}".Trim(),
                Position = player.Position ?? "",
                TopBidderName = win?.Ownername ?? top.OwnerName ?? "",
                Salary = win?.BidSalary ?? top.Bidsalary,
                Years = win?.BidLength ?? top.Bidlength,
                Win = win != null,
                HandoffCount = handoffs,
                SagaDays = sagaDays,
                DistinctBidders = distinctSerious,
                DeadlineMinutes = win != null ? -1 : Math.Max(deadlineMin, 0),
                TopMoneyRank = topMoneyRank,
                WarOpponents = warOpponents,
            };
        }

        private async Task<OwnerContext> BuildOwnerContext(int leagueId, List<LeagueOwnerEntity> owners, List<BidEntity> allBids)
        {
            var lots = await _db.Lots.Where(l => l.Leagueid == leagueId && l.Bidid != null).Select(l => l.Bidid).ToListAsync();
            var winningBidIds = new HashSet<int>(lots.Where(b => b.HasValue).Select(b => b.Value));

            var contracts = await _db.Contracts
                .Where(c => c.Leagueid == leagueId)
                .Select(c => new { c.Ownerid, c.Salary, c.Bidid, c.Mflid, Position = c.Player.Position })
                .ToListAsync();

            var spendByOwner = contracts
                .GroupBy(c => c.Ownerid)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.Salary));

            var dominantPosByOwner = new Dictionary<int, string?>();
            foreach (var g in contracts.GroupBy(c => c.Ownerid))
            {
                var byPos = g.GroupBy(c => c.Position ?? "").ToDictionary(p => p.Key, p => p.Sum(x => x.Salary));
                var totalSpend = byPos.Values.Sum();
                if (totalSpend == 0) { dominantPosByOwner[g.Key] = null; continue; }
                var top = byPos.OrderByDescending(p => p.Value).First();
                dominantPosByOwner[g.Key] = (top.Value * 1.0 / totalSpend) > 0.5 ? top.Key : null;
            }

            var bidsByOwner = allBids.GroupBy(b => b.Ownerid).ToDictionary(g => g.Key, g => g.Select(b => b.Mflid).Distinct().Count());

            var top3Spenders = spendByOwner.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Key).ToHashSet();

            var avgCap = owners.Any(o => o.Caproom.HasValue) ? owners.Average(o => o.Caproom ?? 0) : 0;

            int? minBidCount = null;
            if (bidsByOwner.Count > 0) minBidCount = bidsByOwner.Values.Min();

            var maxCapOwner = owners.Where(o => o.Caproom > 0).OrderByDescending(o => o.Caproom).FirstOrDefault();

            int? topNegotiatorOwnerId = null;
            int topNegotiatorBidCount = 0;
            if (bidsByOwner.Count >= 4)
            {
                var top = bidsByOwner.OrderByDescending(kv => kv.Value).FirstOrDefault();
                if (top.Value >= 3)
                {
                    topNegotiatorOwnerId = top.Key;
                    topNegotiatorBidCount = top.Value;
                }
            }

            return new OwnerContext
            {
                SpendByOwner = spendByOwner,
                DominantPosByOwner = dominantPosByOwner,
                BidCountByOwner = bidsByOwner,
                Top3SpenderOwnerIds = top3Spenders,
                LeagueAvgCap = avgCap,
                MinBidCount = minBidCount,
                MaxCapRoomOwnerId = maxCapOwner?.Leagueownerid,
                TopNegotiatorOwnerId = topNegotiatorOwnerId,
                TopNegotiatorBidCount = topNegotiatorBidCount,
            };
        }

        private OwnerHeadlineInput BuildOwnerInput(LeagueOwnerEntity owner, OwnerContext ctx, BidDTO? justSignedWin)
        {
            var totalSpend = ctx.SpendByOwner.GetValueOrDefault(owner.Leagueownerid, 0);
            var dominantPos = ctx.DominantPosByOwner.GetValueOrDefault(owner.Leagueownerid);
            var bidCount = ctx.BidCountByOwner.GetValueOrDefault(owner.Leagueownerid, 0);

            var capRoom = owner.Caproom ?? 0;
            var isRoomLeft = capRoom > 0 && ctx.LeagueAvgCap > 0 && (capRoom / (double)ctx.LeagueAvgCap) > 1.25 && totalSpend > 0;
            var isBigSpend = ctx.Top3SpenderOwnerIds.Contains(owner.Leagueownerid) && totalSpend > 0;
            var isFewest = ctx.MinBidCount.HasValue && bidCount == ctx.MinBidCount.Value && bidCount > 0 && ctx.BidCountByOwner.Count >= 4;
            var isMaxCapRoom = ctx.MaxCapRoomOwnerId == owner.Leagueownerid && capRoom > 0;
            var isTopNegotiator = ctx.TopNegotiatorOwnerId == owner.Leagueownerid;

            return new OwnerHeadlineInput
            {
                RefId = owner.Leagueownerid,
                OwnerName = owner.Owner?.Ownername ?? owner.Teamname ?? $"Owner {owner.Leagueownerid}",
                JustSignedPlayer = justSignedWin != null ? $"{justSignedWin.Player.FirstName} {justSignedWin.Player.LastName}".Trim() : null,
                JustSignedPosition = justSignedWin?.Player?.Position,
                JustSignedSalary = justSignedWin?.BidSalary ?? 0,
                JustSignedYears = justSignedWin?.BidLength ?? 0,
                CapRoom = capRoom,
                IsBigSpend = isBigSpend,
                TotalSpend = totalSpend,
                DominantPosition = dominantPos,
                IsRoomLeft = isRoomLeft,
                IsFewestNegotiations = isFewest,
                BidCount = bidCount,
                IsMaxCapRoom = isMaxCapRoom,
                IsTopNegotiator = isTopNegotiator,
                TopNegotiatorBidCount = ctx.TopNegotiatorBidCount,
            };
        }

        private static HeadlineDTO ToDto(HeadlineEntity e) => new HeadlineDTO
        {
            HeadlineId = e.Headlineid,
            LeagueId = e.Leagueid,
            ReferenceKind = e.ReferenceKind,
            ReferenceId = e.ReferenceId,
            Text = e.Text,
            Tags = e.Tags,
            CreatedAt = e.CreatedAt,
            ExpiresAt = e.ExpiresAt,
        };

        public static class BidAnalyzer
        {
            public record BidRow(int BidId, int OwnerId, int BidSalary, string? OwnerName);

            public class Result
            {
                public int HandoffCount { get; init; }
                public int SeriousBidderCount { get; init; }
                public List<string> WarOpponentLastNames { get; init; } = new();
            }

            public static Result Analyze(IEnumerable<BidRow> bidsEnum)
            {
                var bids = bidsEnum.OrderBy(b => b.BidId).ToList();
                if (bids.Count == 0) return new Result();

                var topBid = bids[bids.Count - 1].BidSalary;
                var threshold = Math.Max(0.6 * topBid, topBid - 10);

                var latestPerOwner = bids
                    .GroupBy(b => b.OwnerId)
                    .ToDictionary(g => g.Key, g => g.Last());

                var seriousOwnerIds = latestPerOwner
                    .Where(kv => kv.Value.BidSalary >= threshold)
                    .Select(kv => kv.Key)
                    .ToHashSet();

                int handoffs = 0;
                int? lastOwner = null;
                foreach (var b in bids)
                {
                    if (lastOwner != null && lastOwner != b.OwnerId
                        && seriousOwnerIds.Contains(b.OwnerId)
                        && seriousOwnerIds.Contains(lastOwner.Value))
                    {
                        handoffs++;
                    }
                    lastOwner = b.OwnerId;
                }

                var warOpponents = bids
                    .OrderByDescending(b => b.BidId)
                    .Where(b => seriousOwnerIds.Contains(b.OwnerId) && !string.IsNullOrEmpty(b.OwnerName))
                    .Select(b => LastTokenOf(b.OwnerName!))
                    .Distinct()
                    .Take(3)
                    .ToList();

                return new Result
                {
                    HandoffCount = handoffs,
                    SeriousBidderCount = seriousOwnerIds.Count,
                    WarOpponentLastNames = warOpponents,
                };
            }

            public static string LastTokenOf(string name)
            {
                if (string.IsNullOrWhiteSpace(name)) return name ?? "";
                var trimmed = name.Trim();
                var idx = trimmed.LastIndexOf(' ');
                return idx < 0 ? trimmed : trimmed.Substring(idx + 1);
            }
        }

        private class OwnerContext
        {
            public Dictionary<int, int> SpendByOwner { get; set; } = new();
            public Dictionary<int, string?> DominantPosByOwner { get; set; } = new();
            public Dictionary<int, int> BidCountByOwner { get; set; } = new();
            public HashSet<int> Top3SpenderOwnerIds { get; set; } = new();
            public double LeagueAvgCap { get; set; }
            public int? MinBidCount { get; set; }
            public int? MaxCapRoomOwnerId { get; set; }
            public int? TopNegotiatorOwnerId { get; set; }
            public int TopNegotiatorBidCount { get; set; }
        }
    }
}
