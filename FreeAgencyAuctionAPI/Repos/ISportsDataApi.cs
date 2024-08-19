using FreeAgencyAuctionAPI.Models;
using RestEase;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace FreeAgencyAuctionAPI.Repos
{

    public interface ISportsDataApi
    {


        [Get("nfl/scores/json/Standings/{year}")]
        Task <IEnumerable<SportsDataTeamStanding>> GetNflStandingsByYear([Path] int year, [Query] string key);

    }
}
