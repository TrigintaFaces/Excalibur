# Event Upcasting Sample

This sample demonstrates **event schema evolution (upcasting)** for handling breaking changes in event-sourced systems.

## What This Sample Shows

1. **Event Version Transformations** - Upgrade V1 -> V2 -> V3
2. **Direct Upgrade Paths** - Skip intermediate versions (V1 -> V3)
3. **BFS-Based Path Finding** - Automatic optimal path selection
4. **Auto-Upcasting on Replay** - Transparent aggregate hydration
5. **Address Parsing** - Complex data transformation example

## Why Event Upcasting Matters

Event sourcing stores events forever. When your event schema changes, you can't modify historical events:

```
Day 1: UserCreated { Name, Email }           <- V1 events in store
Day 30: UserCreated { Name, Email, Address } <- V2 events
Day 90: UserCreated { Name, Email, Street, City, PostalCode, Country } <- V3 events

Problem: How do you replay V1 events through an aggregate expecting V3?
Solution: Upcasting transforms old events to new versions during replay.
```

## Running the Sample

```bash
cd samples/09-advanced/EventUpcasting
dotnet run
```

## Event Schema Evolution

### Version 1 (Original)

```csharp
public record UserCreatedV1(string Name, string Email);
```

### Version 2 (Added Address)

```csharp
public record UserCreatedV2(string Name, string Email, string? Address);
```

### Version 3 (Structured Address)

```csharp
public record UserCreatedV3(
    string Name,
    string Email,
    string? Street,
    string? City,
    string? PostalCode,
    string? Country);
```

## Implementing Upcasters

### Using IMessageUpcaster (Dispatch Layer)

```csharp
public class UserCreatedV1ToV2 : IMessageUpcaster<UserCreatedV1, UserCreatedV2>
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public UserCreatedV2 Upcast(UserCreatedV1 old) =>
        new(old.Name, old.Email, Address: null);
}
```

### Using EventUpgrader (Event Sourcing Layer)

```csharp
public class UserCreatedV1ToV2Upgrader : EventUpgrader<UserCreatedV1, UserCreatedV2>
{
    public override string EventType => "UserCreated";
    public override int FromVersion => 1;
    public override int ToVersion => 2;

    protected override UserCreatedV2 UpgradeEvent(UserCreatedV1 old) =>
        new(old.Name, old.Email, Address: null);
}
```

## Registering Upcasters

### With Event Sourcing Builder

```csharp
services.AddExcaliburEventSourcing(es =>
{
    es.AddRepository<UserAggregate, string>(id => new UserAggregate(id));

    es.AddUpcastingPipeline(upcasting =>
    {
        // Register individual upcasters
        upcasting.RegisterUpcaster<UserCreatedV1, UserCreatedV2>(new UserCreatedV1ToV2());
        upcasting.RegisterUpcaster<UserCreatedV2, UserCreatedV3>(new UserCreatedV2ToV3());

        // Or scan assembly
        upcasting.ScanAssembly(typeof(Program).Assembly);

        // Enable auto-upcasting during replay
        upcasting.EnableAutoUpcastOnReplay(true);
    });
});
```

### With EventVersionManager

```csharp
var manager = new EventVersionManager(logger);

// Register upgraders
manager.RegisterUpgrader(new UserCreatedV1ToV2Upgrader());
manager.RegisterUpgrader(new UserCreatedV2ToV3Upgrader());
manager.RegisterUpgrader(new UserCreatedV1ToV3Upgrader()); // Direct path

// Upgrade event (BFS finds shortest path)
var upgraded = manager.UpgradeEvent("UserCreated", oldEvent, fromVersion: 1, toVersion: 3);
```

## Upgrade Path Selection

The `EventVersionManager` uses **Breadth-First Search** to find the shortest upgrade path:

```
Available paths from V1 to V3:
├── V1 -> V2 -> V3  (2 transformations)
└── V1 -> V3        (1 transformation) ✓ Selected

The direct V1 -> V3 path is chosen because it's shorter.
```

### Why Provide Direct Upgrades?

| Path Type | Transformations | Performance | Use Case |
|-----------|-----------------|-------------|----------|
| Chain (V1->V2->V3) | 2 | Slower | When logic changes at each version |
| Direct (V1->V3) | 1 | Faster | When you can compute V3 directly |

## Expected Output

```
=================================================
  Event Upcasting Sample
=================================================

=== Demo 1: Manual Event Transformation ===
Demonstrating direct event upgrades (V1 -> V2 -> V3)

V1 Event: UserCreatedV1
  Name: John Doe
  Email: john@example.com
  (No address field in V1)

V2 Event (after upgrade from V1): UserCreatedV2
  Name: John Doe
  Email: john@example.com
  Address: (null)

V3 Event (after upgrade from V2): UserCreatedV3
  Name: John Doe
  Email: john@example.com
  Street: (null)
  City: (null)
  PostalCode: (null)
  Country: (null)

=== Demo 2: Direct Upgrade Path (V1 -> V3) ===
...

=== Demo 3: Address Parsing (V2 -> V3) ===
V2 Event with address string:
  Address: "123 Main Street, Springfield, IL 62701, USA"

V3 Event with parsed address:
  Street: 123 Main Street
  City: Springfield
  PostalCode: 62701
  Country: USA
...
```

## Best Practices

| Practice | Why |
|----------|-----|
| Version events explicitly | Enables automatic path finding |
| Keep upgraders stateless | Ensures deterministic replay |
| Provide direct upgrades | Reduces transformation overhead |
| Test upgrade paths | Catch data loss before production |
| Document schema changes | Future developers need context |
| Never delete old event types | They exist in the event store |

## Common Patterns

### Adding a New Field

```csharp
// V1: No address
// V2: Added address

public UserCreatedV2 Upcast(UserCreatedV1 old) =>
    new(old.Name, old.Email, Address: null); // Default to null
```

### Splitting a Field

```csharp
// V2: Single address string
// V3: Structured address

public UserCreatedV3 Upcast(UserCreatedV2 old) =>
    new(old.Name, old.Email,
        Street: ParseStreet(old.Address),
        City: ParseCity(old.Address),
        ...);
```

### Merging Fields

```csharp
// V1: FirstName, LastName
// V2: FullName

public UserCreatedV2 Upcast(UserCreatedV1 old) =>
    new($"{old.FirstName} {old.LastName}", old.Email);
```

### Renaming a Field

```csharp
// V1: Email
// V2: EmailAddress

public UserCreatedV2 Upcast(UserCreatedV1 old) =>
    new(old.Name, EmailAddress: old.Email);
```

## Project Structure

```
EventUpcasting/
├── EventUpcasting.csproj       # Project file
├── Program.cs                  # Main sample with demos
├── Domain/
│   └── UserProfileAggregate.cs # Aggregate handling V3 events
├── Events/
│   └── UserProfileEvents.cs    # V1, V2, V3 event definitions
├── Upgraders/
│   └── UserEventUpgraders.cs   # Upgrade transformations
└── README.md                   # This file
```

## Related Samples

- [SnapshotStrategies](../SnapshotStrategies/) - Snapshot optimization
- [SqlServerEventStore](../SqlServerEventStore/) - SQL Server persistence
- [ExcaliburCqrs](../../01-getting-started/ExcaliburCqrs/) - Basic event sourcing
