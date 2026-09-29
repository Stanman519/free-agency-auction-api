using System;
using System.Collections.Generic;
using System.Linq;
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
    public class MflService_ContractStatusAuditTests
    {
        private const int LeagueId = 13894;

        private readonly Mock<IGlobalMflApi> _globalApiMock = new();
        private readonly Mock<IMflApi> _leagueApiMock = new();
        private readonly Mock<ILogger<MflService>> _loggerMock = new();
        private readonly Mock<IGMBot> _gmMock = new();
        private readonly Mock<IPlayerRepo> _pRepoMock = new();
        private readonly Mock<IMapper> _mapperMock = new();
        private readonly Mock<IOptionsSnapshot<AppConfig>> _optionsMock = new();

        private MflService NewService(AuctionContext db) => new(
            _globalApiMock.Object, _leagueApiMock.Object, _loggerMock.Object, _gmMock.Object,
            _pRepoMock.Object, _mapperMock.Object, db, _optionsMock.Object);

        private AuctionContext NewDb() =>
            new(new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private void SetupEmptyRostersAndDrafts()
        {
            _leagueApiMock.Setup(x => x.GetMflRostersForPlayerSalaries(LeagueId, It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new RostersRoot { rosters = new Rosters { franchise = new List<FranchiseRoster>() } });
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot { draftResults = new DraftResults { draftUnit = new DraftUnit { draftPick = new List<MflDraftPick>() } } });
            _pRepoMock.Setup(x => x.GetAllTagsForLeague(LeagueId)).Returns(new List<FranchiseTagPlayer>());
        }

        [Fact]
        public async Task CoOwnedFranchise_DuplicateFranchiseId_DoesNotThrow()
        {
            // Regression: found via a live run against real league data — a co-owned
            // franchise had two LeagueOwner rows sharing the same Mflfranchiseid, which
            // crashed ToDictionaryAsync with "An item with the same key has already been added."
            using var db = NewDb();
            SetupEmptyRostersAndDrafts();
            db.LeagueOwners.AddRange(
                new LeagueOwnerEntity { Leagueownerid = 11, Leagueid = LeagueId, Mflfranchiseid = 5, Teamname = "Co-Owner A" },
                new LeagueOwnerEntity { Leagueownerid = 12, Leagueid = LeagueId, Mflfranchiseid = 5, Teamname = "Co-Owner B" });
            db.SaveChanges();

            var result = await NewService(db).GetContractStatusAudit(LeagueId);

            Assert.Empty(result); // no active cases in this fixture — just must not throw
        }

        [Fact]
        public async Task NoActiveCases_ReturnsEmptyList()
        {
            using var db = NewDb();
            SetupEmptyRostersAndDrafts();

            var result = await NewService(db).GetContractStatusAudit(LeagueId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task FranchiseTag_SecondTag_ProducesTag2()
        {
            using var db = NewDb();
            SetupEmptyRostersAndDrafts();
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 10, Leagueid = LeagueId, Mflfranchiseid = 1, Teamname = "Drew" });
            db.SaveChanges();

            var tags = new List<FranchiseTagPlayer>
            {
                new() { Mflplayerid = 500, Mflleagueid = LeagueId, Year = Utils.CurrentYear - 1, Leagueownerid = 10, Fullname = "Old Guy" },
                new() { Mflplayerid = 500, Mflleagueid = LeagueId, Year = Utils.CurrentYear, Leagueownerid = 10, Fullname = "Old Guy" },
            };
            _pRepoMock.Setup(x => x.GetAllTagsForLeague(LeagueId)).Returns(tags);

            var result = await NewService(db).GetContractStatusAudit(LeagueId);

            var entry = Assert.Single(result);
            Assert.Equal("TAG-2", entry.Tag);
            Assert.Equal("Drew", entry.Team);
            Assert.Equal(500, entry.MflPlayerId);
        }

        [Fact]
        public async Task FranchiseTag_BlankStoredFullname_ReResolvesFromMfl()
        {
            // Regression: found via a live run against real league data — two tag rows had
            // Fullname == " " stored at tag-time (same blank first/last name MFL quirk), so
            // the audit showed a blank, unactionable row until re-resolving via MFL.
            using var db = NewDb();
            SetupEmptyRostersAndDrafts();
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 10, Leagueid = LeagueId, Mflfranchiseid = 1, Teamname = "Drew" });
            db.SaveChanges();
            _pRepoMock.Setup(x => x.GetAllTagsForLeague(LeagueId)).Returns(new List<FranchiseTagPlayer>
            {
                new() { Mflplayerid = 500, Mflleagueid = LeagueId, Year = Utils.CurrentYear, Leagueownerid = 10, Fullname = " " }
            });
            _leagueApiMock.Setup(x => x.GetMflPlayerDetails(LeagueId, "500", It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new MflPlayerDetailsRoot
                {
                    players = new MflPlayerDetailsParent
                    {
                        player = new List<MflPlayerDetails> { new() { id = "500", first_name = "", last_name = "", name = "Tagged, Guy" } }
                    }
                });

            var result = await NewService(db).GetContractStatusAudit(LeagueId);

            var entry = Assert.Single(result);
            Assert.Equal("Guy Tagged", entry.PlayerName);
        }

        [Fact]
        public async Task WaiverExtension_ThisYear_ProducesWvrExt()
        {
            using var db = NewDb();
            SetupEmptyRostersAndDrafts();
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 20, Leagueid = LeagueId, Mflfranchiseid = 2, Teamname = "Tyler" });
            db.WaiverExtensions.Add(new WaiverExtension { LeagueId = LeagueId, LeagueOwnerId = 20, Year = Utils.CurrentYear, PlayerId = 600 });
            db.SaveChanges();
            _pRepoMock.Setup(x => x.GetPlayerById(600)).ReturnsAsync(new PlayerEntity { Mflid = 600, Fullname = "Waiver Guy" });

            var result = await NewService(db).GetContractStatusAudit(LeagueId);

            var entry = Assert.Single(result);
            Assert.Equal("WVR-EXT", entry.Tag);
            Assert.Equal("Waiver Guy", entry.PlayerName);
            Assert.Equal("Tyler", entry.Team);
        }

        [Fact]
        public async Task PendingHoldout_ProducesHoldoutTag_AcceptedDoesNotAppear()
        {
            // HOLDOUT means "currently holding out" (Status == "Pending") — once accepted,
            // the player is no longer holding out and must not carry the tag anymore.
            using var db = NewDb();
            SetupEmptyRostersAndDrafts();
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 30, Leagueid = LeagueId, Mflfranchiseid = 3, Teamname = "Caleb" });
            db.SaveChanges();
            _pRepoMock.Setup(x => x.GetHoldoutsForLeague(LeagueId, Utils.CurrentYear)).ReturnsAsync(new List<Holdout>
            {
                new() { LeagueId = LeagueId, LeagueOwnerId = 30, PlayerId = 700, Status = "Pending" },
                new() { LeagueId = LeagueId, LeagueOwnerId = 30, PlayerId = 701, Status = "Accepted" },
            });
            _pRepoMock.Setup(x => x.GetPlayerById(700)).ReturnsAsync(new PlayerEntity { Mflid = 700, Fullname = "Holdout Guy" });

            var result = await NewService(db).GetContractStatusAudit(LeagueId);

            var entry = Assert.Single(result);
            Assert.Equal("HOLDOUT", entry.Tag);
            Assert.Equal("Holdout Guy", entry.PlayerName);
        }

        [Fact]
        public async Task FranchiseTag_BackfillsCurrentSalaryAndContractYearFromRoster()
        {
            // The tag/waiver/holdout categories don't have a roster lookup on hand when built —
            // this confirms the post-processing pass fills CurrentSalary/CurrentContractYear
            // from the live roster so apply-contract-status has real terms to preserve.
            using var db = NewDb();
            _leagueApiMock.Setup(x => x.GetMflRostersForPlayerSalaries(LeagueId, It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new RostersRoot
                {
                    rosters = new Rosters
                    {
                        franchise = new List<FranchiseRoster>
                        {
                            new() { id = "1", player = new List<Player> { new() { id = "500", salary = "42", contractYear = "1" } } }
                        }
                    }
                });
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot { draftResults = new DraftResults { draftUnit = new DraftUnit { draftPick = new List<MflDraftPick>() } } });
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 10, Leagueid = LeagueId, Mflfranchiseid = 1, Teamname = "Drew" });
            db.SaveChanges();
            _pRepoMock.Setup(x => x.GetAllTagsForLeague(LeagueId)).Returns(new List<FranchiseTagPlayer>
            {
                new() { Mflplayerid = 500, Mflleagueid = LeagueId, Year = Utils.CurrentYear, Leagueownerid = 10, Fullname = "Tag Guy" }
            });

            var result = await NewService(db).GetContractStatusAudit(LeagueId);

            var entry = Assert.Single(result);
            Assert.Equal(42, entry.CurrentSalary);
            Assert.Equal(1, entry.CurrentContractYear);
        }

        [Fact]
        public async Task RookieStillOnScale_ProducesRoundYearTag()
        {
            using var db = NewDb();
            var draftYear = Utils.CurrentYear - 2;
            var rookieSalary = Utils.draftPicks[1]; // pick #1 rookie scale

            _leagueApiMock.Setup(x => x.GetMflRostersForPlayerSalaries(LeagueId, Utils.CurrentYear, It.IsAny<string>()))
                .ReturnsAsync(new RostersRoot
                {
                    rosters = new Rosters
                    {
                        franchise = new List<FranchiseRoster>
                        {
                            new() { id = "1", player = new List<Player> { new() { id = "900", salary = rookieSalary.ToString() } } }
                        }
                    }
                });
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, draftYear, It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot
                {
                    draftResults = new DraftResults
                    {
                        draftUnit = new DraftUnit
                        {
                            draftPick = new List<MflDraftPick> { new() { round = "01", pick = "1", player = "900" } }
                        }
                    }
                });
            // other 4 lookback years: empty
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, It.Is<int>(y => y != draftYear), It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot { draftResults = new DraftResults { draftUnit = new DraftUnit { draftPick = new List<MflDraftPick>() } } });
            _leagueApiMock.Setup(x => x.GetMflPlayerDetails(LeagueId, "900", It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new MflPlayerDetailsRoot
                {
                    players = new MflPlayerDetailsParent
                    {
                        player = new List<MflPlayerDetails> { new() { id = "900", first_name = "Rookie", last_name = "Guy", position = "WR" } }
                    }
                });
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 40, Leagueid = LeagueId, Mflfranchiseid = 1, Teamname = "Trent" });
            db.SaveChanges();
            _pRepoMock.Setup(x => x.GetAllTagsForLeague(LeagueId)).Returns(new List<FranchiseTagPlayer>());

            var result = await NewService(db).GetContractStatusAudit(LeagueId);

            var entry = Assert.Single(result);
            Assert.Equal($"R1-{draftYear}", entry.Tag);
            Assert.Equal("Rookie Guy", entry.PlayerName);
            Assert.Equal("Trent", entry.Team);
        }

        [Fact]
        public async Task ExactlyYear4Draft_OnOptionSalary_Produces5YO()
        {
            using var db = NewDb();
            var draftYear = Utils.CurrentYear - 4;
            var optionSalary = (int)Math.Round(Utils.draftPicks[1] * 1.3);

            _leagueApiMock.Setup(x => x.GetMflRostersForPlayerSalaries(LeagueId, Utils.CurrentYear, It.IsAny<string>()))
                .ReturnsAsync(new RostersRoot
                {
                    rosters = new Rosters
                    {
                        franchise = new List<FranchiseRoster>
                        {
                            new() { id = "1", player = new List<Player> { new() { id = "902", salary = optionSalary.ToString() } } }
                        }
                    }
                });
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, draftYear, It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot
                {
                    draftResults = new DraftResults { draftUnit = new DraftUnit { draftPick = new List<MflDraftPick> { new() { round = "01", pick = "1", player = "902" } } } }
                });
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, It.Is<int>(y => y != draftYear), It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot { draftResults = new DraftResults { draftUnit = new DraftUnit { draftPick = new List<MflDraftPick>() } } });
            _leagueApiMock.Setup(x => x.GetMflPlayerDetails(LeagueId, "902", It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new MflPlayerDetailsRoot
                {
                    players = new MflPlayerDetailsParent { player = new List<MflPlayerDetails> { new() { id = "902", first_name = "Option", last_name = "Guy", position = "WR" } } }
                });
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 40, Leagueid = LeagueId, Mflfranchiseid = 1, Teamname = "Trent" });
            db.SaveChanges();
            _pRepoMock.Setup(x => x.GetAllTagsForLeague(LeagueId)).Returns(new List<FranchiseTagPlayer>());

            var result = await NewService(db).GetContractStatusAudit(LeagueId);

            var entry = Assert.Single(result);
            Assert.Equal("5YO", entry.Tag);
        }

        [Fact]
        public async Task NonYear4Draft_CoincidentalOptionSalaryMatch_DoesNotProduce5YO()
        {
            // Regression: 5th-year options are only eligible for the exact year-4 draft class.
            // A different class's salary landing on the same 1.3x-scale number by coincidence
            // (e.g. a raise) used to get mislabeled 5YO.
            using var db = NewDb();
            var draftYear = Utils.CurrentYear - 2; // not eligible for a 5th-year option
            var optionSalary = (int)Math.Round(Utils.draftPicks[1] * 1.3);

            _leagueApiMock.Setup(x => x.GetMflRostersForPlayerSalaries(LeagueId, Utils.CurrentYear, It.IsAny<string>()))
                .ReturnsAsync(new RostersRoot
                {
                    rosters = new Rosters
                    {
                        franchise = new List<FranchiseRoster>
                        {
                            new() { id = "1", player = new List<Player> { new() { id = "903", salary = optionSalary.ToString() } } }
                        }
                    }
                });
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, draftYear, It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot
                {
                    draftResults = new DraftResults { draftUnit = new DraftUnit { draftPick = new List<MflDraftPick> { new() { round = "01", pick = "1", player = "903" } } } }
                });
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, It.Is<int>(y => y != draftYear), It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot { draftResults = new DraftResults { draftUnit = new DraftUnit { draftPick = new List<MflDraftPick>() } } });
            _leagueApiMock.Setup(x => x.GetMflPlayerDetails(LeagueId, "903", It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new MflPlayerDetailsRoot
                {
                    players = new MflPlayerDetailsParent { player = new List<MflPlayerDetails> { new() { id = "903", first_name = "Raised", last_name = "Guy", position = "WR" } } }
                });
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 40, Leagueid = LeagueId, Mflfranchiseid = 1, Teamname = "Trent" });
            db.SaveChanges();
            _pRepoMock.Setup(x => x.GetAllTagsForLeague(LeagueId)).Returns(new List<FranchiseTagPlayer>());

            var result = await NewService(db).GetContractStatusAudit(LeagueId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task RookieCurrentlyHoldingOut_CombinesBothTagsOnOneRow()
        {
            // A player can legitimately match two categories at once — the audit must
            // combine them into one row/one contractStatus string, not pick just one.
            using var db = NewDb();
            var draftYear = Utils.CurrentYear - 2;
            var rookieSalary = Utils.draftPicks[1];

            _leagueApiMock.Setup(x => x.GetMflRostersForPlayerSalaries(LeagueId, Utils.CurrentYear, It.IsAny<string>()))
                .ReturnsAsync(new RostersRoot
                {
                    rosters = new Rosters
                    {
                        franchise = new List<FranchiseRoster>
                        {
                            new() { id = "1", player = new List<Player> { new() { id = "904", salary = rookieSalary.ToString(), contractYear = "2" } } }
                        }
                    }
                });
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, draftYear, It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot
                {
                    draftResults = new DraftResults { draftUnit = new DraftUnit { draftPick = new List<MflDraftPick> { new() { round = "01", pick = "1", player = "904" } } } }
                });
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, It.Is<int>(y => y != draftYear), It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot { draftResults = new DraftResults { draftUnit = new DraftUnit { draftPick = new List<MflDraftPick>() } } });
            _leagueApiMock.Setup(x => x.GetMflPlayerDetails(LeagueId, "904", It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new MflPlayerDetailsRoot
                {
                    players = new MflPlayerDetailsParent { player = new List<MflPlayerDetails> { new() { id = "904", first_name = "Dual", last_name = "Tag", position = "WR" } } }
                });
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 50, Leagueid = LeagueId, Mflfranchiseid = 1, Teamname = "Trent" });
            db.SaveChanges();
            _pRepoMock.Setup(x => x.GetAllTagsForLeague(LeagueId)).Returns(new List<FranchiseTagPlayer>());
            _pRepoMock.Setup(x => x.GetHoldoutsForLeague(LeagueId, Utils.CurrentYear)).ReturnsAsync(new List<Holdout>
            {
                new() { LeagueId = LeagueId, LeagueOwnerId = 50, PlayerId = 904, Status = "Pending" }
            });
            _pRepoMock.Setup(x => x.GetPlayerById(904)).ReturnsAsync(new PlayerEntity { Mflid = 904, Fullname = "Dual Tag" });

            var result = await NewService(db).GetContractStatusAudit(LeagueId);

            var entry = Assert.Single(result);
            var tags = entry.Tag.Split('|');
            Assert.Contains($"R1-{draftYear}", tags);
            Assert.Contains("HOLDOUT", tags);
        }

        [Fact]
        public async Task CurrentYearDraftClass_IsIncluded()
        {
            // Regression: the lookback loop was Utils.CurrentYear-1 through -5, which excluded
            // this year's own rookie class entirely — an off-by-one found via a live run.
            using var db = NewDb();
            var draftYear = Utils.CurrentYear;
            var rookieSalary = Utils.draftPicks[1];

            _leagueApiMock.Setup(x => x.GetMflRostersForPlayerSalaries(LeagueId, Utils.CurrentYear, It.IsAny<string>()))
                .ReturnsAsync(new RostersRoot
                {
                    rosters = new Rosters
                    {
                        franchise = new List<FranchiseRoster>
                        {
                            new() { id = "1", player = new List<Player> { new() { id = "905", salary = rookieSalary.ToString() } } }
                        }
                    }
                });
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, draftYear, It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot
                {
                    draftResults = new DraftResults { draftUnit = new DraftUnit { draftPick = new List<MflDraftPick> { new() { round = "01", pick = "1", player = "905" } } } }
                });
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, It.Is<int>(y => y != draftYear), It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot { draftResults = new DraftResults { draftUnit = new DraftUnit { draftPick = new List<MflDraftPick>() } } });
            _leagueApiMock.Setup(x => x.GetMflPlayerDetails(LeagueId, "905", It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new MflPlayerDetailsRoot
                {
                    players = new MflPlayerDetailsParent { player = new List<MflPlayerDetails> { new() { id = "905", first_name = "This Year", last_name = "Rookie", position = "WR" } } }
                });
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 40, Leagueid = LeagueId, Mflfranchiseid = 1, Teamname = "Trent" });
            db.SaveChanges();
            _pRepoMock.Setup(x => x.GetAllTagsForLeague(LeagueId)).Returns(new List<FranchiseTagPlayer>());

            var result = await NewService(db).GetContractStatusAudit(LeagueId);

            var entry = Assert.Single(result);
            Assert.Equal($"R1-{draftYear}", entry.Tag);
        }

        [Fact]
        public async Task RookieWithAcceptedHoldoutRaise_StillGetsRookieTag()
        {
            // Regression: found via a live run — a round-1 rookie who had an accepted holdout
            // (raising salary above the clean rookie-scale number) fell through the exact-match
            // check and got NO tag at all, even though he's clearly still on a rookie deal.
            using var db = NewDb();
            var draftYear = Utils.CurrentYear - 2;
            var rookieSalary = Utils.draftPicks[1];
            var holdoutRaisedSalary = rookieSalary + 5;

            _leagueApiMock.Setup(x => x.GetMflRostersForPlayerSalaries(LeagueId, Utils.CurrentYear, It.IsAny<string>()))
                .ReturnsAsync(new RostersRoot
                {
                    rosters = new Rosters
                    {
                        franchise = new List<FranchiseRoster>
                        {
                            new() { id = "1", player = new List<Player> { new() { id = "906", salary = holdoutRaisedSalary.ToString() } } }
                        }
                    }
                });
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, draftYear, It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot
                {
                    draftResults = new DraftResults { draftUnit = new DraftUnit { draftPick = new List<MflDraftPick> { new() { round = "01", pick = "1", player = "906" } } } }
                });
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, It.Is<int>(y => y != draftYear), It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot { draftResults = new DraftResults { draftUnit = new DraftUnit { draftPick = new List<MflDraftPick>() } } });
            _leagueApiMock.Setup(x => x.GetMflPlayerDetails(LeagueId, "906", It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new MflPlayerDetailsRoot
                {
                    players = new MflPlayerDetailsParent { player = new List<MflPlayerDetails> { new() { id = "906", first_name = "Held", last_name = "Out", position = "WR" } } }
                });
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 40, Leagueid = LeagueId, Mflfranchiseid = 1, Teamname = "Trent" });
            db.Holdouts.Add(new Holdout
            {
                LeagueId = LeagueId, PlayerId = 906, Year = Utils.CurrentYear, Status = "Accepted",
                OriginalSalary = rookieSalary, HoldoutSalary = holdoutRaisedSalary
            });
            db.SaveChanges();
            _pRepoMock.Setup(x => x.GetAllTagsForLeague(LeagueId)).Returns(new List<FranchiseTagPlayer>());

            var result = await NewService(db).GetContractStatusAudit(LeagueId);

            var entry = Assert.Single(result);
            Assert.Equal($"R1-{draftYear}", entry.Tag);
            Assert.Contains("accepted holdout", entry.Reason);
        }

        [Fact]
        public async Task RookieWithBlankFirstLastName_FallsBackToNameField()
        {
            // Regression: found via a live run against real league data — GetMflPlayerDetails
            // often returns first_name/last_name blank, leaving the audit's PlayerName empty
            // unless it falls back to parsing the "Last, First" name field like GetMflPlayerById does.
            using var db = NewDb();
            var draftYear = Utils.CurrentYear - 2;
            var rookieSalary = Utils.draftPicks[1];

            _leagueApiMock.Setup(x => x.GetMflRostersForPlayerSalaries(LeagueId, Utils.CurrentYear, It.IsAny<string>()))
                .ReturnsAsync(new RostersRoot
                {
                    rosters = new Rosters
                    {
                        franchise = new List<FranchiseRoster>
                        {
                            new() { id = "1", player = new List<Player> { new() { id = "901", salary = rookieSalary.ToString() } } }
                        }
                    }
                });
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, draftYear, It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot
                {
                    draftResults = new DraftResults
                    {
                        draftUnit = new DraftUnit { draftPick = new List<MflDraftPick> { new() { round = "01", pick = "1", player = "901" } } }
                    }
                });
            _leagueApiMock.Setup(x => x.GetDraftResults(LeagueId, It.Is<int>(y => y != draftYear), It.IsAny<string>()))
                .ReturnsAsync(new MflDraftResultsRoot { draftResults = new DraftResults { draftUnit = new DraftUnit { draftPick = new List<MflDraftPick>() } } });
            _leagueApiMock.Setup(x => x.GetMflPlayerDetails(LeagueId, "901", It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new MflPlayerDetailsRoot
                {
                    players = new MflPlayerDetailsParent
                    {
                        player = new List<MflPlayerDetails> { new() { id = "901", first_name = "", last_name = "", name = "Guy, Rookie", position = "WR" } }
                    }
                });
            db.LeagueOwners.Add(new LeagueOwnerEntity { Leagueownerid = 40, Leagueid = LeagueId, Mflfranchiseid = 1, Teamname = "Trent" });
            db.SaveChanges();
            _pRepoMock.Setup(x => x.GetAllTagsForLeague(LeagueId)).Returns(new List<FranchiseTagPlayer>());

            var result = await NewService(db).GetContractStatusAudit(LeagueId);

            var entry = Assert.Single(result);
            Assert.Equal("Rookie Guy", entry.PlayerName);
        }
    }
}
