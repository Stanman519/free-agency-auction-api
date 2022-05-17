using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IPlayerServiceLayer
    {
        public Task<PlayerDTO> GetPlayerById(string id);
        public Task<List<PlayerDTO>> GetRosteredPlayers();
        public Task<PlayerDTO> NominatePlayer(PlayerDTO player);
        Task<List<PlayerDTO>> GetAllPlayers();
        public Task<PlayerDTO> WinPlayer(BidDTO bid);
        public Task<List<PlayerDTO>> GetAllFreeAgents();
        Task LoadAllFreeAgentsIntoDb(List<PlayerEntity> players);
        Task UpdateTeamsAndHeadshotsInDb(List<PlayerEntity> teamChangeList);
        Task<int> GetSuggestedSalary(PlayerTipRequestDTO tip);
    }
    public class PlayerServiceLayer : IPlayerServiceLayer
    {
        private readonly IPlayerRepo _repo;
        private readonly IMapper _mapper;
        private readonly ISharkApi _sharkApi;

        public PlayerServiceLayer(IPlayerRepo playerRepo, IMapper mapper, ISharkApi sharkApi)
        {
            _repo = playerRepo;
            _mapper = mapper;
            _sharkApi = sharkApi;
        }

        public async Task<PlayerDTO> GetPlayerById(string id)
        {
            var entity = await _repo.GetPlayerById(id);
            if (entity == null) return null;
            return _mapper.Map<PlayerEntity, PlayerDTO>(entity);
        }

        public async Task<List<PlayerDTO>> GetRosteredPlayers()
        {
            var entities = await _repo.GetRosteredPlayers();
            if (entities == null) return null;
            return _mapper.Map<List<PlayerEntity>, List<PlayerDTO>>(entities);
        }

        public async Task<PlayerDTO> NominatePlayer(PlayerDTO player)
        {
            var owned = _mapper.Map<PlayerDTO, PlayerEntity>(player);
            var ret = await _repo.SetPlayerOwner(owned);
            return _mapper.Map<PlayerEntity, PlayerDTO>(ret);
        }

        public async Task<PlayerDTO> WinPlayer(BidDTO bid)
        {
            var owned = await _repo.WinPlayer(_mapper.Map<BidDTO, BidEntity>(bid));
            return _mapper.Map<PlayerEntity, PlayerDTO>(owned);
        }

        public async Task<List<PlayerDTO>> GetAllPlayers()
        {
            var freeAgents = await _repo.GetAllPlayers();
            return _mapper.Map<List<PlayerEntity>, List<PlayerDTO>>(freeAgents);
        }
        public async Task<List<PlayerDTO>> GetAllFreeAgents()
        {
            var freeAgents = await _repo.GetAllFreeAgents();
            return _mapper.Map<List<PlayerEntity>, List<PlayerDTO>>(freeAgents);
        }

        public async Task LoadAllFreeAgentsIntoDb(List<PlayerEntity> players)
        {
            await _repo.AddFreshPlayerInventory(players);
        }

        public async Task UpdateTeamsAndHeadshotsInDb(List<PlayerEntity> teamChangeList)
        {
            await _repo.UpdateTeamsAndHeadshotsInDb(teamChangeList);
        }

        public async Task<int> GetSuggestedSalary(PlayerTipRequestDTO tip)
        {
            var yearSugg = new int[] {1, 3};
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
    }
}