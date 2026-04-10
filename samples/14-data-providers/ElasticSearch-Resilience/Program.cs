// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.Resilience;
using ElasticSearch_Resilience.Domain;
using ElasticSearch_Resilience.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

// ============================================================================
// ElasticSearch Resilience Sample
// ============================================================================
//
// Demonstrates three tiers of Elasticsearch registration:
//
//   Tier 1: Basic       -- AddElasticsearchServices (no resilience)
//   Tier 2: Resilient   -- AddResilientElasticsearchServices (retry + circuit breaker)
//   Tier 3: Monitored   -- AddMonitoredResilientElasticsearchServices (metrics + tracing + resilience)
//
// This sample uses Tier 3 to show the full feature set. See the README
// for guidance on which tier to choose.
//
// Prerequisites:
//   - Elasticsearch running on http://localhost:9200
//   - docker run -d --name es -p 9200:9200 -e "discovery.type=single-node" \
//       -e "xpack.security.enabled=false" elasticsearch:8.15.0
//
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

// ---------------------------------------------------------------------------
// Tier 1: Basic (no resilience)
// Uncomment to use the simplest registration -- no retry, no circuit breaker.
// ---------------------------------------------------------------------------
// builder.Services.AddElasticsearchServices(builder.Configuration, registry: null);

// ---------------------------------------------------------------------------
// Tier 2: Resilient (retry + circuit breaker)
// Uncomment for automatic retries and circuit breaker protection without
// metrics or tracing.
// ---------------------------------------------------------------------------
// builder.Services.AddResilientElasticsearchServices(builder.Configuration);

// ---------------------------------------------------------------------------
// Tier 3: Monitored + Resilient (metrics + tracing + retry + circuit breaker)
// The full package -- resilience plus observability.
// ---------------------------------------------------------------------------
builder.Services.AddMonitoredResilientElasticsearchServices(
    builder.Configuration.GetSection("ElasticSearch"));
builder.Services.AddElasticsearchMonitoring(
    builder.Configuration.GetSection("ElasticSearch"));

// Register the Order repository (scoped lifetime + singleton index initializer)
builder.Services.AddRepository<IOrderRepository, OrderRepository>();

// Register health checks
builder.Services.AddHealthChecks()
    .AddElasticHealthCheck("elasticsearch", TimeSpan.FromSeconds(5));

var app = builder.Build();

// Initialize indexes at startup
await app.InitializeElasticsearchIndexesAsync().ConfigureAwait(false);

Console.WriteLine("ElasticSearch Resilience Sample");
Console.WriteLine("===============================");
Console.WriteLine();

// -------------------------------------------------------------------------
// 1. Normal CRUD through the repository
//    Resilience (retry + circuit breaker) is applied transparently.
// -------------------------------------------------------------------------
Console.WriteLine("1. CRUD operations (resilience applied transparently)");
Console.WriteLine("-----------------------------------------------------");

using var scope = app.Services.CreateScope();
var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
var ct = CancellationToken.None;

var order = new Order
{
    Id = "order-001",
    CustomerId = "cust-42",
    Total = 299.99m,
    Status = "Pending",
};

var added = await repo.AddOrUpdateAsync(order.Id, order, ct).ConfigureAwait(false);
Console.WriteLine($"   Added order: {added} (Id: {order.Id})");

var retrieved = await repo.GetByIdAsync("order-001", ct).ConfigureAwait(false);
Console.WriteLine($"   Retrieved: {retrieved?.Id} -- ${retrieved?.Total} ({retrieved?.Status})");

order.Status = "Confirmed";
await repo.AddOrUpdateAsync(order.Id, order, ct).ConfigureAwait(false);
var updated = await repo.GetByIdAsync("order-001", ct).ConfigureAwait(false);
Console.WriteLine($"   Updated status: {updated?.Status}");
Console.WriteLine();

// -------------------------------------------------------------------------
// 2. Inspect the resilient client directly
// -------------------------------------------------------------------------
Console.WriteLine("2. IResilientElasticsearchClient -- circuit breaker status");
Console.WriteLine("----------------------------------------------------------");

var resilientClient = app.Services.GetRequiredService<IResilientElasticsearchClient>();
Console.WriteLine($"   IsCircuitBreakerOpen: {resilientClient.IsCircuitBreakerOpen}");
Console.WriteLine();

// -------------------------------------------------------------------------
// 3. Inspect the circuit breaker component
// -------------------------------------------------------------------------
Console.WriteLine("3. IElasticsearchCircuitBreaker -- detailed state");
Console.WriteLine("-------------------------------------------------");

var circuitBreaker = app.Services.GetRequiredService<IElasticsearchCircuitBreaker>();
Console.WriteLine($"   State:               {circuitBreaker.State}");
Console.WriteLine($"   FailureRate:          {circuitBreaker.FailureRate:P1}");
Console.WriteLine($"   ConsecutiveFailures:  {circuitBreaker.ConsecutiveFailures}");
Console.WriteLine();

// -------------------------------------------------------------------------
// 4. Run the health check
// -------------------------------------------------------------------------
Console.WriteLine("4. Health check");
Console.WriteLine("---------------");

var healthService = app.Services.GetRequiredService<HealthCheckService>();
var report = await healthService.CheckHealthAsync(ct).ConfigureAwait(false);

Console.WriteLine($"   Overall status: {report.Status}");
foreach (var entry in report.Entries)
{
    Console.WriteLine($"   [{entry.Key}] {entry.Value.Status} -- {entry.Value.Description ?? "OK"}");
}
Console.WriteLine();

// -------------------------------------------------------------------------
// Cleanup
// -------------------------------------------------------------------------
var deleted = await repo.RemoveAsync("order-001", ct).ConfigureAwait(false);
Console.WriteLine($"Cleanup: deleted order-001 = {deleted}");
Console.WriteLine();
Console.WriteLine("Done!");
