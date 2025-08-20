using RabbitMQ.Client;
using Shared.Messaging.Interfaces;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Shared.Messaging.Clients;

public class RabbitMqPublisher : IMessagePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private bool _disposed;

    // Construtor
    public RabbitMqPublisher(string hostName, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var factory = new ConnectionFactory()
        {
            HostName = hostName,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };

        try
        {
            _connection = factory.CreateConnection();
            _connection.ConnectionShutdown += OnConnectionShutdown;
            _channel = _connection.CreateModel();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to establish RabbitMQ connection");
            throw;
        }
    }

    public async Task PublishAsync<T>(T message, string routingKey = "", string exchangeType = ExchangeType.Fanout) where T : class
    {
        await PublishInternalAsync(message, routingKey, exchangeType);
    }

    public async Task PublishWithHeadersAsync<T>(T message, IDictionary<string, object> headers, string routingKey = "") where T : class
    {
        var props = _channel.CreateBasicProperties();
        props.Headers = headers;

        await PublishInternalAsync(message, routingKey, ExchangeType.Headers, props);
    }

    public async Task PublishWithDelayAsync<T>(T message, TimeSpan delay, string routingKey = "") where T : class
    {
        var props = _channel.CreateBasicProperties();
        props.Headers = new Dictionary<string, object>
        {
            {"x-delay", (int)delay.TotalMilliseconds}
        };

        await PublishInternalAsync(message, routingKey, "x-delayed-message", props);
    }

    public async Task PublishWithRetryAsync<T>(T message, int retryCount, string routingKey = "") where T : class
    {
        try
        {
            await PublishInternalAsync(message, routingKey);
        }
        catch (Exception ex)
        {
            if (retryCount <= 0) throw;

            _logger.LogWarning(ex, $"Publish failed, {retryCount} retries remaining");
            await Task.Delay(1000);
            await PublishWithRetryAsync(message, retryCount - 1, routingKey);
        }
    }

    private async Task PublishInternalAsync<T>(
        T message, 
        string routingKey, 
        string exchangeType = ExchangeType.Fanout,
        IBasicProperties? properties = null) where T : class
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RabbitMqPublisher));
        if (message == null) throw new ArgumentNullException(nameof(message));

        var exchangeName = GetExchangeName<T>();

        _channel.ExchangeDeclare(
            exchange: exchangeName,
            type: exchangeType,
            durable: true,
            autoDelete: false,
            arguments: exchangeType == "x-delayed-message"
                ? new Dictionary<string, object> { {"x-delayed-type", "direct"} }
                : null);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        _channel.BasicPublish(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body);

        _logger.LogDebug("Published message to {Exchange} with routing {RoutingKey}", exchangeName, routingKey);

        await Task.CompletedTask; // Mantém async sem warnings CS1998
    }

    private string GetExchangeName<T>() => $"{typeof(T).Name.ToLower()}.exchange";

    private void OnConnectionShutdown(object? sender, ShutdownEventArgs e)
    {
        _logger.LogWarning("RabbitMQ connection shutdown: {ReplyText}", e.ReplyText);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            try
            {
                _channel?.Close();
                _connection?.Close();
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing RabbitMQ resources");
            }
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~RabbitMqPublisher() => Dispose(false);
}
