using System;
using System.Collections.Generic;
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
    }
    public class PlayerServiceLayer : IPlayerServiceLayer
    {
        private readonly IPlayerRepo _repo;
        private readonly IMapper _mapper;

        public PlayerServiceLayer(IPlayerRepo playerRepo, IMapper mapper)
        {
            _repo = playerRepo;
            _mapper = mapper;
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
    }
}