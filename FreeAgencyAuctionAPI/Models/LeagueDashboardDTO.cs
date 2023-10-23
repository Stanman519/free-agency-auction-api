using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class LeagueDashboardDTO
    {
        public OwnerDTO Profile { get; set; }
        public List<TransactionDTO> LeagueTransactions { get; set; }
        public List<TeamDeadCapData> TeamDeadCaps { get; set; }
        public List<LeagueDTO> Leagues { get; set; }
    }
}
