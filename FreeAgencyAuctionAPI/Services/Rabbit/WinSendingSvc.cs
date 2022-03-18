// using System.Threading.Tasks;
// using FreeAgencyAuctionAPI.Models;
//
// namespace FreeAgencyAuctionAPI.Services.Rabbit
// {
//     public interface IWinSendingSvc
//     {
//         Task SendWinMessage(WinMessage win);
//     }
//
//     public class WinSendingSvc : IWinSendingSvc
//     {
//         private readonly IRabbitMqProducer<WinMessage> _producer;
//
//         public WinSendingSvc(IRabbitMqProducer<WinMessage> producer) => _producer = producer;
//
//
//         public async Task SendWinMessage(WinMessage win)
//         {
//             _producer.Publish(win);
//         }
//         // public Task StartAsync(CancellationToken cancellationToken, WinMessage win = new WinMessage())
//         // {
//         //     _producer.Publish(win);
//         // }
//         //
//         // public Task StopAsync(CancellationToken cancellationToken)
//         // {
//         //     throw new NotImplementedException();
//         // }
//     }
// }