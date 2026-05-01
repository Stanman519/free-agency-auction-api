using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using FreeAgencyAuctionAPI;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Services
{
    public class PlayerServiceTests
    {
        private readonly Mock<IPlayerRepo> _repoMock = new();
        private readonly Mock<IMapper> _mapperMock = new();
        private readonly Mock<ISharkApi> _sharkMock = new();
        private readonly Mock<IMflApi> _mflApiMock = new();
        private readonly Mock<IGlobalMflApi> _globalMock = new();
        private readonly Mock<IOptionsSnapshot<AppConfig>> _optionsMock = new();
        private readonly PlayerService _service;

        public PlayerServiceTests()
        {
            _optionsMock.Setup(o => o.Value).Returns(new AppConfig { Mfl = new MflKeys { MflApiKey = new List<MflApiKey>() } });
            _service = new PlayerService(_repoMock.Object, _mapperMock.Object, _sharkMock.Object, _mflApiMock.Object, _globalMock.Object, _optionsMock.Object);
        }

        [Fact]
        public async Task GetAllFreeAgents_DemoLeague_SkipsMflAndReturnsDbPlayers()
        {
            var dbPlayers = new List<PlayerEntity>
            {
                new PlayerEntity { Mflid = 1, Firstname = "Josh", Lastname = "Allen", Position = "QB", Team = "BUF" },
                new PlayerEntity { Mflid = 2, Firstname = "CeeDee", Lastname = "Lamb", Position = "WR", Team = "DAL" }
            };
            _repoMock.Setup(r => r.GetAllFreeAgents(-32)).ReturnsAsync(dbPlayers);

            var result = await _service.GetAllFreeAgents(-32);

            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].MflId);
            Assert.Equal("Josh", result[0].FirstName);
            _mflApiMock.Verify(m => m.GetMflFreeAgents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            _globalMock.Verify(g => g.GetMflAdp(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetAllFreeAgents_RealLeague_CallsMflApi()
        {
            _mflApiMock.Setup(m => m.GetMflFreeAgents(13894, It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync(new FreeAgentsRoot
            {
                error = "some error"
            });

            var result = await _service.GetAllFreeAgents(13894);

            Assert.Empty(result);
            _mflApiMock.Verify(m => m.GetMflFreeAgents(13894, It.IsAny<int>(), It.IsAny<string>()), Times.Once);
        }
    }
}
