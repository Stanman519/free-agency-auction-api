using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class MflPlayerDetails
    {
        public string draft_year { get; set; }
        public string draft_round { get; set; }
        public string position { get; set; }
        public string weight { get; set; }
        public string id { get; set; }
        public string draft_team { get; set; }
        public string birthdate { get; set; }
        public string name { get; set; }
        public string draft_pick { get; set; }
        public string college { get; set; }
        public string height { get; set; }
        public string jersey { get; set; }
        public string team { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
    }

    public class MflPlayerDetailsParent
    {
        public string timestamp { get; set; }
        public string since { get; set; }
        public List<MflPlayerDetails> player { get; set; }
    }

    public class MflPlayerDetailsRoot
    {
        public string version { get; set; }
        public MflPlayerDetailsParent players { get; set; }
        public string encoding { get; set; }
    }

}