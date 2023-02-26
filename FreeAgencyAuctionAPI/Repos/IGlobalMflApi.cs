using System.Net.Http;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Services;
using RestEase;


namespace FreeAgencyAuctionAPI.Repos
{
    public interface IGlobalMflApi
    {
        [Header("cookie")]
        public string CommishCookie { get; set; }
        [Get("{year}/import?TYPE=fcfsWaiver&L={leagueId}&ADD={playerId}&DROP=&FRANCHISE_ID={franchiseId}")]
        Task<HttpResponseMessage> AddPlayerToMflTeam([Path] int leagueId, [Path] int playerId, [Path] string franchiseId, [Path] int year = Utils.ThisYear);
        
    }
}
