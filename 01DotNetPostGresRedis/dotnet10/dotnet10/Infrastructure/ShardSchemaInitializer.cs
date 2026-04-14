namespace dotnet10.Infrastructure;

public sealed class ShardSchemaInitializer(
    PostgresShardRegistry registry,
    ILogger<ShardSchemaInitializer> logger) : IHostedService
{
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS orders (
            id uuid PRIMARY KEY,
            tenant_id integer NOT NULL,
            customer_name text NOT NULL,
            description text NOT NULL,
            amount numeric(18,2) NOT NULL,
            created_utc timestamp with time zone NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_orders_tenant_created_utc
            ON orders (tenant_id, created_utc DESC);
        """;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var shard in registry.OrderedShards)
        {
            logger.LogInformation(
                "Ensuring schema exists on shard {ShardId} ({ShardName})",
                shard.ShardId,
                shard.Name);

            await using var command = shard.DataSource.CreateCommand(SchemaSql);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
