using System.Threading.Tasks;
using RestEase;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IGMBot
    {
        [Post("auctionError")]
        Task NotifyMflError([Body] ErrorMessage message);
        [Post("stanfan-msg")]
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