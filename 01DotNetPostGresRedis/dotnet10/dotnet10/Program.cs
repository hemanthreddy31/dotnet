using dotnet10.Health;
using dotnet10.Infrastructure;
using dotnet10.Options;
using dotnet10.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddOptions<ShardingOptions>()
    .Bind(builder.Configuration.GetSection(ShardingOptions.SectionName))
    .Validate(options => options.Shards.Count >= 2, "Configure at least two PostgreSQL shards.")
    .Validate(
        options => options.Shards.Select(shard => shard.ShardId).Distinct().Count() == options.Shards.Count,
        "Shard IDs must be unique.")
    .Validate(
        options => options.Shards.All(shard =>
            !string.IsNullOrWhiteSpace(shard.Name) &&
            !string.IsNullOrWhiteSpace(shard.ConnectionString)),
        "Each shard must define a name and connection string.")
    .ValidateOnStart();

builder.Services
    .AddOptions<RedisSettings>()
    .Bind(builder.Configuration.GetSection(RedisSettings.SectionName))
    .ValidateDataAnnotations()
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.ConnectionString), "Redis connection string is required.")
    .ValidateOnStart();

var redisSettings = builder.Configuration.GetSection(RedisSettings.SectionName).Get<RedisSettings>() ?? new RedisSettings();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PostgresShardRegistry>();
builder.Services.AddSingleton<IShardRouter, ModuloShardRouter>();
builder.Services.AddSingleton<OrderCache>();
builder.Services.AddSingleton<ShardedOrderRepository>();
builder.Services.AddHostedService<ShardSchemaInitializer>();
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var configuration = ConfigurationOptions.Parse(redisSettings.ConnectionString);
    configuration.AbortOnConnectFail = false;
    configuration.ClientName = "dotnet10-sharded-demo";
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisSettings.ConnectionString;
    options.InstanceName = redisSettings.InstanceName;
});

builder.Services.AddHealthChecks()
    .AddCheck<InfrastructureHealthCheck>("infrastructure", tags: ["ready"]);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapGet("/", (PostgresShardRegistry registry) => Results.Ok(new
{
    service = "dotnet10-sharded-postgres-redis-demo",
    shardCount = registry.OrderedShards.Count,
    routingStrategy = "tenantId % shardCount",
    openApi = "/openapi/v1.json",
    endpoints = new[]
    {
        "POST /api/orders",
        "GET /api/orders",
        "GET /api/orders/tenant/{tenantId}",
        "GET /api/orders/tenant/{tenantId}/{orderId}",
        "GET /api/orders/shards/summary",
        "DELETE /api/orders/tenant/{tenantId}/{orderId}",
        "GET /health",
        "GET /alive"
    }
}));
app.MapControllers();

app.Run();

public partial class Program;
