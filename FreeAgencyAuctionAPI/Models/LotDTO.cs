#nullable enable
namespace FreeAgencyAuctionAPI.Models
{
    public class LotDTO
    {
        public int LotId { get; set; }
        public BidDTO? Bid { get; set; }
    }
}