using System.ComponentModel.DataAnnotations;

namespace InventoryService.Models;

public sealed class AddInventoryStockRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; init; }

    [Required]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}
