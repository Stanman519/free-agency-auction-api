using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using Microsoft.AspNetCore.Http;
using RestEase;


namespace FreeAgencyAuctionAPI.Repos
{
    public interface IGlobalMflApi
    {
        [Header("cookie", "MFL_IS_COMMISH=REDACTED_MFL_COMMISH%3D%3D;MFL_USER_ID=REDACTED_MFL_USER_ID%3D")]
        [Get("2021/import?TYPE=fcfsWaiver&L=13894&ADD={playerId}&DROP=&FRANCHISE_ID={franchiseId}")]
        Task<HttpResponseMessage> AddPlayerToMflTeam([Path] string playerId, [Path] string franchiseId);
        
        // [Get("2021/export?TYPE=playerProfile&P={ids}&JSON=1")]
        // Task<MflPlayerProfilesRoot> GetPlayerDetails([Path] string ids);
    }
}
