using System.Collections.Generic;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Hub;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Controllers
{
    public class FreeAgencyControllerRosterTests
    {
        private const int LeagueId = 13894;

        private static FreeAgencyController BuildController(
            IMflService mfl, IPlayerService pService, IOwnerService oService) => new(
                pService, oService, Mock.Of<IBidLotService>(), mfl,
                Mock.Of<IHubContext<AuctionHub>>(), Mock.Of<IGMBot>(), Mock.Of<IHeadshotLoadingService>(),
                Mock.Of<IHeadlineService>(), Mock.Of<IOwnerQuoteRepo>(), Mock.Of<ILogger<FreeAgencyController>>());

        [Fact]
        public async Task GetRosters_PassesThroughContractStatusAndProjectedFifthYearOption()
        {
            var mflRosters = new List<FranchiseRoster>
            {
                new()
                {
                    id = "1",
                    player = new List<Player>
                    {
                        new() { id = "900", salary = "24", contractYear = "1", status = "ROSTER", contractStatus = "R1-2023|HOLDOUT" }
                    }
                }
            };

            var mflMock = new Mock<IMflService>();
            mflMock.Setup(m => m.GetMflRosters(LeagueId)).ReturnsAsync(mflRosters);
            mflMock.Setup(m => m.GetFutureDraftPicksForLeague(LeagueId))
                .ReturnsAsync(new Dictionary<string, List<FutureDraftPickDTO>>());
            mflMock.Setup(m => m.GetProjectedFifthYearOptionSalaries(LeagueId, mflRosters))
                .ReturnsAsync(new Dictionary<string, int> { ["900"] = 31 });

            var pServiceMock = new Mock<IPlayerService>();
            pServiceMock.Setup(p => p.GetAllPlayers()).ReturnsAsync(new List<PlayerDTO>
            {
                new() { MflId = 900, FirstName = "De'Von", LastName = "Achane" }
            });

            var oServiceMock = new Mock<IOwnerService>();
            oServiceMock.Setup(o => o.GetAllOwners(LeagueId)).ReturnsAsync(new List<OpposingFranchiseDTO>
            {
                new() { Mflfranchiseid = 1, TeamName = "Trent" }
            });

            var controller = BuildController(mflMock.Object, pServiceMock.Object, oServiceMock.Object);

            var result = await controller.GetRosters(LeagueId);

            var ok = Assert.IsType<OkObjectResult>(result);
            var franchises = Assert.IsType<List<OpposingFranchiseWithRoster>>(ok.Value);
            var player = Assert.Single(Assert.Single(franchises).Players);
            Assert.Equal("R1-2023|HOLDOUT", player.ContractStatus);
            Assert.Equal(31, player.ProjectedFifthYearOptionSalary);
        }

        [Fact]
        public async Task GetRosters_NoProjection_LeavesProjectedFifthYearOptionSalaryNull()
        {
            var mflRosters = new List<FranchiseRoster>
            {
                new()
                {
                    id = "1",
                    player = new List<Player> { new() { id = "901", salary = "50", contractYear = "3", status = "ROSTER", contractStatus = "TAG-1" } }
                }
            };

            var mflMock = new Mock<IMflService>();
            mflMock.Setup(m => m.GetMflRosters(LeagueId)).ReturnsAsync(mflRosters);
            mflMock.Setup(m => m.GetFutureDraftPicksForLeague(LeagueId))
                .ReturnsAsync(new Dictionary<string, List<FutureDraftPickDTO>>());
            mflMock.Setup(m => m.GetProjectedFifthYearOptionSalaries(LeagueId, mflRosters))
                .ReturnsAsync(new Dictionary<string, int>());

            var pServiceMock = new Mock<IPlayerService>();
            pServiceMock.Setup(p => p.GetAllPlayers()).ReturnsAsync(new List<PlayerDTO>
            {
                new() { MflId = 901, FirstName = "Some", LastName = "Veteran" }
            });

            var oServiceMock = new Mock<IOwnerService>();
            oServiceMock.Setup(o => o.GetAllOwners(LeagueId)).ReturnsAsync(new List<OpposingFranchiseDTO>
            {
                new() { Mflfranchiseid = 1, TeamName = "Trent" }
            });

            var controller = BuildController(mflMock.Object, pServiceMock.Object, oServiceMock.Object);

            var result = await controller.GetRosters(LeagueId);

            var ok = Assert.IsType<OkObjectResult>(result);
            var franchises = Assert.IsType<List<OpposingFranchiseWithRoster>>(ok.Value);
            var player = Assert.Single(Assert.Single(franchises).Players);
            Assert.Equal("TAG-1", player.ContractStatus);
            Assert.Null(player.ProjectedFifthYearOptionSalary);
        }
    }
}
