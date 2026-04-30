using System;
using System.ComponentModel.DataAnnotations;

namespace FreeAgencyAuctionAPI.Models
{
    public partial class HeadlineEntity
    {
        [Key]
        public int Headlineid { get; set; }
        public int Leagueid { get; set; }
        public string ReferenceKind { get; set; } = null!;
        public int ReferenceId { get; set; }
        public string Text { get; set; } = null!;
        public string Tags { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public static class HeadlineRefKind
    {
        public const string Player = "Player";
        public const string Owner = "Owner";
    }
}
