using InventoryService.Grpc;
using InventoryService.Services;

namespace InventoryService.Grpc;

public class InventoryLookupGrpcService : InventoryLookup.InventoryLookupBase
{
    private readonly IInventoryStore _inventoryStore;

    public InventoryLookupGrpcService(IInventoryStore inventoryStore)
    {
        _inventoryStore = inventoryStore;
    }

    public override async Task<InventoryItemReply> GetByProductId(
        GetInventoryItemRequest request,
        global::Grpc.Core.ServerCallContext context)
    {
        var item = await _inventoryStore.GetByProductIdAsync(request.ProductId, context.CancellationToken);

        return new InventoryItemReply
        {
            ProductId = item?.ProductId ?? 0,
            Name = item?.Name ?? string.Empty,
            Quantity = item?.Quantity ?? 0,
            Found = item is not null
        };
    }
}
