using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IHeadlineRepo
    {
        Task<List<HeadlineEntity>> GetActive(int leagueId);
        Task<HeadlineEntity?> GetActiveByRef(int leagueId, string refKind, int refId);
        Task<HeadlineEntity?> GetMostRecentAnyByRef(int leagueId, string refKind, int refId);
        Task<bool> HasEverEmittedTag(int leagueId, string refKind, int refId, string tag);
        Task<HeadlineEntity> Upsert(int leagueId, string refKind, int refId, string text, string tags, DateTime? expiresAt);
        Task<int> DeleteExpired(int leagueId);
    }

    public class HeadlineRepo : IHeadlineRepo
    {
        private readonly AuctionContext _db;
        private readonly ILogger<HeadlineRepo> _logger;

        public HeadlineRepo(AuctionContext db, ILogger<HeadlineRepo> logger)
        {
            _db = db;
            _logger = logger;
        }

        public Task<List<HeadlineEntity>> GetActive(int leagueId) =>
            _db.Headlines
                .Where(h => h.Leagueid == leagueId && h.IsActive)
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();

        public Task<HeadlineEntity?> GetActiveByRef(int leagueId, string refKind, int refId) =>
            _db.Headlines.FirstOrDefaultAsync(h =>
                h.Leagueid == leagueId &&
                h.ReferenceKind == refKind &&
                h.ReferenceId == refId &&
                h.IsActive);

        public Task<HeadlineEntity?> GetMostRecentAnyByRef(int leagueId, string refKind, int refId) =>
            _db.Headlines
                .Where(h => h.Leagueid == leagueId && h.ReferenceKind == refKind && h.ReferenceId == refId)
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefaultAsync();

        public Task<bool> HasEverEmittedTag(int leagueId, string refKind, int refId, string tag) =>
            _db.Headlines.AnyAsync(h =>
                h.Leagueid == leagueId &&
                h.ReferenceKind == refKind &&
                h.ReferenceId == refId &&
                (h.Tags == tag || h.Tags.StartsWith(tag + ",") || h.Tags.EndsWith("," + tag) || h.Tags.Contains("," + tag + ",")));

        public async Task<HeadlineEntity> Upsert(int leagueId, string refKind, int refId, string text, string tags, DateTime? expiresAt)
        {
            try
            {
                var prior = await _db.Headlines
                    .Where(h => h.Leagueid == leagueId && h.ReferenceKind == refKind && h.ReferenceId == refId && h.IsActive)
                    .ToListAsync();
                foreach (var p in prior) p.IsActive = false;

                var fresh = new HeadlineEntity
                {
                    Leagueid = leagueId,
                    ReferenceKind = refKind,
                    ReferenceId = refId,
                    Text = text,
                    Tags = tags,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt
                };
                _db.Headlines.Add(fresh);
                await _db.SaveChangesAsync();
                return fresh;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "headline upsert failed league={leagueId} ref={refKind}:{refId}", leagueId, refKind, refId);
                throw;
            }
        }

        public async Task<int> DeleteExpired(int leagueId)
        {
            var now = DateTime.UtcNow;
            var expired = await _db.Headlines
                .Where(h => h.Leagueid == leagueId && h.IsActive && h.ExpiresAt != null && h.ExpiresAt < now)
                .ToListAsync();
            foreach (var h in expired) h.IsActive = false;
            if (expired.Count > 0) await _db.SaveChangesAsync();
            return expired.Count;
        }
    }
}
