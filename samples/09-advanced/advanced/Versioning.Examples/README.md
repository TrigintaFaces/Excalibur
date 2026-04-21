# Event Versioning Examples

Demonstrates event versioning and upcasting strategies for evolving domain models over time without breaking existing event streams.

## Projects

| Project | Description |
|---------|-------------|
| [EcommerceOrderVersioning](EcommerceOrderVersioning/) | Order event schema evolution (V1 -> V2 -> V3) |
| [EventUpcasting](EventUpcasting/) | Custom event upcaster implementations |
| [IntegrationEventVersioning](IntegrationEventVersioning/) | Versioning integration events across bounded contexts |
| [UserProfileVersioning](UserProfileVersioning/) | User profile event migration with backward compatibility |

## What You'll Learn

- Writing event upcasters for schema evolution
- Maintaining backward compatibility with existing event streams
- Versioning strategies for domain events vs integration events
- Testing upcaster chains

## Related Docs

- [Event Versioning](../../docs-site/docs/event-sourcing/versioning.md)
- [Event Migrations](../../docs-site/docs/event-sourcing/migrations.md)
