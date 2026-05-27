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
        public string BidderSetKey { get; set; } = ""; // stable representation of current serious bidder set
        public bool Cut { get; set; }
        public string CutBy { get; set; } = "";
        public int CutSalary { get; set; }
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
        public int BidCount { get; set; }
        public bool IsMaxCapRoom { get; set; }
        public bool IsTopNegotiator { get; set; }
        public int TopNegotiatorBidCount { get; set; }
        public string? PositionRunPosition { get; set; }
        public string? BigContractPlayer { get; set; }
        public int BigContractSalary { get; set; }
        public int BigContractYears { get; set; }
        public bool IsDrySpell { get; set; }
        public int DrySpellDays { get; set; }
        public string? PositionalLeaderPosition { get; set; }
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
            // BiddingWar / WideInterest also require player to be a non-scrub ($5+ headline salary).
            var notable = x.Salary >= 5 || x.Win;
            if (x.HandoffCount >= 4 && notable) tags.Add("BiddingWar");
            if (x.SagaDays >= 2) tags.Add("SagaLength");
            if (x.DistinctBidders >= 4 && notable) tags.Add("WideInterest");
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

            // Embed bidder-set hash in the primary tag for bidder-set-driven stories so the cooldown
            // comparison only matches when the same bidder lineup is still in play.
            if (!x.Win && (tags[0] == "BiddingWar" || tags[0] == "WideInterest"))
            {
                var hash = (uint)StableHash($"bidders:{x.BidderSetKey}");
                tags[0] = $"{tags[0]}:h{hash:X4}";
            }

            return new ComposedHeadline { Text = text, Tags = string.Join(',', tags) };
        }

        public static ComposedHeadline? ComposeOwner(OwnerHeadlineInput x)
        {
            var tags = new List<string>();
            var justSigned = !string.IsNullOrEmpty(x.JustSignedPlayer);
            var hasBigContract = !string.IsNullOrEmpty(x.BigContractPlayer);
            var hasPositionRun = !string.IsNullOrEmpty(x.PositionRunPosition);
            var hasPositionalLeader = !string.IsNullOrEmpty(x.PositionalLeaderPosition);

            // Order matters — first tag is the primary "category" used for cooldown matching.
            if (justSigned) tags.Add("JustSigned");
            if (hasBigContract) tags.Add($"BigContract:{x.BigContractPlayer}");
            if (hasPositionRun) tags.Add($"PositionRun:{x.PositionRunPosition}");
            if (hasPositionalLeader) tags.Add($"PositionalLeader:{x.PositionalLeaderPosition}");
            if (x.IsDrySpell) tags.Add("DrySpell");
            if (x.IsBigSpend) tags.Add("BigSpend");
            if (x.IsRoomLeft) tags.Add("RoomLeft");
            if (x.IsMaxCapRoom) tags.Add("MaxCapRoom");
            if (x.IsTopNegotiator) tags.Add("MostActive");
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
            else if (hasBigContract)
            {
                text = Pick(BigContractVariants, seed)
                    .Replace("{owner}", x.OwnerName)
                    .Replace("{player}", x.BigContractPlayer ?? "")
                    .Replace("{salary}", x.BigContractSalary.ToString())
                    .Replace("{years}", x.BigContractYears.ToString());
            }
            else if (hasPositionRun)
            {
                text = Pick(PositionRunVariants, seed)
                    .Replace("{owner}", x.OwnerName)
                    .Replace("{pos}", x.PositionRunPosition ?? "");
            }
            else if (hasPositionalLeader)
            {
                text = Pick(PositionalLeaderVariants, seed)
                    .Replace("{owner}", x.OwnerName)
                    .Replace("{pos}", x.PositionalLeaderPosition ?? "")
                    .Replace("{total}", x.TotalSpend.ToString());
            }
            else if (x.IsDrySpell)
            {
                text = Pick(DrySpellVariants, seed)
                    .Replace("{owner}", x.OwnerName)
                    .Replace("{cap}", x.CapRoom.ToString())
                    .Replace("{days}", x.DrySpellDays.ToString());
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
            else if (x.IsMaxCapRoom)
            {
                text = Pick(MaxCapRoomVariants, seed)
                    .Replace("{owner}", x.OwnerName)
                    .Replace("{cap}", x.CapRoom.ToString());
            }
            else if (x.IsTopNegotiator)
            {
                text = Pick(MostActiveVariants, seed)
                    .Replace("{owner}", x.OwnerName)
                    .Replace("{n}", x.TopNegotiatorBidCount.ToString());
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
            "{player} is heading to {owner}",
            "{owner} adds {player} to the roster",
            "{owner} closes on {player}",
            "{owner} reels in {player}",
            "{owner} reaches agreement with {player}",
            "{player} signs with {owner}",
            "{owner} brings {player} home",
            "Source: {owner} agreeing to terms with {player}",
            "Done deal: {owner} lands {player} — ${salary}/{years}yr",
            "It's official: {player} signs with {owner}",
            "{player} off the market — {owner} closes the deal",
            "Confirmed: {owner} secures {player} at ${salary}",
            "{player} finds a home with {owner}",
            "{owner} wins the {player} sweepstakes",
            "{owner} inks {player} to a {years}-year deal",
        };

        private static readonly string[] WinFlavor_SagaWar =
        {
            " after a {days}-day pursuit",
            " — {handoffs} counter-offers over {days} days",
            " ending a {days}-day sweepstakes",
            ", outlasting the field after {days} days",
            " in a chase that ran {days} days",
            " — {days}-day market battle finally over",
            ", closing a {days}-day saga",
            " after {handoffs} counters across {days} days",
            " — {days} days of back-and-forth, done",
        };

        private static readonly string[] WinFlavor_Saga =
        {
            " after {days} days on the market",
            ", finally — {days}-day saga ends",
            " — {days}-day wait pays off",
            ", closing a {days}-day pursuit",
            " after a {days}-day chase",
            ", ending a {days}-day standoff",
            " — {days} days of patience pays off",
            ", {days} days in the making",
        };

        private static readonly string[] WinFlavor_War =
        {
            " after a fierce pursuit",
            ", outpacing the field",
            " in a crowded market",
            " after {handoffs} counter-offers",
            ", beating the market for him",
            " — multi-team chase done",
            " after holding off rival suitors",
            " in a true bidding war",
        };

        private static readonly string[] WinFlavor_Top =
        {
            " — top-{rank} {pos} money",
            ", paying top-{rank} {pos} dollar",
            " at top-{rank} {pos} salary",
            ", now a top-{rank} {pos} deal",
            " — top-{rank} contract at the position",
        };

        private static readonly string[] WarVariants =
        {
            "Sweepstakes heating up: {owners} pursuing {player}",
            "{player} generating buzz — {owners} exchanging offers",
            "{owners} keep going back and forth on {player}",
            "{player} caught in a {handoffs}-offer standoff — {owners}",
            "No one's blinking: {owners} dueling over {player}",
            "Tug-of-war: {owners} refuse to back off {player}",
            "{player} the most pursued name on the market ({owners})",
            "Offers and counters flying — {handoffs} and counting between {owners} on {player}",
            "{owners} locked in a {handoffs}-offer chase for {player}",
            "Multi-team pursuit: {owners} all want {player}",
            "{player} drawing serious interest — {owners} won't quit",
            "Market frenzy on {player}: {owners} keep countering",
            "Source: {owners} all in on {player}",
            "It's a true bidding war for {player} — {owners} swapping leads",
        };

        private static readonly string[] SagaVariants =
        {
            "{player} saga enters day {days}",
            "{player} still on the market after {days} days",
            "Day {days} and {player} still unsigned",
            "{player} the longest standoff at {days} days",
            "{days}-day pursuit of {player} continues",
            "Marathon pursuit of {player} hits day {days}",
            "{player} still unsigned — day {days} and counting",
            "The {player} saga rolls on: day {days}",
            "{days} days in, no deal yet for {player}",
            "{player} remains a free agent — day {days}",
            "Stalemate on {player}: {days} days, no resolution",
            "{player}'s market still cooking on day {days}",
        };

        private static readonly string[] WideInterestVariants =
        {
            "{n} teams pursuing {player}",
            "{n} suitors lined up for {player}",
            "{player} drawing interest from {n} teams",
            "High demand: {n} clubs in on {player}",
            "{player} has {n} legitimate suitors",
            "{n}-team chase heating up for {player}",
            "{n} teams making offers on {player}",
            "{player} the talk of the market — {n} suitors",
            "Crowded market for {player}: {n} teams in",
            "{n} owners actively negotiating for {player}",
            "Sources: {n} teams have made offers on {player}",
            "{player} a hot commodity — {n} teams in the mix",
        };

        private static readonly string[] DeadlineVariants =
        {
            "Final {minutes} minutes: {owner} leads the pursuit of {player}",
            "Clock running out on {player} — {owner} holds the leading offer with {minutes} min left",
            "Deadline looming: {minutes} min left, {owner} on top for {player}",
            "{owner} {minutes} minutes from finalizing terms with {player}",
            "{minutes}-minute warning: {owner} closest to a deal with {player}",
            "Down to {minutes} minutes — {owner} the frontrunner for {player}",
            "{player} window closing in {minutes} min — {owner} on top",
            "Final stretch: {owner}'s offer leads for {player} with {minutes} left",
            "{minutes} min to go and {owner} is in pole position for {player}",
        };

        private static readonly string[] CutVariants =
        {
            "{player} cut, hits free agency",
            "{owner} drops {player} — now a free agent",
            "{position} {player} released, on the open market",
            "{player} on the open market",
            "{owner} parts ways with {player}",
            "{player} cut loose by {owner}",
            "Free agent alert: {player} ({position})",
            "{player} now available — released by {owner}",
            "{owner} waives {player}",
            "{player}'s contract terminated, hits FA",
            "Breaking: {player} released by {owner}",
            "Source: {owner} cutting {player} loose",
            "{player} is now a free agent, per sources",
            "{owner} moves on from {player} — now an unrestricted FA",
            "{player} released by {owner}, free to sign anywhere",
        };

        private static readonly string[] JustSignedVariants =
        {
            "{owner} signs {player} — ${salary}/{years}yr",
            "{owner} just locked in {player} at ${salary}",
            "{player} to {owner}, ${salary}/{years}yr",
            "{owner} adds {player} (${salary}/{years}yr)",
            "{owner} closes {player} for ${salary}/{years}yr",
            "Done deal: {owner} secures {player} — ${salary}/{years}yr",
            "Source: {owner} finalizing {years}-yr deal with {player} at ${salary}",
            "{owner} brings in {player} on a {years}-year, ${salary} contract",
            "{owner} inks {player}: {years} years, ${salary}",
            "Confirmed: {owner}–{player} agree to {years}/${salary}",
            "{owner} agrees to terms with {player} (${salary}/{years}yr)",
            "{player} signs with {owner} — ${salary} over {years} years",
        };

        private static readonly string[] BigSpendVariants =
        {
            "{owner} going all in — ${total} committed to {pos}",
            "{owner} top-3 spender at ${total}, leaning {pos}",
            "{owner} aggressive in the {pos} market — ${total} spent",
            "{owner} loading up at {pos}: ${total} and counting",
            "Big market: {owner} pouring ${total} into {pos}",
            "{owner}'s ${total} {pos} haul leads the field",
            "{owner} the top buyer at {pos} (${total})",
            "{owner} reshaping the roster — ${total} into {pos}",
            "Sources: {owner} prioritizing {pos}, ${total} committed",
            "{owner} dominating the {pos} market with ${total}",
            "All-in at {pos}: {owner} up to ${total}",
        };

        private static readonly string[] RoomLeftVariants =
        {
            "{owner} still has ${cap} to spend",
            "{owner} sitting on ${cap} in cap space",
            "Patient: {owner} holding ${cap} — hasn't blinked",
            "Plenty left in the tank for {owner} (${cap})",
            "{owner} quiet — ${cap} still available",
            "{owner} still has ${cap} to deploy",
            "Waiting game: {owner} holding ${cap} in reserve",
            "{owner} keeping ${cap} on the table, no rush",
            "{owner} hasn't tipped their hand — ${cap} in pocket",
            "${cap} still in {owner}'s war chest",
        };

        private static readonly string[] MaxCapRoomVariants =
        {
            "{owner} leads the league with ${cap} to spend",
            "War chest: {owner} sitting on ${cap} — most in the league",
            "Still holding: {owner} the biggest buyer in waiting (${cap})",
            "{owner} hasn't opened the wallet — ${cap} remaining, league-high",
            "Most room in the league: {owner} with ${cap} to deploy",
            "{owner} the league's biggest threat with ${cap} available",
            "Cap king: {owner} sitting on a league-high ${cap}",
            "All eyes on {owner} — ${cap} to spend, most of any team",
            "{owner} loaded for a late strike with ${cap}",
            "${cap} and ready to pounce: {owner}",
        };

        private static readonly string[] BigContractVariants =
        {
            "Blockbuster: {owner} hands {player} ${salary}/{years}yr — league's biggest deal",
            "{owner} drops a bag on {player}: ${salary} over {years} years, top of the market",
            "Record territory — {owner} pays {player} ${salary}/{years}yr",
            "{owner} swings big — {player} signed at ${salary}/{years}yr, league high",
            "Top-of-market move: {owner} inks {player} to ${salary}/{years}yr",
            "{owner} sets the bar: {player} for ${salary} over {years} years",
        };

        private static readonly string[] PositionRunVariants =
        {
            "Stocking up at {pos}: {owner} adds another",
            "{owner} cornering the {pos} market — multiple signings in 24h",
            "Position run: {owner} loading up at {pos}",
            "{owner} doubles down at {pos}",
            "{owner} going all-in at {pos}",
            "Run on {pos} for {owner} — two in a day",
        };

        private static readonly string[] PositionalLeaderVariants =
        {
            "{owner} owns the {pos} market — ${total} committed",
            "Top dog at {pos}: {owner} with ${total}",
            "{owner} the {pos} kingpin — ${total} on the books",
            "Heaviest {pos} room in the league belongs to {owner} (${total})",
            "{owner} leads the {pos} market by a clear margin (${total})",
            "Nobody close at {pos}: {owner} at ${total}",
        };

        private static readonly string[] DrySpellVariants =
        {
            "{owner} cold streak: ${cap} on the table, no signings in {days} days",
            "Quiet wallet alert — {owner} sitting on ${cap}, hasn't signed in {days} days",
            "{days} days, zero signings: {owner} still holding ${cap}",
            "{owner} on ice — ${cap} unspent, {days} days dry",
            "Waiting game stretches on: {owner} at ${cap}, {days} days since last move",
            "{owner} drought enters day {days} — ${cap} still available",
        };

        private static readonly string[] MostActiveVariants =
        {
            "{owner} the most active suitor — {n} negotiations in",
            "Setting the pace: {owner} with {n} offers, most in the league",
            "{owner} in on everything — {n} players negotiated",
            "{owner} driving the market — {n} offers and counting",
            "Busiest team on the market: {owner} ({n} negotiations)",
            "{owner} relentless — {n} offers extended so far",
            "{owner} casting a wide net: {n} players pursued",
            "{owner} chasing everyone — {n} active negotiations",
            "{owner} the league's busiest GM with {n} offers out",
        };
    }
}
