using System;
using System.ComponentModel.DataAnnotations;

namespace FreeAgencyAuctionAPI.Models
{
    public partial class OwnerQuoteEntity
    {
        [Key]
        public int Quoteid { get; set; }
        public int Leagueid { get; set; }
        public int Ownerid { get; set; }
        public int PlayerMflId { get; set; }
        public string Text { get; set; } = null!;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class OwnerQuoteDTO
    {
        public int QuoteId { get; set; }
        public int LeagueId { get; set; }
        public int OwnerId { get; set; }
        public string OwnerName { get; set; } = "";
        public int PlayerMflId { get; set; }
        public string Text { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class PostQuoteRequest
    {
        public int OwnerId { get; set; }
        public string Text { get; set; } = "";
    }
}
