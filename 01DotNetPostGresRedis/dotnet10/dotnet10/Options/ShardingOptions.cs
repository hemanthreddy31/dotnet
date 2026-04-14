using System.ComponentModel.DataAnnotations;

namespace dotnet10.Options;

public sealed class ShardingOptions
{
    public const string SectionName = "Sharding";

    [MinLength(2)]
    public List<PostgresShardOptions> Shards { get; init; } = [];
}

public sealed class PostgresShardOptions
{
    public int ShardId { get; init; }

    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string ConnectionString { get; init; } = string.Empty;
}
