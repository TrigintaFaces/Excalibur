# Snapshot Store Coverage Matrix

## Provider Coverage

| Provider | EventStore | SnapshotStore | ProjectionStore |
|----------|-----------|---------------|-----------------|
| SqlServer | Yes | Yes | Yes |
| Postgres | Yes | Yes | Yes |
| InMemory | Yes | Yes | Yes |
| Redis | Yes | No | No |
| MongoDB | No | No | No |
| CosmosDB | No | No | No |

## Notes

- **Redis**: Suitable for single-aggregate event sourcing only. No globally ordered streams across keys.
- **MongoDB/CosmosDB**: Event sourcing implementations are CDC-based (change feed), not traditional append-only stores.
- **Snapshot upgrading**: BFS shortest-path version chain via `SnapshotVersionManager` (Sprint 557).
- New providers should implement all 3 interfaces for full coverage.
