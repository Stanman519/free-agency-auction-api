using System;
using System.Collections.Generic;
using System.Linq;
using FreeAgencyAuctionAPI.Models;

namespace FreeAgencyAuctionAPI.Services
{
    // League-wide salary snapshot built from canonical MFL rosters. The Contracts table is unpopulated
    // (see WinPlayer in IPlayerRepo), so headline "league-relative" claims (TopMoney, BigSpend, etc.)
    // must be sourced here instead. Pure builder (no DB/HTTP) so it unit-tests like BidAnalyzer.
    public class LeagueSalaryStats
    {
        public record ContractValue(int OwnerId, int MflId, int Value);

        public int TeamCount { get; init; }
        // ROSTER-status salaries per position, sorted descending — for "WR1 money" ranking.
        public Dictionary<string, List<int>> SalariesByPosition { get; init; } = new();
        // Full-league roster salary totals (all statuses) — for BigSpend / dominant position.
        public Dictionary<int, int> SpendByOwner { get; init; } = new();
        public Dictionary<int, Dictionary<string, int>> SpendByOwnerByPos { get; init; } = new();
        // salary * contractYear for every rostered contract — for BigContract league ranking.
        public List<ContractValue> LeagueContractValues { get; init; } = new();

        public static LeagueSalaryStats Build(
            List<FranchiseRoster> rosters,
            Dictionary<int, string> positionByMflId,
            Dictionary<int, int> ownerIdByFranchiseId)
        {
            var salariesByPos = new Dictionary<string, List<int>>();
            var spendByOwner = new Dictionary<int, int>();
            var spendByOwnerByPos = new Dictionary<int, Dictionary<string, int>>();
            var values = new List<ContractValue>();

            if (rosters == null)
                return new LeagueSalaryStats();

            foreach (var franchise in rosters)
            {
                if (franchise?.player == null) continue;
                if (!int.TryParse(franchise.id, out var franchiseId)) continue;
                if (!ownerIdByFranchiseId.TryGetValue(franchiseId, out var ownerId)) continue;

                foreach (var p in franchise.player)
                {
                    if (p == null || !int.TryParse(p.id, out var mflId)) continue;
                    var salary = int.TryParse(p.salary, out var s) ? s : 0;
                    var years = int.TryParse(p.contractYear, out var y) ? y : 0;
                    var status = string.IsNullOrEmpty(p.status) ? "ROSTER" : p.status;
                    positionByMflId.TryGetValue(mflId, out var pos);
                    pos ??= "";

                    // Spend totals + contract values reflect total commitment (all rostered statuses).
                    spendByOwner[ownerId] = spendByOwner.GetValueOrDefault(ownerId) + salary;
                    if (!string.IsNullOrEmpty(pos))
                    {
                        if (!spendByOwnerByPos.TryGetValue(ownerId, out var posMap))
                            spendByOwnerByPos[ownerId] = posMap = new Dictionary<string, int>();
                        posMap[pos] = posMap.GetValueOrDefault(pos) + salary;
                    }
                    values.Add(new ContractValue(ownerId, mflId, salary * Math.Max(years, 1)));

                    // Positional "money" ranking uses active ROSTER salaries only.
                    if (status == "ROSTER" && !string.IsNullOrEmpty(pos) && salary > 0)
                    {
                        if (!salariesByPos.TryGetValue(pos, out var list))
                            salariesByPos[pos] = list = new List<int>();
                        list.Add(salary);
                    }
                }
            }

            foreach (var list in salariesByPos.Values)
                list.Sort((a, b) => b.CompareTo(a));

            return new LeagueSalaryStats
            {
                TeamCount = rosters.Count,
                SalariesByPosition = salariesByPos,
                SpendByOwner = spendByOwner,
                SpendByOwnerByPos = spendByOwnerByPos,
                LeagueContractValues = values,
            };
        }

        // Rank of `salary` among league salaries at a position (1 = highest paid). Counts only
        // strictly-greater salaries, so the player's own (equal) row doesn't inflate the rank — safe
        // whether or not the just-signed contract is already in the roster snapshot. Ties share a rank.
        public static int LeagueRankOf(List<int> salariesDesc, int salary)
        {
            if (salariesDesc == null) return 1;
            return salariesDesc.Count(s => s > salary) + 1;
        }
    }
}
