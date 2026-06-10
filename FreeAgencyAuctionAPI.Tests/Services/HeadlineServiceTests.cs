using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI;
using FreeAgencyAuctionAPI.Hub;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Services
{
    public class HeadlineServiceTests
    {
        private const int LeagueId = 13894;

        private static AuctionContext NewDb() =>
            new AuctionContext(new DbContextOptionsBuilder<AuctionContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static HeadlineService NewService(AuctionContext db, Mock<IMflService> mfl)
        {
            var repo = new HeadlineRepo(db, NullLogger<HeadlineRepo>.Instance);
            var hub = new Mock<IHubContext<AuctionHub>>(); // broadcast is wrapped in try/catch; upsert persists first
            return new HeadlineService(db, repo, hub.Object, new Mock<IMflApi>().Object, mfl.Object,
                Options.Create(new AppConfig()), NullLogger<HeadlineService>.Instance);
        }

        private static FranchiseRoster Fr(string id, params (string id, string sal, string yr)[] players) =>
            new FranchiseRoster
            {
                id = id,
                player = players.Select(p => new Player { id = p.id, salary = p.sal, contractYear = p.yr, status = "ROSTER" }).ToList(),
            };

        private static BidDTO Win(int salary) => new BidDTO
        {
            BidId = 1, BidSalary = salary, BidLength = 3, OwnerId = 10, LeagueId = LeagueId,
            Ownername = "Drew", Expires = DateTime.UtcNow,
            Player = new PlayerDTO { MflId = 99, FirstName = "Star", LastName = "Receiver", Position = "WR" },
        };

        // Seeds a 2-team league. Owner 10 (franchise 1) signs player 99. League WR salaries: 40, 30, 20.
        private static void SeedWinScenario(AuctionContext db)
        {
            db.Leagues.Add(new LeagueEntity { Mflid = LeagueId, Name = "L", Isauctioning = true });
            var ownerEnt = new OwnerEntity { Ownerid = 5, Ownername = "Drew" };
            db.Owners.Add(ownerEnt);
            var lo10 = new LeagueOwnerEntity { Leagueownerid = 10, Leagueid = LeagueId, Ownerid = 5, Mflfranchiseid = 1, Caproom = 200, Teamname = "Drew", Owner = ownerEnt };
            db.LeagueOwners.Add(lo10);
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 20, Leagueid = LeagueId, Ownerid = 6, Mflfranchiseid = 2, Caproom = 150, Teamname = "Other" });

            db.Players.Add(new PlayerEntity { Mflid = 99, Firstname = "Star", Lastname = "Receiver", Position = "WR" });
            db.Players.Add(new PlayerEntity { Mflid = 100, Firstname = "A", Lastname = "A", Position = "WR" });
            db.Players.Add(new PlayerEntity { Mflid = 200, Firstname = "B", Lastname = "B", Position = "WR" });
            db.Players.Add(new PlayerEntity { Mflid = 300, Firstname = "C", Lastname = "C", Position = "WR" });

            db.Bids.Add(new BidEntity { Bidid = 1, Mflid = 99, Leagueid = LeagueId, Ownerid = 10, Bidsalary = 60, Bidlength = 3, Expires = DateTime.UtcNow, LeagueOwner = lo10 });
            db.SaveChanges();
        }

        private static Mock<IMflService> RostersMock()
        {
            var mfl = new Mock<IMflService>();
            mfl.Setup(m => m.GetMflRosters(LeagueId)).ReturnsAsync(new List<FranchiseRoster>
            {
                Fr("0001", ("100", "40", "2"), ("200", "20", "1")),
                Fr("0002", ("300", "30", "2")),
            });
            return mfl;
        }

        [Fact]
        public async Task OnWin_LeagueTopSalary_TagsTopMoneyWithTierAndRank()
        {
            using var db = NewDb();
            SeedWinScenario(db);
            var svc = NewService(db, RostersMock());

            await svc.OnWin(Win(60)); // $60 beats every league WR (40/30/20) -> rank 1 of 2 teams

            var headlines = await svc.GetActive(LeagueId);
            var player = headlines.Single(h => h.ReferenceKind == HeadlineRefKind.Player && h.ReferenceId == 99);
            Assert.Contains("TopMoney", player.Tags);
            Assert.Contains("WR1 money", player.Text);
            Assert.Contains("1st", player.Text);
        }

        [Fact]
        public async Task OnWin_LowSalary_DoesNotTagTopMoney()
        {
            using var db = NewDb();
            SeedWinScenario(db);
            var svc = NewService(db, RostersMock());

            await svc.OnWin(Win(5)); // $5 ranks 4th of WRs -> beyond the 2-team starter tier

            var headlines = await svc.GetActive(LeagueId);
            var player = headlines.Single(h => h.ReferenceKind == HeadlineRefKind.Player && h.ReferenceId == 99);
            Assert.Contains("Win", player.Tags);
            Assert.DoesNotContain("TopMoney", player.Tags);
            Assert.DoesNotContain("money", player.Text); // no "WRn money" tier line
        }

        [Fact]
        public async Task OnWin_JustSignedOwner_NotTaggedDrySpell()
        {
            using var db = NewDb();
            SeedWinScenario(db);
            var svc = NewService(db, RostersMock());

            await svc.OnWin(Win(60));

            var headlines = await svc.GetActive(LeagueId);
            var owner = headlines.Single(h => h.ReferenceKind == HeadlineRefKind.Owner && h.ReferenceId == 10);
            Assert.StartsWith("JustSigned", owner.Tags);
            Assert.DoesNotContain("DrySpell", owner.Tags);
        }

        // Adds an owner who won `mflId` `daysAgo` days ago (won lot = a leading bid that has expired).
        private static void AddSignedOwner(AuctionContext db, int ownerId, int franchiseId, int mflId, int salary, int years, double daysAgo, string pos = "WR", int cap = 110)
        {
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = ownerId, Leagueid = LeagueId, Ownerid = ownerId, Mflfranchiseid = franchiseId, Caproom = cap, Teamname = "T" + ownerId });
            db.Players.Add(new PlayerEntity { Mflid = mflId, Firstname = "P", Lastname = mflId.ToString(), Position = pos });
            var bid = new BidEntity { Bidid = mflId, Mflid = mflId, Leagueid = LeagueId, Ownerid = ownerId, Bidsalary = salary, Bidlength = years, Expires = DateTime.UtcNow.AddDays(-daysAgo) };
            db.Bids.Add(bid);
            db.Lots.Add(new LotEntity { Lotid = mflId, Bidid = mflId, Leagueid = LeagueId, Bid = bid });
        }

        private static HeadlineService RecomputeService(AuctionContext db) => NewService(db, new Mock<IMflService>());

        [Fact]
        public async Task RecomputeAll_NoAuctionActivity_ProducesNoOwnerHeadlines()
        {
            // The exact pre-auction flood the user hit: auction on, zero bids -> no headlines.
            using var db = NewDb();
            db.Leagues.Add(new LeagueEntity { Mflid = LeagueId, Name = "L", Isauctioning = true });
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 50, Leagueid = LeagueId, Ownerid = 50, Mflfranchiseid = 1, Caproom = 93, Teamname = "Quiet" });
            await db.SaveChangesAsync();
            var svc = RecomputeService(db);

            await svc.RecomputeAllForLeague(LeagueId);

            Assert.Empty(await svc.GetActive(LeagueId));
        }

        [Fact]
        public async Task RecomputeAll_MarketActive_NeverSignedOwner_QuietStartWithoutDayZero()
        {
            using var db = NewDb();
            db.Leagues.Add(new LeagueEntity { Mflid = LeagueId, Name = "L", Isauctioning = true });
            AddSignedOwner(db, 10, 1, 101, 10, 1, 0.1); // three owners have signed -> market is active
            AddSignedOwner(db, 20, 2, 102, 10, 1, 0.1);
            AddSignedOwner(db, 30, 3, 103, 10, 1, 0.1);
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 50, Leagueid = LeagueId, Ownerid = 50, Mflfranchiseid = 9, Caproom = 93, Teamname = "Quiet" });
            await db.SaveChangesAsync();
            var svc = RecomputeService(db);

            await svc.RecomputeAllForLeague(LeagueId);

            var owner = (await svc.GetActive(LeagueId)).Single(h => h.ReferenceKind == HeadlineRefKind.Owner && h.ReferenceId == 50);
            Assert.StartsWith("DrySpell", owner.Tags);
            Assert.Contains("93", owner.Text);
            Assert.DoesNotContain("day 0", owner.Text);
            Assert.DoesNotContain("0 days", owner.Text);
        }

        [Fact]
        public async Task RecomputeAll_SignedFourDaysAgo_DrySpellShowsRealDayCount()
        {
            using var db = NewDb();
            db.Leagues.Add(new LeagueEntity { Mflid = LeagueId, Name = "L", Isauctioning = true });
            AddSignedOwner(db, 10, 1, 77, 20, 2, 4.0, cap: 100); // gap of 4 days, small $40 deal
            AddSignedOwner(db, 20, 2, 78, 10, 1, 0.1);           // two recent signers make the market active
            AddSignedOwner(db, 30, 3, 79, 10, 1, 0.1);
            await db.SaveChangesAsync();
            var svc = RecomputeService(db);

            await svc.RecomputeAllForLeague(LeagueId);

            var owner = (await svc.GetActive(LeagueId)).Single(h => h.ReferenceKind == HeadlineRefKind.Owner && h.ReferenceId == 10);
            Assert.StartsWith("DrySpell", owner.Tags);
            Assert.Contains("4", owner.Text);   // real day count from the won-lot timestamp
            Assert.Contains("100", owner.Text);
        }
    }
}
