using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IOwnerRepo
    {
        public Task UpdateCapRoomForAllOwners(List<int> capSpace);
        public Task<List<OwnerEntity>> GetAllOwners();
        public Task<List<SuggestionEntity>> GetAllTipsByOwnerId(int ownerId);
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
                                                                        o.ownername.ToUpper() == owner.Ownername.ToUpper() &&
                                                                   o.password_hash == owner.Password);
                if (ret == null) return null;
                return new OwnerDTO
                {
                    OwnerId = ret.ownerid,
                    Ownername = ret.ownername,
                    CapRoom = ret.caproom,
                    YearsLeft = ret.yearsleft,
                    Password = ret.password_hash,
                    Premium = ret.premium ?? false,
                    DisplayName = ret.displayname
                };
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return null;
            }
        }

        public async Task<List<SuggestionEntity>> GetAllTipsByOwnerId(int ownerId)
        {
            
            var suggestions = _db.Suggestions.Where(s => s.ownerId == ownerId);
            return !suggestions.Any() ? new List<SuggestionEntity>() : suggestions.ToList();
        }

        public async Task UpdateCapRoomForAllOwners(List<int> capSpace)
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
        }

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