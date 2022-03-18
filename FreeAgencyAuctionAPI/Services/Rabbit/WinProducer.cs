// using FreeAgencyAuctionAPI.Models;
// using RabbitMQ.Client;
//
// namespace FreeAgencyAuctionAPI.Services.Rabbit
// {
//     public class WinProducer : ProducerBase<WinMessage>
//     {
//         protected override string ExchangeName => "WinExchange";
//         protected override string RoutingKeyName  => "log.message";
//         protected override string AppId => "WinProducer";
//         
//
//         public WinProducer(ConnectionFactory connectionFactory) : base(connectionFactory)
//         {
//             
//         }
//
//         // public async Task SendWinMessage(BidDTO bid)
//         // {
//         //     var @event = new WinMessage
//         //         {
//         //             MsgTime = DateTime.Now,
//         //             Id = Guid.NewGuid(),
//         //             Bid = bid
//         //         };
//         //     
//         //         
//         //     await Task.CompletedTask;
//         // }
//
//
//     }
// }