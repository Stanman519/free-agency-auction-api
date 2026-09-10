using FreeAgencyAuctionAPI.Models;
using RestEase;
using System.Threading.Tasks;

namespace FreeAgencyAuctionAPI.Repos
{
    // Free, unofficial, no API key required.
    // ESPN's WAF 403s requests without a curl-style User-Agent (blocks empty/browser UAs) — confirmed via testing.
    [Header("User-Agent", "curl/8.5.0")]
    public interface IEspnApi
    {
        [Get("standings")]
        Task<EspnStandingsResponse> GetNflStandingsByYear([Query] int season);
    }
}
