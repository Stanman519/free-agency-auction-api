using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Services;
using RestEase;

namespace FreeAgencyAuctionAPI.Repos
{
    [Header("User-Agent", "CapnCrunch")]
    public interface IMflApi
    {
        [Header("cookie")]
        public string cookie { get; set; }

        [Post("{year}/import?TYPE=salaries&L={leagueId}&APPEND=1")]
        Task<HttpResponseMessage> EditPlayerSalary([Path] int leagueId, [Body(BodySerializationMethod.UrlEncoded)] Dictionary<string, string> data, [Path] int year = Utils.ThisYear);

        [Post("{year}/import?TYPE=salaryAdj&L={leagueId}")]
        Task<HttpResponseMessage> AddSalaryAdjustment([Path] int leagueId, [Body(BodySerializationMethod.UrlEncoded)] Dictionary<string, string> data, [Path] int year = Utils.ThisYear);

        [Get("{year}/export?TYPE=league&L={leagueId}&APIKEY=&JSON=1")]
        Task<LeagueRoot> GetBigLeagueObject([Path] int leagueId, [Path] int year = Utils.ThisYear);

        [Get("{year}/export?TYPE=rosters&L={leagueId}&APIKEY=&FRANCHISE=&W=&JSON=1")]
        Task<RostersRoot> GetMflRostersForPlayerSalaries([Path] int leagueId, [Path] int year = Utils.ThisYear);
        
        [Get("{year}/export?TYPE=salaryAdjustments&L={leagueId}&APIKEY=&JSON=1")]
        Task<SalaryAdjustmentsRoot> GetMflSalaryAdjustments([Path] int leagueId, [Path] int year = Utils.ThisYear);

        [Get("{year}/export?TYPE=freeAgents&L={leagueId}&APIKEY=&POSITION=&JSON=1")]
        Task<FreeAgentsRoot> GetMflFreeAgents([Path] int leagueId, [Path] int year = Utils.ThisYear);

        [Get("{year}/export?TYPE=players&L={leagueId}&APIKEY=&DETAILS=1&SINCE=&PLAYERS={ids}&JSON=1")]
        Task<MflPlayerDetailsRoot> GetMflPlayerDetails([Path] int leagueId, [Path] string ids, [Path] int year = Utils.ThisYear);

        [Get("{year}/import?TYPE=fcfsWaiver&L={leagueId}&DROP={playerId}&FRANCHISE_ID={franchiseId}")]
        Task<HttpResponseMessage> DropPlayer([Path] int leagueId, [Path] int playerId, [Path] string franchiseId, [Path] int year = Utils.ThisYear);

        [Get("{year}/export?TYPE=playerScores&L={leagueId}&APIKEY={apiKey}&W=YTD&YEAR={year}&PLAYERS=&POSITION={position}&STATUS=&RULES=1&COUNT=&JSON=1")]
        Task<MflPositionRanks> GetMflPositionScoresByYear([Path] int leagueId, [Path] int year, [Path] string position, [Path] string apiKey);
        [Get("{year}/import?TYPE=taxi_squad&L={leagueId}&PROMOTE=&DEMOTE=&DROP={playerId}&FRANCHISE_ID={franchiseId}")]
        Task<HttpResponseMessage> DropPlayerFromTaxi([Path] int leagueId, [Path] int playerId, [Path] string franchiseId, [Path] int year = Utils.ThisYear);

        [Get("{year}/export?TYPE=transactions&L={leagueId}&APIKEY={apiKey}&W=&TRANS_TYPE=&FRANCHISE=&DAYS=&COUNT=&JSON=1")]
        Task<TransactionsRoot> GetLastYearWaiverTransactions([Path] int leagueId, [Path] string apiKey, [Path] int year = Utils.ThisYear - 1);
        [Get("{year}/import?TYPE=tradeProposal&L={leagueId}&OFFEREDTO={offeredTo}&WILL_GIVE_UP={willGiveUp}&WILL_RECEIVE={willReceive}&COMMENTS={comments}&EXPIRES={expires}&FRANCHISE_ID={offeringFranchise}")]
        Task<HttpResponseMessage> SendTradeOffer([Path] int year, [Path] int leagueId, [Path] string offeredTo, [Path] string willGiveUp, [Path] string willReceive, [Path] string comments, [Path] long expires, [Path] string offeringFranchise);
        [Get("{year}/export?TYPE=assets&L={leagueId}&APIKEY=&JSON=1")]
        Task<MflAssetsRoot> GetFranchiseAssets([Path] int leagueId, [Path] int year);
        [Get("{year}/export?TYPE=salaries&L={leagueId}&APIKEY=&JSON=1")]
        Task<MflSalariesParent> GetSalaries([Path] int leagueId, [Path] int year);

        [Get("{year}/export?TYPE=pendingTrades&L={leagueId}&APIKEY={ApiKey}&FRANCHISE_ID={franchiseNum}&JSON=1")]
        Task<MflPendingTradesListRoot> GetPendingTrades([Path] int leagueId, [Path] string franchiseNum, [Path] int year, [Path] string ApiKey);

    }
}
