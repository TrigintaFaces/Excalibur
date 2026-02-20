// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Event Upcasting Sample
// =======================
// This sample demonstrates event schema evolution (upcasting):
// - Event version transformations (V1 -> V2 -> V3)
// - Direct upgrade paths (V1 -> V3 skipping V2)
// - BFS-based optimal path finding
// - Auto-upcasting during aggregate replay
// - Manual upcasting pipeline usage
//
// Run: dotnet run

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using EventUpcasting.Domain;
using EventUpcasting.Events;
using EventUpcasting.Upgraders;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Upcasting;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
// Configure Event Sourcing with Upcasting
// ============================================================
builder.Services.AddExcaliburEventSourcing(es =>
{
	// Register repository
	_ = es.AddRepository<UserProfileAggregate, string>(id => new UserProfileAggregate(id));

	// Configure upcasting pipeline
	_ = es.AddUpcastingPipeline(upcasting =>
	{
		// Register individual upcasters
		_ = upcasting.RegisterUpcaster(new UserCreatedV1ToV2Upgrader());
		_ = upcasting.RegisterUpcaster(new UserCreatedV2ToV3Upgrader());
		_ = upcasting.RegisterUpcaster(new UserAddressChangedV2ToV3Upgrader());

		// Direct V1->V3 upgrader (provides shorter path than V1->V2->V3)
		_ = upcasting.RegisterUpcaster(new UserCreatedV1ToV3DirectUpgrader());

		// Enable auto-upcasting during event replay
		_ = upcasting.EnableAutoUpcastOnReplay(true);
	});
});

// Register EventVersionManager for direct demos
builder.Services.AddSingleton<EventVersionManager>();

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var repository = host.Services.GetRequiredService<IEventSourcedRepository<UserProfileAggregate, string>>();
var upcastingPipeline = host.Services.GetService<IUpcastingPipeline>();
var eventVersionManager = host.Services.GetRequiredService<EventVersionManager>();

logger.LogInformation("=================================================");
logger.LogInformation("  Event Upcasting Sample");
logger.LogInformation("=================================================");
logger.LogInformation("");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Demo 1: Manual Event Transformation
// ============================================================
logger.LogInformation("=== Demo 1: Manual Event Transformation ===");
logger.LogInformation("Demonstrating direct event upgrades (V1 -> V2 -> V3)");
logger.LogInformation("");

// Create a V1 event (simulating legacy data)
var v1Event = new UserCreatedV1(
	AggregateId: "user-001",
	AggregateVersion: 1,
	Name: "John Doe",
	Email: "john@example.com");

logger.LogInformation("V1 Event: {EventType}", v1Event.GetType().Name);
logger.LogInformation("  Name: {Name}", v1Event.Name);
logger.LogInformation("  Email: {Email}", v1Event.Email);
logger.LogInformation("  (No address field in V1)");
logger.LogInformation("");

// Manual upgrade V1 -> V2
var v1ToV2Upgrader = new UserCreatedV1ToV2Upgrader();
var v2Event = v1ToV2Upgrader.Upcast(v1Event);

logger.LogInformation("V2 Event (after upgrade from V1): {EventType}", v2Event.GetType().Name);
logger.LogInformation("  Name: {Name}", v2Event.Name);
logger.LogInformation("  Email: {Email}", v2Event.Email);
logger.LogInformation("  Address: {Address}", v2Event.Address ?? "(null)");
logger.LogInformation("");

// Manual upgrade V2 -> V3
var v2ToV3Upgrader = new UserCreatedV2ToV3Upgrader();
var v3Event = v2ToV3Upgrader.Upcast(v2Event);

logger.LogInformation("V3 Event (after upgrade from V2): {EventType}", v3Event.GetType().Name);
logger.LogInformation("  Name: {Name}", v3Event.Name);
logger.LogInformation("  Email: {Email}", v3Event.Email);
logger.LogInformation("  Street: {Street}", v3Event.Street ?? "(null)");
logger.LogInformation("  City: {City}", v3Event.City ?? "(null)");
logger.LogInformation("  PostalCode: {PostalCode}", v3Event.PostalCode ?? "(null)");
logger.LogInformation("  Country: {Country}", v3Event.Country ?? "(null)");

// ============================================================
// Demo 2: Direct Upgrade Path (V1 -> V3)
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 2: Direct Upgrade Path (V1 -> V3) ===");
logger.LogInformation("Demonstrating direct upgrade skipping V2");
logger.LogInformation("");

var directUpgrader = new UserCreatedV1ToV3DirectUpgrader();
var v3FromV1Direct = directUpgrader.Upcast(v1Event);

logger.LogInformation("V3 Event (direct from V1): {EventType}", v3FromV1Direct.GetType().Name);
logger.LogInformation("  Name: {Name}", v3FromV1Direct.Name);
logger.LogInformation("  Email: {Email}", v3FromV1Direct.Email);
logger.LogInformation("  (All address fields null - V1 had no address)");
logger.LogInformation("");
logger.LogInformation("Direct upgraders are more efficient when available.");
logger.LogInformation("BFS algorithm finds shortest path automatically.");

// ============================================================
// Demo 3: Address Parsing (V2 -> V3)
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 3: Address Parsing (V2 -> V3) ===");
logger.LogInformation("Demonstrating address string to structured conversion");
logger.LogInformation("");

var v2WithAddress = new UserCreatedV2(
	AggregateId: "user-002",
	AggregateVersion: 1,
	Name: "Jane Smith",
	Email: "jane@example.com",
	Address: "123 Main Street, Springfield, IL 62701, USA");

logger.LogInformation("V2 Event with address string:");
logger.LogInformation("  Address: \"{Address}\"", v2WithAddress.Address);
logger.LogInformation("");

var v3WithParsedAddress = v2ToV3Upgrader.Upcast(v2WithAddress);

logger.LogInformation("V3 Event with parsed address:");
logger.LogInformation("  Street: {Street}", v3WithParsedAddress.Street ?? "(null)");
logger.LogInformation("  City: {City}", v3WithParsedAddress.City ?? "(null)");
logger.LogInformation("  PostalCode: {PostalCode}", v3WithParsedAddress.PostalCode ?? "(null)");
logger.LogInformation("  Country: {Country}", v3WithParsedAddress.Country ?? "(null)");
logger.LogInformation("");
logger.LogInformation("Note: Address parsing is a simplified heuristic.");
logger.LogInformation("Real implementations might use geocoding services.");

// ============================================================
// Demo 4: Event Version Manager (BFS Path Finding)
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 4: Event Version Manager ===");
logger.LogInformation("Demonstrating BFS-based optimal path finding");
logger.LogInformation("");

// Register upgraders with the version manager
eventVersionManager.RegisterUpgrader(new UserCreatedEventUpgraderV1ToV2());
eventVersionManager.RegisterUpgrader(new UserCreatedEventUpgraderV2ToV3());
eventVersionManager.RegisterUpgrader(new UserCreatedEventUpgraderV1ToV3());

logger.LogInformation("Registered upgraders for 'UserCreated' event:");
foreach (var upgrader in eventVersionManager.GetUpgradersForEventType("UserCreated"))
{
	logger.LogInformation("  {EventType}: V{From} -> V{To}",
		upgrader.EventType, upgrader.FromVersion, upgrader.ToVersion);
}

logger.LogInformation("");

// Use version manager to upgrade
var originalV1 = new UserCreatedEventData("John", "john@example.com");

logger.LogInformation("Upgrading UserCreated from V1 to V3...");
logger.LogInformation("  Input (V1): Name={Name}, Email={Email}", originalV1.Name, originalV1.Email);

var upgradedV3 = eventVersionManager.UpgradeEvent("UserCreated", originalV1, fromVersion: 1, toVersion: 3);

if (upgradedV3 is UserCreatedEventDataV3 result)
{
	logger.LogInformation("  Output (V3): Name={Name}, Email={Email}, Street={Street}",
		result.Name, result.Email, result.Street ?? "(null)");
}

logger.LogInformation("");
logger.LogInformation("The version manager found the shortest path: V1 -> V3 (direct)");
logger.LogInformation("Alternative path V1 -> V2 -> V3 exists but is longer.");

// ============================================================
// Demo 5: Create New User with Current Schema
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 5: New Users with Current Schema ===");
logger.LogInformation("New users use V3 events directly");
logger.LogInformation("");

var newUser = UserProfileAggregate.Create(
	userId: "user-new-001",
	name: "Alice Johnson",
	email: "alice@example.com",
	street: "456 Oak Avenue",
	city: "Portland",
	postalCode: "97201",
	country: "USA");

await repository.SaveAsync(newUser, CancellationToken.None).ConfigureAwait(false);

logger.LogInformation("Created new user with V3 schema:");
logger.LogInformation("  ID: {Id}", newUser.Id);
logger.LogInformation("  Name: {Name}", newUser.Name);
logger.LogInformation("  Email: {Email}", newUser.Email);
logger.LogInformation("  Full Address: {Address}", newUser.FullAddress);
logger.LogInformation("  Version: {Version}", newUser.Version);

// ============================================================
// Key Takeaways
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Key Takeaways ===");
logger.LogInformation("");
logger.LogInformation("1. Event schemas EVOLVE - plan for versioning from the start");
logger.LogInformation("2. NEVER delete old event types - they exist in the event store");
logger.LogInformation("3. Upgraders transform old -> new during aggregate replay");
logger.LogInformation("4. Direct upgraders (V1 -> V3) are more efficient than chains");
logger.LogInformation("5. BFS finds optimal upgrade path automatically");
logger.LogInformation("6. New aggregates always use the latest event version");
logger.LogInformation("7. Keep upgrader logic PURE and DETERMINISTIC");
logger.LogInformation("");

logger.LogInformation("=== Best Practices ===");
logger.LogInformation("");
logger.LogInformation("| Practice                    | Why                                    |");
logger.LogInformation("|-----------------------------|----------------------------------------|");
logger.LogInformation("| Version events explicitly   | Enables automatic path finding         |");
logger.LogInformation("| Keep upgraders stateless    | Ensures deterministic replay           |");
logger.LogInformation("| Provide direct upgrades     | Reduces transformation overhead        |");
logger.LogInformation("| Test upgrade paths          | Catch data loss before production      |");
logger.LogInformation("| Document schema changes     | Future developers need context         |");
logger.LogInformation("");

logger.LogInformation("Sample completed. Press Ctrl+C to exit...");

await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303

// ============================================================
// Helper Types for EventVersionManager Demo
// ============================================================

// Simple data classes for EventVersionManager demo
file record UserCreatedEventData(string Name, string Email);

file record UserCreatedEventDataV2(string Name, string Email, string? Address);

file record UserCreatedEventDataV3(string Name, string Email, string? Street, string? City, string? PostalCode, string? Country);

// IEventUpgrader implementations for EventVersionManager
file class UserCreatedEventUpgraderV1ToV2 : EventUpgrader<UserCreatedEventData, UserCreatedEventDataV2>
{
	public override string EventType => "UserCreated";
	public override int FromVersion => 1;
	public override int ToVersion => 2;

	protected override UserCreatedEventDataV2 UpgradeEvent(UserCreatedEventData oldEvent) =>
		new(oldEvent.Name, oldEvent.Email, Address: null);
}

file class UserCreatedEventUpgraderV2ToV3 : EventUpgrader<UserCreatedEventDataV2, UserCreatedEventDataV3>
{
	public override string EventType => "UserCreated";
	public override int FromVersion => 2;
	public override int ToVersion => 3;

	protected override UserCreatedEventDataV3 UpgradeEvent(UserCreatedEventDataV2 oldEvent) =>
		new(oldEvent.Name, oldEvent.Email, Street: null, City: null, PostalCode: null, Country: null);
}

file class UserCreatedEventUpgraderV1ToV3 : EventUpgrader<UserCreatedEventData, UserCreatedEventDataV3>
{
	public override string EventType => "UserCreated";
	public override int FromVersion => 1;
	public override int ToVersion => 3;

	protected override UserCreatedEventDataV3 UpgradeEvent(UserCreatedEventData oldEvent) =>
		new(oldEvent.Name, oldEvent.Email, Street: null, City: null, PostalCode: null, Country: null);
}
