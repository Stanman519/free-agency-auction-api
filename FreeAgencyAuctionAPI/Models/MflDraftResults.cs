using System.Collections.Generic;
using Newtonsoft.Json;

namespace FreeAgencyAuctionAPI.Models
{
    public class MflDraftPick
    {
        public string franchise { get; set; }
        public string timestamp { get; set; }
        public string comments { get; set; }
        public string player { get; set; }
        public string pick { get; set; }
        public string round { get; set; }
    }

    public class DraftUnit
    {
        public string unit { get; set; }
        public string round1DraftOrder { get; set; }
        [JsonConverter(typeof(SingleOrArrayConverter<MflDraftPick>))]
        public List<MflDraftPick> draftPick { get; set; }
    }

    public class DraftResults
    {
        public DraftUnit draftUnit { get; set; }
    }

    public class MflDraftResultsRoot
    {
        public DraftResults draftResults { get; set; }
        public string version { get; set; }
        public string encoding { get; set; }
    }
}
