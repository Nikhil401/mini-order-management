using System.Text.Json;
using InventoryService.Models;
using InventoryService.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController(
    IDistributedCache cache,
    IInventoryStore inventoryStore,
    ILogger<InventoryController> logger) : ControllerBase
{
    public async Task<bool> ReserveStockAsync(
        int productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        return await inventoryStore.ReserveStockAsync(productId, quantity, cancellationToken);
    }

    private static string GetAllCacheKey() => "inventory:all";
    private static string GetItemCacheKey(int productId) => $"inventory:item:{productId}";

    private async Task CacheInventoryItemAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        await cache.SetStringAsync(
            GetItemCacheKey(item.ProductId),
            JsonSerializer.Serialize(item),
            new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(5)
            },
            cancellationToken);
    }

    private async Task CacheAllItemsAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken)
    {
        await cache.SetStringAsync(
            GetAllCacheKey(),
            JsonSerializer.Serialize(items),
            new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(2)
            },
            cancellationToken);
    }

    public static async Task InvalidateCachesAsync(
        IDistributedCache cache,
        int? productId = null,
        CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(GetAllCacheKey(), cancellationToken);

        if (productId is not null)
        {
            await cache.RemoveAsync(GetItemCacheKey(productId.Value), cancellationToken);
        }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItem>>> GetAll(
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Inventory request received for all items. TraceId={TraceId}",
            HttpContext.TraceIdentifier);

        var cacheKey = GetAllCacheKey();
        var cachedItems = await cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedItems))
        {
            logger.LogInformation("Inventory all-items cache hit. TraceId={TraceId}", HttpContext.TraceIdentifier);
            var cachedInventory = JsonSerializer.Deserialize<List<InventoryItem>>(cachedItems);
            return Ok(cachedInventory);
        }

        logger.LogInformation("Inventory all-items cache miss. TraceId={TraceId}", HttpContext.TraceIdentifier);
        var items = await inventoryStore.GetAllAsync(cancellationToken);
        await CacheAllItemsAsync(items, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{productId:int}")]
    public async Task<ActionResult<InventoryItem>> GetByProductId(
        int productId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Inventory lookup started for ProductId={ProductId}. TraceId={TraceId}",
            productId,
            HttpContext.TraceIdentifier);

        var cacheKey = GetItemCacheKey(productId);
        var cachedItem = await cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedItem))
        {
            logger.LogInformation("Inventory item cache hit for ProductId={ProductId}. TraceId={TraceId}", productId, HttpContext.TraceIdentifier);
            var cachedInventoryItem = JsonSerializer.Deserialize<InventoryItem>(cachedItem);
            return Ok(cachedInventoryItem);
        }

        logger.LogInformation("Inventory item cache miss for ProductId={ProductId}. TraceId={TraceId}", productId, HttpContext.TraceIdentifier);

        var item = await inventoryStore.GetByProductIdAsync(productId, cancellationToken);

        if (item is null)
        {
            logger.LogWarning(
                "Inventory lookup failed for ProductId={ProductId}. TraceId={TraceId}",
                productId,
                HttpContext.TraceIdentifier);

            return NotFound(new { message = $"Product {productId} was not found." });
        }

        logger.LogInformation(
            "Inventory lookup succeeded for ProductId={ProductId}. Quantity={Quantity}. TraceId={TraceId}",
            item.ProductId,
            item.Quantity,
            HttpContext.TraceIdentifier);

        await CacheInventoryItemAsync(item, cancellationToken);

        return Ok(item);
    }

    [HttpPost("stock")]
    public async Task<ActionResult<InventoryItem>> AddStock(
        AddInventoryStockRequest request,
        CancellationToken cancellationToken)
    {
        var result = await inventoryStore.AddStockAsync(
            request.ProductId,
            request.Name,
            request.Quantity,
            cancellationToken);
        var item = result.Item;
        var created = result.Created;

        await InvalidateCachesAsync(cache, item.ProductId, cancellationToken);

        logger.LogInformation(
            "Inventory stock added. ProductId={ProductId} AddedQuantity={AddedQuantity} NewQuantity={NewQuantity} Created={Created} TraceId={TraceId}",
            item.ProductId,
            request.Quantity,
            item.Quantity,
            created,
            HttpContext.TraceIdentifier);

        return created
            ? CreatedAtAction(nameof(GetByProductId), new { productId = item.ProductId }, item)
            : Ok(item);
    }
}
