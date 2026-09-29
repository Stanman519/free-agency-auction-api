using System;
using System.Collections.Generic;
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
    public class MflService_ProjectedFifthYearOptionTests
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

        private static AuctionContext NewDb() =>
            new(new DbContextOptionsBuilder<AuctionContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        // Utils.CurrentYear resolves to the real current year, so the rookie tag's draft year
        // must be set relative to it for these tests to stay valid over time.
        private static int DraftYearWithOptionDecisionThisYear() => Utils.CurrentYear - 4;

        [Fact]
        public async Task RookieOnOriginalScale_OptionYearInWindow_ProjectsOptionSalary()
        {
            using var db = NewDb();
            var draftYear = DraftYearWithOptionDecisionThisYear();
            var rosters = new List<FranchiseRoster>
            {
                new()
                {
                    id = "1",
                    player = new List<Player>
                    {
                        new() { id = "900", salary = "24", contractYear = "1", contractStatus = $"R1-{draftYear}" }
                    }
                }
            };

            var result = await NewService(db).GetProjectedFifthYearOptionSalaries(LeagueId, rosters);

            Assert.Equal((int)Math.Round(24 * 1.3), result["900"]);
        }

        [Fact]
        public async Task PlayerAlreadyOn5YO_NotProjected()
        {
            using var db = NewDb();
            var draftYear = DraftYearWithOptionDecisionThisYear();
            var rosters = new List<FranchiseRoster>
            {
                new()
                {
                    id = "1",
                    player = new List<Player>
                    {
                        new() { id = "900", salary = "31", contractYear = "1", contractStatus = "5YO" }
                    }
                }
            };

            var result = await NewService(db).GetProjectedFifthYearOptionSalaries(LeagueId, rosters);

            Assert.Empty(result);
            _ = draftYear; // unused, kept for clarity that the tag alone (not the year) excludes 5YO players
        }

        [Fact]
        public async Task OptionYearFarInFuture_NotYetProjected()
        {
            using var db = NewDb();
            // A rookie drafted this year is nowhere near their option decision (4 years away
            // > the 4-year visible window means yearsAway = 4, just outside the 0-3 window).
            var rosters = new List<FranchiseRoster>
            {
                new()
                {
                    id = "1",
                    player = new List<Player>
                    {
                        new() { id = "900", salary = "40", contractYear = "4", contractStatus = $"R1-{Utils.CurrentYear}" }
                    }
                }
            };

            var result = await NewService(db).GetProjectedFifthYearOptionSalaries(LeagueId, rosters);

            Assert.Empty(result);
        }

        [Fact]
        public async Task NonRound1Player_NoRookieTag_NotProjected()
        {
            using var db = NewDb();
            var rosters = new List<FranchiseRoster>
            {
                new()
                {
                    id = "1",
                    player = new List<Player>
                    {
                        new() { id = "900", salary = "40", contractYear = "1", contractStatus = "TAG-1" }
                    }
                }
            };

            var result = await NewService(db).GetProjectedFifthYearOptionSalaries(LeagueId, rosters);

            Assert.Empty(result);
        }

        [Fact]
        public async Task AcceptedHoldoutRaisedSalary_ProjectsOffCurrentRaisedSalaryNotOriginal()
        {
            // The option is always +30% over whatever the player is actually being paid right
            // now — a past holdout raise doesn't get unwound for this calculation.
            using var db = NewDb();
            var draftYear = DraftYearWithOptionDecisionThisYear();
            db.Holdouts.Add(new Holdout
            {
                LeagueId = LeagueId,
                PlayerId = 900,
                Status = "Accepted",
                OriginalSalary = 24,
                HoldoutSalary = 29,
                Year = Utils.CurrentYear - 1
            });
            db.SaveChanges();

            var rosters = new List<FranchiseRoster>
            {
                new()
                {
                    id = "1",
                    player = new List<Player>
                    {
                        // current salary (29) reflects the holdout raise, not the original rookie-scale (24)
                        new() { id = "900", salary = "29", contractYear = "1", contractStatus = $"R1-{draftYear}" }
                    }
                }
            };

            var result = await NewService(db).GetProjectedFifthYearOptionSalaries(LeagueId, rosters);

            Assert.Equal((int)Math.Round(29 * 1.3), result["900"]);
        }
    }
}
