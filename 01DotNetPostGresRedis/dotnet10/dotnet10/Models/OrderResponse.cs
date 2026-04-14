namespace dotnet10.Models;

public sealed record OrderResponse(
    Guid Id,
    int TenantId,
    string CustomerName,
    string Description,
    decimal Amount,
    DateTime CreatedUtc,
    int ShardId,
    string ShardName,
    string Source);
