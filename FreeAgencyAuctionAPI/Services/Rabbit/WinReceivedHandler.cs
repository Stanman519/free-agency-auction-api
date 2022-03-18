// using System.Threading;
// using System.Threading.Tasks;
// using FreeAgencyAuctionAPI.Models;
// using MediatR;
// using Microsoft.Extensions.Logging;
//
// namespace FreeAgencyAuctionAPI.Services.Rabbit
// {
//  
//     public class WinRecievedHandleer : IRequestHandler<WinReceived>
//     {
//         private readonly BidLotService _bService;
//
//         public WinRecievedHandleer(BidLotService bService)
//         {
//             _bService = bService;
//         }
//         
//         public async Task<Unit> Handle(WinReceived win)
//         {
//             await _bService.HandleWinningTasks(win.Bid);
//             return Task.FromResult(Unit.Value).Result;
//         }
//     }
// }