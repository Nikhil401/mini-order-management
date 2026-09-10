using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace OrderService.Messaging;

public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private const string ExchangeName = "orders.exchange";
    private const string RoutingKey = "order.created";
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMq:Host"] ?? "localhost",
            Port = int.Parse(configuration["RabbitMq:Port"] ?? "5672"),
            UserName = configuration["RabbitMq:UserName"] ?? "guest",
            Password = configuration["RabbitMq:Password"] ?? "guest"
        };

        _connection = factory.CreateConnectionAsync(cancellationToken: default).GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync(cancellationToken: default).GetAwaiter().GetResult();
        _channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();
    }

    public Task PublishOrderCreatedAsync(OrderCreatedEvent message, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message);
        var properties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent
        };

        _channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: payload).GetAwaiter().GetResult();

        _logger.LogInformation(
            "Published OrderCreated event. OrderId={OrderId}, ProductId={ProductId}, Quantity={Quantity}",
            message.OrderId,
            message.ProductId,
            message.Quantity);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
