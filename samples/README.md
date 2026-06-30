# Excalibur Samples — Learning Path

Working samples demonstrating the **Dispatch** (messaging) and **Excalibur** (application) frameworks. Each sample is self-contained and runnable with `dotnet run`.

> **New to the frameworks?** Follow the numbered folders in order. Each category builds on the previous. If you skip ahead, some samples assume knowledge from earlier stages.

## How This Directory Is Organized

The 11 top-level folders form a progressive learning path:

```
01-getting-started     →  Fundamentals (read these FIRST)
02-messaging-transports →  Connect to real message brokers
03-cloud-native         →  Claim check and cloud messaging patterns
04-reliability          →  Outbox, retry, circuit breaker, saga
05-serverless           →  Functions / Lambda / GCF hosting
06-security             →  Encryption, audit, secrets, access control
07-observability        →  OpenTelemetry + health checks
08-serialization        →  Binary serializers (MemoryPack, MessagePack, Protobuf)
09-advanced/            →  Production patterns — subcategorized (see below)
10-aot                  →  Native AOT sample
11-real-world           →  End-to-end reference applications
```

`09-advanced/` is large and has five focused subcategories:

```
09-advanced/
├── persistence-patterns/  Event stores, snapshots, inbox, transactions, multi-tenancy
├── cdc/                    Change Data Capture (SQL → events), anti-corruption, scheduled jobs
├── querying/               Projections, streaming, validation, data-provider repositories
├── deployment/             Background services, leader election, production pipelines, testing
└── advanced/               Versioning, schema evolution, edge patterns
```

## Recommended Learning Path

### Level 1 — Fundamentals (start here, <1 hour total)

| Step | Sample | Time | What You Learn |
|------|--------|------|----------------|
| 1 | [01-getting-started/HelloDispatch](01-getting-started/HelloDispatch/) | 5 min | Simplest possible `AddExcalibur` setup — send a command, handle it |
| 2 | [01-getting-started/DispatchOnly](01-getting-started/DispatchOnly/) | 15 min | Commands, events, documents, and a custom middleware |
| 3 | [01-getting-started/WebApiQuickStart](01-getting-started/WebApiQuickStart/) | 15 min | ASP.NET Core API with commands, queries, events, `[AutoRegister]` |
| 4 | [01-getting-started/DataAccessIntro](01-getting-started/DataAccessIntro/) | 10 min | `IDataRequest` pattern with Dapper — no EF Core |
| 5 | [01-getting-started/EventSourcingIntro](01-getting-started/EventSourcingIntro/) | 30 min | CQRS with `AggregateRoot`, in-memory event store, snapshots |

### Level 2 — Pick a Scenario

```
What are you building?
├── MediatR replacement          → 01-getting-started/DispatchOnly
├── Web API with CQRS            → 01-getting-started/WebApiQuickStart
├── Event sourcing
│   ├── In-memory (learning)     → 09-advanced/persistence-patterns/ProjectionsSample
│   ├── SQL Server (production)  → 09-advanced/persistence-patterns/SqlServerEventStore
│   └── Full CQRS + search       → 09-advanced/cdc/CdcEventStoreElasticsearch
├── Messaging transport
│   ├── RabbitMQ                 → 02-messaging-transports/RabbitMQ
│   ├── Kafka                    → 02-messaging-transports/Kafka
│   ├── Azure Service Bus        → 02-messaging-transports/AzureServiceBus
│   ├── AWS SQS                  → 02-messaging-transports/AwsSqs
│   └── Multiple brokers         → 02-messaging-transports/MultiBusSample
├── Serverless
│   ├── Azure Functions          → 05-serverless/AzureFunctions
│   ├── AWS Lambda               → 05-serverless/AwsLambda
│   └── Google Cloud Functions   → 05-serverless/GoogleCloudFunctions
├── Reliability
│   ├── Outbox pattern           → 04-reliability/OutboxPattern
│   ├── Retry + circuit breaker  → 04-reliability/RetryAndCircuitBreaker
│   └── Saga orchestration       → 04-reliability/SagaOrchestration
├── Security & compliance        → 06-security/
├── Observability                → 07-observability/
├── Native AOT                   → 10-aot/
└── Production reference         → 11-real-world/EnterpriseOrderProcessing
```

## Sample Categories

### [01-getting-started/](01-getting-started/) — Fundamentals

| Sample | Description | Time |
|--------|-------------|------|
| [HelloDispatch](01-getting-started/HelloDispatch/) | Simplest `AddExcalibur` setup — send a command, handle it | 5 min |
| [DispatchOnly](01-getting-started/DispatchOnly/) | Commands, events, documents, custom middleware (no Excalibur) | 15 min |
| [WebApiQuickStart](01-getting-started/WebApiQuickStart/) | ASP.NET Core API with commands, queries, events, `[AutoRegister]` | 15 min |
| [EventSourcingIntro](01-getting-started/EventSourcingIntro/) | CQRS with `AggregateRoot`, event sourcing, in-memory store | 30 min |
| [InteractiveDemo](01-getting-started/InteractiveDemo/) | Interactive walkthrough of core concepts | 10 min |
| [DataAccessIntro](01-getting-started/DataAccessIntro/) | `IDataRequest` pattern with Dapper | 10 min |

### [02-messaging-transports/](02-messaging-transports/) — Transport Providers

Each transport sample includes Docker Compose for local development.

| Sample | Transport | Key Features |
|--------|-----------|-------------|
| [RabbitMQ](02-messaging-transports/RabbitMQ/) | RabbitMQ 3.12 | Topic exchange routing, CloudEvents |
| [Kafka](02-messaging-transports/Kafka/) | Kafka 7.5 (KRaft) | Consumer groups, partitioning, compression |
| [AzureServiceBus](02-messaging-transports/AzureServiceBus/) | Azure Service Bus | Queues, topics, sessions |
| [AwsSqs](02-messaging-transports/AwsSqs/) | AWS SQS + LocalStack | Standard/FIFO queues, DLQ |
| [MultiBusSample](02-messaging-transports/MultiBusSample/) | RabbitMQ + Kafka | Multi-transport routing |
| [RemoteBusSample](02-messaging-transports/RemoteBusSample/) | RabbitMQ | Remote bus with outbox/inbox |
| [TransportBindings](02-messaging-transports/TransportBindings/) | In-memory | Transport binding configuration |
| [MultiProviderQueueProcessor](02-messaging-transports/MultiProviderQueueProcessor/) | All 5 providers | Cross-provider message processing |

### [03-cloud-native/](03-cloud-native/) — Cloud Patterns

| Sample | Description |
|--------|-------------|
| [CloudEvents](03-cloud-native/CloudEvents/) | CloudEvents spec — cross-platform event envelope |
| [CloudNativePatterns.Examples](03-cloud-native/CloudNativePatterns.Examples/) | Claim check pattern for large message payloads |

### [04-reliability/](04-reliability/) — Reliability Patterns

| Sample | Pattern | Infrastructure |
|--------|---------|----------------|
| [OutboxPattern](04-reliability/OutboxPattern/) | Transactional outbox, guaranteed delivery | None (in-memory) |
| [RetryAndCircuitBreaker](04-reliability/RetryAndCircuitBreaker/) | Polly: retry, circuit breaker, timeout, bulkhead | None |
| [SagaOrchestration](04-reliability/SagaOrchestration/) | Distributed coordination, compensation, timeout scheduling | None (in-memory) |

### [05-serverless/](05-serverless/) — Serverless Hosting

| Sample | Platform | Triggers | Local Dev |
|--------|----------|----------|-----------|
| [AzureFunctions](05-serverless/AzureFunctions/) | Azure Functions v4 | HTTP, Queue, Timer | `func start` |
| [AwsLambda](05-serverless/AwsLambda/) | AWS Lambda | API Gateway, SQS, EventBridge | `sam local start-api` |
| [GoogleCloudFunctions](05-serverless/GoogleCloudFunctions/) | GCF Gen2 | HTTP, Pub/Sub, Scheduler | `dotnet run` |

### [06-security/](06-security/) — Security & Compliance

| Sample | Pattern | Infrastructure |
|--------|---------|----------------|
| [MessageEncryption](06-security/MessageEncryption/) | Field-level encryption, key rotation, PCI compliance | None (DataProtection) |
| [AuditLogging](06-security/AuditLogging/) | SOC2/HIPAA/GDPR compliance logging, PII redaction | None (in-memory) |
| [AzureKeyVault](06-security/AzureKeyVault/) | `ICredentialStore`, managed identity, secret caching | Azure account |
| [AwsSecretsManager](06-security/AwsSecretsManager/) | Secret retrieval, IAM auth, rotation | LocalStack |
| [StandaloneA3](06-security/StandaloneA3/) | Access control kernel (A3) outside Dispatch | None |
| [AccessReviews](06-security/AccessReviews/) | Periodic access reviews | None |
| [SeparationOfDuties](06-security/SeparationOfDuties/) | SoD policies for sensitive operations | None |
| [ProvisioningWorkflow](06-security/ProvisioningWorkflow/) | Identity lifecycle provisioning | None |
| [JitAccess](06-security/JitAccess/) | Just-in-time elevated access | None |

### [07-observability/](07-observability/) — Monitoring

| Sample | Pattern | Infrastructure |
|--------|---------|----------------|
| [OpenTelemetry](07-observability/OpenTelemetry/) | Distributed tracing (Jaeger), custom spans, metrics | Docker (Jaeger) |
| [HealthChecks](07-observability/HealthChecks/) | Kubernetes liveness/readiness probes, health checks UI | None |

### [08-serialization/](08-serialization/) — Serialization

| Sample | Serializer | Best For |
|--------|------------|----------|
| [MemoryPackSample](08-serialization/MemoryPackSample/) | MemoryPack | Maximum .NET performance, Native AOT |
| [MessagePackSample](08-serialization/MessagePackSample/) | MessagePack + LZ4 | High throughput, compact binary |
| [Protobuf](08-serialization/Protobuf/) | Protocol Buffers | Cross-language interoperability |

### [09-advanced/](09-advanced/) — Advanced Patterns (subcategorized)

See the [09-advanced README](09-advanced/README.md) for detailed learning tracks.

**Five focused subcategories:**

| Subcategory | What It Covers |
|-------------|----------------|
| [persistence-patterns/](09-advanced/persistence-patterns/) | Event stores (SQL Server, Cosmos, in-memory), snapshots, inbox, transactional handlers, multi-database, session state |
| [cdc/](09-advanced/cdc/) | Change Data Capture: anti-corruption layer, CDC → event store → ES projections, Quartz-scheduled CDC |
| [querying/](09-advanced/querying/) | Projections, streaming handlers, validation, and all data-provider repositories (ElasticSearch, CosmosDb, DynamoDb, Firestore, MongoDB, OpenSearch, Postgres, MySql, Redis) |
| [deployment/](09-advanced/deployment/) | Background services, leader election, job workers, production pipeline, testing utilities |
| [advanced/](09-advanced/advanced/) | Versioning and schema evolution (4 scenarios: domain, ecommerce, integration, GDPR) |

### [10-aot/](10-aot/) — Native AOT

| Sample | Description |
|--------|-------------|
| [Excalibur.Dispatch.Aot.Sample](10-aot/Excalibur.Dispatch.Aot.Sample/) | AOT-compatible Dispatch with source generators — no reflection, no dynamic code |

### [11-real-world/](11-real-world/) — Production Reference

| Sample | Focus | Key Patterns |
|--------|-------|-------------|
| [OrderProcessing](11-real-world/OrderProcessing/) | Complete order workflow | Event Sourcing, Saga, Retry, Compensation |
| [EnterpriseOrderProcessing](11-real-world/EnterpriseOrderProcessing/) | Enterprise stack (22+ packages) | CDC, Outbox, RabbitMQ, OTel, Security |
| [ECommerceSample](11-real-world/ECommerceSample/) | ECommerce In-Memory Stores | In-memory `IInboxStore`/`IOutboxStore`/`IScheduleStore`, order processing, OTel tracing |
| [FullStackAddExcalibur](11-real-world/FullStackAddExcalibur/) ⚠️ *wiring reference* | Single `AddExcalibur` composition — OrderManagement domain | ES + CDC + projections + ElasticSearch + IdentityMap + DataProcessing |
| [MultiTenantEventSourcing](11-real-world/MultiTenantEventSourcing/) ⚠️ *shard-map reference* | Multi-tenant SaaS composition | Tenant context, routing, per-tenant projections, query scoping |
| [HealthcareApi](11-real-world/HealthcareApi/) | Vertical-slice architecture | Healthcare domain, per-feature slicing |

> ⚠️ **Reference-scope samples .** The ⚠️-tagged samples above demonstrate *wiring* but do not yet exercise the composed flow end-to-end. Each sample's README has a "Scope note" section explaining exactly what is and is not demonstrated, and links to its Beads task for the operational-flow expansion. This applies to `FullStackAddExcalibur` , `MultiTenantEventSourcing` , `CloudStorageSnapshots` in [`09-advanced/persistence-patterns/`](09-advanced/persistence-patterns/CloudStorageSnapshots/) , and `GdprCompliance` in [`06-security/`](06-security/GdprCompliance/) . Sample compositional quality overall is tracked under umbrella epic .

## Running Samples

```bash
# Build and run any sample
dotnet run --project samples/01-getting-started/HelloDispatch

# Samples with Docker dependencies
cd samples/09-advanced/persistence-patterns/SqlServerEventStore
docker-compose up -d      # Start infrastructure
dotnet run                # Run the sample
```

### Prerequisites

- **.NET 9.0 SDK** or later
- **Docker Desktop** for samples marked with "Docker" infrastructure
- Specific cloud SDKs for serverless/cloud samples (documented per sample)

> ⚠️ **Demo credentials — do not use in production.** Several samples use the canonical Microsoft SQL Server demo password `YourStrong@Passw0rd` (from Microsoft's `mcr.microsoft.com/mssql/server` image documentation) in `appsettings.json` / `Program.cs` connection strings. These are **local development only** — they work with the Docker SQL Server container the samples spin up. In any real deployment, load credentials from a secret store (see [`06-security/AzureKeyVault`](06-security/AzureKeyVault/), [`06-security/AwsSecretsManager`](06-security/AwsSecretsManager/)) and never commit them.

### Build All Samples

```bash
# Sequential
dotnet build eng/ci/shards/SamplesOnly.slnf -c Release

# Or run the CI gate script
./eng/ci/build-samples.sh              # Sequential
./eng/ci/build-samples.sh --parallel   # Parallel (faster, noisier)
```

## Converting to Your Own Project

Samples use `ProjectReference` for development convenience. Convert to `PackageReference` for your own projects:

```xml
<!-- Before (sample) -->
<ProjectReference Include="$(DispatchSourceRoot)Excalibur.Dispatch\Excalibur.Dispatch.csproj" />

<!-- After (your project) -->
<PackageReference Include="Excalibur.Dispatch" Version="1.0.0" />
```

See [CONVERSION-GUIDE.md](CONVERSION-GUIDE.md) for complete instructions.

## Migrating from Dispatch-only to Full Excalibur

If you started with pure Dispatch messaging and want to add event sourcing, sagas, or read model projections, see [MIGRATION.md](MIGRATION.md).

## Where the Old Folders Went (reorganization)

If you're looking for a sample that used to live at `09-advanced/<Something>/`, `13-jobs/`, `14-data-providers/`, `11-aot/`, `12-vertical-slice-api/`, or `10-real-world/`, here is the mapping:

| Old Location | New Location |
|--------------|--------------|
| `09-advanced/ProjectionsSample/` | `09-advanced/persistence-patterns/ProjectionsSample/` |
| `09-advanced/SqlServerEventStore/` | `09-advanced/persistence-patterns/SqlServerEventStore/` |
| `09-advanced/CosmosDbEventStore/` | `09-advanced/persistence-patterns/CosmosDbEventStore/` |
| `09-advanced/SnapshotStrategies/` | `09-advanced/persistence-patterns/SnapshotStrategies/` |
| `09-advanced/InboxIdempotency/` | `09-advanced/persistence-patterns/InboxIdempotency/` |
| `09-advanced/TransactionalHandlers/` | `09-advanced/persistence-patterns/TransactionalHandlers/` |
| `09-advanced/MultiDatabase/` | `09-advanced/persistence-patterns/MultiDatabase/` |
| `09-advanced/SessionManagement/` | `09-advanced/persistence-patterns/SessionManagement/` |
| `09-advanced/CdcAntiCorruption/` | `09-advanced/cdc/CdcAntiCorruption/` |
| `09-advanced/CdcEventStoreElasticsearch/` | `09-advanced/cdc/CdcEventStoreElasticsearch/` |
| `09-advanced/StreamingHandlers/` | `09-advanced/querying/StreamingHandlers/` |
| `09-advanced/FluentValidationSample/` | `09-advanced/querying/FluentValidationSample/` |
| `09-advanced/BackgroundServices/` | `09-advanced/deployment/BackgroundServices/` |
| `09-advanced/DataProcessingBackgroundService/` | `09-advanced/deployment/DataProcessingBackgroundService/` |
| `09-advanced/JobWorkerSample/` | `09-advanced/deployment/JobWorkerSample/` |
| `09-advanced/LeaderElection/` | `09-advanced/deployment/LeaderElection/` |
| `09-advanced/ProductionPipeline/` | `09-advanced/deployment/ProductionPipeline/` |
| `09-advanced/Testing/` | `09-advanced/deployment/Testing/` |
| `09-advanced/Versioning.Examples/` | `09-advanced/advanced/Versioning.Examples/` |
| `13-jobs/CdcJobQuartz/` | `09-advanced/cdc/CdcJobQuartz/` |
| `14-data-providers/*` | `09-advanced/querying/*` (all 15 data-providers) |
| `10-real-world/EnhancedStores/` | `11-real-world/EnhancedStores/` |
| `11-real-world/EnhancedStores/ECommerceSample/` | `11-real-world/ECommerceSample/` |
| `10-real-world/EnterpriseOrderProcessing/` | `11-real-world/EnterpriseOrderProcessing/` |
| `10-real-world/OrderProcessing/` | `11-real-world/OrderProcessing/` |
| `10-real-world/IdentityMapSample/` | `11-real-world/IdentityMapSample/` |
| `10-real-world/FullStackAddExcalibur/` | `11-real-world/FullStackAddExcalibur/` |
| `11-aot/*` | `10-aot/*` |
| `12-vertical-slice-api/HealthcareApi/` | `11-real-world/HealthcareApi/` |

## Related Documentation

- [Framework Documentation](../docs-site/docs/) — Consumer-facing package docs
- [Event Sourcing Guide](../docs-site/docs/event-sourcing/)
- [API Reference](../docs-site/docs/reference/)
- [MIGRATION.md](MIGRATION.md) — Dispatch → Excalibur migration path
- [CONVERSION-GUIDE.md](CONVERSION-GUIDE.md) — ProjectReference → PackageReference
