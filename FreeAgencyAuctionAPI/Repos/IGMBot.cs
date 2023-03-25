using System.Threading.Tasks;
using RestEase;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IGMBot
    {
        [Post("Bot/auctionError")]
        Task NotifyMflError([Body] ErrorMessage message);
        [Post("Bot/stanfan-msg")]
        Task SendBotNotification([Body] ErrorMessage message);
    }

    public class ErrorMessage
    {
        public string Message { get; set; }

        public ErrorMessage(string msg)
        {
            Message = msg;
        }
    }
}