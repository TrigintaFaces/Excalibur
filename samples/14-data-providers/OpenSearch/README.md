# OpenSearch

Demonstrates ALL OpenSearch capabilities provided by `Excalibur.Data.OpenSearch`.

## Prerequisites

OpenSearch 2.x running locally:

```bash
docker run -d -p 9200:9200 \
  -e "discovery.type=single-node" \
  -e "DISABLE_SECURITY_PLUGIN=true" \
  opensearchproject/opensearch:2.11.0
```

## Capabilities Demonstrated

| Section | Capability | API |
|---------|-----------|-----|
| 1 | Single-node DI registration | `AddOpenSearchServices(nodeUri, configureSettings)` |
| 2 | Multi-node cluster setup | `AddOpenSearchServices(nodeUris, configureSettings)` |
| 3 | Preconfigured client | `AddOpenSearchServices(client, registry)` |
| 4 | Resilience configuration | `OpenSearchResilienceOptions`, `CircuitBreakerOptions`, `OpenSearchRetryPolicyOptions`, `OpenSearchTimeoutOptions` |
| 5 | Persistence provider | `AddOpenSearchPersistence(options)` with `OpenSearchPersistenceOptions` |
| 6 | Dead letter handling | `OpenSearchDeadLetterHandler` with `OpenSearchDeadLetterOptions` |
| 7 | Health checks | `AddOpenSearchHealthCheck(name, timeout)` |

## Key Concepts

- **`AddOpenSearchServices`** -- Registers `OpenSearchClient` as a singleton. Supports single URI, multi-URI (StaticConnectionPool), or a preconfigured client instance.
- **`ConnectionSettings`** -- The OpenSearch.Client configuration object. Customize default index, request timeout, debug mode, and field name inference.
- **`AddOpenSearchHealthCheck`** -- Registers an `IHealthCheck` that queries `/_cluster/health` and reports green/yellow as healthy, red as unhealthy.
- **`VerifyOpenSearchConnectivityAsync`** -- Host extension that pings the cluster at startup and throws if unreachable.
- **`AddOpenSearchPersistence`** -- Registers `IPersistenceProvider` (keyed as `"opensearch"` and `"default"`) with configurable index prefix, shard count, replica count, and refresh policy.
- **`OpenSearchDeadLetterHandler`** -- Routes failed documents to a dated dead letter index (`dlq-prefix-yyyy-MM`) and supports retrying stored dead letters.
- **`OpenSearchResilienceOptions`** -- Composes retry policy (exponential backoff with jitter), circuit breaker (failure rate threshold), and per-operation timeouts.

## Additional Capabilities (Not Shown Inline)

These are registered through separate DI extensions and builders:

| Capability | Registration | Description |
|-----------|-------------|-------------|
| Projection Store | `AddOpenSearchProjectionStore<T>(nodeUri)` | Event sourcing read model store per projection type |
| Materialized Views | `builder.UseOpenSearch(options)` via `IMaterializedViewsBuilder` | CDC-style materialized view store |
| Tenant Sharding | `UseOpenSearchTenantProjectionStore<T>()` via `IEventSourcingBuilder` | Index-per-tenant projection isolation |
| Index Management | `IIndexOperationsManager` | Create, delete, check, and update indices programmatically |

## Run

```bash
dotnet run
```
