using System.Collections.Generic;
using FreeAgencyAuctionAPI.Models;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Models
{
    public class TradeRequestNormalizeCapEatsTests
    {
        private static TradeOfferAsset Asset(int eater, int receiver) => new()
        {
            MflId = "1",
            CapEats = new List<CapEat> { new() { EaterId = eater, ReceiverId = receiver, Amount = 33, Year = 2026, MflId = 1 } }
        };

        [Fact]
        public void StaleZeroReceiver_IsFixedFromTrade()
        {
            var t = new TradeRequest
            {
                SenderId = 10, ReceiverId = 2,
                SendingAssets = new List<TradeOfferAsset> { Asset(10, 0) },
                ReceivingAssets = new List<TradeOfferAsset> { Asset(2, 10) }
            };

            t.NormalizeCapEats();

            Assert.Equal(10, t.SendingAssets[0].CapEats[0].EaterId);
            Assert.Equal(2, t.SendingAssets[0].CapEats[0].ReceiverId);
            Assert.Equal(2, t.ReceivingAssets[0].CapEats[0].EaterId);
            Assert.Equal(10, t.ReceivingAssets[0].CapEats[0].ReceiverId);
        }

        [Fact]
        public void WrongEater_IsOverwritten_AmountKept()
        {
            var t = new TradeRequest
            {
                SenderId = 10, ReceiverId = 2,
                SendingAssets = new List<TradeOfferAsset> { Asset(99, 99) },
                ReceivingAssets = new List<TradeOfferAsset>()
            };

            t.NormalizeCapEats();

            Assert.Equal(10, t.SendingAssets[0].CapEats[0].EaterId);
            Assert.Equal(33, t.SendingAssets[0].CapEats[0].Amount);
        }

        [Fact]
        public void NullAssetLists_DoNotThrow()
        {
            new TradeRequest { SenderId = 1, ReceiverId = 2 }.NormalizeCapEats();
        }
    }
}
