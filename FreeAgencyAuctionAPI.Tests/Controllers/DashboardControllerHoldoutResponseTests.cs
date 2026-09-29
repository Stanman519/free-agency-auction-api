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
    public class DashboardControllerHoldoutResponseTests
    {
        private const int LeagueId = 13894;

        private static AuctionContext NewDb(string name) =>
            new(new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(name).Options);

        private static DashboardController BuildController(AuctionContext db, IMflService mfl, IPlayerRepo pRepo) => new(
            Mock.Of<ILeagueService>(), mfl, Mock.Of<IOwnerService>(), pRepo,
            Mock.Of<ILogger<DashboardController>>(), db, Mock.Of<IGMBot>(), Mock.Of<IOwnerRepo>());

        [Fact]
        public async Task AcceptingHoldout_RemovesHoldoutTag_PreservesOtherTags()
        {
            // The literal bug this test guards against: contractStatus was being SET to
            // "HOLDOUT" at acceptance time — the opposite of correct, since accepting
            // resolves the holdout. It must be removed here, not applied.
            using var db = NewDb(nameof(AcceptingHoldout_RemovesHoldoutTag_PreservesOtherTags));
            var pRepoMock = new Mock<IPlayerRepo>();
            pRepoMock.Setup(p => p.GetHoldoutById(1)).ReturnsAsync(new Holdout
            {
                Id = 1,
                LeagueId = LeagueId,
                Status = "Pending",
                OriginalSalary = 20,
                HoldoutSalary = 26,
                YearsRemaining = 1,
                LeagueOwner = new LeagueOwnerEntity { Teamname = "Trent" }
            });

            var mflMock = new Mock<IMflService>();
            mflMock.Setup(m => m.GetMflPlayerById(LeagueId, 900))
                .ReturnsAsync(new MflPlayerDetails { first_name = "Test", last_name = "Player" });
            mflMock.Setup(m => m.GetMflRosters(LeagueId)).ReturnsAsync(new System.Collections.Generic.List<FranchiseRoster>
            {
                new()
                {
                    id = "1",
                    player = new System.Collections.Generic.List<Player>
                    {
                        new() { id = "900", contractYear = "2", contractStatus = "R1-2024|HOLDOUT" }
                    }
                }
            });

            string capturedContractStatus = null;
            mflMock.Setup(m => m.GiveNewContractToPlayer(LeagueId, 900, 26, 2, It.IsAny<string>(), It.IsAny<string>(), true))
                .Callback<int, int, int, int, string, string, bool>((_, __, ___, ____, _____, status, ______) => capturedContractStatus = status)
                .Returns(Task.CompletedTask);

            var controller = BuildController(db, mflMock.Object, pRepoMock.Object);
            var result = await controller.RespondToHoldout(new HoldoutResponseBody
            {
                holdoutId = 1,
                status = "Accepted",
                leagueId = LeagueId,
                mflPlayerId = 900,
                mflFranchiseId = 1
            });

            Assert.IsType<NoContentResult>(result);
            Assert.Equal("R1-2024", capturedContractStatus);
        }
    }
}
