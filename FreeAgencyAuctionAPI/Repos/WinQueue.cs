using FreeAgencyAuctionAPI.Models;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IQueueService
    {
        void SendMessageToQueue(BidDTO bid);
        ValueTask<BidDTO> ReadAsync(CancellationToken ct);
    }

    public class InMemoryWinQueue : IQueueService
    {
        private readonly Channel<BidDTO> _channel = Channel.CreateUnbounded<BidDTO>();

        public void SendMessageToQueue(BidDTO bid)
        {
            _channel.Writer.TryWrite(bid);
        }

        public ValueTask<BidDTO> ReadAsync(CancellationToken ct)
        {
            return _channel.Reader.ReadAsync(ct);
        }
    }
}
