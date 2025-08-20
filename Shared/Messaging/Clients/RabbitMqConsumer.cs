using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Messaging.Interfaces;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace Shared.Messaging.Clients;

public class RabbitMqConsumer : IMessageConsumer, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqConsumer> _logger;
    private AsyncEventingBasicConsumer? _consumer;
    private bool _disposed;
    private string? _currentQueueName;

    public event EventHandler<MessageConsumerErrorEventArgs>? OnError;
    public event EventHandler<MessageConsumerStartedEventArgs>? OnStarted;

    public RabbitMqConsumer(string hostName, ILogger<RabbitMqConsumer> logger)
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

    public async Task StartConsumingAsync<T>(
        string queueName,
        Func<T, Task> messageHandler,
        CancellationToken cancellationToken = default) where T : class
    {
        _currentQueueName = queueName;

        try
        {
            _channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            _consumer = new AsyncEventingBasicConsumer(_channel);

            _consumer.Received += async (model, ea) =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    _logger.LogDebug("Message received from {QueueName}", queueName);

                    var messageObject = JsonSerializer.Deserialize<T>(message);
                    if (messageObject != null)
                        await messageHandler(messageObject);

                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "JSON deserialization error");
                    OnError?.Invoke(this, new MessageConsumerErrorEventArgs(jsonEx, queueName));
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    OnError?.Invoke(this, new MessageConsumerErrorEventArgs(ex, queueName));
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            _channel.BasicConsume(
                queue: queueName,
                autoAck: false,
                consumer: _consumer);

            OnStarted?.Invoke(this, new MessageConsumerStartedEventArgs(queueName));

            await Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(1000);
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting consumer");
            OnError?.Invoke(this, new MessageConsumerErrorEventArgs(ex, queueName));
            throw;
        }
    }

    public void ConfigureQos(ushort prefetchSize = 0, ushort prefetchCount = 1, bool global = false)
    {
        _channel.BasicQos(prefetchSize, prefetchCount, global);
    }

    public string CreateTempQueue()
    {
        var result = _channel.QueueDeclare(
            queue: "",
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null);

        return result.QueueName;
    }

    private void OnConnectionShutdown(object? sender, ShutdownEventArgs e)
    {
        _logger.LogWarning("RabbitMQ connection shutdown: {ReplyText}", e.ReplyText);
        OnError?.Invoke(this, new MessageConsumerErrorEventArgs(
            new Exception($"Connection shutdown: {e.ReplyText}"),
            _currentQueueName ?? string.Empty));
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

    ~RabbitMqConsumer()
    {
        Dispose(false);
    }
}
