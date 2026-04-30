using System;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Repos
{
    public class HeadlineRepoTests
    {
        private static AuctionContext NewDb()
        {
            var opts = new DbContextOptionsBuilder<AuctionContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AuctionContext(opts);
        }

        [Fact]
        public async Task Upsert_SetsPriorActiveToFalse()
        {
            using var db = NewDb();
            var repo = new HeadlineRepo(db, NullLogger<HeadlineRepo>.Instance);

            await repo.Upsert(13894, HeadlineRefKind.Player, 12345, "first", "Win", null);
            await repo.Upsert(13894, HeadlineRefKind.Player, 12345, "second", "Win,SagaLength", null);

            var rows = await db.Headlines.Where(h => h.Leagueid == 13894 && h.ReferenceId == 12345).ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.Single(rows, r => r.IsActive);
            Assert.Equal("second", rows.Single(r => r.IsActive).Text);
        }

        [Fact]
        public async Task Upsert_DifferentReferences_BothRemainActive()
        {
            using var db = NewDb();
            var repo = new HeadlineRepo(db, NullLogger<HeadlineRepo>.Instance);

            await repo.Upsert(13894, HeadlineRefKind.Player, 1, "p1", "Win", null);
            await repo.Upsert(13894, HeadlineRefKind.Player, 2, "p2", "Win", null);
            await repo.Upsert(13894, HeadlineRefKind.Owner, 1, "o1", "BigSpend", null);

            var active = await repo.GetActive(13894);
            Assert.Equal(3, active.Count);
        }

        [Fact]
        public async Task GetActiveByRef_ReturnsOnlyActiveRow()
        {
            using var db = NewDb();
            var repo = new HeadlineRepo(db, NullLogger<HeadlineRepo>.Instance);

            await repo.Upsert(13894, HeadlineRefKind.Player, 99, "old", "Win", null);
            await repo.Upsert(13894, HeadlineRefKind.Player, 99, "new", "Win", null);

            var found = await repo.GetActiveByRef(13894, HeadlineRefKind.Player, 99);
            Assert.NotNull(found);
            Assert.Equal("new", found!.Text);
        }

        [Fact]
        public async Task DeleteExpired_DeactivatesOnlyExpiredActive()
        {
            using var db = NewDb();
            var repo = new HeadlineRepo(db, NullLogger<HeadlineRepo>.Instance);

            await repo.Upsert(13894, HeadlineRefKind.Player, 1, "expired", "Win", DateTime.UtcNow.AddHours(-1));
            await repo.Upsert(13894, HeadlineRefKind.Player, 2, "fresh", "Win", DateTime.UtcNow.AddHours(1));
            await repo.Upsert(13894, HeadlineRefKind.Player, 3, "perm", "BiddingWar", null);

            var deactivatedCount = await repo.DeleteExpired(13894);
            Assert.Equal(1, deactivatedCount);

            var active = await repo.GetActive(13894);
            Assert.Equal(2, active.Count);
            Assert.DoesNotContain(active, h => h.Text == "expired");
        }

        [Fact]
        public async Task GetActive_SortsByCreatedAtDesc()
        {
            using var db = NewDb();
            var repo = new HeadlineRepo(db, NullLogger<HeadlineRepo>.Instance);

            await repo.Upsert(13894, HeadlineRefKind.Player, 1, "first", "", null);
            await Task.Delay(10);
            await repo.Upsert(13894, HeadlineRefKind.Player, 2, "second", "", null);

            var active = await repo.GetActive(13894);
            Assert.Equal("second", active[0].Text);
            Assert.Equal("first", active[1].Text);
        }
    }
}
