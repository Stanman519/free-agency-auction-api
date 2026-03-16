using System.Collections.Generic;
using Newtonsoft.Json;

namespace FreeAgencyAuctionAPI.Models
{
    public class FutureDraftPicksRoot { public FutureDraftPicksData futureDraftPicks { get; set; } }
    public class FutureDraftPicksData
    {
        [JsonConverter(typeof(SingleOrArrayConverter<FutureDraftFranchise>))]
        public List<FutureDraftFranchise> franchise { get; set; }
    }
    public class FutureDraftFranchise
    {
        public string id { get; set; }
        [JsonConverter(typeof(SingleOrArrayConverter<FutureDraftPick>))]
        public List<FutureDraftPick> futureDraftPick { get; set; } = new();
    }
    public class FutureDraftPick
    {
        public string year { get; set; }
        public string round { get; set; }
        public string originalPickFor { get; set; }
        public string description { get; set; }
    }

    public class TradeBaitsParent { public TradeBaitsMulti tradeBaits { get; set; } }
    public class TradeBaitsMulti
    {
        [JsonConverter(typeof(SingleOrArrayConverter<TradeBait>))]
        public List<TradeBait> tradeBait { get; set; } = new();
    }
    public class TradeBait
    {
        public string timestamp { get; set; }
        public string franchise_id { get; set; }
        public string willGiveUp { get; set; }
        public string inExchangeFor { get; set; }
    }
    public class TradeBaitDTO
    {
        public string FranchiseId { get; set; }
        public string WillGiveUp { get; set; }
        public string InExchangeFor { get; set; }
    }
}
