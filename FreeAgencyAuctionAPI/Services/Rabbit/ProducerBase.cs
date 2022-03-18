// using System;
// using System.Text;
// using Newtonsoft.Json;
// using RabbitMQ.Client;
//
// namespace FreeAgencyAuctionAPI.Services.Rabbit
// {
//     public abstract class ProducerBase<T> : RabbitMqClientBase, IRabbitMqProducer<T>
//     {
//
//         
//         protected abstract string ExchangeName { get; }
//         protected abstract string RoutingKeyName { get; }
//         protected abstract string AppId { get; }
//
//         protected ProducerBase(ConnectionFactory connectionFactory) : base(connectionFactory)
//         {
//             
//         }
//
//         public virtual void Publish(T @event)
//         {
//             try
//             {
//                 var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@event));
//                 var properties = Channel.CreateBasicProperties();
//                 properties.AppId = AppId;
//                 properties.ContentType = "application/json";
//                 properties.DeliveryMode = 2;
//                 properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
//                 Channel.ConfirmSelect();
//                 Channel.BasicPublish(exchange: ExchangeName, routingKey: RoutingKeyName, body: body, basicProperties: properties);
//
//                 var confirmed = Channel.WaitForConfirms();
//                 Console.WriteLine(confirmed);
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine(ex.Message, "Error while publishing");
//             }
//         }
//     }
// }