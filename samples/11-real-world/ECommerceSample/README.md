# ECommerce In-Memory Stores Sample

**Beads:** `bd-w8clun` (S790 rename-and-trim)
**Location:** `samples/11-real-world/EnhancedStores/ECommerceSample/`

A realistic order-processing scenario that exercises the framework's stock
`IInboxStore` / `IOutboxStore` / `IScheduleStore` contracts using their
**in-memory** implementations, wired into business services (order processing,
email notifications, scheduled inventory checks) with OpenTelemetry tracing
and health checks.

> **Sprint 790 scope correction:** Earlier revisions of this README advertised
> "Enhanced" inbox/outbox/schedule stores (`AddEnhancedInboxStore`,
> `AddEnhancedOutboxStore`, `AddEnhancedScheduleStore`). Those extension
> methods were never shipped, so the wording and the aspirational `/* ... */`
> block in `Program.cs` have been removed. The sample now honestly reflects
> the in-memory store contracts that the framework actually ships. If a
> consumer scenario surfaces a concrete need for a hardened implementation
> (e.g. content-based dedup at scale, persistent batch staging, time-indexed
> schedules), that will be scoped as a new package and its own sample; see
> `bd-w8clun`.

## What the sample demonstrates

| Area | Wired via | Running in-process |
|------|-----------|-------------------|
| Order processing + deduplication | `IInboxStore` (`InMemoryInboxStore`) | `OrderProcessingService`, `OrderProcessorHostedService` |
| Email fan-out | `IOutboxStore` (`InMemoryOutboxStore`) | `NotificationService`, `NotificationProcessorHostedService` |
| Scheduled inventory checks | `IScheduleStore` (`InMemoryScheduleStore`) | `InventoryService`, `InventoryCheckProcessor` |
| Observability | `AddDispatchTelemetry(...)` + OpenTelemetry tracing/metrics | `MetricsReportingService`, console exporter |
| Health checks | `Microsoft.Extensions.Diagnostics.HealthChecks` | `EnhancedStoreHealthCheck`, `BusinessLogicHealthCheck` |

## Run locally

```bash
dotnet run
```

The sample seeds a short workload (orders with duplicates, customer notifications,
scheduled inventory checks), then drops into an interactive console:

- `m` – print current metrics
- `h` – print health status
- `q` – shut down cleanly

## File layout

```
ECommerceSample/
├── Program.cs                        // DI wiring + host startup + console loop
├── Services/
│   └── OrderProcessingService.cs     // OrderProcessing / Notification / Inventory services
├── HostedServices/
│   └── OrderProcessorHostedService.cs// Background workers over the store contracts
└── Infrastructure/
    ├── InMemoryStores.cs             // In-memory IInboxStore/IOutboxStore/IScheduleStore
    ├── InMemoryRepositories.cs       // Sample data + email fake
    └── HealthChecks.cs               // Health-check classes consumed in Program.cs
```

## Related samples

- `11-real-world/EnterpriseOrderProcessing/` – full multi-package reference app
- `04-reliability/Outbox/` – outbox pattern focus with SQL Server
- `04-reliability/InboxConsumer/` – inbox deduplication focus
