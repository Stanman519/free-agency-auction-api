namespace FreeAgencyAuctionAPI.Models
{
    public class PlayerTipRequestDTO
    {
        public string MflId { get; set; }
        public int OwnerId { get; set; }
        public string Position { get; set; }
        public int Age { get; set; }
    }
}