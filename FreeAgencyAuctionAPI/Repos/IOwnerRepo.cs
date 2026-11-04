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
        public Task<OwnerDTO> AddOwnerAndRelatedLeagues(AuthUser user, List<FranchisePlusAssets> franchises);
        public Task<OwnerDTO?> LinkExistingOwnerByMflFranchises(AuthUser user, List<FranchisePlusAssets> franchises);

        public Task UpdateCapRoomForAllOwners(List<int> capSpace, int leagueId);
        public Task<List<LeagueOwnerEntity>> GetAllOwners(int leagueId);
        public Task<IEnumerable<int>> GetAllRealLeagueIds();
        public Task<OwnerDTO> GetOwnerByAuthId(string sub);
        public Task<OwnerDTO> Login(OwnerDTO owner, string? sub = "");
        public Task UpdateOwnerStreamToken(OwnerDTO owner, string token);
        public Task MakeTestLeague();
        public Task<OwnerEntity> Register(OwnerEntity newUser);
        public Task<OwnerMergeResult> MergeOwners(int keepOwnerId, int dropOwnerId);
    }

    public class OwnerMergeResult
    {
        public int IdentitiesMoved { get; set; }
        public int LeagueOwnersMoved { get; set; }
        public int LeagueOwnersMerged { get; set; }
        public int PicksMoved { get; set; }
        public int PicksDropped { get; set; }
        public int ExtraPicksMoved { get; set; }
        public int ExtraPicksDropped { get; set; }
        public int PoolUsersMoved { get; set; }
        public int PoolUsersMerged { get; set; }
        public int OverUnderPicksMoved { get; set; }
        public int OverUnderPicksDropped { get; set; }
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
                var currentDate = DateTime.UtcNow;

                // Main owner + leagues projection — no Pools/OUPicks subqueries here
                var ownerDto = await _db.Owners.AsNoTracking()
                    .Where(o => o.authid == sub || o.Identities.Any(i => i.AuthId == sub))
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
                        }).OrderBy(l => l.League.IsAuctioning ? 0 : 1).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (ownerDto == null) return null;

                // Pools as a separate query — isolated so a translation failure can't null out the owner
                try
                {
                    var ownerId = ownerDto.OwnerId;
                    var myPoolUserIds = await _db.PoolUsers.AsNoTracking()
                        .Where(pu => pu.OwnerId == ownerId)
                        .Select(pu => new { pu.Id, pu.PoolId })
                        .ToListAsync();
                    var myPoolUserIdSet = myPoolUserIds.Select(x => x.Id).ToHashSet();
                    var poolsImInByPoolId = myPoolUserIds.ToDictionary(x => x.PoolId, x => x.Id);

                    var activePools = await _db.Pools.AsNoTracking()
                        .Where(p => (p.OpenDate <= currentDate && p.StartDate >= currentDate) || poolsImInByPoolId.Keys.Contains(p.Id))
                        .Select(p => new
                        {
                            p.Id,
                            p.Name,
                            p.League,
                            p.OpenDate,
                            p.StartDate,
                            p.Type,
                            p.Year,
                            Picks = p.OUPicks.Where(pk => myPoolUserIdSet.Contains(pk.UserId)).Select(pick => new OverUnderPickDTO
                            {
                                Id = pick.Id,
                                LineId = pick.LineId,
                                UserId = pick.UserId,
                                IsOver = pick.IsOver,
                                LineAdjustment = pick.LineAdjustment,
                                PoolId = pick.PoolId
                            }).ToList()
                        })
                        .ToListAsync();

                    ownerDto.Pools = activePools.Select(p => new PoolDTO
                    {
                        Id = p.Id,
                        Name = p.Name,
                        League = p.League,
                        OpenDate = p.OpenDate,
                        StartDate = p.StartDate,
                        Type = p.Type,
                        Year = p.Year,
                        PoolOwnerId = poolsImInByPoolId.TryGetValue(p.Id, out var puId) ? puId : 0,
                        MyOverUnderPicks = p.Picks
                    }).ToList();
                }
                catch (Exception poolEx)
                {
                    _logger.LogError(poolEx, "pools fetch failed, continuing without pools");
                    ownerDto.Pools = new List<PoolDTO>();
                }

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

        public async Task<OwnerDTO?> LinkExistingOwnerByMflFranchises(AuthUser user, List<FranchisePlusAssets> franchises)
        {
            if (franchises == null || franchises.Count == 0) return null;

            // Build (leagueId, mflFranchiseId) pairs from MFL matches
            var pairs = franchises
                .Select(f => new { LeagueId = f.leagueId, FranchiseId = int.TryParse(f.id, out var x) ? x : 0 })
                .Where(p => p.FranchiseId > 0)
                .ToList();
            if (pairs.Count == 0) return null;

            var leagueIds = pairs.Select(p => p.LeagueId).Distinct().ToList();
            var franchiseIds = pairs.Select(p => p.FranchiseId).Distinct().ToList();

            // Find existing leagueowner rows for those franchises
            var existingRows = await _db.LeagueOwners
                .Where(lo => leagueIds.Contains(lo.Leagueid) && franchiseIds.Contains(lo.Mflfranchiseid))
                .ToListAsync();

            // Keep only exact (league, franchise) matches
            var matchedRows = existingRows
                .Where(lo => pairs.Any(p => p.LeagueId == lo.Leagueid && p.FranchiseId == lo.Mflfranchiseid))
                .ToList();
            if (matchedRows.Count == 0) return null;

            // Pick the ownerid that appears most often across matches
            var ownerId = matchedRows
                .GroupBy(lo => lo.Ownerid)
                .OrderByDescending(g => g.Count())
                .First()
                .Key;
            if (ownerId < 1) return null;

            var existingOwner = await _db.Owners.FirstOrDefaultAsync(o => o.Ownerid == ownerId);
            if (existingOwner == null) return null;

            await AddIdentityIfMissing(existingOwner, user.Sub);
            if (string.IsNullOrEmpty(existingOwner.authid)) existingOwner.authid = user.Sub;
            if (!string.IsNullOrEmpty(user.Picture)) existingOwner.Avatar = user.Picture;
            if (string.IsNullOrWhiteSpace(existingOwner.Displayname))
                existingOwner.Displayname = user.Name ?? user.Nickname ?? user.Email;
            await _db.SaveChangesAsync();

            return await GetOwnerByAuthId(user.Sub);
        }

        // Adds the sub as an identity of this owner unless some owner already has it (unique on authid).
        private async Task AddIdentityIfMissing(OwnerEntity owner, string sub)
        {
            if (string.IsNullOrEmpty(sub)) return;
            if (await _db.OwnerIdentities.AnyAsync(i => i.AuthId == sub)) return;
            owner.Identities.Add(new OwnerIdentityEntity { AuthId = sub, CreatedAt = DateTime.UtcNow });
        }

        // Folds dropOwner into keepOwner (same person, two Auth0 accounts). Everything is changed on tracked
        // entities and saved with a single SaveChanges, so it lands all-or-nothing.
        public async Task<OwnerMergeResult> MergeOwners(int keepOwnerId, int dropOwnerId)
        {
            if (keepOwnerId == dropOwnerId) throw new ArgumentException("keep and drop owner are the same");
            var keep = await _db.Owners.Include(o => o.Identities).FirstOrDefaultAsync(o => o.Ownerid == keepOwnerId)
                ?? throw new KeyNotFoundException($"owner {keepOwnerId} not found");
            var drop = await _db.Owners.Include(o => o.Identities).FirstOrDefaultAsync(o => o.Ownerid == dropOwnerId)
                ?? throw new KeyNotFoundException($"owner {dropOwnerId} not found");
            var result = new OwnerMergeResult();

            // identities (+ drop's legacy authid if it never made it into the table)
            var knownSubs = keep.Identities.Select(i => i.AuthId).Concat(drop.Identities.Select(i => i.AuthId)).ToHashSet();
            foreach (var identity in drop.Identities.ToList())
            {
                identity.OwnerId = keepOwnerId;
                result.IdentitiesMoved++;
            }
            if (!string.IsNullOrEmpty(drop.authid) && !knownSubs.Contains(drop.authid)
                && !await _db.OwnerIdentities.AnyAsync(i => i.AuthId == drop.authid))
            {
                keep.Identities.Add(new OwnerIdentityEntity { AuthId = drop.authid, CreatedAt = DateTime.UtcNow });
                result.IdentitiesMoved++;
            }
            if (string.IsNullOrEmpty(keep.authid)) keep.authid = drop.authid;

            // flags: keep whatever either account had earned
            keep.Premium = (keep.Premium ?? false) || (drop.Premium ?? false);
            keep.ConfidencePaid = keep.ConfidencePaid || drop.ConfidencePaid;
            keep.ConfidenceTitles = System.Text.Json.JsonSerializer.Serialize(
                keep.ConfidenceTitleList.Concat(drop.ConfidenceTitleList).Distinct().OrderBy(x => x).ToList());

            // league ownership: same (league, franchise) on both -> fold the dropped row's children into the keeper's row
            var keepLos = await _db.LeagueOwners.Where(lo => lo.Ownerid == keepOwnerId).ToListAsync();
            var dropLos = await _db.LeagueOwners
                .Include(lo => lo.Bids).Include(lo => lo.Lots).Include(lo => lo.Contracts)
                .Include(lo => lo.FranchiseTags).Include(lo => lo.Buyouts)
                .Include(lo => lo.WaiverExtensions).Include(lo => lo.Holdouts)
                .Where(lo => lo.Ownerid == dropOwnerId).ToListAsync();
            foreach (var dlo in dropLos)
            {
                var match = keepLos.FirstOrDefault(k => k.Leagueid == dlo.Leagueid && k.Mflfranchiseid == dlo.Mflfranchiseid);
                if (match == null)
                {
                    dlo.Ownerid = keepOwnerId;
                    result.LeagueOwnersMoved++;
                    continue;
                }
                foreach (var x in dlo.Bids.ToList()) x.Ownerid = match.Leagueownerid;
                foreach (var x in dlo.Lots.ToList()) x.Nominatedby = match.Leagueownerid;
                foreach (var x in dlo.Contracts.ToList()) x.Ownerid = match.Leagueownerid;
                foreach (var x in dlo.FranchiseTags.ToList()) x.Leagueownerid = match.Leagueownerid;
                foreach (var x in dlo.Buyouts.ToList()) x.LeagueOwnerId = match.Leagueownerid;
                foreach (var x in dlo.WaiverExtensions.ToList()) x.LeagueOwnerId = match.Leagueownerid;
                foreach (var x in dlo.Holdouts.ToList()) x.LeagueOwnerId = match.Leagueownerid;
                _db.LeagueOwners.Remove(dlo);
                result.LeagueOwnersMerged++;
            }

            // confidence picks: one pick per matchup / prop per owner -> keeper's wins on overlap
            var keepMatchups = (await _db.NflPicks.Where(p => p.OwnerId == keepOwnerId).Select(p => p.MatchupId).ToListAsync()).ToHashSet();
            foreach (var pick in await _db.NflPicks.Where(p => p.OwnerId == dropOwnerId).ToListAsync())
            {
                if (keepMatchups.Contains(pick.MatchupId)) { _db.NflPicks.Remove(pick); result.PicksDropped++; }
                else { pick.OwnerId = keepOwnerId; result.PicksMoved++; }
            }
            var keepProps = (await _db.ExtraPicks.Where(p => p.OwnerId == keepOwnerId).Select(p => p.PropId).ToListAsync()).ToHashSet();
            foreach (var pick in await _db.ExtraPicks.Where(p => p.OwnerId == dropOwnerId).ToListAsync())
            {
                if (keepProps.Contains(pick.PropId)) { _db.ExtraPicks.Remove(pick); result.ExtraPicksDropped++; }
                else { pick.OwnerId = keepOwnerId; result.ExtraPicksMoved++; }
            }

            // over/under pools: one pooluser per (pool, owner); picks hang off pooluser.Id
            var keepPus = await _db.PoolUsers.Where(pu => pu.OwnerId == keepOwnerId).ToListAsync();
            foreach (var dpu in await _db.PoolUsers.Where(pu => pu.OwnerId == dropOwnerId).ToListAsync())
            {
                var kpu = keepPus.FirstOrDefault(k => k.PoolId == dpu.PoolId);
                if (kpu == null) { dpu.OwnerId = keepOwnerId; result.PoolUsersMoved++; continue; }

                kpu.IsPaid = kpu.IsPaid || dpu.IsPaid;
                var keepLines = (await _db.OverUnderPicks.Where(p => p.UserId == kpu.Id).Select(p => p.LineId).ToListAsync()).ToHashSet();
                foreach (var pick in await _db.OverUnderPicks.Where(p => p.UserId == dpu.Id).ToListAsync())
                {
                    if (keepLines.Contains(pick.LineId)) { _db.OverUnderPicks.Remove(pick); result.OverUnderPicksDropped++; }
                    else { pick.UserId = kpu.Id; result.OverUnderPicksMoved++; }
                }
                _db.PoolUsers.Remove(dpu);
                result.PoolUsersMerged++;
            }

            // loose ends keyed straight on ownerid
            foreach (var q in await _db.OwnerQuotes.Where(q => q.Ownerid == dropOwnerId).ToListAsync()) q.Ownerid = keepOwnerId;
            foreach (var sg in await _db.Suggestions.Where(sg => sg.Ownerid == dropOwnerId).ToListAsync()) sg.Ownerid = keepOwnerId;

            _db.Owners.Remove(drop);
            await _db.SaveChangesAsync();
            return result;
        }

        public async Task<OwnerDTO> AddOwnerAndRelatedLeagues(AuthUser user, List<FranchisePlusAssets> franchises)
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
            owner.Identities.Add(new OwnerIdentityEntity { AuthId = user.Sub, CreatedAt = DateTime.UtcNow });
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
                        FirstYear = _.League.FirstYear,
                        IsAuctioning = _.League.Isauctioning,
                        IsFranchiseTagSzn = _.League.IsFranchiseTagSzn,
                        IsBuyoutSzn = _.League.IsBuyoutSzn,
                        IsTaxiCutSzn = _.League.IsTaxiSzn
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