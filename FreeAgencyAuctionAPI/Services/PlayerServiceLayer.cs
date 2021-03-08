using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IPlayerServiceLayer
    {
        public Task<PlayerDTO> GetPlayerById(int id);
        public Task<List<PlayerDTO>> GetRosteredPlayers();
        public Task<PlayerDTO> NominatePlayer();
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

        public async Task<PlayerDTO> GetPlayerById(int id)
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
            var entity = await _repo.SetPlayerOwner(owned);
        }
    }
}