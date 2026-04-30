using System;
using Newtonsoft.Json;

namespace FreeAgencyAuctionAPI.Models
{
    public class HeadlineDTO
    {
        [JsonProperty("headlineId")]
        public int HeadlineId { get; set; }
        [JsonProperty("leagueId")]
        public int LeagueId { get; set; }
        [JsonProperty("referenceKind")]
        public string ReferenceKind { get; set; } = null!;
        [JsonProperty("referenceId")]
        public int ReferenceId { get; set; }
        [JsonProperty("text")]
        public string Text { get; set; } = null!;
        [JsonProperty("tags")]
        public string Tags { get; set; } = string.Empty;
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }
        [JsonProperty("expiresAt")]
        public DateTime? ExpiresAt { get; set; }
    }
}
