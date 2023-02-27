using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IPlayerRepo
    {
        public Task<PlayerEntity> GetPlayerById(int playerId);
        public Task<IEnumerable<PlayerEntity>> GetRosteredPlayers(int leagueId);
        //public Task<PlayerEntity> SetPlayerOwner(PlayerEntity player);
        //public Task<PlayerEntity> WinPlayer(BidEntity bid);
        public Task<IEnumerable<PlayerEntity>> GetAllFreeAgents(int leagueId);
        // Task AddFreshPlayerInventory(List<PlayerEntity> players);
        Task<IEnumerable<PlayerEntity>> GetAllPlayers();
        Task<IEnumerable<PlayerEntity>> GetPlayersByListOfIds(IEnumerable<int> mflIds);
        Task<PlayerEntity> SavePlayerActionShot(string mflId, string actionShot);
        /*        Task UpdateTeamsAndHeadshotsInDb(List<PlayerEntity> teamChangeList);*/
        Task AddTipToDb(string tipMflId, int tipOwnerId, int salary);
        Task<IEnumerable<PlayerEntity>> GetPlayersByMflIds(IEnumerable<int> freeAgentMflIds);
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
        public async Task<PlayerEntity> GetPlayerById(int playerId)
        {
            try
            {
                return await _db.Players.FirstAsync(p => p.Mflid == playerId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "get player error");
                return null;
            }
        }

        public async Task<IEnumerable<PlayerEntity>> GetRosteredPlayers(int leagueId)
        {
            try
            {
                var leagueContracts = await _db.Contracts.AsQueryable().Where(c => c.Leagueid == leagueId).ToListAsync();
                return leagueContracts.Select(c => c.Player).ToList();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "error getting rostered players");
                return null;
            }
        }

        /*        public async Task<PlayerEntity> SetPlayerOwner(PlayerEntity player)
                {
                    try
                    {
                        var dbPlayer = await _db.Players.AsQueryable().FirstAsync(p => p.Mflid == player.Mflid);
                        dbPlayer.ownerid = player.ownerid;
                        await _db.SaveChangesAsync();
                        return dbPlayer;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e.Message);
                        return null;
                    }
                }*/
        public async Task<IEnumerable<PlayerEntity>> GetPlayersByMflIds(IEnumerable<int> freeAgentMflIds)
        {
            try
            {
                return await _db.Players.Where(p => freeAgentMflIds.Contains(p.Mflid)).ToListAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error fetching free agents");
                throw;
            }
        }
        public async Task<PlayerEntity> SavePlayerActionShot(string mflId, string actionShot)
        {
            try
            {
                var dbPlayer = await _db.Players.AsQueryable().FirstAsync(p => p.Mflid == int.Parse(mflId));
                dbPlayer.Actionshot = actionShot;
                await _db.SaveChangesAsync();
                return dbPlayer;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return null;
            }
        }

        public async Task WinPlayer(BidEntity bid)
        {
            try
            {
                var newContract = new ContractEntity
                {
                    Bid = bid,
                    Mflid = bid.Mflid,
                    Length = bid.Bidlength,
                    Salary = bid.Bidsalary,
                    Contractvalue = (bid.Bidlength * 5) + bid.Bidsalary,
                    Ownerid = bid.Ownerid,
                    Leagueid = bid.Leagueid
                };

                await _db.SaveChangesAsync();
                return;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "error winning contract");
                return;
            }
        }

        public async Task<IEnumerable<PlayerEntity>> GetAllPlayers()
        {
            try
            {
                return await _db.Players.ToListAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "error getting all players");
                return null;
            }
        }

        public async Task<IEnumerable<PlayerEntity>> GetPlayersByListOfIds(IEnumerable<int> mflIds)
        {
            try
            {
                return await _db.Players.Where(p => mflIds.Contains(p.Mflid)).ToListAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "error getting all players");
                return null;
            }
        }

        public async Task<IEnumerable<PlayerEntity>> GetAllFreeAgents(int leagueId)
        {
            try
            {
                //outer join players and contracts, then remove lot players
                return await (from p in _db.Players
                              join c in _db.Contracts.Where(cont => cont.Leagueid == leagueId) on p.Mflid equals c.Mflid into temp
                              from c in temp.DefaultIfEmpty()
                              join l in _db.Lots.Where(lot => lot.Leagueid == leagueId) on p.Mflid equals l.Bid.Mflid into temp2
                              from l in temp2.DefaultIfEmpty()
                              select new { p, c, l }).Where(_ => _.c == null && _.l == null)
                            .Select(_ => _.p)
                            .OrderBy(p => p.Position)
                            .ThenBy(p => p.Lastname)
                            .ToListAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "free agent fetch");
                return null;
            }
        }

        public async Task AddTipToDb(string tipMflId, int tipOwnerId, int salary)
        {
            var suggestion = new SuggestionEntity(tipOwnerId, tipMflId, salary);
            _db.Suggestions.Add(suggestion);
            await _db.SaveChangesAsync();
        }

        /*        public async Task UpdateTeamsAndHeadshotsInDb(List<PlayerEntity> teamChangeList)
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
                }*/

        /*public async Task AddFreshPlayerInventory(List<PlayerEntity> players)
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
        }*/
    }
}