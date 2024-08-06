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
        public Task<OwnerDTO> AddOwnerAndRelatedLeagues(AuthUser user, List<Franchise> franchises);

        public Task UpdateCapRoomForAllOwners(List<int> capSpace, int leagueId);
        public Task<List<LeagueOwnerEntity>> GetAllOwners(int leagueId);
        public Task<IEnumerable<int>> GetAllRealLeagueIds();
        public Task<OwnerDTO> GetOwnerByAuthId(string sub);
        public Task<OwnerDTO> Login(OwnerDTO owner, string? sub = "");
        public Task UpdateOwnerStreamToken(OwnerDTO owner, string token);
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

        public async Task<OwnerDTO> GetOwnerByAuthId(string authId)
        {
            return await Login(new OwnerDTO(), authId);
        }

        public async Task<IEnumerable<int>> GetAllRealLeagueIds()
        {
            return (await _db.Leagues.Where(_ => _.Mflid > 0).ToListAsync()).Select(_ => _.Mflid);
        }

        public async Task UpdateOwnerStreamToken(OwnerDTO owner, string token)
        {
            var entity = await _db.Owners.FirstOrDefaultAsync(o => o.Ownerid == owner.OwnerId);
            if (entity == null) return;
            entity.StreamToken = token;
            await _db.SaveChangesAsync();
        }

        public async Task<OwnerDTO> Login(OwnerDTO owner, string? sub = "")
        {
            try
            {
                var currentDate = DateTime.Now;

                var ownerDto = await _db.Owners.AsNoTracking()
                    .Where(o => o.authid == sub)
                    .Select(o => new OwnerDTO
                    {
                        OwnerId = o.Ownerid,
                        Ownername = o.Ownername,
                        Password = o.PasswordHash,
                        Premium = o.Premium ?? false,
                        DisplayName = o.Displayname,
                        Leagues = o.Leagueowners.Select(lo => new LeagueOwnerDTO
                        {
                            CapRoom = lo.Caproom ?? 0,
                            YearsLeft = lo.Yearsleft ?? 0,
                            Mflfranchiseid = lo.Mflfranchiseid,
                            Leagueownerid = lo.Leagueownerid,
                            TeamName = lo.Teamname,
                            League = new LeagueDTO
                            {
                                LeagueId = lo.League.Mflid,
                                Name = lo.League.Name,
                                MflHash = lo.League.Mflhash,
                                CommishCookie = lo.League.Commishcookie,
                                FirstYear = lo.League.FirstYear,
                                IsFranchiseTagSzn = lo.League.IsFranchiseTagSzn,
                                IsBuyoutSzn = lo.League.IsBuyoutSzn,
                                IsAuctioning = lo.League.Isauctioning,
                                IsTaxiCutSzn = lo.League.IsTaxiSzn
                            }
                        }).OrderBy(l => l.League.IsAuctioning ? 0 : 1).ToList(),
                        Pools = _db.Pools.AsNoTracking()
                            .Where(p => (p.OpenDate <= currentDate && p.StartDate >= currentDate) || p.OUPicks.Any(pk => pk.UserId == p.PoolUsers.First(u => u.OwnerId == o.Ownerid).Id))
                            .Select(p => new PoolDTO
                            {
                                Id = p.Id,
                                Name = p.Name,
                                League = p.League,
                                OpenDate = p.OpenDate,
                                PoolOwnerId = p.PoolUsers.FirstOrDefault(pu => pu.OwnerId == o.Ownerid).Id,
                                StartDate = p.StartDate,
                                Type = p.Type,
                                Year = p.Year,
                                MyOverUnderPicks = p.OUPicks
                                    .Where(pck => pck.UserId == p.PoolUsers.First(u => u.OwnerId == o.Ownerid).Id)
                                    .Select(pick => new OverUnderPickDTO
                                    {
                                        Id = pick.Id,
                                        LineId = pick.LineId,
                                        UserId = pick.UserId,
                                        IsOver = pick.IsOver,
                                        LineAdjustment = pick.LineAdjustment,
                                        PoolId = pick.PoolId
                                    })
                            }).ToList()
                    })
                    .FirstOrDefaultAsync();

                return ownerDto;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "login exception");
                return null;
            }

            /* try
             {
                 var ret = await _db.Owners.FirstOrDefaultAsync(o => o.authid == sub);
                 var pools = await _db.Pools.Where(p => p.OpenDate <= DateTime.Now && p.StartDate >= DateTime.Now).ToListAsync();
                 if (ret == null) return null;

                 return new OwnerDTO
                 {
                     OwnerId = ret.Ownerid,
                     Ownername = ret.Ownername,
                     Password = ret.PasswordHash,
                     Premium = ret.Premium ?? false,
                     DisplayName = ret.Displayname,
                     Pools = pools.Select(_ => new PoolDTO
                     {
                         Id = _.Id,
                         Name = _.Name,
                         League = _.League,
                         OpenDate = _.OpenDate,
                         PoolOwnerId = _.PoolUsers.FirstOrDefault(p => p.OwnerId == ret.Ownerid)?.Id,
                         StartDate = _.StartDate,
                         Type = _.Type,
                         Year = _.Year,
                         MyOverUnderPicks = _.OUPicks
                         .Where(pck => pck.UserId == _.PoolUsers.First(u => u.OwnerId == ret.Ownerid)?.Id)
                     } ),
                     Leagues = ret.Leagueowners.Select(_ => new LeagueOwnerDTO
                     {
                         CapRoom = _.Caproom ?? 0,
                         YearsLeft = _.Yearsleft ?? 0,
                         Mflfranchiseid = _.Mflfranchiseid,
                         Leagueownerid = _.Leagueownerid,
                         TeamName = _.Teamname,
                         League = new LeagueDTO
                         {
                             LeagueId = _.Leagueid,
                             Name = _.League.Name,
                             MflHash = _.League.Mflhash,
                             CommishCookie = _.League.Commishcookie,
                             FirstYear = _.League.FirstYear,
                             IsFranchiseTagSzn = _.League.IsFranchiseTagSzn,
                             IsBuyoutSzn = _.League.IsBuyoutSzn,
                             IsAuctioning = _.League.Isauctioning,
                             IsTaxiCutSzn = _.League.IsTaxiSzn


                         }
                     }).OrderBy(_ => _.League.IsAuctioning ? 0 : 1).ToList()
                 };

             }
             catch (Exception e)
             {
                 _logger.LogError(e, "login exception");
                 return null;
             }*/
        }

        public async Task<OwnerDTO> AddOwnerAndRelatedLeagues(AuthUser user, List<Franchise> franchises)
        {
            if (franchises.Count == 0) 
            {
            
            }
            var owner = new OwnerEntity
            {
                authid = user.Sub,
                Avatar = user.Picture,
                Displayname = !string.IsNullOrEmpty(user.Name) ? user.Name :
                    !string.IsNullOrEmpty(user.Nickname) ? user.Nickname :
                    !string.IsNullOrEmpty(user.GivenName) ? user.GivenName :
                    !string.IsNullOrEmpty(user.PreferredUsername) ? user.PreferredUsername :
                    (franchises.Count > 0 && !string.IsNullOrEmpty(franchises[0].owner_name)) ? franchises[0].owner_name :
                    (franchises.Count > 0 && !string.IsNullOrEmpty(franchises[0].username)) ? franchises[0].username :
                    user.Email,
                istest = false,
                Premium = false,
                ConfidencePaid = false,
                PasswordHash = string.Empty,
                Ownername = !string.IsNullOrEmpty(user.Name) ? user.Name :
                    !string.IsNullOrEmpty(user.Nickname) ? user.Nickname :
                    !string.IsNullOrEmpty(user.GivenName) ? user.GivenName :
                    !string.IsNullOrEmpty(user.PreferredUsername) ? user.PreferredUsername :
                    (franchises.Count > 0 && !string.IsNullOrEmpty(franchises[0].owner_name)) ? franchises[0].owner_name :
                    (franchises.Count > 0 && !string.IsNullOrEmpty(franchises[0].username)) ? franchises[0].username :
                    user.Email
            };
            await _db.Owners.AddAsync(owner);
            await _db.SaveChangesAsync();
            var ownerId = owner.Ownerid;
            var newFranchises = new List<LeagueOwnerEntity>();
            foreach (var fran in franchises)
            {
                var leagueOwner = new LeagueOwnerEntity
                {
                    Caproom = 500,
                    Ownerid = ownerId,
                    Leagueid = fran.leagueId,
                    Yearsleft = 75,
                    Mflfranchiseid = int.TryParse(fran.id, out var x) ? x : 0,
                    Teamname = fran.name
                };
                newFranchises.Add(leagueOwner);
            }
            if (newFranchises.Any())
            {
                await _db.LeagueOwners.AddRangeAsync(newFranchises);
                await _db.SaveChangesAsync();
            }


            return new OwnerDTO
            {
                OwnerId = ownerId,
                Ownername = owner.Ownername,
                Password = owner.PasswordHash,
                Premium = owner.Premium ?? false,
                DisplayName = owner.Displayname,
                Leagues = newFranchises.Select(_ => new LeagueOwnerDTO
                {
                    CapRoom = _.Caproom ?? 0,
                    YearsLeft = _.Yearsleft ?? 0,
                    Mflfranchiseid = _.Mflfranchiseid,
                    Leagueownerid = _.Leagueownerid,
                    TeamName = _.Teamname,
                    League = new LeagueDTO
                    {
                        LeagueId = _.Leagueid,
                        Name = _.League.Name,
                        MflHash = _.League.Mflhash,
                        CommishCookie = _.League.Commishcookie,

                    }
                }).ToList()
            };
        }



        public async Task UpdateCapRoomForAllOwners(List<int> capSpace, int leagueId)
        {
            try
            {
                var owners = await _db.LeagueOwners.Where(l => l.Leagueid == leagueId).ToListAsync();

                for (int i = 0; i < capSpace.Count; i++)
                {
                    var teamToUpdate = owners.FirstOrDefault(o => o.Mflfranchiseid == i + 1);
                    if (teamToUpdate != null)
                        teamToUpdate.Caproom = capSpace[i];
                }
                await _db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }
        }

        public async Task<List<LeagueOwnerEntity>> GetAllOwners(int leagueId)
        {
            try
            {
                return await _db.LeagueOwners.Where(_ => _.Leagueid == leagueId).ToListAsync();
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