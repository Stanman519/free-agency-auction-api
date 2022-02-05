namespace FreeAgencyAuctionAPI.Models
{
    public class PlayerDTO
    {
        public string FirstName { set; get; }
        public string LastName { set; get; }
        public string? FullName { get; set; }
        public string? Team { get; set; }
        public int? Age { get; set; }
        public int? Height { get; set; }
        public int? Weight { get; set; }
        public string? Headshot { get; set; }
        public int? Salary { set; get; }
        public int? Length { set; get; }
        public string? OwnerName { set; get; }
        public string? Position { set; get; }
        public int? OwnerId { set; get; }
        public int MflId { set; get; }
        public int PlayerId { set; get; }
        public int? ContractValue { set; get; }
    }
}