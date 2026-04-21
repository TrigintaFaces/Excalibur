// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using ElasticSearch_Projections.ReadModels;
using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ============================================================================
// ElasticSearch Projections Sample
// ============================================================================
//
// Demonstrates:
//   1. Using ElasticSearch as a projection store for CQRS read models
//   2. Registering multiple projection types with named options
//   3. Full CRUD via IProjectionStore<T>: Upsert, GetById, Query, Count, Delete
//   4. Dictionary-based filtering and QueryOptions for pagination
//
// Prerequisites:
//   - Elasticsearch running on http://localhost:9200
//   - docker run -d --name es -p 9200:9200 -e "discovery.type=single-node" \
//       -e "xpack.security.enabled=false" elasticsearch:8.15.0
//
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

// Step 1: Register ElasticSearch base services from configuration
builder.Services.AddElasticsearchServices(builder.Configuration, registry: null);

// Step 2: Register ElasticSearch projection infrastructure (error handler, rebuild manager, etc.)
builder.Services.AddElasticsearchProjections(builder.Configuration);

// Step 3: Register projection stores with named options per projection type.
// Each projection type gets its own ElasticSearchProjectionStoreOptions keyed by typeof(T).Name.
// The IndexName option overrides the default index naming convention.
builder.Services.AddElasticSearchProjectionStore<OrderSummary>(options =>
{
    options.IndexName = "order-summaries";
    options.CreateIndexOnInitialize = true;
});

builder.Services.AddElasticSearchProjectionStore<CustomerDashboard>(options =>
{
    options.IndexName = "customer-dashboards";
    options.CreateIndexOnInitialize = true;
});

var app = builder.Build();

Console.WriteLine("ElasticSearch Projections Sample");
Console.WriteLine("================================");
Console.WriteLine();

using var scope = app.Services.CreateScope();
var orderStore = scope.ServiceProvider.GetRequiredService<IProjectionStore<OrderSummary>>();
var customerStore = scope.ServiceProvider.GetRequiredService<IProjectionStore<CustomerDashboard>>();
var ct = CancellationToken.None;

// --- 1. UPSERT: Create order summary projections ---
Console.WriteLine("1. Upserting order summary projections...");

await orderStore.UpsertAsync("order-001", new OrderSummary
{
    OrderId = "order-001",
    CustomerName = "Alice Johnson",
    TotalAmount = 149.99m,
    Status = "Shipped",
    ItemCount = 3,
    LastUpdated = DateTimeOffset.UtcNow
}, ct).ConfigureAwait(false);

await orderStore.UpsertAsync("order-002", new OrderSummary
{
    OrderId = "order-002",
    CustomerName = "Bob Smith",
    TotalAmount = 89.50m,
    Status = "Pending",
    ItemCount = 1,
    LastUpdated = DateTimeOffset.UtcNow
}, ct).ConfigureAwait(false);

await orderStore.UpsertAsync("order-003", new OrderSummary
{
    OrderId = "order-003",
    CustomerName = "Alice Johnson",
    TotalAmount = 299.00m,
    Status = "Shipped",
    ItemCount = 5,
    LastUpdated = DateTimeOffset.UtcNow
}, ct).ConfigureAwait(false);

Console.WriteLine("   Upserted 3 order summaries.");
Console.WriteLine();

// --- 2. UPSERT: Create customer dashboard projections ---
Console.WriteLine("2. Upserting customer dashboard projections...");

await customerStore.UpsertAsync("cust-001", new CustomerDashboard
{
    CustomerId = "cust-001",
    Name = "Alice Johnson",
    TotalOrders = 2,
    LifetimeSpend = 448.99m,
    LastOrderDate = DateTimeOffset.UtcNow
}, ct).ConfigureAwait(false);

await customerStore.UpsertAsync("cust-002", new CustomerDashboard
{
    CustomerId = "cust-002",
    Name = "Bob Smith",
    TotalOrders = 1,
    LifetimeSpend = 89.50m,
    LastOrderDate = DateTimeOffset.UtcNow
}, ct).ConfigureAwait(false);

Console.WriteLine("   Upserted 2 customer dashboards.");
Console.WriteLine();

// Allow ElasticSearch to refresh indices before querying
await Task.Delay(1500, ct).ConfigureAwait(false);

// --- 3. GET BY ID: Retrieve a specific projection ---
Console.WriteLine("3. Getting order-001 by ID...");

var order = await orderStore.GetByIdAsync("order-001", ct).ConfigureAwait(false);
if (order is not null)
{
    Console.WriteLine($"   Order: {order.OrderId}, Customer: {order.CustomerName}, " +
                      $"Amount: {order.TotalAmount:C}, Status: {order.Status}");
}

Console.WriteLine();

// --- 4. QUERY WITH FILTERS: Find shipped orders ---
Console.WriteLine("4. Querying orders with Status = 'Shipped'...");

var shippedOrders = await orderStore.QueryAsync(
    filters: new Dictionary<string, object> { ["Status"] = "Shipped" },
    options: null,
    ct).ConfigureAwait(false);

Console.WriteLine($"   Found {shippedOrders.Count} shipped order(s):");
foreach (var shipped in shippedOrders)
{
    Console.WriteLine($"   - {shipped.OrderId}: {shipped.CustomerName}, {shipped.TotalAmount:C}");
}

Console.WriteLine();

// --- 5. QUERY WITH OPTIONS: Paginated results ---
Console.WriteLine("5. Querying orders with pagination (Take=2, OrderBy=TotalAmount descending)...");

var pagedOrders = await orderStore.QueryAsync(
    filters: null,
    options: new QueryOptions(Skip: 0, Take: 2, OrderBy: "TotalAmount", Descending: true),
    ct).ConfigureAwait(false);

Console.WriteLine($"   Got {pagedOrders.Count} order(s) (top 2 by amount):");
foreach (var paged in pagedOrders)
{
    Console.WriteLine($"   - {paged.OrderId}: {paged.TotalAmount:C}");
}

Console.WriteLine();

// --- 6. COUNT: Count projections matching a filter ---
Console.WriteLine("6. Counting shipped orders...");

var shippedCount = await orderStore.CountAsync(
    filters: new Dictionary<string, object> { ["Status"] = "Shipped" },
    ct).ConfigureAwait(false);

Console.WriteLine($"   Shipped orders: {shippedCount}");

var totalCount = await orderStore.CountAsync(filters: null, ct).ConfigureAwait(false);
Console.WriteLine($"   Total orders: {totalCount}");
Console.WriteLine();

// --- 7. UPDATE: Upsert with changed data (simulating event replay) ---
Console.WriteLine("7. Updating order-002 status to 'Shipped' (simulating OrderShipped event)...");

await orderStore.UpsertAsync("order-002", new OrderSummary
{
    OrderId = "order-002",
    CustomerName = "Bob Smith",
    TotalAmount = 89.50m,
    Status = "Shipped",
    ItemCount = 1,
    LastUpdated = DateTimeOffset.UtcNow
}, ct).ConfigureAwait(false);

// Allow refresh
await Task.Delay(1500, ct).ConfigureAwait(false);

var updatedOrder = await orderStore.GetByIdAsync("order-002", ct).ConfigureAwait(false);
Console.WriteLine($"   Updated order-002 status: {updatedOrder?.Status}");
Console.WriteLine();

// --- 8. DELETE: Remove a projection ---
Console.WriteLine("8. Deleting order-003...");

await orderStore.DeleteAsync("order-003", ct).ConfigureAwait(false);

// Allow refresh
await Task.Delay(1500, ct).ConfigureAwait(false);

var deleted = await orderStore.GetByIdAsync("order-003", ct).ConfigureAwait(false);
Console.WriteLine($"   order-003 after delete: {(deleted is null ? "not found (deleted)" : "still exists")}");
Console.WriteLine();

// --- 9. Verify customer dashboards ---
Console.WriteLine("9. Verifying customer dashboards...");

var alice = await customerStore.GetByIdAsync("cust-001", ct).ConfigureAwait(false);
if (alice is not null)
{
    Console.WriteLine($"   Customer: {alice.Name}, Orders: {alice.TotalOrders}, " +
                      $"Lifetime Spend: {alice.LifetimeSpend:C}");
}

var customerCount = await customerStore.CountAsync(filters: null, ct).ConfigureAwait(false);
Console.WriteLine($"   Total customers: {customerCount}");
Console.WriteLine();

Console.WriteLine("Done! ElasticSearch projection store operations completed successfully.");
