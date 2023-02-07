using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IOwnerRepo
    {
        //public Task UpdateCapRoomForAllOwners(List<int> capSpace);
        public Task<List<OwnerEntity>> GetAllOwners();
        public Task<OwnerDTO> Login(OwnerDTO owner);
        public Task<OwnerEntity> Register(OwnerEntity newUser);
    }

    public class OwnerRepo : IOwnerRepo
    {
        private readonly AuctionContext _db;
        private readonly ILogger<OwnerRepo> _logger;

        public OwnerRepo(AuctionContext db, ILogger<OwnerRepo> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<OwnerDTO> Login(OwnerDTO owner)
        {
            try
            {
                var ret = await _db.Owners.FirstOrDefaultAsync(o => 
                        o.Ownername.ToUpper() == owner.Ownername.ToUpper() &&
                    o.PasswordHash == owner.Password);
                if (ret == null) return null;

                return await (from o in _db.Owners
                              join l in _db.LeagueOwners on o.Ownerid equals l.Ownerid into temp
                              select new OwnerDTO
                              {
                                  OwnerId = o.Ownerid,
                                  Ownername = o.Ownername,
                                  Password = o.PasswordHash,
                                  Premium = o.Premium ?? false,
                                  DisplayName = o.Displayname,
                                  Leagues = temp.Select(_ => new LeagueOwnerDTO
                                  {
                                      CapRoom = _.Caproom ?? 0,
                                      YearsLeft = _.Yearsleft ?? 0,
                                      Mflfranchiseid = _.Mflfranchiseid,
                                      Leagueownerid = _.Leagueownerid,
                                      League = new LeagueDTO
                                      {
                                          LeagueId = _.Leagueid,
                                          Name = _.League.Name,
                                          MflHash = _.League.Mflhash,
                                          CommishCookie = _.League.Commishcookie,
                                          
                                      }
                                  })
                              }).FirstOrDefaultAsync(o => o.Ownername.ToUpper() == owner.Ownername.ToUpper() && o.Password == owner.Password);

            }
            catch (Exception e)
            {
                _logger.LogError(e, "login exception");
                return null;
            }
        }
        //MOVE TO AZURE FUNCTION
/*        public async Task UpdateCapRoomForAllOwners(List<int> capSpace)
        {
            try
            {
                var owners = _db.Owners;
                for (int i = 0; i < capSpace.Count; i++)
                {
                    var teamToUpdate = owners.FirstOrDefault(o => o.ownerid == i + 1);
                    if(teamToUpdate != null) 
                        teamToUpdate.caproom = capSpace[i];
                }
                await _db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }
        }*/

        public async Task<List<OwnerEntity>> GetAllOwners()
        {
            try
            {
                return await _db.Owners.ToListAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return null;
            }
        }

        public async Task<OwnerEntity> Register(OwnerEntity newUser)
        {
            try
            {
                var ret = await _db.Owners.AddAsync(newUser);
                await _db.SaveChangesAsync();
                return newUser;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                throw;
            }
            
        }
    }
}