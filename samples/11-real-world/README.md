# 11-real-world — Production Reference Applications

End-to-end samples that compose multiple Dispatch and Excalibur patterns into realistic production applications. These are the showcases — they demonstrate *"this is what a complete system looks like"*.

## Samples

| Sample | Focus | Key Patterns |
|--------|-------|-------------|
| [OrderProcessing](OrderProcessing/) | Complete order workflow | Event Sourcing, CQRS, Saga, Retry, Compensation |
| [EnterpriseOrderProcessing](EnterpriseOrderProcessing/) | Enterprise stack (22+ packages) | CDC, Outbox, RabbitMQ, OpenTelemetry, Security, Resilience |
| [ECommerceSample](ECommerceSample/) | ECommerce In-Memory Stores | In-memory `IInboxStore`/`IOutboxStore`/`IScheduleStore`, order processing, OTel tracing |
| [FullStackAddExcalibur](FullStackAddExcalibur/) 🧩 *infra-gated flow* | Single-builder composition — OrderManagement domain | ES + CDC + projections + ElasticSearch + IdentityMap + DataProcessing, all composed through `AddExcalibur`. Sprint 790 added the operational `POST /orders` command → handler → outbox → projection → `GET /orders/{id}` flow (`bd-hctd97`); the full E2E path requires SQL Server + ElasticSearch. Host boot + `GET /health` work without external infra. |
| [MultiTenantEventSourcing](MultiTenantEventSourcing/) 🧩 *infra-gated flow* | Multi-tenant SaaS composition | Tenant context resolution, API routing, per-tenant projections, query scoping, sharding. Sprint 790 added the operational `POST /orders` command with `ITenantId` resolution that exercises the `TenantRoutingEventStore` decorator (`bd-vpna3f`); full decorator path requires SQL Server per shard. `GET /shards` + `ValidateOnStart` work without external infra. |
| [HealthcareApi](HealthcareApi/) | Vertical-slice architecture | Per-feature slicing, healthcare domain |
| [IdentityMapSample](IdentityMapSample/) | Identity map pattern documentation | See README for usage (no runnable project) |

## OrderProcessing — Flagship

The [OrderProcessing](OrderProcessing/) sample is the canonical *"how does all this fit together"* showcase.

### Patterns Demonstrated

| Pattern | Implementation | Package |
|---------|----------------|---------|
| **Event Sourcing** | `OrderAggregate` with full event history | `Excalibur.Domain` |
| **CQRS** | Separate command/query models | `Excalibur.Dispatch` |
| **Saga Pattern** | 5-step workflow orchestration | `Excalibur.Saga` |
| **Retry** | Exponential backoff for payment | `Excalibur.Dispatch.Resilience.Polly` |
| **Compensation** | Automatic rollback on failure | Saga state machine |
| **Validation** | FluentValidation integration | `Excalibur.Dispatch.Validation.FluentValidation` |

### Order Workflow

```
┌─────────┐    ┌───────────┐    ┌─────────────────┐    ┌───────────┐    ┌───────────┐
│ Created │───►│ Validated │───►│ PaymentProcessed│───►│  Shipped  │───►│ Completed │
└─────────┘    └───────────┘    └─────────────────┘    └───────────┘    └───────────┘
     │              │
     │              ▼
     │    ┌──────────────────┐
     │    │ ValidationFailed │
     │    └──────────────────┘
     │
     └──────────────────────────────────────────────────────────┐
                                                                 │
                                                         ┌───────▼──┐
                                                         │ Cancelled│
                                                         └──────────┘
```

### Running

```bash
cd samples/11-real-world/OrderProcessing
dotnet run
```

### Demo Scenarios

1. **Successful Order Processing** — Complete workflow from creation to shipping
2. **Retry Pattern** — Transient payment failures with exponential backoff
3. **Validation Failure** — FluentValidation rejecting invalid commands
4. **Saga Compensation** — Inventory validation failure with rollback
5. **Order Cancellation** — Cancelling an order before processing
6. **Delivery Confirmation** — Completing the order lifecycle

## FullStackAddExcalibur — Canonical Composition

[FullStackAddExcalibur](FullStackAddExcalibur/) is the newest showcase (Sprint 789): it demonstrates how a realistic enterprise application is composed through a single `AddExcalibur` builder. Domain: OrderManagement. Every canonical pattern plugs in at its expected position:

- **Event Sourcing** (write side)
- **CDC** (change feed out of the event store)
- **Projections** (inline + async)
- **ElasticSearch** (read side)
- **IdentityMap** (unit-of-work caching)
- **DataProcessing** (CDC/outbox/job workers)
- **BindConfiguration** (`appsettings.json` driven setup)

Read this sample to understand *"how should I wire a real app together?"*.

## EnterpriseOrderProcessing — Full Stack Variant

[EnterpriseOrderProcessing](EnterpriseOrderProcessing/) extends OrderProcessing with the **full enterprise stack**: CDC from SQL Server, RabbitMQ transport for events, full OpenTelemetry instrumentation, encrypted fields, audit logging, outbox for reliability, and health checks. Use this as the template for a production deployment.

## HealthcareApi — Vertical Slice Architecture

[HealthcareApi](HealthcareApi/) demonstrates **vertical-slice architecture** — each feature is self-contained with its own command, handler, validator, and endpoint. Ideal if you prefer per-feature cohesion over horizontal layering.

## Design Principles

All samples in this category follow:

1. **Clean Architecture** — separation of concerns between layers (or vertical slicing in `HealthcareApi`)
2. **CQRS** — commands for writes, queries for reads
3. **Domain-Driven Design** — rich domain models
4. **Dependency Injection** — loosely coupled components
5. **Configuration binding** — environment-based settings via `IOptions<T>` and `BindConfiguration`

## Production Checklist

Your production deployment should verify:

- [ ] Health checks configured (see [07-observability/HealthChecks](../07-observability/HealthChecks/))
- [ ] Structured logging enabled
- [ ] Metrics exported (OTel)
- [ ] Distributed tracing active
- [ ] Retry policies in place (`Excalibur.Dispatch.Resilience.Polly`)
- [ ] Circuit breakers configured
- [ ] Outbox pattern for reliability (`Excalibur.EventSourcing.SqlServer`)
- [ ] Validation middleware enabled
- [ ] Leader election for singletons (`Excalibur.LeaderElection.Redis`)
- [ ] PII redaction via `ITelemetrySanitizer` (see [06-security/AuditLogging](../06-security/AuditLogging/))
- [ ] Secret management (Azure Key Vault or AWS Secrets Manager)

## Upgrading from In-Memory to Production

```csharp
// Compose through AddExcalibur (the canonical builder)
services.AddExcalibur(excalibur =>
{
    excalibur.AddExcaliburSqlServer(sql =>
    {
        sql.ConnectionString = connectionString;
        sql.UseInbox = true;
        sql.ConfigureAuditLogging(audit => audit.Enabled = true);
    });

    excalibur.AddEventSourcing(es =>
    {
        es.AddRepository<OrderAggregate, Guid>();
    });

    excalibur.AddExcaliburCdc();
    excalibur.AddResilience();
    excalibur.AddObservability();
});
```

## Related Categories

- [09-advanced/](../09-advanced/) — individual pattern samples (use these to dig into any specific aspect)
- [04-reliability/](../04-reliability/) — outbox, retry, circuit breaker (composed in these real-world apps)
- [06-security/](../06-security/) — encryption, audit logging, secrets
- [07-observability/](../07-observability/) — OpenTelemetry, health checks

---

*Category: Real-World | Sprint 789 reorg*
