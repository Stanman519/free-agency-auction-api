using Newtonsoft.Json;
using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class EspnStandingsResponse
    {
        [JsonProperty("children")]
        public List<EspnConference> Children { get; set; }
    }

    public class EspnConference
    {
        [JsonProperty("standings")]
        public EspnStandings Standings { get; set; }
    }

    public class EspnStandings
    {
        [JsonProperty("entries")]
        public List<EspnStandingEntry> Entries { get; set; }
    }

    public class EspnStandingEntry
    {
        [JsonProperty("team")]
        public EspnTeam Team { get; set; }

        [JsonProperty("stats")]
        public List<EspnStat> Stats { get; set; }
    }

    public class EspnTeam
    {
        [JsonProperty("abbreviation")]
        public string Abbreviation { get; set; }
    }

    public class EspnStat
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public double Value { get; set; }
    }
}
