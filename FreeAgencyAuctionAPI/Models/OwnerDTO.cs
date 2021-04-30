namespace FreeAgencyAuctionAPI.Models
{
    public class OwnerDTO
    {
        public int OwnerId { get; set; }
        public string Ownername { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public int CapRoom { get; set; }
        public int YearsLeft { get; set; }
    }
}