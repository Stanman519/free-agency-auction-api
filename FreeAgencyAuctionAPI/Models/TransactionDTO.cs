using System;
using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class TransactionDTO
    {
        public DateTime Timestamp { get; set; }
        public int TransactionId { set; get; }
        public int FranchiseId { get; set; }
        public int Salary { get; set; }
        public double Amount { get; set; }
        public string PlayerName { get; set; }
        public string Position { get; set; }
        public string Team { get; set; }
        public int Years { get; set; }
        public int YearOfTransaction { get; set; }
        public int LeagueId { get; set; }
    }

    public class MflTransaction
    {
        public string activated { get; set; }
        public string deactivated { get; set; }
        public string type { get; set; }
        public string franchise { get; set; }
        public string timestamp { get; set; }
        public string transaction { get; set; }
        public string franchise2_gave_up { get; set; }
        public string franchise1_gave_up { get; set; }
        public string franchise2 { get; set; }
        public string expires { get; set; }
        public string action { get; set; }
        public string original_timestamp { get; set; }
    }
    public class MflTransactions
    {
        public List<MflTransaction> transaction { get; set; }
    }
    public class TransactionsRoot
    {
        public string version { get; set; }
        public string encoding { get; set; }
        public MflTransactions transactions { get; set; }
    }
}