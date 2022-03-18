// using System;
// using System.Threading;
// using System.Threading.Channels;
// using System.Threading.Tasks;
// using FreeAgencyAuctionAPI.Models;
// using MediatR;
// using Microsoft.Extensions.Hosting;
// using Microsoft.Extensions.Logging;
// using RabbitMQ.Client;
// using RabbitMQ.Client.Events;
//
// namespace FreeAgencyAuctionAPI.Services.Rabbit
// {
//     public class WinConsumer : ConsumerBase, IHostedService
//     {
//         protected override string QueueName => "CUSTOM_HOST.log.message";
//
//         public WinConsumer(
//             IMediator mediator,
//             ConnectionFactory connectionFactory) :
//             base(mediator, connectionFactory)
//         {
//             try
//             {
//                 var consumer = new AsyncEventingBasicConsumer(Channel);
//                 consumer.Received += OnEventReceived<WinReceived>;
//                 Channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
//             }
//             catch (Exception ex)
//             {
//                 //TODO: something
//             }
//         }
//
//         public virtual Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
//
//         public virtual Task StopAsync(CancellationToken cancellationToken)
//         {
//             Dispose();
//             return Task.CompletedTask;
//         }
//     }
// }