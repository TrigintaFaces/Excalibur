// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;
using ElasticSearch_GettingStarted.Domain;
using ElasticSearch_GettingStarted.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ============================================================================
// ElasticSearch Getting Started
// ============================================================================
//
// Demonstrates:
//   1. Registering ElasticSearch services via DI
//   2. Creating a repository with index initialization
//   3. Basic CRUD: Add, Get, Update, Partial Update, Bulk Upsert, Delete
//
// Prerequisites:
//   - Elasticsearch running on http://localhost:9200
//   - docker run -d --name es -p 9200:9200 -e "discovery.type=single-node" \
//       -e "xpack.security.enabled=false" elasticsearch:8.15.0
//
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

// Step 1: Register ElasticSearch services from configuration
// appsettings.json contains: { "ElasticSearch": { "Url": "http://localhost:9200" } }
builder.Services.AddElasticsearchServices(builder.Configuration, registry: null);

// Step 2: Register our Product repository (scoped lifetime + singleton index initializer)
builder.Services.AddRepository<IProductRepository, ProductRepository>();

var app = builder.Build();

// Step 3: Initialize indexes at startup (creates the "products" index if it doesn't exist)
await app.InitializeElasticsearchIndexesAsync().ConfigureAwait(false);

Console.WriteLine("ElasticSearch Getting Started");
Console.WriteLine("=============================");
Console.WriteLine();

using var scope = app.Services.CreateScope();
var repo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
var ct = CancellationToken.None;

// --- ADD ---
Console.WriteLine("1. Adding a product...");
var product = new Product
{
    Id = "prod-001",
    Name = "Mechanical Keyboard",
    Category = "Electronics",
    Price = 149.99m,
    StockQuantity = 50,
};
var added = await repo.AddOrUpdateAsync(product.Id, product, ct).ConfigureAwait(false);
Console.WriteLine($"   Added: {added} (Id: {product.Id})");

// --- GET BY ID ---
Console.WriteLine();
Console.WriteLine("2. Retrieving by ID...");
var retrieved = await repo.GetByIdAsync("prod-001", ct).ConfigureAwait(false);
Console.WriteLine($"   Found: {retrieved?.Name} - ${retrieved?.Price}");

// --- UPDATE (full replace) ---
Console.WriteLine();
Console.WriteLine("3. Updating product (full replace)...");
product.Price = 129.99m;
product.StockQuantity = 45;
await repo.AddOrUpdateAsync(product.Id, product, ct).ConfigureAwait(false);
var updated = await repo.GetByIdAsync("prod-001", ct).ConfigureAwait(false);
Console.WriteLine($"   Updated price: ${updated?.Price}, stock: {updated?.StockQuantity}");

// --- PARTIAL UPDATE ---
Console.WriteLine();
Console.WriteLine("4. Partial update (only stockQuantity)...");
await repo.UpdateAsync("prod-001", new Dictionary<string, object> { ["stockQuantity"] = 30 }, ct).ConfigureAwait(false);
var partial = await repo.GetByIdAsync("prod-001", ct).ConfigureAwait(false);
Console.WriteLine($"   Stock after partial update: {partial?.StockQuantity} (price unchanged: ${partial?.Price})");

// --- BULK UPSERT ---
Console.WriteLine();
Console.WriteLine("5. Bulk upserting 3 products...");
var products = new[]
{
    new Product { Id = "prod-002", Name = "USB-C Hub", Category = "Electronics", Price = 49.99m, StockQuantity = 200 },
    new Product { Id = "prod-003", Name = "Standing Desk", Category = "Furniture", Price = 599.00m, StockQuantity = 15 },
    new Product { Id = "prod-004", Name = "Monitor Light Bar", Category = "Electronics", Price = 89.99m, StockQuantity = 75 },
};
var bulkResult = await repo.BulkAddOrUpdateAsync(products, p => p.Id, ct).ConfigureAwait(false);
Console.WriteLine($"   Bulk upsert success: {bulkResult}");

// --- DELETE ---
Console.WriteLine();
Console.WriteLine("6. Deleting a product...");
var deleted = await repo.RemoveAsync("prod-004", ct).ConfigureAwait(false);
Console.WriteLine($"   Deleted prod-004: {deleted}");
var gone = await repo.GetByIdAsync("prod-004", ct).ConfigureAwait(false);
Console.WriteLine($"   Verify deleted (should be null): {gone is null}");

Console.WriteLine();
Console.WriteLine("Done! All CRUD operations demonstrated.");
