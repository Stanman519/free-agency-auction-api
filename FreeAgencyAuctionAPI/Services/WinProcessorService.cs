using System;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IWinProcessorService
    {
        Task ProcessWin(BidDTO bid);
    }

    public class WinProcessorService : IWinProcessorService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<WinProcessorService> _logger;

        public WinProcessorService(IServiceScopeFactory scopeFactory, ILogger<WinProcessorService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task ProcessWin(BidDTO bid)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AuctionContext>();
            var mflService = scope.ServiceProvider.GetRequiredService<IMflService>();
            var ownerRepo = scope.ServiceProvider.GetRequiredService<IOwnerRepo>();
            var gmBot = scope.ServiceProvider.GetRequiredService<IGMBot>();
            var headlineService = scope.ServiceProvider.GetRequiredService<IHeadlineService>();
            var quoteRepo = scope.ServiceProvider.GetRequiredService<IOwnerQuoteRepo>();

            var botId = Utils.leagueBotDict.TryGetValue(bid.LeagueId, out var x) ? x : string.Empty;

            if (bid.Expires > DateTime.UtcNow)
            {
                _logger.LogError("Bid has still not expired. {lastname} - league: {leagueId}", bid.Player.LastName, bid.LeagueId);
                await TryNotify(gmBot, botId, $"WIN ERROR: bid for {bid.Player.LastName} hasn't expired yet (league {bid.LeagueId})");
                return;
            }

            var latestBid = await db.Bids
                .OrderByDescending(b => b.Bidid)
                .FirstOrDefaultAsync(b => b.Mflid == bid.Player.MflId && b.Leagueid == bid.LeagueId);
            if (latestBid == null || latestBid.Bidid != bid.BidId)
            {
                _logger.LogInformation("Not the latest bid for player: {lastname} - league: {leagueId}", bid.Player.LastName, bid.LeagueId);
                return;
            }

            if (bid.LeagueId < 0)
            {
                var demoOwner = await db.LeagueOwners.FirstOrDefaultAsync(l => l.Leagueownerid == bid.OwnerId);
                if (demoOwner != null)
                {
                    demoOwner.Caproom = (demoOwner.Caproom ?? 0) - bid.BidSalary;
                    await db.SaveChangesAsync();
                }
                _logger.LogInformation("Demo win: {firstname} {lastname} to {ownername}", bid.Player.FirstName, bid.Player.LastName, bid.Ownername);
                return;
            }

            var rosters = await mflService.GetMflRosters(bid.LeagueId);
            var rosteredPlayerIds = rosters?
                .Where(f => f?.player != null)
                .SelectMany(f => f.player)
                .Select(p => int.TryParse(p.id, out var id) ? id : -1)
                .ToList() ?? new();
            if (rosteredPlayerIds.Contains(bid.Player.MflId))
            {
                _logger.LogInformation("Player already rostered {lastname} - league: {leagueId} (concurrent win, ignoring)", bid.Player.LastName, bid.LeagueId);
                return;
            }

            var leagueOwner = await db.LeagueOwners.FirstOrDefaultAsync(l => l.Leagueownerid == bid.OwnerId);
            var mflOwnerId = leagueOwner?.Mflfranchiseid ?? 0;
            if (mflOwnerId == 0)
            {
                _logger.LogError("Could not find MFL franchise ID for ownerId {ownerId} - league: {leagueId}", bid.OwnerId, bid.LeagueId);
                await TryNotify(gmBot, botId, $"WIN ERROR: can't find MFL franchise for owner {bid.Ownername} (ownerId {bid.OwnerId}, league {bid.LeagueId}) — {bid.Player.LastName} not added");
                return;
            }

            try
            {
                await mflService.AddPlayerToTeam(bid.LeagueId, bid.Player.MflId, mflOwnerId, $"{bid.Player.FirstName} {bid.Player.LastName}");
                await gmBot.SendBotNotification(new BotMessage($"{bid.Ownername} won {bid.Player.FirstName} {bid.Player.LastName} at ${bid.BidSalary}/{bid.BidLength}", botId));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "couldn't add player to MFL team");
                await TryNotify(gmBot, botId, $"WIN ERROR: failed to add {bid.Player.FirstName} {bid.Player.LastName} to {bid.Ownername}'s MFL roster (league {bid.LeagueId}) — {e.Message}");
                await Task.Delay(60000);
                return;
            }

            try
            {
                var contractMsg = $"{bid.Player.FirstName} {bid.Player.LastName} signed ${bid.BidSalary}/{bid.BidLength}yr";
                await mflService.GiveNewContractToPlayer(bid.LeagueId, bid.Player.MflId, bid.BidSalary, bid.BidLength, contractMsg);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "couldn't write contract for {mflId} in league {leagueId}", bid.Player.MflId, bid.LeagueId);
                await TryNotify(gmBot, botId, $"WIN ERROR: {bid.Player.FirstName} {bid.Player.LastName} was added to roster but contract write failed (league {bid.LeagueId}) — {e.Message}");
                return;
            }

            try
            {
                var capSpace = await mflService.GetSalaryCapRoom(bid.LeagueId);
                var capList = capSpace.OrderBy(c => c.Mflfranchiseid).Select(c => c.Caproom ?? 0).ToList();
                await ownerRepo.UpdateCapRoomForAllOwners(capList, bid.LeagueId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "error syncing cap room after win for player {mflId}", bid.Player.MflId);
                await TryNotify(gmBot, botId, $"WIN ERROR: cap room sync failed after {bid.Player.LastName} win (league {bid.LeagueId}) — {e.Message}");
                throw;
            }

            try
            {
                await headlineService.OnWin(bid);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "headline OnWin failed (non-fatal) for player {mflId}", bid.Player.MflId);
            }

            try
            {
                await quoteRepo.DeactivateForPlayer(bid.LeagueId, bid.Player.MflId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "quote deactivate-on-win failed (non-fatal) for player {mflId}", bid.Player.MflId);
            }
        }
        private async Task TryNotify(IGMBot gmBot, string botId, string message)
        {
            try
            {
                await gmBot.NotifyMflError(new BotMessage(message, botId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GroupMe notification failed: {message}", message);
            }
        }
    }
}
