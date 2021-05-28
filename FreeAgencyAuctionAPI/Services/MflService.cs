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
        Task<List<int>> GetSalaryCapRoom();
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

        public async Task<List<int>> GetSalaryCapRoom()
        {

            var bigLeagueObject = _leagueApi.GetBigLeagueObject().Result.league.franchises.franchise;
            var salaryAdjustments = _leagueApi.GetMflSalaryAdjustments().Result.salaryAdjustments.salaryAdjustment;
            var rosters = _leagueApi.GetMflRostersForPlayerSalaries().Result.rosters.franchise;
            var rosteredSalaryTotals = rosters.Select(f => f.player.Sum(p =>
            {
                if(p.status == "ROSTER")
                    return Int32.Parse(p.salary);
                return Int32.Parse(p.salary) * 0.2;
            })).ToList();
            var eachTeamCapTotalString = bigLeagueObject.Select(f => f.salaryCapAmount).ToList();
            var eachTeamCapTotal = eachTeamCapTotalString.Select(_ =>
            {
                if (string.IsNullOrEmpty(_))
                    return 500;
                return Int32.Parse(_);
            }).ToList();

            var reducedSalaryAdjustments = new List<decimal>
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0};
            salaryAdjustments.ForEach(a => reducedSalaryAdjustments[Int32.Parse(a.franchise_id) - 1] += decimal.Parse(a.amount));

            var capSpace = new List<int>
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0};
            
            for (int i = 0; i < 12; i++)
            {
                var subtotal = eachTeamCapTotal[i] - Convert.ToInt32(Math.Ceiling(rosteredSalaryTotals[i]));
                capSpace[i] = subtotal - Convert.ToInt32(Math.Ceiling(reducedSalaryAdjustments[i]));
            }

            return capSpace;
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
            {"turley69", "0005"},
            {"CrappieDuster", "0006"},
            {"cory", "0007"},
            {"jeremimattern", "0008"},
            {"Not a noob", "0009"},
            {"Flapjackcarl", "0010"},
            {"Juanard", "0011"},
            {"Tbux", "0012"}
        };
    }

    public class MflRosterResponse
    {
        [XmlElement("error")]
        public string Error { get; set; }
    }

}