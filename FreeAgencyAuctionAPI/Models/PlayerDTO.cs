namespace FreeAgencyAuctionAPI.Models
{
    public class PlayerDTO
    {
        public string FirstName { set; get; }
        public string LastName { set; get; }
        public int? Salary { set; get; }
        public int? Length { set; get; }
        public string? OwnerName { set; get; }
        public string? Position { set; get; }
        public int? OwnerId { set; get; }
        public int EspnId { set; get; }
        public int PlayerId { set; get; }
        public int? ContractValue { set; get; }
    }
}