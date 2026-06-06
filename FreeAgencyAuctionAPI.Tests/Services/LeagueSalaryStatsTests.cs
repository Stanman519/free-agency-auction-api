using System.Collections.Generic;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Services;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Services
{
    public class LeagueSalaryStatsTests
    {
        private static FranchiseRoster Roster(string id, params (string mflId, string salary, string years, string status)[] players)
            => new FranchiseRoster
            {
                id = id,
                player = new List<Player>(System.Array.ConvertAll(players, p => new Player
                {
                    id = p.mflId, salary = p.salary, contractYear = p.years, status = p.status,
                })),
            };

        [Fact]
        public void Build_RanksSalariesByPosition_RosterStatusOnly()
        {
            var rosters = new List<FranchiseRoster>
            {
                Roster("0001", ("100", "50", "3", "ROSTER"), ("101", "20", "2", "ROSTER")),
                Roster("0002", ("200", "40", "1", "ROSTER"), ("201", "99", "2", "TAXI_SQUAD")),
            };
            var pos = new Dictionary<int, string> { [100] = "WR", [101] = "WR", [200] = "WR", [201] = "WR" };
            var owners = new Dictionary<int, int> { [1] = 10, [2] = 20 };

            var stats = LeagueSalaryStats.Build(rosters, pos, owners);

            // TAXI_SQUAD ($99) excluded from positional money ranking; only ROSTER salaries counted.
            Assert.Equal(new List<int> { 50, 40, 20 }, stats.SalariesByPosition["WR"]);
            Assert.Equal(2, stats.TeamCount);
        }

        [Fact]
        public void LeagueRankOf_CountsStrictlyGreater_TiesShareRank()
        {
            var salaries = new List<int> { 50, 40, 40, 20 };
            Assert.Equal(1, LeagueSalaryStats.LeagueRankOf(salaries, 50)); // highest
            Assert.Equal(2, LeagueSalaryStats.LeagueRankOf(salaries, 40)); // tie -> rank 2
            Assert.Equal(4, LeagueSalaryStats.LeagueRankOf(salaries, 20));
            Assert.Equal(5, LeagueSalaryStats.LeagueRankOf(salaries, 5));  // below everyone
        }

        [Fact]
        public void LeagueRankOf_NewTopSalary_RanksFirst()
        {
            var salaries = new List<int> { 30, 20, 10 };
            Assert.Equal(1, LeagueSalaryStats.LeagueRankOf(salaries, 75));
        }

        [Fact]
        public void Build_SpendTotals_IncludeAllStatuses_PerOwner()
        {
            var rosters = new List<FranchiseRoster>
            {
                Roster("0001", ("100", "50", "3", "ROSTER"), ("101", "10", "1", "TAXI_SQUAD")),
            };
            var pos = new Dictionary<int, string> { [100] = "WR", [101] = "RB" };
            var owners = new Dictionary<int, int> { [1] = 10 };

            var stats = LeagueSalaryStats.Build(rosters, pos, owners);

            Assert.Equal(60, stats.SpendByOwner[10]);          // both statuses
            Assert.Equal(50, stats.SpendByOwnerByPos[10]["WR"]);
            Assert.Equal(10, stats.SpendByOwnerByPos[10]["RB"]);
        }

        [Fact]
        public void Build_ContractValues_AreSalaryTimesYears()
        {
            var rosters = new List<FranchiseRoster> { Roster("0001", ("100", "20", "4", "ROSTER")) };
            var pos = new Dictionary<int, string> { [100] = "WR" };
            var owners = new Dictionary<int, int> { [1] = 10 };

            var stats = LeagueSalaryStats.Build(rosters, pos, owners);

            Assert.Single(stats.LeagueContractValues);
            Assert.Equal(80, stats.LeagueContractValues[0].Value); // 20 * 4
        }

        [Fact]
        public void Build_SkipsFranchisesWithNoMappedOwner()
        {
            var rosters = new List<FranchiseRoster> { Roster("0009", ("100", "50", "3", "ROSTER")) };
            var pos = new Dictionary<int, string> { [100] = "WR" };
            var owners = new Dictionary<int, int>(); // franchise 9 unmapped

            var stats = LeagueSalaryStats.Build(rosters, pos, owners);

            Assert.Empty(stats.SpendByOwner);
            Assert.False(stats.SalariesByPosition.ContainsKey("WR"));
        }
    }
}
