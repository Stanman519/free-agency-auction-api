using System.Collections.Generic;
using FreeAgencyAuctionAPI.Services;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Services
{
    public class HeadlineComposerTests
    {
        [Fact]
        public void ComposePlayer_NoSignals_ReturnsNull()
        {
            var input = new PlayerHeadlineInput { RefId = 1, PlayerName = "Test" };
            var result = HeadlineComposer.ComposePlayer(input);
            Assert.Null(result);
        }

        [Fact]
        public void ComposePlayer_Win_TagsIncludeWin()
        {
            var input = new PlayerHeadlineInput
            {
                RefId = 100, PlayerName = "Justin Jefferson", TopBidderName = "Owner A",
                Salary = 30, Years = 3, Win = true,
            };
            var result = HeadlineComposer.ComposePlayer(input);
            Assert.NotNull(result);
            Assert.Contains("Win", result!.Tags);
            Assert.Contains("Justin Jefferson", result.Text);
            Assert.Contains("Owner A", result.Text);
        }

        [Fact]
        public void ComposePlayer_WinWithSagaAndWar_FoldsFlavor()
        {
            var input = new PlayerHeadlineInput
            {
                RefId = 200, PlayerName = "CMC", TopBidderName = "Owner Z",
                Win = true, HandoffCount = 6, SagaDays = 4, Salary = 80, Years = 2,
            };
            var result = HeadlineComposer.ComposePlayer(input);
            Assert.NotNull(result);
            Assert.Contains("Win", result!.Tags);
            Assert.Contains("BiddingWar", result.Tags);
            Assert.Contains("SagaLength", result.Tags);
            Assert.Contains("CMC", result.Text);
            Assert.True(result.Text.Length > 20, $"flavor should make text longer; got: {result.Text}");
        }

        [Fact]
        public void ComposePlayer_BiddingWarNoWin_UsesWarTemplate()
        {
            var input = new PlayerHeadlineInput
            {
                RefId = 300, PlayerName = "Saquon", TopBidderName = "Owner B",
                Win = false, HandoffCount = 5, Salary = 25,
                WarOpponents = new List<string> { "Owner A", "Owner B" },
            };
            var result = HeadlineComposer.ComposePlayer(input);
            Assert.NotNull(result);
            Assert.Contains("BiddingWar", result!.Tags);
            Assert.DoesNotContain("Win", result.Tags);
            Assert.Contains("Saquon", result.Text);
        }

        [Fact]
        public void ComposePlayer_DeterministicVariantOnIdenticalInput()
        {
            var make = () => new PlayerHeadlineInput
            {
                RefId = 42, PlayerName = "Player", TopBidderName = "Owner",
                Win = true, Salary = 20, Years = 3,
            };
            var a = HeadlineComposer.ComposePlayer(make());
            var b = HeadlineComposer.ComposePlayer(make());
            Assert.Equal(a!.Text, b!.Text);
            Assert.Equal(a.Tags, b.Tags);
        }

        [Fact]
        public void ComposePlayer_DifferentSignalsProduceDifferentTags()
        {
            var win = HeadlineComposer.ComposePlayer(new PlayerHeadlineInput { RefId = 1, PlayerName = "P", TopBidderName = "O", Win = true, Salary = 10, Years = 2 });
            var saga = HeadlineComposer.ComposePlayer(new PlayerHeadlineInput { RefId = 1, PlayerName = "P", TopBidderName = "O", SagaDays = 3 });
            Assert.NotNull(win);
            Assert.NotNull(saga);
            Assert.NotEqual(win!.Tags, saga!.Tags);
        }

        [Fact]
        public void ComposeOwner_NoSignals_ReturnsNull()
        {
            var result = HeadlineComposer.ComposeOwner(new OwnerHeadlineInput { RefId = 1, OwnerName = "X" });
            Assert.Null(result);
        }

        [Fact]
        public void ComposeOwner_JustSigned_RoomLeft_AppendsCapSuffix()
        {
            var result = HeadlineComposer.ComposeOwner(new OwnerHeadlineInput
            {
                RefId = 1, OwnerName = "Bob",
                JustSignedPlayer = "Tyreek Hill", JustSignedSalary = 50, JustSignedYears = 3,
                IsRoomLeft = true, CapRoom = 200,
            });
            Assert.NotNull(result);
            Assert.Contains("JustSigned", result!.Tags);
            Assert.Contains("RoomLeft", result.Tags);
            Assert.Contains("$200", result.Text);
        }

        [Fact]
        public void ComposePlayer_Cut_UsesCutTemplate()
        {
            var input = new PlayerHeadlineInput
            {
                RefId = 555, PlayerName = "Najee Harris", Position = "RB",
                Cut = true, CutBy = "Owner Z",
            };
            var result = HeadlineComposer.ComposePlayer(input);
            Assert.NotNull(result);
            Assert.Contains("Cut", result!.Tags);
            Assert.Contains("Najee Harris", result.Text);
        }

        [Fact]
        public void ComposePlayer_CutWithBoardSignal_PrefersBoardSignal()
        {
            // Player on the board (BiddingWar) should win over Cut signal.
            var input = new PlayerHeadlineInput
            {
                RefId = 777, PlayerName = "Player X", TopBidderName = "Owner A",
                Cut = true, CutBy = "Owner B",
                HandoffCount = 5, Salary = 25,
                WarOpponents = new List<string> { "Owner A", "Owner B" },
            };
            var result = HeadlineComposer.ComposePlayer(input);
            Assert.NotNull(result);
            Assert.Contains("BiddingWar", result!.Tags);
            Assert.DoesNotContain("Cut", result.Tags);
        }

        [Fact]
        public void ComposeOwner_BigSpend_IncludesDominantPos()
        {
            var result = HeadlineComposer.ComposeOwner(new OwnerHeadlineInput
            {
                RefId = 5, OwnerName = "Alice",
                IsBigSpend = true, TotalSpend = 250, DominantPosition = "RB",
            });
            Assert.NotNull(result);
            Assert.Contains("BigSpend", result!.Tags);
            Assert.Contains("Alice", result.Text);
            Assert.Contains("RB", result.Text);
        }

        [Fact]
        public void ComposeOwner_MaxCapRoom_ProducesHeadlineWithOwnerAndCap()
        {
            var result = HeadlineComposer.ComposeOwner(new OwnerHeadlineInput
            {
                RefId = 10, OwnerName = "Rich Guy",
                IsMaxCapRoom = true, CapRoom = 350,
            });
            Assert.NotNull(result);
            Assert.Contains("MaxCapRoom", result!.Tags);
            Assert.Contains("Rich Guy", result.Text);
            Assert.Contains("350", result.Text);
        }

        [Fact]
        public void ComposeOwner_JustSigned_TakesPriorityOverMaxCapRoom()
        {
            var result = HeadlineComposer.ComposeOwner(new OwnerHeadlineInput
            {
                RefId = 11, OwnerName = "Bob",
                JustSignedPlayer = "Tyreek Hill", JustSignedSalary = 40, JustSignedYears = 2,
                IsMaxCapRoom = true, CapRoom = 300,
            });
            Assert.NotNull(result);
            Assert.Contains("JustSigned", result!.Tags);
            Assert.Contains("MaxCapRoom", result.Tags);
            Assert.Contains("Tyreek Hill", result.Text);
        }

        [Fact]
        public void ComposeOwner_TopNegotiator_ProducesHeadlineWithOwnerAndBidCount()
        {
            var result = HeadlineComposer.ComposeOwner(new OwnerHeadlineInput
            {
                RefId = 12, OwnerName = "Active Andy",
                IsTopNegotiator = true, TopNegotiatorBidCount = 15,
            });
            Assert.NotNull(result);
            Assert.Contains("MostActive", result!.Tags);
            Assert.Contains("Active Andy", result.Text);
            Assert.Contains("15", result.Text);
        }

        [Fact]
        public void ComposePlayer_LowSalaryBiddingWar_DoesNotTag()
        {
            // BiddingWar requires salary >= $5 — scrub-level players don't get headlines.
            var input = new PlayerHeadlineInput
            {
                RefId = 1, PlayerName = "Scrub", TopBidderName = "Owner",
                HandoffCount = 6, DistinctBidders = 5, Salary = 2,
                WarOpponents = new List<string> { "A", "B" },
            };
            var result = HeadlineComposer.ComposePlayer(input);
            Assert.Null(result);
        }

        [Fact]
        public void ComposePlayer_WideInterestRequiresFourBidders()
        {
            var threeBidders = HeadlineComposer.ComposePlayer(new PlayerHeadlineInput
            {
                RefId = 1, PlayerName = "P", TopBidderName = "O", Salary = 20,
                DistinctBidders = 3,
            });
            Assert.Null(threeBidders);

            var fourBidders = HeadlineComposer.ComposePlayer(new PlayerHeadlineInput
            {
                RefId = 1, PlayerName = "P", TopBidderName = "O", Salary = 20,
                DistinctBidders = 4,
            });
            Assert.NotNull(fourBidders);
            Assert.Contains("WideInterest", fourBidders!.Tags);
        }

        [Fact]
        public void ComposePlayer_BidderSetHashAppendedToWideInterestPrimaryTag()
        {
            var input1 = new PlayerHeadlineInput
            {
                RefId = 10, PlayerName = "P", TopBidderName = "O", Salary = 20,
                DistinctBidders = 4, BidderSetKey = "1|2|3|4",
            };
            var input2 = new PlayerHeadlineInput
            {
                RefId = 10, PlayerName = "P", TopBidderName = "O", Salary = 20,
                DistinctBidders = 4, BidderSetKey = "5|6|7|8",
            };
            var r1 = HeadlineComposer.ComposePlayer(input1);
            var r2 = HeadlineComposer.ComposePlayer(input2);
            Assert.NotNull(r1); Assert.NotNull(r2);
            Assert.StartsWith("WideInterest:h", r1!.Tags);
            Assert.StartsWith("WideInterest:h", r2!.Tags);
            Assert.NotEqual(r1.Tags, r2.Tags);
        }

        [Fact]
        public void ComposePlayer_WinPathDoesNotHashBidderSet()
        {
            var result = HeadlineComposer.ComposePlayer(new PlayerHeadlineInput
            {
                RefId = 10, PlayerName = "P", TopBidderName = "O", Salary = 30, Years = 3,
                Win = true, DistinctBidders = 5, BidderSetKey = "1|2|3|4|5",
            });
            Assert.NotNull(result);
            Assert.StartsWith("Win", result!.Tags);
            Assert.DoesNotContain(":h", result.Tags);
        }

        [Fact]
        public void ComposeOwner_FewestNegotiationsRemoved()
        {
            // FewestNegotiations is no longer a recognized signal.
            var result = HeadlineComposer.ComposeOwner(new OwnerHeadlineInput
            {
                RefId = 1, OwnerName = "Quiet",
                BidCount = 1,
            });
            Assert.Null(result);
        }

        [Fact]
        public void ComposeOwner_BigContract_PrimaryTagIncludesPlayerId()
        {
            var result = HeadlineComposer.ComposeOwner(new OwnerHeadlineInput
            {
                RefId = 1, OwnerName = "Spender",
                BigContractPlayer = "Tyreek Hill", BigContractSalary = 90, BigContractYears = 4,
            });
            Assert.NotNull(result);
            Assert.StartsWith("BigContract:Tyreek Hill", result!.Tags);
            Assert.Contains("Tyreek Hill", result.Text);
            Assert.Contains("90", result.Text);
        }

        [Fact]
        public void ComposeOwner_PositionRun_PrimaryTagIncludesPos()
        {
            var result = HeadlineComposer.ComposeOwner(new OwnerHeadlineInput
            {
                RefId = 1, OwnerName = "Stocker",
                PositionRunPosition = "WR",
            });
            Assert.NotNull(result);
            Assert.StartsWith("PositionRun:WR", result!.Tags);
            Assert.Contains("WR", result.Text);
        }

        [Fact]
        public void ComposeOwner_PositionalLeader_PrimaryTagIncludesPos()
        {
            var result = HeadlineComposer.ComposeOwner(new OwnerHeadlineInput
            {
                RefId = 1, OwnerName = "Top",
                PositionalLeaderPosition = "TE", TotalSpend = 120,
            });
            Assert.NotNull(result);
            Assert.StartsWith("PositionalLeader:TE", result!.Tags);
            Assert.Contains("TE", result.Text);
        }

        [Fact]
        public void ComposeOwner_DrySpell_HasSigned_ShowsDayCount()
        {
            var result = HeadlineComposer.ComposeOwner(new OwnerHeadlineInput
            {
                RefId = 1, OwnerName = "Cold",
                IsDrySpell = true, CapRoom = 180, DrySpellDays = 4, HasSignedThisAuction = true,
            });
            Assert.NotNull(result);
            Assert.StartsWith("DrySpell", result!.Tags);
            Assert.Contains("180", result.Text);
            Assert.Contains("4", result.Text);
        }

        [Fact]
        public void ComposeOwner_DrySpell_NeverSigned_QuietStartNoDayCount()
        {
            // Never-signed owner: "quiet start" wording, no bogus "day 0".
            var result = HeadlineComposer.ComposeOwner(new OwnerHeadlineInput
            {
                RefId = 1, OwnerName = "Quiet",
                IsDrySpell = true, CapRoom = 93, DrySpellDays = 0, HasSignedThisAuction = false,
            });
            Assert.NotNull(result);
            Assert.StartsWith("DrySpell", result!.Tags);
            Assert.Contains("93", result.Text);
            Assert.DoesNotContain("day 0", result.Text);
            Assert.DoesNotContain("0 days", result.Text);
        }

        [Fact]
        public void ComposePlayer_TopMoney_UsesTierAndOrdinalWording()
        {
            // Rank 3 at WR -> "WR1 money" tier label + "3rd" ordinal, no legacy "top-3 WR money".
            var input = new PlayerHeadlineInput
            {
                RefId = 1, PlayerName = "Star WR", TopBidderName = "Owner A", Position = "WR",
                Salary = 60, Years = 3, Win = true, TopMoneyRank = 3,
            };
            var result = HeadlineComposer.ComposePlayer(input);
            Assert.NotNull(result);
            Assert.Contains("TopMoney", result!.Tags);
            Assert.Contains("3rd", result.Text);          // true league rank as an ordinal
            Assert.Contains("WR", result.Text);           // position tier referenced
            Assert.DoesNotContain("top-3", result.Text);  // legacy "top-N pos money" wording gone
        }

        [Fact]
        public void ComposePlayer_TopMoney_RankBeyondThreeStillTags()
        {
            // Rank 8 (still a starter-tier qualifier decided upstream) must NOT be dropped.
            var input = new PlayerHeadlineInput
            {
                RefId = 2, PlayerName = "RB Guy", TopBidderName = "Owner B", Position = "RB",
                Salary = 30, Years = 2, Win = true, TopMoneyRank = 8,
            };
            var result = HeadlineComposer.ComposePlayer(input);
            Assert.NotNull(result);
            Assert.Contains("TopMoney", result!.Tags);
            Assert.Contains("8th", result.Text);
        }

        [Fact]
        public void ComposeOwner_JustSignedPreemptsBigContract()
        {
            var result = HeadlineComposer.ComposeOwner(new OwnerHeadlineInput
            {
                RefId = 1, OwnerName = "Bob",
                JustSignedPlayer = "X", JustSignedSalary = 10, JustSignedYears = 2,
                BigContractPlayer = "Y", BigContractSalary = 90, BigContractYears = 4,
            });
            Assert.NotNull(result);
            Assert.StartsWith("JustSigned", result!.Tags);
        }
    }
}
