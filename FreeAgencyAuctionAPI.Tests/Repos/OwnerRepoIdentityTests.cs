using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Repos
{
    public class OwnerRepoIdentityTests
    {
        private const int LeagueId = 13894;
        private const string GoogleSub = "google-oauth2|111";
        private const string PasswordSub = "auth0|abc";

        private static AuctionContext NewDb(string name) =>
            new(new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(name).Options);

        private static OwnerRepo BuildRepo(AuctionContext db) => new(db, Mock.Of<ILogger<OwnerRepo>>());

        private static AuthUser User(string sub) => new() { Sub = sub, Name = "Aaron", Email = "a@x.com" };

        private static List<FranchisePlusAssets> Franchise(int id) =>
            new() { new FranchisePlusAssets { id = id.ToString("D4"), leagueId = LeagueId, name = "Team" } };

        private static async Task<OwnerEntity> SeedOwner(AuctionContext db, string authId, int franchiseId = 1)
        {
            var owner = new OwnerEntity { authid = authId, Ownername = "Aaron", PasswordHash = "", ConfidenceTitles = "[]" };
            owner.Identities.Add(new OwnerIdentityEntity { AuthId = authId });
            owner.Leagueowners.Add(new LeagueOwnerEntity { Leagueid = LeagueId, Mflfranchiseid = franchiseId, Teamname = "Team" });
            db.Owners.Add(owner);
            await db.SaveChangesAsync();
            return owner;
        }

        [Fact]
        public async Task Link_SecondSub_AddsIdentity_AndBothSubsResolveToSameOwner()
        {
            using var db = NewDb(nameof(Link_SecondSub_AddsIdentity_AndBothSubsResolveToSameOwner));
            var owner = await SeedOwner(db, GoogleSub);
            var repo = BuildRepo(db);

            var linked = await repo.LinkExistingOwnerByMflFranchises(User(PasswordSub), Franchise(1));

            Assert.Equal(owner.Ownerid, linked.OwnerId);
            Assert.Equal(owner.Ownerid, (await repo.GetOwnerByAuthId(GoogleSub)).OwnerId);   // old login still works
            Assert.Equal(owner.Ownerid, (await repo.GetOwnerByAuthId(PasswordSub)).OwnerId);
            Assert.Equal(GoogleSub, (await db.Owners.SingleAsync()).authid);                   // legacy authid not overwritten
            Assert.Equal(1, await db.Owners.CountAsync());
        }

        [Fact]
        public async Task Link_SubAlreadyOnAnotherOwner_DoesNotAddDuplicateIdentity()
        {
            using var db = NewDb(nameof(Link_SubAlreadyOnAnotherOwner_DoesNotAddDuplicateIdentity));
            var owner = await SeedOwner(db, GoogleSub);
            await SeedOwner(db, PasswordSub, franchiseId: 2);
            var repo = BuildRepo(db);

            await repo.LinkExistingOwnerByMflFranchises(User(PasswordSub), Franchise(1));

            Assert.Equal(1, await db.OwnerIdentities.CountAsync(i => i.AuthId == PasswordSub));
        }

        [Fact]
        public async Task AddOwner_CreatesIdentityForSub()
        {
            using var db = NewDb(nameof(AddOwner_CreatesIdentityForSub));
            var repo = BuildRepo(db);

            var dto = await repo.AddOwnerAndRelatedLeagues(User(PasswordSub), new List<FranchisePlusAssets>());

            Assert.Equal(dto.OwnerId, (await repo.GetOwnerByAuthId(PasswordSub)).OwnerId);
            Assert.Equal(PasswordSub, (await db.OwnerIdentities.SingleAsync()).AuthId);
        }

        [Fact]
        public async Task Merge_MovesIdentities_FoldsDuplicateLeagueOwner_AndOrsFlags()
        {
            using var db = NewDb(nameof(Merge_MovesIdentities_FoldsDuplicateLeagueOwner_AndOrsFlags));
            var keep = await SeedOwner(db, GoogleSub);
            var drop = await SeedOwner(db, PasswordSub);   // same (league, franchise) -> duplicate leagueowner
            drop.Premium = true;
            drop.ConfidencePaid = true;
            drop.ConfidenceTitles = "[2023]";
            keep.ConfidenceTitles = "[2021]";
            var dropLo = await db.LeagueOwners.SingleAsync(lo => lo.Ownerid == drop.Ownerid);
            var keepLo = await db.LeagueOwners.SingleAsync(lo => lo.Ownerid == keep.Ownerid);
            db.Bids.Add(new BidEntity { Ownerid = dropLo.Leagueownerid });
            await db.SaveChangesAsync();

            var result = await BuildRepo(db).MergeOwners(keep.Ownerid, drop.Ownerid);

            Assert.Equal(1, await db.Owners.CountAsync());
            Assert.Equal(2, await db.OwnerIdentities.CountAsync(i => i.OwnerId == keep.Ownerid));
            Assert.Equal(1, result.LeagueOwnersMerged);
            Assert.Single(await db.LeagueOwners.ToListAsync());                                    // duplicate row gone
            Assert.Equal(keepLo.Leagueownerid, (await db.Bids.SingleAsync()).Ownerid);              // child repointed
            var merged = await db.Owners.SingleAsync();
            Assert.True(merged.Premium);
            Assert.True(merged.ConfidencePaid);
            Assert.Equal(new[] { 2021, 2023 }, merged.ConfidenceTitleList);
            Assert.Equal(keep.Ownerid, (await BuildRepo(db).GetOwnerByAuthId(PasswordSub)).OwnerId);
        }

        [Fact]
        public async Task Merge_DedupesPicksAndPools_KeeperWinsOnOverlap()
        {
            using var db = NewDb(nameof(Merge_DedupesPicksAndPools_KeeperWinsOnOverlap));
            var keep = await SeedOwner(db, GoogleSub);
            var drop = await SeedOwner(db, PasswordSub, franchiseId: 2);
            db.NflPicks.AddRange(
                new Pick { OwnerId = keep.Ownerid, MatchupId = 1, Choice = 10 },
                new Pick { OwnerId = drop.Ownerid, MatchupId = 1, Choice = 20 },   // overlap -> dropped
                new Pick { OwnerId = drop.Ownerid, MatchupId = 2, Choice = 30 });  // unique -> moved
            db.ExtraPicks.AddRange(
                new ExtraPick { OwnerId = keep.Ownerid, PropId = 5, Choice = "A" },
                new ExtraPick { OwnerId = drop.Ownerid, PropId = 5, Choice = "B" });
            db.PoolUsers.AddRange(
                new PoolUser { Id = 100, PoolId = 1, OwnerId = keep.Ownerid, IsPaid = false },
                new PoolUser { Id = 200, PoolId = 1, OwnerId = drop.Ownerid, IsPaid = true });
            db.OverUnderPicks.AddRange(
                new OverUnderPick { LineId = 1, UserId = 100, PoolId = 1 },
                new OverUnderPick { LineId = 1, UserId = 200, PoolId = 1 },   // overlap -> dropped
                new OverUnderPick { LineId = 2, UserId = 200, PoolId = 1 });  // unique -> moved to pooluser 100
            await db.SaveChangesAsync();

            var result = await BuildRepo(db).MergeOwners(keep.Ownerid, drop.Ownerid);

            Assert.Equal(1, result.PicksMoved);
            Assert.Equal(1, result.PicksDropped);
            Assert.Equal(1, result.ExtraPicksDropped);
            Assert.Equal(new[] { 10, 30 }, db.NflPicks.OrderBy(p => p.MatchupId).Select(p => p.Choice ?? 0).ToArray());
            Assert.All(db.NflPicks, p => Assert.Equal(keep.Ownerid, p.OwnerId));
            var pu = await db.PoolUsers.SingleAsync();
            Assert.Equal(100, pu.Id);
            Assert.True(pu.IsPaid);
            Assert.Equal(new[] { 1, 2 }, db.OverUnderPicks.Select(p => p.LineId).OrderBy(x => x).ToArray());
            Assert.All(db.OverUnderPicks, p => Assert.Equal(100, p.UserId));
        }

        [Fact]
        public async Task Merge_SameOwner_Throws_UnknownOwner_Throws()
        {
            using var db = NewDb(nameof(Merge_SameOwner_Throws_UnknownOwner_Throws));
            var keep = await SeedOwner(db, GoogleSub);
            var repo = BuildRepo(db);

            await Assert.ThrowsAsync<System.ArgumentException>(() => repo.MergeOwners(keep.Ownerid, keep.Ownerid));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => repo.MergeOwners(keep.Ownerid, 999));
        }
    }
}
