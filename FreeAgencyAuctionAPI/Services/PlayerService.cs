using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IPlayerService
    {
        public Task<PlayerDTO> GetPlayerById(int id);
        public Task<List<PlayerDTO>> GetRosteredPlayers(int leagueId);
        //public Task<PlayerDTO> NominatePlayer(PlayerDTO player);
        Task<List<PlayerDTO>> GetAllPlayers();
        public Task<List<PlayerDTO>> GetAllFreeAgents(int leagueId);
        //Task LoadAllFreeAgentsIntoDb(List<PlayerEntity> players);
        //Task UpdateTeamsAndHeadshotsInDb(List<PlayerEntity> teamChangeList);
        Task<int> GetSuggestedSalary(PlayerTipRequestDTO tip);
    }
    public class PlayerService : IPlayerService
    {
        private readonly IGlobalMflApi _global;
        private readonly IPlayerRepo _repo;
        private readonly IMapper _mapper;
        private readonly ISharkApi _sharkApi;
        private readonly IMflApi _mflApi;
        private readonly IOptionsSnapshot<AppConfig> _options;

        public PlayerService(IPlayerRepo playerRepo, IMapper mapper, ISharkApi sharkApi, IMflApi mflApi, IGlobalMflApi global, IOptionsSnapshot<AppConfig> options)
        {
            _global = global;
            _repo = playerRepo;
            _mapper = mapper;
            _sharkApi = sharkApi;
            _mflApi = mflApi;
            _options = options;
        }

        private string GetApiKey(int leagueId) =>
            _options.Value.Mfl?.MflApiKey?.FirstOrDefault(k => k.id == leagueId)?.key ?? string.Empty;

        public async Task<PlayerDTO> GetPlayerById(int id)
        {
            var entity = await _repo.GetPlayerById(id);
            if (entity == null) return null;
            return _mapper.Map<PlayerEntity, PlayerDTO>(entity);
        }

        public async Task<List<PlayerDTO>> GetRosteredPlayers(int leagueId)
        {
            var entities = await _repo.GetRosteredPlayers(leagueId);
            if (entities == null) return null;
            return _mapper.Map<List<PlayerDTO>>(entities);
        }

/*        public async Task<PlayerDTO> NominatePlayer(PlayerDTO player)
        {
            var owned = _mapper.Map<PlayerDTO, PlayerEntity>(player);
            var ret = await _repo.SetPlayerOwner(owned);
            return _mapper.Map<PlayerEntity, PlayerDTO>(ret);
        }*/

        public async Task<List<PlayerDTO>> GetAllPlayers()
        {
            var freeAgents = await _repo.GetAllPlayers(); 
            return _mapper.Map<List<PlayerDTO>>(freeAgents);
        }
        public async Task<List<PlayerDTO>> GetAllFreeAgents(int leagueId)
        {
            if (leagueId < 0)
            {
                var demoPlayers = await _repo.GetAllFreeAgents(leagueId);
                return demoPlayers.Select(f => new PlayerDTO
                {
                    FullName = f.Fullname,
                    Team = f.Team,
                    Position = f.Position,
                    MflId = f.Mflid,
                    Headshot = f.Headshot,
                    Age = f.Age,
                    FirstName = f.Firstname,
                    LastName = f.Lastname
                }).ToList();
            }

            var freeAgentMflIdsRootTask =  _mflApi.GetMflFreeAgents(leagueId, Utils.CurrentYear, GetApiKey(leagueId));
            var adpPlayerRootTask =  _global.GetMflAdp(Utils.CurrentYear);
            await Task.WhenAll(freeAgentMflIdsRootTask, adpPlayerRootTask);
            if (freeAgentMflIdsRootTask.Result.error != null) return new List<PlayerDTO>();
            var adpPlayers = adpPlayerRootTask.Result.adp.player;
            var freeAgentMflIds = freeAgentMflIdsRootTask.Result.freeAgents.leagueUnit.player.Select(p => int.Parse(p.id));
            var freeAgents = await _repo.GetPlayersByMflIds(freeAgentMflIds);

            //var freeAgents = await _repo.GetAllFreeAgents(leagueId);
            var unsorted = freeAgents.Select(f => new PlayerDTO
            {
                FullName = f.Fullname,
                Team = f.Team,
                Position = f.Position,
                MflId = f.Mflid,
                Headshot = f.Headshot,
                Age = f.Age,
                FirstName = f.Firstname, 
                LastName = f.Lastname
                
            });
            var addedADP = unsorted.GroupJoin(adpPlayers, dto => dto.MflId, adp => int.TryParse(adp.id, out var p) ? p : -1, (dto, adp) => {
                var tempAdp = adp.SingleOrDefault()?.rank;
                dto.Adp = decimal.TryParse(tempAdp, out var y) ? y : null;
                return dto;
                }).ToList();
            return addedADP;
        }

        public async Task<int> GetSuggestedSalary(PlayerTipRequestDTO tip)
        {
            var yearSugg = new int[] { 1, 3 };
            var projections = await _sharkApi.GetSharkProjectionsByPosition(tip.Position);
            var player = projections.FirstOrDefault(p => p.ID == tip.MflId);
            var isImpactStarter = (tip.Position == "QB" && player.Rank < 16) || (tip.Position == "RB" && player.Rank < 33) ||
                              (tip.Position == "WR" && player.Rank < 37) || (tip.Position == "TE" && player.Rank < 11);
            if (player == null)
            {
                // return null or record 1 in the db for this player
                return -1;
            }
            var positionRange = Utils.PositionRanges.First(pos =>
                pos.Position == tip.Position && (pos.RankMax >= player.Rank && pos.RankMin <= player.Rank));
            if (positionRange.SalaryUpper == 1)
            {
                await _repo.AddTipToDb(tip.MflId, tip.OwnerId, 1);
                return 1;
            }
            var playerRangeLevel = player.Rank % 12;
            if (playerRangeLevel == 0) playerRangeLevel = 12;  // lower is better except 0. 0 is worst so make it 12
            playerRangeLevel -= 1;
            var percentile = 1 - (playerRangeLevel / 12.0);
            var operatingRange = positionRange.SalaryUpper - positionRange.SalaryMed;
            var percentileOnRange = operatingRange * percentile;
            var subtotal = percentileOnRange + positionRange.SalaryMed;

            var ageCliff = Utils.AgeCliffs[tip.Position];
            var ageMultiplier = 1.0;
            if (tip.Age > ageCliff.High)
            {
                // keep years short, penalize with age multiplier
                yearSugg[1] = tip.Position == "QB" ? 3 : 2;
                ageMultiplier -= isImpactStarter ? 0.03 : 0.05;
                // if you are over the bonus, shorten years more, but dont penalize more if an impact starter
                if (tip.Age >= ageCliff.High + ageCliff.BonusThreshhold)
                {
                    yearSugg[1] -= 1;
                    ageMultiplier -= isImpactStarter ? 0 : 0.05;
                }
            }
            else if (tip.Age < ageCliff.Low)
            {
                // should be min 2 years unless they are $1
                if (subtotal > 1) yearSugg[0] = 2;
                // 
                ageMultiplier += isImpactStarter ? 0.05 : 0.03;

                if (tip.Age <= ageCliff.Low - ageCliff.BonusThreshhold)
                {
                    // if impact starter and younger than bonus?? shiiit make a 3-4 (5 if that salary is high enough)
                    ageMultiplier += isImpactStarter ? 0.03 : 0;
                    yearSugg[1] = isImpactStarter ? 5 : 4;
                    if (tip.Position == "RB") yearSugg[1] -= 1;
                }
            }
            // salaries under 35 can only go up to 3
            // salaries under 20 can only go up to 2

            var rnd = new Random();
            var variance = 1 + (rnd.Next(-30, 30) * .001);

            var salary = (int)Math.Round((subtotal * ageMultiplier) * variance);
            if (salary < 40 && yearSugg[1] > 3) yearSugg[1] = 3;
            if (salary < 25 && yearSugg[1] > 2) yearSugg[1] = 2;
            // TODO: check 2nd highest team cap space, if salary is way over that.. lower the salary

            await _repo.AddTipToDb(tip.MflId, tip.OwnerId, salary);
            return salary;
            // 2 years or more younger than RB age cliff ? + 10%
            // 1 year younger + 5
            // 2 years over -10%


        }

        private List<SharkPlayerProjection> getLocalProjectionByPosition(string tipPosition)
        {
            switch (tipPosition.ToUpper())
            {
                case "QB":
                    return parseLocalProjectionByPosition("qb");
                case "RB":
                    return parseLocalProjectionByPosition("rb");
                case "WR":
                    return parseLocalProjectionByPosition("wr");
                case "TE":
                    return parseLocalProjectionByPosition("te");
                default:
                    return new List<SharkPlayerProjection>();
            }
        }

        private List<SharkPlayerProjection> parseLocalProjectionByPosition(string pos)
        {
            using (StreamReader file = File.OpenText($"{pos}.json"))
            {
                JsonSerializer serializer = new JsonSerializer();
                var rawProjections = (List<SharkPlayerProjection>)serializer.Deserialize(file, typeof(List<SharkPlayerProjection>));

                return rawProjections;
            }
        }
    }
}