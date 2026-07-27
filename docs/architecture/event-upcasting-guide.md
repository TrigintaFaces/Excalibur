# Event Upcasting and Migration Guide

## Upcasting vs Migration

| Concept | Purpose | When |
|---------|---------|------|
| **Upcasting** | Transform event schema at read time | Schema evolved but data stays in place |
| **Migration** | Transform event data in storage | Major structural changes requiring rewrite |

## Upcasting (Preferred)

Events are upcasted during replay, not in storage:
```csharp
services.AddMessageUpcasting(builder => builder
    .RegisterUpcaster<OrderCreatedV1, OrderCreatedV2>(new OrderCreatedV1ToV2())
    .EnableAutoUpcastOnReplay());
```

- **BFS shortest-path**: SnapshotVersionManager finds the shortest upgrade path between versions
- **Multi-hop**: V1 -> V2 -> V3 happens transparently during replay
- **No data modification**: Original events are preserved

## Snapshot Upgrading

Snapshots use a BFS version chain via `SnapshotVersionManager` (Sprint 557):
- Register version upgraders
- On load, if snapshot version < current, upgrade automatically
- Controlled by `EventSourcedRepositoryOptions.EnableAutoSnapshotUpgrade`
