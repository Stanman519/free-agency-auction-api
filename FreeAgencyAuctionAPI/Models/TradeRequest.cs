using System;
using System.Collections.Generic;

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
        public string DisplayName { get; set; } // just for comment purposes
        public List<CapEat> CapEats { get; set; }
    }
}
