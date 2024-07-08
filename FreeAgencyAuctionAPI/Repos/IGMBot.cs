using System.Threading.Tasks;
using RestEase;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IGMBot
    {
        [Post("auctionError")]
        Task NotifyMflError([Body] BotMessage message);
        [Post("stanfan-msg")]
        Task SendBotNotification([Body] BotMessage message);
    }

    public class BotMessage
    {
        public string Message { get; set; }
        public string BotId { get; set; }

        public BotMessage(string msg, string botId)
        {
            Message = msg;
            BotId = botId;
        }
    }
}