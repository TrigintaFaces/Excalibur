# IOutboxPublisher ISP Design

## Interface Split

| Interface | Purpose | Methods |
|-----------|---------|---------|
| `IOutboxStore` | Core outbox operations | StageMessageAsync, EnqueueAsync, GetUnsentMessagesAsync, MarkSentAsync, MarkFailedAsync |
| `IOutboxStoreAdmin` | Admin/diagnostic operations | GetAllTenantsFailedMessagesAsync, GetAllTenantsScheduledMessagesAsync, CleanupAllTenantsSentMessagesAsync, GetAllTenantsStatisticsAsync |
| `IOutboxStoreBatch` | Batch operations | MarkBatchSentAsync, MarkBatchFailedAsync |

## Rationale

Core `IOutboxStore` has 5 methods (Microsoft IDistributedCache pattern). Admin operations are separated because:
- Not needed for normal message flow
- Used only by background services, health checks, and admin tooling
- Keeps the core interface minimal for implementors
