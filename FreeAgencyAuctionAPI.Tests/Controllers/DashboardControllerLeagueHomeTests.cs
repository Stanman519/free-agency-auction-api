using System.Collections.Generic;
using System.Linq;
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
    public class DashboardControllerLeagueHomeTests
    {
        private static AuctionContext NewDb(string name)
        {
            var opts = new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(name).Options;
            return new AuctionContext(opts);
        }

        private static OwnerDTO BuildProfile(int leagueId, params (int franchiseId, int staleCap)[] owners)
        {
            return new OwnerDTO
            {
                OwnerId = 1,
                Leagues = owners.Select(o => new LeagueOwnerDTO
                {
                    Mflfranchiseid = o.franchiseId,
                    CapRoom = o.staleCap,
                    League = new LeagueDTO { LeagueId = leagueId }
                }).ToList()
            };
        }

        [Fact]
        public async Task GetOnLoadInfo_RefreshesCaproomFromMfl_AndPatchesProfileInMemory()
        {
            var leagueId = 13894;
            var db = NewDb(nameof(GetOnLoadInfo_RefreshesCaproomFromMfl_AndPatchesProfileInMemory));
            db.LeagueOwners.AddRange(
                new LeagueOwnerEntity { Leagueownerid = 1, Leagueid = leagueId, Mflfranchiseid = 1, Caproom = 211 },
                new LeagueOwnerEntity { Leagueownerid = 2, Leagueid = leagueId, Mflfranchiseid = 2, Caproom = 250 });
            await db.SaveChangesAsync();

            var profile = BuildProfile(leagueId, (1, 211), (2, 250));

            var ownerSvcMock = new Mock<IOwnerService>();
            ownerSvcMock.Setup(s => s.SynchronizeAuthorizedUser(It.IsAny<AuthUser>())).ReturnsAsync(profile);

            var mflMock = new Mock<IMflService>();
            mflMock.Setup(m => m.GetTagAndTaxiInfos(leagueId, It.IsAny<LeagueOwnerDTO>()))
                   .ReturnsAsync(new LeagueOwnerDTO
                   {
                       TagCandidates = new List<TagCandidate>(),
                       TaxiPlayers = new List<PlayerDTO>(),
                       CutCandidates = new List<PlayerDTO>()
                   });
            mflMock.Setup(m => m.GetSalaryCapRoom(leagueId)).ReturnsAsync(new List<LeagueOwnerEntity>
            {
                new LeagueOwnerEntity { Mflfranchiseid = 1, Caproom = 132 },
                new LeagueOwnerEntity { Mflfranchiseid = 2, Caproom = 175 },
            });

            var ownerRepoMock = new Mock<IOwnerRepo>();
            List<int> capturedCapList = null;
            int capturedLeagueId = 0;
            ownerRepoMock.Setup(r => r.UpdateCapRoomForAllOwners(It.IsAny<List<int>>(), It.IsAny<int>()))
                .Callback<List<int>, int>((list, lid) => { capturedCapList = list; capturedLeagueId = lid; })
                .Returns(Task.CompletedTask);

            var controller = new DashboardController(
                Mock.Of<ILeagueService>(),
                mflMock.Object,
                ownerSvcMock.Object,
                Mock.Of<IPlayerRepo>(),
                Mock.Of<ILogger<DashboardController>>(),
                db,
                Mock.Of<IGMBot>(),
                ownerRepoMock.Object);

            var result = await controller.GetOnLoadInfo(new AuthUser { Sub = "auth0|x" }, leagueId.ToString());

            Assert.IsType<OkObjectResult>(result);
            mflMock.Verify(m => m.GetSalaryCapRoom(leagueId), Times.Once);
            ownerRepoMock.Verify(r => r.UpdateCapRoomForAllOwners(It.IsAny<List<int>>(), leagueId), Times.Once);

            Assert.Equal(leagueId, capturedLeagueId);
            Assert.Equal(new[] { 132, 175 }, capturedCapList.ToArray());

            var franchise1 = profile.Leagues.First(l => l.Mflfranchiseid == 1);
            var franchise2 = profile.Leagues.First(l => l.Mflfranchiseid == 2);
            Assert.Equal(132, franchise1.CapRoom);
            Assert.Equal(175, franchise2.CapRoom);
        }

        [Fact]
        public async Task GetOnLoadInfo_NoChosenLeague_SkipsCapRefresh()
        {
            var db = NewDb(nameof(GetOnLoadInfo_NoChosenLeague_SkipsCapRefresh));
            var profile = new OwnerDTO { OwnerId = 1, Leagues = new List<LeagueOwnerDTO>() };

            var ownerSvcMock = new Mock<IOwnerService>();
            ownerSvcMock.Setup(s => s.SynchronizeAuthorizedUser(It.IsAny<AuthUser>())).ReturnsAsync(profile);

            var mflMock = new Mock<IMflService>();
            var ownerRepoMock = new Mock<IOwnerRepo>();

            var controller = new DashboardController(
                Mock.Of<ILeagueService>(),
                mflMock.Object,
                ownerSvcMock.Object,
                Mock.Of<IPlayerRepo>(),
                Mock.Of<ILogger<DashboardController>>(),
                db,
                Mock.Of<IGMBot>(),
                ownerRepoMock.Object);

            await controller.GetOnLoadInfo(new AuthUser { Sub = "auth0|x" }, null);

            mflMock.Verify(m => m.GetSalaryCapRoom(It.IsAny<int>()), Times.Never);
            ownerRepoMock.Verify(r => r.UpdateCapRoomForAllOwners(It.IsAny<List<int>>(), It.IsAny<int>()), Times.Never);
        }
    }
}
