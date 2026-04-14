using dotnet10.Options;
using Microsoft.Extensions.Options;
using Npgsql;

namespace dotnet10.Infrastructure;

public sealed class PostgresShardRegistry : IAsyncDisposable
{
    private readonly IReadOnlyDictionary<int, PostgresShard> _shardsById;

    public PostgresShardRegistry(IOptions<ShardingOptions> options, ILoggerFactory loggerFactory)
    {
        OrderedShards = options.Value.Shards
            .OrderBy(shard => shard.ShardId)
            .Select(shard => new PostgresShard(
                BuildDataSource(shard.ConnectionString, loggerFactory),
                shard.ShardId,
                shard.Name))
            .ToArray();

        _shardsById = OrderedShards.ToDictionary(shard => shard.ShardId);
    }

    public IReadOnlyList<PostgresShard> OrderedShards { get; }

    public PostgresShard GetByShardId(int shardId) => _shardsById[shardId];

    public async ValueTask DisposeAsync()
    {
        foreach (var shard in OrderedShards)
        {
            await shard.DisposeAsync();
        }
    }

    private static NpgsqlDataSource BuildDataSource(string connectionString, ILoggerFactory loggerFactory)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseLoggerFactory(loggerFactory);
        return builder.Build();
    }
}
