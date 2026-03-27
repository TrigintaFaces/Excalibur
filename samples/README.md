# Excalibur Samples

Working samples demonstrating the Dispatch and Excalibur frameworks. Each sample is self-contained and runnable with `dotnet run`.

## Quick Start

**New to Excalibur?** Follow this path:

```
1. HelloDispatch ................... Simplest possible setup (5 min)
2. DispatchOnly .................... Commands, events, queries, middleware (15 min)
3. WebApiQuickStart ................ ASP.NET Core API with CQRS (15 min)
4. EventSourcingIntro .............. Aggregates, event sourcing, snapshots (30 min)
```

**Ready for production patterns?** Pick your scenario:

```
What are you building?
├── MediatR replacement              → 01-getting-started/DispatchOnly
├── Web API with CQRS                → 01-getting-started/WebApiQuickStart
├── Event sourcing
│   ├── In-memory (learning)         → 09-advanced/ProjectionsSample
│   ├── SQL Server (production)      → 09-advanced/SqlServerEventStore
│   └── Full CQRS + search           → 09-advanced/CdcEventStoreElasticsearch
├── Messaging transport
│   ├── RabbitMQ                     → 02-messaging-transports/RabbitMQ
│   ├── Kafka                        → 02-messaging-transports/Kafka
│   ├── Azure Service Bus            → 02-messaging-transports/AzureServiceBus
│   ├── AWS SQS                      → 02-messaging-transports/AwsSqs
│   └── Multiple brokers             → 02-messaging-transports/MultiBusSample
├── Serverless
│   ├── Azure Functions              → 05-serverless/AzureFunctions
│   ├── AWS Lambda                   → 05-serverless/AwsLambda
│   └── Google Cloud Functions       → 05-serverless/GoogleCloudFunctions
├── Reliability
│   ├── Outbox pattern               → 04-reliability/OutboxPattern
│   ├── Retry + circuit breaker      → 04-reliability/RetryAndCircuitBreaker
│   └── Saga orchestration           → 04-reliability/SagaOrchestration
├── Security & compliance            → 06-security/
├── Observability                    → 07-observability/
├── NativeAOT                        → 11-aot/
└── Production reference             → 10-real-world/EnterpriseOrderProcessing
```

## Sample Categories

### [01-getting-started/](01-getting-started/) -- Fundamentals

| Sample | Description | Time |
|--------|-------------|------|
| [HelloDispatch](01-getting-started/HelloDispatch/) | Simplest Dispatch setup -- send a command, handle it | 5 min |
| [DispatchOnly](01-getting-started/DispatchOnly/) | Commands, events, documents, custom middleware (no Excalibur) | 15 min |
| [WebApiQuickStart](01-getting-started/WebApiQuickStart/) | ASP.NET Core API with commands, queries, events, `[AutoRegister]` | 15 min |
| [EventSourcingIntro](01-getting-started/EventSourcingIntro/) | CQRS with `AggregateRoot`, event sourcing, in-memory store | 30 min |
| [InteractiveDemo](01-getting-started/InteractiveDemo/) | Interactive walkthrough of core concepts | 10 min |
| [DataAccessIntro](01-getting-started/DataAccessIntro/) | `IDataRequest` pattern with Dapper | 10 min |

### [02-messaging-transports/](02-messaging-transports/) -- Transport Providers

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

### [03-cloud-native/](03-cloud-native/) -- Cloud Patterns

| Sample | Description |
|--------|-------------|
| [CloudNativePatterns.Examples](03-cloud-native/CloudNativePatterns.Examples/) | Claim check pattern for large message payloads |

### [04-reliability/](04-reliability/) -- Reliability Patterns

| Sample | Pattern | Infrastructure |
|--------|---------|----------------|
| [OutboxPattern](04-reliability/OutboxPattern/) | Transactional outbox, guaranteed delivery | None (in-memory) |
| [RetryAndCircuitBreaker](04-reliability/RetryAndCircuitBreaker/) | Polly: retry, circuit breaker, timeout, bulkhead | None |
| [SagaOrchestration](04-reliability/SagaOrchestration/) | Distributed coordination, compensation, timeout scheduling | None (in-memory) |

### [05-serverless/](05-serverless/) -- Serverless Hosting

| Sample | Platform | Triggers | Local Dev |
|--------|----------|----------|-----------|
| [AzureFunctions](05-serverless/AzureFunctions/) | Azure Functions v4 | HTTP, Queue, Timer | `func start` |
| [AwsLambda](05-serverless/AwsLambda/) | AWS Lambda (.NET 8) | API Gateway, SQS, EventBridge | `sam local start-api` |
| [GoogleCloudFunctions](05-serverless/GoogleCloudFunctions/) | GCF Gen2 (.NET 8) | HTTP, Pub/Sub, Scheduler | `dotnet run` |

### [06-security/](06-security/) -- Security & Compliance

| Sample | Pattern | Infrastructure |
|--------|---------|----------------|
| [MessageEncryption](06-security/MessageEncryption/) | Field-level encryption, key rotation, PCI compliance | None (DataProtection) |
| [AuditLogging](06-security/AuditLogging/) | SOC2/HIPAA/GDPR compliance logging, PII redaction | None (in-memory) |
| [AzureKeyVault](06-security/AzureKeyVault/) | `ICredentialStore`, managed identity, secret caching | Azure account |
| [AwsSecretsManager](06-security/AwsSecretsManager/) | Secret retrieval, IAM auth, rotation | LocalStack |

### [07-observability/](07-observability/) -- Monitoring

| Sample | Pattern | Infrastructure |
|--------|---------|----------------|
| [OpenTelemetry](07-observability/OpenTelemetry/) | Distributed tracing (Jaeger), custom spans, metrics | Docker (Jaeger) |
| [HealthChecks](07-observability/HealthChecks/) | Kubernetes liveness/readiness probes, health checks UI | None |

### [08-serialization/](08-serialization/) -- Serialization

| Sample | Serializer | Best For |
|--------|------------|----------|
| [MemoryPack](08-serialization/MemoryPackSample/) | MemoryPack | Maximum .NET performance, NativeAOT |
| [MessagePack](08-serialization/MessagePackSample/) | MessagePack + LZ4 | High throughput, compact binary |
| [Protobuf](08-serialization/Protobuf/) | Protocol Buffers | Cross-language interoperability |

### [09-advanced/](09-advanced/) -- Advanced Patterns

See the [09-advanced README](09-advanced/README.md) for learning tracks and detailed guidance.

**Event Sourcing:**

| Sample | What You'll Learn | Infrastructure |
|--------|-------------------|----------------|
| [ProjectionsSample](09-advanced/ProjectionsSample/) | CQRS read models, inline/async projections, rebuild | None |
| [SqlServerEventStore](09-advanced/SqlServerEventStore/) | SQL Server persistence, rehydration, configuration | Docker |
| [CosmosDbEventStore](09-advanced/CosmosDbEventStore/) | Cosmos DB partitioning, change feed | Cosmos Emulator |
| [SnapshotStrategies](09-advanced/SnapshotStrategies/) | 5 snapshot strategies, tuning guide | None |

**CDC & Legacy Integration:**

| Sample | What You'll Learn | Infrastructure |
|--------|-------------------|----------------|
| [CdcAntiCorruption](09-advanced/CdcAntiCorruption/) | Anti-corruption layer, schema adaptation, backfill | Docker |
| [CdcEventStoreElasticsearch](09-advanced/CdcEventStoreElasticsearch/) | Full CQRS: CDC -> ES projections -> search API | Docker |

**Versioning, Coordination, Background Processing:**

| Sample | What You'll Learn |
|--------|-------------------|
| [Versioning.Examples/](09-advanced/Versioning.Examples/) | 4 event versioning scenarios (domain, ecommerce, integration, GDPR) |
| [LeaderElection](09-advanced/LeaderElection/) | Redis-based distributed leader election |
| [BackgroundServices](09-advanced/BackgroundServices/) | 4 background service hosting patterns |
| [JobWorkerSample](09-advanced/JobWorkerSample/) | Quartz scheduling with persistent store and Redis coordination |

### [10-real-world/](10-real-world/) -- Production Reference

| Sample | Focus | Key Patterns |
|--------|-------|-------------|
| [OrderProcessing](10-real-world/OrderProcessing/) | Complete order workflow | Event Sourcing, Saga, Retry, Compensation |
| [EnterpriseOrderProcessing](10-real-world/EnterpriseOrderProcessing/) | Enterprise stack (22+ packages) | CDC, Outbox, RabbitMQ, OTel, Security |
| [EnhancedStores](10-real-world/EnhancedStores/) | Modern async patterns | Inbox deduplication, Outbox batching, Observability |

### [11-aot/](11-aot/) -- NativeAOT

| Sample | Description |
|--------|-------------|
| [Excalibur.Dispatch.Aot.Sample](11-aot/Excalibur.Dispatch.Aot.Sample/) | AOT-compatible Dispatch with source generators |

### [12-vertical-slice-api/](12-vertical-slice-api/) -- Architecture

| Sample | Description |
|--------|-------------|
| [HealthcareApi](12-vertical-slice-api/HealthcareApi/) | Vertical slice architecture for healthcare domain |

### [13-jobs/](13-jobs/) -- Job Scheduling

| Sample | Description |
|--------|-------------|
| [CdcJobQuartz](13-jobs/CdcJobQuartz/) | Quartz-scheduled CDC processing job |

## Running Samples

```bash
# Build and run any sample
dotnet run --project samples/01-getting-started/HelloDispatch

# Samples with Docker dependencies
cd samples/09-advanced/SqlServerEventStore
docker-compose up -d    # Start infrastructure
dotnet run              # Run the sample
```

### Prerequisites

- **.NET 9.0 SDK** or later
- **Docker Desktop** for samples marked with "Docker" infrastructure
- Specific cloud SDKs for serverless/cloud samples (documented per sample)

## Converting to Your Own Project

Samples use `ProjectReference` for development convenience. Convert to `PackageReference` for your own projects:

```xml
<!-- Before (sample) -->
<ProjectReference Include="$(DispatchSourceRoot)Excalibur.Dispatch\Excalibur.Dispatch.csproj" />

<!-- After (your project) -->
<PackageReference Include="Excalibur.Dispatch" Version="1.0.0" />
```

See [CONVERSION-GUIDE.md](CONVERSION-GUIDE.md) for complete instructions.

## Related Documentation

- [Framework Documentation](../docs-site/docs/)
- [Event Sourcing Guide](../docs-site/docs/event-sourcing/)
- [API Reference](../docs-site/docs/reference/)
