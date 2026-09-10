namespace OrderService.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishOrderCreatedAsync(OrderCreatedEvent message, CancellationToken cancellationToken = default);
}
