// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Out-of-Band Projections Sample
// ============================================================================
// This sample demonstrates materialized views that update asynchronously
// via IMaterializedViewProcessor and MaterializedViewRefreshService, rather
// than inline during SaveAsync().
//
// Key concepts:
// - IMaterializedViewBuilder<T>    — Defines how events map to a view
// - IMaterializedViewProcessor     — Processes events through builders
// - MaterializedViewRefreshService — Background service for periodic catch-up
// - IMaterializedViewStore         — Persistence for views + position tracking
// - Manual ProcessEventAsync()     — Direct event processing without background service
// - CatchUpAsync() / RebuildAsync() — Catch-up and full rebuild scenarios
//
// When to use out-of-band projections (vs inline):
// - Cross-aggregate views (e.g., regional sales across all orders)
// - Expensive computations that should not block SaveAsync()
// - Views that tolerate eventual consistency (dashboards, reports)
// - CDC/change-data-capture scenarios with external data sources
// ============================================================================

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using System.Text.Json;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OutOfBandProjections.Domain;
using OutOfBandProjections.Infrastructure;
using OutOfBandProjections.Views;

Console.WriteLine("==========================================================");
Console.WriteLine("  Out-of-Band Projections Sample");
Console.WriteLine("  MaterializedViewProcessor + IMaterializedViewBuilder<T>");
Console.WriteLine("==========================================================");
Console.WriteLine();

// ============================================================================
// Step 1: Configure Services
// ============================================================================

var services = new ServiceCollection();

services.AddLogging(builder => builder
    .AddConsole()
    .SetMinimumLevel(LogLevel.Information));
services.AddMetrics();

// Register the in-memory materialized view store.
// In production, use SqlServerMaterializedViewStore, PostgresMaterializedViewStore, etc.
var viewStore = new InMemoryMaterializedViewStore();
services.AddSingleton<IMaterializedViewStore>(viewStore);

// Register materialized views with the builder pattern.
// AddMaterializedViews configures the IMaterializedViewProcessor and registers builders.
services.AddMaterializedViews(builder =>
{
    // Register the RegionalSalesViewBuilder.
    // The processor routes events to this builder based on HandledEventTypes.
    builder.AddBuilder<RegionalSalesSummary>(sp =>
        new RegionalSalesViewBuilder());

    // UseRefreshService enables the background MaterializedViewRefreshService.
    // It periodically calls CatchUpAsync for each registered view.
    builder.UseRefreshService(opts =>
    {
        opts.Enabled = true;
        opts.RefreshInterval = TimeSpan.FromSeconds(5);
        opts.CatchUpOnStartup = true;
        opts.MaxRetryCount = 3;
    });

    // Optional: batch size for catch-up/rebuild (events per batch).
    builder.WithBatchSize(100);
});

var provider = services.BuildServiceProvider();

var processor = provider.GetRequiredService<IMaterializedViewProcessor>();
var logger = provider.GetRequiredService<ILogger<Program>>();

// ============================================================================
// Demo 1: Manual Event Processing (Direct API)
// ============================================================================
Console.WriteLine("+---------------------------------------------------------+");
Console.WriteLine("|  Demo 1: Manual Event Processing (Direct API)           |");
Console.WriteLine("+---------------------------------------------------------+");
Console.WriteLine();
Console.WriteLine("Use IMaterializedViewProcessor.ProcessEventAsync() to push");
Console.WriteLine("events directly. This is useful for serverless scenarios,");
Console.WriteLine("manual triggers, or integration with external event sources.");
Console.WriteLine();

// Simulate a stream of order events
var events = new (Excalibur.Dispatch.Abstractions.IDomainEvent Event, long Position)[]
{
    (new OrderPlaced
    {
        EventId = Guid.NewGuid().ToString(),
        AggregateId = "order-001",
        Version = 1,
        OccurredAt = DateTimeOffset.UtcNow,
        EventType = nameof(OrderPlaced),
        CustomerId = "cust-100",
        CustomerName = "Alice",
        TotalAmount = 250.00m,
        Region = "US-West"
    }, 1),
    (new OrderPlaced
    {
        EventId = Guid.NewGuid().ToString(),
        AggregateId = "order-002",
        Version = 1,
        OccurredAt = DateTimeOffset.UtcNow,
        EventType = nameof(OrderPlaced),
        CustomerId = "cust-200",
        CustomerName = "Bob",
        TotalAmount = 175.50m,
        Region = "US-East"
    }, 2),
    (new OrderPlaced
    {
        EventId = Guid.NewGuid().ToString(),
        AggregateId = "order-003",
        Version = 1,
        OccurredAt = DateTimeOffset.UtcNow,
        EventType = nameof(OrderPlaced),
        CustomerId = "cust-300",
        CustomerName = "Charlie",
        TotalAmount = 420.00m,
        Region = "US-West"
    }, 3),
    (new OrderPlaced
    {
        EventId = Guid.NewGuid().ToString(),
        AggregateId = "order-004",
        Version = 1,
        OccurredAt = DateTimeOffset.UtcNow,
        EventType = nameof(OrderPlaced),
        CustomerId = "cust-400",
        CustomerName = "Diana",
        TotalAmount = 89.99m,
        Region = "EU-West"
    }, 4),
};

// Process events one at a time (single-event API)
Console.WriteLine("Processing 4 OrderPlaced events via ProcessEventAsync...");
foreach (var (evt, pos) in events)
{
    await processor.ProcessEventAsync(evt, pos, CancellationToken.None)
        .ConfigureAwait(false);
}

Console.WriteLine();
PrintViews(viewStore);

// ============================================================================
// Demo 2: Batch Event Processing
// ============================================================================
Console.WriteLine("+---------------------------------------------------------+");
Console.WriteLine("|  Demo 2: Batch Event Processing                         |");
Console.WriteLine("+---------------------------------------------------------+");
Console.WriteLine();
Console.WriteLine("ProcessEventsAsync() processes a batch of events in one call,");
Console.WriteLine("saving position checkpoints only after the entire batch.");
Console.WriteLine("This is more efficient for catch-up and replay scenarios.");
Console.WriteLine();

var batchEvents = new (Excalibur.Dispatch.Abstractions.IDomainEvent Event, long Position)[]
{
    (new OrderPlaced
    {
        EventId = Guid.NewGuid().ToString(),
        AggregateId = "order-005",
        Version = 1,
        OccurredAt = DateTimeOffset.UtcNow,
        EventType = nameof(OrderPlaced),
        CustomerId = "cust-500",
        CustomerName = "Eve",
        TotalAmount = 330.00m,
        Region = "US-East"
    }, 5),
    (new OrderPlaced
    {
        EventId = Guid.NewGuid().ToString(),
        AggregateId = "order-006",
        Version = 1,
        OccurredAt = DateTimeOffset.UtcNow,
        EventType = nameof(OrderPlaced),
        CustomerId = "cust-600",
        CustomerName = "Frank",
        TotalAmount = 1200.00m,
        Region = "US-West"
    }, 6),
};

Console.WriteLine("Processing batch of 2 events via ProcessEventsAsync...");
await processor.ProcessEventsAsync(batchEvents, CancellationToken.None)
    .ConfigureAwait(false);

Console.WriteLine();
PrintViews(viewStore);

// ============================================================================
// Demo 3: Position Tracking
// ============================================================================
Console.WriteLine("+---------------------------------------------------------+");
Console.WriteLine("|  Demo 3: Position Tracking                              |");
Console.WriteLine("+---------------------------------------------------------+");
Console.WriteLine();
Console.WriteLine("The processor automatically tracks the last processed position");
Console.WriteLine("per view via IMaterializedViewStore. This enables resuming");
Console.WriteLine("from where you left off after a restart (catch-up).");
Console.WriteLine();

var lastPosition = await viewStore.GetPositionAsync("RegionalSalesSummary", CancellationToken.None)
    .ConfigureAwait(false);
Console.WriteLine($"  Last processed position for 'RegionalSalesSummary': {lastPosition}");
Console.WriteLine($"  (After restart, CatchUpAsync reads from position {lastPosition + 1} onward)");
Console.WriteLine();

// ============================================================================
// Summary
// ============================================================================
Console.WriteLine("==========================================================");
Console.WriteLine("  Sample Complete!");
Console.WriteLine("==========================================================");
Console.WriteLine();
Console.WriteLine("Key takeaways:");
Console.WriteLine("  - IMaterializedViewBuilder<T> defines event-to-view mapping");
Console.WriteLine("  - GetViewId() determines the view key (e.g., region, tenant)");
Console.WriteLine("  - ProcessEventAsync() for single events (serverless, manual)");
Console.WriteLine("  - ProcessEventsAsync() for batches (efficient catch-up)");
Console.WriteLine("  - MaterializedViewRefreshService for periodic background catch-up");
Console.WriteLine("  - Position tracking enables resume-after-restart");
Console.WriteLine("  - CatchUpAsync() replays from last position for a single view");
Console.WriteLine("  - RebuildAsync() replays all events for all views from scratch");
Console.WriteLine();
Console.WriteLine("Use out-of-band projections when:");
Console.WriteLine("  - Views aggregate across multiple aggregates");
Console.WriteLine("  - Eventual consistency is acceptable");
Console.WriteLine("  - Projections are expensive (external lookups, ML scoring)");
Console.WriteLine("  - Serverless functions trigger view updates on demand");
Console.WriteLine();

#pragma warning restore CA1506
#pragma warning restore CA1303

// ============================================================================
// Helper: Print current view state
// ============================================================================

static void PrintViews(InMemoryMaterializedViewStore store)
{
    Console.WriteLine("Current RegionalSalesSummary views:");
    var allViews = store.GetAllViews();
    var regionViews = allViews
        .Where(kvp => kvp.Key.StartsWith("RegionalSalesSummary:", StringComparison.Ordinal))
        .OrderBy(kvp => kvp.Key, StringComparer.Ordinal);

    foreach (var (key, data) in regionViews)
    {
        var view = JsonSerializer.Deserialize<RegionalSalesSummary>(data);
        if (view is not null)
        {
            Console.WriteLine($"  {view.Region}:");
            Console.WriteLine($"    Orders: {view.TotalOrders}  Revenue: {view.TotalRevenue:C}");
            Console.WriteLine($"    Shipped: {view.ShippedOrders}  Cancelled: {view.CancelledOrders}");
        }
    }

    Console.WriteLine();
}
