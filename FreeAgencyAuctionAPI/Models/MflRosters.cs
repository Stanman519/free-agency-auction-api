using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class Player
    {
        public string contractYear { get; set; }
        public string status { get; set; }
        public string id { get; set; }
        public string salary { get; set; }
        // Only populated by MFL if the league's "Contract Status" salary-cap setting is
        // enabled — otherwise null even after a successful write. Used to verify a
        // contractStatus write actually landed rather than being silently dropped.
        public string contractStatus { get; set; }
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
        public string error { get; set; }
        public Rosters rosters { get; set; }
        public string version { get; set; }
        public string encoding { get; set; }
    }


}