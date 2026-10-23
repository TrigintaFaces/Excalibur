// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// ============================================================================
// Projections Sample - CQRS Read Models with Event Sourcing
// ============================================================================
// This sample demonstrates projection patterns for building read models from
// event-sourced aggregates:
//
// Key concepts demonstrated:
// - IProjection<TKey> - Read model interface
// - IProjectionStore<T> - Storage abstraction
// - AddProjection<T>().Async() - the projection host owns the read model
// - EnableProjectionProcessing() - starts that host; without it nothing applies
// - IProjectionProcessor.ProcessPendingAsync() - driving the host by hand, which is
//   what a console app or a serverless trigger does instead of a BackgroundService
// - SaveResult.SessionToken + IProjectionSessionReader - reading your own write
// - KeyedBy<TEvent>() - Multi-stream projections keyed by custom data
// - Checkpoint tracking - how the host remembers where it got to
// - Projection rebuild - Rebuilding from scratch
// - IEventSourcedRepository - Aggregate persistence
// ============================================================================

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;

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

// Add logging and metrics (IMeterFactory required by projection observability)
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
services.AddMetrics();

// Register event types so the serializer resolves them without an assembly scan.
services.AddEventTypesFromAssembly(typeof(Program).Assembly);

// Add Excalibur event sourcing with an in-memory event store and async projections.
// AddProjection<T>() registers a projection; EnableProjectionProcessing() below starts the
// host that applies events to it. A save does NOT write read models -- it returns a session
// token, and a read that must observe that save presents the token.
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
{
	_ = builder.AddRepository<ProductAggregate, Guid>(id => new ProductAggregate(id));

	// -----------------------------------------------------------------------
	// Async projection: ProductCatalogProjection (single-stream)
	// -----------------------------------------------------------------------
	// This projection demonstrates all three handler tiers:
	//
	// Tier 1: When<TEvent> lambdas -- simple, synchronous, no DI
	// Tier 3: WhenHandledBy<TEvent, THandler> -- DI-resolved, async, logging
	//
	// The projection host loads the projection from IProjectionStore<T>, applies
	// each handler, then writes it back -- reading the global stream in order, as the
	// only writer. WhenHandledBy handlers are resolved from DI and registered automatically.
	builder.AddProjection<ProductCatalogProjection>(p => p
		.Async()
		// Tier 3: DI-resolved handler (has ILogger injection, see ProductCreatedHandler)
		.WhenHandledBy<ProductCreated, ProductCreatedHandler>()
		// Tier 1: Simple lambdas for straightforward property updates
		.When<ProductPriceChanged>((proj, e, ctx) =>
		{
			proj.CurrentPrice = e.NewPrice;
			proj.LastModified = DateTimeOffset.UtcNow;
			// The aggregate stream version travels in the persistence envelope, not the
			// event payload. Lambda handlers receive replay/position context only;
			// use the DI-resolved handler tier (ProjectionHandlerContext.CommittedVersion)
			// when a projection must record the committed stream version.
			proj.Version = ctx.GlobalPosition ?? proj.Version;
		})
		.When<ProductStockAdded>((proj, e, ctx) =>
		{
			proj.StockLevel = e.NewStockLevel;
			proj.LastModified = DateTimeOffset.UtcNow;
			proj.Version = ctx.GlobalPosition ?? proj.Version;
		})
		.When<ProductStockRemoved>((proj, e, ctx) =>
		{
			proj.StockLevel = e.NewStockLevel;
			proj.LastModified = DateTimeOffset.UtcNow;
			proj.Version = ctx.GlobalPosition ?? proj.Version;
		})
		// Tier 3: DI-resolved handler (logs discontinuation reason)
		.WhenHandledBy<ProductDiscontinued, ProductDiscontinuedHandler>());

	// -----------------------------------------------------------------------
	// Async projection: CategorySummaryProjection (multi-stream via KeyedBy)
	// -----------------------------------------------------------------------
	// This projection aggregates data across multiple product streams into
	// category-level summaries. KeyedBy<TEvent>() tells the framework to
	// derive the projection ID from the event's Category field instead of
	// the aggregate ID. This means all products in "Electronics" share one
	// CategorySummaryProjection instance, loaded and saved by the projection
	// host as it applies each event -- no manual handler calls needed.
	builder.AddProjection<CategorySummaryProjection>(p => p
		.Async()
		.KeyedBy<ProductCreated>(e => e.Category.ToUpperInvariant())
		.KeyedBy<ProductPriceChanged>(e => e.Category.ToUpperInvariant())
		.KeyedBy<ProductStockAdded>(e => e.Category.ToUpperInvariant())
		.KeyedBy<ProductStockRemoved>(e => e.Category.ToUpperInvariant())
		.KeyedBy<ProductDiscontinued>(e => e.Category.ToUpperInvariant())
		.When<ProductCreated>((proj, e) =>
		{
			var productId = e.ProductId.ToString();
			proj.CategoryName = e.Category;
			proj.ProductIds.Add(productId);
			proj.ProductPrices[productId] = e.Price;
			proj.ProductStocks[productId] = e.InitialStock;
			proj.TotalProducts++;
			proj.ActiveProducts++;
			if (e.InitialStock > 0)
			{
				proj.ProductsInStock++;
				if (e.InitialStock <= 10)
				{
					proj.ProductsLowStock++;
				}
			}

			RecalculatePriceStats(proj);
			RecalculateInventoryValue(proj);
			proj.LastModified = DateTimeOffset.UtcNow;
			proj.Version++;
		})
		.When<ProductPriceChanged>((proj, e) =>
		{
			proj.ProductPrices[e.ProductId.ToString()] = e.NewPrice;
			RecalculatePriceStats(proj);
			RecalculateInventoryValue(proj);
			proj.LastModified = DateTimeOffset.UtcNow;
			proj.Version++;
		})
		.When<ProductStockAdded>((proj, e) =>
		{
			var productId = e.ProductId.ToString();
			var oldStock = proj.ProductStocks.GetValueOrDefault(productId, 0);
			proj.ProductStocks[productId] = e.NewStockLevel;
			if (oldStock == 0 && e.NewStockLevel > 0)
			{
				proj.ProductsInStock++;
			}

			if (oldStock <= 10 && e.NewStockLevel > 10)
			{
				proj.ProductsLowStock--;
			}
			else if (oldStock > 10 && e.NewStockLevel <= 10)
			{
				proj.ProductsLowStock++;
			}

			RecalculateInventoryValue(proj);
			proj.LastModified = DateTimeOffset.UtcNow;
			proj.Version++;
		})
		.When<ProductStockRemoved>((proj, e) =>
		{
			var productId = e.ProductId.ToString();
			var oldStock = proj.ProductStocks.GetValueOrDefault(productId, 0);
			proj.ProductStocks[productId] = e.NewStockLevel;
			if (oldStock > 0 && e.NewStockLevel == 0)
			{
				proj.ProductsInStock--;
			}

			if (oldStock > 10 && e.NewStockLevel <= 10 && e.NewStockLevel > 0)
			{
				proj.ProductsLowStock++;
			}
			else if (oldStock <= 10 && e.NewStockLevel == 0)
			{
				proj.ProductsLowStock--;
			}

			RecalculateInventoryValue(proj);
			proj.LastModified = DateTimeOffset.UtcNow;
			proj.Version++;
		})
		.When<ProductDiscontinued>((proj, e) =>
		{
			var productId = e.ProductId.ToString();
			var stock = proj.ProductStocks.GetValueOrDefault(productId, 0);
			proj.ActiveProducts--;
			if (stock > 0)
			{
				proj.ProductsInStock--;
				if (stock <= 10)
				{
					proj.ProductsLowStock--;
				}
			}

			proj.ProductPrices.Remove(productId);
			proj.ProductStocks.Remove(productId);
			RecalculatePriceStats(proj);
			RecalculateInventoryValue(proj);
			proj.LastModified = DateTimeOffset.UtcNow;
			proj.Version++;
		}));
	// WITHOUT THIS LINE EVERY READ MODEL BELOW STAYS EMPTY. Registering a projection does not
	// start anything; this registers the host that applies committed events to it, in
	// global-stream order, as the only writer of those rows.
	_ = builder.EnableProjectionProcessing(options => options.SubscriptionScope = "projections-sample/memory-events/memory-views");
}));
services.AddInMemoryEventStore();

// Register in-memory projection stores.
// In production, use SqlServerProjectionStore, PostgresProjectionStore, etc.
// The projection host resolves IProjectionStore<T> from DI to persist state.
// The framework ships this store, so the sample uses it rather than carrying its own copy. It is
// keyed by the id the projection system passes on each write, so there is no key selector.
services.AddInMemoryProjectionStore<ProductCatalogProjection>();
services.AddInMemoryProjectionStore<CategorySummaryProjection>();

// Register checkpoint store (for async projection support)
services.AddSingleton<InMemoryCheckpointStore>();

var provider = services.BuildServiceProvider();

// Get services
var logger = provider.GetRequiredService<ILogger<Program>>();
var catalogStore = provider.GetRequiredService<IProjectionStore<ProductCatalogProjection>>();
var categoryStore = provider.GetRequiredService<IProjectionStore<CategorySummaryProjection>>();
var checkpointStore = provider.GetRequiredService<InMemoryCheckpointStore>();
var repository = provider.GetRequiredService<IEventSourcedRepository<ProductAggregate, Guid>>();

// The host, driven by hand. A long-running service lets the BackgroundService poll; a console app
// and a serverless trigger call the processor directly, which is the same code path.
var processor = provider.GetRequiredService<IProjectionProcessor>();
var sessionReader = provider.GetRequiredService<IProjectionSessionReader>();

// ============================================================================
// Demo 1: Stored Projections - Session-Consistent Reads
// ============================================================================
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 1: Stored Projections                    ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("A save commits events and returns a session token. The");
Console.WriteLine("projection host applies those events to the read models in");
Console.WriteLine("global-stream order -- as the only writer, so two concurrent");
Console.WriteLine("saves cannot overwrite each other's fold. A read that must");
Console.WriteLine("observe a particular save presents that save's token.");
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

// Save aggregates. Both projections are fed by the host afterwards:
// - ProductCatalogProjection: keyed by aggregate ID (single-stream, default)
// - CategorySummaryProjection: keyed by Category via KeyedBy<TEvent> (multi-stream)
var lastSave = default(SaveResult);
foreach (var aggregate in new[] { laptop, phone, headphones, chair, desk })
{
	lastSave = await repository.SaveAsync(aggregate, CancellationToken.None).ConfigureAwait(false);
}

// Drive the host once. In a web application a BackgroundService has already done this; here the
// process would exit before any background poll ran, so the sample drives it explicitly -- the
// same call a Function or a Lambda makes on its trigger.
var processed = await processor.ProcessPendingAsync(CancellationToken.None).ConfigureAwait(false);
Console.WriteLine(
	$"Projection host applied {processed.EventsApplied} event(s); outcome {processed.Outcome}.");

// Read the last product back THROUGH ITS TOKEN. Freshness is the part worth noticing: Fresh means
// the projection had observed this very save before the read returned, so this is not a hopeful
// query -- it is a read that can tell you whether it saw your write.
var confirmed = await sessionReader
	.ReadAsync<ProductCatalogProjection>(desk.Id.ToString(), lastSave, CancellationToken.None)
	.ConfigureAwait(false);
Console.WriteLine(
	$"Read of the last save: Freshness={confirmed.Freshness}, found={confirmed.Projection is not null}.");
Console.WriteLine();

// Query the catalog projection -- populated by the host above
Console.WriteLine("Product Catalog Read Model:");
var allProducts = await catalogStore.QueryAsync(null, new QueryOptions(OrderBy: "Name"), CancellationToken.None);
foreach (var product in allProducts)
{
	Console.WriteLine($"  - {product.Name} ({product.Category}): {product.CurrentPrice:C} - Stock: {product.StockLevel}");
}

Console.WriteLine();

// ============================================================================
// Demo 2: Multi-Stream Projections (KeyedBy)
// ============================================================================
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 2: Multi-Stream Projections (KeyedBy)    ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("Multi-stream projections aggregate data across multiple");
Console.WriteLine("aggregates. The CategorySummaryProjection combines data");
Console.WriteLine("from all products in a category. KeyedBy<TEvent>() tells");
Console.WriteLine("the framework to derive the projection ID from the event's");
Console.WriteLine("Category field, so all products in the same category share");
Console.WriteLine("one projection instance, updated by the host as events arrive.");
Console.WriteLine();

// Query category summaries -- populated by the host from the KeyedBy projection
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
Console.WriteLine("When domain events are committed, the host applies them to both");
Console.WriteLine("the single-stream and the multi-stream projection. KeyedBy ensures");
Console.WriteLine("the correct CategorySummaryProjection instance is loaded and saved");
Console.WriteLine("without manual handler calls.");
Console.WriteLine();

// Reload aggregates from repository for mutations (correct load-mutate-save pattern)
laptop = await repository.GetByIdAsync(laptop.Id, CancellationToken.None).ConfigureAwait(false)
	?? throw new InvalidOperationException("Laptop not found");

// Price change
Console.WriteLine("1. Changing laptop price from $1,299.99 to $1,099.99 (sale!)");
laptop.ChangePrice(1099.99m);

await repository.SaveAsync(laptop, CancellationToken.None).ConfigureAwait(false);

// Reload headphones for mutation
headphones = await repository.GetByIdAsync(headphones.Id, CancellationToken.None).ConfigureAwait(false)
	?? throw new InvalidOperationException("Headphones not found");

// Stock update
Console.WriteLine("2. Removing 45 headphones from stock (big sale!)");
headphones.RemoveStock(45, "Flash sale");

await repository.SaveAsync(headphones, CancellationToken.None).ConfigureAwait(false);

// Reload desk for mutation
desk = await repository.GetByIdAsync(desk.Id, CancellationToken.None).ConfigureAwait(false)
	?? throw new InvalidOperationException("Desk not found");

// Add more stock
Console.WriteLine("3. Restocking desks (+10 units)");
desk.AddStock(10);

await repository.SaveAsync(desk, CancellationToken.None).ConfigureAwait(false);

// Drive the host again. The three saves above are durable; the read models are not updated until
// something applies them, and in this console app that something is this line.
processed = await processor.ProcessPendingAsync(CancellationToken.None).ConfigureAwait(false);
Console.WriteLine(
	$"Projection host applied {processed.EventsApplied} event(s); outcome {processed.Outcome}.");

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
Console.WriteLine("When a product is discontinued, the host applies the event");
Console.WriteLine("to both projections. The catalog projection marks the product");
Console.WriteLine("inactive; the category summary recalculates statistics -- all");
Console.WriteLine("driven by KeyedBy and When<T> handlers.");
Console.WriteLine();

// Reload chair for mutation
chair = await repository.GetByIdAsync(chair.Id, CancellationToken.None).ConfigureAwait(false)
	?? throw new InvalidOperationException("Chair not found");

Console.WriteLine("Discontinuing the Office Chair...");
chair.Discontinue("End of product line");

await repository.SaveAsync(chair, CancellationToken.None).ConfigureAwait(false);

processed = await processor.ProcessPendingAsync(CancellationToken.None).ConfigureAwait(false);
Console.WriteLine(
	$"Projection host applied {processed.EventsApplied} event(s); outcome {processed.Outcome}.");

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
Console.WriteLine("  - AddProjection<T>().Async() for single-stream projections");
Console.WriteLine("  - KeyedBy<TEvent>() for multi-stream projections (custom key)");
Console.WriteLine("  - The projection host applies both projection types, not SaveAsync()");
Console.WriteLine("  - A read that must observe its own write carries the save's token");
Console.WriteLine("  - No manual GetUncommittedEvents() iteration needed");
Console.WriteLine("  - IProjectionStore<T> provides storage abstraction for both");
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

// ============================================================================
// Helper methods for CategorySummaryProjection recalculations
// ============================================================================

static void RecalculatePriceStats(CategorySummaryProjection proj)
{
	if (proj.ProductPrices.Count == 0)
	{
		proj.AveragePrice = 0;
		proj.MinPrice = 0;
		proj.MaxPrice = 0;
		return;
	}

	var prices = proj.ProductPrices.Values.ToList();
	proj.AveragePrice = Math.Round(prices.Average(), 2);
	proj.MinPrice = prices.Min();
	proj.MaxPrice = prices.Max();
}

static void RecalculateInventoryValue(CategorySummaryProjection proj)
{
	proj.TotalInventoryValue = proj.ProductStocks
		.Sum(ps => ps.Value * proj.ProductPrices.GetValueOrDefault(ps.Key, 0));
}
