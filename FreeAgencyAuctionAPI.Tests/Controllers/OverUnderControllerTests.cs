using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Mapping;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.OverUnders;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Controllers
{
    public class OverUnderControllerTests
    {
        private const int OpenPoolId = 1;
        private const int ClosedPoolId = 2;
        private const int OwnerId = 42;

        private AuctionContext BuildDb(string name)
        {
            var opts = new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(name).Options;
            var db = new AuctionContext(opts);
            db.Pools.Add(new Pool
            {
                Id = OpenPoolId,
                Year = 2026,
                Type = "over-under-wins",
                League = "NFL",
                Name = "open",
                OpenDate = DateTime.UtcNow.AddDays(-1),
                StartDate = DateTime.UtcNow.AddDays(14)
            });
            db.Pools.Add(new Pool
            {
                Id = ClosedPoolId,
                Year = 2024,
                Type = "over-under-wins",
                League = "NFL",
                Name = "closed",
                OpenDate = DateTime.UtcNow.AddYears(-2),
                StartDate = DateTime.UtcNow.AddYears(-2).AddDays(30)
            });
            db.SaveChanges();
            return db;
        }

        // Use the app's real mapping profile — a mocked IMapper returns null and
        // the save path would NRE on it rather than exercising anything useful.
        private static IMapper BuildMapper() =>
            new MapperConfiguration(cfg => cfg.AddProfile<OverUnderPickProfile>())
                .CreateMapper();

        private OverUnderController BuildController(AuctionContext db, IAdminAuthorizationService adminAuthService = null)
        {
            var options = new Mock<IOptionsSnapshot<AppConfig>>();
            options.SetupGet(o => o.Value).Returns(new AppConfig());
            return new OverUnderController(
                db,
                BuildMapper(),
                new Mock<ILogger<OverUnderController>>().Object,
                new Mock<ISportsDataApi>().Object,
                options.Object,
                adminAuthService ?? new Mock<IAdminAuthorizationService>().Object);
        }

        private static List<OverUnderPickDTO> Picks(int poolId) => new()
        {
            new OverUnderPickDTO { LineId = 1, IsOver = true, LineAdjustment = 0, PoolId = poolId },
            new OverUnderPickDTO { LineId = 2, IsOver = false, LineAdjustment = 1, PoolId = poolId },
        };

        [Fact]
        public async Task SavePicks_RejectsPool_ThatHasAlreadyStarted()
        {
            var db = BuildDb(nameof(SavePicks_RejectsPool_ThatHasAlreadyStarted));
            var controller = BuildController(db);

            var result = await controller.UpsertTeamWinTotals(ClosedPoolId, OwnerId, Picks(ClosedPoolId));

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(db.OverUnderPicks.ToList());
        }

        [Fact]
        public async Task SavePicks_AcceptsPool_StillOpen()
        {
            var db = BuildDb(nameof(SavePicks_AcceptsPool_StillOpen));
            var controller = BuildController(db);

            var result = await controller.UpsertTeamWinTotals(OpenPoolId, OwnerId, Picks(OpenPoolId));

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal(2, db.OverUnderPicks.Count());
        }

        [Fact]
        public async Task SavePicks_ReturnsNotFound_ForUnknownPool()
        {
            var db = BuildDb(nameof(SavePicks_ReturnsNotFound_ForUnknownPool));
            var controller = BuildController(db);

            var result = await controller.UpsertTeamWinTotals(9999, OwnerId, Picks(9999));

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetAllUsersAndPicks_ReturnsNotFound_ForUnknownPool()
        {
            var db = BuildDb(nameof(GetAllUsersAndPicks_ReturnsNotFound_ForUnknownPool));
            var controller = BuildController(db);

            // Previously dereferenced a null pool and threw.
            var result = await controller.GetAllUsersAndPicksForPool(9999);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        private static void AddOwner(AuctionContext db, int ownerId, string authId = null, bool premium = false)
        {
            db.Owners.Add(new OwnerEntity
            {
                Ownerid = ownerId,
                Ownername = $"owner{ownerId}",
                Displayname = $"Owner {ownerId}",
                authid = authId,
                Premium = premium,
                Avatar = "",
                istest = false,
            });
            db.SaveChanges();
        }

        private static Mock<IAdminAuthorizationService> AdminAuthMock(bool authenticated, bool authorized, OwnerEntity owner = null)
        {
            var mock = new Mock<IAdminAuthorizationService>();
            var result = !authenticated
                ? AdminAuthResult.Unauthenticated()
                : authorized
                    ? AdminAuthResult.Authorized(owner)
                    : AdminAuthResult.Unauthorized(owner);
            mock.Setup(m => m.AuthorizeAdminAsync(It.IsAny<string>())).ReturnsAsync(result);
            return mock;
        }

        [Fact]
        public async Task GetUnpaidPoolUsers_ReturnsUnauthorized_WhenNotAuthenticated()
        {
            var db = BuildDb(nameof(GetUnpaidPoolUsers_ReturnsUnauthorized_WhenNotAuthenticated));
            var controller = BuildController(db, AdminAuthMock(authenticated: false, authorized: false).Object);

            var result = await controller.GetUnpaidPoolUsers(OpenPoolId);

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetUnpaidPoolUsers_ReturnsForbidden_WhenNotAdmin()
        {
            var db = BuildDb(nameof(GetUnpaidPoolUsers_ReturnsForbidden_WhenNotAdmin));
            var nonAdmin = new OwnerEntity { Ownerid = 1, Premium = false };
            var controller = BuildController(db, AdminAuthMock(authenticated: true, authorized: false, nonAdmin).Object);

            var result = await controller.GetUnpaidPoolUsers(OpenPoolId);

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
        }

        [Fact]
        public async Task GetUnpaidPoolUsers_ReturnsOnlyUnpaidPoolUsers_ForThatPool()
        {
            var db = BuildDb(nameof(GetUnpaidPoolUsers_ReturnsOnlyUnpaidPoolUsers_ForThatPool));
            AddOwner(db, OwnerId);
            AddOwner(db, OwnerId + 1);
            db.PoolUsers.Add(new PoolUser { Id = 1, PoolId = OpenPoolId, OwnerId = OwnerId, IsPaid = false });
            db.PoolUsers.Add(new PoolUser { Id = 2, PoolId = OpenPoolId, OwnerId = OwnerId + 1, IsPaid = true });
            db.PoolUsers.Add(new PoolUser { Id = 3, PoolId = ClosedPoolId, OwnerId = OwnerId, IsPaid = false });
            db.SaveChanges();
            var admin = new OwnerEntity { Ownerid = 99, Premium = true };
            var controller = BuildController(db, AdminAuthMock(authenticated: true, authorized: true, admin).Object);

            var result = await controller.GetUnpaidPoolUsers(OpenPoolId);

            var ok = Assert.IsType<OkObjectResult>(result);
            var unpaid = Assert.IsAssignableFrom<List<OverUnderController.PoolUserDTO>>(ok.Value);
            var single = Assert.Single(unpaid);
            Assert.Equal(1, single.Id);
        }

        [Fact]
        public async Task MarkPoolUsersAsPaid_ReturnsForbidden_WhenNotAdmin()
        {
            var db = BuildDb(nameof(MarkPoolUsersAsPaid_ReturnsForbidden_WhenNotAdmin));
            var nonAdmin = new OwnerEntity { Ownerid = 1, Premium = false };
            var controller = BuildController(db, AdminAuthMock(authenticated: true, authorized: false, nonAdmin).Object);

            var result = await controller.MarkPoolUsersAsPaid(OpenPoolId, new List<int> { 1 });

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
        }

        [Fact]
        public async Task MarkPoolUsersAsPaid_ReturnsBadRequest_WhenNoMatchingPoolUsers()
        {
            var db = BuildDb(nameof(MarkPoolUsersAsPaid_ReturnsBadRequest_WhenNoMatchingPoolUsers));
            var admin = new OwnerEntity { Ownerid = 99, Premium = true };
            var controller = BuildController(db, AdminAuthMock(authenticated: true, authorized: true, admin).Object);

            var result = await controller.MarkPoolUsersAsPaid(OpenPoolId, new List<int> { 12345 });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task MarkPoolUsersAsPaid_SetsIsPaid_ForMatchingPoolUsersInThatPoolOnly()
        {
            var db = BuildDb(nameof(MarkPoolUsersAsPaid_SetsIsPaid_ForMatchingPoolUsersInThatPoolOnly));
            AddOwner(db, OwnerId);
            AddOwner(db, OwnerId + 1);
            db.PoolUsers.Add(new PoolUser { Id = 1, PoolId = OpenPoolId, OwnerId = OwnerId, IsPaid = false });
            db.PoolUsers.Add(new PoolUser { Id = 2, PoolId = ClosedPoolId, OwnerId = OwnerId + 1, IsPaid = false });
            db.SaveChanges();
            var admin = new OwnerEntity { Ownerid = 99, Premium = true };
            var controller = BuildController(db, AdminAuthMock(authenticated: true, authorized: true, admin).Object);

            var result = await controller.MarkPoolUsersAsPaid(OpenPoolId, new List<int> { 1, 2 });

            Assert.IsType<OkObjectResult>(result);
            Assert.True(db.PoolUsers.Single(p => p.Id == 1).IsPaid);
            Assert.False(db.PoolUsers.Single(p => p.Id == 2).IsPaid);
        }
    }
}
