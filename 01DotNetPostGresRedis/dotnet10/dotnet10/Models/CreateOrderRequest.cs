using System.ComponentModel.DataAnnotations;

namespace dotnet10.Models;

public sealed class CreateOrderRequest
{
    [Range(1, int.MaxValue)]
    public int TenantId { get; init; }

    [Required]
    [StringLength(120)]
    public string CustomerName { get; init; } = string.Empty;

    [Required]
    [StringLength(240)]
    public string Description { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999.99")]
    public decimal Amount { get; init; }
}
