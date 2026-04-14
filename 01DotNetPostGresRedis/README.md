# .NET 10 PostgreSQL Sharding + Redis Cache Demo

This repository now demonstrates a .NET 10 Web API that:

- Routes writes and reads to one of two PostgreSQL servers using application-level sharding.
- Uses Redis as a distributed cache with a cache-aside pattern.
- Runs the complete stack with Docker Compose.
- Persists PostgreSQL and Redis data with named Docker volumes.
- Exposes health endpoints and OpenAPI for quick verification.

## What Was Implemented

1. The starter weather sample was replaced with a shard-aware `OrdersController`.
2. The API now binds typed `Sharding` and `Redis` configuration at startup and validates it on boot.
3. Two `NpgsqlDataSource` instances are created, one per PostgreSQL shard.
4. A `ModuloShardRouter` sends each tenant to a shard using `tenantId % shardCount`.
5. A startup hosted service creates the `orders` table on both shards.
6. Reads use Redis through `IDistributedCache` and `AddStackExchangeRedisCache`.
7. `/health` and `/alive` endpoints are mapped for container health checks.
8. `compose.yaml` starts the API, both PostgreSQL shards, and Redis with named volumes.
9. The Dockerfile was fixed so the project restore includes the shared `dotnet10.ServiceDefaults` project.

## Architecture

```text
Client
  |
  v
.NET 10 API
  |-- cache-aside reads/writes --> Redis
  |
  |-- tenantId % 2 == 0 -------> PostgreSQL shard 0
  |
  |-- tenantId % 2 == 1 -------> PostgreSQL shard 1
```

This is application-level sharding, not PostgreSQL table partitioning. The API decides which PostgreSQL server owns a tenant, so each shard is an independent database server with the same schema.

## Key Files

- `compose.yaml`
- `dotnet10/dotnet10/Program.cs`
- `dotnet10/dotnet10/Controllers/OrdersController.cs`
- `dotnet10/dotnet10/Services/ShardedOrderRepository.cs`
- `dotnet10/dotnet10/Services/OrderCache.cs`
- `dotnet10/dotnet10/Infrastructure/PostgresShardRegistry.cs`
- `dotnet10/dotnet10/Infrastructure/ShardSchemaInitializer.cs`
- `dotnet10/dotnet10/appsettings.json`
- `dotnet10/dotnet10/Dockerfile`

## Latest Official Documentation Used

- .NET 10 releases and support: https://learn.microsoft.com/en-us/dotnet/core/releases-and-support
- ASP.NET Core distributed caching: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-10.0
- `AddStackExchangeRedisCache` API: https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.stackexchangerediscacheservicecollectionextensions?view=net-10.0-pp
- Npgsql basic usage and `NpgsqlDataSource`: https://www.npgsql.org/doc/basic-usage.html
- Docker Compose quickstart, health checks, and named volumes: https://docs.docker.com/compose/gettingstarted/
- PostgreSQL Docker official image: https://hub.docker.com/_/postgres
- Redis persistence: https://redis.io/docs/latest/operate/oss_and_stack/management/persistence/
- Run Redis on Docker: https://redis.io/docs/latest/operate/oss_and_stack/install/install-stack/docker/

## Prerequisites

- .NET 10 SDK
- Docker Desktop with Linux containers enabled
- Free host ports: `8080`, `5433`, `5434`, `6379`

## Run The Full Stack On Docker

### 1. Start everything

From the repository root:

```powershell
docker compose up --build -d
```

### 2. Check container health

```powershell
docker compose ps
Invoke-RestMethod http://localhost:8080/health
Invoke-RestMethod http://localhost:8080/
```

Expected result:

- `postgres-shard-0`, `postgres-shard-1`, and `redis` become `healthy`.
- `api` becomes `running`.
- `http://localhost:8080/` returns the list of demo endpoints.

### 3. Create data on both shards

Even tenant IDs go to shard `0`. Odd tenant IDs go to shard `1`.

```powershell
$orderOnShard0 = Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:8080/api/orders `
  -ContentType 'application/json' `
  -Body (@{
    tenantId = 100
    customerName = 'Ada Lovelace'
    description = 'Even tenant routed to shard 0'
    amount = 149.99
  } | ConvertTo-Json)

$orderOnShard1 = Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:8080/api/orders `
  -ContentType 'application/json' `
  -Body (@{
    tenantId = 101
    customerName = 'Grace Hopper'
    description = 'Odd tenant routed to shard 1'
    amount = 249.50
  } | ConvertTo-Json)

$orderOnShard0
$orderOnShard1
```

### 4. Verify sharding through the API

```powershell
Invoke-RestMethod http://localhost:8080/api/orders/shards/summary
Invoke-RestMethod http://localhost:8080/api/orders
Invoke-RestMethod http://localhost:8080/api/orders/tenant/100
Invoke-RestMethod http://localhost:8080/api/orders/tenant/101
```

Expected behavior:

- The order for tenant `100` reports `shardId = 0`.
- The order for tenant `101` reports `shardId = 1`.
- `/api/orders/shards/summary` shows one order on each shard.

### 5. Prove Redis caching

The first `GET` after `POST` reads from PostgreSQL and populates Redis. The second `GET` should come from Redis and return `source = redis-cache`.

```powershell
$firstRead = Invoke-RestMethod "http://localhost:8080/api/orders/tenant/100/$($orderOnShard0.id)"
$secondRead = Invoke-RestMethod "http://localhost:8080/api/orders/tenant/100/$($orderOnShard0.id)"

$firstRead.source
$secondRead.source
```

Expected output:

- First read: `database`
- Second read: `redis-cache`

### 6. Inspect PostgreSQL data directly in each shard

```powershell
docker compose exec postgres-shard-0 psql -U appuser -d orders -c "SELECT id, tenant_id, customer_name, amount, created_utc FROM orders ORDER BY created_utc DESC;"
docker compose exec postgres-shard-1 psql -U appuser -d orders -c "SELECT id, tenant_id, customer_name, amount, created_utc FROM orders ORDER BY created_utc DESC;"
```

Expected behavior:

- `postgres-shard-0` contains tenant `100`.
- `postgres-shard-1` contains tenant `101`.

### 7. Inspect Redis keys directly

```powershell
docker compose exec redis redis-cli KEYS "sharded-orders:*"
```

After at least one cached read, you should see a key that starts with `sharded-orders:orders:tenant:100:order:`.

### 8. Prove persistent volumes

Stop and start the stack without removing volumes:

```powershell
docker compose down
docker compose up -d
```

Now verify the data is still there:

```powershell
Invoke-RestMethod http://localhost:8080/api/orders/shards/summary
docker compose exec postgres-shard-0 psql -U appuser -d orders -c "SELECT COUNT(*) FROM orders;"
docker compose exec postgres-shard-1 psql -U appuser -d orders -c "SELECT COUNT(*) FROM orders;"
```

You should still see the original rows because data is stored in these named volumes:

- `dotnet10-postgres-shard-0-data`
- `dotnet10-postgres-shard-1-data`
- `dotnet10-redis-data`

### 9. Reset the environment completely

This removes containers and named volumes:

```powershell
docker compose down -v
```

Use this only when you intentionally want a clean slate.

## Optional: Run API On Host, Infra In Docker

If you want to debug the API outside Docker while still using the same PostgreSQL and Redis containers:

```powershell
docker compose up -d postgres-shard-0 postgres-shard-1 redis
dotnet run --project .\dotnet10\dotnet10\dotnet10.csproj
```

The default `appsettings.json` already points to:

- PostgreSQL shard 0 at `localhost:5433`
- PostgreSQL shard 1 at `localhost:5434`
- Redis at `localhost:6379`

## API Endpoints

- `GET /`
- `GET /openapi/v1.json`
- `GET /health`
- `GET /alive`
- `POST /api/orders`
- `GET /api/orders`
- `GET /api/orders/tenant/{tenantId}`
- `GET /api/orders/tenant/{tenantId}/{orderId}`
- `DELETE /api/orders/tenant/{tenantId}/{orderId}`
- `GET /api/orders/shards/summary`

## Example Request Body

```json
{
  "tenantId": 100,
  "customerName": "Ada Lovelace",
  "description": "Even tenant routed to shard 0",
  "amount": 149.99
}
```

## Notes About The Implementation

- Shard routing is deterministic and based only on `tenantId`.
- Every shard uses the same `orders` schema.
- `/api/orders` and `/api/orders/shards/summary` intentionally fan out across all shards.
- Redis is used as a distributed cache for order lookups, not as the primary data store.
- Redis persistence is enabled with append-only file mode in the Compose service.
- PostgreSQL uses version `18` in Docker Compose, and its named volumes are mounted at `/var/lib/postgresql`.

## Troubleshooting

- If `api` restarts immediately, check `docker compose logs api`.
- If PostgreSQL services never become healthy, check `docker compose logs postgres-shard-0` and `docker compose logs postgres-shard-1`.
- If Redis caching is not visible, run the same `GET` twice and inspect the `source` field.
- If you want to inspect the fully expanded Compose file, run `docker compose config`.
