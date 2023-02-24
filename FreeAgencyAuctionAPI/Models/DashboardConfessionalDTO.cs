using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class DashboardConfessionalDTO
    {
        public OwnerDTO Profile { get; set; }
        public List<TransactionDTO> LeagueTransactions { get; set; }
        public List<DeadCapData> LeagueDeadCap { get; set; }
        public List<LeagueDTO> Leagues { get; set; }
    }
}
