using Newtonsoft.Json;

namespace FreeAgencyAuctionAPI.Models
{
    public class SportsDataTeamStanding
    {
   
        [JsonProperty("Conference")]
        public string Conference { get; set; }

        [JsonProperty("Division")]
        public string Division { get; set; }

        [JsonProperty("Team")]
        public string Team { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("Wins")]
        public int Wins { get; set; }

        [JsonProperty("Losses")]
        public int Losses { get; set; }

        [JsonProperty("Ties")]
        public int Ties { get; set; }

        [JsonProperty("Percentage")]
        public double Percentage { get; set; }

        [JsonProperty("PointsFor")]
        public int PointsFor { get; set; }

        [JsonProperty("PointsAgainst")]
        public int PointsAgainst { get; set; }

        [JsonProperty("NetPoints")]
        public int NetPoints { get; set; }

        [JsonProperty("Touchdowns")]
        public int Touchdowns { get; set; }

        [JsonProperty("DivisionWins")]
        public int DivisionWins { get; set; }

        [JsonProperty("DivisionLosses")]
        public int DivisionLosses { get; set; }

        [JsonProperty("TeamID")]
        public int TeamID { get; set; }

        [JsonProperty("GlobalTeamID")]
        public int GlobalTeamID { get; set; }

    }

}
