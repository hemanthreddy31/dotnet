using Npgsql;

namespace dotnet10.Infrastructure;

public sealed class PostgresShard(NpgsqlDataSource dataSource, int shardId, string name) : IAsyncDisposable
{
    public NpgsqlDataSource DataSource { get; } = dataSource;

    public int ShardId { get; } = shardId;

    public string Name { get; } = name;

    public ValueTask DisposeAsync() => DataSource.DisposeAsync();
}
