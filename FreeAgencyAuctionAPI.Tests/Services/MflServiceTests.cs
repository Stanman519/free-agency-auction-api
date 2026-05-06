using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Services
{
    public class MflServiceTests
    {
        private readonly Mock<IGlobalMflApi> _globalApiMock;
        private readonly Mock<IMflApi> _leagueApiMock;
        private readonly Mock<IBingImageApi> _bingApiMock;
        private readonly Mock<ILogger<MflService>> _loggerMock;
        private readonly Mock<IGMBot> _gmMock;
        private readonly Mock<IPlayerRepo> _pRepoMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IOptionsSnapshot<AppConfig>> _optionsMock;
        private readonly AuctionContext _db;
        private readonly MflService _service;

        public MflServiceTests()
        {
            _globalApiMock = new Mock<IGlobalMflApi>();
            _leagueApiMock = new Mock<IMflApi>();
            _bingApiMock = new Mock<IBingImageApi>();
            _loggerMock = new Mock<ILogger<MflService>>();
            _gmMock = new Mock<IGMBot>();
            _pRepoMock = new Mock<IPlayerRepo>();
            _mapperMock = new Mock<IMapper>();
            _optionsMock = new Mock<IOptionsSnapshot<AppConfig>>();
            var dbOptions = new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase("MflServiceTests").Options;
            _db = new AuctionContext(dbOptions);

            _service = new MflService(
                _globalApiMock.Object,
                _leagueApiMock.Object,
                _bingApiMock.Object,
                _loggerMock.Object,
                _gmMock.Object,
                _pRepoMock.Object,
                _mapperMock.Object,
                _db,
                _optionsMock.Object
            );
        }

        [Fact]
        public async Task GetSalaryCapRoom_AppliesCorrectWeightsByStatus()
        {
            var leagueId = 13894;
            _leagueApiMock.Setup(x => x.GetBigLeagueObject(leagueId, It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync(new LeagueRoot
            {
                league = new League2
                {
                    franchises = new Franchises
                    {
                        franchise = new List<FranchisePlusAssets>
                        {
                            new FranchisePlusAssets { id = "0001", salaryCapAmount = "500" },
                        }
                    }
                }
            });
            _leagueApiMock.Setup(x => x.GetMflRostersForPlayerSalaries(leagueId, It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync(new RostersRoot
            {
                rosters = new Rosters
                {
                    franchise = new List<FranchiseRoster>
                    {
                        new FranchiseRoster
                        {
                            id = "0001",
                            player = new List<Player>
                            {
                                new Player { status = "ROSTER", salary = "100" },          // 100
                                new Player { status = "TAXI_SQUAD", salary = "50" },       // 10
                                new Player { status = "INJURED_RESERVE", salary = "40" },  // 20
                            }
                        }
                    }
                }
            });
            _leagueApiMock.Setup(x => x.GetMflSalaryAdjustments(leagueId, It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync(new SalaryAdjustmentsRoot
            {
                salaryAdjustments = new SalaryAdjustments
                {
                    salaryAdjustment = new List<SalaryAdjustment>
                    {
                        new SalaryAdjustment { franchise_id = "0001", amount = "5" },
                    }
                }
            });

            var result = await _service.GetSalaryCapRoom(leagueId);

            // 500 - 100 - 10 - 20 - 5 = 365
            Assert.Single(result);
            Assert.Equal(1, result[0].Mflfranchiseid);
            Assert.Equal(365, result[0].Caproom);
        }

        [Fact]
        public async Task GiveNewContractToPlayer_FranchiseTag_SendsCorrectBotMessage()
        {
            // Arrange
            var leagueId = 13894;
            var mflPlayerId = 12345;
            var salary = 30;
            var playerName = "Patrick Mahomes";
            var expectedMsg = $"{playerName} got franchise tagged for ${salary}.";
            var expectedBotId = Utils.leagueBotDict[leagueId];

            var okResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<salaries></salaries>") };
            _leagueApiMock.Setup(x => x.EditPlayerSalary(leagueId, It.IsAny<Dictionary<string, string>>(), It.IsAny<int>()))
                .ReturnsAsync(okResponse);
            _gmMock.Setup(x => x.SendBotNotification(It.IsAny<BotMessage>())).Returns(Task.CompletedTask);

            // Act
            await _service.GiveNewContractToPlayer(leagueId, mflPlayerId, salary, isFranchiseTag: true, playerName);

            // Assert
            _leagueApiMock.Verify(x => x.EditPlayerSalary(leagueId, It.IsAny<Dictionary<string, string>>(), It.IsAny<int>()), Times.Once);
            _gmMock.Verify(x => x.SendBotNotification(
                It.Is<BotMessage>(m => m.Message == expectedMsg && m.BotId == expectedBotId)
            ), Times.Once);
        }

        [Fact]
        public async Task GiveNewContractToPlayer_FranchiseTag_MessageFormat_ContainsPlayerNameAndSalary()
        {
            // Arrange
            var leagueId = 13894;
            var playerName = "Justin Jefferson";
            var salary = 45;

            var okResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<salaries></salaries>") };
            _leagueApiMock.Setup(x => x.EditPlayerSalary(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>()))
                .ReturnsAsync(okResponse);

            string capturedMessage = null;
            _gmMock.Setup(x => x.SendBotNotification(It.IsAny<BotMessage>()))
                .Callback<BotMessage>(m => capturedMessage = m.Message)
                .Returns(Task.CompletedTask);

            // Act
            await _service.GiveNewContractToPlayer(leagueId, 99999, salary, isFranchiseTag: true, playerName);

            // Assert
            Assert.Contains(playerName, capturedMessage);
            Assert.Contains($"${salary}", capturedMessage);
            Assert.Contains("franchise tagged", capturedMessage);
        }

        [Fact]
        public async Task GiveNewContractToPlayer_FranchiseTag_MflError_DoesNotSendBotNotification()
        {
            // Arrange
            var leagueId = 13894;
            var errorResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<error>some error</error>") };
            _leagueApiMock.Setup(x => x.EditPlayerSalary(leagueId, It.IsAny<Dictionary<string, string>>(), It.IsAny<int>()))
                .ReturnsAsync(errorResponse);
            _gmMock.Setup(x => x.NotifyMflError(It.IsAny<BotMessage>())).Returns(Task.CompletedTask);

            // Act
            await _service.GiveNewContractToPlayer(leagueId, 12345, 30, isFranchiseTag: true, "Test Player");

            // Assert
            _gmMock.Verify(x => x.SendBotNotification(It.IsAny<BotMessage>()), Times.Never);
            _gmMock.Verify(x => x.NotifyMflError(It.IsAny<BotMessage>()), Times.Once);
        }

        [Fact]
        public async Task GiveNewContractToPlayer_SendBotNotificationThrows_FallsBackToNotifyMflError()
        {
            var leagueId = 13894;
            var okResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<salaries></salaries>") };
            _leagueApiMock.Setup(x => x.EditPlayerSalary(leagueId, It.IsAny<Dictionary<string, string>>(), It.IsAny<int>()))
                .ReturnsAsync(okResponse);
            _gmMock.Setup(x => x.SendBotNotification(It.IsAny<BotMessage>())).ThrowsAsync(new System.Net.Http.HttpRequestException("bot-api unreachable"));
            _gmMock.Setup(x => x.NotifyMflError(It.IsAny<BotMessage>())).Returns(Task.CompletedTask);

            await _service.GiveNewContractToPlayer(leagueId, 12345, 30, isFranchiseTag: true, "Test Player");

            _gmMock.Verify(x => x.SendBotNotification(It.IsAny<BotMessage>()), Times.Once);
            _gmMock.Verify(x => x.NotifyMflError(It.Is<BotMessage>(m => m.Message.Contains("announcement failed"))), Times.Once);
        }

        [Fact]
        public async Task GiveNewContractToPlayer_EditPlayerSalaryThrows_NotifiesMflError()
        {
            var leagueId = 13894;
            _leagueApiMock.Setup(x => x.EditPlayerSalary(leagueId, It.IsAny<Dictionary<string, string>>(), It.IsAny<int>()))
                .ThrowsAsync(new System.Net.Http.HttpRequestException("429 Too Many Requests"));
            _gmMock.Setup(x => x.NotifyMflError(It.IsAny<BotMessage>())).Returns(Task.CompletedTask);

            await _service.GiveNewContractToPlayer(leagueId, 12345, 30, isFranchiseTag: true, "Test Player");

            _gmMock.Verify(x => x.NotifyMflError(It.Is<BotMessage>(m => m.Message.Contains("threw"))), Times.Once);
            _gmMock.Verify(x => x.SendBotNotification(It.IsAny<BotMessage>()), Times.Never);
        }

        // -------- 5th year option candidate filter tests --------

        private const int FifthYrLeagueId = 13894;
        private const int FifthYrFranchiseId = 1;
        private const int FifthYrLeagueOwnerId = 99;

        private void SetupFifthYearMocks(
            string playerId,
            string position,
            string lastYearSalary,
            string round = "01",
            string pick = "1",
            int? franchiseIdInRoster = null,
            List<Holdout> holdouts = null)
        {
            var franchiseId = (franchiseIdInRoster ?? FifthYrFranchiseId).ToString("D4");
            _leagueApiMock.Setup(x => x.GetMflRostersForPlayerSalaries(FifthYrLeagueId, Utils.CurrentYear - 1, It.IsAny<string>()))
                .ReturnsAsync(new RostersRoot
                {
                    rosters = new Rosters
                    {
                        franchise = new List<FranchiseRoster>
                        {
                            new FranchiseRoster
                            {
                                id = franchiseId,
                                player = new List<Player>
                                {
                                    new Player { id = playerId, salary = lastYearSalary, contractYear = "4", status = "ROSTER" }
                                }
                            }
                        }
                    }
                });

            _leagueApiMock.Setup(x => x.GetDraftResults(FifthYrLeagueId, Utils.CurrentYear - 4, It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot
                {
                    draftResults = new DraftResults
                    {
                        draftUnit = new DraftUnit
                        {
                            draftPick = new List<MflDraftPick>
                            {
                                new MflDraftPick { player = playerId, pick = pick, round = round, franchise = franchiseId }
                            }
                        }
                    }
                });

            _leagueApiMock.Setup(x => x.GetMflPlayerDetails(FifthYrLeagueId, It.IsAny<string>(), Utils.CurrentYear, It.IsAny<string>()))
                .ReturnsAsync(new MflPlayerDetailsRoot
                {
                    players = new MflPlayerDetailsParent
                    {
                        player = new List<MflPlayerDetails>
                        {
                            new MflPlayerDetails { id = playerId, position = position, name = "Test Player", first_name = "Test", last_name = "Player", team = "KC" }
                        }
                    }
                });

            _pRepoMock.Setup(x => x.GetHoldoutsForLeague(FifthYrLeagueId, It.IsAny<int>()))
                .ReturnsAsync(holdouts ?? new List<Holdout>());
        }

        [Fact]
        public async Task GetFifthYearOptionCandidates_FirstRoundPickAtRookieSalary_Included()
        {
            // Pick 1 → rookie salary 30. 30% increase rounded = 39.
            SetupFifthYearMocks(playerId: "1001", position: "WR", lastYearSalary: "30", pick: "1");

            var result = await _service.GetFifthYearOptionCandidates(FifthYrLeagueId, FifthYrLeagueOwnerId, FifthYrFranchiseId);

            Assert.Single(result);
            Assert.Equal(1001, result[0].Player.MflId);
            Assert.Equal(30, result[0].OriginalRookieSalary);
            Assert.Equal(39, result[0].OptionSalary);
            Assert.Equal(Utils.CurrentYear - 4, result[0].DraftYear);
            Assert.Equal(1, result[0].DraftPick);
        }

        [Fact]
        public async Task GetFifthYearOptionCandidates_SecondRoundPick_Excluded()
        {
            SetupFifthYearMocks(playerId: "1002", position: "WR", lastYearSalary: "30", round: "02");

            var result = await _service.GetFifthYearOptionCandidates(FifthYrLeagueId, FifthYrLeagueOwnerId, FifthYrFranchiseId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFifthYearOptionCandidates_PlayerOnDifferentFranchiseLastYear_Excluded()
        {
            SetupFifthYearMocks(playerId: "1003", position: "WR", lastYearSalary: "30", franchiseIdInRoster: 2);

            var result = await _service.GetFifthYearOptionCandidates(FifthYrLeagueId, FifthYrLeagueOwnerId, FifthYrFranchiseId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFifthYearOptionCandidates_SalaryAboveRookie_NoHoldout_Excluded()
        {
            // Pick 1 rookie scale = 30. Last year salary 50 means re-signed mid-rookie deal.
            SetupFifthYearMocks(playerId: "1004", position: "WR", lastYearSalary: "50", pick: "1");

            var result = await _service.GetFifthYearOptionCandidates(FifthYrLeagueId, FifthYrLeagueOwnerId, FifthYrFranchiseId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFifthYearOptionCandidates_HoldoutPrecedence_LastYearSalaryAboveRookieButHoldoutOriginalMatches_Included()
        {
            // Pick 1 rookie = 30. Last-year roster shows 36 (post-holdout raise). Holdout's OriginalSalary = 30.
            // Holdout precedence: comparison runs against pre-holdout original → included.
            var holdouts = new List<Holdout>
            {
                new Holdout { LeagueId = FifthYrLeagueId, Year = Utils.CurrentYear, PlayerId = 1005, OriginalSalary = 30, HoldoutSalary = 36, Status = "Accepted" }
            };
            SetupFifthYearMocks(playerId: "1005", position: "WR", lastYearSalary: "36", pick: "1", holdouts: holdouts);

            var result = await _service.GetFifthYearOptionCandidates(FifthYrLeagueId, FifthYrLeagueOwnerId, FifthYrFranchiseId);

            Assert.Single(result);
            Assert.Equal(30, result[0].OriginalRookieSalary);
            Assert.Equal(39, result[0].OptionSalary);
        }

        [Theory]
        [InlineData(1, 30, 39)]   // 30 * 1.3 = 39
        [InlineData(5, 22, 29)]   // 22 * 1.3 = 28.6 → 29
        [InlineData(13, 18, 23)]  // 18 * 1.3 = 23.4 → 23
        public async Task GetFifthYearOptionCandidates_OptionSalaryRoundsToNearestWhole(int pick, int rookieScale, int expectedOption)
        {
            SetupFifthYearMocks(playerId: "2000", position: "WR", lastYearSalary: rookieScale.ToString(), pick: pick.ToString());

            var result = await _service.GetFifthYearOptionCandidates(FifthYrLeagueId, FifthYrLeagueOwnerId, FifthYrFranchiseId);

            Assert.Single(result);
            Assert.Equal(rookieScale, result[0].OriginalRookieSalary);
            Assert.Equal(expectedOption, result[0].OptionSalary);
        }
    }
}
