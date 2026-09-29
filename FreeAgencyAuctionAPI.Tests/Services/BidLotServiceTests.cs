using System;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Services
{
    public class BidLotServiceTests
    {
        private const int LeagueId = 13894;

        private static AuctionContext NewDb() =>
            new AuctionContext(new DbContextOptionsBuilder<AuctionContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static BidLotService NewService(AuctionContext db, Mock<IGMBot> bot)
        {
            var repo = new BidLotRepo(db, NullLogger<BidLotRepo>.Instance, new Mock<AutoMapper.IMapper>().Object);
            return new BidLotService(new Mock<AutoMapper.IMapper>().Object, new Mock<IQueueService>().Object, repo, bot.Object, NullLogger<BidLotService>.Instance);
        }

        [Fact]
        public async Task PostNewBidChangesToGroup_SkipsWhenNotAuctioning()
        {
            var db = NewDb();
            db.Leagues.Add(new LeagueEntity { Mflid = LeagueId, Name = "L", Isauctioning = false });
            db.SaveChanges();
            var bot = new Mock<IGMBot>();
            var service = NewService(db, bot);

            await service.PostNewBidChangesToGroup(LeagueId);

            bot.Verify(b => b.SendBotNotification(It.IsAny<BotMessage>()), Times.Never);
        }

        [Fact]
        public async Task PostNewBidChangesToGroup_SkipsWhenLeagueMissing()
        {
            var db = NewDb();
            var bot = new Mock<IGMBot>();
            var service = NewService(db, bot);

            await service.PostNewBidChangesToGroup(LeagueId);

            bot.Verify(b => b.SendBotNotification(It.IsAny<BotMessage>()), Times.Never);
        }

        [Fact]
        public async Task PostNewBidChangesToGroup_ProceedsWhenAuctioning()
        {
            var db = NewDb();
            db.Leagues.Add(new LeagueEntity { Mflid = LeagueId, Name = "L", Isauctioning = true });
            var ownerEnt = new OwnerEntity { Ownerid = 5, Ownername = "Drew" };
            db.Owners.Add(ownerEnt);
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 10, Leagueid = LeagueId, Ownerid = 5, Mflfranchiseid = 1, Caproom = 200, Teamname = "Drew", Owner = ownerEnt });
            db.Players.Add(new PlayerEntity { Mflid = 99, Firstname = "Star", Lastname = "Receiver", Position = "WR" });
            db.Bids.Add(new BidEntity { Bidid = 1, Mflid = 99, Leagueid = LeagueId, Ownerid = 10, Bidsalary = 10, Bidlength = 1, Expires = DateTime.UtcNow.AddHours(30) });
            db.SaveChanges();
            var bot = new Mock<IGMBot>();
            var service = NewService(db, bot);

            await service.PostNewBidChangesToGroup(LeagueId);

            bot.Verify(b => b.SendBotNotification(It.IsAny<BotMessage>()), Times.Once);
        }
    }
}
