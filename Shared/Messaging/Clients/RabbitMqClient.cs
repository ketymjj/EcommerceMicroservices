using RabbitMQ.Client;
using Shared.Messaging.Interfaces;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Shared.Messaging.Clients
{
    public class RabbitMqClient : IRabbitMqClient
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMqClient> _logger;

        public RabbitMqClient(
            string hostName,
            string userName,
            string password,
            int port,
            string virtualHost,
            ILogger<RabbitMqClient> logger)
        {
            _logger = logger;

            var factory = new ConnectionFactory()
            {
                HostName = hostName,
                UserName = userName,
                Password = password,
                Port = port,
                VirtualHost = virtualHost
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
        }

        public async Task PublishAsync<T>(T message, string routingKey, string exchangeType = ExchangeType.Fanout) where T : class
        {
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            _channel.ExchangeDeclare(exchange: routingKey, type: exchangeType, durable: true);
            _channel.BasicPublish(exchange: routingKey, routingKey: "", basicProperties: null, body: body);

            _logger.LogInformation($"Mensagem publicada em {routingKey}: {json}");
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}
