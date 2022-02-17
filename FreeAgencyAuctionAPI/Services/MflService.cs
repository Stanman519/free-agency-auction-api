using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IMflService
    {
        Task<string> AddPlayerToTeam(BidDTO bid);
        Task<string> GiveNewContractToPlayer(BidDTO bid);
        Task<List<int>> GetSalaryCapRoom();
        Task<List<MflPlayerDetails>> GetAllMflFreeAgents();
        Task<PlayerBioDTO> GetMflPlayerBioDetails(int lastYear, string id, string firstName, string lastName, string position);
        int? GetAgeInt(string birthdate);
    }
    public class MflService : IMflService
    {
        private readonly IGlobalMflApi _globalApi;
        private readonly IMflApi _leagueApi;
        private readonly IBingImageApi _bingApi;

        public MflService(IGlobalMflApi globalApi, IMflApi leagueApi, IBingImageApi bingApi)
        {
            _globalApi = globalApi;
            _leagueApi = leagueApi;
            _bingApi = bingApi;
        }
        
        public async Task<string> AddPlayerToTeam(BidDTO bid)
        {
            if (owners.TryGetValue(bid.Ownername, out var teamId))
            {
                try
                {
                   var resp = await _globalApi.AddPlayerToMflTeam(bid.Player.MflId, teamId);
                   var respString = await resp.Content.ReadAsStringAsync();
                   if (respString.Contains("error"))
                   {
                       return $"{bid.Player.FirstName} {bid.Player.LastName} was not added to a team in mfl.  ";
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

        public async Task<PlayerBioDTO> GetMflPlayerBioDetails(int lastYear, string id, string firstName, string lastName, string position)
        {
            
            var bioTask = _leagueApi.GetMflPlayerDetails(id + ",15237,15281"); // adding two dummy players so that the response will be array lol
            var actionShotTask = _bingApi.GetActionShotForPlayer(firstName, lastName);
            var salaryTask = _leagueApi.GetMflRostersForPlayerSalaries();
            var scoringTaskYrNeg1 = _leagueApi.GetMflPositionScoresByYear(lastYear, position);
            var scoringTaskYrNeg2 = _leagueApi.GetMflPositionScoresByYear(lastYear - 1, position);
            var scoringTaskYrNeg3 = _leagueApi.GetMflPositionScoresByYear(lastYear - 2, position);

            await Task.WhenAll(bioTask, actionShotTask, salaryTask, scoringTaskYrNeg1, scoringTaskYrNeg2,
                scoringTaskYrNeg3);
            var lastSeasonTeam =
                salaryTask.Result.rosters.franchise.FirstOrDefault(tm => tm.player.Exists(_ => _.id == id));
            var lastSeasonSalary = 0;
            if (lastSeasonTeam != null)
                lastSeasonSalary = int.Parse(lastSeasonTeam.player.First(_ => _.id == id).salary);



            var allScores = new List<List<PlayerScore>>
            { scoringTaskYrNeg1.Result.PlayerScores.PlayerScore, scoringTaskYrNeg2.Result.PlayerScores.PlayerScore, scoringTaskYrNeg3.Result.PlayerScores.PlayerScore };
            var bio = bioTask.Result.players.player.First(p => p.id == id);
            var playerBio = new PlayerBioDTO
            {
                MflId = id,
                Age = GetAgeInt(bio.birthdate),
                FirstName = firstName,
                LastName = lastName,
                DraftRound = bio.draft_round,
                DraftYear = bio.draft_year,
                DraftPick = bio.draft_pick,
                Height = Int32.Parse(bio.height),
                Weight = Int32.Parse(bio.weight),
                Position = bio.position,
                Team = bio.team,
                College = bio.college,
                ActionShot = actionShotTask.Result.Value.FirstOrDefault()?.ContentUrl,
                LastSeasonSalary = lastSeasonSalary,
                PrevOwner = lastSeasonTeam?.id == null ? "" : owners.First(_ => _.Value == lastSeasonTeam.id).Key,
                PositionRanks = allScores.Select((year, index) =>
                {
                    var foundIndex = year.FindIndex(p => p.Id == id);
                    return new MflBioPositionRank
                    {
                        Year = lastYear - index,
                        Points = foundIndex < 0 ? 0 : decimal.Parse(year[foundIndex].Score),
                        Rank = foundIndex < 0 ? null : foundIndex + 1
                    };
                }).ToList()
            };
            return playerBio;
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
                    return $"{bid.Player.FirstName} {bid.Player.LastName}'s contract was was not updated in mfl.  ";
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

        public async Task<List<MflPlayerDetails>> GetAllMflFreeAgents()
        {
            var freeAgentIds = (await _leagueApi.GetMflFreeAgents()).freeAgents.leagueUnit.player.Select(_ => _.id).ToList();
            
            var freeAgents1 = new List<string>();
            var freeAgents2 = new List<string>();

            // get names via other get call
            string queryParam1 = "";
            string queryParam2 = "";

            var midpoint = (int) Math.Floor(((decimal) freeAgentIds.Count) / 2);

            freeAgents1 = freeAgentIds.GetRange(0, midpoint);
            freeAgents2 = freeAgentIds.GetRange(midpoint, (freeAgentIds.Count) - midpoint);

            freeAgents1.ForEach(p => queryParam1 = $"{queryParam1}{p},");
            freeAgents2.ForEach(p => queryParam2 = $"{queryParam2}{p},");


            var playerDetails1Task =  await _leagueApi.GetMflPlayerDetails(queryParam1);
            var playerDetails2Task =  await _leagueApi.GetMflPlayerDetails(queryParam2);

            //await Task.WhenAll(playerDetails1Task, playerDetails2Task);
            
            var playerDetailsList = playerDetails1Task.players.player;
            playerDetailsList.AddRange(playerDetails2Task.players.player);
            
            playerDetailsList.ForEach(p =>
            {
                var nameArr = p.name.Split(",");
                p.first_name = nameArr[1].Remove(0,1);
                p.last_name = nameArr[0];
            });

            return playerDetailsList;
        }
        
        
        
        private Dictionary<string, string> CreateBodyData(BidDTO bid)
        {
            var ret = new Dictionary<string, string>()
            {
                {
                    "DATA",
                    $"<?xml version='1.0' encoding='UTF-8' ?><salaries><leagueUnit unit=\"LEAGUE\"><player id=\"{bid.Player.MflId}\" salary=\"{bid.BidSalary}\" contractYear=\"{bid.BidLength}\"/></leagueUnit></salaries>"
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
        public int? GetAgeInt(string birthdate) {
            return Convert.ToInt32(Math.Floor(
                (DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(Int32.Parse(birthdate))).TotalDays / 365));
        }
    }


    public class MflRosterResponse
    {
        [XmlElement("error")]
        public string Error { get; set; }
    }

}