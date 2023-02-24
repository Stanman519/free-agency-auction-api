using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bogus;
using FreeAgencyAuctionAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StreamChat.Models;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IOwnerRepo
    {
        //public Task UpdateCapRoomForAllOwners(List<int> capSpace);
        public Task<List<OwnerEntity>> GetAllOwners();
        public Task<OwnerDTO> Login(OwnerDTO owner);
        public Task MakeTestLeague();
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
        public async Task MakeTestLeague() 
        {
            var random = new Random();
            var league = new Faker<LeagueEntity>()
                .RuleFor(l => l.Mflid, f => f.Random.Number(-99, -1))
                .RuleFor(l => l.Istest, f => true)
                .RuleFor(l => l.Isauctioning, f => true)
                .RuleFor(l => l.Name, f => f.Internet.DomainName())
                .Generate();
            var leagueId = league.Mflid;
            await _db.Leagues.AddAsync(league);
            await _db.SaveChangesAsync();
            var users = new Faker<OwnerEntity>()
                .RuleFor(o => o.Ownername, f => f.Internet.UserName())
                .RuleFor(o => o.PasswordHash, f => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(f.Internet.Password())))
                .RuleFor(o => o.Displayname, f => f.Name.FullName())
                .RuleFor(o => o.Avatar, f => f.Internet.Avatar())
                .RuleFor(o => o.istest, f => true)
                .Generate(11);
            await _db.Owners.AddRangeAsync(users); 
            await _db.SaveChangesAsync();
            var oIdsIndex = 0;
            var relevantOwners = (await _db.Owners.Where(o => o.istest).ToListAsync()).TakeLast(11).ToList();
            var franchises = new Faker<LeagueOwnerEntity>()
                .RuleFor(l => l.Caproom, f => f.Random.Number(50, 300))
                .RuleFor(l => l.Yearsleft, f => f.Random.Number(10, 60))
                .RuleFor(l => l.Mflfranchiseid, f => f.Random.Number(1, 40))
                .RuleFor(l => l.Leagueid, leagueId)
                .RuleFor(l => l.Ownerid, f => relevantOwners[oIdsIndex++].Ownerid);
            LeagueOwnerEntity MakeLeagueOwner(int seed, bool isRyan = false)
            {
                if (isRyan)
                {
                    oIdsIndex = 0;
                    var x = franchises.Generate();
                    x.Ownerid = 1;
                    return x;
                }
                return franchises.Generate();
            }

            var leagueOwners = Enumerable.Range(0, 11)
                .Select(s => MakeLeagueOwner(s))
                .ToList();

            leagueOwners = leagueOwners.Append(MakeLeagueOwner(0, true)).ToList();

            await _db.LeagueOwners.AddRangeAsync(leagueOwners);
            await _db.SaveChangesAsync();
            var ownerIds = await _db.LeagueOwners.OrderByDescending(o => o.Leagueownerid).Take(12).ToListAsync();

            var playerIds = await _db.Players
                .Where(p => !string.IsNullOrEmpty(p.Headshot) && p.Draftround != null && p.Draftround < 3 && p.Draftyear > 2017)
                .OrderByDescending(p => p.Lastseasonpts)
                .Skip(10)
                .Take(55)
                .ToListAsync();

                    

            int? n = null;
            var playerIndex = 0;
            var bids = new Faker<BidEntity>()
                .RuleFor(b => b.Leagueid, leagueId)
                .RuleFor(b => b.Bidlength, f => f.Random.Number(1, 3))
                .RuleFor(b => b.Bidsalary, f => f.Random.Number(1, 44))
                .RuleFor(b => b.Expires, f => DateTime.UtcNow.AddDays(200).AddHours(f.Random.Number(-5, 5)).AddMinutes(f.Random.Number(-5, 5)).AddSeconds(f.Random.Number(-5, 5)))
                .RuleFor(b => b.Mflid, f => playerIds[f.Random.Number(0, playerIds.Count)].Mflid)
                .RuleFor(b => b.Ownerid, f => ownerIds[f.Random.Number(0, 12)].Leagueownerid);

            BidEntity MakeBid(int seed)
            {
                return bids.UseSeed(seed).Generate();
            }
            var completeBids = Enumerable.Range(0, 6)
                .Select(MakeBid)
                .ToList();
            await _db.Bids.AddRangeAsync(completeBids);
            await _db.SaveChangesAsync();
            var lots = new Faker<LotEntity>()
                .RuleFor(l => l.Leagueid, leagueId)
                .RuleFor(l => l.Bidid, n)
                .Generate(12);
            await _db.Lots.AddRangeAsync(lots);
            await _db.SaveChangesAsync();
        }
    }
}