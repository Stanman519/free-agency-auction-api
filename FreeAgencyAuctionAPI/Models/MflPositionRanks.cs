using System.Collections.Generic;
using Newtonsoft.Json;

namespace FreeAgencyAuctionAPI.Models
{
    public class PlayerScore
    {
        [JsonProperty("isAvailable")]
        public string IsAvailable { get; set; }

        [JsonProperty("week")]
        public string Week { get; set; }

        [JsonProperty("score")]
        public string Score { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class PlayerScores
    {
        [JsonProperty("week")]
        public string Week { get; set; }

        [JsonProperty("playerScore")]
        public List<PlayerScore> PlayerScore { get; set; }
    }

    public class MflPositionRanks
    {
        [JsonProperty("playerScores")]
        public PlayerScores PlayerScores { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("encoding")]
        public string Encoding { get; set; }
    }


}