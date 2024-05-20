using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class Adp
    {
        public string timestamp { get; set; }
        public List<AdpPlayer> player { get; set; }
        public string totalDrafts { get; set; }
        public string totalPicks { get; set; }
    }

    public class AdpPlayer
    {
        public string id { get; set; }
        public string averagePick { get; set; }
        public string draftSelPct { get; set; }
        public string rank { get; set; }
        public string maxPick { get; set; }
        public string minPick { get; set; }
        public string draftsSelectedIn { get; set; }
    }

    public class AdpRoot
    {
        public Adp adp { get; set; }
        public string encoding { get; set; }
        public string version { get; set; }
    }
}
