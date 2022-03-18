// using System;
// using System.Text;
// using System.Threading.Tasks;
// using MediatR;
// using Microsoft.Extensions.Logging;
// using Newtonsoft.Json;
// using RabbitMQ.Client;
// using RabbitMQ.Client.Events;
//
// namespace FreeAgencyAuctionAPI.Services.Rabbit
// {
//     public abstract class ConsumerBase : RabbitMqClientBase
//     {
//         private readonly IMediator _mediator;
//         protected abstract string QueueName { get; }
//
//         public ConsumerBase(
//             IMediator mediator,
//             ConnectionFactory connectionFactory) :
//             base(connectionFactory)
//         {
//             _mediator = mediator;
//         }
//
//         protected virtual async Task OnEventReceived<T>(object sender, BasicDeliverEventArgs @event)
//         {
//             try
//             {
//                 var body = Encoding.UTF8.GetString(@event.Body.ToArray());
//                 var message = JsonConvert.DeserializeObject<T>(body);
//
//                 await _mediator.Send(message);
//             }
//             catch (Exception ex)
//             {
//                 // TODO: do something
//             }
//             finally
//             {
//                 Channel.BasicAck(@event.DeliveryTag, false);
//             }
//         }
//     }
// }