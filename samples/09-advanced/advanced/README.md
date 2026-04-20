# 09-advanced/advanced — Versioning and Edge Patterns

Schema evolution, event upcasting, and other edge scenarios that don't fit neatly in the other subcategories.

## Versioning

| Sample | What You Learn | Infrastructure |
|--------|----------------|----------------|
| [Versioning.Examples/](Versioning.Examples/) | Four focused sub-projects covering all event-versioning scenarios | None (in-memory) |

### Versioning.Examples Sub-Projects

| Sub-project | Scenario | Key Patterns |
|-------------|----------|-------------|
| [EventUpcasting](Versioning.Examples/EventUpcasting/) | Domain event V1 → V2 → V3 with aggregate replay | BFS path finding, auto-upcasting |
| [EcommerceOrderVersioning](Versioning.Examples/EcommerceOrderVersioning/) | Order event evolution with multi-hop transforms | Schema splitting (Total → Subtotal + Tax) |
| [IntegrationEventVersioning](Versioning.Examples/IntegrationEventVersioning/) | Cross-service message compatibility | `UpcastingMessageBusDecorator`, migration detection |
| [UserProfileVersioning](Versioning.Examples/UserProfileVersioning/) | GDPR-focused schema evolution | Consent tracking, email encryption, assembly scanning |

## When to Read These

- **Before** you ship v1 of an event-sourced system — designing with versioning in mind prevents painful migrations later.
- **Whenever** you need to change an existing `IDomainEvent` or integration event shape.
- **Whenever** you need to align schema evolution with compliance constraints (e.g., GDPR right-to-erasure).

## Upcasting Strategy

Excalibur's snapshot upgrading uses a **BFS shortest-path version chain** via `SnapshotVersionManager`. The `EventUpcasting` sample demonstrates this end-to-end: register upcasters for V1→V2 and V2→V3 hops, and the framework picks the shortest path automatically.

## Related

- [09-advanced/persistence-patterns/SnapshotStrategies](../persistence-patterns/SnapshotStrategies/) — snapshot upgrading intersects with versioning
- [../../06-security/AuditLogging](../../06-security/AuditLogging/) — GDPR audit patterns pair with `UserProfileVersioning`
