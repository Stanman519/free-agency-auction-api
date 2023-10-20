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
        Task AddPlayerToTeam(int leaugeId, int playerId, int franchiseId);

        Task GiveNewContractToPlayer(int leagueId, int mflPlayerId, int salary);
        Task FreeDropTaxiPlayer(CutRequestBody request);
        Task BuyoutPlayer(CutRequestBody request);
        Task<List<PlayerDTO>> GetBuyoutCandidates(int leagueId, int leagueOwnerId, int mflFranchiseId);
        Task<List<PlayerDTO>> GetTaxiSquadPlayers(int leagueId, int leagueOwnerId, int mflFranchiseId);
        Task<List<LeagueOwnerEntity>> GetSalaryCapRoom(int leagueId);
        Task<List<MflPlayerDetails>> GetAllMflFreeAgents(int leagueId);
        Task<List<TagCandidate>> GetFranchiseTagCandidates(int leagueId, int leagueOwnerId, int mflFranchiseId);
        Task<PlayerBioDTO> GetMflPlayerBioDetails(int leagueId, int lastYear, string id, string firstName,
            string lastName, string position, bool hasAction);

        int? GetAgeInt(string birthdate);
        Task<LeagueOwnerDTO> GetTagAndTaxiInfos(int defaultLeagueId, LeagueOwnerDTO leagueOwner);
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

        public async Task AddPlayerToTeam(int leaugeId, int playerId, int franchiseId)
        {
            var strFrId = franchiseId.ToString("D4");

                try
                {
                    var resp = await _globalApi.AddPlayerToMflTeam(leaugeId, playerId, strFrId);
                    var respString = await resp.Content.ReadAsStringAsync();
                    if (respString.ToUpper().Contains("ERROR"))
                    {
                        var error = respString.XmlDeserializeFromString<MflXmlError>();
                        _logger.LogInformation(error.ErrorMsg);
                        _logger.LogError("${playerId} was not added to a team in mfl.", playerId);
                        await _gm.NotifyMflError(new ErrorMessage( $"{playerId} was not added to a team in mfl! \n\n{error.ErrorMsg}"));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
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


        public async Task GiveNewContractToPlayer(int leagueId, int mflPlayerId, int salary)
        {
            var data = CreateBodyDataForNewContract(mflPlayerId, salary);
            try
            {
                var resp = await _leagueApi.EditPlayerSalary(leagueId, data);
                var respString = await resp.Content.ReadAsStringAsync();
                if (respString.ToUpper().Contains("ERROR"))
                {
                    var error = respString.XmlDeserializeFromString<MflXmlError>();
                    _logger.LogInformation(respString);
                    _logger.LogError("{lastname}'s contract was not updated in mfl.", mflPlayerId);
                    await _gm.NotifyMflError(new ErrorMessage( $"league: {leagueId} player:{mflPlayerId} contract was not updated in mfl. \n\n${error.ErrorMsg}"));
                }
                else await _gm.SendBotNotification(message: new ErrorMessage($"Someone got franchise tagged but Ryan forgot to add player name here. Any way it was player {mflPlayerId} for ${salary}."));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "New Contract mfl");
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
        public async Task<List<PlayerDTO>> GetTaxiSquadPlayers(int leagueId, int leagueOwnerId, int mflFranchiseId)
        {
            var thisRosterRootTask = await _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.ThisYear);
            var myCurrentRoster = thisRosterRootTask.error == null ? thisRosterRootTask.rosters.franchise.FirstOrDefault(f => int.Parse(f.id) == mflFranchiseId).player : new List<Player>();
            var myTaxiPlayersNow = myCurrentRoster.Where(p => p.status == "TAXI_SQUAD");
            var queryIds = myTaxiPlayersNow.Select(p => int.Parse(p.id));
            var dbPlayers = await _pRepo.GetPlayersByListOfIds(queryIds) ?? new List<PlayerEntity>();

            return myTaxiPlayersNow.Join(dbPlayers, mfl => int.Parse(mfl.id), db => db.Mflid, (mfl, db) => new PlayerDTO
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

        }
        public async Task<List<TagCandidate>> GetFranchiseTagCandidates(int leagueId, int leagueOwnerId, int mflFranchiseId)
        {
            var lastRosterRootTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.ThisYear - 1);
            var thisRosterRootTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.ThisYear);
            await Task.WhenAll(lastRosterRootTask, thisRosterRootTask);


            var leagueTagData = await _pRepo.GetLeagueTagInfo(leagueId, Utils.ThisYear);
            var previousTags = _pRepo.GetTagsUsedForTeam(leagueOwnerId);

            var myCurrentRoster = thisRosterRootTask.Result.error == null ? thisRosterRootTask.Result.rosters.franchise.FirstOrDefault(f => int.Parse(f.id) == mflFranchiseId).player : new List<Player>();

            IEnumerable<Player> myExpiringPlayersLastYear = new List<Player>();

            var myTaxiPlayersNow = myCurrentRoster.Where(p => p.status == "TAXI_SQUAD");
            var cutCandidates = myCurrentRoster.Where(p => p.status != "TAXI_SQUAD");
            IEnumerable<int> queryIds = new List<int>();
            var tagCandidates = new List<TagCandidate>();

            if (previousTags.Count == 0)
            {
                myExpiringPlayersLastYear = lastRosterRootTask.Result.error == null ? lastRosterRootTask.Result.rosters.franchise.First(f => int.Parse(f.id) == mflFranchiseId).player.Where(p => p.contractYear == "1").ToList() : new List<Player>();
                queryIds = queryIds.Concat(myExpiringPlayersLastYear.Select(p => int.Parse(p.id)));
            }
            var dbPlayers = await _pRepo.GetPlayersByListOfIds(queryIds) ?? new List<PlayerEntity>();
            if (previousTags.Count == 0 && myExpiringPlayersLastYear.Count() > 0)
            {
                tagCandidates = myExpiringPlayersLastYear.Join(dbPlayers, mfl => int.Parse(mfl.id), db => db.Mflid, (mfl, db) => new TagCandidate
                {
                    Player = _mapper.Map<PlayerDTO>(db),
                    LastSeasonSalary = int.TryParse(mfl.salary, out var s) ? s : 0,
                    TagAmount = GetTagValueFromPosition(db.Position, leagueTagData)
                }).ToList();
            }
            return tagCandidates;
        }
        public async Task<List<PlayerDTO>> GetBuyoutCandidates(int leagueId, int leagueOwnerId, int mflFranchiseId)
        {
            var previousBuyouts = _pRepo.GetBuyoutsUsedForTeam(leagueOwnerId);
            var thisRosterRootTask = await _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.ThisYear);
            var myCurrentRoster = thisRosterRootTask.error == null ? thisRosterRootTask.rosters.franchise.FirstOrDefault(f => int.Parse(f.id) == mflFranchiseId).player : new List<Player>();

            var cutCandidates = myCurrentRoster.Where(p => p.status != "TAXI_SQUAD").ToList();
            var queryIds = cutCandidates.Select(p => int.Parse(p.id));
            var dbPlayers = await _pRepo.GetPlayersByListOfIds(queryIds) ?? new List<PlayerEntity>();
            var fullCutCandidates = new List<PlayerDTO>();
            if (previousBuyouts.Count == 0)
            {
                fullCutCandidates = cutCandidates.Join(dbPlayers, mfl => int.Parse(mfl.id), db => db.Mflid, (mfl, db) => new PlayerDTO
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
            }
            return fullCutCandidates;
        }


        public async Task<LeagueOwnerDTO> GetTagAndTaxiInfos(int leagueId, LeagueOwnerDTO leagueOwner)
        {
            var retOwner = new LeagueOwnerDTO();
            retOwner.TagCandidates = new List<TagCandidate>();
            try
            {
                var lastRosterRootTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.ThisYear - 1);
                var thisRosterRootTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.ThisYear);
                await Task.WhenAll(lastRosterRootTask, thisRosterRootTask);

                var previousBuyouts = _pRepo.GetBuyoutsUsedForTeam(leagueOwner.Leagueownerid);
                var previousTags = _pRepo.GetTagsUsedForTeam(leagueOwner.Leagueownerid);

                var myCurrentRoster = thisRosterRootTask.Result.error == null ? thisRosterRootTask.Result.rosters.franchise.FirstOrDefault(f => int.Parse(f.id) == leagueOwner.Mflfranchiseid).player : new List<Player>();

                IEnumerable<Player> myExpiringPlayersLastYear = new List<Player>();

                var myTaxiPlayersNow = myCurrentRoster.Where(p => p.status == "TAXI_SQUAD");
                var cutCandidates = myCurrentRoster.Where(p => p.status != "TAXI_SQUAD");
                var queryIds = myTaxiPlayersNow.Select(p => int.Parse(p.id)).Concat(cutCandidates.Select(p => int.Parse(p.id)));


                if (previousTags.Count == 0)
                {
                    myExpiringPlayersLastYear = lastRosterRootTask.Result.error == null ? lastRosterRootTask.Result.rosters.franchise.First(f => int.Parse(f.id) == leagueOwner.Mflfranchiseid).player.Where(p => p.contractYear == "1").ToList() : new List<Player>();
                    queryIds = queryIds.Concat(myExpiringPlayersLastYear.Select(p => int.Parse(p.id)));
                }

                var dbPlayers = await _pRepo.GetPlayersByListOfIds(queryIds) ?? new List<PlayerEntity>();
                if (previousTags.Count == 0 && myExpiringPlayersLastYear.Count() > 0) 
                {
                    var leagueTagData = await _pRepo.GetLeagueTagInfo(leagueId, Utils.ThisYear);
                    retOwner.TagCandidates = myExpiringPlayersLastYear.Join(dbPlayers, mfl => int.Parse(mfl.id), db => db.Mflid, (mfl, db) => new TagCandidate
                        {
                            Player = _mapper.Map<PlayerDTO>(db),
                            LastSeasonSalary = int.TryParse(mfl.salary, out var s) ? s : 0,
                            TagAmount = GetTagValueFromPosition(db.Position, leagueTagData)
                        }).ToList();
                }
                
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
                if (previousBuyouts.Count == 0)
                {
                    retOwner.CutCandidates = cutCandidates.Join(dbPlayers, mfl => int.Parse(mfl.id), db => db.Mflid, (mfl, db) => new PlayerDTO
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
                }
                return retOwner;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "retrieval of last year tag players. bad franchise id?");
                return retOwner;
            }

        }
        public async Task FreeDropTaxiPlayer(CutRequestBody req)
        {
            var franchiseStr = req.mflFranchiseId.ToString("D4");
            try { 
                var dropRequest = await _leagueApi.DropPlayerFromTaxi(req.leagueId, req.player.MflId, franchiseStr);
                var respString = await dropRequest.Content.ReadAsStringAsync();
                if (respString.ToUpper().Contains("ERROR"))
                {
                    var error = respString.XmlDeserializeFromString<MflXmlError>();
                    _logger.LogInformation(respString);
                    _logger.LogError("{mflPlayerId}'s contract was not updated in mfl.", req.player.FullName);
                    await _gm.NotifyMflError(new ErrorMessage($"league: {req.leagueId} player: {req.player.FullName} could not be taxi cut."));
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "drop player mfl");
                return;
            }
            try
            {
                var data = CreateBodyDataForNewSalaryAdj(franchiseStr, -req.rebate, "TAXI_REBATE", req.player);
                var dropRequest = await _leagueApi.AddSalaryAdjustment(req.leagueId, data);
                var respString = await dropRequest.Content.ReadAsStringAsync();
                if (respString.ToUpper().Contains("ERROR"))
                {
                    var error = respString.XmlDeserializeFromString<MflXmlError>();
                    _logger.LogInformation(respString);
                    _logger.LogError("{mflPlayerId}'s taxi rebate salary adjustment was not added in mfl.", req.player.FullName);
                    await _gm.NotifyMflError(new ErrorMessage($"league: {req.leagueId} player: {req.player.FullName} could not properly be taxi cut."));
                }
                else await _gm.SendBotNotification(message: new ErrorMessage($"New taxi cut submitted on stanfan.net\n{req.player.FullName} was cut."));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Salary Adj mfl");
                return;
            }

        }

        public async Task BuyoutPlayer(CutRequestBody req)
        {
            var franchiseStr = req.mflFranchiseId.ToString("D4");
            try
            {
                var dropRequest = await _leagueApi.DropPlayer(req.leagueId, req.player.MflId, franchiseStr);
                var respString = await dropRequest.Content.ReadAsStringAsync();
                if (respString.ToUpper().Contains("ERROR"))
                {
                    var error = respString.XmlDeserializeFromString<MflXmlError>();
                    _logger.LogInformation(respString);
                    _logger.LogError("{mflPlayerId}'s contract was not updated in mfl.", req.player.FullName);
                    await _gm.NotifyMflError(new ErrorMessage($"league: {req.leagueId} player: {req.player.FullName} could not be bought out."));
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Drop player mfl");
                return;
            }
            try
            {
                var rebateData = CreateBodyDataForNewSalaryAdj(franchiseStr, -req.rebate, "BUYOUT_REBATE", req.player, req.player.Length ?? 1); //rebate for auto multi year dead cap
                var rebRequest = await _leagueApi.AddSalaryAdjustment(req.leagueId, rebateData);
                var rebRespString = await rebRequest.Content.ReadAsStringAsync();
                if (rebRespString.ToUpper().Contains("ERROR"))
                {
                    var error = rebRespString.XmlDeserializeFromString<MflXmlError>();
                    _logger.LogInformation(rebRespString);
                    _logger.LogError("{mflPlayerId}'s buyout rebate salary adjustment was not added in mfl.", req.player.FullName);
                    await _gm.NotifyMflError(new ErrorMessage($"league: {req.leagueId} player: {req.player.FullName} could not properly apply buyout rebate salary adjustment."));
                }
                var penaltyData = CreateBodyDataForNewSalaryAdj(franchiseStr, (req.rebate * 0.5), "BUYOUT_PENALTY", req.player, 1); //half penalty for first year only
                var penaltyRequest = await _leagueApi.AddSalaryAdjustment(req.leagueId, penaltyData);
                var penRespString = await penaltyRequest.Content.ReadAsStringAsync();
                if (penRespString.ToUpper().Contains("ERROR"))
                {
                    var error = penRespString.XmlDeserializeFromString<MflXmlError>();
                    _logger.LogInformation(penRespString);
                    _logger.LogError("{mflPlayerId}'s buyout penalty salary adjustment was not added in mfl.", req.player.FullName);
                    await _gm.NotifyMflError(new ErrorMessage($"league: {req.leagueId} player: {req.player.FullName} could not properly apply buyout penalty salary adjustment."));
                }
                await _pRepo.AddBuyoutPlayer(req);
                await _gm.SendBotNotification(message: new ErrorMessage($"New buyout submitted on stanfan.net\n{req.player.FullName} was cut."));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Salary Adj mfl");
                return;
            }
        }

        private int GetTagValueFromPosition(string position, FranchiseTagLeague l)
        {
             switch (position.ToUpper())
            {
                case "QB":
                    return l.QB;
                case "RB":
                    return l.RB;
                case "WR":
                    return l.WR;
                case "TE":
                    return l.TE;
                default: 
                    return 0;
            }

        }

        private Dictionary<string, string> CreateBodyDataForNewSalaryAdj(string franchiseId, double adjustmentAmount, string reason, PlayerDTO player, int length = 1)
        {
            var ret = new Dictionary<string, string>()
            {
                {
                    "DATA",
                    $"<?xml version='1.0' encoding='UTF-8' ?><salary_adjustments><salary_adjustment franchise_id=\"{franchiseId}\" amount=\"{adjustmentAmount}\" explanation=\"{reason} {player.LastName}, {player.FirstName} {player.Team} {player.Position} (Salary: ${player.Salary}, years left: {length})\"/></salary_adjustments>"
                }
            };
            return ret;
        }

        private Dictionary<string, string> CreateBodyDataForNewContract(int playerId, int salary, int length = 1)
        {
            var ret = new Dictionary<string, string>()
            {
                {
                    "DATA",
                    $"<?xml version='1.0' encoding='UTF-8' ?><salaries><leagueUnit unit=\"LEAGUE\"><player id=\"{playerId}\" salary=\"{salary}\" contractYear=\"{length}\"/></leagueUnit></salaries>"
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