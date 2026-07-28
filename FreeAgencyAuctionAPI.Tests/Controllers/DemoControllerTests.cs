using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Controllers
{
    public class DemoControllerTests
    {
        private const int DemoLeague = -32;

        private AuctionContext BuildDb(string name)
        {
            var opts = new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(name).Options;
            var db = new AuctionContext(opts);
            db.Leagues.Add(new LeagueEntity
            {
                Mflid = DemoLeague,
                Name = "Demo League",
                FirstYear = 2020,
                Isauctioning = false,
                IsFranchiseTagSzn = true,
                IsTaxiSzn = true,
                IsBuyoutSzn = true
            });
            db.SaveChanges();
            return db;
        }

        private static List<OpposingFranchiseDTO> DemoOwners() => new()
        {
            new OpposingFranchiseDTO { Leagueownerid = 88, Mflfranchiseid = 1, CapRoom = 397, YearsLeft = 44, TeamName = "My lil' test team", OwnerName = "Tester" },
            new OpposingFranchiseDTO { Leagueownerid = 49, Mflfranchiseid = 32, CapRoom = 118, YearsLeft = 42, TeamName = "", OwnerName = "Al" },
            new OpposingFranchiseDTO { Leagueownerid = 50, Mflfranchiseid = 24, CapRoom = 114, YearsLeft = 19, TeamName = "", OwnerName = "Bo" },
        };

        private static List<PlayerDTO> DemoFreeAgents(int count = 12) =>
            Enumerable.Range(1, count)
                .Select(i => new PlayerDTO { MflId = i, FirstName = $"Free{i}", LastName = "Agent", Position = "WR", LastSeasonPts = count - i })
                .ToList();

        private DemoController BuildController(AuctionContext db, Mock<IOwnerService> ownerSvc, Mock<IPlayerService> playerSvc)
        {
            return new DemoController(ownerSvc.Object, playerSvc.Object, db, new Mock<ILogger<DemoController>>().Object);
        }

        [Fact]
        public async Task Bootstrap_ReturnsDemoProfile_ScopedToDemoLeague()
        {
            var db = BuildDb(nameof(Bootstrap_ReturnsDemoProfile_ScopedToDemoLeague));
            var ownerSvc = new Mock<IOwnerService>();
            ownerSvc.Setup(s => s.GetAllOwners(DemoLeague)).ReturnsAsync(DemoOwners());
            var playerSvc = new Mock<IPlayerService>();

            var controller = BuildController(db, ownerSvc, playerSvc);

            var result = Assert.IsType<OkObjectResult>(await controller.Bootstrap());
            var profile = Assert.IsType<OwnerDTO>(result.Value);

            Assert.Equal("demo|fanpools", profile.Ownername);
            var league = Assert.Single(profile.Leagues);
            Assert.Equal(DemoLeague, league.League.LeagueId);
            Assert.True(league.League.IsAuctioning); // forced on for the demo
            Assert.Equal("My lil' test team", league.TeamName); // seeded team preferred
        }

        [Fact]
        public async Task Bootstrap_NoOwners_ReturnsNotFound()
        {
            var db = BuildDb(nameof(Bootstrap_NoOwners_ReturnsNotFound));
            var ownerSvc = new Mock<IOwnerService>();
            ownerSvc.Setup(s => s.GetAllOwners(DemoLeague)).ReturnsAsync(new List<OpposingFranchiseDTO>());
            var playerSvc = new Mock<IPlayerService>();

            var controller = BuildController(db, ownerSvc, playerSvc);

            Assert.IsType<NotFoundObjectResult>(await controller.Bootstrap());
        }

        [Fact]
        public async Task AuctionBundle_BuildsLiveLots_WithFutureExpiries()
        {
            var db = BuildDb(nameof(AuctionBundle_BuildsLiveLots_WithFutureExpiries));
            var ownerSvc = new Mock<IOwnerService>();
            ownerSvc.Setup(s => s.GetAllOwners(DemoLeague)).ReturnsAsync(DemoOwners());
            var playerSvc = new Mock<IPlayerService>();
            playerSvc.Setup(s => s.GetAllFreeAgents(DemoLeague)).ReturnsAsync(DemoFreeAgents(12));

            var controller = BuildController(db, ownerSvc, playerSvc);

            var result = Assert.IsType<OkObjectResult>(await controller.AuctionBundle());
            var data = Assert.IsType<LoadData>(result.Value);

            var activeLots = data.lots.Where(l => l.Bid != null).ToList();
            Assert.NotEmpty(activeLots);
            Assert.All(activeLots, l =>
            {
                Assert.Equal(DemoLeague, l.LeagueId);
                Assert.Equal(DemoLeague, l.Bid.LeagueId);
                Assert.True(l.Bid.Expires > DateTime.UtcNow, "lot timer must be in the future");
                // A real nomination/bid caps the clock at 18h; demo must never exceed it.
                Assert.True(l.Bid.Expires < DateTime.UtcNow.AddHours(18), "lot timer must be under 18h");
                Assert.NotNull(l.Bid.Player);
            });

            // Open lots exist for the nominate UI.
            Assert.Contains(data.lots, l => l.Bid == null);

            // Free agents on the board are excluded from the free-agent pool.
            var lottedIds = activeLots.Select(l => l.Bid.Player.MflId).ToHashSet();
            Assert.DoesNotContain(data.freeAgents, p => lottedIds.Contains(p.MflId));
        }

        [Fact]
        public async Task AuctionBundle_BoardHasPositionVariety_NotAllQbs()
        {
            var db = BuildDb(nameof(AuctionBundle_BoardHasPositionVariety_NotAllQbs));
            var ownerSvc = new Mock<IOwnerService>();
            ownerSvc.Setup(s => s.GetAllOwners(DemoLeague)).ReturnsAsync(DemoOwners());

            // A points-sorted pool where the top scorers are all QBs (the real-world case).
            var mixed = new List<PlayerDTO>();
            var id = 1;
            foreach (var (pos, n, basePts) in new[] { ("QB", 10, 400m), ("WR", 20, 200m), ("RB", 20, 180m), ("TE", 10, 120m) })
                for (var k = 0; k < n; k++)
                    mixed.Add(new PlayerDTO { MflId = id++, FirstName = $"{pos}{k}", LastName = "Demo", Position = pos, LastSeasonPts = basePts - k });

            var playerSvc = new Mock<IPlayerService>();
            playerSvc.Setup(s => s.GetAllFreeAgents(DemoLeague)).ReturnsAsync(mixed);

            var controller = BuildController(db, ownerSvc, playerSvc);

            var result = Assert.IsType<OkObjectResult>(await controller.AuctionBundle());
            var data = Assert.IsType<LoadData>(result.Value);

            var positions = data.lots.Where(l => l.Bid?.Player != null)
                .Select(l => l.Bid.Player.Position).Distinct().ToList();
            Assert.True(positions.Count >= 3, $"board should span positions, saw: {string.Join(",", positions)}");
        }

        [Fact]
        public async Task Rosters_DistributesPlayersAcrossDemoFranchises()
        {
            var db = BuildDb(nameof(Rosters_DistributesPlayersAcrossDemoFranchises));
            var ownerSvc = new Mock<IOwnerService>();
            ownerSvc.Setup(s => s.GetAllOwners(DemoLeague)).ReturnsAsync(DemoOwners());
            var playerSvc = new Mock<IPlayerService>();
            playerSvc.Setup(s => s.GetAllFreeAgents(DemoLeague)).ReturnsAsync(DemoFreeAgents(12));

            var controller = BuildController(db, ownerSvc, playerSvc);

            var result = Assert.IsType<OkObjectResult>(await controller.Rosters());
            var rosters = Assert.IsType<List<OpposingFranchiseWithRoster>>(result.Value);

            Assert.Equal(DemoOwners().Count, rosters.Count);
            Assert.Contains(rosters, r => r.Players.Count > 0);
            Assert.All(rosters, r => Assert.All(r.Players, p =>
            {
                Assert.Equal(r.Mflfranchiseid, p.MflFranchiseId);
                Assert.True(p.Salary > 0);
                Assert.True(p.Length >= 1);
            }));
        }

        [Fact]
        public async Task Rosters_GivesUniqueFranchiseIds_WhenOwnersCollide()
        {
            var db = BuildDb(nameof(Rosters_GivesUniqueFranchiseIds_WhenOwnersCollide));
            // Demo owners with duplicate (0) and colliding franchise ids — the real defect.
            var owners = new List<OpposingFranchiseDTO>
            {
                new() { Leagueownerid = 88, Mflfranchiseid = 0, OwnerName = "A" },
                new() { Leagueownerid = 49, Mflfranchiseid = 0, OwnerName = "B" },
                new() { Leagueownerid = 50, Mflfranchiseid = 5, OwnerName = "C" },
                new() { Leagueownerid = 51, Mflfranchiseid = 5, OwnerName = "D" },
            };
            var ownerSvc = new Mock<IOwnerService>();
            ownerSvc.Setup(s => s.GetAllOwners(DemoLeague)).ReturnsAsync(owners);
            var playerSvc = new Mock<IPlayerService>();
            playerSvc.Setup(s => s.GetAllFreeAgents(DemoLeague)).ReturnsAsync(DemoFreeAgents(40));

            var controller = BuildController(db, ownerSvc, playerSvc);

            var result = Assert.IsType<OkObjectResult>(await controller.Rosters());
            var rosters = Assert.IsType<List<OpposingFranchiseWithRoster>>(result.Value);

            var ids = rosters.Select(r => r.Mflfranchiseid).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count()); // all unique → no React key collision
        }
    }
}
