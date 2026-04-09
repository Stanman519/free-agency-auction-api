using System;
using System.Threading;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Repos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FreeAgencyAuctionAPI.Services
{
    public class WinProcessorBackgroundService : BackgroundService
    {
        private readonly IQueueService _queue;
        private readonly IWinProcessorService _processor;
        private readonly ILogger<WinProcessorBackgroundService> _logger;

        public WinProcessorBackgroundService(IQueueService queue, IWinProcessorService processor, ILogger<WinProcessorBackgroundService> logger)
        {
            _queue = queue;
            _processor = processor;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WinProcessor background service started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var bid = await _queue.ReadAsync(stoppingToken);
                    _logger.LogInformation("Processing win for {player} in league {leagueId}", bid.Player?.LastName, bid.LeagueId);
                    await _processor.ProcessWin(bid);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing win message");
                }
            }
        }
    }
}
