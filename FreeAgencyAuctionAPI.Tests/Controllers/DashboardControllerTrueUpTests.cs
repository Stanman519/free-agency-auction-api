using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Controllers
{
    public class DashboardControllerTrueUpTests
    {
        private static AuctionContext NewDb(string name)
        {
            var opts = new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(name).Options;
            return new AuctionContext(opts);
        }

        private static DashboardController BuildController(AuctionContext db, IMflService mfl)
        {
            return new DashboardController(
                Mock.Of<ILeagueService>(),
                mfl,
                Mock.Of<IOwnerService>(),
                Mock.Of<IPlayerRepo>(),
                Mock.Of<ILogger<DashboardController>>(),
                db,
                Mock.Of<IGMBot>(),
                Mock.Of<IOwnerRepo>());
        }

        [Fact]
        public async Task TrueUpSalaryCaps_UpdatesCaproomForMatchingFranchises()
        {
            var leagueId = 13894;
            var db = NewDb(nameof(TrueUpSalaryCaps_UpdatesCaproomForMatchingFranchises));
            db.LeagueOwners.AddRange(
                new LeagueOwnerEntity { Leagueownerid = 1, Leagueid = leagueId, Mflfranchiseid = 1, Caproom = 100 },
                new LeagueOwnerEntity { Leagueownerid = 2, Leagueid = leagueId, Mflfranchiseid = 2, Caproom = 100 },
                new LeagueOwnerEntity { Leagueownerid = 3, Leagueid = leagueId, Mflfranchiseid = 3, Caproom = 100 });
            await db.SaveChangesAsync();

            var mflMock = new Mock<IMflService>();
            mflMock.Setup(m => m.GetSalaryCapRoom(leagueId)).ReturnsAsync(new List<LeagueOwnerEntity>
            {
                new LeagueOwnerEntity { Mflfranchiseid = 3, Caproom = 30 },
                new LeagueOwnerEntity { Mflfranchiseid = 1, Caproom = 10 },
                new LeagueOwnerEntity { Mflfranchiseid = 2, Caproom = 20 },
            });

            var controller = BuildController(db, mflMock.Object);

            var result = await controller.TrueUpSalaryCaps(leagueId);

            Assert.IsType<OkObjectResult>(result);
            var owners = await db.LeagueOwners.OrderBy(o => o.Mflfranchiseid).ToListAsync();
            Assert.Equal(new int?[] { 10, 20, 30 }, owners.Select(o => o.Caproom).ToArray());
        }

        [Fact]
        public async Task TrueUpSalaryCaps_NullCaproomFromMfl_TreatedAsZero()
        {
            var leagueId = 26548;
            var db = NewDb(nameof(TrueUpSalaryCaps_NullCaproomFromMfl_TreatedAsZero));
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 1, Leagueid = leagueId, Mflfranchiseid = 1, Caproom = 50 });
            await db.SaveChangesAsync();

            var mflMock = new Mock<IMflService>();
            mflMock.Setup(m => m.GetSalaryCapRoom(leagueId)).ReturnsAsync(new List<LeagueOwnerEntity>
            {
                new LeagueOwnerEntity { Mflfranchiseid = 1, Caproom = null },
            });

            var controller = BuildController(db, mflMock.Object);

            await controller.TrueUpSalaryCaps(leagueId);

            var owner = await db.LeagueOwners.FirstAsync();
            Assert.Equal(0, owner.Caproom);
        }
    }
}
