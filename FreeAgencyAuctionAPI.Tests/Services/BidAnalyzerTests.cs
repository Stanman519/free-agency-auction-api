using System.Collections.Generic;
using FreeAgencyAuctionAPI.Services;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Services
{
    public class BidAnalyzerTests
    {
        private static HeadlineService.BidAnalyzer.BidRow Row(int id, int owner, int salary, string name = "")
            => new(id, owner, salary, name);

        [Fact]
        public void Analyze_Empty_ReturnsZero()
        {
            var r = HeadlineService.BidAnalyzer.Analyze(new List<HeadlineService.BidAnalyzer.BidRow>());
            Assert.Equal(0, r.HandoffCount);
            Assert.Equal(0, r.SeriousBidderCount);
            Assert.Empty(r.WarOpponentLastNames);
        }

        [Fact]
        public void Analyze_StaleLowBid_NotCountedAsSerious()
        {
            // top=$40, owner A only ever bid $1 → only B is serious
            var bids = new[]
            {
                Row(1, 100, 1, "Alice Smith"),
                Row(2, 200, 40, "Bob Jones"),
            };
            var r = HeadlineService.BidAnalyzer.Analyze(bids);
            Assert.Equal(1, r.SeriousBidderCount);
            Assert.Equal(new List<string> { "Jones" }, r.WarOpponentLastNames);
        }

        [Fact]
        public void Analyze_TwoCompetitiveBidders_SeriousCountTwo()
        {
            // top=$40, A($30 within $10 of top), B($40)
            var bids = new[]
            {
                Row(1, 100, 30, "Alice Smith"),
                Row(2, 200, 40, "Bob Jones"),
            };
            var r = HeadlineService.BidAnalyzer.Analyze(bids);
            Assert.Equal(2, r.SeriousBidderCount);
        }

        [Fact]
        public void Analyze_HandoffsBetweenSeriousOnly()
        {
            // Sequence: A($1), B($5), A($40), B($45), A($50)
            // top=$50, threshold = max(30, 40) = 40
            // Latest per owner: A=$50 (serious), B=$45 (serious)
            // But early A($1) and B($5) handoff should NOT count because those bids are not serious
            // (the serious filter is per-owner-latest; for handoffs we only count when both owners' LATEST are serious — both are here)
            // So we count handoffs in the sequence where current and prior owner are both ultimately serious.
            // Sequence owners: A,B,A,B,A → 4 handoffs total. All owners are serious by their latest bid.
            var bids = new[]
            {
                Row(1, 100, 1, "Alice Smith"),
                Row(2, 200, 5, "Bob Jones"),
                Row(3, 100, 40, "Alice Smith"),
                Row(4, 200, 45, "Bob Jones"),
                Row(5, 100, 50, "Alice Smith"),
            };
            var r = HeadlineService.BidAnalyzer.Analyze(bids);
            Assert.Equal(2, r.SeriousBidderCount);
            Assert.Equal(4, r.HandoffCount);
        }

        [Fact]
        public void Analyze_HandoffsSkipNonSeriousOwner()
        {
            // Sequence: A($30), C($1), A($35), B($40), A($45)
            // top=$45, threshold = max(27, 35) = 35
            // Latest per owner: A=$45 (serious), B=$40 (serious), C=$1 (NOT serious)
            // Walking owner sequence: A,C,A,B,A
            //   A→C: skip (C not serious)
            //   C→A: skip (C not serious)
            //   A→B: count (+1)
            //   B→A: count (+1)
            // Expected handoffs = 2
            var bids = new[]
            {
                Row(1, 100, 30, "Alice Smith"),
                Row(2, 300, 1, "Charlie Brown"),
                Row(3, 100, 35, "Alice Smith"),
                Row(4, 200, 40, "Bob Jones"),
                Row(5, 100, 45, "Alice Smith"),
            };
            var r = HeadlineService.BidAnalyzer.Analyze(bids);
            Assert.Equal(2, r.SeriousBidderCount);
            Assert.Equal(2, r.HandoffCount);
            Assert.DoesNotContain("Brown", r.WarOpponentLastNames);
        }

        [Fact]
        public void Analyze_CheapPlayer_UsesPercentThreshold()
        {
            // Bids monotonically increase by BidId in real auctions; last bid = top.
            // Sequence: A($1), C($3), B($5) → top=$5, threshold = max(3, -5) = 3
            // A($1) NOT serious; C($3) serious; B($5) serious
            var bids = new[]
            {
                Row(1, 100, 1, "A One"),
                Row(2, 300, 3, "C Three"),
                Row(3, 200, 5, "B Two"),
            };
            var r = HeadlineService.BidAnalyzer.Analyze(bids);
            Assert.Equal(2, r.SeriousBidderCount);
        }

        [Fact]
        public void Analyze_WarOpponents_AreLastNames_OrderedByRecency()
        {
            var bids = new[]
            {
                Row(1, 100, 40, "Alice Smith"),
                Row(2, 200, 42, "Bob Jones"),
                Row(3, 100, 45, "Alice Smith"),
            };
            var r = HeadlineService.BidAnalyzer.Analyze(bids);
            // Most-recent serious owner first
            Assert.Equal(new List<string> { "Smith", "Jones" }, r.WarOpponentLastNames);
        }

        [Fact]
        public void LastTokenOf_TwoWordName_ReturnsLast()
        {
            Assert.Equal("Smith", HeadlineService.BidAnalyzer.LastTokenOf("Alice Smith"));
        }

        [Fact]
        public void LastTokenOf_SingleWord_ReturnsAsIs()
        {
            Assert.Equal("stanmanley", HeadlineService.BidAnalyzer.LastTokenOf("stanmanley"));
        }

        [Fact]
        public void LastTokenOf_LeadingTrailingWhitespace_Trims()
        {
            Assert.Equal("Jones", HeadlineService.BidAnalyzer.LastTokenOf("  Bob Jones  "));
        }

        [Fact]
        public void LastTokenOf_Empty_ReturnsEmpty()
        {
            Assert.Equal("", HeadlineService.BidAnalyzer.LastTokenOf(""));
        }

        [Fact]
        public void LastTokenOf_ThreeWords_ReturnsLast()
        {
            Assert.Equal("Smith", HeadlineService.BidAnalyzer.LastTokenOf("Alice Mae Smith"));
        }
    }
}
