using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class DashboardTradeLeagueDTO
    {
        public List<DashboardTradeFranchiseDTO> Franchises { get; set; } = new List<DashboardTradeFranchiseDTO>();
        public string Name { get; set; }
    }
    public class DashboardTradeFranchiseDTO
    {
        public string icon { get; set; }
        public string abbrev { get; set; }
        public string name { get; set; }
        public string waiverSortOrder { get; set; }
        public string logo { get; set; }
        public string salaryCapAmount { get; set; }
        public string id { get; set; }
        public string stadium { get; set; }
        public string email { get; set; }
        public string username { get; set; }
        public string owner_name { get; set; }
        public int leagueId { get; set; } // doesn't come from API, my use only
        public DashboardTradeFranchiseAssetsDTO assets { get; set; }
    }
    public class DashboardTradeFranchiseAssetsDTO {
        public string Id { get; set; }
        public List<PlayerDTO> Players { get; set; } = new List<PlayerDTO>();
        public List<DraftPick> futureYearDraftPicks { get; set; } = new List<DraftPick>();
        public List<DraftPick> currentYearDraftPicks { get; set; } = new List<DraftPick>();
    }
}
