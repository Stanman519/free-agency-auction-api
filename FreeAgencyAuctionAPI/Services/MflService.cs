using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Serialization;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IMflService
    {
        Task AddPlayerToTeam(BidDTO bid);
        Task GiveNewContractToPlayer(BidDTO bid);
        Task<List<LeagueOwnerEntity>> GetSalaryCapRoom(int leagueId);
        Task<List<MflPlayerDetails>> GetAllMflFreeAgents(int leagueId);

        Task<PlayerBioDTO> GetMflPlayerBioDetails(int leagueId, int lastYear, string id, string firstName,
            string lastName, string position, bool hasAction);

        int? GetAgeInt(string birthdate);
        Task<LeagueOwnerDTO> GetTagAndTaxiInfos(int defaultLeagueId, int franchiseId);
    }

    public class MflService : IMflService
    {
        private readonly IGlobalMflApi _globalApi;
        private readonly IMflApi _leagueApi;
        private readonly IBingImageApi _bingApi;
        private readonly ILogger<MflService> _logger;
        private readonly IGMBot _gm;
        private readonly IPlayerRepo _pRepo;
        private readonly IMapper _mapper;
        private readonly IOptionsSnapshot<AppConfig> _options;

        public MflService(IGlobalMflApi globalApi, IMflApi leagueApi, IBingImageApi bingApi, ILogger<MflService> logger, IGMBot gm, IPlayerRepo pRepo, IMapper mapper, IOptionsSnapshot<AppConfig> options)
        {
            _globalApi = globalApi;
            _leagueApi = leagueApi;
            _bingApi = bingApi;
            _logger = logger;
            _gm = gm;
            _pRepo = pRepo;
            _mapper = mapper;
            _options = options;
        }

        public async Task AddPlayerToTeam(BidDTO bid)
        {
            if (owners.TryGetValue(bid.Ownername, out var teamId))
            {
                try
                {
                    var resp = await _globalApi.AddPlayerToMflTeam(bid.LeagueId, bid.Player.MflId, teamId);
                    var respString = await resp.Content.ReadAsStringAsync();
                    if (respString.ToUpper().Contains("ERROR"))
                    {
                        var error = respString.XmlDeserializeFromString<MflXmlError>();
                        _logger.LogInformation(error.ErrorMsg);
                        _logger.LogError("${lastname} was not added to a team in mfl.", bid.Player.LastName);
                        await _gm.NotifyMflError(new ErrorMessage( $"{bid.Player.FirstName} {bid.Player.LastName} was not added to a team in mfl! \n\n{error.ErrorMsg}"));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        public async Task<PlayerBioDTO> GetMflPlayerBioDetails(int leagueId, int lastYear, string id, string firstName,
            string lastName, string position, bool hasAction)
        {
            var bioTask =
                _leagueApi.GetMflPlayerDetails(leagueId, id + ",15237,15281"); // adding two dummy players so that the response will be array lol
            //Check out other api to add custom json serializer so you dont have to do this.
            var actionShotTask = _bingApi.GetActionShotForPlayer(firstName, lastName);
            var salaryTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId);
            var apiKey = _options.Value.Mfl.MflApiKey;
            var scoringTaskYrNeg1 = _leagueApi.GetMflPositionScoresByYear(leagueId, lastYear, position, apiKey);
            var scoringTaskYrNeg2 = _leagueApi.GetMflPositionScoresByYear(leagueId, lastYear - 1, position, apiKey);
            var scoringTaskYrNeg3 = _leagueApi.GetMflPositionScoresByYear(leagueId, lastYear - 2, position, apiKey);

            var taskList = new List<Task>
            {
                bioTask, salaryTask, scoringTaskYrNeg1, scoringTaskYrNeg2,
                scoringTaskYrNeg3
            };
            if (!hasAction) taskList.Add(actionShotTask);

            await Task.WhenAll(taskList);
            var lastSeasonTeam =
                salaryTask.Result.rosters.franchise.FirstOrDefault(tm => tm.player.Exists(_ => _.id == id));
            var lastSeasonSalary = 0;
            if (lastSeasonTeam != null)
                lastSeasonSalary = int.Parse(lastSeasonTeam.player.First(_ => _.id == id).salary);


            var allScores = new List<List<PlayerScore>>
            {
                scoringTaskYrNeg1.Result.PlayerScores.PlayerScore, scoringTaskYrNeg2.Result.PlayerScores.PlayerScore,
                scoringTaskYrNeg3.Result.PlayerScores.PlayerScore
            };
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
                LastSeasonSalary = lastSeasonSalary,
                PrevOwner = lastSeasonTeam?.id == null ? "" : owners.First(_ => _.Value == lastSeasonTeam.id).Key,
                PositionRanks = allScores.Select((year, index) =>
                {
                    if (year == null) return null;
                    var foundIndex = year.FindIndex(p => p?.Id == id);
                    return new MflBioPositionRank
                    {
                        Year = lastYear - index,
                        Points = foundIndex < 0 ? 0 : decimal.Parse(year[foundIndex].Score),
                        Rank = foundIndex < 0 ? null : foundIndex + 1
                    };
                }).ToList()
            };
            if (!hasAction)
            {
                playerBio.ActionShot = actionShotTask.Result.Value.FirstOrDefault()?.ContentUrl;
                Console.WriteLine("azure call for bing");
                await _pRepo.SavePlayerActionShot(playerBio.MflId, playerBio.ActionShot);
            }
            return playerBio;
        }


        public async Task GiveNewContractToPlayer(BidDTO bid)
        {
            var data = CreateBodyData(bid);
            try
            {
                var resp = await _leagueApi.AdjustPlayerSalary(bid.LeagueId, data);
                var respString = await resp.Content.ReadAsStringAsync();
                if (respString.ToUpper().Contains("ERROR"))
                {
                    var error = respString.XmlDeserializeFromString<MflXmlError>();
                    _logger.LogInformation(respString);
                    _logger.LogError("{lastname}'s contract was not updated in mfl.", bid.Player.LastName);
                    //await _gm.NotifyMflError(new ErrorMessage( $"{bid.Player.FirstName} {bid.Player.LastName}'s contract was not updated in mfl. \n\n${error.ErrorMsg}"));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return;
            }
        }

        public async Task<List<LeagueOwnerEntity>> GetSalaryCapRoom(int leagueId)
        {
            var bigLeagueTask = _leagueApi.GetBigLeagueObject(leagueId);
            var salaryTask = _leagueApi.GetMflSalaryAdjustments(leagueId);
            var rostersTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId);
            await Task.WhenAll(bigLeagueTask, salaryTask, rostersTask);

            var bigLeagueObject = bigLeagueTask.Result.league.franchises.franchise;

            var salaryAdjustments = salaryTask.Result.salaryAdjustments.salaryAdjustment;

            var rosters = rostersTask.Result.rosters.franchise;
            var rosteredSalaryTotals = rosters.Select(f =>
                new
                {
                    Id = f.id,
                    rosterSalarySum = f.player.Sum(p =>
                    {
                        if (p.status == "ROSTER")
                            return double.Parse(p.salary);
                        return double.Parse(p.salary) * 0.2;
                    })
                }
            ).ToList();
            var eachTeamCapTotal = bigLeagueObject.Select(f =>
            {
                var capNumber = string.IsNullOrEmpty(f.salaryCapAmount) ? 500 : double.Parse(f.salaryCapAmount);
                return new
                {
                    Id = f.id,
                    SalaryCapAmount = capNumber
                };
            }).ToList();

            var reducedSalaryAdjustments = salaryAdjustments
                .GroupBy(adj => adj.franchise_id, adj => double.TryParse(adj.amount, out var amount) ? amount : 0,
                    (id, adjustments) => new
                    {
                        Id = id,
                        SalaryAdjustments = adjustments.Sum()
                    }).ToList();

            var preAdjustmentsCapSpace = eachTeamCapTotal.Join(rosteredSalaryTotals, tmCap => tmCap.Id,
                salaryTot => salaryTot.Id,
                (tm, sal) => new
                {
                    tm.Id,
                    CapSpace = tm.SalaryCapAmount - sal.rosterSalarySum
                });
            
            var finalCapSpace = preAdjustmentsCapSpace.Join(reducedSalaryAdjustments, cap => cap.Id, adj => adj.Id,
                (cap, adj) => new LeagueOwnerEntity()
                {
                    Mflfranchiseid = int.Parse(cap.Id),
                    Caproom = (int) Math.Floor(cap.CapSpace - adj.SalaryAdjustments)
                }).ToList();
            return finalCapSpace;
        }

        public async Task<List<MflPlayerDetails>> GetAllMflFreeAgents(int leagueId)
        {
            var freeAgentIds = (await _leagueApi.GetMflFreeAgents(leagueId)).freeAgents.leagueUnit.player.Select(_ => _.id)
                .ToList();

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


            var playerDetails1Task = await _leagueApi.GetMflPlayerDetails(leagueId, queryParam1);
            var playerDetails2Task = await _leagueApi.GetMflPlayerDetails(leagueId, queryParam2);

            //await Task.WhenAll(playerDetails1Task, playerDetails2Task);

            var playerDetailsList = playerDetails1Task.players.player;
            playerDetailsList.AddRange(playerDetails2Task.players.player);

            playerDetailsList.ForEach(p =>
            {
                var nameArr = p.name.Split(",");
                p.first_name = nameArr[1].Remove(0, 1);
                p.last_name = nameArr[0];
            });

            return playerDetailsList;
        }
        public async Task<LeagueOwnerDTO> GetTagAndTaxiInfos(int defaultLeagueId, int franchiseId)
        {
            var retOwner = new LeagueOwnerDTO();
            try
            {
                var lastRosterRootTask = _leagueApi.GetMflRostersForPlayerSalaries(defaultLeagueId, Utils.ThisYear - 1);
                var thisRosterRootTask = _leagueApi.GetMflRostersForPlayerSalaries(defaultLeagueId, Utils.ThisYear);
                await Task.WhenAll(lastRosterRootTask, thisRosterRootTask);
                var myExpiringPlayersLastYear = lastRosterRootTask.Result.rosters.franchise.First(f => int.Parse(f.id) == franchiseId).player.Where(p => p.contractYear == "1");
                var myTaxiPlayersNow = thisRosterRootTask.Result.rosters.franchise.First(f => int.Parse(f.id) == franchiseId).player.Where(p => p.status == "TAXI_SQUAD");
                var queryIds = myExpiringPlayersLastYear.Select(p => int.Parse(p.id)).Concat(myTaxiPlayersNow.Select(p => int.Parse(p.id)));
                var dbPlayers = await _pRepo.GetPlayersByListOfIds(queryIds);
                retOwner.TagCandidates = myExpiringPlayersLastYear.Join(dbPlayers, mfl => int.Parse(mfl.id), db => db.Mflid, (mfl, db) => new TagCandidate
                {
                    Player = _mapper.Map<PlayerDTO>(db),
                    LastSeasonSalary = int.TryParse(mfl.salary, out var s) ? s : 0

                }).ToList();
                retOwner.TaxiPlayers = myTaxiPlayersNow.Join(dbPlayers, mfl => int.Parse(mfl.id), db => db.Mflid, (mfl, db) => new PlayerDTO
                {
                    
                    ActionShot = db.Actionshot,
                    Age = db.Age,
                    FirstName = db.Firstname,
                    FullName = db.Fullname,
                    Headshot = db.Headshot,
                    LastName = db.Lastname,
                    Salary = int.TryParse(mfl.salary, out var s) ? s : 0,
                    MflId = db.Mflid,
                    Position = db.Position,
                    Team = db.Team,
                    Length = int.TryParse(mfl.contractYear, out var l) ? l : 0

                }).ToList();
                return retOwner;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "retrieval of last year tag players. bad franchise id?");
                return retOwner;
            }

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


        public Dictionary<string, string> owners = new Dictionary<string, string>()
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
            {"dkirsch16", "0012"}
        };

        public int? GetAgeInt(string birthdate)
        {
            try
            {
                return Convert.ToInt32(Math.Floor(
                    (DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(Int32.Parse(birthdate))).TotalDays / 365));
            }
            catch (Exception e)
            {
                return null;
            } 
        }
    }


    public class MflRosterResponse
    {
        [XmlElement("error")] public string Error { get; set; }
    }
}