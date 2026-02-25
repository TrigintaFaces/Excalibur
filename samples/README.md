# Excalibur Samples

This directory contains working samples demonstrating the Dispatch and Excalibur frameworks.

## Getting Started

**New to Dispatch?** Start with these foundational samples:

| Sample | Description |
|--------|-------------|
| [GettingStarted](01-getting-started/GettingStarted/) | ASP.NET Core API with commands, queries, and events |
| [DispatchMinimal](01-getting-started/DispatchMinimal/) | Console app showing pure Dispatch messaging patterns |
| [ExcaliburCqrs](01-getting-started/ExcaliburCqrs/) | Full CQRS with aggregate roots and event sourcing |

## Sample Categories

Samples are organized into numbered categories for easy discovery:

### [01-getting-started/](01-getting-started/)

Foundational samples for learning Dispatch and Excalibur basics.

| Sample | Description |
|--------|-------------|
| [GettingStarted](01-getting-started/GettingStarted/) | ASP.NET Core API demonstrating commands, queries, events, and `[AutoRegister]` |
| [DispatchMinimal](01-getting-started/DispatchMinimal/) | Lightweight Dispatch-only console app with custom middleware |
| [ExcaliburCqrs](01-getting-started/ExcaliburCqrs/) | CQRS pattern with Excalibur domain modeling |
| [MinimalSample](01-getting-started/MinimalSample/) | Simplest possible Dispatch setup |
| [QuickDemo](01-getting-started/QuickDemo/) | Rapid demonstration of core concepts |

### [02-messaging-transports/](02-messaging-transports/)

Transport configuration, multi-bus scenarios, and cross-provider messaging.

**Dedicated Transport Samples** (with Docker Compose for local development):

| Sample | Transport | Description |
|--------|-----------|-------------|
| [RabbitMQ](02-messaging-transports/RabbitMQ/) | RabbitMQ 3.12 | Topic exchange routing, CloudEvents |
| [Kafka](02-messaging-transports/Kafka/) | Kafka 7.5 (KRaft) | Consumer groups, partitioning, compression |
| [AzureServiceBus](02-messaging-transports/AzureServiceBus/) | Azure Service Bus | Queues, topics, sessions |
| [AwsSqs](02-messaging-transports/AwsSqs/) | AWS SQS | Standard/FIFO queues, DLQ (LocalStack) |

**Multi-Transport Samples:**

| Sample | Description |
|--------|-------------|
| [MultiTransport](02-messaging-transports/MultiTransport/) | Using multiple transports in a single application |
| [MultiBusSample](02-messaging-transports/MultiBusSample/) | RabbitMQ + Kafka multi-bus configuration |
| [RemoteBusSample](02-messaging-transports/RemoteBusSample/) | RabbitMQ remote bus with command/event handlers |
| [TransportBindings](02-messaging-transports/TransportBindings/) | Transport binding configuration examples |
| [MultiProviderQueueProcessor](02-messaging-transports/MultiProviderQueueProcessor/) | Processing messages from multiple providers |

### [03-cloud-native/](03-cloud-native/)

Cloud-native patterns and integrations.

| Sample | Description |
|--------|-------------|
| [CloudNativePatterns.Examples](03-cloud-native/CloudNativePatterns.Examples/) | Claim check pattern and cloud-native messaging |

### [04-reliability/](04-reliability/)

Reliability patterns for distributed systems.

| Sample | Pattern | Local Dev |
|--------|---------|-----------|
| [OutboxPattern](04-reliability/OutboxPattern/) | Transactional outbox, guaranteed delivery | Yes - in-memory |
| [RetryAndCircuitBreaker](04-reliability/RetryAndCircuitBreaker/) | Polly resilience patterns | Yes - no dependencies |
| [SagaOrchestration](04-reliability/SagaOrchestration/) | Distributed transaction coordination | Yes - in-memory |

### [05-serverless/](05-serverless/)

Serverless hosting patterns for cloud platforms.

| Sample | Platform | Triggers | Local Dev |
|--------|----------|----------|-----------|
| [AzureFunctions](05-serverless/AzureFunctions/) | Azure Functions v4 | HTTP, Queue Storage, Timer | `func start` |
| [AwsLambda](05-serverless/AwsLambda/) | AWS Lambda (.NET 8) | API Gateway, SQS, EventBridge | `sam local start-api` |
| [GoogleCloudFunctions](05-serverless/GoogleCloudFunctions/) | GCF Gen2 (.NET 8) | HTTP, Pub/Sub, Cloud Scheduler | `dotnet run` |

### [06-security/](06-security/)

Security and compliance patterns for enterprise applications.

| Sample | Pattern | Local Dev |
|--------|---------|-----------|
| [MessageEncryption](06-security/MessageEncryption/) | Field-level encryption, PCI compliance | Yes - DataProtection |
| [AuditLogging](06-security/AuditLogging/) | SOC2/HIPAA/GDPR compliance logging | Yes - in-memory |
| [AzureKeyVault](06-security/AzureKeyVault/) | ICredentialStore, secret caching | Requires Azure |
| [AwsSecretsManager](06-security/AwsSecretsManager/) | Secret retrieval, IAM auth | Yes - LocalStack |

### [07-observability/](07-observability/)

Logging, tracing, and metrics for production monitoring.

| Sample | Pattern | Local Dev |
|--------|---------|-----------|
| [OpenTelemetry](07-observability/OpenTelemetry/) | Distributed tracing, custom spans, metrics | Yes - Jaeger Docker |
| [HealthChecks](07-observability/HealthChecks/) | Kubernetes liveness/readiness probes | Yes - no dependencies |

### [08-serialization/](08-serialization/)

High-performance serialization alternatives.

| Sample | Serializer | Best For |
|--------|------------|----------|
| [Protobuf](08-serialization/Protobuf/) | Protocol Buffers | Cross-language systems |
| [MessagePack](08-serialization/MessagePackSample/) | MessagePack + LZ4 | High throughput |
| [MemoryPack](08-serialization/MemoryPackSample/) | MemoryPack | Maximum .NET performance |

### [09-advanced/](09-advanced/)

Advanced patterns including distributed coordination, validation, projections, streaming, and event sourcing.

**Sprint 436 Samples (Streaming Handlers):**

| Sample | Description | Key Technologies |
|--------|-------------|------------------|
| [StreamingHandlers](09-advanced/StreamingHandlers/) | All streaming handler patterns | IAsyncEnumerable, backpressure, progress |

**Sprint 434 Samples:**

| Sample | Description | Key Technologies |
|--------|-------------|------------------|
| [LeaderElection](09-advanced/LeaderElection/) | Distributed leader election | Redis, TTL leases, callbacks |
| [FluentValidationSample](09-advanced/FluentValidationSample/) | Pipeline validation integration | FluentValidation, Middleware |
| [ProjectionsSample](09-advanced/ProjectionsSample/) | CQRS read model generation | Checkpoint tracking, rebuild |

**Event Sourcing Providers:**

| Sample | Description | Key Technologies |
|--------|-------------|------------------|
| [SqlServerEventStore](09-advanced/SqlServerEventStore/) | SQL Server event persistence | Dapper, Docker, Transactions |
| [CosmosDbEventStore](09-advanced/CosmosDbEventStore/) | Cosmos DB with partition strategies | Azure SDK, Change Feed |
| [SnapshotStrategies](09-advanced/SnapshotStrategies/) | Aggregate snapshot optimization | Interval, Time, Size, Composite |
| [EventUpcasting](09-advanced/EventUpcasting/) | Event schema evolution (V1->V2->V3) | BFS Path Finding, Auto-Upcast |

**Other Advanced Patterns:**

| Sample | Description |
|--------|-------------|
| [BackgroundServices](09-advanced/BackgroundServices/) | Various background service patterns |
| [CdcAntiCorruption](09-advanced/CdcAntiCorruption/) | Anti-corruption layer for CDC integration |
| [CdcEventStoreElasticsearch](09-advanced/CdcEventStoreElasticsearch/) | CDC with Elasticsearch event store |
| [Consolidated](09-advanced/Consolidated/) | Multiple patterns combined in one application |
| [JobWorkerSample](09-advanced/JobWorkerSample/) | Job worker pattern with multiple job types |
| [CrossLangSample](09-advanced/CrossLangSample/) | Cross-language messaging (Python, JavaScript) |
| [DistributedScheduling](09-advanced/DistributedScheduling/) | Distributed scheduling examples |
| [PipelineProfiles](09-advanced/PipelineProfiles/) | Named pipeline profiles for per-message-type middleware |
| [SessionManagement](09-advanced/SessionManagement/) | Session management and state tracking |
| [Versioning.Examples](09-advanced/Versioning.Examples/) | Event versioning and upcasting patterns |

### [10-real-world/](10-real-world/)

Production-style samples demonstrating how multiple patterns work together.

| Sample | Description | Patterns |
|--------|-------------|----------|
| [OrderProcessing](10-real-world/OrderProcessing/) | Complete order workflow | Event Sourcing, CQRS, Saga, Retry |
| [ECommerce](10-real-world/ECommerce/) | E-commerce order processing | Hosted Services, Health Checks |
| [EnhancedStores](10-real-world/EnhancedStores/) | Enhanced store patterns | Repository, Persistence |

### [10-jobs/](13-jobs/)

Job scheduling and background task patterns.

| Sample | Description |
|--------|-------------|
| [CdcJobQuartz](13-jobs/CdcJobQuartz/) | Quartz-scheduled CDC processing job |

### [11-aot/](11-aot/)

Native AOT compilation samples.

| Sample | Description |
|--------|-------------|
| [Excalibur.Dispatch.Aot.Sample](11-aot/Excalibur.Dispatch.Aot.Sample/) | AOT-compatible Dispatch application |

### [12-vertical-slice-api/](12-vertical-slice-api/)

Vertical slice architecture samples.

| Sample | Description |
|--------|-------------|
| [HealthcareApi](12-vertical-slice-api/HealthcareApi/) | Healthcare API using vertical slice architecture |

---

## Running Samples

All samples build with `dotnet build` and run with `dotnet run`:

```bash
# Build a specific sample
dotnet build samples/01-getting-started/GettingStarted

# Run a specific sample
dotnet run --project samples/01-getting-started/GettingStarted
```

### Prerequisites

- .NET 9.0 SDK or later
- For transport samples: Docker (for RabbitMQ, Kafka containers)

## Converting to Your Own Project

Samples use `ProjectReference` for development convenience. To use them in your own projects, convert to `PackageReference`.

See [CONVERSION-GUIDE.md](CONVERSION-GUIDE.md) for step-by-step instructions.

### Quick Conversion Example

**Before (Sample):**
```xml
<ProjectReference Include="$(DispatchSourceRoot)Dispatch\Excalibur.Dispatch.csproj" />
```

**After (Your Project):**
```xml
<PackageReference Include="Dispatch" Version="1.0.0" />
```

## Sample Structure

Each sample follows a consistent structure:

```
SampleName/
├── SampleName.csproj   # Project file
├── README.md           # Documentation
├── Program.cs          # Entry point
├── Messages/           # Commands, queries, events
├── Handlers/           # Message handlers
└── ...                 # Sample-specific folders
```

## Contributing Samples

When adding new samples:

1. Place in the appropriate numbered category
2. Include a comprehensive README.md with:
   - What the sample demonstrates
   - Prerequisites
   - How to run
   - Code highlights
   - Next steps
3. Ensure the sample builds and runs with no external dependencies (or document them clearly)
4. Follow the naming conventions of existing samples

## Related Documentation

- [Main Documentation](../docs-site/docs/)
- [Source Generators Guide](../docs-site/docs/source-generators/getting-started.md)
- [API Reference](../docs-site/docs/reference/)

---

*Updated: Sprint 581 - Release Readiness*

