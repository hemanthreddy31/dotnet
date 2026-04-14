using System.Text.Json;
using dotnet10.Models;
using dotnet10.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace dotnet10.Services;

public sealed class OrderCache(IDistributedCache cache, IOptions<RedisSettings> settings)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<OrderResponse?> GetAsync(int tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        var payload = await cache.GetStringAsync(GetKey(tenantId, orderId), cancellationToken);

        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var order = JsonSerializer.Deserialize<OrderResponse>(payload, SerializerOptions);
        return order is null ? null : order with { Source = "redis-cache" };
    }

    public Task SetAsync(OrderResponse order, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(order with { Source = "database" }, SerializerOptions);
        var entryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(settings.Value.DefaultExpirationMinutes)
        };

        return cache.SetStringAsync(GetKey(order.TenantId, order.Id), payload, entryOptions, cancellationToken);
    }

    public Task RemoveAsync(int tenantId, Guid orderId, CancellationToken cancellationToken) =>
        cache.RemoveAsync(GetKey(tenantId, orderId), cancellationToken);

    private static string GetKey(int tenantId, Guid orderId) => $"orders:tenant:{tenantId}:order:{orderId:N}";
}
