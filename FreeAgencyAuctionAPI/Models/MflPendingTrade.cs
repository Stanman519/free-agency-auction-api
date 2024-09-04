using System.Collections.Generic;
using Newtonsoft.Json;

namespace FreeAgencyAuctionAPI.Models
{
    public class MflPendingTradesListRoot
    {
        public string Version { get; set; }
        public MflPendingTradesListParent pendingTrades { get; set; }
        public string Encoding { get; set; }
    }
    public class MflPendingTradesListParent
    {
        [JsonConverter(typeof(SingleOrArrayConverter<MflPendingTrade>))]
        public List<MflPendingTrade> pendingTrade { get; set; } = new List<MflPendingTrade>();
    }
    public class MflPendingTrade
    {
        public string trade_id { get; set; }
        public string offeredTo { get; set; }
        public string offeringTeam { get; set; }
        public string comments { get; set; }
        public string description { get; set; }
        public string will_receive { get; set; }
        public string timestamp { get; set; }
        public string will_give_up { get; set; }
        public string expires { get; set; }
    }
}