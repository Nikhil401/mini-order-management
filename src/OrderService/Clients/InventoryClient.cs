using InventoryService.Grpc;
using OrderService.Models;

namespace OrderService.Clients;

public class InventoryClient(InventoryLookup.InventoryLookupClient grpcClient) : IInventoryClient
{
    public async Task<InventoryItemResponse?> GetByProductIdAsync(
        int productId,
        CancellationToken cancellationToken)
    {
        var reply = await grpcClient.GetByProductIdAsync(
            new GetInventoryItemRequest { ProductId = productId },
            cancellationToken: cancellationToken);

        if (!reply.Found)
        {
            return null;
        }

        return new InventoryItemResponse(reply.ProductId, reply.Name, reply.Quantity);
    }
}
