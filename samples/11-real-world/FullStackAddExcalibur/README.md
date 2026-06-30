# Full-Stack AddExcalibur Composition Sample

**Location:** `samples/11-real-world/FullStackAddExcalibur/`

> **Canonical L3 template :** This sample is the reference wiring
> for composing every major Excalibur subsystem through a single `AddExcalibur()`
> builder chain **and** for the canonical operational pipeline used across the
> L3 real-world samples:
>
> ```
> IDispatcher.DispatchAsync(command)
>   -> ICommandHandler<CreateOrderCommand, Guid>      (Excalibur.Application CQRS layer)
>   -> IEventSourcedRepository.SaveAsync              (event store + outbox)
>   -> IEventHandler<OrderCreated/LineItemAdded>      (projection write)
>   -> GET /orders/{id}                               (projection read)
> ```
>
> The command derives from `CommandBase<Guid>` and marks `IAmAuditable`, so it
> automatically picks up:
> - Correlation (`IAmCorrelatable`) — traces every pipeline step
> - Tenant propagation (`IAmMultiTenant`) — flows into outbox + transport envelopes
> - **Audit (`IAmAuditable`)** — `Excalibur.A3.AuditMiddleware` builds an
>   `ActivityAudited` record for every dispatch (activity name + status + tenant
>   + correlation + user + request/response/exception) and publishes it via the
>   registered `IAuditMessagePublisher`. The sample ships an in-memory
>   publisher + store so `GET /audit/recent` returns the captured records; a
>   production deployment swaps the publisher for Kafka / EventHubs /
>   ElasticSearch / Splunk / etc.
> - Activity metadata (`IActivity`) — observable name/display/description for tracing + logs
> - Transactional defaults — `TransactionScopeOption.Required`, `IsolationLevel.ReadCommitted`

### Audit wiring in this sample

```csharp
// Context services that feed IActivityContext
builder.Services.TryAddCorrelationId();
builder.Services.TryAddETag();
builder.Services.TryAddClientAddress();
builder.Services.AddScoped<IActivityContext, ActivityContext>();

// Demo in-memory audit destination (swap in Kafka/ES/etc in production)
builder.Services.AddSingleton<InMemoryAuditStore>();
builder.Services.AddSingleton<IAuditMessagePublisher, InMemoryAuditMessagePublisher>();

// The middleware that observes IAmAuditable commands
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IDispatchMiddleware, AuditMiddleware>());
```

After running, hit `GET /audit/recent?take=20` to see the recorded activities.
>
> Other full-framework L3 samples (`MultiTenantEventSourcing`,
> `CloudStorageSnapshots`) mirror this shape. Samples that do not use
> `AddExcalibur()` (such as `06-security/GdprCompliance`, `02-messaging-transports/TransportBindings`)
> stay on the thinner `IDispatchAction<T>` + `IActionHandler<T,R>` pattern.

This sample is the canonical reference for composing **every major Excalibur
subsystem under a single `AddExcalibur()` root** using the unified builder API
introduced in the builder-unification epic.

## What it demonstrates

### Subsystem composition

| # | Subsystem | How it's wired |
|---|-----------|----------------|
| 1 | Event Sourcing   | `excalibur.AddEventSourcing(es => es.UseSqlServer(...))` |
| 2 | Transactional Outbox | `excalibur.AddOutbox(outbox => outbox.UseSqlServer(...))` |
| 3 | CDC (Change Data Capture) | `excalibur.AddCdc(cdc => cdc.UseSqlServer(...).EnableBackgroundProcessing())` |
| 4 | IdentityMap (ACL ID mapping) | `excalibur.AddIdentityMap(identity => identity.UseSqlServer(...))` |
| 5 | ElasticSearch projections | `services.AddElasticSearchProjections(...)` (composes alongside) |
| 6 | DataProcessing pipeline | `services.AddDataProcessor<T>(config, section).EnableDataProcessingBackgroundService(...)` |

### Operational flow (command → handler → outbox → projection → query)

| Stage | Type | Location |
|-------|------|----------|
| Command | `CreateOrderCommand : IDispatchAction<Guid>` | `Commands/CreateOrderCommand.cs` |
| Handler | `CreateOrderHandler : IActionHandler<CreateOrderCommand, Guid>` | `Commands/CreateOrderHandler.cs` |
| Aggregate | `OrderAggregate : AggregateRoot<Guid>` | `Domain/OrderAggregate.cs` |
| Persistence | `IEventSourcedRepository<OrderAggregate, Guid>.SaveAsync(...)` | framework (writes event store + outbox atomically) |
| Projection handler | `IEventHandler<OrderCreated>` / `IEventHandler<OrderLineItemAdded>` | `Projections/OrderProjectionHandlers.cs` |
| Read model store | `IOrderProjectionStore` (in-memory demo impl) | `Projections/InMemoryOrderProjectionStore.cs` |
| HTTP read | `GET /orders/{id}`, `GET /orders` | `Program.cs` |

`CreateOrderHandler` captures the aggregate's uncommitted events before
`SaveAsync`, persists, then dispatches each event through `IDispatcher` so the
registered `IEventHandler<T>` projections update the read model in-process.
The outbox has already durably captured the same events for cross-process or
cross-service delivery.

The sample uses an `OrderAggregate` domain (event-sourced) as the business
vertical. This mirrors the `OrderManagement` domain used in the other
real-world samples.

> **Note:** Subsystems 5 and 6 (ElasticSearch, DataProcessing) currently
> register directly on `IServiceCollection`. They compose cleanly alongside
> `AddExcalibur(...)` and are included here so you can see the full pattern.

## Why this sample exists

Before builder unification every subsystem had its own top-level `services.AddX(...)` call. Consumers had to keep track of ordering rules, option
binding, and shared infrastructure by hand. The unified `AddExcalibur()` root:

- Registers **Dispatch primitives** once (idempotent via `TryAdd`)
- Wires **subsystem builders** in a single fluent chain
- Binds **every option** via `IOptions<T>` + `ValidateOnStart()`
- Produces a **single** DI graph that composes without conflicts

## Run locally

### 1. Start the infrastructure

The sample assumes two SQL Server instances and a single-node Elasticsearch
cluster on the default local ports. Any of these work:

```bash
# Option A: the CDC sample's docker-compose (which we re-use here)
docker compose -f ../../09-advanced/CdcEventStoreElasticsearch/docker-compose.yml up -d
```

### 2. Apply the schema

The sample does not ship schema scripts of its own; re-use the CDC sample
scripts for the legacy DB, event store, and outbox:

```bash
cd ../../09-advanced/CdcEventStoreElasticsearch/scripts
# run the 01-*.sql through 04-*.sql files against the two SQL Server instances
```

### 3. Run

```bash
dotnet run
```

Then exercise the canonical operational flow:

```bash
# Create an order (dispatches CreateOrderCommand)
curl -X POST http://localhost:5000/orders \
  -H 'Content-Type: application/json' \
  -d '{
    "externalOrderId": "LEGACY-00123",
    "customerId": "11111111-1111-1111-1111-111111111111",
    "customerExternalId": "CUST-42",
    "lineItems": [
      { "productName": "Widget",  "quantity": 2, "unitPrice": 9.99 },
      { "productName": "Gadget",  "quantity": 1, "unitPrice": 49.50 }
    ]
  }'
# -> 201 Created { "orderId": "<guid>" }

# Read the projected read model (populated by IEventHandler<OrderCreated>
# and IEventHandler<OrderLineItemAdded>)
curl http://localhost:5000/orders/<guid>

# Browse all projected orders
curl http://localhost:5000/orders

# Browse the audit trail (populated by Excalibur.A3.AuditMiddleware for
# every IAmAuditable command dispatched through IDispatcher)
curl http://localhost:5000/audit/recent
```

Additional probes:

- `GET /` -- summary of the wired subsystems and available endpoints
- `GET /health` -- health probe

> The demo uses the in-memory `InMemoryOrderProjectionStore`, so projections are
> visible immediately after `POST /orders` returns. A production deployment
> swaps this for an ElasticSearch implementation using the
> `ElasticsearchClient` already wired in `Program.cs`.

## Configuration surface

Every subsystem is configured via `appsettings.json`. See the file for the
full structure. Every `AddX()` builder in the framework also supports
`BindConfiguration("<section>")` directly, so you can move any value in the
JSON above into code-free configuration if you prefer.

Environment overrides:

- `appsettings.Development.json` -- lighter values for dev
- Environment variables (double-underscore nesting, e.g. `ConnectionStrings__EventStore`)

## File layout

```
FullStackAddExcalibur/
├── Commands/
│   ├── CreateOrderCommand.cs        // IDispatchAction<Guid> command
│   ├── CreateOrderHandler.cs        // IActionHandler<CreateOrderCommand,Guid>
│   └── CreateOrderRequest.cs        // HTTP contract for POST /orders
├── Domain/
│   ├── OrderAggregate.cs            // event-sourced aggregate
│   └── OrderEvents.cs               // OrderCreated / OrderLineItemAdded / ...
├── Projections/
│   ├── OrderReadModel.cs            // read-model shape
│   ├── IOrderProjectionStore.cs     // read-side abstraction
│   ├── InMemoryOrderProjectionStore.cs  // demo store (thread-safe)
│   └── OrderProjectionHandlers.cs   // IEventHandler<OrderCreated/LineItemAdded>
├── Audit/
│   ├── InMemoryAuditStore.cs            // captures ActivityAudited records
│   └── InMemoryAuditMessagePublisher.cs // IAuditMessagePublisher (demo)
├── Processors/
│   └── OrderBatchProcessor.cs       // DataProcessing producer + handler
├── Program.cs                       // AddExcalibur(...) composition + endpoints
├── appsettings.json
└── appsettings.Development.json
```

## Related samples

- `09-advanced/cdc/CdcEventStoreElasticsearch/` -- CDC + ES deep dive
- `09-advanced/deployment/DataProcessingBackgroundService/` -- DataProcessing focus
- `11-real-world/IdentityMapSample/` -- IdentityMap ACL patterns
- `11-real-world/EnterpriseOrderProcessing/` -- legacy 22-package demo (pre-unification)
- `01-getting-started/MetapackageQuickStart/` -- AddExcaliburSqlServer single-line setup
- `01-getting-started/BindConfigurationPatterns/` -- appsettings-driven configuration
- `11-real-world/MultiTenantEventSourcing/` -- tenant-scoped ES + sharding
- `09-advanced/persistence-patterns/CloudStorageSnapshots/` -- S3/Blob/GCS cold store

## Related ADRs

- `management/architecture/adr-107-hosting-consolidation.md`
- `management/architecture/adr-108-net10-multi-targeting.md`
