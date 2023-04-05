using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class LoadData
    {
        public OwnerDTO profile { get; set; }
        public List<OpposingFranchiseDTO> owners { get; set; }
        public List<LotDTO> lots { get; set; }
        public List<PlayerDTO> freeAgents { get; set; }
    }
}