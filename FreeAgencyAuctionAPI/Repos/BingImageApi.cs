using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using RestEase;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IBingImageApi
    {
        //images/search?q=jonathan+taylor+nfl+game&mkt=en-us&safeSearch=moderate&count=1&offset=0
        [Header("Ocp-Apim-Subscription-Key")]
        public string BingKey { get; set; }
        [Get("images/search?q={firstName}+{lastName}+nfl&mkt=en-us&safeSearch=moderate&count=1&offset=0")]
        Task<ActionShotQueryResponse> GetActionShotForPlayer([Path] string firstName, [Path] string lastName);
    }
}