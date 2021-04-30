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
        [JsonProperty("playerId")]
        public int PlayerId { set; get; }
        [JsonProperty("ownername")]
        public string Ownername { set; get; }
        [JsonProperty("expires")]
        public string Expires { set; get; }
        [JsonProperty("lotId")]
        public int? LotId { set; get; }
        [JsonProperty("playerFirstName")]
        public string PlayerFirstName { set; get; }
        [JsonProperty("playerLastName")]
        public string PlayerLastName { set; get; }
    }
}