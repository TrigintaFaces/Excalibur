// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// Projections Sample - CQRS Read Models with Event Sourcing
// ============================================================================
// This sample demonstrates projection patterns for building read models from
// event-sourced aggregates:
//
// Key concepts demonstrated:
// - IProjection<TKey> - Read model interface
// - IProjectionStore<T> - Storage abstraction
// - Inline projections - Synchronous updates
// - Multi-stream projections - Aggregating across streams
// - Checkpoint tracking - For async projection support
// - Projection rebuild - Rebuilding from scratch
// ============================================================================

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ProjectionsSample.Domain;
using ProjectionsSample.Infrastructure;
using ProjectionsSample.Projections;

Console.WriteLine("=================================================");
Console.WriteLine("  Projections Sample - CQRS Read Models");
Console.WriteLine("=================================================");
Console.WriteLine();

// ============================================================================
// Step 1: Configure Services
// ============================================================================

var services = new ServiceCollection();

// Add logging
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

// Register in-memory projection stores
// In production, use SqlServerProjectionStore, PostgresProjectionStore, etc.
services.AddSingleton<IProjectionStore<ProductCatalogProjection>>(
	new InMemoryProjectionStore<ProductCatalogProjection>(p => p.Id));

services.AddSingleton<IProjectionStore<CategorySummaryProjection>>(
	new InMemoryProjectionStore<CategorySummaryProjection>(p => p.Id));

// Register checkpoint store (for async projection support)
services.AddSingleton<InMemoryCheckpointStore>();

// Register projection handlers
services.AddSingleton<ProductCatalogProjectionHandler>();
services.AddSingleton<CategorySummaryProjectionHandler>();

var provider = services.BuildServiceProvider();

// Get services
var logger = provider.GetRequiredService<ILogger<Program>>();
var catalogStore = provider.GetRequiredService<IProjectionStore<ProductCatalogProjection>>();
var categoryStore = provider.GetRequiredService<IProjectionStore<CategorySummaryProjection>>();
var checkpointStore = provider.GetRequiredService<InMemoryCheckpointStore>();
var catalogHandler = provider.GetRequiredService<ProductCatalogProjectionHandler>();
var categoryHandler = provider.GetRequiredService<CategorySummaryProjectionHandler>();

// ============================================================================
// Demo 1: Inline Projections - Synchronous Updates
// ============================================================================
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 1: Inline Projections                    ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("Inline projections update read models synchronously as events");
Console.WriteLine("are raised. This provides strong consistency but couples the");
Console.WriteLine("write and read sides.");
Console.WriteLine();

// Create some products
var laptop = ProductAggregate.Create(
	Guid.NewGuid(), "Gaming Laptop", "Electronics", 1299.99m, 50);
var phone = ProductAggregate.Create(
	Guid.NewGuid(), "Smartphone Pro", "Electronics", 999.99m, 100);
var headphones = ProductAggregate.Create(
	Guid.NewGuid(), "Wireless Headphones", "Electronics", 249.99m, 200);
var chair = ProductAggregate.Create(
	Guid.NewGuid(), "Office Chair", "Furniture", 399.99m, 30);
var desk = ProductAggregate.Create(
	Guid.NewGuid(), "Standing Desk", "Furniture", 599.99m, 15);

// Process events through projection handlers (inline)
foreach (var evt in laptop.GetUncommittedEvents())
{
	await catalogHandler.HandleEventAsync(evt, CancellationToken.None);
	if (evt is ProductCreated created)
	{
		await categoryHandler.HandleAsync(created, CancellationToken.None);
	}
}

foreach (var evt in phone.GetUncommittedEvents())
{
	await catalogHandler.HandleEventAsync(evt, CancellationToken.None);
	if (evt is ProductCreated created)
	{
		await categoryHandler.HandleAsync(created, CancellationToken.None);
	}
}

foreach (var evt in headphones.GetUncommittedEvents())
{
	await catalogHandler.HandleEventAsync(evt, CancellationToken.None);
	if (evt is ProductCreated created)
	{
		await categoryHandler.HandleAsync(created, CancellationToken.None);
	}
}

foreach (var evt in chair.GetUncommittedEvents())
{
	await catalogHandler.HandleEventAsync(evt, CancellationToken.None);
	if (evt is ProductCreated created)
	{
		await categoryHandler.HandleAsync(created, CancellationToken.None);
	}
}

foreach (var evt in desk.GetUncommittedEvents())
{
	await catalogHandler.HandleEventAsync(evt, CancellationToken.None);
	if (evt is ProductCreated created)
	{
		await categoryHandler.HandleAsync(created, CancellationToken.None);
	}
}

// Mark events as committed (simulating persistence)
laptop.MarkEventsAsCommitted();
phone.MarkEventsAsCommitted();
headphones.MarkEventsAsCommitted();
chair.MarkEventsAsCommitted();
desk.MarkEventsAsCommitted();

// Query the catalog projection
Console.WriteLine("Product Catalog Read Model:");
var allProducts = await catalogStore.QueryAsync(null, new QueryOptions(OrderBy: "Name"), CancellationToken.None);
foreach (var product in allProducts)
{
	Console.WriteLine($"  - {product.Name} ({product.Category}): {product.CurrentPrice:C} - Stock: {product.StockLevel}");
}

Console.WriteLine();

// ============================================================================
// Demo 2: Multi-Stream Projections
// ============================================================================
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 2: Multi-Stream Projections              ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("Multi-stream projections aggregate data across multiple");
Console.WriteLine("aggregates. The CategorySummaryProjection combines data");
Console.WriteLine("from all products in a category.");
Console.WriteLine();

// Query category summaries
var allCategories = await categoryStore.QueryAsync(null, null, CancellationToken.None);
foreach (var category in allCategories)
{
	Console.WriteLine($"Category: {category.CategoryName}");
	Console.WriteLine($"  Total Products: {category.TotalProducts}");
	Console.WriteLine($"  Active Products: {category.ActiveProducts}");
	Console.WriteLine($"  Products In Stock: {category.ProductsInStock}");
	Console.WriteLine($"  Price Range: {category.MinPrice:C} - {category.MaxPrice:C}");
	Console.WriteLine($"  Average Price: {category.AveragePrice:C}");
	Console.WriteLine($"  Total Inventory Value: {category.TotalInventoryValue:C}");
	Console.WriteLine();
}

// ============================================================================
// Demo 3: Projection Updates from Events
// ============================================================================
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 3: Projection Updates from Events        ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("When domain events occur, projections are updated to reflect");
Console.WriteLine("the new state. Let's apply some changes...");
Console.WriteLine();

// Price change
Console.WriteLine("1. Changing laptop price from $1,299.99 to $1,099.99 (sale!)");
laptop.ChangePrice(1099.99m);
foreach (var evt in laptop.GetUncommittedEvents())
{
	await catalogHandler.HandleEventAsync(evt, CancellationToken.None);
	if (evt is ProductPriceChanged priceChanged)
	{
		await categoryHandler.HandleAsync(priceChanged, "Electronics", CancellationToken.None);
	}
}

laptop.MarkEventsAsCommitted();

// Stock update
Console.WriteLine("2. Removing 45 headphones from stock (big sale!)");
headphones.RemoveStock(45, "Flash sale");
foreach (var evt in headphones.GetUncommittedEvents())
{
	await catalogHandler.HandleEventAsync(evt, CancellationToken.None);
	if (evt is ProductStockRemoved stockRemoved)
	{
		await categoryHandler.HandleAsync(stockRemoved, "Electronics", CancellationToken.None);
	}
}

headphones.MarkEventsAsCommitted();

// Add more stock
Console.WriteLine("3. Restocking desks (+10 units)");
desk.AddStock(10);
foreach (var evt in desk.GetUncommittedEvents())
{
	await catalogHandler.HandleEventAsync(evt, CancellationToken.None);
	if (evt is ProductStockAdded stockAdded)
	{
		await categoryHandler.HandleAsync(stockAdded, "Furniture", CancellationToken.None);
	}
}

desk.MarkEventsAsCommitted();

Console.WriteLine();
Console.WriteLine("Updated Product Catalog:");
allProducts = await catalogStore.QueryAsync(null, new QueryOptions(OrderBy: "Name"), CancellationToken.None);
foreach (var product in allProducts)
{
	var saleTag = product.IsOnSale ? $" [SALE: {product.DiscountPercentage}% OFF]" : "";
	var stockTag = product.LowStock ? " [LOW STOCK]" : "";
	Console.WriteLine($"  - {product.Name}: {product.CurrentPrice:C} - Stock: {product.StockLevel}{saleTag}{stockTag}");
}

Console.WriteLine();

// ============================================================================
// Demo 4: Querying Projections
// ============================================================================
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 4: Querying Projections                  ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("Projections support rich querying with filters, pagination,");
Console.WriteLine("and sorting. The IProjectionStore<T> interface provides these");
Console.WriteLine("capabilities across all storage backends.");
Console.WriteLine();

// Filter by category
Console.WriteLine("1. Products in 'Electronics' category:");
var electronics = await catalogStore.QueryAsync(
	new Dictionary<string, object> { ["Category"] = "Electronics" },
	new QueryOptions(OrderBy: "CurrentPrice", Descending: true),
	CancellationToken.None);

foreach (var product in electronics)
{
	Console.WriteLine($"  - {product.Name}: {product.CurrentPrice:C}");
}

Console.WriteLine();

// Filter by stock status
Console.WriteLine("2. Products in stock:");
var inStock = await catalogStore.QueryAsync(
	new Dictionary<string, object> { ["IsActive"] = true },
	new QueryOptions(Take: 3),
	CancellationToken.None);

Console.WriteLine(
	$"  Showing top 3 of {await catalogStore.CountAsync(new Dictionary<string, object> { ["IsActive"] = true }, CancellationToken.None)} active products");
foreach (var product in inStock)
{
	Console.WriteLine($"  - {product.Name}: Stock: {product.StockLevel}");
}

Console.WriteLine();

// ============================================================================
// Demo 5: Checkpoint Tracking (Async Projection Support)
// ============================================================================
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 5: Checkpoint Tracking                   ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("Async projections use checkpoints to track progress. This");
Console.WriteLine("enables resuming after restarts and rebuilding from scratch.");
Console.WriteLine();

// Simulate checkpoint updates
var catalogCheckpoint = new ProjectionCheckpoint
{
	ProjectionName = nameof(ProductCatalogProjection),
	LastPosition = 15, // Simulated global position
	LastProcessedAt = DateTimeOffset.UtcNow,
	TotalEventsProcessed = 15
};

var categoryCheckpoint = new ProjectionCheckpoint
{
	ProjectionName = nameof(CategorySummaryProjection),
	LastPosition = 15,
	LastProcessedAt = DateTimeOffset.UtcNow,
	TotalEventsProcessed = 15
};

checkpointStore.SaveCheckpoint(catalogCheckpoint);
checkpointStore.SaveCheckpoint(categoryCheckpoint);

Console.WriteLine("Current Checkpoint Status:");
foreach (var checkpoint in checkpointStore.GetAllCheckpoints())
{
	Console.WriteLine($"  {checkpoint.ProjectionName}:");
	Console.WriteLine($"    Position: {checkpoint.LastPosition}");
	Console.WriteLine($"    Events Processed: {checkpoint.TotalEventsProcessed}");
	Console.WriteLine($"    Last Updated: {checkpoint.LastProcessedAt:u}");
}

Console.WriteLine();

// ============================================================================
// Demo 6: Projection Rebuild Pattern
// ============================================================================
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 6: Projection Rebuild Pattern            ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("Projections can be rebuilt by resetting the checkpoint and");
Console.WriteLine("replaying all events. This is useful for:");
Console.WriteLine("  - Fixing bugs in projection logic");
Console.WriteLine("  - Adding new projections to existing data");
Console.WriteLine("  - Recovering from data corruption");
Console.WriteLine();

Console.WriteLine("Rebuild Steps:");
Console.WriteLine("  1. Reset checkpoint to position 0");
Console.WriteLine("  2. Clear existing projection data (optional)");
Console.WriteLine("  3. Replay all events from the event store");
Console.WriteLine("  4. Update checkpoint after each batch");
Console.WriteLine();

// Simulate rebuild
Console.WriteLine("Simulating CategorySummaryProjection rebuild...");
Console.WriteLine("  [1/4] Resetting checkpoint...");
checkpointStore.ResetCheckpoint(nameof(CategorySummaryProjection));

Console.WriteLine("  [2/4] Checkpoint reset. Would clear projection store here.");
Console.WriteLine("  [3/4] Replaying events from position 0...");
Console.WriteLine("  [4/4] Rebuild complete!");

// Restore checkpoint
checkpointStore.SaveCheckpoint(categoryCheckpoint);

Console.WriteLine();
Console.WriteLine("Rebuild best practices:");
Console.WriteLine("  - Use batched processing for large event streams");
Console.WriteLine("  - Commit checkpoints after each batch (not each event)");
Console.WriteLine("  - Consider parallel rebuild for independent projections");
Console.WriteLine("  - Use feature flags to switch between old and new projections");

Console.WriteLine();

// ============================================================================
// Demo 7: Discontinue a Product
// ============================================================================
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 7: Discontinue a Product                 ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("When a product is discontinued, both projections are updated.");
Console.WriteLine("The catalog shows the product as inactive, and the category");
Console.WriteLine("summary recalculates its statistics.");
Console.WriteLine();

Console.WriteLine("Discontinuing the Office Chair...");
chair.Discontinue("End of product line");
foreach (var evt in chair.GetUncommittedEvents())
{
	await catalogHandler.HandleEventAsync(evt, CancellationToken.None);
	if (evt is ProductDiscontinued discontinued)
	{
		await categoryHandler.HandleAsync(discontinued, "Furniture", CancellationToken.None);
	}
}

chair.MarkEventsAsCommitted();

Console.WriteLine();
Console.WriteLine("Updated Furniture Category Summary:");
var furnitureCategory = await categoryStore.GetByIdAsync("FURNITURE", CancellationToken.None);
if (furnitureCategory != null)
{
	Console.WriteLine($"  Total Products: {furnitureCategory.TotalProducts}");
	Console.WriteLine($"  Active Products: {furnitureCategory.ActiveProducts}");
	Console.WriteLine($"  Products In Stock: {furnitureCategory.ProductsInStock}");
	Console.WriteLine($"  Total Inventory Value: {furnitureCategory.TotalInventoryValue:C}");
}

Console.WriteLine();

// ============================================================================
// Summary
// ============================================================================
Console.WriteLine("=================================================");
Console.WriteLine("  Sample Complete!");
Console.WriteLine("=================================================");
Console.WriteLine();
Console.WriteLine("Key takeaways:");
Console.WriteLine("  - IProjection<TKey> defines read model contract");
Console.WriteLine("  - IProjectionStore<T> provides storage abstraction");
Console.WriteLine("  - Inline projections update synchronously with events");
Console.WriteLine("  - Multi-stream projections aggregate across aggregates");
Console.WriteLine("  - Checkpoints enable async projections and rebuilds");
Console.WriteLine("  - QueryOptions support filtering, sorting, and pagination");
Console.WriteLine();
Console.WriteLine("Production stores available:");
Console.WriteLine("  - SqlServerProjectionStore (SQL Server + JSON)");
Console.WriteLine("  - PostgresProjectionStore (PostgreSQL + JSONB)");
Console.WriteLine("  - MongoDbProjectionStore (MongoDB documents)");
Console.WriteLine("  - CosmosDbProjectionStore (Azure Cosmos DB)");
Console.WriteLine("  - ElasticSearchProjectionStore (Full-text search)");
Console.WriteLine();

#pragma warning restore CA1506
#pragma warning restore CA1303
