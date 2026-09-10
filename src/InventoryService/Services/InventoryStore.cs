using InventoryService.Data;
using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Services;

public class InventoryStore(InventoryDbContext dbContext) : IInventoryStore
{
    public Task<List<InventoryItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.InventoryItems
            .AsNoTracking()
            .OrderBy(item => item.ProductId)
            .ToListAsync(cancellationToken);
    }

    public Task<InventoryItem?> GetByProductIdAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ProductId == productId, cancellationToken);
    }

    public async Task<(InventoryItem Item, bool Created)> AddStockAsync(
        int productId,
        string name,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.InventoryItems
            .FirstOrDefaultAsync(existing => existing.ProductId == productId, cancellationToken);

        var created = item is null;
        if (item is null)
        {
            item = new InventoryItem
            {
                ProductId = productId,
                Name = name.Trim(),
                Quantity = quantity
            };
            dbContext.InventoryItems.Add(item);
        }
        else
        {
            item.Name = name.Trim();
            item.Quantity += quantity;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (item, created);
    }

    public async Task<bool> ReserveStockAsync(
        int productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.InventoryItems
            .FirstOrDefaultAsync(existing => existing.ProductId == productId, cancellationToken);

        if (item is null || item.Quantity < quantity)
        {
            return false;
        }

        item.Quantity -= quantity;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
