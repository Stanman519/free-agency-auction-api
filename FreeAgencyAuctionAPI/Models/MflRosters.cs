using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class Player
    {
        public string contractYear { get; set; }
        public string status { get; set; }
        public string id { get; set; }
        public string salary { get; set; }
    }

    public class FranchiseRoster
    {
        public string week { get; set; }
        public List<Player> player { get; set; }
        public string id { get; set; }
    }

    public class Rosters
    {
        public List<FranchiseRoster> franchise { get; set; }
    }

    public class RostersRoot
    {
        public Rosters rosters { get; set; }
        public string version { get; set; }
        public string encoding { get; set; }
    }


}