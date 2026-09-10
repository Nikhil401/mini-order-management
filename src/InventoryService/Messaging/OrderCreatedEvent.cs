namespace InventoryService.Messaging;

public record OrderCreatedEvent(int OrderId, int ProductId, int Quantity, DateTimeOffset CreatedAtUtc);
