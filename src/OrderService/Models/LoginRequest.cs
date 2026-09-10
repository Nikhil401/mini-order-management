using System.ComponentModel.DataAnnotations;

namespace OrderService.Models;

public record LoginRequest(
    [Required] string Username,
    [Required] string Password);
