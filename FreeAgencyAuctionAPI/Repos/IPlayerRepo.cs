using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IPlayerRepo
    {
        public Task<PlayerEntity> GetPlayerById(int playerId);
        public Task<List<PlayerEntity>> GetRosteredPlayers();
        public Task<PlayerEntity> SetPlayerOwner(PlayerEntity player);
    }

    public class PlayerRepo : IPlayerRepo
    {
        private readonly AuctionContext _db;

        public PlayerRepo(AuctionContext db)
        {
            _db = db;
        }
        public async Task<PlayerEntity> GetPlayerById(int playerId)
        {
            try
            {
                return await _db.Players.FirstAsync(p => p.playerid == playerId);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public async Task<List<PlayerEntity>> GetRosteredPlayers()
        {
            try
            {
                return await _db.Players.Where(p => p.ownername != null).ToListAsync();
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public async Task<PlayerEntity> SetPlayerOwner(PlayerEntity player)
        {
            try
            {

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}