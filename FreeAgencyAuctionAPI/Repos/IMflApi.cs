using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using RestEase;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IMflApi
    {
        [Header("cookie", "MFL_IS_COMMISH=REDACTED_MFL_COMMISH%3D%3D;MFL_USER_ID=REDACTED_MFL_USER_ID%3D")]
        [Post("2021/import?TYPE=salaries&L=13894&APPEND=1")]
        Task<HttpResponseMessage> AdjustPlayerSalary([Body(BodySerializationMethod.UrlEncoded)] Dictionary<string, string> data);
        
        [Header("cookie", "MFL_IS_COMMISH=REDACTED_MFL_COMMISH%3D%3D;MFL_USER_ID=REDACTED_MFL_USER_ID%3D")]
        [Get("2021/export?TYPE=league&L=13894&APIKEY=&JSON=1")]
        Task<LeagueRoot> GetBigLeagueObject();

        [Header("cookie", "MFL_IS_COMMISH=REDACTED_MFL_COMMISH%3D%3D;MFL_USER_ID=REDACTED_MFL_USER_ID%3D")]
        [Get("2021/export?TYPE=rosters&L=13894&APIKEY=&FRANCHISE=&W=&JSON=1")]
        Task<RostersRoot> GetMflRostersForPlayerSalaries();
        
        [Header("cookie", "MFL_IS_COMMISH=REDACTED_MFL_COMMISH%3D%3D;MFL_USER_ID=REDACTED_MFL_USER_ID%3D")]
        [Get("2021/export?TYPE=salaryAdjustments&L=13894&APIKEY=&JSON=1")]
        Task<SalaryAdjustmentsRoot> GetMflSalaryAdjustments();
    }
}