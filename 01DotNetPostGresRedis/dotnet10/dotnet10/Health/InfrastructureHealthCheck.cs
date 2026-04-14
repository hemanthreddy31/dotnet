using dotnet10.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace dotnet10.Health;

public sealed class InfrastructureHealthCheck(PostgresShardRegistry registry, IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        List<string> failures = [];

        foreach (var shard in registry.OrderedShards)
        {
            try
            {
                await using var command = shard.DataSource.CreateCommand("SELECT 1");
                var result = await command.ExecuteScalarAsync(cancellationToken);

                if (result is not int scalar || scalar != 1)
                {
                    failures.Add($"Shard {shard.Name} returned an unexpected probe result.");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"Shard {shard.Name} is unavailable: {ex.Message}");
            }
        }

        try
        {
            await redis.GetDatabase().PingAsync();
        }
        catch (Exception ex)
        {
            failures.Add($"Redis is unavailable: {ex.Message}");
        }

        return failures.Count == 0
            ? HealthCheckResult.Healthy("All PostgreSQL shards and Redis are reachable.")
            : HealthCheckResult.Unhealthy(string.Join(" ", failures));
    }
}
