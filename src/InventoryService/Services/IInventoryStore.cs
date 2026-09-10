using InventoryService.Models;

namespace InventoryService.Services;

public interface IInventoryStore
{
    Task<List<InventoryItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<InventoryItem?> GetByProductIdAsync(
        int productId,
        CancellationToken cancellationToken = default);

    Task<(InventoryItem Item, bool Created)> AddStockAsync(
        int productId,
        string name,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<bool> ReserveStockAsync(
        int productId,
        int quantity,
        CancellationToken cancellationToken = default);
}
