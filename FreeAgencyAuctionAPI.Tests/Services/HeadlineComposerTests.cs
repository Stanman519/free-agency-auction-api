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
                Win = false, HandoffCount = 5,
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
                HandoffCount = 5,
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
    }
}
