using System;

namespace FreeAgencyAuctionAPI.Models
{
    public class RecentMoveDTO
    {
        public DateTime Timestamp { get; set; }
        public int FranchiseId { get; set; }
        public string Action { get; set; } = ""; // "ADD" | "DROP"
        public int MflPlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public string Position { get; set; } = "";
        public string Team { get; set; } = "";
        public int? Salary { get; set; }
        public int? Years { get; set; }
    }
}
