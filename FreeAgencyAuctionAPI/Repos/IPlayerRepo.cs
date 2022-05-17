using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IPlayerRepo
    {
        public Task<PlayerEntity> GetPlayerById(string playerId);
        public Task<List<PlayerEntity>> GetRosteredPlayers();
        public Task<PlayerEntity> SetPlayerOwner(PlayerEntity player);
        public Task<PlayerEntity> WinPlayer(BidEntity bid);
        public Task<List<PlayerEntity>> GetAllFreeAgents();
        Task AddFreshPlayerInventory(List<PlayerEntity> players);
        Task<List<PlayerEntity>> GetAllPlayers();
        Task<PlayerEntity> SavePlayerActionShot(string mflId, string actionShot);
        Task UpdateTeamsAndHeadshotsInDb(List<PlayerEntity> teamChangeList);
        Task AddTipToDb(string tipMflId, int tipOwnerId, int salary);
    }

    public class PlayerRepo : IPlayerRepo
    {
        private readonly AuctionContext _db;
        private readonly ILogger<PlayerRepo> _logger;

        public PlayerRepo(AuctionContext db, ILogger<PlayerRepo> logger)
        {
            _db = db;
            _logger = logger;
        }
        public async Task<PlayerEntity> GetPlayerById(string playerId)
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
                return await _db.Players.AsQueryable().Where(p => p.ownername != null).ToListAsync();
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return null;
            }
        }

        public async Task<PlayerEntity> SetPlayerOwner(PlayerEntity player)
        {
            try
            {
                var dbPlayer = await _db.Players.AsQueryable().Where(p => p.mflid == player.mflid).FirstAsync();
                dbPlayer.ownerid = player.ownerid;
                await _db.SaveChangesAsync();
                return dbPlayer;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return null;
            }
        }
        
        public async Task<PlayerEntity> SavePlayerActionShot(string mflId, string actionShot)
        {
            try
            {
                var dbPlayer = await _db.Players.AsQueryable().Where(p => p.mflid == mflId).FirstAsync();
                dbPlayer.actionshot = actionShot;
                await _db.SaveChangesAsync();
                return dbPlayer;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
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
                _logger.LogError(e.Message);
                return null;
            }
        }
        
        public async Task<List<PlayerEntity>> GetAllPlayers()
        {
            try
            {
                return await _db.Players.ToListAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return null;
            }
        } 

        public async Task<List<PlayerEntity>> GetAllFreeAgents()
        {
            try
            {
                return await _db.Players.AsQueryable()
                    .Where(p => p.ownerid == null && p.ownername == null)
                    .OrderBy(p => p.position)
                    .ThenBy(p => p.lastname)
                    .ToListAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return null;
            }
        }

        public async Task AddTipToDb(string tipMflId, int tipOwnerId, int salary)
        {
            var suggestion = new SuggestionEntity(tipOwnerId, tipMflId, salary);
            _db.Suggestions.Add(suggestion);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateTeamsAndHeadshotsInDb(List<PlayerEntity> teamChangeList)
        {
            try
            {
                // get player
                // if player has a headshot leave it.
                // change team
                //save it 
           
                var dbPlayers = await _db.Players.Where(p => teamChangeList.Select(tm => tm.mflid).Contains(p.mflid)).ToListAsync();
                dbPlayers.ForEach(p =>
                {
                    var updatePlayer = teamChangeList.FirstOrDefault(tm => tm.mflid == p.mflid);
                    if (string.IsNullOrEmpty(p.headshot) && !string.IsNullOrEmpty(updatePlayer?.headshot ?? ""))
                        p.headshot = updatePlayer?.headshot;
                    p.team = updatePlayer?.team;
                });
                await _db.SaveChangesAsync();
                
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                throw;
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
                _logger.LogError(e.Message);
                throw;
            }

        }
    }
}