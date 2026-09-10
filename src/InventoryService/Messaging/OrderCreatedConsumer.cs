using System.Text.Json;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using Microsoft.Extensions.Caching.Distributed;
using InventoryService.Controllers;
using InventoryService.Services;

namespace InventoryService.Messaging;

public class OrderCreatedConsumer : BackgroundService
{
    private const string ExchangeName = "orders.exchange";
    private const string QueueName = "inventory.order-created";
    private const string RoutingKey = "order.created";
    private const string DeadLetterExchangeName = "orders.dead-letter.exchange";
    private const string DeadLetterQueueName = "inventory.order-created.dead-letter";
    private const string DeadLetterRoutingKey = "order.created.failed";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<OrderCreatedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMq:Host"] ?? "localhost",
            Port = int.Parse(_configuration["RabbitMq:Port"] ?? "5672"),
            UserName = _configuration["RabbitMq:UserName"] ?? "guest",
            Password = _configuration["RabbitMq:Password"] ?? "guest"
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConsumerAsync(factory, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "RabbitMQ consumer could not connect. Retrying in 5 seconds.");

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task RunConsumerAsync(ConnectionFactory factory, CancellationToken stoppingToken)
    {
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Direct, durable: true, cancellationToken: stoppingToken);
        await channel.ExchangeDeclareAsync(DeadLetterExchangeName, ExchangeType.Direct, durable: true, cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(
            DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);
        await channel.QueueBindAsync(
            DeadLetterQueueName,
            DeadLetterExchangeName,
            DeadLetterRoutingKey,
            cancellationToken: stoppingToken);

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
            arguments: queueArguments,
            cancellationToken: stoppingToken);
        await channel.QueueBindAsync(QueueName, ExchangeName, RoutingKey, cancellationToken: stoppingToken);
        await channel.BasicQosAsync(0, 10, false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<OrderCreatedEvent>(ea.Body.ToArray());
                if (message is null)
                {
                    throw new InvalidOperationException("Received an invalid OrderCreated message.");
                }

                await using var scope = _scopeFactory.CreateAsyncScope();
                var inventoryStore = scope.ServiceProvider.GetRequiredService<IInventoryStore>();
                var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

                await inventoryStore.ReserveStockAsync(message.ProductId, message.Quantity, stoppingToken);
                await InventoryController.InvalidateCachesAsync(cache, message.ProductId, stoppingToken);

                _logger.LogInformation(
                    "Received OrderCreated event asynchronously. OrderId={OrderId}, ProductId={ProductId}, Quantity={Quantity}",
                    message.OrderId,
                    message.ProductId,
                    message.Quantity);

                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // The host is shutting down; leave the message unacknowledged.
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "OrderCreated message processing failed. Sending message to the dead-letter queue.");

                await channel.BasicNackAsync(
                    ea.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("RabbitMQ consumer started for queue {QueueName}", QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }
}
