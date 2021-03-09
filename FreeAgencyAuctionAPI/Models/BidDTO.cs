using System;

namespace FreeAgencyAuctionAPI.Models
{
    public class BidDTO
    {
        public int BidId { set; get; }
        public int BidLength { set; get; }
        public int BidSalary { set; get; }
        public int PlayerId { set; get; }
        public string Bidder { set; get; }
        public DateTime Expires { set; get; }
        public int? LotId { get; set; }
    }
}