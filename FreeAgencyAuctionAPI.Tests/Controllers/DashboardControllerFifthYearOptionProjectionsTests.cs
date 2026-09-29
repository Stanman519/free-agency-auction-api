using System.Collections.Generic;
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
    public class DashboardControllerFifthYearOptionProjectionsTests
    {
        private const int LeagueId = 13894;

        private static AuctionContext NewDb(string name) =>
            new(new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(name).Options);

        private static DashboardController BuildController(AuctionContext db, IMflService mfl) => new(
            Mock.Of<ILeagueService>(), mfl, Mock.Of<IOwnerService>(), Mock.Of<IPlayerRepo>(),
            Mock.Of<ILogger<DashboardController>>(), db, Mock.Of<IGMBot>(), Mock.Of<IOwnerRepo>());

        [Fact]
        public async Task GetFifthYearOptionProjections_ReturnsLeagueWideProjections()
        {
            using var db = NewDb(nameof(GetFifthYearOptionProjections_ReturnsLeagueWideProjections));
            var rosters = new List<FranchiseRoster> { new() { id = "1", player = new List<Player>() } };
            var mflMock = new Mock<IMflService>();
            mflMock.Setup(m => m.GetMflRosters(LeagueId)).ReturnsAsync(rosters);
            mflMock.Setup(m => m.GetProjectedFifthYearOptionSalaries(LeagueId, rosters))
                .ReturnsAsync(new Dictionary<string, int> { ["900"] = 31, ["901"] = 44 });

            var controller = BuildController(db, mflMock.Object);

            var result = await controller.GetFifthYearOptionProjections(LeagueId);

            var ok = Assert.IsType<OkObjectResult>(result);
            var items = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value);
            var count = 0;
            foreach (var _ in items) count++;
            Assert.Equal(2, count);
        }
    }
}
