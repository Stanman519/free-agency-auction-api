using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AutoMapper;
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
    }
}
