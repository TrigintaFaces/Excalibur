# ElasticSearch Resilience Sample

Demonstrates three tiers of Elasticsearch registration and when to use each.

## Prerequisites

Elasticsearch running locally:

```bash
docker run -d --name es -p 9200:9200 \
  -e "discovery.type=single-node" \
  -e "xpack.security.enabled=false" \
  elasticsearch:8.15.0
```

## Registration Tiers

| Tier | Method | Retry | Circuit Breaker | Metrics | Tracing | Use When |
|------|--------|:-----:|:---------------:|:-------:|:-------:|----------|
| 1 -- Basic | `AddElasticsearchServices` | -- | -- | -- | -- | Development, low-traffic internal tools |
| 2 -- Resilient | `AddResilientElasticsearchServices` | Yes | Yes | -- | -- | Production services that need fault tolerance without observability overhead |
| 3 -- Monitored | `AddMonitoredResilientElasticsearchServices` | Yes | Yes | Yes | Yes | Production services requiring full observability and resilience |

## What This Sample Shows

1. **Transparent resilience** -- Repository CRUD operations work identically across all tiers. Retry and circuit breaker logic is applied automatically by the resilient client wrapper.
2. **Circuit breaker inspection** -- Resolve `IResilientElasticsearchClient` or `IElasticsearchCircuitBreaker` to check whether the circuit is open, the current failure rate, and consecutive failure count.
3. **Health checks** -- `AddElasticHealthCheck` registers a standard ASP.NET Core health check that reports cluster reachability.
4. **Monitoring** -- `AddElasticsearchMonitoring` registers metrics, tracing, request logging, and a background health monitor.

## Running

```bash
dotnet run --project samples/09-advanced/querying/ElasticSearch-Resilience
```
