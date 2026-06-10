using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Repos
{
    public class BidLotRepoExtendTests
    {
        private static AuctionContext NewDb(string name)
        {
            var opts = new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(name).Options;
            return new AuctionContext(opts);
        }

        private static BidLotRepo BuildRepo(AuctionContext db) =>
            new BidLotRepo(db, Mock.Of<ILogger<BidLotRepo>>(), Mock.Of<IMapper>());

        [Fact]
        public async Task ExtendActiveBidExpirations_OnlyExtendsActiveBids()
        {
            var db = NewDb(nameof(ExtendActiveBidExpirations_OnlyExtendsActiveBids));
            var future = DateTime.UtcNow.AddHours(5);
            var past = DateTime.UtcNow.AddHours(-1);

            db.Bids.AddRange(
                new BidEntity { Bidid = 1, Leagueid = 13894, Expires = future, Mflid = 1, Ownerid = 1, Bidlength = 1, Bidsalary = 10 },
                new BidEntity { Bidid = 2, Leagueid = 13894, Expires = future, Mflid = 2, Ownerid = 1, Bidlength = 1, Bidsalary = 10 },
                new BidEntity { Bidid = 3, Leagueid = 13894, Expires = past,   Mflid = 3, Ownerid = 1, Bidlength = 1, Bidsalary = 10 }
            );
            await db.SaveChangesAsync();

            var repo = BuildRepo(db);
            await repo.ExtendActiveBidExpirations(13894, 4);

            var bids = await db.Bids.OrderBy(b => b.Bidid).ToListAsync();
            Assert.True(bids[0].Expires >= future.AddHours(4).AddSeconds(-1));
            Assert.True(bids[1].Expires >= future.AddHours(4).AddSeconds(-1));
            Assert.Equal(past, bids[2].Expires); // expired bid unchanged
        }

        [Fact]
        public async Task ExtendActiveBidExpirations_DoesNotTouchOtherLeague()
        {
            var db = NewDb(nameof(ExtendActiveBidExpirations_DoesNotTouchOtherLeague));
            var future = DateTime.UtcNow.AddHours(5);

            db.Bids.AddRange(
                new BidEntity { Bidid = 1, Leagueid = 13894, Expires = future, Mflid = 1, Ownerid = 1, Bidlength = 1, Bidsalary = 10 },
                new BidEntity { Bidid = 2, Leagueid = 26548, Expires = future, Mflid = 2, Ownerid = 1, Bidlength = 1, Bidsalary = 10 }
            );
            await db.SaveChangesAsync();

            var repo = BuildRepo(db);
            await repo.ExtendActiveBidExpirations(13894, 3);

            var league2Bid = await db.Bids.FirstAsync(b => b.Leagueid == 26548);
            Assert.Equal(future, league2Bid.Expires);
        }

        [Fact]
        public async Task ExtendActiveBidExpirations_NoActiveBids_ReturnsEmpty()
        {
            var db = NewDb(nameof(ExtendActiveBidExpirations_NoActiveBids_ReturnsEmpty));
            db.Bids.Add(new BidEntity { Bidid = 1, Leagueid = 13894, Expires = DateTime.UtcNow.AddHours(-1), Mflid = 1, Ownerid = 1, Bidlength = 1, Bidsalary = 10 });
            await db.SaveChangesAsync();

            var repo = BuildRepo(db);
            var result = await repo.ExtendActiveBidExpirations(13894, 2);

            Assert.Empty(result);
        }
    }
}
