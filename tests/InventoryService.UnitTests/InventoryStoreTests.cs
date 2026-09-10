using InventoryService.Data;
using InventoryService.Models;
using InventoryService.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InventoryService.UnitTests;

public class InventoryStoreTests
{
    [Fact]
    public async Task ReserveStockAsync_DecreasesQuantity()
    {
        await using var dbContext = CreateContext();
        dbContext.InventoryItems.Add(new InventoryItem
        {
            ProductId = 1,
            Name = "Keyboard",
            Quantity = 10
        });
        await dbContext.SaveChangesAsync();

        var store = new InventoryStore(dbContext);

        var result = await store.ReserveStockAsync(1, 3);

        Assert.True(result);
        Assert.Equal(7, await dbContext.InventoryItems
            .Where(item => item.ProductId == 1)
            .Select(item => item.Quantity)
            .SingleAsync());
    }

    [Fact]
    public async Task ReserveStockAsync_ReturnsFalseForUnknownProduct()
    {
        await using var dbContext = CreateContext();
        var store = new InventoryStore(dbContext);

        var result = await store.ReserveStockAsync(999, 1);

        Assert.False(result);
    }

    private static InventoryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new InventoryDbContext(options);
    }
}
