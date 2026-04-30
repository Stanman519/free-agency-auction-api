using System;
using System.Collections.Generic;
using System.Linq;

namespace FreeAgencyAuctionAPI.Services
{
    public class PlayerHeadlineInput
    {
        public int RefId { get; set; }
        public string PlayerName { get; set; } = "";
        public string Position { get; set; } = "";
        public string TopBidderName { get; set; } = "";
        public int Salary { get; set; }
        public int Years { get; set; }
        public bool Win { get; set; }
        public int HandoffCount { get; set; }
        public int SagaDays { get; set; }
        public int DistinctBidders { get; set; }
        public int DeadlineMinutes { get; set; } = -1;
        public int TopMoneyRank { get; set; } = 0;
        public List<string> WarOpponents { get; set; } = new();
        public bool Cut { get; set; }
        public string CutBy { get; set; } = "";
    }

    public class OwnerHeadlineInput
    {
        public int RefId { get; set; }
        public string OwnerName { get; set; } = "";
        public string? JustSignedPlayer { get; set; }
        public string? JustSignedPosition { get; set; }
        public int JustSignedSalary { get; set; }
        public int JustSignedYears { get; set; }
        public int CapRoom { get; set; }
        public bool IsBigSpend { get; set; }
        public int TotalSpend { get; set; }
        public string? DominantPosition { get; set; }
        public bool IsRoomLeft { get; set; }
        public bool IsFewestNegotiations { get; set; }
        public int BidCount { get; set; }
    }

    public class ComposedHeadline
    {
        public string Text { get; set; } = "";
        public string Tags { get; set; } = "";
    }

    public static class HeadlineComposer
    {
        public static ComposedHeadline? ComposePlayer(PlayerHeadlineInput x)
        {
            var tags = new List<string>();
            if (x.Win) tags.Add("Win");
            if (x.HandoffCount >= 4) tags.Add("BiddingWar");
            if (x.SagaDays >= 2) tags.Add("SagaLength");
            if (x.DistinctBidders >= 3) tags.Add("WideInterest");
            if (x.DeadlineMinutes >= 0 && x.DeadlineMinutes < 120 && !x.Win) tags.Add("DeadlinePressure");
            if (x.TopMoneyRank > 0 && x.TopMoneyRank <= 3 && x.Win) tags.Add("TopMoney");

            if (x.Cut && tags.Count == 0) tags.Add("Cut");
            if (tags.Count == 0) return null;

            var seed = StableHash($"P:{x.RefId}:{string.Join(',', tags)}");
            string text;

            if (x.Win)
            {
                text = Pick(WinVariants, seed)
                    .Replace("{owner}", x.TopBidderName)
                    .Replace("{player}", x.PlayerName)
                    .Replace("{salary}", x.Salary.ToString())
                    .Replace("{years}", x.Years.ToString());
                var flavor = WinFlavor(x, tags, seed);
                if (!string.IsNullOrEmpty(flavor)) text += flavor;
            }
            else if (tags.Contains("BiddingWar"))
            {
                text = Pick(WarVariants, seed)
                    .Replace("{player}", x.PlayerName)
                    .Replace("{handoffs}", x.HandoffCount.ToString())
                    .Replace("{owners}", FormatList(x.WarOpponents));
                if (tags.Contains("SagaLength"))
                    text += $" — day {x.SagaDays}";
            }
            else if (tags.Contains("SagaLength"))
            {
                text = Pick(SagaVariants, seed)
                    .Replace("{player}", x.PlayerName)
                    .Replace("{days}", x.SagaDays.ToString());
            }
            else if (tags.Contains("WideInterest"))
            {
                text = Pick(WideInterestVariants, seed)
                    .Replace("{player}", x.PlayerName)
                    .Replace("{n}", x.DistinctBidders.ToString());
            }
            else if (tags.Contains("DeadlinePressure"))
            {
                text = Pick(DeadlineVariants, seed)
                    .Replace("{player}", x.PlayerName)
                    .Replace("{owner}", x.TopBidderName)
                    .Replace("{minutes}", x.DeadlineMinutes.ToString());
            }
            else if (tags.Contains("Cut"))
            {
                text = Pick(CutVariants, seed)
                    .Replace("{player}", x.PlayerName)
                    .Replace("{position}", x.Position)
                    .Replace("{owner}", x.CutBy);
            }
            else
            {
                return null;
            }

            return new ComposedHeadline { Text = text, Tags = string.Join(',', tags) };
        }

        public static ComposedHeadline? ComposeOwner(OwnerHeadlineInput x)
        {
            var tags = new List<string>();
            var justSigned = !string.IsNullOrEmpty(x.JustSignedPlayer);
            if (justSigned) tags.Add("JustSigned");
            if (x.IsBigSpend) tags.Add("BigSpend");
            if (x.IsRoomLeft) tags.Add("RoomLeft");
            if (x.IsFewestNegotiations) tags.Add("FewestNegotiations");
            if (!string.IsNullOrEmpty(x.DominantPosition)) tags.Add($"Pos:{x.DominantPosition}");

            if (tags.Count == 0) return null;

            var seed = StableHash($"O:{x.RefId}:{string.Join(',', tags)}");
            string text;

            if (justSigned)
            {
                text = Pick(JustSignedVariants, seed)
                    .Replace("{owner}", x.OwnerName)
                    .Replace("{player}", x.JustSignedPlayer ?? "")
                    .Replace("{salary}", x.JustSignedSalary.ToString())
                    .Replace("{years}", x.JustSignedYears.ToString());
                if (x.IsRoomLeft) text += $", still ${x.CapRoom} to spend";
            }
            else if (x.IsBigSpend)
            {
                text = Pick(BigSpendVariants, seed)
                    .Replace("{owner}", x.OwnerName)
                    .Replace("{total}", x.TotalSpend.ToString())
                    .Replace("{pos}", x.DominantPosition ?? "the board");
            }
            else if (x.IsRoomLeft)
            {
                text = Pick(RoomLeftVariants, seed)
                    .Replace("{owner}", x.OwnerName)
                    .Replace("{cap}", x.CapRoom.ToString());
            }
            else if (x.IsFewestNegotiations)
            {
                text = Pick(FewestVariants, seed)
                    .Replace("{owner}", x.OwnerName)
                    .Replace("{n}", x.BidCount.ToString());
            }
            else
            {
                return null;
            }

            return new ComposedHeadline { Text = text, Tags = string.Join(',', tags) };
        }

        private static string WinFlavor(PlayerHeadlineInput x, List<string> tags, int seed)
        {
            var hasSaga = tags.Contains("SagaLength");
            var hasWar = tags.Contains("BiddingWar");
            var hasTop = tags.Contains("TopMoney");

            if (hasSaga && hasWar)
                return Pick(WinFlavor_SagaWar, seed).Replace("{days}", x.SagaDays.ToString()).Replace("{handoffs}", x.HandoffCount.ToString());
            if (hasSaga)
                return Pick(WinFlavor_Saga, seed).Replace("{days}", x.SagaDays.ToString());
            if (hasWar)
                return Pick(WinFlavor_War, seed).Replace("{handoffs}", x.HandoffCount.ToString());
            if (hasTop)
                return Pick(WinFlavor_Top, seed).Replace("{rank}", x.TopMoneyRank.ToString()).Replace("{pos}", x.Position);
            return $" — ${x.Salary}/{x.Years}yr";
        }

        private static string FormatList(List<string> names)
        {
            if (names.Count == 0) return "";
            if (names.Count == 1) return names[0];
            if (names.Count == 2) return $"{names[0]} and {names[1]}";
            return string.Join(", ", names.Take(names.Count - 1)) + ", and " + names.Last();
        }

        private static T Pick<T>(IReadOnlyList<T> variants, int seed) =>
            variants[Math.Abs(seed) % variants.Count];

        private static int StableHash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (var c in s) h = h * 31 + c;
                return h;
            }
        }

        private static readonly string[] WinVariants =
        {
            "{owner} signs {player}",
            "{owner} lands {player}",
            "{owner} locks down {player}",
            "{player} is going to {owner}",
            "{owner} adds {player} to the roster",
            "{owner} closes on {player}",
            "{owner} reels in {player}",
            "{owner} grabs {player}",
            "{player} signs with {owner}",
            "{owner} brings {player} home",
        };

        private static readonly string[] WinFlavor_SagaWar =
        {
            " after a {days}-day bidding war",
            " — {handoffs} bid swaps over {days} days",
            " ending a marathon ({days} days, {handoffs} swaps)",
            ", outlasting the field after {days} days",
            " in a war that ran {days} days",
        };

        private static readonly string[] WinFlavor_Saga =
        {
            " after {days} days on the board",
            ", finally — {days}-day saga ends",
            " — {days}-day wait pays off",
            ", closing a {days}-day pursuit",
        };

        private static readonly string[] WinFlavor_War =
        {
            " after a fierce bidding war",
            ", outbidding the field",
            " in heavy traffic",
            " after {handoffs} bid swaps",
        };

        private static readonly string[] WinFlavor_Top =
        {
            " — top-{rank} {pos} money",
            ", paying top-{rank} {pos} dollar",
            " at top-{rank} {pos} salary",
        };

        private static readonly string[] WarVariants =
        {
            "{owners} trading bids on {player}",
            "Bidding war heating up for {player} ({owners})",
            "{owners} won't let go of {player}",
            "{player} caught between {owners}",
            "Tug-of-war on {player}: {owners}",
            "{owners} duking it out for {player}",
            "{player} the prize in {owners} standoff",
            "{handoffs} bids deep on {player} — {owners}",
        };

        private static readonly string[] SagaVariants =
        {
            "{player} saga enters day {days}",
            "{player} still on the board after {days} days",
            "Day {days} and {player} hasn't budged",
            "{player} the longest standoff at {days} days",
            "{days}-day pursuit of {player} continues",
            "Marathon bid for {player} hits day {days}",
        };

        private static readonly string[] WideInterestVariants =
        {
            "{n} teams chasing {player}",
            "{player} drawing interest from {n} owners",
            "{n}-way race for {player}",
            "{player} the most-wanted FA — {n} bidders",
            "{n} owners circling {player}",
        };

        private static readonly string[] DeadlineVariants =
        {
            "{player}'s clock running out — {minutes} min left, {owner} on top",
            "{minutes} minutes to land {player}, {owner} leads",
            "Deadline approaching on {player} — {minutes} min, {owner} top bidder",
            "{owner} {minutes} minutes from signing {player}",
        };

        private static readonly string[] CutVariants =
        {
            "{player} cut, hits free agency",
            "{owner} drops {player} — now a free agent",
            "{position} {player} released, available to bid",
            "{player} on the open market",
            "{owner} parts ways with {player}",
            "{player} cut loose by {owner}",
            "Free agent alert: {player} ({position})",
            "{player} now available — released by {owner}",
            "{owner} waives {player}",
            "{player}'s contract terminated, hits FA",
        };

        private static readonly string[] JustSignedVariants =
        {
            "{owner} signs {player} — ${salary}/{years}yr",
            "{owner} just locked in {player} at ${salary}",
            "{player} to {owner}, ${salary}/{years}yr",
            "{owner} adds {player} (${salary}/{years}yr)",
            "{owner} closes {player} for ${salary}/{years}yr",
        };

        private static readonly string[] BigSpendVariants =
        {
            "{owner} spending big — ${total} on {pos}",
            "{owner} top-3 spender so far (${total} on {pos})",
            "{owner} flexing the wallet, ${total} into {pos}",
            "{owner} loading up at {pos}, ${total} committed",
            "{owner} doubling down — ${total} on {pos}",
            "{owner}'s ${total} {pos} push leads the board",
        };

        private static readonly string[] RoomLeftVariants =
        {
            "{owner} still has ${cap} to spend",
            "{owner} sitting on ${cap} cap",
            "{owner}'s ${cap} stash waiting",
            "Plenty of room left for {owner} (${cap})",
            "{owner} quiet — ${cap} unspent",
        };

        private static readonly string[] FewestVariants =
        {
            "{owner} barely engaged — only {n} bids placed",
            "{owner} the quietest in negotiations ({n} bids)",
            "{owner} laying low: {n} bids in",
            "{n} bids and counting for {owner}",
        };
    }
}
