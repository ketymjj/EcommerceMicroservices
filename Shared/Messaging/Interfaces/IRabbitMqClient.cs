using RabbitMQ.Client;

namespace Shared.Messaging.Interfaces;

public interface IRabbitMqClient : IDisposable
{
    Task PublishAsync<T>(T message, string routingKey, string exchangeType = ExchangeType.Fanout) where T : class;
}