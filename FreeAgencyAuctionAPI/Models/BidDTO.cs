using System;
using Newtonsoft.Json;

namespace FreeAgencyAuctionAPI.Models
{
    public class BidDTO
    {
        [JsonProperty("bidId")]
        public int BidId { set; get; }
        [JsonProperty("bidLength")]
        public int BidLength { set; get; }
        [JsonProperty("bidSalary")]
        public int BidSalary { set; get; }
        [JsonProperty("ownername")]
        public string Ownername { set; get; }
        [JsonProperty("expires")]
        public DateTime Expires { set; get; }
        [JsonProperty("lotId")]
        public int? LotId { set; get; }
        [JsonProperty("ownerId")]
        public int OwnerId { get; set; }
        [JsonProperty("leagueId")]
        public int LeagueId { get; set; }
        [JsonProperty("player")]
        public PlayerDTO Player { get; set; }
    }
}