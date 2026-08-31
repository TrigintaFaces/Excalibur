---
sidebar_position: 6
title: AOT Compatibility Matrix
description: Per-package Native AOT compatibility status for Excalibur
---

# AOT Compatibility Matrix

This page documents the Native AOT compatibility status for every shipping package. Use this as a reference when planning AOT-published applications.

**Summary:** the large majority of shipping packages are AOT-compatible. **The table below is the per-package authority** — read it rather than a headline count. A package marked *Not compatible* has a documented blocking dependency in an external SDK, not in framework code.

For setup instructions and source generator usage, see the [Native AOT Guide](native-aot.md).

---

## How to Read This Matrix

| Status | Meaning |
|--------|---------|
| **AOT-safe** | `IsAotCompatible=true`. Zero IL2xxx/IL3xxx warnings in PublishAot builds. |
| **Annotated** | Contains `[RequiresUnreferencedCode]` or `[RequiresDynamicCode]` on specific methods. Safe to use if you avoid the annotated paths — check the Notes column, because for some packages the annotated path is the only entry point. |
| **Not compatible** | `IsAotCompatible=false`. The package does not claim ahead-of-time compatibility — usually a blocking dependency, sometimes reflection in its own code. |
| **N/A** | Tooling package (analyzer, source generator) — runs at compile time, not at runtime. |

---

## Dispatch Packages

### Core

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch` | Annotated | Source-generated handler resolution via `PrecompiledHandlerRegistry`. Annotated paths: `AddAdaptiveTimeAwareScheduling`, `AddContextValidation`, `AddDefaultDispatchPipelines` and 58 more. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Abstractions` | Annotated | All interfaces and base types are trim-safe. Annotated paths: `AddEventTypesFromAssembly`, `EnqueueAsync`, `GetUnsentMessagesAsync` and 2 more. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Patterns` | Annotated | Annotated paths: `ClaimCheckMessageSerializer`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Patterns.Azure` | AOT-safe | |
| `Excalibur.Dispatch.Patterns.ClaimCheck.InMemory` | AOT-safe | |
| `Excalibur.Dispatch.Patterns.Hosting.Json` | AOT-safe | |

### Middleware and Pipeline

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Caching` | Annotated | `CachingMiddleware` uses `RuntimeFeature.IsDynamicCodeSupported` branching. Analysis reports 14 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Dispatch.Resilience.Polly` | Annotated | Polly v8 is AOT-compatible. Annotated paths: `AddPollyResilience`, `UseResilience`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Validation.FluentValidation` | Annotated | Dual-path: `AotFluentValidatorResolver` + source-gen `IAotValidationDispatcher`. Annotated paths: `WithFluentValidation`. The rest of the surface publishes clean. |

### Serialization

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Serialization.MemoryPack` | Annotated | MemoryPack uses source generation. Annotated paths: `ISerializer`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Serialization.Avro` | **Not compatible** | Apache.Avro uses runtime code generation |
| `Excalibur.Dispatch.Serialization.MessagePack` | **Not compatible** | MessagePack reflection-based resolvers |
| `Excalibur.Dispatch.Serialization.Protobuf` | Annotated | Uses Google.Protobuf (v3.32.1+), which is AOT-compatible. Annotated paths: `ISerializer`. The rest of the surface publishes clean. |

### Transport

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Transport.Abstractions` | AOT-safe | |
| `Excalibur.Dispatch.Transport.RabbitMQ` | Annotated | Builder pattern, no reflection. Annotated paths: `ToTransportMessageAsync`, `AddCloudEventsForRabbitMq`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Transport.AwsSqs` | Annotated | Builder pattern, no reflection. Annotated paths: `AddAwsLongPolling`, `ToBatchSqsMessageAsync`, `ToEventBridgeEventAsync` and 4 more, plus the CloudEvents registrations `UseCloudEvents`, `AddCloudEventsForSqs`, `AddCloudEventsForSns`, `AddCloudEventsForEventBridge`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Transport.AzureServiceBus` | Annotated | `MessageDeserializerRegistry` typed pattern; `AzureLogicAppsScheduler`/`EventGridTransportSender` annotated. Annotated paths: `ToTransportMessageAsync`, plus the CloudEvents registrations `UseCloudEvents`, `AddCloudEventsForServiceBus`, `AddCloudEventsForEventHubs`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Transport.GooglePubSub` | **Not compatible** | Google Cloud SDK dependency uses reflection. `UseCloudEvents` and `AddCloudEventsForPubSub` are separately annotated: the bundled mapper serializes payloads reflectively |
| `Excalibur.Dispatch.Transport.Kafka` | **Not compatible** | Confluent.Kafka SchemaRegistry uses `Activator.CreateInstance`. `AddCloudEventsForKafka` is separately annotated: the bundled mapper serializes payloads reflectively |
| `Excalibur.Dispatch.Transport.Grpc` | AOT-safe | `GrpcJsonSerializerContext` source-gen JSON for all 10 transport types |

### Hosting

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Hosting.AspNetCore` | Annotated | Annotated paths: `AddDispatch`, `public static RouteHandlerBuilder Dispat`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Hosting.AwsLambda` | Annotated | Annotated paths: `AddAwsLambdaServerless`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Hosting.AzureFunctions` | AOT-safe | |
| `Excalibur.Dispatch.Hosting.GoogleCloudFunctions` | AOT-safe | |
| `Excalibur.Dispatch.Hosting.Serverless.Abstractions` | AOT-safe | |

### Observability

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Observability` | Annotated | Uses `System.Diagnostics` (OTel-aligned). Analysis reports 4 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Dispatch.Observability.Aws` | AOT-safe | |

### Security

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Security` | Annotated | Annotated paths: `AddDispatchSecurity`, `AddDispatchSecurityMiddleware`, `AddInputValidation` and 8 more. The rest of the surface publishes clean. |
| `Excalibur.Security.Aws` | **Not compatible** | AWS SDK v3 (`AWSSDK.Core`, `AWSSDK.SecretsManager`) is reflection-based and declares no trim or AOT properties, so the analyzer cannot see its reflection and reports nothing while a native publish fails at link time |
| `Excalibur.Security.Azure` | AOT-safe | |

### Compliance

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Compliance.Abstractions` | AOT-safe | |
| `Excalibur.Compliance` | Annotated | Reflection paths annotated with `[DynamicallyAccessedMembers]`. Annotated paths: `DynamicallyAccessedMembers`, `EnforceRetentionAsync`, `EnqueueAsync` and 3 more. The rest of the surface publishes clean. |
| `Excalibur.Compliance.Aws` | **Not compatible** | AWS KMS SDK dependency |
| `Excalibur.Compliance.Azure` | AOT-safe | |
| `Excalibur.Compliance.Vault` | AOT-safe | |

### Audit Logging

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.AuditLogging` | AOT-safe | |
| `Excalibur.AuditLogging.Aws` | AOT-safe | |
| `Excalibur.AuditLogging.Datadog` | AOT-safe | |
| `Excalibur.AuditLogging.Elasticsearch` | AOT-safe | |
| `Excalibur.AuditLogging.GoogleCloud` | AOT-safe | |
| `Excalibur.AuditLogging.OpenSearch` | AOT-safe | |
| `Excalibur.AuditLogging.Postgres` | AOT-safe | |
| `Excalibur.AuditLogging.Sentinel` | AOT-safe | |
| `Excalibur.AuditLogging.Splunk` | AOT-safe | |
| `Excalibur.AuditLogging.SqlServer` | AOT-safe | |

### Claim Check

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.ClaimCheck.AwsS3` | **Not compatible** | AWS S3 SDK dependency |
| `Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage` | **Not compatible** | Google Cloud Storage SDK dependency |

### Leader Election

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.LeaderElection.Abstractions` | AOT-safe | |

### Testing

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Testing` | Annotated | Annotated paths: `CreateAsyncScope`, `CreateScope`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Testing.Shouldly` | AOT-safe | |

### Tooling (compile-time only)

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.SourceGenerators` | N/A | Roslyn source generator (netstandard2.0) |
| `Excalibur.Dispatch.SourceGenerators.Analyzers` | N/A | Roslyn analyzer (netstandard2.0) |
| `Excalibur.Dispatch.Analyzers` | N/A | Roslyn analyzer (netstandard2.0) |

---

## Excalibur Packages

### Domain and Data Access

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Domain` | Annotated | Annotated paths: `AddBoundedContextEnforcement`, `AddImplementations`, `DefaultBoundedContextValidator` and 3 more. The rest of the surface publishes clean. |
| `Excalibur.Application` | Annotated | Annotated paths: `AddActivities`. The rest of the surface publishes clean. |
| `Excalibur.Data.Abstractions` | AOT-safe | |
| `Excalibur.Data` | Annotated | Annotated paths: `ExcaliburJsonSerializerOptions`. The rest of the surface publishes clean. |
| `Excalibur.Data.InMemory` | Annotated | Analysis reports 4 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Data.SqlServer` | Annotated | Dapper itself is AOT-compatible. The dead-letter store carries **no** annotation: its message property bag is serialized through a source-generated JSON context, so it publishes clean and needs no consumer workaround. Annotated path: `AddSqlServerPersistence`. The rest of the surface publishes clean |
| `Excalibur.Data.Postgres` | Annotated | Dapper itself is AOT-compatible. The dead-letter store carries **no** annotation: its message property bag is serialized through a source-generated JSON context, so it publishes clean and needs no consumer workaround. Annotated paths: the `PostgresPersistenceProvider` constructor, `AddPostgresPersistence`, `AddPostgresPersistenceFromSection`. The rest of the surface publishes clean |
| `Excalibur.Data.MySql` | AOT-safe | |
| `Excalibur.Data.MongoDB` | Annotated | Analysis reports 8 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Data.Redis` | AOT-safe | |
| `Excalibur.Data.ElasticSearch` | Annotated | Analysis reports 76 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Data.DataProcessing` | Annotated | Annotated paths: `AddDataProcessing`, `AddProcessorsFromAssembly`, `AddRecordHandlersFromAssembly` and 1 more. The rest of the surface publishes clean. |
| `Excalibur.Data.CosmosDb` | **Not compatible** | CosmosDB SDK uses `Expression.Compile()` |
| `Excalibur.Data.DynamoDb` | **Not compatible** | DynamoDB SDK reflection-based marshalling |
| `Excalibur.Data.Firestore` | **Not compatible** | Firestore SDK uses reflection |
| `Excalibur.Data.OpenSearch` | **Not compatible** | OpenSearch SDK dependency |

### Event Sourcing

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.EventSourcing.Abstractions` | Annotated | Annotated paths: `public static async Task<CursorPagedResu`, `public static async Task<IReadOnlyList<o`, `public static async Task<PagedResult<TPr` and 1 more. The rest of the surface publishes clean. |
| `Excalibur.EventSourcing` | Annotated | Annotated paths: `AddEventNotificationHandlersFromAssembly`, `AddEventSourcing`, `AddImmutableProjectionHandlersFromAssembly` and 25 more. The rest of the surface publishes clean. |
| `Excalibur.EventSourcing.InMemory` | AOT-safe | |
| `Excalibur.EventSourcing.SqlServer` | Annotated | Analysis reports 28 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.EventSourcing.Postgres` | Annotated | Analysis reports 24 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.EventSourcing.MongoDB` | AOT-safe | Analysis reports 8 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.EventSourcing.Redis` | AOT-safe | Analysis reports 24 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.EventSourcing.Sqlite` | AOT-safe | Analysis reports 8 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.EventSourcing.AwsS3` | AOT-safe | Analysis reports 12 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.EventSourcing.AzureBlob` | AOT-safe | Analysis reports 12 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.EventSourcing.Gcs` | AOT-safe | Analysis reports 12 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.EventSourcing.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |
| `Excalibur.EventSourcing.DynamoDb` | **Not compatible** | DynamoDB SDK dependency |
| `Excalibur.EventSourcing.Firestore` | **Not compatible** | Firestore SDK dependency |

### Outbox

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Outbox` | Annotated | Annotated paths: `AddMultiTransportOutbox`, `AddOutbox`, `DispatchPendingMessagesAsync` and 6 more. The rest of the surface publishes clean. |
| `Excalibur.Outbox.InMemory` | Annotated | Analysis reports 4 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Outbox.SqlServer` | Annotated | Analysis reports 32 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Outbox.Postgres` | Annotated | Annotated paths: `EnqueueAsync`, `GetUnsentMessagesAsync`. The rest of the surface publishes clean. |
| `Excalibur.Outbox.Redis` | Annotated | Annotated paths: `EnqueueAsync`, `GetUnsentMessagesAsync`. The rest of the surface publishes clean. |
| `Excalibur.Outbox.ElasticSearch` | Annotated | Annotated paths: `EnqueueAsync`, `GetUnsentMessagesAsync`. The rest of the surface publishes clean. |
| `Excalibur.Outbox.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |
| `Excalibur.Outbox.DynamoDb` | **Not compatible** | DynamoDB SDK dependency |
| `Excalibur.Outbox.Firestore` | **Not compatible** | Firestore SDK dependency |
| `Excalibur.Outbox.MongoDB` | **Not compatible** | MongoDB driver dependency |

### Inbox

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Inbox` | AOT-safe | |
| `Excalibur.Inbox.InMemory` | AOT-safe | |
| `Excalibur.Inbox.SqlServer` | AOT-safe | Analysis reports 4 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Inbox.Postgres` | AOT-safe | |
| `Excalibur.Inbox.Redis` | AOT-safe | |
| `Excalibur.Inbox.ElasticSearch` | AOT-safe | |
| `Excalibur.Inbox.MongoDB` | AOT-safe | |
| `Excalibur.Inbox.DynamoDb` | **Not compatible** | Ships `Excalibur.Data.DynamoDb`, which is not AOT-compatible |
| `Excalibur.Inbox.Firestore` | **Not compatible** | Ships `Excalibur.Data.Firestore`, which is not AOT-compatible |
| `Excalibur.Inbox.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |

### Saga

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Saga` | Annotated | Source-gen registry population via `IPostConfigureOptions` pattern. Annotated paths: `AddSagas`, `ProcessEventAsync`, the `RequestTimeoutAsync` overload that carries timeout data (the parameterless overload is AOT-safe) and 5 more. The rest of the surface publishes clean. |
| `Excalibur.Saga.SqlServer` | Annotated | Annotated paths: `AddSqlServerSagaStore`, `SaveSagaRequest`, `UseSqlServerSagaStore` and 1 more. The rest of the surface publishes clean. |
| `Excalibur.Saga.Postgres` | Annotated | Annotated paths: `AddPostgresSagaStore`, `SaveSagaRequest`. The rest of the surface publishes clean. |
| `Excalibur.Saga.MongoDB` | AOT-safe | Analysis reports 4 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Saga.DynamoDb` | **Not compatible** | Ships `Excalibur.Data.DynamoDb`, which is not AOT-compatible |
| `Excalibur.Saga.Firestore` | **Not compatible** | Ships `Excalibur.Data.Firestore`, which is not AOT-compatible |
| `Excalibur.Saga.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |

### CDC (Change Data Capture)

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Cdc` | AOT-safe | |
| `Excalibur.Cdc.SqlServer` | Annotated | Annotated paths: `AddDataChangeHandlersFromAssembly`. The rest of the surface publishes clean. |
| `Excalibur.Cdc.Postgres` | AOT-safe | |
| `Excalibur.Cdc.MongoDB` | AOT-safe | |
| `Excalibur.Cdc.DynamoDb` | **Not compatible** | Ships `Excalibur.Data.DynamoDb`, which is not AOT-compatible |
| `Excalibur.Cdc.Firestore` | **Not compatible** | Ships `Excalibur.Data.Firestore`, which is not AOT-compatible |
| `Excalibur.Cdc.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |

### Leader Election

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.LeaderElection` | Annotated | Annotated paths: `AddLeaderElectionWatcher`. The rest of the surface publishes clean. |
| `Excalibur.LeaderElection.InMemory` | AOT-safe | |
| `Excalibur.LeaderElection.SqlServer` | AOT-safe | |
| `Excalibur.LeaderElection.Postgres` | AOT-safe | |
| `Excalibur.LeaderElection.Redis` | AOT-safe | |
| `Excalibur.LeaderElection.MongoDB` | AOT-safe | |
| `Excalibur.LeaderElection.Consul` | **Not compatible** | Consul SDK dependency |
| `Excalibur.LeaderElection.Kubernetes` | **Not compatible** | Kubernetes SDK dependency |

### Hosting

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Hosting` | Annotated | Annotated paths: `AddDispatch`, `AddExcalibur`, `AddExcaliburHealthChecks` and 1 more. The rest of the surface publishes clean. |
| `Excalibur.Hosting.Web` | AOT-safe | |
| `Excalibur.Hosting.Aws` | AOT-safe | |
| `Excalibur.Hosting.AwsLambda` | Annotated | Annotated paths: `AddExcaliburAwsLambdaServerless`. The rest of the surface publishes clean. |
| `Excalibur.Hosting.AzureFunctions` | AOT-safe | |
| `Excalibur.Hosting.GoogleCloudFunctions` | AOT-safe | |
| `Excalibur.Hosting.HealthChecks` | Annotated | Annotated paths: `UseExcaliburHealthChecks`. The rest of the surface publishes clean. |
| `Excalibur.Hosting.Jobs` | **Not compatible** | Ships `Excalibur.Jobs`, which is not AOT-compatible |
| `Excalibur.Hosting.Observability` | AOT-safe | |
| `Excalibur.Hosting.Logging.Serilog` | AOT-safe | |

### A3 (Authentication, Authorization, Audit)

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.A3` | Annotated | Annotated paths: `AddA3DispatchServices`, `AddExcaliburA3`, `ExtractResourceId` and 4 more. The rest of the surface publishes clean. |
| `Excalibur.A3.Abstractions` | AOT-safe | |
| `Excalibur.A3.Core` | AOT-safe | |
| `Excalibur.A3.Governance` | AOT-safe | |
| `Excalibur.A3.Governance.Abstractions` | AOT-safe | |
| `Excalibur.A3.Policy.Cedar` | AOT-safe | |
| `Excalibur.A3.Policy.Opa` | AOT-safe | |

### Security and Compliance

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Security.Abstractions` | AOT-safe | |
| `Excalibur.Security` | Annotated | `[DynamicallyAccessedMembers]` annotations for property-level encryption. Annotated paths: `AddDispatchSecurity`, `AddDispatchSecurityMiddleware`, `AddInputValidation` and 8 more. The rest of the surface publishes clean. |
| `Excalibur.Compliance.SqlServer` | AOT-safe | |
| `Excalibur.Compliance.Postgres` | AOT-safe | |
| `Excalibur.Caching` | **Not compatible** | HybridCache uses reflection |

### Jobs

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Jobs` | **Not compatible** | The projection-rebuild and outbox jobs drive reflective paths; the requirement is annotated at the members that do it |
| `Excalibur.Jobs.Abstractions` | AOT-safe | |
| `Excalibur.Jobs.Aws` | AOT-safe | |
| `Excalibur.Jobs.Azure` | AOT-safe | |
| `Excalibur.Jobs.Cdc` | **Not compatible** | Ships `Excalibur.Jobs`, which is not AOT-compatible |
| `Excalibur.Jobs.DataProcessing` | **Not compatible** | Ships `Excalibur.Jobs`, which is not AOT-compatible |
| `Excalibur.Jobs.GoogleCloud` | AOT-safe | |
| `Excalibur.Jobs.Redis` | Annotated | Annotated paths: `DistributeJobAsync`, `ReportJobCompletionAsync`. The rest of the surface publishes clean. |
| `Excalibur.Jobs.SqlServer` | **Not compatible** | Ships `Excalibur.Jobs`, which is not AOT-compatible |

### Testing

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Testing` | AOT-safe | |
| `Excalibur.Testing.Conformance` | **Not compatible** | Test kits that read stored data back through the store under test, which deserializes reflectively. A trimmed test host is not a supported configuration |

### Tools

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Migrate.Tool` | AOT-safe | |

---

## Metapackages

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.RabbitMQ` | Annotated | The single entry point is annotated, so there is no unannotated path to avoid |
| `Excalibur.Dispatch.Aws` | Annotated | The single entry point is annotated, so there is no unannotated path to avoid |
| `Excalibur.Dispatch.Azure` | Annotated | The single entry point is annotated, so there is no unannotated path to avoid |
| `Excalibur.Dispatch.Kafka` | **Not compatible** | Ships `Excalibur.Dispatch.Transport.Kafka`, which is not AOT-compatible |
| `Excalibur.Dispatch.Postgres` | Annotated | Annotated paths: `AddDispatchWithPostgres`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.SqlServer` | Annotated | Registers an outbox, which serializes payloads reflectively. The single entry point is annotated, so there is no unannotated path to avoid |
| `Excalibur.Postgres` | Annotated | Annotated paths: `AddExcaliburPostgres`. The rest of the surface publishes clean. |
| `Excalibur.SqlServer` | Annotated | Registers an outbox, which serializes payloads reflectively. Both entry points are annotated, so there is no unannotated path to avoid |

---

## Blocking Dependencies

These third-party dependencies prevent AOT compatibility in the affected packages:

| Dependency | Blocking Reason | Affected Packages |
|------------|----------------|-------------------|
| Azure CosmosDB SDK | `Expression.Compile()` in LINQ provider | CosmosDb data, event sourcing, outbox, inbox, saga, CDC |
| AWS DynamoDB SDK | Reflection-based marshalling | DynamoDb data, event sourcing, outbox |
| Google Firestore SDK | Reflection-based serialization | Firestore data, event sourcing, outbox |
| Confluent.Kafka | `Activator.CreateInstance` for schema strategies | Kafka transport |
| FluentValidation | `Expression.Compile()` for validators | FluentValidation middleware |
| Apache.Avro | Runtime code generation | Avro serialization |
| MessagePack-CSharp | Reflection-based resolvers | MessagePack serialization |
| protobuf-net | `Expression.Compile()` | *(none currently — `Excalibur.Dispatch.Serialization.Protobuf` uses Google.Protobuf, which is AOT-safe)* |
| OpenSearch SDK | Reflection-based serialization | OpenSearch data |
| Consul SDK | Reflection-based HTTP client | Consul leader election |
| Kubernetes SDK | Reflection-based client | Kubernetes leader election |

When these dependencies release AOT-compatible versions, the affected packages will be updated.

---

## Consumer Checklist

To publish an AOT application with Excalibur:

1. **Verify all referenced packages are AOT-safe** using the matrix above
2. **Add source generators** to your project:
   ```xml
   <PackageReference Include="Excalibur.Dispatch.SourceGenerators" />
   <PackageReference Include="Excalibur.Dispatch.SourceGenerators.Analyzers" />
   ```
3. **Create a `JsonSerializerContext`** for your application types (see [Native AOT Guide](native-aot.md#json-serialization))
4. **Mark handlers with `[AutoRegister]`** for compile-time DI registration
5. **Publish with AOT**:
   ```bash
   dotnet publish -c Release
   ```
6. **Check for warnings**: Zero IL2xxx/IL3xxx warnings means you are fully AOT-safe

If you must use an AOT-incompatible package, the `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` attributes on your entry points will propagate warnings to callers, enabling informed decisions.


## Additional Packages

These packages ship from this repository and were absent from the matrix above. Status is read
directly from each project's `IsAotCompatible` property, the same source the sections above use.

| Package | AOT Status | Notes |
|---------|------------|-------|
| `Excalibur.Dispatch.Compat.MassTransit` | AOT-safe |  |
| `Excalibur.Dispatch.Compat.MediatR` | Annotated | Annotated paths: `AddMediatRCompat`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Migration` | **Not compatible** |  |
| `Excalibur.Dispatch.Transport.IbmMq` | **Not compatible** | NOT compatible. The IBM MQ managed client uses runtime reflection and dynamic assembly loading. |
| `Excalibur.Dispatch.Transport.Mqtt` | AOT-safe |  |
| `Excalibur.Dispatch.Transport.Pulsar` | AOT-safe |  |
| `Excalibur.AuditLogging.Abstractions` | AOT-safe |  |
| `Excalibur.Compliance.MongoDb` | AOT-safe |  |
| `Excalibur.Compliance.Pdf` | **Not compatible** |  |
| `Excalibur.Data.IdentityMap.SqlServer` | AOT-safe |  |
| `Excalibur.Data.IdentityMap` | AOT-safe |  |
| `Excalibur.Data.Spanner` | **Not compatible** | NOT compatible. Google.Cloud.Spanner.Data uses gRPC + reflection-based value conversion. |
| `Excalibur.EventSourcing.Handlers` | AOT-safe |  |
| `Excalibur.EventSourcing.Oracle` | AOT-safe | Analysis reports 16 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Hosting.Compliance` | AOT-safe |  |
| `Excalibur.Inbox.Oracle` | AOT-safe | Analysis reports 4 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.MultiTenancy` | **Not compatible** | Row-discriminator decoration of open-generic IProjectionStore&lt;T&gt; uses reflective MakeGenericType over the DI descriptor set, so this composition |
| `Excalibur.Operations.Dashboard.EventSourcing` | AOT-safe |  |
| `Excalibur.Operations.Dashboard.Spa` | AOT-safe |  |
| `Excalibur.Operations.Dashboard` | AOT-safe |  |
| `Excalibur.Outbox.Marten` | **Not compatible** |  |
| `Excalibur.Outbox.Oracle` | Annotated | Annotated paths: `EnqueueAsync`, `GetUnsentMessagesAsync`. The rest of the surface publishes clean. |
| `Excalibur.Saga.Oracle` | Annotated | Annotated paths: `AddOracleSagaStore`, `SaveSagaRequest`, `UseOracleSagaStore` and 1 more. The rest of the surface publishes clean. |
| `Excalibur.Security.AuditLogging` | AOT-safe |  |
| `Excalibur.Testing.Containers` | **Not compatible** | Fixtures run under a test host, not a trimmed consumer app; reflection in Testcontainers is acceptable here. |
| `Excalibur.Workflows.Abstractions` | AOT-safe |  |
| `Excalibur.Workflows.Analyzers` | **Not compatible** | Disable AOT analysis - not applicable to analyzers (run in compiler, not runtime) |
| `Excalibur.Workflows.CodeFixes` | **Not compatible** |  |
| `Excalibur.Workflows.SqlServer` | AOT-safe |  |
| `Excalibur.Workflows` | AOT-safe |  |
| `Excalibur.Dispatch.AspNetCore` | Annotated | Annotated paths: `AddDispatchAspNetCore`. The rest of the surface publishes clean. |

---

## Related Documentation

- [Native AOT Guide](native-aot.md) - Setup, source generators, and troubleshooting
- [Source Generators](source-generators.md) - Full generator reference
- [Package Guide](../package-guide.md) - Package selection guide
