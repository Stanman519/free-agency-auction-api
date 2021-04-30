using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using RestEase;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IMflApi
    {
        [Header("cookie", "MFL_IS_COMMISH=REDACTED_MFL_COMMISH%3D%3D;")]
        [Post("2021/import?TYPE=salaries&L=13894&APPEND=1")]
        Task<HttpResponseMessage> AdjustPlayerSalary([Body(BodySerializationMethod.UrlEncoded)] Dictionary<string, string> data);

    }
}