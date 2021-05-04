using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using RestEase;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IGMBot
    {
        [Post("Bot/auctionError")]
        Task NotifyMflError([Body] string message);
    }
}