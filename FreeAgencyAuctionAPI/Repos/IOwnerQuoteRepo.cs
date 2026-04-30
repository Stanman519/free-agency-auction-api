using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IOwnerQuoteRepo
    {
        Task<List<OwnerQuoteDTO>> GetActive(int leagueId);
        Task<OwnerQuoteEntity?> GetActiveByOwnerPlayer(int leagueId, int ownerId, int playerMflId);
        Task<OwnerQuoteEntity> Upsert(int leagueId, int ownerId, int playerMflId, string text);
        Task<int> DeactivateForPlayer(int leagueId, int playerMflId);
        Task<int> DeactivateForOwnerPlayer(int leagueId, int ownerId, int playerMflId);
    }

    public class OwnerQuoteRepo : IOwnerQuoteRepo
    {
        private readonly AuctionContext _db;
        private readonly ILogger<OwnerQuoteRepo> _logger;

        public OwnerQuoteRepo(AuctionContext db, ILogger<OwnerQuoteRepo> logger)
        {
            _db = db;
            _logger = logger;
        }

        public Task<List<OwnerQuoteDTO>> GetActive(int leagueId) =>
            _db.OwnerQuotes
                .Where(q => q.Leagueid == leagueId && q.IsActive)
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => new OwnerQuoteDTO
                {
                    QuoteId = q.Quoteid,
                    LeagueId = q.Leagueid,
                    OwnerId = q.Ownerid,
                    OwnerName = _db.LeagueOwners
                        .Where(lo => lo.Leagueownerid == q.Ownerid)
                        .Select(lo => lo.Owner.Ownername)
                        .FirstOrDefault() ?? "",
                    PlayerMflId = q.PlayerMflId,
                    Text = q.Text,
                    CreatedAt = q.CreatedAt,
                })
                .ToListAsync();

        public Task<OwnerQuoteEntity?> GetActiveByOwnerPlayer(int leagueId, int ownerId, int playerMflId) =>
            _db.OwnerQuotes.FirstOrDefaultAsync(q =>
                q.Leagueid == leagueId &&
                q.Ownerid == ownerId &&
                q.PlayerMflId == playerMflId &&
                q.IsActive);

        public async Task<OwnerQuoteEntity> Upsert(int leagueId, int ownerId, int playerMflId, string text)
        {
            try
            {
                var prior = await _db.OwnerQuotes
                    .Where(q => q.Leagueid == leagueId && q.Ownerid == ownerId && q.PlayerMflId == playerMflId && q.IsActive)
                    .ToListAsync();
                foreach (var p in prior) p.IsActive = false;

                var fresh = new OwnerQuoteEntity
                {
                    Leagueid = leagueId,
                    Ownerid = ownerId,
                    PlayerMflId = playerMflId,
                    Text = text,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                };
                _db.OwnerQuotes.Add(fresh);
                await _db.SaveChangesAsync();
                return fresh;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "quote upsert failed league={leagueId} owner={ownerId} player={playerMflId}", leagueId, ownerId, playerMflId);
                throw;
            }
        }

        public async Task<int> DeactivateForPlayer(int leagueId, int playerMflId)
        {
            var rows = await _db.OwnerQuotes
                .Where(q => q.Leagueid == leagueId && q.PlayerMflId == playerMflId && q.IsActive)
                .ToListAsync();
            foreach (var r in rows) r.IsActive = false;
            if (rows.Count > 0) await _db.SaveChangesAsync();
            return rows.Count;
        }

        public async Task<int> DeactivateForOwnerPlayer(int leagueId, int ownerId, int playerMflId)
        {
            var rows = await _db.OwnerQuotes
                .Where(q => q.Leagueid == leagueId && q.Ownerid == ownerId && q.PlayerMflId == playerMflId && q.IsActive)
                .ToListAsync();
            foreach (var r in rows) r.IsActive = false;
            if (rows.Count > 0) await _db.SaveChangesAsync();
            return rows.Count;
        }
    }
}
