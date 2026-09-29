using System;
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
    public class DashboardControllerFifthYearOptionTests
    {
        private const int LeagueId = 13894;

        private static AuctionContext NewDb(string name) =>
            new(new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(name).Options);

        private static DashboardController BuildController(AuctionContext db, IMflService mfl) => new(
            Mock.Of<ILeagueService>(), mfl, Mock.Of<IOwnerService>(), Mock.Of<IPlayerRepo>(),
            Mock.Of<ILogger<DashboardController>>(), db, Mock.Of<IGMBot>(), Mock.Of<IOwnerRepo>());

        private static FifthYearOptionCandidate Candidate(int mflId) => new()
        {
            Player = new PlayerDTO { MflId = mflId, FirstName = "Test", LastName = "Player" },
            OriginalRookieSalary = 30,
            OptionSalary = 39,
            DraftYear = DateTime.UtcNow.Year - 4,
            DraftPick = 1
        };

        [Fact]
        public async Task SignFifthYearOption_MflWriteThrows_ReturnsBadRequestInsteadOfUnhandled500()
        {
            // Regression: GiveNewContractToPlayer's 5-arg overload used to be uncaught here —
            // any MFL write failure (network, MFL error response) surfaced as a raw unhandled
            // 500 with no useful response body and no chance for a controller-level log.
            using var db = NewDb(nameof(SignFifthYearOption_MflWriteThrows_ReturnsBadRequestInsteadOfUnhandled500));
            var mflMock = new Mock<IMflService>();
            mflMock.Setup(m => m.GetFifthYearOptionCandidates(LeagueId, It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<FifthYearOptionCandidate> { Candidate(900) });
            mflMock.Setup(m => m.GetMflPlayerById(LeagueId, 900))
                .ReturnsAsync(new MflPlayerDetails { first_name = "Test", last_name = "Player" });
            mflMock.Setup(m => m.AddPlayerToTeam(LeagueId, 900, It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            mflMock.Setup(m => m.GiveNewContractToPlayer(LeagueId, 900, 39, 1, It.IsAny<string>(), "5YO", It.IsAny<bool>()))
                .ThrowsAsync(new Exception("MFL contract update failed for player 900: some error"));

            var controller = BuildController(db, mflMock.Object);
            var result = await controller.SignFifthYearOption(new FifthYearOptionRequestBody
            {
                leagueId = LeagueId,
                mflPlayerId = 900,
                mflFranchiseId = 1,
                leagueOwnerId = 10
            });

            Assert.IsType<BadRequestObjectResult>(result);
            mflMock.Verify(m => m.GetSalaryCapRoom(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task SignFifthYearOption_Success_PassesContractStatus5YO()
        {
            using var db = NewDb(nameof(SignFifthYearOption_Success_PassesContractStatus5YO));
            var mflMock = new Mock<IMflService>();
            mflMock.Setup(m => m.GetFifthYearOptionCandidates(LeagueId, It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<FifthYearOptionCandidate> { Candidate(900) });
            mflMock.Setup(m => m.GetMflPlayerById(LeagueId, 900))
                .ReturnsAsync(new MflPlayerDetails { first_name = "Test", last_name = "Player" });
            mflMock.Setup(m => m.AddPlayerToTeam(LeagueId, 900, It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            mflMock.Setup(m => m.GiveNewContractToPlayer(LeagueId, 900, 39, 1, It.IsAny<string>(), "5YO", It.IsAny<bool>())).Returns(Task.CompletedTask);
            mflMock.Setup(m => m.GetSalaryCapRoom(LeagueId)).ReturnsAsync(new List<LeagueOwnerEntity>());

            var controller = BuildController(db, mflMock.Object);
            var result = await controller.SignFifthYearOption(new FifthYearOptionRequestBody
            {
                leagueId = LeagueId,
                mflPlayerId = 900,
                mflFranchiseId = 1,
                leagueOwnerId = 10
            });

            Assert.IsType<NoContentResult>(result);
            mflMock.Verify(m => m.GiveNewContractToPlayer(LeagueId, 900, 39, 1, It.IsAny<string>(), "5YO", It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public async Task ApplyContractStatus_Success_ConfirmsAgainstMfl()
        {
            using var db = NewDb(nameof(ApplyContractStatus_Success_ConfirmsAgainstMfl));
            var mflMock = new Mock<IMflService>();
            mflMock.Setup(m => m.GiveNewContractToPlayer(LeagueId, 900, 10, 4, It.IsAny<string>(), "R1-2024", false))
                .Returns(Task.CompletedTask);
            mflMock.Setup(m => m.GetPlayerContractStatusFromMfl(LeagueId, 900)).ReturnsAsync("R1-2024");

            var controller = BuildController(db, mflMock.Object);
            var result = await controller.ApplyContractStatus(LeagueId, 900,
                new ApplyContractStatusBody { ContractStatus = "R1-2024", Salary = 10, ContractYear = 4 });

            var ok = Assert.IsType<OkObjectResult>(result);
            mflMock.Verify(m => m.GiveNewContractToPlayer(LeagueId, 900, 10, 4, It.IsAny<string>(), "R1-2024", false), Times.Once);
        }

        [Fact]
        public async Task ApplyContractStatus_MflSettingDisabled_ReturnsUnmatchedConfirmation()
        {
            // The write itself succeeds (no exception), but MFL never actually stored the
            // attribute because the league's Contract Status setting is off — this must be
            // visible in the response, not silently reported as a clean success.
            using var db = NewDb(nameof(ApplyContractStatus_MflSettingDisabled_ReturnsUnmatchedConfirmation));
            var mflMock = new Mock<IMflService>();
            mflMock.Setup(m => m.GiveNewContractToPlayer(LeagueId, 900, 10, 4, It.IsAny<string>(), "R1-2024", false))
                .Returns(Task.CompletedTask);
            mflMock.Setup(m => m.GetPlayerContractStatusFromMfl(LeagueId, 900)).ReturnsAsync((string)null);

            var controller = BuildController(db, mflMock.Object);
            var result = await controller.ApplyContractStatus(LeagueId, 900,
                new ApplyContractStatusBody { ContractStatus = "R1-2024", Salary = 10, ContractYear = 4 });

            var ok = Assert.IsType<OkObjectResult>(result);
            var matchedProp = ok.Value.GetType().GetProperty("matched");
            Assert.False((bool)matchedProp.GetValue(ok.Value));
        }

        [Fact]
        public async Task ApplyContractStatus_MflWriteThrows_ReturnsBadRequest()
        {
            using var db = NewDb(nameof(ApplyContractStatus_MflWriteThrows_ReturnsBadRequest));
            var mflMock = new Mock<IMflService>();
            mflMock.Setup(m => m.GiveNewContractToPlayer(LeagueId, 900, 10, 4, It.IsAny<string>(), "R1-2024", false))
                .ThrowsAsync(new Exception("MFL down"));

            var controller = BuildController(db, mflMock.Object);
            var result = await controller.ApplyContractStatus(LeagueId, 900,
                new ApplyContractStatusBody { ContractStatus = "R1-2024", Salary = 10, ContractYear = 4 });

            Assert.IsType<BadRequestObjectResult>(result);
            mflMock.Verify(m => m.GetPlayerContractStatusFromMfl(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }
    }
}
