using FreeAgencyAuctionAPI.Models;
using RestEase;
using System.Threading.Tasks;

namespace FreeAgencyAuctionAPI.Repos
{
    // Free, unofficial, no API key required.
    public interface IEspnApi
    {
        [Get("standings")]
        Task<EspnStandingsResponse> GetNflStandingsByYear([Query] int season);
    }
}
