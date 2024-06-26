namespace FreeAgencyAuctionAPI.Models
{
    public class LeagueDTO
    {
        public int LeagueId { get; set; }
        public string Name { get; set; }
        public string MflHash { get; set; }
        public string CommishCookie { get; set; }
        public int FirstYear { get; set; }
        public bool IsAuctioning { get; set; }
        public bool IsFranchiseTagSzn { get; set; }
        public bool IsTaxiCutSzn { get; set; }
        public bool IsBuyoutSzn { get; set; }
    }
}
