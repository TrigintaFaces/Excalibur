// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Snapshot Strategies Sample
// ===========================
// This sample demonstrates various snapshot strategies:
// - Interval-based (every N events)
// - Time-based (every N minutes)
// - Size-based (based on aggregate size)
// - Composite (combining multiple strategies)
// - On-demand (manual trigger)
//
// Run: dotnet run

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Snapshots;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SnapshotStrategies.Aggregates;

// Build configuration
var builder = new HostApplicationBuilder(args);

// Configure logging
builder.Services.AddLogging(logging =>
{
	_ = logging.AddConsole();
	_ = logging.SetMinimumLevel(LogLevel.Information);
});

// Configure Dispatch messaging
builder.Services.AddDispatch(typeof(Program).Assembly);

// Add event serializer
builder.Services.AddSingleton<IEventSerializer, JsonEventSerializer>();

// Add in-memory event store for demo
builder.Services.AddInMemoryEventStore();

// ============================================================
// Configure Event Sourcing with Snapshot Strategy
// ============================================================

// Demo: Interval-based snapshot strategy (every 5 events)
builder.Services.AddExcaliburEventSourcing(es =>
{
	// Register repository
	_ = es.AddRepository<ShoppingCartAggregate, Guid>(id => new ShoppingCartAggregate(id));

	// Configure interval-based snapshots (every 5 events for demo visibility)
	_ = es.UseIntervalSnapshots(5);
});

// Create strategy instances for demo purposes (to check ShouldCreateSnapshot)
var intervalStrategy = new IntervalSnapshotStrategy(interval: 5);

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var repository = host.Services.GetRequiredService<IEventSourcedRepository<ShoppingCartAggregate, Guid>>();
var snapshotStore = host.Services.GetRequiredService<ISnapshotStore>();

logger.LogInformation("=================================================");
logger.LogInformation("  Snapshot Strategies Sample");
logger.LogInformation("=================================================");
logger.LogInformation("");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Demo 1: Interval-Based Snapshotting
// ============================================================
logger.LogInformation("=== Demo 1: Interval-Based Snapshotting ===");
logger.LogInformation("Strategy: Create snapshot every 5 events");
logger.LogInformation("");

var cartId = Guid.NewGuid();
var cart = ShoppingCartAggregate.Create(cartId);
logger.LogInformation("Created cart: {CartId}", cartId);

// Add items - this generates events
var products = new[]
{
	("PROD-001", "Laptop", 999.99m), ("PROD-002", "Mouse", 29.99m), ("PROD-003", "Keyboard", 79.99m), ("PROD-004", "Monitor", 349.99m),
	("PROD-005", "Headphones", 149.99m), ("PROD-006", "USB Hub", 24.99m), ("PROD-007", "Webcam", 89.99m),
	("PROD-008", "Desk Lamp", 39.99m),
};

foreach (var (id, name, price) in products)
{
	cart.AddItem(id, name, price);
	logger.LogInformation("Added item: {Name} - Version now: {Version}", name, cart.Version);

	// Save after each operation
	await repository.SaveAsync(cart, CancellationToken.None).ConfigureAwait(false);

	// Check if snapshot was created (every 5 events)
	if (intervalStrategy.ShouldCreateSnapshot(cart))
	{
		logger.LogInformation("  >>> Snapshot would be created at version {Version}", cart.Version);
	}
}

logger.LogInformation("");
logger.LogInformation("Final cart state:");
logger.LogInformation("  Items: {Count}", cart.Items.Count);
logger.LogInformation("  Total Price: {Price:C}", cart.TotalPrice);
logger.LogInformation("  Version: {Version}", cart.Version);

// ============================================================
// Demo 2: Time-Based Snapshotting
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 2: Time-Based Snapshotting ===");
logger.LogInformation("Strategy: Create snapshot every 30 seconds");
logger.LogInformation("");

// Create a time-based strategy (30 seconds for demo)
var timeStrategy = new TimeBasedSnapshotStrategy(TimeSpan.FromSeconds(30));

var cart2 = ShoppingCartAggregate.Create(Guid.NewGuid());
logger.LogInformation("Created cart: {CartId}", cart2.Id);

// First check - should create snapshot (first time)
var shouldSnapshot1 = timeStrategy.ShouldCreateSnapshot(cart2);
logger.LogInformation("Initial check: ShouldCreateSnapshot = {Result}", shouldSnapshot1);

// Immediate second check - should NOT create (not enough time passed)
cart2.AddItem("PROD-001", "Item 1", 10.00m);
var shouldSnapshot2 = timeStrategy.ShouldCreateSnapshot(cart2);
logger.LogInformation("After adding item: ShouldCreateSnapshot = {Result}", shouldSnapshot2);

logger.LogInformation("");
logger.LogInformation("Time-based strategy tracks last snapshot time per aggregate.");
logger.LogInformation("In production, snapshots would be created after 30 seconds of activity.");

// ============================================================
// Demo 3: Composite Snapshotting
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 3: Composite Snapshotting ===");
logger.LogInformation("Strategy: Snapshot when EITHER interval OR time condition is met (Any mode)");
logger.LogInformation("");

// Composite: snapshot every 10 events OR every 1 hour
var compositeStrategy = new CompositeSnapshotStrategy(
	CompositeSnapshotStrategy.CompositeMode.Any,
	new IntervalSnapshotStrategy(10),
	new TimeBasedSnapshotStrategy(TimeSpan.FromHours(1)));

logger.LogInformation("CompositeStrategy configured:");
logger.LogInformation("  - IntervalSnapshotStrategy(10): Every 10 events");
logger.LogInformation("  - TimeBasedSnapshotStrategy(1h): Every hour");
logger.LogInformation("  - Mode: Any (OR logic)");
logger.LogInformation("");

var cart3 = ShoppingCartAggregate.Create(Guid.NewGuid());

// Check at different versions
for (var i = 0; i < 12; i++)
{
	cart3.AddItem($"PROD-{i:D3}", $"Product {i}", 10.00m * i);
	var shouldCreate = compositeStrategy.ShouldCreateSnapshot(cart3);
	if (shouldCreate || i == 0 || i == 9 || i == 10)
	{
		logger.LogInformation("Version {Version}: ShouldCreateSnapshot = {Result}",
			cart3.Version, shouldCreate);
	}
}

// ============================================================
// Demo 4: All Mode Composite
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 4: All Mode Composite ===");
logger.LogInformation("Strategy: Snapshot only when BOTH conditions are met (All mode)");
logger.LogInformation("");

// All mode: BOTH interval AND size must be satisfied
// This is more restrictive - rarely used but available
var allModeStrategy = new CompositeSnapshotStrategy(
	CompositeSnapshotStrategy.CompositeMode.All,
	new IntervalSnapshotStrategy(5),
	new IntervalSnapshotStrategy(10));

logger.LogInformation("CompositeStrategy (All mode):");
logger.LogInformation("  - IntervalSnapshotStrategy(5): Every 5 events");
logger.LogInformation("  - IntervalSnapshotStrategy(10): Every 10 events");
logger.LogInformation("  - Mode: All (AND logic)");
logger.LogInformation("");
logger.LogInformation("Result: Only creates snapshot at versions divisible by BOTH 5 AND 10");
logger.LogInformation("        (i.e., versions 10, 20, 30, etc.)");

var cart4 = ShoppingCartAggregate.Create(Guid.NewGuid());
for (var i = 0; i < 22; i++)
{
	cart4.AddItem($"PROD-{i:D3}", $"Product {i}", 5.00m);
	var shouldCreate = allModeStrategy.ShouldCreateSnapshot(cart4);
	if (shouldCreate || cart4.Version % 5 == 0)
	{
		logger.LogInformation("Version {Version}: ShouldCreateSnapshot = {Result}",
			cart4.Version, shouldCreate);
	}
}

// ============================================================
// Demo 5: No Snapshot Strategy
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 5: No Snapshot Strategy ===");
logger.LogInformation("Strategy: Never create snapshots (for testing or special cases)");
logger.LogInformation("");

var noSnapshotStrategy = new NoSnapshotStrategy();
var cart5 = ShoppingCartAggregate.Create(Guid.NewGuid());

for (var i = 0; i < 100; i++)
{
	cart5.AddItem($"PROD-{i:D3}", $"Product {i}", 1.00m);
}

var shouldSnapshotNever = noSnapshotStrategy.ShouldCreateSnapshot(cart5);
logger.LogInformation("After 100 events: ShouldCreateSnapshot = {Result}", shouldSnapshotNever);
logger.LogInformation("NoSnapshotStrategy always returns false.");

// ============================================================
// Strategy Comparison Table
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Strategy Comparison ===");
logger.LogInformation("");
logger.LogInformation("| Strategy                | Best For                              | Configuration     |");
logger.LogInformation("|-------------------------|---------------------------------------|-------------------|");
logger.LogInformation("| IntervalSnapshotStrategy | High-velocity aggregates             | Every N events    |");
logger.LogInformation("| TimeBasedSnapshotStrategy| Long-running, frequently accessed    | Every N minutes   |");
logger.LogInformation("| SizeBasedSnapshotStrategy| Large aggregates with many properties| Above N KB        |");
logger.LogInformation("| CompositeSnapshotStrategy| Complex multi-condition rules        | Any/All mode      |");
logger.LogInformation("| NoSnapshotStrategy       | Testing, small aggregates            | Never snapshot    |");
logger.LogInformation("");

logger.LogInformation("=== Recommendations ===");
logger.LogInformation("");
logger.LogInformation("1. Start with IntervalSnapshotStrategy(100) for most aggregates");
logger.LogInformation("2. Use TimeBasedSnapshotStrategy for infrequently modified but read-heavy aggregates");
logger.LogInformation("3. Use CompositeSnapshotStrategy(Any) for belt-and-suspenders approach");
logger.LogInformation("4. Monitor event counts per aggregate to tune interval values");
logger.LogInformation("5. Consider aggregate load patterns when choosing strategy");
logger.LogInformation("");

logger.LogInformation("Sample completed. Press Ctrl+C to exit...");

await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
