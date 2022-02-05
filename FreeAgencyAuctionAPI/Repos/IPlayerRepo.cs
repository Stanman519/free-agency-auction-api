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
        public Task<PlayerEntity> WinPlayer(BidEntity bid);
        public Task<List<PlayerEntity>> GetAllFreeAgents();
        Task AddFreshPlayerInventory(List<PlayerEntity> players);
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
                return await _db.Players.FirstAsync(p => p.mflid == playerId);
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
                var dbPlayer = await _db.Players.Where(p => p.mflid == player.mflid).FirstAsync();
                dbPlayer.ownerid = player.ownerid;
                await _db.SaveChangesAsync();
                return dbPlayer;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public async Task<PlayerEntity> WinPlayer(BidEntity bid)
        {
            try
            {
                var playerToUpdate = await _db.Players.FirstAsync(p => p.mflid == bid.mflid);
                playerToUpdate.ownername = bid.ownername;
                playerToUpdate.length = bid.bidlength;
                playerToUpdate.salary = bid.bidsalary;
                playerToUpdate.contractvalue = (bid.bidlength * 5) + bid.bidsalary;
                await _db.SaveChangesAsync();
                return playerToUpdate;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public async Task<List<PlayerEntity>> GetAllFreeAgents()
        {
            try
            {
                return await _db.Players
                    .Where(p => p.ownerid == null && p.ownername == null)
                    .OrderBy(p => p.position)
                    .ThenBy(p => p.lastname)
                    .ToListAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public async Task AddFreshPlayerInventory(List<PlayerEntity> players)
        {
            try
            {
                await _db.Players.AddRangeAsync(players);
                await _db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

        }
    }
}