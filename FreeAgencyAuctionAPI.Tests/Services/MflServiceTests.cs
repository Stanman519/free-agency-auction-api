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
        public async Task ProposeMflTrade_OnSuccess_NotifiesGroupMe()
        {
            var req = new TradeRequest
            {
                LeagueId = 13894,
                SenderId = 1,
                ReceiverId = 9,
                SenderTeamName = "A",
                ReceiverTeamName = "B",
                SendingAssets = new List<TradeOfferAsset>(),
                ReceivingAssets = new List<TradeOfferAsset>(),
                Expires = 0
            };
            _leagueApiMock.Setup(x => x.SendTradeOffer(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<ok/>") });

            await _service.ProposeMflTrade(req);

            _gmMock.Verify(x => x.NotifyTradeOffer(It.Is<TradeOfferNotification>(n => n.LeagueId == 13894 && n.OfferedToFranchiseId == 9)), Times.Once);
        }

        [Fact]
        public async Task ProposeMflTrade_GroupMeFailure_DoesNotThrow()
        {
            var req = new TradeRequest
            {
                LeagueId = 13894,
                SenderId = 1,
                ReceiverId = 9,
                SenderTeamName = "A",
                ReceiverTeamName = "B",
                SendingAssets = new List<TradeOfferAsset>(),
                ReceivingAssets = new List<TradeOfferAsset>(),
                Expires = 0
            };
            _leagueApiMock.Setup(x => x.SendTradeOffer(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<ok/>") });
            _gmMock.Setup(x => x.NotifyTradeOffer(It.IsAny<TradeOfferNotification>())).ThrowsAsync(new System.Exception("gm down"));

            await _service.ProposeMflTrade(req); // should not throw
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

        [Fact]
        public void ParseRecentMoves_AddOnly_EmitsAdd()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "FREE_AGENT", franchise = "0007", timestamp = "1778243059", transaction = "14840,|" }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Single(result);
            Assert.Equal("ADD", result[0].Action);
            Assert.Equal(14840, result[0].MflPlayerId);
            Assert.Equal(7, result[0].FranchiseId);
        }

        [Fact]
        public void ParseRecentMoves_DropOnly_EmitsDrop()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "FREE_AGENT", franchise = "0001", timestamp = "1778002117", transaction = "|14073," }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Single(result);
            Assert.Equal("DROP", result[0].Action);
            Assert.Equal(14073, result[0].MflPlayerId);
        }

        [Fact]
        public void ParseRecentMoves_MultipleIdsBothSides_EmitsAll()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "FREE_AGENT", franchise = "0002", timestamp = "1778000000", transaction = "100,200,|300," }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Equal(3, result.Count);
            Assert.Contains(result, m => m.Action == "ADD" && m.MflPlayerId == 100);
            Assert.Contains(result, m => m.Action == "ADD" && m.MflPlayerId == 200);
            Assert.Contains(result, m => m.Action == "DROP" && m.MflPlayerId == 300);
        }

        [Fact]
        public void ParseRecentMoves_SkipsTradeProposalsOnly()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "TRADE_PROPOSAL", franchise = "0001", timestamp = "1778090099" },
                new MflTransaction { type = "TRADE_REJECTION", franchise = "0001", timestamp = "1778090099" },
                new MflTransaction { type = "TRADE_REVOKE", franchise = "0001", timestamp = "1778090099" },
                new MflTransaction { type = "TRADE_OFFER_EXPIRED", franchise = "0001", timestamp = "1778090099" },
                new MflTransaction { type = "BBID_WAIVER_REQUEST", franchise = "0001", timestamp = "1778090099", transaction = "|100_1_0000" },
                new MflTransaction { type = "FREE_AGENT", franchise = "0001", timestamp = "1778002000", transaction = "999,|" }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Single(result);
            Assert.Equal(999, result[0].MflPlayerId);
            Assert.Equal("ADD", result[0].Action);
        }

        [Fact]
        public void ParseRecentMoves_HandlesNullAndMalformed()
        {
            var raw = new List<MflTransaction>
            {
                null,
                new MflTransaction { type = "FREE_AGENT", franchise = "0001", timestamp = "abc", transaction = "1,|" },
                new MflTransaction { type = "FREE_AGENT", franchise = "xyz", timestamp = "1778000000", transaction = "1,|" },
                new MflTransaction { type = "FREE_AGENT", franchise = "0001", timestamp = "1778000000", transaction = null },
                new MflTransaction { type = "FREE_AGENT", franchise = "0001", timestamp = "1778000000", transaction = "abc,5,|" }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Single(result);
            Assert.Equal(5, result[0].MflPlayerId);
        }

        [Fact]
        public void ParseRecentMoves_BbidWaiver_EmitsAddAndDrop()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "BBID_WAIVER", franchise = "0002", timestamp = "1778000000", transaction = "100,|5|200," }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, m => m.Action == "ADD" && m.MflPlayerId == 100);
            Assert.Contains(result, m => m.Action == "DROP" && m.MflPlayerId == 200);
        }

        [Fact]
        public void ParseRecentMoves_BbidWaiver_NoDrop()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "BBID_WAIVER", franchise = "0002", timestamp = "1778000000", transaction = "100,|5|" }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Single(result);
            Assert.Equal("ADD", result[0].Action);
            Assert.Equal(100, result[0].MflPlayerId);
        }

        [Fact]
        public void ParseRecentMoves_IR_ActivatedEmitsAdd()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "IR", franchise = "0004", timestamp = "1778000000", activated = "123,", deactivated = "" }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Single(result);
            Assert.Equal("ADD", result[0].Action);
            Assert.Equal(123, result[0].MflPlayerId);
        }

        [Fact]
        public void ParseRecentMoves_IR_DeactivatedEmitsDrop()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "IR", franchise = "0004", timestamp = "1778000000", activated = "", deactivated = "456," }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Single(result);
            Assert.Equal("DROP", result[0].Action);
            Assert.Equal(456, result[0].MflPlayerId);
        }

        [Fact]
        public void ParseRecentMoves_IR_MultipleIds()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "IR", franchise = "0011", timestamp = "1778000000", activated = "14777,12263,", deactivated = "17243,16222," }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Equal(4, result.Count);
            Assert.Contains(result, m => m.Action == "ADD" && m.MflPlayerId == 14777);
            Assert.Contains(result, m => m.Action == "ADD" && m.MflPlayerId == 12263);
            Assert.Contains(result, m => m.Action == "DROP" && m.MflPlayerId == 17243);
            Assert.Contains(result, m => m.Action == "DROP" && m.MflPlayerId == 16222);
        }

        [Fact]
        public void ParseRecentMoves_TAXI_PromotedEmitsAdd()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "TAXI", franchise = "0008", timestamp = "1778000000", promoted = "17105,", demoted = "" }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Single(result);
            Assert.Equal("ADD", result[0].Action);
            Assert.Equal(17105, result[0].MflPlayerId);
        }

        [Fact]
        public void ParseRecentMoves_TAXI_DemotedEmitsDrop()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "TAXI", franchise = "0012", timestamp = "1778000000", promoted = "", demoted = "17086," }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Single(result);
            Assert.Equal("DROP", result[0].Action);
            Assert.Equal(17086, result[0].MflPlayerId);
        }

        [Fact]
        public void ParseRecentMoves_FilterCommishBulkDrop()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "FREE_AGENT", franchise = "0010", timestamp = "1742232341", by_commish = "1",
                    transaction = "|14122,14239,15873,14797,12650,15292,15694," }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Empty(result);
        }

        [Fact]
        public void ParseRecentMoves_KeepsCommishSingleAction()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "FREE_AGENT", franchise = "0005", timestamp = "1778410477", by_commish = "1", transaction = "13593,|" }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Single(result);
            Assert.Equal("ADD", result[0].Action);
            Assert.Equal(13593, result[0].MflPlayerId);
        }

        [Fact]
        public void ParseRecentMoves_Trade_EmitsAddAndDropForBothSides()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "TRADE", franchise = "0004", franchise2 = "0009", timestamp = "1764692716",
                    franchise1_gave_up = "100,", franchise2_gave_up = "200," }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Equal(4, result.Count);
            Assert.Contains(result, m => m.FranchiseId == 4 && m.Action == "DROP" && m.MflPlayerId == 100);
            Assert.Contains(result, m => m.FranchiseId == 9 && m.Action == "ADD" && m.MflPlayerId == 100);
            Assert.Contains(result, m => m.FranchiseId == 9 && m.Action == "DROP" && m.MflPlayerId == 200);
            Assert.Contains(result, m => m.FranchiseId == 4 && m.Action == "ADD" && m.MflPlayerId == 200);
        }

        [Fact]
        public void ParseRecentMoves_Trade_SkipsPickOnlyTrade()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "TRADE", franchise = "0001", franchise2 = "0003", timestamp = "1748371531",
                    franchise1_gave_up = "DP_1_8,FP_0004_2026_2,", franchise2_gave_up = "DP_1_5," }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Empty(result);
        }

        [Fact]
        public void ParseRecentMoves_Trade_MixedPlayerAndPicks()
        {
            var raw = new List<MflTransaction>
            {
                new MflTransaction { type = "TRADE", franchise = "0010", franchise2 = "0012", timestamp = "1762103124",
                    franchise1_gave_up = "FP_0010_2026_3,", franchise2_gave_up = "16757," }
            };
            var result = MflService.ParseRecentMoves(raw);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, m => m.FranchiseId == 12 && m.Action == "DROP" && m.MflPlayerId == 16757);
            Assert.Contains(result, m => m.FranchiseId == 10 && m.Action == "ADD" && m.MflPlayerId == 16757);
        }
    }
}
