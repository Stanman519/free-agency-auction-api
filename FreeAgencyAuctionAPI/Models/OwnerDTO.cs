using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class OwnerDTO
    {
        public int OwnerId { get; set; }
        public string Ownername { get; set; }
        public string Password { get; set; }
        public bool Premium { get; set; }
        public string DisplayName { get; set; }
        public string StreamToken { get; set; }
        public IEnumerable<LeagueOwnerDTO> Leagues { get; set; }

    }

    public class LeagueOwnerDTO 
    {
        public int CapRoom { get; set; }
        public int YearsLeft { get; set; }
        public int Mflfranchiseid { get; set; }
        public int Leagueownerid { get; set; }
        public LeagueDTO League { get; set; }
    }
}