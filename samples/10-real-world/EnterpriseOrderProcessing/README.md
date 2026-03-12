# EnterpriseOrderProcessing Reference Application

A comprehensive reference application demonstrating how to compose 14+ Excalibur package domains into a single .NET application. This sample proves that all packages register into DI without conflicts and build together end-to-end.

## What It Demonstrates

This reference application wires the complete Excalibur.Dispatch and Excalibur framework stack:

| # | Package Domain | Purpose | DI Registration |
|---|---------------|---------|-----------------|
| 1 | Core Dispatch | Message dispatching, pipeline | `AddDispatch()` |
| 2 | Handler Discovery | Assembly-based handler scanning | `AddHandlersFromAssembly()` |
| 3 | Validation | FluentValidation integration | `AddDispatchValidation().WithFluentValidation()` |
| 4 | Resilience | Polly retry + circuit breaker | `AddDispatchResilience()` |
| 5 | Transport | RabbitMQ integration event publishing | `UseRabbitMQ()` |
| 6 | Security | Message encryption + audit logging | `AddDispatchSecurity()` |
| 7 | Compliance | GDPR erasure + compliance monitoring | `AddGdprErasure()`, `AddComplianceMonitoring()` |
| 8 | Observability | OpenTelemetry metrics + distributed tracing | `ConfigureExcaliburMetrics()`, `ConfigureExcaliburTracing()` |
| 9 | Logging | Serilog structured logging | `ConfigureExcaliburLogging()` |
| 10 | Health Checks | `/health`, `/health/ready`, `/health/live` | `AddExcaliburHealthChecks()` |
| 11 | Event Sourcing | AggregateRoot, IEventSourcedRepository | `AddExcaliburEventSourcing()` |
| 12 | Outbox | SQL Server transactional outbox | `AddSqlServerOutboxStore()` |
| 13 | CDC | Change Data Capture anti-corruption layer | `IDataChangeHandler` |
| 14 | Domain | Aggregates, entities, domain events | `AggregateRoot<Guid>` |

## Architecture

The application implements an order processing pipeline:

```
Legacy SQL Database
       |
       v
CDC Handler (LegacyOrderChangeHandler)
  -- Translates legacy rows to domain commands
       |
       v
Dispatch Pipeline
  --> Validation (FluentValidation)
  --> Resilience (Polly retry + circuit breaker)
  --> CreateOrderHandler (IActionHandler<CreateOrderCommand, Guid>)
       |
       v
Event Sourcing
  --> OrderAggregate raises domain events
  --> Events persisted to EventStore
       |
       v
Outbox + Transport
  --> Events written to SQL Server outbox
  --> Published to RabbitMQ for downstream consumers
```

## Domain Model

- **OrderAggregate** -- Event-sourced aggregate with `OrderCreated`, `OrderLineAdded`, `OrderSubmitted` events
- **CreateOrderCommand** -- Command dispatched through the pipeline
- **CreateOrderValidator** -- FluentValidation rules for order creation
- **LegacyOrderChangeHandler** -- CDC anti-corruption layer translating legacy data changes

## Prerequisites

To run the full application with infrastructure:

- .NET 8.0+ SDK
- Docker (for infrastructure containers)

### Infrastructure Services

| Service | Port | Purpose |
|---------|------|---------|
| SQL Server | 1433 | Event store, outbox, CDC source |
| RabbitMQ | 5672 / 15672 | Transport for integration events |
| Elasticsearch | 9200 | Projections (optional) |

### Docker Compose

```yaml
version: '3.8'
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=YourStrong@Passw0rd
    ports:
      - "1433:1433"

  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - "5672:5672"
      - "15672:15672"

  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
    environment:
      - discovery.type=single-node
      - xpack.security.enabled=false
    ports:
      - "9200:9200"
```

Save as `docker-compose.yml` and run:

```bash
docker compose up -d
```

### Build and Run

```bash
# Build the reference application
dotnet build -c Release

# Run (validates DI composition at startup)
dotnet run --project samples/10-real-world/EnterpriseOrderProcessing/
```

The application validates DI composition at startup and prints all wired packages.

## Verification Scenarios

Seven automated verification scenarios are implemented as smoke tests in `tests/smoke/Excalibur.Dispatch.Tests.Smoke/`:

| # | Scenario | Test File | What It Proves |
|---|----------|-----------|----------------|
| 1 | Full Pipeline | `PipelineScenarioTests.cs` | CDC -> Dispatcher -> Validation -> Resilience -> Handler -> EventStore |
| 2 | Validation | `VerificationScenarioTests.cs` | Invalid command rejected by DataAnnotations middleware |
| 3 | Resilience | `VerificationScenarioTests.cs` | Polly retry middleware is active in the pipeline |
| 4 | Security | `VerificationScenarioTests.cs` | Encryption services resolve from AddDispatchSecurity() |
| 5 | Observability | `VerificationScenarioTests.cs` | OTel activities captured during command dispatch |
| 6 | Health Checks | `VerificationScenarioTests.cs` | HealthCheckService resolves and returns Healthy |
| 7 | Outbox | `VerificationScenarioTests.cs` | Outbox DI composition builds without errors |

Run all verification scenarios:

```bash
dotnet test tests/smoke/Excalibur.Dispatch.Tests.Smoke/ --filter "Component=Pipeline"
```

## Connection Strings

Update `Program.cs` connection strings for your environment:

```csharp
// SQL Server (Event Store + Outbox)
options.ConnectionString = "Server=localhost;Database=EventStore;Trusted_Connection=true;TrustServerCertificate=true";

// RabbitMQ
rmq.HostName("localhost").Credentials("guest", "guest");
```

## Related Documentation

- [Pre-Release Validation Spec](../../../management/specs/pre-release-validation-spec.md)
- [ADR-177: Sprint 603 Foundation](../../../management/architecture/adr-177-sprint-603-pre-release-validation-foundation.md)
- [ADR-178: Sprint 604 Smoke Tests + Pipeline](../../../management/architecture/adr-178-sprint-604-smoke-tests-ref-app-pipeline-serilog-fix.md)
- [ADR-179: Sprint 605 Ref App Completion](../../../management/architecture/adr-179-sprint-605-ref-app-completion-ci-gate-validation-fix.md)
