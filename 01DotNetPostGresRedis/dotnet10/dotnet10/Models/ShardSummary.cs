namespace dotnet10.Models;

public sealed record ShardSummary(
    int ShardId,
    string ShardName,
    long OrderCount,
    decimal TotalAmount,
    DateTime? LatestOrderCreatedUtc,
    IReadOnlyList<int> TenantIds);
