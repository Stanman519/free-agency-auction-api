using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Mapping;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.OverUnders;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Controllers
{
    public class OverUnderControllerTests
    {
        private const int OpenPoolId = 1;
        private const int ClosedPoolId = 2;
        private const int OwnerId = 42;

        private AuctionContext BuildDb(string name)
        {
            var opts = new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(name).Options;
            var db = new AuctionContext(opts);
            db.Pools.Add(new Pool
            {
                Id = OpenPoolId,
                Year = 2026,
                Type = "over-under-wins",
                League = "NFL",
                Name = "open",
                OpenDate = DateTime.UtcNow.AddDays(-1),
                StartDate = DateTime.UtcNow.AddDays(14)
            });
            db.Pools.Add(new Pool
            {
                Id = ClosedPoolId,
                Year = 2024,
                Type = "over-under-wins",
                League = "NFL",
                Name = "closed",
                OpenDate = DateTime.UtcNow.AddYears(-2),
                StartDate = DateTime.UtcNow.AddYears(-2).AddDays(30)
            });
            db.SaveChanges();
            return db;
        }

        // Use the app's real mapping profile — a mocked IMapper returns null and
        // the save path would NRE on it rather than exercising anything useful.
        private static IMapper BuildMapper() =>
            new MapperConfiguration(cfg => cfg.AddProfile<OverUnderPickProfile>())
                .CreateMapper();

        private OverUnderController BuildController(AuctionContext db)
        {
            var options = new Mock<IOptionsSnapshot<AppConfig>>();
            options.SetupGet(o => o.Value).Returns(new AppConfig());
            return new OverUnderController(
                db,
                BuildMapper(),
                new Mock<ILogger<OverUnderController>>().Object,
                new Mock<ISportsDataApi>().Object,
                options.Object);
        }

        private static List<OverUnderPickDTO> Picks(int poolId) => new()
        {
            new OverUnderPickDTO { LineId = 1, IsOver = true, LineAdjustment = 0, PoolId = poolId },
            new OverUnderPickDTO { LineId = 2, IsOver = false, LineAdjustment = 1, PoolId = poolId },
        };

        [Fact]
        public async Task SavePicks_RejectsPool_ThatHasAlreadyStarted()
        {
            var db = BuildDb(nameof(SavePicks_RejectsPool_ThatHasAlreadyStarted));
            var controller = BuildController(db);

            var result = await controller.UpsertTeamWinTotals(ClosedPoolId, OwnerId, Picks(ClosedPoolId));

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(db.OverUnderPicks.ToList());
        }

        [Fact]
        public async Task SavePicks_AcceptsPool_StillOpen()
        {
            var db = BuildDb(nameof(SavePicks_AcceptsPool_StillOpen));
            var controller = BuildController(db);

            var result = await controller.UpsertTeamWinTotals(OpenPoolId, OwnerId, Picks(OpenPoolId));

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal(2, db.OverUnderPicks.Count());
        }

        [Fact]
        public async Task SavePicks_ReturnsNotFound_ForUnknownPool()
        {
            var db = BuildDb(nameof(SavePicks_ReturnsNotFound_ForUnknownPool));
            var controller = BuildController(db);

            var result = await controller.UpsertTeamWinTotals(9999, OwnerId, Picks(9999));

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetAllUsersAndPicks_ReturnsNotFound_ForUnknownPool()
        {
            var db = BuildDb(nameof(GetAllUsersAndPicks_ReturnsNotFound_ForUnknownPool));
            var controller = BuildController(db);

            // Previously dereferenced a null pool and threw.
            var result = await controller.GetAllUsersAndPicksForPool(9999);

            Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}
