using Azure.Storage.Queues;
using FreeAgencyAuctionAPI.Models;
using System.Text.Json;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IQueueService
    {
        void SendMessageToQueue(BidDTO bid);
    }

    public class AzureQueueService : IQueueService
    {
        private readonly QueueServiceClient _queueServiceClient;

        public AzureQueueService(QueueServiceClient queueServiceClient)
        {
            _queueServiceClient = queueServiceClient;
        }

        public void SendMessageToQueue(BidDTO bid)
        {
            _queueServiceClient.GetQueueClient("winmessages")
                .SendMessage(JsonSerializer.Serialize(bid, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}
