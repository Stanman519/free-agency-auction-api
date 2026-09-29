using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Serialization;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestEase;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IMflService
    {
        Task AddPlayerToTeam(int leaugeId, int playerId, int franchiseId, string playerName = null);
        
        Task GiveNewContractToPlayer(int leagueId, int mflPlayerId, int salary, bool isFranchiseTag, string playerName, string contractStatus = null);
        Task GiveNewContractToPlayer(int leagueId, int mflPlayerId, int salary, int contractLength, string botMessage, string contractStatus = null, bool announceOnSuccess = true);
        Task FreeDropTaxiPlayer(CutRequestBody request);
        Task BuyoutPlayer(CutRequestBody request);
        Task<List<FranchiseRoster>> GetMflRosters(int leagueId);
        Task<List<PlayerDTO>> GetBuyoutCandidates(int leagueId, int leagueOwnerId, int mflFranchiseId);
        Task<List<PlayerDTO>> GetTaxiSquadPlayers(int leagueId, int leagueOwnerId, int mflFranchiseId);
        Task<List<LeagueOwnerEntity>> GetSalaryCapRoom(int leagueId);
        Task<List<PlayerDTO>> GetMflPlayersByIds(int leagueId, int year, string mflIds);
        Task<DashboardTradeLeagueDTO> GetMflLeagueRootAndAssets(int leagueId, int year, int franchiseId);
        Task<List<MflPlayerDetails>> GetAllMflFreeAgents(int leagueId);
        Task<List<TagCandidate>> GetFranchiseTagCandidates(int leagueId, int leagueOwnerId, int mflFranchiseId);
        Task<List<PlayerDTO>> GetWaiverExtensionCandidates(int leagueId, int leagueOwnerId, int mflFranchiseId);
        Task<List<FifthYearOptionCandidate>> GetFifthYearOptionCandidates(int leagueId, int leagueOwnerId, int mflFranchiseId);
        Task<PlayerBioDTO> GetMflPlayerBioDetails(int leagueId, int lastYear, string id, string firstName,
            string lastName, string position);
        Task<MflPlayerDetails> GetMflPlayerById(int leagueId, int mflId);
        int? GetAgeInt(string birthdate);
        Task<LeagueOwnerDTO> GetTagAndTaxiInfos(int defaultLeagueId, LeagueOwnerDTO leagueOwner);
        Task<PendingTradeResponse> GetMyPendingTrades(int leagueId, int mflFranchiseId);
        Task ProposeMflTrade(TradeRequest req);
        Task ResponseToMflTrade(int year, int leagueId, int tradeId, string response, string comments, string franchiseId);
        Task<List<PlayerEligibility>> GetHoldoutPlayers(int leagueId);
        Task<List<HoldoutDTO>> GenerateAndSaveHoldouts(int leagueId, int year);
        Task<FranchiseTagLeague> GenerateFranchiseTagValues(int leagueId, int year);
        Task<List<TradeBaitDTO>> GetTradeBaitForLeague(int leagueId);
        Task<Dictionary<string, List<FutureDraftPickDTO>>> GetFutureDraftPicksForLeague(int leagueId);
        Task<List<RecentMoveDTO>> GetRecentMoves(int leagueId, int limit = 20);
        Task<List<ContractStatusAuditEntry>> GetContractStatusAudit(int leagueId);
        Task<string> GetPlayerContractStatusFromMfl(int leagueId, int mflPlayerId);
        Task<List<PlayerSnapshotEntry>> GetLeagueRosterSnapshot(int leagueId);
        Task<List<PlayerSnapshotEntry>> GetLeagueRosterSnapshotForYear(int leagueId, int year);
        Task<object> GetDraftPickDiagnostics(int leagueId, int draftYear, int mflPlayerId);
        Task<object> GetRound1PicksDiagnostics(int leagueId, int draftYear);
        Task<Dictionary<string, int>> GetProjectedFifthYearOptionSalaries(int leagueId, List<FranchiseRoster> rosters);
    }

    public class MflService : IMflService
    {
        private readonly IGlobalMflApi _globalApi;
        private readonly IMflApi _leagueApi;
        private readonly ILogger<MflService> _logger;
        private readonly IGMBot _gm;
        private readonly IPlayerRepo _pRepo;
        private readonly IMapper _mapper;
        private readonly IOptionsSnapshot<AppConfig> _options;
        private readonly AuctionContext _db;

        public MflService(IGlobalMflApi globalApi, IMflApi leagueApi, ILogger<MflService> logger, IGMBot gm, IPlayerRepo pRepo, IMapper mapper, AuctionContext db, IOptionsSnapshot<AppConfig> options)
        {
            _globalApi = globalApi;
            _leagueApi = leagueApi;
            _logger = logger;
            _gm = gm;
            _pRepo = pRepo;
            _mapper = mapper;
            _options = options;
            _db = db;
        }

        private string GetApiKey(int leagueId) =>
            _options?.Value?.Mfl?.MflApiKey?.FirstOrDefault(k => k.id == leagueId)?.key ?? string.Empty;

        public async Task<MflPlayerDetails> GetMflPlayerById(int leagueId, int mflId)
        {
            var playerRes = await _leagueApi.GetMflPlayerDetails(leagueId, mflId.ToString(), Utils.CurrentYear, GetApiKey(leagueId));
            var player = playerRes.players.player.FirstOrDefault();
            if (player != null && string.IsNullOrWhiteSpace(player.first_name) && !string.IsNullOrWhiteSpace(player.name))
            {
                var nameArr = player.name.Split(",");
                if (nameArr.Length == 2)
                {
                    player.last_name = nameArr[0];
                    player.first_name = nameArr[1].TrimStart();
                }
            }
            return player;
        }
        public async Task<List<PlayerDTO>> GetMflPlayersByIds(int leagueId, int year, string mflIds)
        {
            var playerRes = await _leagueApi.GetMflPlayerDetails(leagueId, mflIds, year, GetApiKey(leagueId));

            return playerRes.players.player.Select(p => new PlayerDTO
            {
                Age = GetAgeInt(p.birthdate),
                FirstName = p.first_name,
                LastName = p.last_name,
                FullName = p.name,
                Team = p.team,
                Position = p.position,
                MflId = int.TryParse(p.id, out var x) ? x : 0
            }).ToList();

        }

        public async Task AddPlayerToTeam(int leaugeId, int playerId, int franchiseId, string playerName = null)
        {
            var botId = Utils.leagueBotDict.TryGetValue(leaugeId, out var x) ? x : string.Empty;
            var strFrId = franchiseId.ToString("D4");
            var displayName = playerName ?? playerId.ToString();

                try
                {
                    var resp = await _globalApi.AddPlayerToMflTeam(leaugeId, playerId, strFrId, Utils.CurrentYear);
                    var respString = await resp.Content.ReadAsStringAsync();
                    if (respString.ToUpper().Contains("ERROR"))
                    {
                        var error = respString.XmlDeserializeFromString<MflXmlError>();
                        _logger.LogInformation(error.ErrorMsg);
                        _logger.LogError("${playerName} ({playerId}) was not added to a team in mfl.", displayName, playerId);
                        await _gm.NotifyMflError(new BotMessage( $"{displayName} ({playerId}) was not added to a team in mfl! \n\n{error.ErrorMsg}", botId));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

        }

        public async Task<DashboardTradeLeagueDTO> GetMflLeagueRootAndAssets(int leagueId, int year, int franchiseId)
        {
            var bigRet = new DashboardTradeLeagueDTO();
            var bigLeagueTask = _leagueApi.GetBigLeagueObject(leagueId, year, GetApiKey(leagueId));
            var assetsTask = _leagueApi.GetFranchiseAssets(leagueId, year, GetApiKey(leagueId));
            // Use the rosters export, not the salaries export, for contract length/status.
            // The salaries export silently omits the contractYear attribute for players on
            // exactly a 1-year deal (confirmed empirically), which showed up as trades listing
            // otherwise-normal players as "0YR" — as if they'd be off the roster, which they
            // aren't. The rosters export (same source the roster page and GetMyPendingTrades
            // already use) doesn't have this gap.
            var rostersTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId, year, GetApiKey(leagueId));
            await Task.WhenAll(bigLeagueTask, assetsTask, rostersTask);
            var myPlayerIds = assetsTask.Result.assets.franchise
                .FirstOrDefault(f => f.id == franchiseId.ToString("0000"))
                .players.player
                .Select(p => p.id)
                .ToList();
            MflPlayerDetailsRoot myPlayers = new MflPlayerDetailsRoot();
            if (myPlayerIds != null && myPlayerIds.Count > 0) 
            {
                myPlayers = await _leagueApi.GetMflPlayerDetails(leagueId, string.Join(',', myPlayerIds), Utils.CurrentYear, GetApiKey(leagueId));
            }
            var mflLeague = bigLeagueTask.Result.league;
            bigRet.Name = mflLeague.name;
            var playerContracts = rostersTask.Result?.error == null
                ? rostersTask.Result.rosters.franchise.SelectMany(f => f.player).ToList()
                : new List<Player>();
            // Trade screens should show the same speculative 5th-year-option projection the
            // roster page does, so a team doesn't unknowingly trade away/for an option-year risk.
            var projectedFifthYearOptions = await GetProjectedFifthYearOptionSalaries(leagueId,
                new List<FranchiseRoster> { new FranchiseRoster { player = playerContracts } });


            mflLeague.franchises.franchise.ForEach(f =>
            {
                var strFranchId = franchiseId.ToString("0000");
                var assetsDTO = new DashboardTradeFranchiseAssetsDTO();
                var isMyFranchise = f.id == strFranchId;



                var newFranch = new DashboardTradeFranchiseDTO
                {
                    name = f.name,
                    username = f.username,
                    email = f.email,
                    icon = f.icon,
                    id = f.id,
                    abbrev = f.abbrev,
                    logo = f.logo,
                    owner_name = f.owner_name,
                    salaryCapAmount = f.salaryCapAmount
                };

                var foundAssets = assetsTask.Result.assets.franchise.FirstOrDefault(a => a.id == f.id);

                if (foundAssets != null)
                {
                    assetsDTO.futureYearDraftPicks = foundAssets.futureYearDraftPicks?.draftPick ?? new List<DraftPick>();
                    assetsDTO.currentYearDraftPicks = foundAssets.currentYearDraftPicks?.draftPick ?? new List<DraftPick>();
                    foundAssets.players.player.ForEach(p =>
                    {
                        var foundPlayerContract = playerContracts.FirstOrDefault(s => s.id == p.id);
                        if (foundPlayerContract != null)
                        {
                            var playerDTO = new PlayerDTO
                            {
                                Length = int.TryParse(foundPlayerContract.contractYear, out var cY) ? cY : 0,
                                Salary = int.TryParse(foundPlayerContract.salary, out var s) ? s : 0,
                                MflId = int.TryParse(foundPlayerContract.id, out var id) ? id : 0,
                                ContractStatus = foundPlayerContract.contractStatus,
                                ProjectedFifthYearOptionSalary = projectedFifthYearOptions.TryGetValue(foundPlayerContract.id, out var opt) ? opt : (int?)null,
                            };
                          // if my player get more details
                            if (isMyFranchise && myPlayers != null)
                            {
                                var fpd = myPlayers.players.player.FirstOrDefault(mp => mp.id == p.id);
                                if (fpd != null)
                                {
                                    playerDTO.Position = fpd.position;
                                    playerDTO.FullName = fpd.name;
                                    playerDTO.FirstName = fpd.first_name;
                                    playerDTO.LastName = fpd.last_name;
                                    playerDTO.Team = fpd.team;
                                }
                            }
                            assetsDTO.Players.Add(playerDTO);
                        }
                    });
                    newFranch.assets = assetsDTO;
                }
                bigRet.Franchises.Add(newFranch);
            });
            return bigRet;
        }

        public async Task<PlayerBioDTO> GetMflPlayerBioDetails(int leagueId, int lastYear, string id, string firstName,
            string lastName, string position)
        {
            var bioTask =
                _leagueApi.GetMflPlayerDetails(leagueId, id + ",15237,15281", Utils.CurrentYear, GetApiKey(leagueId)); // adding two dummy players so that the response will be array lol
            var salaryTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.CurrentYear, GetApiKey(leagueId));
            var apiKey = _options.Value.Mfl.MflApiKey.First(k => k.id == leagueId).key;
            var scoringTaskYrNeg1 = _leagueApi.GetMflPositionScoresByYear(leagueId, lastYear, position, apiKey);
            var scoringTaskYrNeg2 = _leagueApi.GetMflPositionScoresByYear(leagueId, lastYear - 1, position, apiKey);
            var scoringTaskYrNeg3 = _leagueApi.GetMflPositionScoresByYear(leagueId, lastYear - 2, position, apiKey);

            await Task.WhenAll(bioTask, salaryTask, scoringTaskYrNeg1, scoringTaskYrNeg2, scoringTaskYrNeg3);
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
            return playerBio;
        }
        public async Task<List<PlayerDTO>> GetWaiverExtensionCandidates(int leagueId, int leagueOwnerId, int mflFranchiseId)
        {
            var apiKey = _options.Value.Mfl.MflApiKey.First(k => k.id == leagueId).key;
            // get all waivered guys
            var transactionResp = await _leagueApi.GetLastYearWaiverTransactions(leagueId, apiKey, Utils.CurrentYear - 1);
            if (transactionResp.transactions.transaction == null) return new List<PlayerDTO>();
            var thisTeamTransactions = transactionResp.transactions.transaction.Where(trans => int.Parse(trans.franchise) == mflFranchiseId).ToList();
            var trades = thisTeamTransactions.Where(_ => _.type == "TRADE").ToList();
            var drops = thisTeamTransactions.Where(_ => _.type == "BBID_WAIVER" || _.type == "FREE_AGENT");
            // get list of picked up guys, loop through and check if they were traded or dropped after.
            var pickups = thisTeamTransactions.Where(_ => _.type == "BBID_WAIVER").ToList();
            var extensionCandidates = new List<int>();
            foreach (var pu in pickups)
            {
                var success = int.TryParse(pu.transaction.Split(",")[0], out var addedPlayerId);

                if (!success) continue;
                var remove = false;
                var cuts = drops.Where(_ => _.transaction.Contains(addedPlayerId.ToString()) && long.Parse(_.timestamp) > long.Parse(pu.timestamp)).ToList();
                cuts.ForEach(cut =>
                {
                    if (cut.type == "BBID_WAIVER")
                    {
                        var transactionArr = cut.transaction.Split(",");
                        if (transactionArr.Length > 1)
                        {
                            var bidPlusDroppedPlayer = transactionArr[1];
                            var droppedPlayerArr = bidPlusDroppedPlayer.Split("|");
                            var droppedPlayerId = droppedPlayerArr[2].Replace(",", "");
                            if (!string.IsNullOrEmpty(droppedPlayerId) && droppedPlayerId == addedPlayerId.ToString())
                            {
                                remove = true;
                            }
                        }
                    } else if (cut.type == "FREE_AGENT") // I believe these are just drops for our leagues purposes
                    {
                        var drops = cut.transaction.Split("|")[1].Split(",").ToList();
                        drops.ForEach(drop =>
                        {
                            if (drop == addedPlayerId.ToString()) remove = true;
                        });
                    } 
                });
                trades.ForEach(t =>
                {
                    var assetsMoved = t.franchise2_gave_up.Split(",").ToList();
                    assetsMoved.AddRange(t.franchise1_gave_up.Split(",").ToList());
                    assetsMoved.ForEach(a =>
                    {
                        if (a == addedPlayerId.ToString() && long.Parse(t.timestamp) > long.Parse(pu.timestamp)) remove = true;
                    });
                });
                if (!remove) extensionCandidates.Add(addedPlayerId);
            }
            var dbPlayers = await _pRepo.GetPlayersByListOfIds(extensionCandidates) ?? new List<PlayerEntity>();

            return _mapper.Map<List<PlayerDTO>>(dbPlayers.Where(p => p.Position != "QB").ToList());
            // do another call with all the player ids. then remove all the guys that are QBs


        }

        public async Task GiveNewContractToPlayer(int leagueId, int mflPlayerId, int salary, bool isFranchiseTag, string playerName, string contractStatus = null)
        {
            var botId = Utils.leagueBotDict.TryGetValue(leagueId, out var x) ? x : string.Empty;
            var data = CreateBodyDataForNewContract(mflPlayerId, salary, 1, contractStatus);
            var botMsg = isFranchiseTag ? $"{playerName} got franchise tagged for ${salary}." : $"{playerName} was given a waiver extension of 1 year, ${salary}";
            LogContractStatusAttempt(leagueId, mflPlayerId, contractStatus);
            HttpResponseMessage resp;
            string respString;
            try
            {
                resp = await _leagueApi.EditPlayerSalary(leagueId, data, Utils.CurrentYear);
                respString = await resp.Content.ReadAsStringAsync();
                _logger.LogInformation("MFL salary import raw response: {respString}", respString);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "MFL salary import threw for league {leagueId} player {mflPlayerId}", leagueId, mflPlayerId);
                await TrySendGm(() => _gm.NotifyMflError(new BotMessage($"league: {leagueId} player:{mflPlayerId} contract update threw: {e.Message}", botId)), "NotifyMflError(threw)");
                return;
            }

            if (!resp.IsSuccessStatusCode || respString.ToUpper().Contains("ERROR"))
            {
                string errorMsg = respString;
                try { errorMsg = respString.XmlDeserializeFromString<MflXmlError>().ErrorMsg; } catch { }
                _logger.LogError("MFL contract update failed. Status: {status} Body: {body}", resp.StatusCode, respString);
                await TrySendGm(() => _gm.NotifyMflError(new BotMessage($"league: {leagueId} player:{mflPlayerId} contract was not updated in mfl. \n\n{errorMsg}", botId)), "NotifyMflError(badResp)");
                return;
            }

            await TrySendGm(
                () => _gm.SendBotNotification(new BotMessage(botMsg, botId)),
                "SendBotNotification(contract)",
                fallback: () => _gm.NotifyMflError(new BotMessage($"announcement failed to post: {botMsg}", botId)));
        }

        private async Task TrySendGm(Func<Task> send, string label, Func<Task> fallback = null)
        {
            try { await send(); }
            catch (Exception e)
            {
                _logger.LogError(e, "GroupMe call failed: {label}", label);
                if (fallback == null) return;
                try { await fallback(); }
                catch (Exception f) { _logger.LogError(f, "GroupMe fallback also failed: {label}", label); }
            }
        }

        // contractStatus depends on the "Contract Status" field being enabled on the
        // league's Salary Cap Setup page in MFL — if it's off, MFL silently drops the
        // attribute with no error, so a successful response here does NOT guarantee
        // the status actually landed. Logged explicitly so that's traceable later.
        private void LogContractStatusAttempt(int leagueId, int mflPlayerId, string contractStatus)
        {
            // null = leave contractStatus untouched (attribute omitted from the write entirely).
            // "" = explicitly clear it (e.g. a fresh veteran signing wiping out a stale rookie/tag).
            if (contractStatus == null) return;
            if (contractStatus == "")
            {
                _logger.LogInformation(
                    "Clearing MFL contractStatus for league {leagueId} player {mflPlayerId}", leagueId, mflPlayerId);
                return;
            }
            _logger.LogInformation(
                "Attempting MFL contractStatus={contractStatus} for league {leagueId} player {mflPlayerId} " +
                "(requires the league's Contract Status salary-cap setting to be enabled, or MFL silently ignores it)",
                contractStatus, leagueId, mflPlayerId);
        }

        // Overload: Accept custom bot message and contract length for holdout and other scenarios
        public async Task GiveNewContractToPlayer(int leagueId, int mflPlayerId, int salary, int contractLength, string botMessage, string contractStatus = null, bool announceOnSuccess = true)
        {
            var botId = Utils.leagueBotDict.TryGetValue(leagueId, out var x) ? x : string.Empty;
            var data = CreateBodyDataForNewContract(mflPlayerId, salary, contractLength, contractStatus);
            LogContractStatusAttempt(leagueId, mflPlayerId, contractStatus);

            HttpResponseMessage resp;
            string respString;
            try
            {
                resp = await _leagueApi.EditPlayerSalary(leagueId, data, Utils.CurrentYear);
                respString = await resp.Content.ReadAsStringAsync();
                _logger.LogInformation("MFL salary import raw response: {respString}", respString);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "MFL salary import threw for league {leagueId} player {mflPlayerId}", leagueId, mflPlayerId);
                await TrySendGm(() => _gm.NotifyMflError(new BotMessage($"league: {leagueId} player:{mflPlayerId} contract update threw: {e.Message}", botId)), "NotifyMflError(threw)");
                throw;
            }

            if (!resp.IsSuccessStatusCode || respString.ToUpper().Contains("ERROR"))
            {
                string errorMsg = respString;
                try { errorMsg = respString.XmlDeserializeFromString<MflXmlError>().ErrorMsg; } catch { }
                _logger.LogError("MFL contract update failed. Status: {status} Body: {body}", resp.StatusCode, respString);
                await TrySendGm(() => _gm.NotifyMflError(new BotMessage($"league: {leagueId} player:{mflPlayerId} contract was not updated in mfl. \n\n{errorMsg}", botId)), "NotifyMflError(badResp)");
                throw new Exception($"MFL contract update failed for player {mflPlayerId}: {errorMsg}");
            }

            if (!announceOnSuccess) return; // e.g. a silent contractStatus backfill — no league-chat spam

            // GroupMe being flaky here must never look like the MFL write itself failed —
            // TrySendGm swallows+logs instead of throwing, so callers only see an exception
            // when the actual contract update failed.
            await TrySendGm(
                () => _gm.SendBotNotification(new BotMessage(botMessage, botId)),
                "SendBotNotification(contract)",
                fallback: () => _gm.NotifyMflError(new BotMessage($"announcement failed to post: {botMessage}", botId)));
        }
        public async Task<List<FranchiseRoster>> GetMflRosters(int leagueId)
        {
            var rosterRoot = await _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.CurrentYear, GetApiKey(leagueId));
            return rosterRoot.rosters.franchise;
        }
        // Computes cap from rosters + salary adjustments. Don't switch to bbidAvailableBalance —
        // it's only accurate post-auction when waivers are FCFS, and was the source of a silent
        // bug during the offseason.
        // Formula: salaryCapAmount(per franchise, default 500) - sum(ROSTER salaries)
        //          - sum(TAXI_SQUAD salaries)*0.2 - sum(INJURED_RESERVE salaries)*0.5
        //          - sum(salaryAdjustments)
        public async Task<List<LeagueOwnerEntity>> GetSalaryCapRoom(int leagueId)
        {
            var year = Utils.CurrentYear;
            var bigLeagueTask = _leagueApi.GetBigLeagueObject(leagueId, year, GetApiKey(leagueId));
            var rostersTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId, year, GetApiKey(leagueId));
            var adjustmentsTask = _leagueApi.GetMflSalaryAdjustments(leagueId, year, GetApiKey(leagueId));
            await Task.WhenAll(bigLeagueTask, rostersTask, adjustmentsTask);

            var franchises = bigLeagueTask.Result.league.franchises.franchise;
            var rosters = rostersTask.Result.rosters?.franchise ?? new List<FranchiseRoster>();
            var adjustments = adjustmentsTask.Result.salaryAdjustments?.salaryAdjustment ?? new List<SalaryAdjustment>();

            double ParseSalary(string s) => double.TryParse(s, out var v) ? v : 0;

            double CapWeight(string status) => status switch
            {
                "TAXI_SQUAD" => 0.2,
                "INJURED_RESERVE" => 0.5,
                _ => 1.0,
            };

            var rosterSalariesByFranchise = rosters.ToDictionary(
                f => f.id,
                f => (f.player ?? new List<Player>()).Sum(p => ParseSalary(p.salary) * CapWeight(p.status)));

            var adjustmentsByFranchise = adjustments
                .GroupBy(a => a.franchise_id)
                .ToDictionary(g => g.Key, g => g.Sum(a => ParseSalary(a.amount)));

            return franchises.Select(f =>
            {
                var totalCap = string.IsNullOrEmpty(f.salaryCapAmount) ? 500d : double.Parse(f.salaryCapAmount);
                var rosterSum = rosterSalariesByFranchise.TryGetValue(f.id, out var r) ? r : 0;
                var adjSum = adjustmentsByFranchise.TryGetValue(f.id, out var a) ? a : 0;
                return new LeagueOwnerEntity
                {
                    Mflfranchiseid = int.Parse(f.id),
                    Caproom = (int)Math.Floor(totalCap - rosterSum - adjSum)
                };
            }).ToList();
        }

        public async Task<List<MflPlayerDetails>> GetAllMflFreeAgents(int leagueId)
        {
            var freeAgentIds = (await _leagueApi.GetMflFreeAgents(leagueId, Utils.CurrentYear, GetApiKey(leagueId))).freeAgents.leagueUnit.player.Select(_ => _.id)
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


            var playerDetails1Task = await _leagueApi.GetMflPlayerDetails(leagueId, queryParam1, Utils.CurrentYear, GetApiKey(leagueId));
            var playerDetails2Task = await _leagueApi.GetMflPlayerDetails(leagueId, queryParam2, Utils.CurrentYear, GetApiKey(leagueId));

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
            var thisRosterRootTask = await _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.CurrentYear, GetApiKey(leagueId));
            var myCurrentRoster = thisRosterRootTask.error == null ? thisRosterRootTask.rosters.franchise.FirstOrDefault(f => int.Parse(f.id) == mflFranchiseId).player : new List<Player>();
            if (myCurrentRoster == null) return new List<PlayerDTO>();
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
            var lastRosterRootTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.CurrentYear - 1, GetApiKey(leagueId));
            var thisRosterRootTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.CurrentYear, GetApiKey(leagueId));
            await Task.WhenAll(lastRosterRootTask, thisRosterRootTask);


            var leagueTagData = await _pRepo.GetLeagueTagInfo(leagueId, Utils.CurrentYear);
            var previousTags = _pRepo.GetTagsUsedForTeam(leagueOwnerId);
            var allTags = _pRepo.GetAllTagsForLeague(leagueId);

            var myCurrentRoster = thisRosterRootTask.Result.error == null ? thisRosterRootTask.Result.rosters.franchise.FirstOrDefault(f => int.Parse(f.id) == mflFranchiseId).player : new List<Player>();

            IEnumerable<Player> myExpiringPlayersLastYear = new List<Player>();
            var tagCandidates = new List<TagCandidate>();
            if (myCurrentRoster == null) return tagCandidates;
            var myTaxiPlayersNow = myCurrentRoster.Where(p => p.status == "TAXI_SQUAD");
            var cutCandidates = myCurrentRoster.Where(p => p.status != "TAXI_SQUAD");
            IEnumerable<int> queryIds = new List<int>();


            if (previousTags.Count == 0)
            {
                myExpiringPlayersLastYear = lastRosterRootTask.Result.error == null ? lastRosterRootTask.Result.rosters.franchise.First(f => int.Parse(f.id) == mflFranchiseId).player.Where(p => p.contractYear == "1").ToList() : new List<Player>();
                queryIds = queryIds.Concat(myExpiringPlayersLastYear.Select(p => int.Parse(p.id)));
            }
            var dbPlayers = await _pRepo.GetPlayersByListOfIds(queryIds) ?? new List<PlayerEntity>();
            MflPlayerDetailsRoot mflDetails = new();
            if (queryIds.Any())
                mflDetails = await _leagueApi.GetMflPlayerDetails(leagueId, string.Join(',', queryIds.Select(id => id.ToString())), Utils.CurrentYear, GetApiKey(leagueId));
            if (previousTags.Count == 0 && myExpiringPlayersLastYear.Count() > 0)
            {
                tagCandidates = myExpiringPlayersLastYear.Join(dbPlayers, mfl => int.Parse(mfl.id), db => db.Mflid, (mfl, db) =>
                {
                    var careerTags = allTags.Count(t => t.Mflplayerid == db.Mflid);
                    if (careerTags >= 3) return null; // max 3 career tags

                    var lastSeasonSalary = int.TryParse(mfl.salary, out var s) ? s : 0;
                    var top6Price = GetTagValueFromPosition(db.Position, leagueTagData);
                    var twentyPctRaise = (int)Math.Round(lastSeasonSalary * 1.2);
                    var tagAmount = Math.Max(top6Price, twentyPctRaise); // 1st-year: max(top6, +20%)

                    var wasTaggedByThisOwnerLastYear = allTags.Any(t =>
                        t.Mflplayerid == db.Mflid &&
                        t.Year == Utils.CurrentYear - 1 &&
                        t.Leagueownerid == leagueOwnerId);

                    if (wasTaggedByThisOwnerLastYear)
                    {
                        var top3Price = GetTop3TagValueFromPosition(db.Position, leagueTagData);
                        tagAmount = Math.Max(twentyPctRaise, top3Price); // 2nd-year: max(top3, +20%)
                    }

                    var playerDto = _mapper.Map<PlayerDTO>(db);
                    playerDto.Team = mflDetails?.players?.player?.FirstOrDefault(p => p.id == db.Mflid.ToString())?.team ?? db.Team;
                    return new TagCandidate
                    {
                        Player = playerDto,
                        LastSeasonSalary = lastSeasonSalary,
                        TagAmount = tagAmount
                    };
                })
                .Where(tc => tc != null)
                .ToList();
            }
            return tagCandidates;
        }
        
        public async Task<List<FifthYearOptionCandidate>> GetFifthYearOptionCandidates(int leagueId, int leagueOwnerId, int mflFranchiseId)
        {
            // Rookie deal (4 yrs) just expired; option = +30% 1-yr deal.
            // For 2026, look at 2022 draft (Year-4) and 2025 rosters (Year-1).
            var draftYear = Utils.CurrentYear - 4;
            var lastYear = Utils.CurrentYear - 1;

            var lastYearRosterTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId, lastYear, GetApiKey(leagueId));
            var draftResultsTask = _leagueApi.GetDraftResults(leagueId, draftYear, GetApiKey(leagueId));
            var acceptedHoldoutsTask = _pRepo.GetHoldoutsForLeague(leagueId, Utils.CurrentYear);

            await Task.WhenAll(lastYearRosterTask, draftResultsTask, acceptedHoldoutsTask);

            var lastYearRoster = lastYearRosterTask.Result.error == null ?
                lastYearRosterTask.Result.rosters.franchise.FirstOrDefault(f => int.Parse(f.id) == mflFranchiseId)?.player :
                new List<Player>();

            if (lastYearRoster == null || lastYearRoster.Count == 0)
                return new List<FifthYearOptionCandidate>();

            var draftResults = draftResultsTask.Result.draftResults?.draftUnit?.draftPick ?? new List<MflDraftPick>();
            var firstRoundPicks = draftResults.Where(d => d.round == "01").ToList();

            // Picks from Year-4 draft that this franchise still had on its Year-1 roster
            var myFirstRoundPicks = firstRoundPicks
                .Where(pick => lastYearRoster.Any(p => p.id == pick.player))
                .ToList();

            if (myFirstRoundPicks.Count == 0)
                return new List<FifthYearOptionCandidate>();

            var playerIds = myFirstRoundPicks.Select(p => p.player).ToList();
            var mflPlayers = await _leagueApi.GetMflPlayerDetails(leagueId, string.Join(',', playerIds), Utils.CurrentYear, GetApiKey(leagueId));

            var acceptedHoldouts = acceptedHoldoutsTask.Result
                .Where(h => h.Status == "Accepted")
                .ToDictionary(h => h.PlayerId, h => h);

            var optionCandidates = new List<FifthYearOptionCandidate>();

            foreach (var draftPick in myFirstRoundPicks)
            {
                var lastYearPlayerData = lastYearRoster.FirstOrDefault(p => p.id == draftPick.player);
                if (lastYearPlayerData == null) continue;

                var mflPlayer = mflPlayers.players.player.FirstOrDefault(p => p.id == draftPick.player);
                if (mflPlayer == null) continue;

                var pickNumber = int.TryParse(draftPick.pick, out var pn) ? pn : 0;
                if (pickNumber == 0) continue;

                var isRB = mflPlayer.position == "RB";
                var originalSalary = isRB ?
                    Utils.rbDraftPicks.GetValueOrDefault(pickNumber, 0) :
                    Utils.draftPicks.GetValueOrDefault(pickNumber, 0);

                if (originalSalary == 0) continue;

                var lastYearSalary = int.TryParse(lastYearPlayerData.salary, out var cs) ? cs : 0;

                // Holdout precedence: if player had accepted holdout, compare against pre-holdout salary,
                // even if Year-1 roster salary is higher.
                var salaryToCompare = lastYearSalary;
                if (acceptedHoldouts.TryGetValue(int.Parse(draftPick.player), out var holdout))
                {
                    salaryToCompare = holdout.OriginalSalary;
                    _logger.LogInformation($"Player {mflPlayer.name} had accepted holdout. Using original salary {holdout.OriginalSalary} instead of last-year {lastYearSalary}");
                }

                if (salaryToCompare == originalSalary)
                {
                    var optionSalary = (int)Math.Round(originalSalary * 1.3);

                    optionCandidates.Add(new FifthYearOptionCandidate
                    {
                        Player = new PlayerDTO
                        {
                            MflId = int.TryParse(mflPlayer.id, out var id) ? id : 0,
                            FirstName = mflPlayer.first_name,
                            LastName = mflPlayer.last_name,
                            FullName = mflPlayer.name,
                            Position = mflPlayer.position,
                            Team = mflPlayer.team,
                            Age = GetAgeInt(mflPlayer.birthdate),
                            Salary = salaryToCompare,
                            Length = int.TryParse(lastYearPlayerData.contractYear, out var l) ? l : 0
                        },
                        OriginalRookieSalary = originalSalary,
                        OptionSalary = optionSalary,
                        DraftYear = draftYear,
                        DraftPick = pickNumber
                    });
                }
            }

            return optionCandidates;
        }

        // contractStatus can carry more than one tag at once (e.g. a rookie who is also
        // currently holding out: "R1-2024|HOLDOUT"). These helpers do read-modify-write
        // set operations on that pipe-delimited string rather than blindly overwriting it.
        public static List<string> ParseTags(string contractStatus) =>
            string.IsNullOrWhiteSpace(contractStatus)
                ? new List<string>()
                : contractStatus.Split('|').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();

        public static string JoinTags(IEnumerable<string> tags)
        {
            var joined = string.Join("|", tags.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct());
            return string.IsNullOrEmpty(joined) ? null : joined;
        }

        public static string AddTag(string existingContractStatus, string tag)
        {
            var tags = ParseTags(existingContractStatus);
            if (!tags.Contains(tag)) tags.Add(tag);
            return JoinTags(tags);
        }

        public static string RemoveTag(string existingContractStatus, string tag) =>
            JoinTags(ParseTags(existingContractStatus).Where(t => t != tag));

        // Strips any tag starting with `prefix` (e.g. "R" for a rookie-round tag being
        // superseded by a 5th-year option) before adding the replacement.
        public static string ReplaceTagsWithPrefix(string existingContractStatus, string prefix, string newTag)
        {
            var tags = ParseTags(existingContractStatus).Where(t => !t.StartsWith(prefix)).ToList();
            tags.Add(newTag);
            return JoinTags(tags);
        }

        // MFL's "name" field comes back as "Last, First" — convert to "First Last".
        private static string NameFromMflFullName(string mflName)
        {
            if (string.IsNullOrWhiteSpace(mflName)) return mflName ?? "";
            var parts = mflName.Split(",");
            return parts.Length == 2 ? $"{parts[1].Trim()} {parts[0].Trim()}" : mflName;
        }

        // Read-only report: every currently-rostered player who should carry a short
        // MFL contractStatus tag (rookie deal, franchise tag count, waiver extension,
        // accepted holdout) but doesn't yet. Never writes to MFL — for manual review.
        public async Task<List<ContractStatusAuditEntry>> GetContractStatusAudit(int leagueId)
        {
            var apiKey = GetApiKey(leagueId);
            var entries = new List<ContractStatusAuditEntry>();

            // GroupBy + First (not ToDictionaryAsync) — some leagues have co-owned franchises
            // sharing one Mflfranchiseid across two LeagueOwner rows, which throws on a
            // straight ToDictionary. Duplicates just pick whichever owner's name comes first.
            var leagueOwners = await _db.LeagueOwners.Where(lo => lo.Leagueid == leagueId).ToListAsync();
            var teamNameByFranchiseId = leagueOwners
                .GroupBy(lo => lo.Mflfranchiseid)
                .ToDictionary(g => g.Key, g => g.First().Teamname);
            var teamNameByLeagueOwnerId = leagueOwners
                .GroupBy(lo => lo.Leagueownerid)
                .ToDictionary(g => g.Key, g => g.First().Teamname);

            string TeamNameForFranchise(string franchiseIdStr) =>
                int.TryParse(franchiseIdStr, out var fid) && teamNameByFranchiseId.TryGetValue(fid, out var name) ? name : franchiseIdStr;
            string TeamNameForOwner(int leagueOwnerId) =>
                teamNameByLeagueOwnerId.TryGetValue(leagueOwnerId, out var name) ? name : leagueOwnerId.ToString();

            var currentRosterRoot = await _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.CurrentYear, apiKey);
            var allFranchises = currentRosterRoot?.error == null
                ? currentRosterRoot.rosters?.franchise ?? new List<FranchiseRoster>()
                : new List<FranchiseRoster>();
            var rosterByPlayerId = allFranchises
                .SelectMany(f => (f.player ?? new List<Player>()).Select(p => (Franchise: f, Player: p)))
                .GroupBy(x => x.Player.id)
                .ToDictionary(g => g.Key, g => g.First());

            // Accepted holdouts (any year) — a player's current salary may reflect a past
            // holdout raise on top of their rookie-scale/option salary, which would otherwise
            // make them fall through the exact-match checks below and get no tag at all.
            var acceptedHoldoutsByPlayer = (await _db.Holdouts
                    .Where(h => h.LeagueId == leagueId && h.Status == "Accepted")
                    .ToListAsync())
                .GroupBy(h => h.PlayerId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // ---- Rookie deal / 5th-year option — round-1 picks from the last 5 draft classes,
            // including THIS year's own rookie class (off-by-one here previously excluded it) ----
            for (var draftYear = Utils.CurrentYear; draftYear >= Utils.CurrentYear - 4; draftYear--)
            {
                var draftResultsRoot = await _leagueApi.GetDraftResults(leagueId, draftYear, apiKey);
                var firstRoundPicks = draftResultsRoot?.draftResults?.draftUnit?.draftPick?
                    .Where(d => d.round == "01")
                    .ToList() ?? new List<MflDraftPick>();
                var pickIds = firstRoundPicks
                    .Where(p => rosterByPlayerId.ContainsKey(p.player))
                    .Select(p => p.player)
                    .ToList();
                if (pickIds.Count == 0) continue;

                var mflPlayers = await _leagueApi.GetMflPlayerDetails(leagueId, string.Join(',', pickIds), Utils.CurrentYear, apiKey);

                foreach (var pick in firstRoundPicks)
                {
                    if (!rosterByPlayerId.TryGetValue(pick.player, out var rosterEntry)) continue;
                    var mflPlayer = mflPlayers?.players?.player?.FirstOrDefault(p => p.id == pick.player);
                    if (mflPlayer == null) continue;
                    if (!int.TryParse(pick.pick, out var pickNumber) || pickNumber == 0) continue;

                    var isRB = mflPlayer.position == "RB";
                    var originalSalary = isRB
                        ? Utils.rbDraftPicks.GetValueOrDefault(pickNumber, 0)
                        : Utils.draftPicks.GetValueOrDefault(pickNumber, 0);
                    if (originalSalary == 0) continue;

                    var currentSalary = int.TryParse(rosterEntry.Player.salary, out var cs) ? cs : 0;
                    var optionSalary = (int)Math.Round(originalSalary * 1.3);
                    var team = TeamNameForFranchise(rosterEntry.Franchise.id);
                    // GetMflPlayerDetails often returns first_name/last_name blank for this
                    // endpoint shape — same MFL quirk GetMflPlayerById already works around.
                    var playerName = !string.IsNullOrWhiteSpace(mflPlayer.first_name) || !string.IsNullOrWhiteSpace(mflPlayer.last_name)
                        ? $"{mflPlayer.first_name} {mflPlayer.last_name}".Trim()
                        : NameFromMflFullName(mflPlayer.name);

                    var currentContractYear = int.TryParse(rosterEntry.Player.contractYear, out var cy) ? cy : 0;

                    // A player's current salary may not exactly match the clean rookie-scale or
                    // option number if an accepted holdout raised it since — check whether an
                    // accepted holdout explains the gap (its pre-raise salary equals what we
                    // expected, and its post-raise salary equals what they're on today) before
                    // concluding they're off this tier entirely.
                    var matchesRookieScale = currentSalary == originalSalary;
                    var matchesOptionScale = currentSalary == optionSalary;
                    var explainingHoldout = (!matchesRookieScale && !matchesOptionScale) && acceptedHoldoutsByPlayer.TryGetValue(int.Parse(pick.player), out var playerHoldouts)
                        ? playerHoldouts.FirstOrDefault(h => h.HoldoutSalary == currentSalary && (h.OriginalSalary == originalSalary || h.OriginalSalary == optionSalary))
                        : null;
                    if (explainingHoldout != null)
                    {
                        matchesRookieScale = explainingHoldout.OriginalSalary == originalSalary;
                        matchesOptionScale = explainingHoldout.OriginalSalary == optionSalary;
                    }
                    var holdoutSuffix = explainingHoldout != null
                        ? $", raised to ${currentSalary} via an accepted holdout ({explainingHoldout.Year})"
                        : "";

                    if (matchesRookieScale)
                    {
                        entries.Add(new ContractStatusAuditEntry
                        {
                            MflPlayerId = int.Parse(pick.player),
                            PlayerName = playerName,
                            Team = team,
                            Tag = $"R1-{draftYear}",
                            Reason = $"Round 1, {draftYear} draft, on rookie-scale salary (${originalSalary}){holdoutSuffix}",
                            CurrentSalary = currentSalary,
                            CurrentContractYear = currentContractYear
                        });
                    }
                    // 5th-year options are only ever eligible the year after a 4-year rookie
                    // deal expires — i.e. exactly the year-4 draft class. Checking this across
                    // all 5 lookback years risked mislabeling a coincidental salary match from
                    // a different class (e.g. a year-2 raise that happens to equal 1.3x scale).
                    else if (draftYear == Utils.CurrentYear - 4 && matchesOptionScale)
                    {
                        entries.Add(new ContractStatusAuditEntry
                        {
                            MflPlayerId = int.Parse(pick.player),
                            PlayerName = playerName,
                            Team = team,
                            Tag = "5YO",
                            Reason = $"Round 1, {draftYear} draft, on 5th-year option salary (${optionSalary}){holdoutSuffix}",
                            CurrentSalary = currentSalary,
                            CurrentContractYear = currentContractYear
                        });
                    }
                }
            }

            // ---- Franchise tags — currently on an active tag contract this year ----
            var allTags = _pRepo.GetAllTagsForLeague(leagueId) ?? new List<FranchiseTagPlayer>();
            foreach (var tag in allTags.Where(t => t.Year == Utils.CurrentYear))
            {
                var tagCount = allTags.Count(t => t.Mflplayerid == tag.Mflplayerid);
                var tagPlayerName = tag.Fullname;
                if (string.IsNullOrWhiteSpace(tagPlayerName))
                {
                    // The name stored at tag-time was blank (same MFL first_name/last_name
                    // quirk as the rookie section) — re-resolve it now rather than show a
                    // blank row the commissioner can't act on.
                    var tagMflPlayer = (await _leagueApi.GetMflPlayerDetails(leagueId, tag.Mflplayerid.ToString(), Utils.CurrentYear, apiKey))
                        ?.players?.player?.FirstOrDefault();
                    tagPlayerName = tagMflPlayer != null
                        ? (!string.IsNullOrWhiteSpace(tagMflPlayer.first_name) || !string.IsNullOrWhiteSpace(tagMflPlayer.last_name)
                            ? $"{tagMflPlayer.first_name} {tagMflPlayer.last_name}".Trim()
                            : NameFromMflFullName(tagMflPlayer.name))
                        : tag.Mflplayerid.ToString();
                }
                entries.Add(new ContractStatusAuditEntry
                {
                    MflPlayerId = tag.Mflplayerid,
                    PlayerName = tagPlayerName,
                    Team = TeamNameForOwner(tag.Leagueownerid),
                    Tag = $"TAG-{tagCount}",
                    Reason = $"Franchise tag #{tagCount} ({tag.Year})"
                });
            }

            // ---- Waiver extensions — signed this year ----
            var waiverExtensions = await _db.WaiverExtensions
                .Where(w => w.LeagueId == leagueId && w.Year == Utils.CurrentYear)
                .ToListAsync();
            foreach (var w in waiverExtensions)
            {
                var player = await _pRepo.GetPlayerById(w.PlayerId);
                entries.Add(new ContractStatusAuditEntry
                {
                    MflPlayerId = w.PlayerId,
                    PlayerName = player?.Fullname ?? w.PlayerId.ToString(),
                    Team = TeamNameForOwner(w.LeagueOwnerId),
                    Tag = "WVR-EXT",
                    Reason = $"Waiver extension ({w.Year})"
                });
            }

            // ---- Currently holding out (Pending only — Accepted means it's resolved and
            // the player is no longer holding out, so HOLDOUT must not apply anymore) ----
            var holdouts = await _pRepo.GetHoldoutsForLeague(leagueId, Utils.CurrentYear) ?? new List<Holdout>();
            foreach (var h in holdouts.Where(h => h.Status == "Pending"))
            {
                var player = await _pRepo.GetPlayerById(h.PlayerId);
                entries.Add(new ContractStatusAuditEntry
                {
                    MflPlayerId = h.PlayerId,
                    PlayerName = player?.Fullname ?? h.PlayerId.ToString(),
                    Team = TeamNameForOwner(h.LeagueOwnerId),
                    Tag = "HOLDOUT",
                    Reason = $"Currently holding out ({Utils.CurrentYear})"
                });
            }

            // Tag/waiver/holdout entries didn't have a roster lookup in hand when built above —
            // backfill current salary/contractYear here so apply-contract-status always has
            // the real current terms to preserve, not a derived/assumed value.
            foreach (var entry in entries)
            {
                if (entry.CurrentSalary != 0 || entry.CurrentContractYear != 0) continue;
                if (!rosterByPlayerId.TryGetValue(entry.MflPlayerId.ToString(), out var rosterEntry)) continue;
                entry.CurrentSalary = int.TryParse(rosterEntry.Player.salary, out var s) ? s : 0;
                entry.CurrentContractYear = int.TryParse(rosterEntry.Player.contractYear, out var cy) ? cy : 0;
            }

            // Combine every category into one row per player — a player can legitimately match
            // more than one (e.g. a round-1 rookie who is also currently holding out), and the
            // live write paths now build combined tags too, so the audit must match that shape.
            var combined = entries
                .GroupBy(e => e.MflPlayerId)
                .Select(g => new ContractStatusAuditEntry
                {
                    MflPlayerId = g.Key,
                    PlayerName = g.First().PlayerName,
                    Team = g.First().Team,
                    Tag = JoinTags(g.Select(e => e.Tag)),
                    Reason = string.Join("; ", g.Select(e => e.Reason)),
                    CurrentSalary = g.First().CurrentSalary,
                    CurrentContractYear = g.First().CurrentContractYear
                })
                .OrderBy(e => e.PlayerName)
                .ToList();

            return combined;
        }

        // Full-roster snapshot — every player, every franchise, current salary/contractYear/
        // contractStatus. A before/after safety net for batch contractStatus writes.
        public Task<List<PlayerSnapshotEntry>> GetLeagueRosterSnapshot(int leagueId) =>
            GetLeagueRosterSnapshotForYear(leagueId, Utils.CurrentYear);

        // Same as above, for an arbitrary (e.g. historical) year — used to trace exactly
        // when/where a contract-length or salary discrepancy originated.
        public async Task<List<PlayerSnapshotEntry>> GetLeagueRosterSnapshotForYear(int leagueId, int year)
        {
            var apiKey = GetApiKey(leagueId);
            var teamNameByFranchiseId = (await _db.LeagueOwners.Where(lo => lo.Leagueid == leagueId).ToListAsync())
                .GroupBy(lo => lo.Mflfranchiseid)
                .ToDictionary(g => g.Key, g => g.First().Teamname);

            var rosterRoot = await _leagueApi.GetMflRostersForPlayerSalaries(leagueId, year, apiKey);
            var allFranchises = rosterRoot?.error == null ? rosterRoot.rosters?.franchise ?? new List<FranchiseRoster>() : new List<FranchiseRoster>();
            var allPlayers = allFranchises.SelectMany(f => (f.player ?? new List<Player>()).Select(p => (Franchise: f, Player: p))).ToList();
            if (allPlayers.Count == 0) return new List<PlayerSnapshotEntry>();

            var ids = allPlayers.Select(p => p.Player.id).Distinct().ToList();
            var namesByPlayerId = new Dictionary<string, string>();
            // MFL's player-details endpoint has a practical URL-length limit — batch the lookup.
            foreach (var batch in ids.Select((id, i) => (id, i)).GroupBy(x => x.i / 200).Select(g => g.Select(x => x.id).ToList()))
            {
                var details = await _leagueApi.GetMflPlayerDetails(leagueId, string.Join(',', batch), year, apiKey);
                foreach (var p in details?.players?.player ?? new List<MflPlayerDetails>())
                {
                    namesByPlayerId[p.id] = !string.IsNullOrWhiteSpace(p.first_name) || !string.IsNullOrWhiteSpace(p.last_name)
                        ? $"{p.first_name} {p.last_name}".Trim()
                        : NameFromMflFullName(p.name);
                }
            }

            return allPlayers.Select(x => new PlayerSnapshotEntry
            {
                MflPlayerId = int.TryParse(x.Player.id, out var id) ? id : 0,
                PlayerName = namesByPlayerId.GetValueOrDefault(x.Player.id, x.Player.id),
                Team = int.TryParse(x.Franchise.id, out var fid) && teamNameByFranchiseId.TryGetValue(fid, out var name) ? name : x.Franchise.id,
                Salary = int.TryParse(x.Player.salary, out var sal) ? sal : 0,
                ContractYear = int.TryParse(x.Player.contractYear, out var cy) ? cy : 0,
                ContractStatus = x.Player.contractStatus
            }).OrderBy(p => p.PlayerName).ToList();
        }

        public async Task<object> GetDraftPickDiagnostics(int leagueId, int draftYear, int mflPlayerId)
        {
            var apiKey = GetApiKey(leagueId);
            var draftResultsRoot = await _leagueApi.GetDraftResults(leagueId, draftYear, apiKey);
            var pick = draftResultsRoot?.draftResults?.draftUnit?.draftPick?.FirstOrDefault(p => p.player == mflPlayerId.ToString());
            if (pick == null) return new { found = false };

            var playerDetails = await _leagueApi.GetMflPlayerDetails(leagueId, mflPlayerId.ToString(), draftYear, apiKey);
            var mflPlayer = playerDetails?.players?.player?.FirstOrDefault();
            var pickNumber = int.TryParse(pick.pick, out var pn) ? pn : 0;
            var isRB = mflPlayer?.position == "RB";

            return new
            {
                found = true,
                round = pick.round,
                pick = pick.pick,
                pickNumber,
                position = mflPlayer?.position,
                isRB,
                computedOriginalSalary = isRB ? Utils.rbDraftPicks.GetValueOrDefault(pickNumber, 0) : Utils.draftPicks.GetValueOrDefault(pickNumber, 0),
                rbTableValueAtThisPick = Utils.rbDraftPicks.GetValueOrDefault(pickNumber, 0),
                nonRbTableValueAtThisPick = Utils.draftPicks.GetValueOrDefault(pickNumber, 0)
            };
        }

        // Roster-page-facing projection: for round-1 rookies still tagged R1-{year} (option not
        // yet exercised) whose 4-year rookie deal ends within the next few seasons, project what
        // their 5th-year-option salary would be. Speculative — MFL is the source of truth once a
        // team actually exercises the option (at which point the player gets tagged 5YO instead).
        public async Task<Dictionary<string, int>> GetProjectedFifthYearOptionSalaries(int leagueId, List<FranchiseRoster> rosters)
        {
            var result = new Dictionary<string, int>();
            var candidates = rosters
                .SelectMany(f => f.player)
                .Select(p => new { Player = p, Tags = ParseTags(p.contractStatus) })
                .Where(x => !x.Tags.Contains("5YO"))
                .Select(x => new { x.Player, RookieTag = x.Tags.FirstOrDefault(t => t.StartsWith("R1-")) })
                .Where(x => x.RookieTag != null)
                .ToList();

            if (candidates.Count == 0) return result;

            var acceptedHoldoutsByPlayer = (await _db.Holdouts
                    .Where(h => h.LeagueId == leagueId && h.Status == "Accepted")
                    .ToListAsync())
                .GroupBy(h => h.PlayerId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var c in candidates)
            {
                if (!int.TryParse(c.RookieTag.Split('-')[1], out var draftYear)) continue;
                // The option decision comes right after a 4-year rookie deal ends — only worth
                // projecting if that falls within the roster page's visible year window.
                var yearsAway = draftYear + 4 - Utils.CurrentYear;
                if (yearsAway < 0 || yearsAway > 3) continue;

                var currentSalary = int.TryParse(c.Player.salary, out var s) ? s : 0;
                var baseSalary = currentSalary;
                if (int.TryParse(c.Player.id, out var pid) && acceptedHoldoutsByPlayer.TryGetValue(pid, out var holdouts))
                {
                    var matchingHoldout = holdouts.FirstOrDefault(h => h.HoldoutSalary == currentSalary);
                    if (matchingHoldout != null) baseSalary = matchingHoldout.OriginalSalary;
                }
                if (baseSalary == 0) continue;

                result[c.Player.id] = (int)Math.Round(baseSalary * 1.3);
            }

            return result;
        }

        public async Task<object> GetRound1PicksDiagnostics(int leagueId, int draftYear)
        {
            var apiKey = GetApiKey(leagueId);
            var draftResultsRoot = await _leagueApi.GetDraftResults(leagueId, draftYear, apiKey);
            var firstRoundPicks = draftResultsRoot?.draftResults?.draftUnit?.draftPick?
                .Where(d => d.round == "01")
                .ToList() ?? new List<MflDraftPick>();

            var currentRosterRoot = await _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.CurrentYear, apiKey);
            var allFranchises = currentRosterRoot?.error == null
                ? currentRosterRoot.rosters?.franchise ?? new List<FranchiseRoster>()
                : new List<FranchiseRoster>();
            var rosterByPlayerId = allFranchises
                .SelectMany(f => (f.player ?? new List<Player>()).Select(p => (Franchise: f, Player: p)))
                .GroupBy(x => x.Player.id)
                .ToDictionary(g => g.Key, g => g.First());

            var pickIds = firstRoundPicks.Select(p => p.player).ToList();
            var mflPlayers = pickIds.Count > 0
                ? await _leagueApi.GetMflPlayerDetails(leagueId, string.Join(',', pickIds), Utils.CurrentYear, apiKey)
                : null;

            var results = new List<object>();
            foreach (var pick in firstRoundPicks)
            {
                var mflPlayer = mflPlayers?.players?.player?.FirstOrDefault(p => p.id == pick.player);
                var stillRostered = rosterByPlayerId.TryGetValue(pick.player, out var rosterEntry);
                int.TryParse(pick.pick, out var pickNumber);
                var isRB = mflPlayer?.position == "RB";
                var originalSalary = isRB
                    ? Utils.rbDraftPicks.GetValueOrDefault(pickNumber, 0)
                    : Utils.draftPicks.GetValueOrDefault(pickNumber, 0);
                var optionSalary = (int)Math.Round(originalSalary * 1.3);
                var currentSalary = stillRostered && int.TryParse(rosterEntry.Player.salary, out var cs) ? cs : (int?)null;
                var currentContractYear = stillRostered && int.TryParse(rosterEntry.Player.contractYear, out var cy) ? cy : (int?)null;
                var playerName = mflPlayer != null
                    ? (!string.IsNullOrWhiteSpace(mflPlayer.first_name) || !string.IsNullOrWhiteSpace(mflPlayer.last_name)
                        ? $"{mflPlayer.first_name} {mflPlayer.last_name}".Trim()
                        : NameFromMflFullName(mflPlayer.name))
                    : null;

                results.Add(new
                {
                    mflPlayerId = pick.player,
                    playerName,
                    position = mflPlayer?.position,
                    isRB,
                    pickNumber,
                    stillRostered,
                    currentTeam = stillRostered ? rosterEntry.Franchise.id : null,
                    currentSalary,
                    currentContractYear,
                    currentContractStatus = stillRostered ? rosterEntry.Player.contractStatus : null,
                    computedOriginalSalary = originalSalary,
                    computedOptionSalary = optionSalary,
                    matchesRookieScale = currentSalary == originalSalary,
                    matchesOptionScale = currentSalary == optionSalary
                });
            }

            return results;
        }

        // Re-reads a single player's contractStatus straight from MFL after a write, since a
        // 200 response never proves the attribute actually landed (MFL silently drops it if
        // the league's Contract Status salary-cap setting is off).
        public async Task<string> GetPlayerContractStatusFromMfl(int leagueId, int mflPlayerId)
        {
            var apiKey = GetApiKey(leagueId);
            var rosterRoot = await _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.CurrentYear, apiKey);
            if (rosterRoot?.error != null) return null;
            return rosterRoot?.rosters?.franchise
                ?.SelectMany(f => f.player ?? new List<Player>())
                ?.FirstOrDefault(p => p.id == mflPlayerId.ToString())
                ?.contractStatus;
        }

        public async Task<List<PlayerDTO>> GetBuyoutCandidates(int leagueId, int leagueOwnerId, int mflFranchiseId)
        {
            var previousBuyouts = _pRepo.GetBuyoutsUsedForTeam(leagueOwnerId);
            var thisRosterRootTask = await _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.CurrentYear, GetApiKey(leagueId));
            var myCurrentRoster = thisRosterRootTask.error == null ? thisRosterRootTask.rosters.franchise.FirstOrDefault(f => int.Parse(f.id) == mflFranchiseId).player : new List<Player>();
            if (myCurrentRoster == null) return new List<PlayerDTO>();
            var cutCandidates = myCurrentRoster.Where(p => p.status != "TAXI_SQUAD").ToList();
            var queryIds = cutCandidates.Select(p => int.Parse(p.id));
            var dbPlayers = await _pRepo.GetPlayersByListOfIds(queryIds) ?? new List<PlayerEntity>();
            MflPlayerDetailsRoot mflDetails = new();
            if (queryIds.Any())
                mflDetails = await _leagueApi.GetMflPlayerDetails(leagueId, string.Join(',', queryIds), Utils.CurrentYear, GetApiKey(leagueId));
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
                    Team = mflDetails?.players?.player?.FirstOrDefault(p => p.id == db.Mflid.ToString())?.team ?? db.Team,
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
                var lastRosterRootTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.CurrentYear - 1, GetApiKey(leagueId));
                var thisRosterRootTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.CurrentYear, GetApiKey(leagueId));
                await Task.WhenAll(lastRosterRootTask, thisRosterRootTask);

                var previousBuyouts = _pRepo.GetBuyoutsUsedForTeam(leagueOwner.Leagueownerid);

                var allTags = _pRepo.GetAllTagsForLeague(leagueId);
                var allTagsForThisFranchise = allTags.Where(t => t.Leagueownerid == leagueOwner.Leagueownerid).ToList();
                var tagNotUsedYet = allTagsForThisFranchise.FirstOrDefault(t => t.Year == DateTime.UtcNow.Year) == null;
                

                var myCurrentRoster = thisRosterRootTask.Result.error == null ? thisRosterRootTask.Result.rosters.franchise.FirstOrDefault(f => int.Parse(f.id) == leagueOwner.Mflfranchiseid).player : new List<Player>();

                IEnumerable<Player> myExpiringPlayersLastYear = new List<Player>();

                var myTaxiPlayersNow = myCurrentRoster.Where(p => p.status == "TAXI_SQUAD");
                var cutCandidates = myCurrentRoster.Where(p => p.status != "TAXI_SQUAD");
                IEnumerable<int> queryIds = new List<int>();


                if (tagNotUsedYet)
                {
                    myExpiringPlayersLastYear = lastRosterRootTask.Result.error == null ? lastRosterRootTask.Result.rosters.franchise.First(f => int.Parse(f.id) == leagueOwner.Mflfranchiseid).player.Where(p => p.contractYear == "1").ToList() : new List<Player>();
                    queryIds = queryIds.Concat(myExpiringPlayersLastYear.Select(p => int.Parse(p.id)));
                }

                var dbPlayers = await _pRepo.GetPlayersByListOfIds(queryIds) ?? new List<PlayerEntity>();





                if (tagNotUsedYet && myExpiringPlayersLastYear.Count() > 0)
                {
                    var leagueTagData = await _pRepo.GetLeagueTagInfo(leagueId, Utils.CurrentYear);
                    retOwner.TagCandidates = myExpiringPlayersLastYear.Join(dbPlayers, mfl => int.Parse(mfl.id), db => db.Mflid, (mfl, db) =>
                    {
                        var lastSeasonSalary = int.TryParse(mfl.salary, out var s) ? s : 0;
                        var prevTagsForPlayer = allTags.Where(t => t.Mflplayerid == db.Mflid).ToList();
                        if (prevTagsForPlayer.Count >= 3) 
                        {
                            return null; // max of 3 tags per player
                        }
                        var defaultTagAmount = GetTagValueFromPosition(db.Position, leagueTagData);
                        
                        var altTagAmount = lastSeasonSalary * 1.2; // last years salary + 20%

                        var tagAmount = Math.Max(defaultTagAmount, (int)Math.Round(altTagAmount));
                        if (allTags.FirstOrDefault(t => t.Mflplayerid == db.Mflid && t.Year == Utils.CurrentYear - 1 && t.Leagueownerid == leagueOwner.Leagueownerid) != null) //if this player was tagged by this team last year.
                        {
                            var top3Price = GetTop3TagValueFromPosition(db.Position, leagueTagData);
                            tagAmount = Math.Max((int)Math.Round(altTagAmount), top3Price);
                        }
                        return new TagCandidate
                        {
                            Player = _mapper.Map<PlayerDTO>(db),
                            LastSeasonSalary = lastSeasonSalary,
                            TagAmount = tagAmount 
                        };
                    })
                    .Where(tc => tc != null)
                    .ToList();
                
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
            var botId = Utils.leagueBotDict.TryGetValue(req.leagueId, out var x) ? x : string.Empty;
            var franchiseStr = req.mflFranchiseId.ToString("D4");
            try { 
                var dropRequest = await _leagueApi.DropPlayerFromTaxi(req.leagueId, req.player.MflId, franchiseStr, Utils.CurrentYear);
                var respString = await dropRequest.Content.ReadAsStringAsync();
                if (respString.ToUpper().Contains("ERROR"))
                {
                    var error = respString.XmlDeserializeFromString<MflXmlError>();
                    _logger.LogInformation(respString);
                    _logger.LogError("{mflPlayerId}'s contract was not updated in mfl.", req.player.FullName);
                    await _gm.NotifyMflError(new BotMessage($"league: {req.leagueId} player: {req.player.FullName} could not be taxi cut.", botId));
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "drop player mfl");
                return;
            }
            try
            {
                var data = CreateBodyDataForNewSalaryAdj(franchiseStr, -req.rebate, "TAXI_REBATE", req.player, req.player.Length ?? 1);
                var dropRequest = await _leagueApi.AddSalaryAdjustment(req.leagueId, data, Utils.CurrentYear);
                var respString = await dropRequest.Content.ReadAsStringAsync();
                if (respString.ToUpper().Contains("ERROR"))
                {
                    var error = respString.XmlDeserializeFromString<MflXmlError>();
                    _logger.LogInformation(respString);
                    _logger.LogError("{mflPlayerId}'s taxi rebate salary adjustment was not added in mfl.", req.player.FullName);
                    await _gm.NotifyMflError(new BotMessage($"league: {req.leagueId} player: {req.player.FullName} could not properly be taxi cut.", botId));
                }
                else await _gm.SendBotNotification(message: new BotMessage($"New taxi cut submitted on fanpools.net\n{req.player.FullName} was cut.", botId));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Salary Adj mfl");
                return;
            }

        }

        public async Task BuyoutPlayer(CutRequestBody req)
        {
            var botId = Utils.leagueBotDict.TryGetValue(req.leagueId, out var x) ? x : string.Empty;
            var franchiseStr = req.mflFranchiseId.ToString("D4");
            try
            {
                var dropRequest = await _leagueApi.DropPlayer(req.leagueId, req.player.MflId, franchiseStr, Utils.CurrentYear);
                var respString = await dropRequest.Content.ReadAsStringAsync();
                if (respString.ToUpper().Contains("ERROR"))
                {
                    var error = respString.XmlDeserializeFromString<MflXmlError>();
                    _logger.LogInformation(respString);
                    _logger.LogError("{mflPlayerId}'s contract was not updated in mfl.", req.player.FullName);
                    await _gm.NotifyMflError(new BotMessage($"league: {req.leagueId} player: {req.player.FullName} could not be bought out.", botId));
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
                var rebRequest = await _leagueApi.AddSalaryAdjustment(req.leagueId, rebateData, Utils.CurrentYear);
                var rebRespString = await rebRequest.Content.ReadAsStringAsync();
                if (rebRespString.ToUpper().Contains("ERROR"))
                {
                    var error = rebRespString.XmlDeserializeFromString<MflXmlError>();
                    _logger.LogInformation(rebRespString);
                    _logger.LogError("{mflPlayerId}'s buyout rebate salary adjustment was not added in mfl.", req.player.FullName);
                    await _gm.NotifyMflError(new BotMessage($"league: {req.leagueId} player: {req.player.FullName} could not properly apply buyout rebate salary adjustment.", botId));
                }
                var penaltyData = CreateBodyDataForNewSalaryAdj(franchiseStr, (req.rebate * 0.5), "BUYOUT_PENALTY", req.player, 1); //half penalty for first year only
                var penaltyRequest = await _leagueApi.AddSalaryAdjustment(req.leagueId, penaltyData, Utils.CurrentYear);
                var penRespString = await penaltyRequest.Content.ReadAsStringAsync();
                if (penRespString.ToUpper().Contains("ERROR"))
                {
                    var error = penRespString.XmlDeserializeFromString<MflXmlError>();
                    _logger.LogInformation(penRespString);
                    _logger.LogError("{mflPlayerId}'s buyout penalty salary adjustment was not added in mfl.", req.player.FullName);
                    await _gm.NotifyMflError(new BotMessage($"league: {req.leagueId} player: {req.player.FullName} could not properly apply buyout penalty salary adjustment.", botId));
                }
                await _pRepo.AddBuyoutPlayer(req);
                await _gm.SendBotNotification(message: new BotMessage($"New buyout submitted on fanpools.net\n{req.player.FullName} was cut.", botId));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Salary Adj mfl");
                return;
            }
        }

        public async Task ProposeMflTrade(TradeRequest req)
        {
            var hasSenderEats = req.SendingAssets.SelectMany(a => a.CapEats).Any();
            var hasReceiverEats = req.ReceivingAssets.SelectMany(a => a.CapEats).Any();

            var botId = Utils.leagueBotDict.TryGetValue(req.LeagueId, out var x) ? x : string.Empty;
            var now = DateTime.UtcNow;
            var comment = $"THIS TRADE INCLUDES SALARY CAP EATING. - - - "; 
              
            if (hasSenderEats) {
                comment = comment + $"{req.SenderTeamName} will eat these salary portions: - - - ";
                req.SendingAssets.ForEach(a =>
                {
                    if (!a.MflId.StartsWith("DP_") && !a.MflId.StartsWith("FP_") && a.CapEats.Any())
                    {
                        comment = comment + $"{a.PlayerDetails.FullName}: - - - ";
                        a.CapEats.ForEach(c =>
                        {
                            comment = comment + $"************ {c.Year}: ${c.Amount} - - - ";
                        });
                    }
                });
            }
            if (hasReceiverEats)
            {
                comment = comment + $"{req.ReceiverTeamName} will eat these salary portions: - - - ";
                req.ReceivingAssets.ForEach(a =>
                {
                    if (!a.MflId.StartsWith("DP_") && !a.MflId.StartsWith("FP_") && a.CapEats.Any())
                    {
                        comment = comment + $"{a.PlayerDetails.FullName}: - - - ";
                        a.CapEats.ForEach(c =>
                        {
                            comment = comment + $"************ {c.Year}: ${c.Amount} - - - ";
                        });
                    }
                });
            }

            comment = comment + $" * * * * If you would like to counter with a trade that involves salary cap eating, you'll need to go to fanpools.net";

            var sendingAssetIds = string.Join(",", req.SendingAssets.Select(a => a.MflId).ToList());
            var receivingAssetIds = string.Join(",", req.ReceivingAssets.Select(a => a.MflId).ToList());

            try
            {
                var tradeReqRes = await _leagueApi.SendTradeOffer(now.Year, req.LeagueId, req.ReceiverId.ToString("D4"),sendingAssetIds,receivingAssetIds, comment, req.Expires, req.SenderId.ToString("D4"));
                var respString = await tradeReqRes.Content.ReadAsStringAsync();
                if (respString.ToUpper().Contains("ERROR"))
                {
                    var error = respString.XmlDeserializeFromString<MflXmlError>();
                    _logger.LogInformation(respString);
                    _logger.LogError($"league: {req.LeagueId} Trade offer failed by {req.SenderTeamName}: {respString}");
                    await _gm.NotifyMflError(new BotMessage($"league: {req.LeagueId} Trade offer failed by {req.SenderTeamName}", botId));
                    throw new Exception($"league: {req.LeagueId} Trade offer failed by {req.SenderTeamName}: {respString}");
                }
                try
                {
                    await _gm.NotifyTradeOffer(new TradeOfferNotification { LeagueId = req.LeagueId, OfferedToFranchiseId = req.ReceiverId });
                }
                catch (Exception gmEx)
                {
                    _logger.LogError(gmEx, $"league: {req.LeagueId} GroupMe notify failed for trade by {req.SenderTeamName}");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "trade offer fail");
                throw;
            }
        }

        public async Task<PendingTradeResponse> GetMyPendingTrades(int leagueId, int mflFranchiseId)
        {
            var now = DateTimeOffset.UtcNow;
            var apiKey = _options.Value.Mfl.MflApiKey.First(k => k.id == leagueId).key;
            var pendingMflTradesRes = await _leagueApi.GetPendingTrades(leagueId, mflFranchiseId.ToString("D4"), now.Year, apiKey);
            var pendingTrades = pendingMflTradesRes.pendingTrades?.pendingTrade ?? new List<MflPendingTrade>();
            if (pendingTrades.Count == 0) 
            {
                return new PendingTradeResponse
                {
                    tradeRequests = new List<TradeRequest>()
                };
            }
            var dbCapEatsTask = _db.CapEatCandidates
                .Where(c => c.LeagueId == leagueId && pendingTrades.Select(pt => pt.expires).ToList()
                    .Contains(c.Proposal.Expires.ToString()))
                .ToListAsync();
            var lookupIds = pendingTrades.SelectMany(p => p.will_give_up.Split(","))
                    .Concat(pendingTrades.SelectMany(pt => pt.will_receive.Split(",")))
                    .Where(id => !id.StartsWith("FP_") && !id.StartsWith("DP_"));


            var tradePlayersTask =  _leagueApi.GetMflPlayerDetails(leagueId, string.Join(",", lookupIds), Utils.CurrentYear, GetApiKey(leagueId));
            var assetsTask = _leagueApi.GetFranchiseAssets(leagueId, now.Year, GetApiKey(leagueId));
            var rostersTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId, Utils.CurrentYear, GetApiKey(leagueId));
            try
            {
                await Task.WhenAll(tradePlayersTask, assetsTask, dbCapEatsTask, rostersTask);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }

            
            var dbPlayers = await _db.Players.Where(p => lookupIds.Contains(p.Mflid.ToString())).ToListAsync();
            var tradePlayers = tradePlayersTask.Result.players.player;
            var pickAssets = assetsTask.Result.assets.franchise.SelectMany(f => f.futureYearDraftPicks.draftPick)
                .Concat(assetsTask.Result.assets.franchise.SelectMany(f => f.currentYearDraftPicks.draftPick));
            var playerContracts = rostersTask.Result.rosters.franchise.SelectMany(f => f.player);
            var projectedFifthYearOptions = await GetProjectedFifthYearOptionSalaries(leagueId, rostersTask.Result.rosters.franchise);


            var returnList = pendingTrades.GroupJoin(dbCapEatsTask.Result,
                mfl => new { mfl.expires, mfl.offeringTeam }, 
                db => new { expires = db.Proposal.Expires.ToString(), offeringTeam = db.Proposal.SenderId.ToString("D4") }, 
                (mfl, db) => new TradeRequest
            {
                    TradeId = mfl.trade_id,
                    Expires = long.Parse(mfl.expires),
                    ReceiverId = int.Parse(mfl.offeredTo),
                    LeagueId = leagueId,
                    SenderId = int.Parse(mfl.offeringTeam),
                    ReceiverTeamName = mfl.offeredTo,
                    SenderTeamName = mfl.offeringTeam,
                    ReceivingAssets = mfl.will_receive.Split(",").Where(_ => !string.IsNullOrEmpty(_)).Select(a => new TradeOfferAsset
                    {
                        MflId = a,
                        PlayerDetails = (a.StartsWith("DP_") || a.StartsWith("FP_")) ?
                            new PlayerDTO { FullName = pickAssets.FirstOrDefault(asset => asset.pick == a).description } : 
                            tradePlayers.Where(t => t.id == a).Select(found => {

                                var dbFound = dbPlayers.FirstOrDefault(dbPlayer => dbPlayer.Mflid.ToString() == a);
                                var contract = playerContracts.FirstOrDefault(con => con.id == a);
                                return new PlayerDTO
                                {
                                    Headshot = dbFound.Headshot,
                                    Age = GetAgeInt(found.birthdate),
                                    FirstName = found.first_name,
                                    LastName = found.last_name,
                                    FullName = found.name,
                                    Length = int.TryParse(contract.contractYear, out var l) ? l : 0,
                                    Salary = int.TryParse(contract.salary, out var s) ? s : 0,
                                    MflId = int.Parse(a),
                                    Position = found.position,
                                    Team = found.team,
                                    ContractStatus = contract.contractStatus,
                                    ProjectedFifthYearOptionSalary = projectedFifthYearOptions.TryGetValue(a, out var opt) ? opt : (int?)null
                                };
                                }).FirstOrDefault(),
                        CapEats = db.Where(capEat => (capEat.EaterId == (int.TryParse(mfl.offeredTo, out var to) ? to : 0)) && capEat.MflPlayerId.ToString() == a).Select(capEat => new CapEat
                        {
                            Amount = capEat.CapAdjustment,
                            Year = capEat.Year,
                            EaterId = capEat.EaterId,
                            ReceiverId = capEat.ReceiverId,
                            MflId = capEat.MflPlayerId
                        }).ToList()
                    }).ToList(),
                    SendingAssets = mfl.will_give_up.Split(",").Where(_ => !string.IsNullOrEmpty(_)).Select(a => new TradeOfferAsset
                    {
                        MflId = a,
                        PlayerDetails = (a.StartsWith("DP_") || a.StartsWith("FP_")) ?
                        new PlayerDTO { FullName = pickAssets.FirstOrDefault(asset => asset.pick == a).description } :
                        tradePlayers.Where(t => t.id == a).Select(found => {

                            var dbFound = dbPlayers.FirstOrDefault(dbPlayer => dbPlayer.Mflid.ToString() == a);
                            var contract = playerContracts.FirstOrDefault(con => con.id == a);
                            return new PlayerDTO
                            {
                                Headshot = dbFound.Headshot,
                                Age = GetAgeInt(found.birthdate),
                                FirstName = found.first_name,
                                LastName = found.last_name,
                                FullName = found.name,
                                Length = int.TryParse(contract.contractYear, out var l) ? l : 0,
                                Salary = int.TryParse(contract.salary, out var s) ? s : 0,
                                MflId = int.Parse(a),
                                Position = found.position,
                                Team = found.team,
                                ContractStatus = contract.contractStatus,
                                ProjectedFifthYearOptionSalary = projectedFifthYearOptions.TryGetValue(a, out var opt) ? opt : (int?)null
                            };
                        }).FirstOrDefault(),
                        CapEats = db.Where(capEat => (capEat.EaterId == (int.TryParse(mfl.offeringTeam, out var to) ? to : 0) && capEat.MflPlayerId.ToString() == a)).Select(capEat => new CapEat
                        {
                            Amount = capEat.CapAdjustment,
                            Year = capEat.Year,
                            EaterId = capEat.EaterId,
                            ReceiverId = capEat.ReceiverId,
                            MflId = capEat.MflPlayerId
                        }).ToList()
                    }).ToList()


                });

            return new PendingTradeResponse
            {
                tradeRequests = returnList.ToList()
            };
        }

        public async Task ResponseToMflTrade( int year,  int leagueId,int tradeId,string response, string comments, string franchiseId)
        {
            var res = await _leagueApi.RespondToTrade(year, leagueId, tradeId, response, comments, franchiseId);
        }

        public async Task<List<PlayerEligibility>> GetHoldoutPlayers(int leagueId)
        {
            // need config for threshholds of position rankings
            // get all scores from mfl with YTD as W
            // get players from mfl 
            // get contracts from mfl
            var year = DateTime.UtcNow.Month < 8 ? Utils.CurrentYear - 1 : Utils.CurrentYear;
            var apiKey = _options.Value.Mfl.MflApiKey.FirstOrDefault(k => k.id == leagueId).key;
            if (apiKey == null) return new List<PlayerEligibility>();
            var scorePlayerTask = _leagueApi.GetMflPositionScoresByYear(leagueId, year, string.Empty, apiKey);
            var playerTask = _leagueApi.GetMflRostersForPlayerSalaries(leagueId, year, GetApiKey(leagueId));

            await Task.WhenAll(scorePlayerTask, playerTask);
            var scoresAndContracts = scorePlayerTask.Result.PlayerScores.PlayerScore
                .Join(
                    playerTask.Result.rosters.franchise.SelectMany(f =>
                        f.player.Select(p => new { Player = p, FranchiseId = f.id })),
                    s => s.Id,
                    fp => fp.Player.id,
                    (s, fp) => new { s, p = fp.Player, FranchiseId = fp.FranchiseId })
                .ToList();
            var playerDetails = await _leagueApi.GetMflPlayerDetails(leagueId, string.Join(",", scoresAndContracts.Select(s => s.p.id)), Utils.CurrentYear, GetApiKey(leagueId));

            var allJoined = scoresAndContracts.Join(playerDetails.players.player, s => s.s.Id, pd => pd.id,
                (sc, pd) => new ScoreContractPlayer { sc = new ScoreAndContract { s = sc.s, p = sc.p, franchiseId = int.TryParse(sc.FranchiseId, out var fid) ? fid : 0 } , pd =  pd }).ToList().OrderByDescending(_ => decimal.TryParse(_.sc.s.Score, out var ts) ? ts : 0);

            var qb = new HoldoutPosThreshhold("QB");
            var rb = new HoldoutPosThreshhold("RB");
            var wr = new HoldoutPosThreshhold("WR");
            var te = new HoldoutPosThreshhold("TE");
            var scoreThreshholds = new List<HoldoutPosThreshhold> { qb, rb, wr, te };
            foreach (var item in allJoined)
            {
                var posGroup = scoreThreshholds.FirstOrDefault(t => t.Pos == item.pd.position);
                if (posGroup == null) continue;
                if (posGroup.scores1st.Count < 12)
                {
                    posGroup.scores1st.Add(decimal.TryParse(item.sc.s.Score, out var ts) ? ts : 0);
                    continue;
                }
                else if (posGroup.scores2nd.Count < 12)
                {
                    posGroup.scores2nd.Add(decimal.TryParse(item.sc.s.Score, out var ts) ? ts : 0);
                }
                else if (posGroup.scores3rd.Count < 12)
                {
                    posGroup.scores3rd.Add(decimal.TryParse(item.sc.s.Score, out var ts) ? ts : 0);
                }
            }

            var qb2 = new HoldoutPosThreshhold("QB");
            var rb2 = new HoldoutPosThreshhold("RB");
            var wr2 = new HoldoutPosThreshhold("WR");
            var te2 = new HoldoutPosThreshhold("TE");
            var payThreshholds = new List<HoldoutPosThreshhold> { qb2, rb2, wr2, te2 };
            var payOrder = allJoined.OrderByDescending(_ => int.TryParse(_.sc.p.salary, out var sal) ? sal : 1);
            foreach (var item in payOrder)
            {
                var posGroup = payThreshholds.FirstOrDefault(t => t.Pos == item.pd.position);
                if (posGroup == null) continue;
                if (posGroup.scores1st.Count < 12)
                {
                    posGroup.scores1st.Add(decimal.TryParse(item.sc.p.salary, out var ts) ? ts : 0);
                    continue;
                }
                else if (posGroup.scores2nd.Count < 12)
                {
                    posGroup.scores2nd.Add(decimal.TryParse(item.sc.p.salary, out var ts) ? ts : 0);
                }
                else if (posGroup.scores3rd.Count < 12)
                {
                    posGroup.scores3rd.Add(decimal.TryParse(item.sc.p.salary, out var ts) ? ts : 0);
                }
                else if (posGroup.scores4th.Count < 12)
                {
                    posGroup.scores4th.Add(decimal.TryParse(item.sc.p.salary, out var ts) ? ts : 0);
                }
            }

            var franchiseIds = Enumerable.Range(1, 12).Select(i => i.ToString("D4")).ToArray();
            var calculator = new HoldoutEligibilityCalculator();
            var eligiblePlayers = calculator.GetEligiblePlayers(scoreThreshholds, payThreshholds, allJoined);

            return eligiblePlayers;
        }

        public async Task<List<HoldoutDTO>> GenerateAndSaveHoldouts(int leagueId, int year)
        {
            //TODO: check year here and only do next year if it is the last half of the year,
            // Check if holdouts already exist for this league and year
            var existingHoldouts = await _pRepo.GetHoldoutsForLeague(leagueId, year + 1);
            if (existingHoldouts.Any())
            {
                _logger.LogWarning($"Holdouts already exist for league {leagueId} year {year}");
                return new List<HoldoutDTO>();
            }

            // Get all eligible players
            var eligiblePlayers = await GetHoldoutPlayers(leagueId);

            // Group by franchise and select the player with highest raise for each
            var holdoutsByFranchise = eligiblePlayers
                .GroupBy(p => p.FranchiseId)
                .Select(g => g.OrderByDescending(p => p.HoldoutSalary - p.CurrentSalary).First())
                .ToList();

            // Get league owners to map franchise ID to league owner ID
            var leagueOwners = await _db.LeagueOwners
                .Where(lo => lo.Leagueid == leagueId)
                .ToListAsync();

            var savedHoldouts = new List<HoldoutDTO>();

            foreach (var player in holdoutsByFranchise)
            {
                var leagueOwner = leagueOwners.FirstOrDefault(lo => lo.Mflfranchiseid == player.FranchiseId);
                if (leagueOwner == null)
                {
                    _logger.LogWarning($"Could not find league owner for franchise {player.FranchiseId}");
                    continue;
                }

                // Get player details from database
                var dbPlayer = await _pRepo.GetPlayerById(int.Parse(player.PlayerId));
                if (dbPlayer == null)
                {
                    _logger.LogWarning($"Could not find player {player.PlayerId} in database");
                    continue;
                }

                var holdout = new Holdout
                {
                    LeagueId = leagueId,
                    LeagueOwnerId = leagueOwner.Leagueownerid,
                    Year = year + 1,
                    PlayerId = int.Parse(player.PlayerId),
                    OriginalSalary = player.CurrentSalary,
                    HoldoutSalary = player.HoldoutSalary,
                    Status = "Pending",
                    ScoreTier = player.ScoreTier,
                    SalaryComparison = player.SalaryComparison,
                    YearsRemaining = 0 // Will be set from MFL data
                };

                // Get years remaining from MFL roster data
                var roster = await _leagueApi.GetMflRostersForPlayerSalaries(leagueId, year, GetApiKey(leagueId));
                var franchiseRoster = roster.rosters.franchise.FirstOrDefault(f => int.Parse(f.id) == player.FranchiseId);
                Player playerContract = null;
                if (franchiseRoster != null)
                {
                    playerContract = franchiseRoster.player.FirstOrDefault(p => p.id == player.PlayerId);
                    if (playerContract != null)
                    {
                        holdout.YearsRemaining = int.TryParse(playerContract.contractYear, out var years) ? Math.Max(0, years - 1) : 0;
                    }
                }

                await _pRepo.AddHoldout(holdout);

                // Flag on MFL that this player is now on the holdout clock — a pure
                // annotation, so salary/length are preserved exactly as they are today.
                // One player's tag failing must not stop the rest of the batch.
                if (playerContract != null)
                {
                    try
                    {
                        var combinedTags = AddTag(playerContract.contractStatus, "HOLDOUT");
                        var preservedSalary = int.TryParse(playerContract.salary, out var sal) ? sal : holdout.OriginalSalary;
                        var preservedContractYear = int.TryParse(playerContract.contractYear, out var cy) ? cy : holdout.YearsRemaining + 1;
                        await GiveNewContractToPlayer(leagueId, holdout.PlayerId, preservedSalary, preservedContractYear,
                            $"[holdout generated] {dbPlayer.Fullname} -> {combinedTags}", contractStatus: combinedTags, announceOnSuccess: false);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to tag HOLDOUT contractStatus for league {leagueId} player {playerId}", leagueId, holdout.PlayerId);
                    }
                }

                savedHoldouts.Add(new HoldoutDTO
                {
                    Id = holdout.Id,
                    LeagueId = holdout.LeagueId,
                    LeagueOwnerId = holdout.LeagueOwnerId,
                    Year = holdout.Year,
                    Player = _mapper.Map<PlayerDTO>(dbPlayer),
                    OriginalSalary = holdout.OriginalSalary,
                    HoldoutSalary = holdout.HoldoutSalary,
                    Status = holdout.Status,
                    ScoreTier = holdout.ScoreTier,
                    SalaryComparison = holdout.SalaryComparison,
                    YearsRemaining = holdout.YearsRemaining
                });
            }

            var botId = Utils.leagueBotDict.TryGetValue(leagueId, out var x) ? x : string.Empty;
            await _gm.SendBotNotification(new BotMessage($"{savedHoldouts.Count} holdouts generated for {year} season.", botId));

            return savedHoldouts;
        }

        private int GetTop3TagValueFromPosition(string position, FranchiseTagLeague l)
        {
            switch (position.ToUpper())
            {
                case "QB":
                    return l.QBTop3;
                case "RB":
                    return l.RBTop3;
                case "WR":
                    return l.WRTop3;
                case "TE":
                    return l.TETop3;
                default:
                    return 0;
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

        private Dictionary<string, string> CreateBodyDataForNewSalaryAdj(string franchiseId, double adjustmentAmount, string reason, PlayerDTO player, int length)
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
        //private Dictionary<string, string> CreateBodyDataForNewTradeProposal(string franchiseId, string offeredTo, string willGiveUp, string willReceive, string comments,string franchiseId )
        //{
        //    var ret = new Dictionary<string, string>()
        //    {
        //        {
        //            "DATA",
        //            $"<?xml version='1.0' encoding='UTF-8' ?><salary_adjustments><salary_adjustment franchise_id=\"{franchiseId}\" amount=\"{adjustmentAmount}\" explanation=\"{reason} {player.LastName}, {player.FirstName} {player.Team} {player.Position} (Salary: ${player.Salary}, years left: {length})\"/></salary_adjustments>"
        //        }
        //    };
        //    return ret;
        //}
        private Dictionary<string, string> CreateBodyDataForNewContract(int playerId, int salary, int length = 1, string contractStatus = null)
        {
            // null = don't touch contractStatus at all (attribute omitted, MFL's APPEND=1 keeps
            // whatever is already there). "" = explicitly write an empty value to clear it.
            var statusAttr = contractStatus == null
                ? ""
                : $" contractStatus=\"{System.Security.SecurityElement.Escape(contractStatus)}\"";
            var ret = new Dictionary<string, string>()
            {
                {
                    "DATA",
                    $"<?xml version='1.0' encoding='UTF-8' ?><salaries><leagueUnit unit=\"LEAGUE\"><player id=\"{playerId}\" salary=\"{salary}\" contractYear=\"{length}\"{statusAttr}/></leagueUnit></salaries>"
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

        public async Task<FranchiseTagLeague> GenerateFranchiseTagValues(int leagueId, int year)
        {
            var salariesRoot = await _leagueApi.GetSalaries(leagueId, year - 1, GetApiKey(leagueId));
            var mflPlayers = salariesRoot.Salaries.LeagueUnit.Player;
            var mflIds = mflPlayers
                .Select(p => int.TryParse(p.id, out var id) ? id : 0)
                .Where(id => id != 0)
                .ToList();
            var dbPlayers = (await _pRepo.GetPlayersByListOfIds(mflIds))?.ToList() ?? new List<PlayerEntity>();

            var joined = mflPlayers
                .Join(dbPlayers,
                    mfl => int.TryParse(mfl.id, out var id) ? id : 0,
                    db => db.Mflid,
                    (mfl, db) => new { Position = db.Position?.ToUpper(), Salary = int.TryParse(mfl.salary, out var s) ? s : 0 })
                .Where(x => x.Salary > 0)
                .ToList();

            int Top6Avg(string pos) => (int)Math.Round(joined.Where(x => x.Position == pos).Select(x => x.Salary).OrderByDescending(s => s).Take(6).DefaultIfEmpty(0).Average());
            int Top3Avg(string pos) => (int)Math.Round(joined.Where(x => x.Position == pos).Select(x => x.Salary).OrderByDescending(s => s).Take(3).DefaultIfEmpty(0).Average());

            var entity = new FranchiseTagLeague
            {
                Mflleagueid = leagueId,
                Year = year,
                QB = Top6Avg("QB"),
                RB = Top6Avg("RB"),
                WR = Top6Avg("WR"),
                TE = Top6Avg("TE"),
                QBTop3 = Top3Avg("QB"),
                RBTop3 = Top3Avg("RB"),
                WRTop3 = Top3Avg("WR"),
                TETop3 = Top3Avg("TE")
            };

            await _pRepo.UpsertLeagueTagInfo(entity);
            return entity;
        }

        public async Task<Dictionary<string, List<FutureDraftPickDTO>>> GetFutureDraftPicksForLeague(int leagueId)
        {
            var result = await _leagueApi.GetFutureDraftPicks(leagueId, Utils.CurrentYear, GetApiKey(leagueId));
            return (result?.futureDraftPicks?.franchise ?? new List<FutureDraftFranchise>())
                .ToDictionary(
                    f => f.id,
                    f => f.futureDraftPick.Select(p => new FutureDraftPickDTO
                    {
                        Year = p.year,
                        Round = p.round,
                        OriginalPickFor = p.originalPickFor,
                        Description = p.description
                    }).ToList()
                );
        }

        public async Task<List<TradeBaitDTO>> GetTradeBaitForLeague(int leagueId)
        {
            var apiKey = _options.Value.Mfl.MflApiKey.First(k => k.id == leagueId).key;
            var result = await _leagueApi.GetTradeBait(leagueId, apiKey, Utils.CurrentYear);
            return (result?.tradeBaits?.tradeBait ?? new List<TradeBait>())
                .Select(tb => new TradeBaitDTO
                {
                    FranchiseId = tb.franchise_id,
                    WillGiveUp = tb.willGiveUp,
                    InExchangeFor = tb.inExchangeFor
                }).ToList();
        }

        public async Task<List<RecentMoveDTO>> GetRecentMoves(int leagueId, int limit = 20)
        {
            var apiKey = GetApiKey(leagueId);
            var year = Utils.CurrentYear;
            var transactionsTask = _leagueApi.GetLastYearWaiverTransactions(leagueId, apiKey, year);
            var salariesTask = _leagueApi.GetSalaries(leagueId, year, apiKey);
            await Task.WhenAll(transactionsTask, salariesTask);

            var rawTxns = transactionsTask.Result?.transactions?.transaction ?? new List<MflTransaction>();
            var moves = ParseRecentMoves(rawTxns)
                .OrderByDescending(m => m.Timestamp)
                .Take(limit)
                .ToList();

            if (!moves.Any()) return moves;

            var ids = moves.Select(m => m.MflPlayerId).Distinct().ToList();
            var players = (await _pRepo.GetPlayersByListOfIds(ids))?.ToDictionary(p => p.Mflid)
                          ?? new Dictionary<int, PlayerEntity>();

            var salaryMap = (salariesTask.Result?.Salaries?.LeagueUnit?.Player ?? new List<Player>())
                .Where(p => int.TryParse(p.id, out _))
                .GroupBy(p => int.Parse(p.id))
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var m in moves)
            {
                if (players.TryGetValue(m.MflPlayerId, out var pe))
                {
                    m.PlayerName = pe.Fullname ?? $"#{m.MflPlayerId}";
                    m.Position = pe.Position ?? "";
                    m.Team = pe.Team ?? "";
                }
                else
                {
                    m.PlayerName = $"#{m.MflPlayerId}";
                }

                if (m.Action == "ADD" && salaryMap.TryGetValue(m.MflPlayerId, out var s))
                {
                    if (int.TryParse(s.salary, out var sv)) m.Salary = sv;
                    if (int.TryParse(s.contractYear, out var yv)) m.Years = yv;
                }
            }

            return moves;
        }

        // Parses MFL transactions into per-player ADD/DROP rows for the Recent Moves feed.
        // Formats:
        //   FREE_AGENT:  "<adds>|<drops>"                 e.g. "|14840," = drop 14840; "14073,|" = add 14073
        //   BBID_WAIVER: "<wonId>,|<bid>|<droppedId>,"    bid ignored
        //   IR:          activated/deactivated comma-separated id lists
        //   TAXI:        promoted/demoted comma-separated id lists
        //   TRADE:       franchise1_gave_up / franchise2_gave_up — numeric tokens are players,
        //                DP_*/FP_* are picks (skipped). Each player swap → DROP for giver + ADD for receiver.
        // Filter: by_commish=="1" with more than 3 player ids in the transaction string → skipped (bulk reset noise).
        public static List<RecentMoveDTO> ParseRecentMoves(List<MflTransaction> raw)
        {
            var moves = new List<RecentMoveDTO>();
            if (raw == null) return moves;

            foreach (var t in raw)
            {
                if (t == null) continue;
                if (!long.TryParse(t.timestamp, out var ts)) continue;
                var when = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;

                switch (t.type)
                {
                    case "FREE_AGENT":
                    {
                        if (string.IsNullOrEmpty(t.transaction)) break;
                        if (!int.TryParse(t.franchise, out var franchiseId)) break;
                        var parts = t.transaction.Split('|');
                        var added = parts.Length > 0 ? parts[0] : "";
                        var dropped = parts.Length > 1 ? parts[1] : "";
                        var addIds = ParseIds(added).ToList();
                        var dropIds = ParseIds(dropped).ToList();
                        if (t.by_commish == "1" && (addIds.Count + dropIds.Count) > 3) break;
                        foreach (var pid in addIds)
                            moves.Add(new RecentMoveDTO { Timestamp = when, FranchiseId = franchiseId, Action = "ADD", MflPlayerId = pid });
                        foreach (var pid in dropIds)
                            moves.Add(new RecentMoveDTO { Timestamp = when, FranchiseId = franchiseId, Action = "DROP", MflPlayerId = pid });
                        break;
                    }
                    case "BBID_WAIVER":
                    {
                        if (string.IsNullOrEmpty(t.transaction)) break;
                        if (!int.TryParse(t.franchise, out var franchiseId)) break;
                        var parts = t.transaction.Split('|');
                        var won = parts.Length > 0 ? parts[0] : "";
                        var dropped = parts.Length > 2 ? parts[2] : "";
                        foreach (var pid in ParseIds(won))
                            moves.Add(new RecentMoveDTO { Timestamp = when, FranchiseId = franchiseId, Action = "ADD", MflPlayerId = pid });
                        foreach (var pid in ParseIds(dropped))
                            moves.Add(new RecentMoveDTO { Timestamp = when, FranchiseId = franchiseId, Action = "DROP", MflPlayerId = pid });
                        break;
                    }
                    case "IR":
                    {
                        if (!int.TryParse(t.franchise, out var franchiseId)) break;
                        foreach (var pid in ParseIds(t.activated))
                            moves.Add(new RecentMoveDTO { Timestamp = when, FranchiseId = franchiseId, Action = "ADD", MflPlayerId = pid });
                        foreach (var pid in ParseIds(t.deactivated))
                            moves.Add(new RecentMoveDTO { Timestamp = when, FranchiseId = franchiseId, Action = "DROP", MflPlayerId = pid });
                        break;
                    }
                    case "TAXI":
                    {
                        if (!int.TryParse(t.franchise, out var franchiseId)) break;
                        foreach (var pid in ParseIds(t.promoted))
                            moves.Add(new RecentMoveDTO { Timestamp = when, FranchiseId = franchiseId, Action = "ADD", MflPlayerId = pid });
                        foreach (var pid in ParseIds(t.demoted))
                            moves.Add(new RecentMoveDTO { Timestamp = when, FranchiseId = franchiseId, Action = "DROP", MflPlayerId = pid });
                        break;
                    }
                    case "TRADE":
                    {
                        if (!int.TryParse(t.franchise, out var f1)) break;
                        if (!int.TryParse(t.franchise2, out var f2)) break;
                        foreach (var pid in ParseIds(t.franchise1_gave_up))
                        {
                            moves.Add(new RecentMoveDTO { Timestamp = when, FranchiseId = f1, Action = "DROP", MflPlayerId = pid });
                            moves.Add(new RecentMoveDTO { Timestamp = when, FranchiseId = f2, Action = "ADD", MflPlayerId = pid });
                        }
                        foreach (var pid in ParseIds(t.franchise2_gave_up))
                        {
                            moves.Add(new RecentMoveDTO { Timestamp = when, FranchiseId = f2, Action = "DROP", MflPlayerId = pid });
                            moves.Add(new RecentMoveDTO { Timestamp = when, FranchiseId = f1, Action = "ADD", MflPlayerId = pid });
                        }
                        break;
                    }
                }
            }
            return moves;
        }

        private static IEnumerable<int> ParseIds(string segment)
        {
            if (string.IsNullOrEmpty(segment)) yield break;
            foreach (var token in segment.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(token, out var id)) yield return id;
        }
    }
    public class HoldoutPosThreshhold
    {
        public string Pos { get; set; }
        public List<decimal> scores1st = new List<decimal>();
        public List<decimal> scores2nd = new List<decimal>();
        public List<decimal> scores3rd = new List<decimal>();
        public List<decimal> scores4th = new List<decimal>();

        public HoldoutPosThreshhold()
        {
            
        }
        public HoldoutPosThreshhold(string pos)
        {
            Pos = pos;
        }
    }

    public class MflRosterResponse
    {
        [XmlElement("error")] public string Error { get; set; }
    }
    public class ScoreAndContract
    {
        public int franchiseId { get; set; }
        public PlayerScore s { get; set; }
        public Player p { get; set; } 
    }
    public class ScoreContractPlayer
    {
        public ScoreAndContract sc { get; set; }
        public MflPlayerDetails pd { get; set; }
    }
    public class HoldoutEligibilityCalculator
    {
        private const decimal MINIMUM_RAISE = 3m;
        private const decimal MAXIMUM_RAISE = 10m;
        private const decimal RAISE_PERCENTAGE = 0.20m;

        // Dictionary format: Position -> (ScoreTierToCheck, SalaryTierToCompareAgainst)
        private readonly Dictionary<(string Position, int ScoreTier), int> _positionRules = new()
    {
        { ("QB", 1), 2 },  // QB1 (top 12 scores) compared against QB2 (next 12) median salary
        { ("RB", 1), 2 },  // RB1 (top 12 scores) compared against RB2 (next 12) median salary
        { ("RB", 2), 3 },  // RB2 (13-24 scores) compared against RB3 (25-36) median salary
        { ("WR", 1), 2 },  // WR1 (top 12 scores) compared against WR2 (next 12) median salary
        { ("WR", 2), 3 },  // WR2 (13-24 scores) compared against WR3 (25-36) median salary
        { ("WR", 3), 4 },  // WR3 (25-36 scores) compared against WR4 (37-48) median salary
    };

        public List<PlayerEligibility> GetEligiblePlayers(
            List<HoldoutPosThreshhold> scoreThresholds,
            List<HoldoutPosThreshhold> payThresholds,
            IEnumerable<ScoreContractPlayer> players)
        {
            var eligiblePlayers = new List<PlayerEligibility>();

            foreach (var player in players)
            {

                var position = player.pd.position;
                if (position == "TE") continue; // TE's are not eligible for holdout
                if (eligiblePlayers.FirstOrDefault(ep => ep.FranchiseId == player.sc.franchiseId) != null) continue; // Only one player per franchise
                
                // Check years remaining - must have more than 1 year left on contract
                int yearsRemaining = int.TryParse(player.sc.p.contractYear, out var years) ? years : 0;
                if (yearsRemaining <= 1) continue; // Players with 1 or 0 years left will be free agents
                
                decimal playerScore = decimal.TryParse(player.sc.s.Score, out var score) ? score : 0;
                int playerSalary = int.TryParse(player.sc.p.salary, out var salary) ? salary : 0;

                // Get the score and salary thresholds for this position
                var posScoreThreshold = scoreThresholds.First(t => t.Pos == position);
                var posSalaryThreshold = payThresholds.First(t => t.Pos == position);

                // Get player's score tier
                var scoreTier = GetScoreTier(position, playerScore, posScoreThreshold);

                // Check if this position/tier combination is eligible for holdout
                if (!_positionRules.TryGetValue((position, scoreTier), out var salaryTierToCompare))
                    continue;

                // Get the median salary of the comparison tier
                var salaryThresholdMedian = GetSalaryTierMedian(posSalaryThreshold, salaryTierToCompare);

                // Player is eligible if their salary is below the comparison tier's median
                if (playerSalary < salaryThresholdMedian)
                {
                    var holdoutSalary = CalculateHoldoutSalary(playerSalary);
                    eligiblePlayers.Add(new PlayerEligibility
                    {
                        FranchiseId = player.sc.franchiseId,
                        PlayerId = player.pd.id,
                        Name = player.pd.name,
                        Position = position,
                        Score = playerScore,
                        CurrentSalary = playerSalary,
                        HoldoutSalary = holdoutSalary,
                        ScoreTier = scoreTier,
                        SalaryComparison = salaryThresholdMedian
                    });
                }
            }

            return eligiblePlayers;
        }
        private int CalculateHoldoutSalary(int currentSalary)
        {
            // Calculate 20% raise and round to integer
            var raise = Math.Round(currentSalary * RAISE_PERCENTAGE, 0);

            // Ensure minimum $3 raise
            raise = Math.Max(raise, MINIMUM_RAISE);

            // Cap raise at $10
            raise = Math.Min(raise, MAXIMUM_RAISE);

            return currentSalary + (int)raise;
        }
        private int GetScoreTier(string position, decimal playerScore, HoldoutPosThreshhold threshold)
        {
            if (threshold.scores1st.Contains(playerScore)) return 1;
            if (threshold.scores2nd.Contains(playerScore)) return 2;
            if (threshold.scores3rd.Contains(playerScore)) return 3;
            return 4;
        }

        private decimal GetSalaryTierMedian(HoldoutPosThreshhold threshold, int tier)
        {
            var salaries = tier switch
            {
                1 => threshold.scores1st,
                2 => threshold.scores2nd,
                3 => threshold.scores3rd,
                4 => threshold.scores4th,
                _ => new List<decimal>()
            };

            if (!salaries.Any()) return 0;

            var sortedSalaries = salaries.OrderBy(x => x).ToList();
            var mid = sortedSalaries.Count / 2;

            return sortedSalaries.Count % 2 == 0
                ? (sortedSalaries[mid - 1] + sortedSalaries[mid]) / 2
                : sortedSalaries[mid];
        }

    }

    public class PlayerEligibility
    {
        public int FranchiseId { get; set; }
        public string PlayerId { get; set; }
        public string Name { get; set; }
        public string Position { get; set; }
        public decimal Score { get; set; }
        public int CurrentSalary { get; set; }
        public int HoldoutSalary { get; set; }
        public int ScoreTier { get; set; }
        public decimal SalaryComparison { get; set; }
    }
}