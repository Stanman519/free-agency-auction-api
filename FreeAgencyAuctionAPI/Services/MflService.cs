using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml.Serialization;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using Microsoft.AspNetCore.Http;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IMflService
    {
        Task<string> AddPlayerToTeam(BidDTO bid);
        Task<string> GiveNewContractToPlayer(BidDTO bid);
    }
    public class MflService : IMflService
    {
        private readonly IGlobalMflApi _globalApi;
        private readonly IMflApi _leagueApi;
        
        public MflService(IGlobalMflApi globalApi, IMflApi leagueApi)
        {
            _globalApi = globalApi;
            _leagueApi = leagueApi;
        }
        
        public async Task<string> AddPlayerToTeam(BidDTO bid)
        {
            if (owners.TryGetValue(bid.Ownername, out var teamId))
            {
                try
                {
                   var resp = await _globalApi.AddPlayerToMflTeam(bid.PlayerId, teamId);
                   var respString = await resp.Content.ReadAsStringAsync();
                   if (respString.Contains("error"))
                   {
                       return $"{bid.PlayerFirstName} {bid.PlayerLastName} was not added to a team in mfl.  ";
                   }
                   return "";
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return null;
                }
            }
            return null;
        }

        public async Task<string> GiveNewContractToPlayer(BidDTO bid)
        {
            var data = CreateBodyData(bid);
            try
            {
                var resp =  await _leagueApi.AdjustPlayerSalary(data);
                var respString = await resp.Content.ReadAsStringAsync();
                if (respString.Contains("error"))
                {
                    return $"{bid.PlayerFirstName} {bid.PlayerLastName}'s contract was was not updated in mfl.  ";
                }
                return "";
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        private Dictionary<string, string> CreateBodyData(BidDTO bid)
        {
            var ret = new Dictionary<string, string>()
            {
                {
                    "DATA",
                    $"<?xml version='1.0' encoding='UTF-8' ?><salaries><leagueUnit unit=\"LEAGUE\"><player id=\"{bid.PlayerId}\" salary=\"{bid.BidSalary}\" contractYear=\"{bid.BidLength}\"/></leagueUnit></salaries>"
                }
            };
            return ret;
        }


        private Dictionary<string, string> owners = new Dictionary<string, string>()
        {
            {"Ryan", "0001"},
            {"tylerwelsh", "0002"},
            {"Leb", "0003"},
            {"caboroberts", "0004"},
            {"Taylor", "0005"},
            {"CrappieDuster", "0006"},
            {"Cory", "0007"},
            {"jeremimattern", "0008"},
            {"Not a noob", "0009"},
            {"Flapjackcarl", "0010"},
            {"Juan", "0011"},
            {"Tbux", "0012"}
        };
    }

    public class MflRosterResponse
    {
        [XmlElement("error")]
        public string Error { get; set; }
    }
}