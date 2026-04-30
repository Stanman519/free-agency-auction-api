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
    public class OwnerQuoteRepoTests
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
            var repo = new OwnerQuoteRepo(db, NullLogger<OwnerQuoteRepo>.Instance);

            await repo.Upsert(13894, 5, 12345, "first take");
            await repo.Upsert(13894, 5, 12345, "updated take");

            var rows = await db.OwnerQuotes
                .Where(q => q.Leagueid == 13894 && q.Ownerid == 5 && q.PlayerMflId == 12345)
                .ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.Single(rows, r => r.IsActive);
            Assert.Equal("updated take", rows.Single(r => r.IsActive).Text);
        }

        [Fact]
        public async Task Upsert_DifferentOwnersOnSamePlayer_BothActive()
        {
            using var db = NewDb();
            var repo = new OwnerQuoteRepo(db, NullLogger<OwnerQuoteRepo>.Instance);

            await repo.Upsert(13894, 1, 100, "owner 1 take");
            await repo.Upsert(13894, 2, 100, "owner 2 take");

            var active = await db.OwnerQuotes.Where(q => q.IsActive && q.PlayerMflId == 100).ToListAsync();
            Assert.Equal(2, active.Count);
        }

        [Fact]
        public async Task DeactivateForPlayer_DeactivatesAllOwnersQuotesForThatPlayer()
        {
            using var db = NewDb();
            var repo = new OwnerQuoteRepo(db, NullLogger<OwnerQuoteRepo>.Instance);

            await repo.Upsert(13894, 1, 100, "a");
            await repo.Upsert(13894, 2, 100, "b");
            await repo.Upsert(13894, 1, 200, "different player");

            var deactivated = await repo.DeactivateForPlayer(13894, 100);
            Assert.Equal(2, deactivated);

            var active = await db.OwnerQuotes.Where(q => q.IsActive).ToListAsync();
            Assert.Single(active);
            Assert.Equal(200, active[0].PlayerMflId);
        }

        [Fact]
        public async Task DeactivateForOwnerPlayer_OnlyAffectsThatOwner()
        {
            using var db = NewDb();
            var repo = new OwnerQuoteRepo(db, NullLogger<OwnerQuoteRepo>.Instance);

            await repo.Upsert(13894, 1, 100, "a");
            await repo.Upsert(13894, 2, 100, "b");

            await repo.Upsert(13894, 1, 100, "a"); // recreate to ensure deactivate works
            var deactivated = await repo.DeactivateForOwnerPlayer(13894, 1, 100);
            Assert.Equal(1, deactivated);

            var active = await db.OwnerQuotes.Where(q => q.IsActive).ToListAsync();
            Assert.Single(active);
            Assert.Equal(2, active[0].Ownerid);
        }

        [Fact]
        public async Task GetActiveByOwnerPlayer_ReturnsLatestActiveOnly()
        {
            using var db = NewDb();
            var repo = new OwnerQuoteRepo(db, NullLogger<OwnerQuoteRepo>.Instance);

            await repo.Upsert(13894, 5, 12345, "old");
            await repo.Upsert(13894, 5, 12345, "new");

            var found = await repo.GetActiveByOwnerPlayer(13894, 5, 12345);
            Assert.NotNull(found);
            Assert.Equal("new", found!.Text);
        }
    }
}
