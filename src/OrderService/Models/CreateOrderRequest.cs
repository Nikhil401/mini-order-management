using System.ComponentModel.DataAnnotations;

namespace OrderService.Models;

public record CreateOrderRequest(
    [Range(1, int.MaxValue)] int ProductId,
    [Range(1, int.MaxValue)] int Quantity);
