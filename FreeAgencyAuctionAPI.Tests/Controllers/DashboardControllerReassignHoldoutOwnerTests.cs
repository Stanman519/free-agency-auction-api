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
    public class DashboardControllerReassignHoldoutOwnerTests
    {
        private const int LeagueId = 13894;

        private static AuctionContext NewDb(string name) =>
            new(new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(name).Options);

        private static DashboardController BuildController(AuctionContext db, IPlayerRepo pRepo) => new(
            Mock.Of<ILeagueService>(), Mock.Of<IMflService>(), Mock.Of<IOwnerService>(), pRepo,
            Mock.Of<ILogger<DashboardController>>(), db, Mock.Of<IGMBot>(), Mock.Of<IOwnerRepo>());

        [Fact]
        public async Task ReassignHoldoutOwner_DelegatesToRepo_WithCurrentYear()
        {
            using var db = NewDb(nameof(ReassignHoldoutOwner_DelegatesToRepo_WithCurrentYear));
            var pRepoMock = new Mock<IPlayerRepo>();
            pRepoMock.Setup(p => p.ReassignHoldoutOwner(LeagueId, 900, 2, Utils.CurrentYear))
                .ReturnsAsync(true);

            var controller = BuildController(db, pRepoMock.Object);
            var result = await controller.ReassignHoldoutOwner(LeagueId, new HoldoutReassignOwnerBody
            {
                mflPlayerId = 900,
                newMflFranchiseId = 2
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            pRepoMock.Verify(p => p.ReassignHoldoutOwner(LeagueId, 900, 2, Utils.CurrentYear), Times.Once);
        }

        [Fact]
        public async Task ReassignHoldoutOwner_NoPendingHoldout_StillReturnsOk()
        {
            // Most trades don't involve a holdout player — must not error on the no-op case.
            using var db = NewDb(nameof(ReassignHoldoutOwner_NoPendingHoldout_StillReturnsOk));
            var pRepoMock = new Mock<IPlayerRepo>();
            pRepoMock.Setup(p => p.ReassignHoldoutOwner(LeagueId, 900, 2, Utils.CurrentYear))
                .ReturnsAsync(false);

            var controller = BuildController(db, pRepoMock.Object);
            var result = await controller.ReassignHoldoutOwner(LeagueId, new HoldoutReassignOwnerBody
            {
                mflPlayerId = 900,
                newMflFranchiseId = 2
            });

            Assert.IsType<OkObjectResult>(result);
        }
    }
}
