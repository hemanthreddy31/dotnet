using System.ComponentModel.DataAnnotations;

namespace dotnet10.Options;

public sealed class RedisSettings
{
    public const string SectionName = "Redis";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Required]
    public string InstanceName { get; init; } = "sharded-orders:";

    [Range(1, 1440)]
    public int DefaultExpirationMinutes { get; init; } = 10;
}
