using OrderService.Models;

namespace OrderService.Clients;

public interface IInventoryClient
{
    Task<InventoryItemResponse?> GetByProductIdAsync(
        int productId,
        CancellationToken cancellationToken);
}
