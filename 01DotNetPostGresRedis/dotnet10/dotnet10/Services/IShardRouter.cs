using dotnet10.Infrastructure;

namespace dotnet10.Services;

public interface IShardRouter
{
    PostgresShard Resolve(int tenantId);
}

public sealed class ModuloShardRouter(PostgresShardRegistry registry) : IShardRouter
{
    public PostgresShard Resolve(int tenantId)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tenantId), tenantId, "TenantId must be greater than zero.");
        }

        var shardIndex = tenantId % registry.OrderedShards.Count;
        return registry.OrderedShards[shardIndex];
    }
}
