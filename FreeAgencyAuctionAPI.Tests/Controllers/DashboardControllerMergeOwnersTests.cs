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
    public class DashboardControllerMergeOwnersTests
    {
        private static DashboardController Build(Mock<IOwnerRepo> repo) => new(
            Mock.Of<ILeagueService>(), Mock.Of<IMflService>(), Mock.Of<IOwnerService>(), Mock.Of<IPlayerRepo>(),
            Mock.Of<ILogger<DashboardController>>(),
            new AuctionContext(new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options),
            Mock.Of<IGMBot>(), repo.Object);

        [Fact]
        public async Task MergeOwners_DelegatesToRepo_ReturnsResult()
        {
            var repo = new Mock<IOwnerRepo>();
            repo.Setup(r => r.MergeOwners(1, 2)).ReturnsAsync(new OwnerMergeResult { IdentitiesMoved = 1 });

            var ok = Assert.IsType<OkObjectResult>(await Build(repo).MergeOwners(1, 2));

            Assert.Equal(1, Assert.IsType<OwnerMergeResult>(ok.Value).IdentitiesMoved);
        }

        [Fact]
        public async Task MergeOwners_UnknownOwner_Returns404_SameOwner_Returns400()
        {
            var repo = new Mock<IOwnerRepo>();
            repo.Setup(r => r.MergeOwners(1, 99)).ThrowsAsync(new KeyNotFoundException("owner 99 not found"));
            repo.Setup(r => r.MergeOwners(1, 1)).ThrowsAsync(new ArgumentException("same"));
            var controller = Build(repo);

            Assert.IsType<NotFoundObjectResult>(await controller.MergeOwners(1, 99));
            Assert.IsType<BadRequestObjectResult>(await controller.MergeOwners(1, 1));
        }
    }
}
