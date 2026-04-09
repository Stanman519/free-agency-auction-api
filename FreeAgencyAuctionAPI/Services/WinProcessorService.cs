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

            var botId = Utils.leagueBotDict.TryGetValue(bid.LeagueId, out var x) ? x : string.Empty;

            if (bid.Expires > DateTime.UtcNow)
            {
                _logger.LogError("Bid has still not expired. {lastname} - league: {leagueId}", bid.Player.LastName, bid.LeagueId);
                return;
            }

            var latestBid = await db.Bids
                .OrderByDescending(b => b.Bidid)
                .FirstOrDefaultAsync(b => b.Mflid == bid.Player.MflId && b.Leagueid == bid.LeagueId);
            if (latestBid == null || latestBid.Bidid != bid.BidId)
            {
                _logger.LogError("Not the latest bid for player: {lastname} - league: {leagueId}", bid.Player.LastName, bid.LeagueId);
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
                _logger.LogError("Player already rostered {lastname} - league: {leagueId}", bid.Player.LastName, bid.LeagueId);
                return;
            }

            var leagueOwner = await db.LeagueOwners.FirstOrDefaultAsync(l => l.Leagueownerid == bid.OwnerId);
            var mflOwnerId = leagueOwner?.Mflfranchiseid ?? 0;

            try
            {
                await mflService.AddPlayerToTeam(bid.LeagueId, bid.Player.MflId, mflOwnerId, $"{bid.Player.FirstName} {bid.Player.LastName}");
                await gmBot.SendBotNotification(new BotMessage($"{bid.Ownername} won {bid.Player.FirstName} {bid.Player.LastName} at ${bid.BidSalary}/{bid.BidLength}", botId));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "couldn't add player to MFL team");
                await Task.Delay(60000);
                return;
            }

            var contractMsg = $"{bid.Player.FirstName} {bid.Player.LastName} signed ${bid.BidSalary}/{bid.BidLength}yr";
            await mflService.GiveNewContractToPlayer(bid.LeagueId, bid.Player.MflId, bid.BidSalary, bid.BidLength, contractMsg);

            try
            {
                var capSpace = await mflService.GetSalaryCapRoom(bid.LeagueId);
                var capList = capSpace.OrderBy(c => c.Mflfranchiseid).Select(c => c.Caproom ?? 0).ToList();
                await ownerRepo.UpdateCapRoomForAllOwners(capList, bid.LeagueId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "error syncing cap room after win for player {mflId}", bid.Player.MflId);
                try
                {
                    await gmBot.NotifyMflError(new BotMessage($"error syncing cap room after win for player {bid.Player.MflId}", botId));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "GM could not be notified with cap sync failure");
                }
                throw;
            }
        }
    }
}
