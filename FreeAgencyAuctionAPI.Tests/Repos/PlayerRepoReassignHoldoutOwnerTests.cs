using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Repos
{
    public class PlayerRepoReassignHoldoutOwnerTests
    {
        private const int LeagueId = 13894;
        private const int Year = 2026;

        private static AuctionContext NewDb(string name) =>
            new(new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(name).Options);

        private static PlayerRepo BuildRepo(AuctionContext db) =>
            new(db, Mock.Of<IGMBot>(), Mock.Of<ILogger<PlayerRepo>>());

        [Fact]
        public async Task ReassignHoldoutOwner_MovesPendingHoldout_ToNewFranchisesOwner()
        {
            using var db = NewDb(nameof(ReassignHoldoutOwner_MovesPendingHoldout_ToNewFranchisesOwner));
            db.LeagueOwners.AddRange(
                new LeagueOwnerEntity { Leagueownerid = 1, Leagueid = LeagueId, Mflfranchiseid = 1, Teamname = "Old" },
                new LeagueOwnerEntity { Leagueownerid = 2, Leagueid = LeagueId, Mflfranchiseid = 2, Teamname = "New" });
            db.Holdouts.Add(new Holdout
            {
                Id = 1, LeagueId = LeagueId, LeagueOwnerId = 1, Year = Year, PlayerId = 900, Status = "Pending"
            });
            await db.SaveChangesAsync();

            var repo = BuildRepo(db);
            var reassigned = await repo.ReassignHoldoutOwner(LeagueId, 900, newFranchiseId: 2, Year);

            Assert.True(reassigned);
            Assert.Equal(2, db.Holdouts.Single(h => h.Id == 1).LeagueOwnerId);
        }

        [Fact]
        public async Task ReassignHoldoutOwner_NoPendingHoldoutForPlayer_ReturnsFalse_NoThrow()
        {
            // Traded player with no active holdout — the common case; must be a silent no-op.
            using var db = NewDb(nameof(ReassignHoldoutOwner_NoPendingHoldoutForPlayer_ReturnsFalse_NoThrow));
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 2, Leagueid = LeagueId, Mflfranchiseid = 2, Teamname = "New" });
            await db.SaveChangesAsync();

            var repo = BuildRepo(db);
            var reassigned = await repo.ReassignHoldoutOwner(LeagueId, 900, newFranchiseId: 2, Year);

            Assert.False(reassigned);
        }

        [Fact]
        public async Task ReassignHoldoutOwner_IgnoresNonPendingHoldouts()
        {
            using var db = NewDb(nameof(ReassignHoldoutOwner_IgnoresNonPendingHoldouts));
            db.LeagueOwners.AddRange(
                new LeagueOwnerEntity { Leagueownerid = 1, Leagueid = LeagueId, Mflfranchiseid = 1, Teamname = "Old" },
                new LeagueOwnerEntity { Leagueownerid = 2, Leagueid = LeagueId, Mflfranchiseid = 2, Teamname = "New" });
            db.Holdouts.Add(new Holdout
            {
                Id = 1, LeagueId = LeagueId, LeagueOwnerId = 1, Year = Year, PlayerId = 900, Status = "Accepted"
            });
            await db.SaveChangesAsync();

            var repo = BuildRepo(db);
            var reassigned = await repo.ReassignHoldoutOwner(LeagueId, 900, newFranchiseId: 2, Year);

            Assert.False(reassigned);
            Assert.Equal(1, db.Holdouts.Single(h => h.Id == 1).LeagueOwnerId);
        }

        [Fact]
        public async Task ReassignHoldoutOwner_UnknownFranchise_ReturnsFalse_NoThrow()
        {
            using var db = NewDb(nameof(ReassignHoldoutOwner_UnknownFranchise_ReturnsFalse_NoThrow));
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 1, Leagueid = LeagueId, Mflfranchiseid = 1, Teamname = "Old" });
            db.Holdouts.Add(new Holdout
            {
                Id = 1, LeagueId = LeagueId, LeagueOwnerId = 1, Year = Year, PlayerId = 900, Status = "Pending"
            });
            await db.SaveChangesAsync();

            var repo = BuildRepo(db);
            var reassigned = await repo.ReassignHoldoutOwner(LeagueId, 900, newFranchiseId: 99, Year);

            Assert.False(reassigned);
        }
    }
}
