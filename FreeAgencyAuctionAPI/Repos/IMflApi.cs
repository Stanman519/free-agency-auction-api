using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Services;
using RestEase;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IMflApi
    {
        [Header("cookie", "MFL_IS_COMMISH=REDACTED_MFL_COMMISH%3D%3D;MFL_USER_ID=REDACTED_MFL_USER_ID%3D")]
        [Post("{year}/import?TYPE=salaries&L=13894&APPEND=1")]
        Task<HttpResponseMessage> AdjustPlayerSalary([Body(BodySerializationMethod.UrlEncoded)] Dictionary<string, string> data, [Path] string year = Utils.ThisYear);
        
        [Header("cookie", "MFL_IS_COMMISH=REDACTED_MFL_COMMISH%3D%3D;MFL_USER_ID=REDACTED_MFL_USER_ID%3D")]
        [Get("{year}/export?TYPE=league&L=13894&APIKEY=&JSON=1")]
        Task<LeagueRoot> GetBigLeagueObject([Path] string year = Utils.ThisYear);

        [Header("cookie", "MFL_IS_COMMISH=REDACTED_MFL_COMMISH%3D%3D;MFL_USER_ID=REDACTED_MFL_USER_ID%3D")]
        [Get("{year}/export?TYPE=rosters&L=13894&APIKEY=&FRANCHISE=&W=&JSON=1")]
        Task<RostersRoot> GetMflRostersForPlayerSalaries([Path] string year = Utils.ThisYear);
        
        [Header("cookie", "MFL_IS_COMMISH=REDACTED_MFL_COMMISH%3D%3D;MFL_USER_ID=REDACTED_MFL_USER_ID%3D")]
        [Get("{year}/export?TYPE=salaryAdjustments&L=13894&APIKEY=&JSON=1")]
        Task<SalaryAdjustmentsRoot> GetMflSalaryAdjustments([Path] string year = Utils.ThisYear);

        [Get("{year}/export?TYPE=freeAgents&L=13894&APIKEY=&POSITION=&JSON=1")]
        Task<FreeAgentsRoot> GetMflFreeAgents([Path] string year = Utils.ThisYear);

        [Get("{year}/export?TYPE=players&L=13894&APIKEY=&DETAILS=1&SINCE=&PLAYERS={ids}&JSON=1")]
        Task<MflPlayerDetailsRoot> GetMflPlayerDetails([Path] string ids, [Path] string year = Utils.ThisYear);
        
        [Get("2021/export?TYPE=playerScores&L=13894&APIKEY=REDACTED_MFL_API_KEY&W=YTD&YEAR={year}&PLAYERS=&POSITION={position}&STATUS=&RULES=1&COUNT=&JSON=1")]
        Task<MflPositionRanks> GetMflPositionScoresByYear([Path] int year, [Path] string position);
    }
}
