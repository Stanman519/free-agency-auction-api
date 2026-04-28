using System;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Services
{
    public class WinProcessorServiceTests
    {
        private AuctionContext BuildDb(string name)
        {
            var opts = new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(name).Options;
            return new AuctionContext(opts);
        }

        private IServiceScopeFactory BuildScopeFactory(AuctionContext db, IMflService mflService, IOwnerRepo ownerRepo, IGMBot gmBot)
        {
            var sp = new Mock<IServiceProvider>();
            sp.Setup(x => x.GetService(typeof(AuctionContext))).Returns(db);
            sp.Setup(x => x.GetService(typeof(IMflService))).Returns(mflService);
            sp.Setup(x => x.GetService(typeof(IOwnerRepo))).Returns(ownerRepo);
            sp.Setup(x => x.GetService(typeof(IGMBot))).Returns(gmBot);

            var scope = new Mock<IServiceScope>();
            scope.Setup(x => x.ServiceProvider).Returns(sp.Object);

            var factory = new Mock<IServiceScopeFactory>();
            factory.Setup(x => x.CreateScope()).Returns(scope.Object);
            return factory.Object;
        }

        [Fact]
        public async Task ProcessWin_DemoLeague_SkipsMflCallsAndDecrementsCapRoom()
        {
            var db = BuildDb("WinProc_Demo");
            var owner = new LeagueOwnerEntity { Leagueownerid = 10, Leagueid = -32, Mflfranchiseid = 1, Caproom = 200 };
            db.LeagueOwners.Add(owner);
            var bid = new BidEntity
            {
                Bidid = 1,
                Mflid = 99,
                Leagueid = -32,
                Ownerid = 10,
                Bidsalary = 25,
                Bidlength = 2,
                Expires = DateTime.UtcNow.AddSeconds(-1)
            };
            db.Bids.Add(bid);
            await db.SaveChangesAsync();

            var mflMock = new Mock<IMflService>();
            var ownerRepoMock = new Mock<IOwnerRepo>();
            var gmBotMock = new Mock<IGMBot>();
            var logger = new Mock<ILogger<WinProcessorService>>();

            var factory = BuildScopeFactory(db, mflMock.Object, ownerRepoMock.Object, gmBotMock.Object);
            var service = new WinProcessorService(factory, logger.Object);

            var bidDto = new BidDTO
            {
                BidId = 1,
                BidSalary = 25,
                BidLength = 2,
                OwnerId = 10,
                LeagueId = -32,
                Expires = bid.Expires,
                Player = new PlayerDTO { MflId = 99, FirstName = "Demo", LastName = "Player" }
            };

            await service.ProcessWin(bidDto);

            var updatedOwner = await db.LeagueOwners.FirstAsync(o => o.Leagueownerid == 10);
            Assert.Equal(175, updatedOwner.Caproom);
            mflMock.Verify(m => m.GetMflRosters(It.IsAny<int>()), Times.Never);
            mflMock.Verify(m => m.AddPlayerToTeam(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            mflMock.Verify(m => m.GiveNewContractToPlayer(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ProcessWin_DemoLeague_NotLatestBid_DoesNothing()
        {
            var db = BuildDb("WinProc_Demo_NotLatest");
            var owner = new LeagueOwnerEntity { Leagueownerid = 10, Leagueid = -32, Mflfranchiseid = 1, Caproom = 200 };
            db.LeagueOwners.Add(owner);
            db.Bids.Add(new BidEntity { Bidid = 1, Mflid = 99, Leagueid = -32, Ownerid = 10, Bidsalary = 25, Bidlength = 2, Expires = DateTime.UtcNow.AddSeconds(-1) });
            db.Bids.Add(new BidEntity { Bidid = 2, Mflid = 99, Leagueid = -32, Ownerid = 10, Bidsalary = 30, Bidlength = 2, Expires = DateTime.UtcNow.AddSeconds(-1) });
            await db.SaveChangesAsync();

            var mflMock = new Mock<IMflService>();
            var factory = BuildScopeFactory(db, mflMock.Object, new Mock<IOwnerRepo>().Object, new Mock<IGMBot>().Object);
            var service = new WinProcessorService(factory, new Mock<ILogger<WinProcessorService>>().Object);

            var bidDto = new BidDTO
            {
                BidId = 1,
                BidSalary = 25,
                OwnerId = 10,
                LeagueId = -32,
                Expires = DateTime.UtcNow.AddSeconds(-1),
                Player = new PlayerDTO { MflId = 99 }
            };

            await service.ProcessWin(bidDto);

            var updatedOwner = await db.LeagueOwners.FirstAsync(o => o.Leagueownerid == 10);
            Assert.Equal(200, updatedOwner.Caproom);
        }
    }
}
