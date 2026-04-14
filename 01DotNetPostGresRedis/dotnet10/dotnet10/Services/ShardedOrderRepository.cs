using dotnet10.Infrastructure;
using dotnet10.Models;
using Npgsql;

namespace dotnet10.Services;

public sealed class ShardedOrderRepository(
    PostgresShardRegistry registry,
    IShardRouter shardRouter,
    OrderCache cache,
    TimeProvider timeProvider)
{
    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(request.Amount, 0m);

        var customerName = NormalizeRequiredValue(request.CustomerName, nameof(request.CustomerName));
        var description = NormalizeRequiredValue(request.Description, nameof(request.Description));
        var shard = shardRouter.Resolve(request.TenantId);
        var order = new OrderResponse(
            Guid.NewGuid(),
            request.TenantId,
            customerName,
            description,
            request.Amount,
            timeProvider.GetUtcNow().UtcDateTime,
            shard.ShardId,
            shard.Name,
            "database");

        const string sql = """
            INSERT INTO orders (id, tenant_id, customer_name, description, amount, created_utc)
            VALUES ($1, $2, $3, $4, $5, $6);
            """;

        await using var command = shard.DataSource.CreateCommand(sql);
        command.Parameters.Add(new NpgsqlParameter { Value = order.Id });
        command.Parameters.Add(new NpgsqlParameter { Value = order.TenantId });
        command.Parameters.Add(new NpgsqlParameter { Value = order.CustomerName });
        command.Parameters.Add(new NpgsqlParameter { Value = order.Description });
        command.Parameters.Add(new NpgsqlParameter { Value = order.Amount });
        command.Parameters.Add(new NpgsqlParameter { Value = order.CreatedUtc });

        await command.ExecuteNonQueryAsync(cancellationToken);

        return order;
    }

    public async Task<OrderResponse?> GetAsync(int tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        var cachedOrder = await cache.GetAsync(tenantId, orderId, cancellationToken);

        if (cachedOrder is not null)
        {
            return cachedOrder;
        }

        var shard = shardRouter.Resolve(tenantId);
        const string sql = """
            SELECT id, tenant_id, customer_name, description, amount, created_utc
            FROM orders
            WHERE tenant_id = $1 AND id = $2;
            """;

        await using var command = shard.DataSource.CreateCommand(sql);
        command.Parameters.Add(new NpgsqlParameter { Value = tenantId });
        command.Parameters.Add(new NpgsqlParameter { Value = orderId });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var order = MapOrder(reader, shard, "database");
        await cache.SetAsync(order, cancellationToken);

        return order;
    }

    public async Task<IReadOnlyList<OrderResponse>> ListForTenantAsync(int tenantId, CancellationToken cancellationToken)
    {
        var shard = shardRouter.Resolve(tenantId);
        const string sql = """
            SELECT id, tenant_id, customer_name, description, amount, created_utc
            FROM orders
            WHERE tenant_id = $1
            ORDER BY created_utc DESC;
            """;

        List<OrderResponse> orders = [];

        await using var command = shard.DataSource.CreateCommand(sql);
        command.Parameters.Add(new NpgsqlParameter { Value = tenantId });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            orders.Add(MapOrder(reader, shard, "database"));
        }

        return orders;
    }

    public async Task<IReadOnlyList<OrderResponse>> ListAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, tenant_id, customer_name, description, amount, created_utc
            FROM orders
            ORDER BY created_utc DESC;
            """;

        List<OrderResponse> orders = [];

        foreach (var shard in registry.OrderedShards)
        {
            await using var command = shard.DataSource.CreateCommand(sql);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                orders.Add(MapOrder(reader, shard, "database"));
            }
        }

        return orders
            .OrderByDescending(order => order.CreatedUtc)
            .ToArray();
    }

    public async Task<IReadOnlyList<ShardSummary>> GetShardSummaryAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COUNT(*)::bigint AS order_count,
                COALESCE(SUM(amount), 0)::numeric(18,2) AS total_amount,
                MAX(created_utc) AS latest_order_created_utc,
                COALESCE(array_agg(DISTINCT tenant_id ORDER BY tenant_id), ARRAY[]::integer[]) AS tenant_ids
            FROM orders;
            """;

        List<ShardSummary> summaries = [];

        foreach (var shard in registry.OrderedShards)
        {
            await using var command = shard.DataSource.CreateCommand(sql);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);

            summaries.Add(new ShardSummary(
                shard.ShardId,
                shard.Name,
                reader.GetInt64(0),
                reader.GetFieldValue<decimal>(1),
                reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTime>(2),
                reader.GetFieldValue<int[]>(3)));
        }

        return summaries;
    }

    public async Task<bool> DeleteAsync(int tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        var shard = shardRouter.Resolve(tenantId);
        const string sql = """
            DELETE FROM orders
            WHERE tenant_id = $1 AND id = $2;
            """;

        await using var command = shard.DataSource.CreateCommand(sql);
        command.Parameters.Add(new NpgsqlParameter { Value = tenantId });
        command.Parameters.Add(new NpgsqlParameter { Value = orderId });

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            return false;
        }

        await cache.RemoveAsync(tenantId, orderId, cancellationToken);
        return true;
    }

    private static OrderResponse MapOrder(NpgsqlDataReader reader, PostgresShard shard, string source) =>
        new(
            reader.GetGuid(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetFieldValue<decimal>(4),
            reader.GetFieldValue<DateTime>(5),
            shard.ShardId,
            shard.Name,
            source);

    private static string NormalizeRequiredValue(string? value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        return value.Trim();
    }
}
