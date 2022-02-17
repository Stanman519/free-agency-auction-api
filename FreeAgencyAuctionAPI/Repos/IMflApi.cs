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

        [Get("2021/export?TYPE=freeAgents&L=13894&APIKEY=&POSITION=&JSON=1")]
        Task<FreeAgentsRoot> GetMflFreeAgents();

        [Get("2021/export?TYPE=players&L=13894&APIKEY=&DETAILS=1&SINCE=&PLAYERS={ids}&JSON=1")]
        Task<MflPlayerDetailsRoot> GetMflPlayerDetails([Path] string ids);
        
        [Get("2021/export?TYPE=playerScores&L=13894&APIKEY=REDACTED_MFL_API_KEY&W=YTD&YEAR={year}&PLAYERS=&POSITION={position}&STATUS=&RULES=1&COUNT=&JSON=1")]
        Task<MflPositionRanks> GetMflPositionScoresByYear([Path] int year, [Path] string position);
    }
}

//2021/export?TYPE=playerScores&L=13894&APIKEY=REDACTED_MFL_API_KEY&W=YTD&YEAR=2021&PLAYERS=&POSITION=RB&STATUS=&RULES=1&COUNT=&JSON=1