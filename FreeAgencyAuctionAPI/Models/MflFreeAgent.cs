using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class FreeAgentPlayer
    {
        public string contractYear { get; set; }
        public string status { get; set; }
        public string id { get; set; }
        public string salary { get; set; }
    }

    public class LeagueUnit
    {
        public string unit { get; set; }
        public List<FreeAgentPlayer> player { get; set; }
    }

    public class FreeAgents
    {
        public LeagueUnit leagueUnit { get; set; }
    }

    public class FreeAgentsRoot
    {
        public string error { get; set; }
        public FreeAgents freeAgents { get; set; }
        public string version { get; set; }
        public string encoding { get; set; }
    }


}