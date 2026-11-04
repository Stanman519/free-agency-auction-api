using System;
using System.Collections.Generic;
using System.Linq;

namespace FreeAgencyAuctionAPI.Models
{
    public class TradeRequest
    {
        public int SenderId { get; set; }
        public string SenderTeamName { get; set; } //just for comment purposes
        public string ReceiverTeamName { get; set; }
        public int ReceiverId { get; set; }
        public List<TradeOfferAsset> SendingAssets { get; set; }
        public List<TradeOfferAsset> ReceivingAssets { get; set; }
        public Guid CommentGuid { get; set; }
        public int LeagueId { get; set; }
        public long Expires { get; set; }
        public string TradeId { get; set; }

        // Client-sent eater/receiver ids are baked in per asset at check time and can go stale
        // (seen: receiverId 0 → rebate posted to franchise 0000). Sender's assets are eaten by
        // the sender, receiver's assets by the receiver, so derive both from the trade itself.
        public void NormalizeCapEats()
        {
            foreach (var cap in SendingAssets?.SelectMany(a => a.CapEats) ?? Enumerable.Empty<CapEat>())
            {
                cap.EaterId = SenderId;
                cap.ReceiverId = ReceiverId;
            }
            foreach (var cap in ReceivingAssets?.SelectMany(a => a.CapEats) ?? Enumerable.Empty<CapEat>())
            {
                cap.EaterId = ReceiverId;
                cap.ReceiverId = SenderId;
            }
        }
    }
    public class CapEat
    {
        public int EaterId { get; set; }
        public int ReceiverId { get; set; }
        public int Amount { get; set; }
        public int Year { get; set; }
        public int MflId { get; set; }
    }
    public class TradeOfferAsset {
        public string MflId { get; set; }
        public PlayerDTO PlayerDetails { get; set; }
        public List<CapEat> CapEats { get; set; } = new List<CapEat>();
    }
    


    public class PendingTradeResponse
    {
        public List<TradeRequest> tradeRequests { get; set; }
    }
}
