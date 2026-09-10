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
    private const string QueueName = "inventory.order-created";
    private const string DeadLetterExchangeName = "orders.dead-letter.exchange";
    private const string DeadLetterQueueName = "inventory.order-created.dead-letter";
    private const string DeadLetterRoutingKey = "order.created.failed";
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
        DeclareTopologyAsync(_channel).GetAwaiter().GetResult();
    }

    private static async Task DeclareTopologyAsync(IChannel channel)
    {
        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Direct, durable: true);
        await channel.ExchangeDeclareAsync(DeadLetterExchangeName, ExchangeType.Direct, durable: true);
        await channel.QueueDeclareAsync(
            DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        await channel.QueueBindAsync(
            DeadLetterQueueName,
            DeadLetterExchangeName,
            DeadLetterRoutingKey);

        var queueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = DeadLetterExchangeName,
            ["x-dead-letter-routing-key"] = DeadLetterRoutingKey
        };

        await channel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments);
        await channel.QueueBindAsync(QueueName, ExchangeName, RoutingKey);
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
