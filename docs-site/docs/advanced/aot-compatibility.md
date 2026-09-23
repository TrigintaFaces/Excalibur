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

The column answers one question: **if you publish ahead-of-time, what will your build tell you?**

| Status | What it means for your build | What to do |
|--------|------------------------------|------------|
| **Clean** | No reflection we are aware of. Your publish is silent, and the silence is earned. | Nothing. |
| **Warns you** | Reflection is present and **annotated**, so the compiler flags the exact call **at your call site**. | Read the warning, use the alternative it names, or suppress it deliberately in your own code where your team can review it. |
| **Doesn't warn you** | Reflection is present, but it is internal and **suppressed here**, so **your build will say nothing about it**. | Your compiler cannot help you for this package. Publish an ahead-of-time binary and exercise these paths before you ship. |
| **Not compatible** | `IsAotCompatible=false`. The package does not claim ahead-of-time compatibility — usually a blocking dependency, sometimes reflection in its own code. |
| **N/A** | Tooling package (analyzer, source generator) — runs at compile time, not at runtime. |

---

## Dispatch Packages

### Core

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch` | Warns you | Source-generated handler resolution via `PrecompiledHandlerRegistry`. Annotated paths: `AddAdaptiveTimeAwareScheduling`, `AddContextValidation`, `AddDefaultDispatchPipelines` and 58 more. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Abstractions` | Warns you | All interfaces and base types are trim-safe. Annotated paths: `AddEventTypesFromAssembly`, `EnqueueAsync`, `GetUnsentMessagesAsync` and 2 more. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Patterns` | Warns you | Annotated paths: `ClaimCheckMessageSerializer`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Patterns.Azure` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddAzureBlobClaimCheck`, which reflects over the options type. The overload that takes its options in code publishes clean. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Patterns.ClaimCheck.InMemory` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddInMemoryClaimCheck`, which reflects over the options type. The overload that takes its options in code publishes clean. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Patterns.Hosting.Json` | Doesn't warn you | |

### Middleware and Pipeline

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Caching` | Warns you | `CachingMiddleware` uses `RuntimeFeature.IsDynamicCodeSupported` branching. Analysis reports 14 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Dispatch.Resilience.Polly` | Warns you | Polly v8 is AOT-compatible. Annotated paths: `AddPollyResilience`, `UseResilience`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Validation.FluentValidation` | Warns you | Dual-path: `AotFluentValidatorResolver` + source-gen `IAotValidationDispatcher`. Annotated paths: `WithFluentValidation`. The rest of the surface publishes clean. |

### Serialization

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Serialization.MemoryPack` | Warns you | MemoryPack uses source generation. Annotated paths: `ISerializer`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Serialization.Avro` | **Not compatible** | Apache.Avro uses runtime code generation |
| `Excalibur.Dispatch.Serialization.MessagePack` | **Not compatible** | MessagePack reflection-based resolvers |
| `Excalibur.Dispatch.Serialization.Protobuf` | Warns you | Uses Google.Protobuf (v3.32.1+), which is AOT-compatible. Annotated paths: `ISerializer`. The rest of the surface publishes clean. |

### Transport

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Transport.Abstractions` | Warns you | `CloudEventEncoderAdapter<TOutbound>` forwards to the registered `ICloudEventEncoder<TOutbound>`, which may serialize reflectively. Annotated paths: `ToTransportAsync`. Each transport's own `UseCloudEvents`/`AddCloudEventsForX` registration carries the same annotation, so the signal reaches a consumer who opts into CloudEvents. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Transport.RabbitMQ` | Warns you | Builder pattern, no reflection. Annotated paths: `ToTransportMessageAsync`, `AddCloudEventsForRabbitMq`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Transport.AwsSqs` | Warns you | Builder pattern, no reflection. Annotated paths: `ToBatchSqsMessageAsync`, `ToEventBridgeEventAsync` and 4 more, plus the CloudEvents registrations `UseCloudEvents`, `AddCloudEventsForSqs`, `AddCloudEventsForSns`, `AddCloudEventsForEventBridge`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Transport.AzureServiceBus` | Warns you | `MessageDeserializerRegistry` typed pattern; `EventGridTransportSender` annotated. Annotated paths: `ToTransportMessageAsync`, plus the CloudEvents registrations `UseCloudEvents`, `AddCloudEventsForServiceBus`, `AddCloudEventsForEventHubs`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Transport.GooglePubSub` | **Not compatible** | Google Cloud SDK dependency uses reflection. `UseCloudEvents` and `AddCloudEventsForPubSub` are separately annotated: the bundled mapper serializes payloads reflectively |
| `Excalibur.Dispatch.Transport.Kafka` | **Not compatible** | Confluent.Kafka SchemaRegistry uses `Activator.CreateInstance`. `AddCloudEventsForKafka` is separately annotated: the bundled mapper serializes payloads reflectively |
| `Excalibur.Dispatch.Transport.Grpc` | Doesn't warn you | `GrpcJsonSerializerContext` source-gen JSON for all 10 transport types |

### Hosting

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Hosting.AspNetCore` | Warns you | Annotated paths: `AddDispatch`, `public static RouteHandlerBuilder Dispat`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Hosting.AwsLambda` | Warns you | Annotated paths: `AddAwsLambdaServerless`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Hosting.AzureFunctions` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddAzureFunctionsServerless`, which reflects over the options type. The overload that takes its options in code publishes clean. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Hosting.GoogleCloudFunctions` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddGoogleCloudFunctionsServerless`, which reflects over the options type. The overload that takes its options in code publishes clean. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Hosting.Serverless.Abstractions` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddServerlessHosting`, which reflects over the options type. The overload that takes its options in code publishes clean. The rest of the surface publishes clean. |

### Observability

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Observability` | Warns you | Uses `System.Diagnostics` (OTel-aligned). Analysis reports 4 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Dispatch.Observability.Aws` | Clean | |

### Security

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Security` | Warns you | Annotated paths: `AddDispatchSecurity`, `AddDispatchSecurityMiddleware`, `AddInputValidation` and 8 more. The rest of the surface publishes clean. |
| `Excalibur.Security.Aws` | **Not compatible** | AWS SDK v3 (`AWSSDK.Core`, `AWSSDK.SecretsManager`) is reflection-based and declares no trim or AOT properties, so the analyzer cannot see its reflection and reports nothing while a native publish fails at link time |
| `Excalibur.Security.Azure` | Clean | |

### Compliance

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Compliance.Abstractions` | Clean | |
| `Excalibur.Compliance` | Warns you | Reflection paths annotated with `[DynamicallyAccessedMembers]`. Annotated paths: `DynamicallyAccessedMembers`, `EnforceRetentionAsync`, `EnqueueAsync` and 3 more. The rest of the surface publishes clean. |
| `Excalibur.Compliance.Aws` | **Not compatible** | AWS KMS SDK dependency |
| `Excalibur.Compliance.Azure` | Doesn't warn you | |
| `Excalibur.Compliance.Vault` | Doesn't warn you | |

### Audit Logging

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.AuditLogging` | Doesn't warn you | |
| `Excalibur.AuditLogging.Aws` | Doesn't warn you | |
| `Excalibur.AuditLogging.Datadog` | Doesn't warn you | |
| `Excalibur.AuditLogging.Elasticsearch` | Doesn't warn you | |
| `Excalibur.AuditLogging.GoogleCloud` | Doesn't warn you | |
| `Excalibur.AuditLogging.OpenSearch` | Doesn't warn you | |
| `Excalibur.AuditLogging.Postgres` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddPostgresAuditStore`, which reflects over the options type. The overload that takes its options in code publishes clean. The rest of the surface publishes clean. |
| `Excalibur.AuditLogging.Sentinel` | Doesn't warn you | |
| `Excalibur.AuditLogging.Splunk` | Doesn't warn you | |
| `Excalibur.AuditLogging.SqlServer` | Doesn't warn you | |

### Claim Check

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.ClaimCheck.AwsS3` | **Not compatible** | AWS S3 SDK dependency |
| `Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage` | **Not compatible** | Google Cloud Storage SDK dependency |

### Leader Election

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.LeaderElection.Abstractions` | Clean | |

### Testing

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Testing` | Warns you | Annotated paths: `CreateAsyncScope`, `CreateScope`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Testing.Shouldly` | Clean | |

### Tooling (compile-time only)

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch` (bundled generator) | N/A | Roslyn source generator (netstandard2.0) |
| `Excalibur.Dispatch` (bundled analyzer) | N/A | Roslyn analyzer (netstandard2.0) |
| `Excalibur.Dispatch.Analyzers` | N/A | Roslyn analyzer (netstandard2.0) |

---

## Excalibur Packages

### Domain and Data Access

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Domain` | Warns you | Annotated paths: `AddBoundedContextEnforcement`, `AddImplementations`, `DefaultBoundedContextValidator` and 3 more. The rest of the surface publishes clean. |
| `Excalibur.Application` | Warns you | Annotated paths: `AddActivities`. The rest of the surface publishes clean. |
| `Excalibur.Data.Abstractions` | Clean | |
| `Excalibur.Data` | Warns you | Annotated paths: `ExcaliburJsonSerializerOptions`. The rest of the surface publishes clean. |
| `Excalibur.Data.InMemory` | Warns you | Analysis reports 4 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Data.SqlServer` | Warns you | Dapper itself is AOT-compatible. The dead-letter store carries **no** annotation: its message property bag is serialized through a source-generated JSON context, so it publishes clean and needs no consumer workaround. Annotated path: `AddSqlServerPersistence`. The rest of the surface publishes clean |
| `Excalibur.Data.Postgres` | Warns you | Dapper itself is AOT-compatible. The dead-letter store carries **no** annotation: its message property bag is serialized through a source-generated JSON context, so it publishes clean and needs no consumer workaround. Annotated paths: the `PostgresPersistenceProvider` constructor, `AddPostgresPersistence`, `AddPostgresPersistenceFromSection`. The rest of the surface publishes clean |
| `Excalibur.Data.MySql` | Doesn't warn you | |
| `Excalibur.Data.MongoDB` | Warns you | Analysis reports 8 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Data.Redis` | Doesn't warn you | |
| `Excalibur.Data.ElasticSearch` | Warns you | Analysis reports 76 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Data.DataProcessing` | Warns you | Annotated paths: `AddDataProcessing`, `AddProcessorsFromAssembly`, `AddRecordHandlersFromAssembly` and 1 more. The rest of the surface publishes clean. |
| `Excalibur.Data.CosmosDb` | **Not compatible** | CosmosDB SDK uses `Expression.Compile()` |
| `Excalibur.Data.DynamoDb` | **Not compatible** | DynamoDB SDK reflection-based marshalling |
| `Excalibur.Data.Firestore` | **Not compatible** | Firestore SDK uses reflection |
| `Excalibur.Data.OpenSearch` | **Not compatible** | OpenSearch SDK dependency |

### Event Sourcing

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.EventSourcing.Abstractions` | Warns you | Annotated paths: `public static async Task<CursorPagedResu`, `public static async Task<IReadOnlyList<o`, `public static async Task<PagedResult<TPr` and 1 more. The rest of the surface publishes clean. |
| `Excalibur.EventSourcing` | Warns you | Annotated paths: `AddEventNotificationHandlersFromAssembly`, `AddEventSourcing`, `AddImmutableProjectionHandlersFromAssembly` and 25 more. The rest of the surface publishes clean. |
| `Excalibur.EventSourcing.InMemory` | Doesn't warn you | |
| `Excalibur.EventSourcing.SqlServer` | Warns you | Analysis reports 28 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.EventSourcing.Postgres` | Warns you | Analysis reports 24 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.EventSourcing.MongoDB` | Doesn't warn you | Analysis reports 8 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.EventSourcing.Redis` | Doesn't warn you | Analysis reports 24 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.EventSourcing.Sqlite` | Warns you | Annotated path: the `IConfiguration`-binding overload of `UseSqlite`, which reflects over the options type. The overload that takes its options in code publishes clean. The rest of the surface publishes clean. |
| `Excalibur.EventSourcing.AwsS3` | Doesn't warn you | Analysis reports 12 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.EventSourcing.AzureBlob` | Doesn't warn you | Analysis reports 12 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.EventSourcing.Gcs` | Doesn't warn you | Analysis reports 12 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.EventSourcing.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |
| `Excalibur.EventSourcing.DynamoDb` | **Not compatible** | DynamoDB SDK dependency |
| `Excalibur.EventSourcing.Firestore` | **Not compatible** | Firestore SDK dependency |

### Outbox

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Outbox` | Warns you | Annotated paths: `AddMultiTransportOutbox`, `AddOutbox`, `DispatchPendingMessagesAsync` and 6 more. The rest of the surface publishes clean. |
| `Excalibur.Outbox.InMemory` | Warns you | Analysis reports 4 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Outbox.SqlServer` | Warns you | Analysis reports 32 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Outbox.Postgres` | Warns you | Annotated paths: `EnqueueAsync`, `GetUnsentMessagesAsync`. The rest of the surface publishes clean. |
| `Excalibur.Outbox.Redis` | Warns you | Annotated paths: `EnqueueAsync`, `GetUnsentMessagesAsync`. The rest of the surface publishes clean. |
| `Excalibur.Outbox.ElasticSearch` | Warns you | Annotated paths: `EnqueueAsync`, `GetUnsentMessagesAsync`. The rest of the surface publishes clean. |
| `Excalibur.Outbox.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |
| `Excalibur.Outbox.DynamoDb` | **Not compatible** | DynamoDB SDK dependency |
| `Excalibur.Outbox.Firestore` | **Not compatible** | Firestore SDK dependency |
| `Excalibur.Outbox.MongoDB` | **Not compatible** | MongoDB driver dependency |

### Inbox

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Inbox` | Clean | |
| `Excalibur.Inbox.InMemory` | Clean | |
| `Excalibur.Inbox.SqlServer` | Doesn't warn you | `UseSqlServer` no longer binds configuration for you, so it publishes clean. To read options from configuration, call `services.AddOptions<SqlServerInboxOptions>().BindConfiguration("Section:Path")` alongside the registration — binding is reflective, so the trim/AOT warning belongs at your own call site where the trimmer can see it. |
| `Excalibur.Inbox.Postgres` | Doesn't warn you | |
| `Excalibur.Inbox.Redis` | Doesn't warn you | |
| `Excalibur.Inbox.ElasticSearch` | Doesn't warn you | |
| `Excalibur.Inbox.MongoDB` | Doesn't warn you | |
| `Excalibur.Inbox.DynamoDb` | **Not compatible** | Ships `Excalibur.Data.DynamoDb`, which is not AOT-compatible |
| `Excalibur.Inbox.Firestore` | **Not compatible** | Ships `Excalibur.Data.Firestore`, which is not AOT-compatible |
| `Excalibur.Inbox.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |

### Saga

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Saga` | Warns you | Source-gen registry population via `IPostConfigureOptions` pattern. Annotated paths: `AddSagas`, `ProcessEventAsync`, the `RequestTimeoutAsync` overload that carries timeout data (the parameterless overload is AOT-safe) and 5 more. The rest of the surface publishes clean. |
| `Excalibur.Saga.SqlServer` | Warns you | Annotated paths: `AddSqlServerSagaStore`, `SaveSagaRequest`, `UseSqlServerSagaStore` and 1 more. The rest of the surface publishes clean. |
| `Excalibur.Saga.Postgres` | Warns you | Annotated paths: `AddPostgresSagaStore`, `SaveSagaRequest`. The rest of the surface publishes clean. |
| `Excalibur.Saga.MongoDB` | Warns you | Annotated path: `SaveAsync` — saga state is serialized with a reflection-based serializer. The rest of the surface publishes clean. |
| `Excalibur.Saga.DynamoDb` | **Not compatible** | Ships `Excalibur.Data.DynamoDb`, which is not AOT-compatible |
| `Excalibur.Saga.Firestore` | **Not compatible** | Ships `Excalibur.Data.Firestore`, which is not AOT-compatible |
| `Excalibur.Saga.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |

### CDC (Change Data Capture)

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Cdc` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddCdcHealthCheck`, which reflects over the options type. The overload that takes its options in code publishes clean. The rest of the surface publishes clean. |
| `Excalibur.Cdc.SqlServer` | Warns you | Annotated paths: `AddDataChangeHandlersFromAssembly`. The rest of the surface publishes clean. |
| `Excalibur.Cdc.Postgres` | Warns you | Annotated path: `UsePostgres` — its only overload. The annotation covers the configuration-binding path the builder exposes, so the call site warns even when you configure in code. The rest of the surface publishes clean. |
| `Excalibur.Cdc.MongoDB` | Doesn't warn you | |
| `Excalibur.Cdc.DynamoDb` | **Not compatible** | Ships `Excalibur.Data.DynamoDb`, which is not AOT-compatible |
| `Excalibur.Cdc.Firestore` | **Not compatible** | Ships `Excalibur.Data.Firestore`, which is not AOT-compatible |
| `Excalibur.Cdc.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |

### Leader Election

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.LeaderElection` | Warns you | Annotated paths: `AddLeaderElectionWatcher`. The rest of the surface publishes clean. |
| `Excalibur.LeaderElection.InMemory` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddInMemoryLeaderElection`, which reflects over the options type. The overload that takes its options in code publishes clean. The rest of the surface publishes clean. |
| `Excalibur.LeaderElection.SqlServer` | Clean | `UseSqlServer` no longer binds configuration for you, so it publishes clean. To read options from configuration, call `services.AddOptions<SqlServerLeaderElectionOptions>().BindConfiguration("Section:Path")` alongside the registration — binding is reflective, so the trim/AOT warning belongs at your own call site where the trimmer can see it. |
| `Excalibur.LeaderElection.Postgres` | Clean | `UsePostgres` no longer binds configuration for you, so it publishes clean. To read options from configuration, call `services.AddOptions<PostgresLeaderElectionOptions>().BindConfiguration("Section:Path")` alongside the registration — binding is reflective, so the trim/AOT warning belongs at your own call site where the trimmer can see it. |
| `Excalibur.LeaderElection.Redis` | Doesn't warn you | |
| `Excalibur.LeaderElection.MongoDB` | Doesn't warn you | |
| `Excalibur.LeaderElection.Consul` | **Not compatible** | Consul SDK dependency |
| `Excalibur.LeaderElection.Kubernetes` | **Not compatible** | Kubernetes SDK dependency |

### Hosting

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Hosting` | Warns you | Annotated paths: `AddDispatch`, `AddExcalibur`, `AddExcaliburHealthChecks` and 1 more. The rest of the surface publishes clean. |
| `Excalibur.Hosting.Web` | Clean | |
| `Excalibur.Hosting.Aws` | Clean | |
| `Excalibur.Hosting.AwsLambda` | Warns you | Annotated paths: `AddExcaliburAwsLambdaServerless`. The rest of the surface publishes clean. |
| `Excalibur.Hosting.AzureFunctions` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddExcaliburAzureFunctionsServerless`, which reflects over the options type. The overload that takes its options in code publishes clean. The rest of the surface publishes clean. |
| `Excalibur.Hosting.GoogleCloudFunctions` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddExcaliburGoogleCloudFunctionsServerless`, which reflects over the options type. The overload that takes its options in code publishes clean. The rest of the surface publishes clean. |
| `Excalibur.Hosting.HealthChecks` | Warns you | Annotated paths: `UseExcaliburHealthChecks`. The rest of the surface publishes clean. |
| `Excalibur.Hosting.Jobs` | **Not compatible** | Ships `Excalibur.Jobs`, which is not AOT-compatible |
| `Excalibur.Hosting.Observability` | Clean | |
| `Excalibur.Hosting.Logging.Serilog` | Clean | |

### A3 (Authentication, Authorization, Audit)

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.A3` | Warns you | Annotated paths: `AddA3DispatchServices`, `AddExcaliburA3`, `ExtractResourceId` and 4 more. The rest of the surface publishes clean. |
| `Excalibur.A3.Abstractions` | Clean | |
| `Excalibur.A3.AspNetCore` | Clean | |
| `Excalibur.A3.Core` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddRoles`, which reflects over the options type. The overload that takes its options in code publishes clean. The rest of the surface publishes clean. |
| `Excalibur.A3.Governance` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddOrphanedAccessDetection`, which reflects over the options type. The overload that takes its options in code publishes clean. The rest of the surface publishes clean. |
| `Excalibur.A3.Governance.Abstractions` | Clean | |
| `Excalibur.A3.Policy.Cedar` | Clean | |
| `Excalibur.A3.Policy.Opa` | Clean | |

### Security and Compliance

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Security.Abstractions` | Clean | |
| `Excalibur.Security` | Warns you | `[DynamicallyAccessedMembers]` annotations for property-level encryption. Annotated paths: `AddDispatchSecurity`, `AddDispatchSecurityMiddleware`, `AddInputValidation` and 8 more. The rest of the surface publishes clean. |
| `Excalibur.Compliance.SqlServer` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddSqlServerKeyEscrow`, which reflects over the options type. The overload that takes its options in code publishes clean. The rest of the surface publishes clean. |
| `Excalibur.Compliance.Postgres` | Clean | |
| `Excalibur.Caching` | **Not compatible** | HybridCache uses reflection |

### Jobs

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Jobs` | **Not compatible** | The projection-rebuild and outbox jobs drive reflective paths; the requirement is annotated at the members that do it |
| `Excalibur.Jobs.Abstractions` | Clean | |
| `Excalibur.Jobs.Aws` | Clean | |
| `Excalibur.Jobs.Azure` | Clean | |
| `Excalibur.Jobs.Cdc` | **Not compatible** | Ships `Excalibur.Jobs`, which is not AOT-compatible |
| `Excalibur.Jobs.DataProcessing` | **Not compatible** | Ships `Excalibur.Jobs`, which is not AOT-compatible |
| `Excalibur.Jobs.GoogleCloud` | Clean | |
| `Excalibur.Jobs.Redis` | Warns you | Annotated paths: `DistributeJobAsync`, `ReportJobCompletionAsync`. The rest of the surface publishes clean. |
| `Excalibur.Jobs.SqlServer` | **Not compatible** | Ships `Excalibur.Jobs`, which is not AOT-compatible |

### Testing

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Testing` | Clean | |
| `Excalibur.Testing.Conformance` | **Not compatible** | Test kits that read stored data back through the store under test, which deserializes reflectively. A trimmed test host is not a supported configuration |

### Tools

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Migrate.Tool` | Doesn't warn you | |

---

## Metapackages

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.RabbitMQ` | Warns you | The single entry point is annotated, so there is no unannotated path to avoid |
| `Excalibur.Dispatch.Aws` | Warns you | The single entry point is annotated, so there is no unannotated path to avoid |
| `Excalibur.Dispatch.Azure` | Warns you | The single entry point is annotated, so there is no unannotated path to avoid |
| `Excalibur.Dispatch.Kafka` | **Not compatible** | Ships `Excalibur.Dispatch.Transport.Kafka`, which is not AOT-compatible |
| `Excalibur.Dispatch.Postgres` | Warns you | Annotated paths: `AddDispatchWithPostgres`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.SqlServer` | Warns you | Registers an outbox, which serializes payloads reflectively. The single entry point is annotated, so there is no unannotated path to avoid |
| `Excalibur.Postgres` | Warns you | Annotated paths: `AddExcaliburPostgres`. The rest of the surface publishes clean. |
| `Excalibur.SqlServer` | Warns you | Registers an outbox, which serializes payloads reflectively. Both entry points are annotated, so there is no unannotated path to avoid |

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
   <!-- The generators and DISP analyzers are bundled in Excalibur.Dispatch; no extra reference needed. -->
   <PackageReference Include="Excalibur.Dispatch" />
   ```
3. **Create a `JsonSerializerContext`** for your application types (see [Native AOT Guide](native-aot.md#json-serialization))
4. **Mark handlers with `[AutoRegister]`** for compile-time DI registration
5. **Publish with AOT**:
   ```bash
   dotnet publish -c Release
   ```
6. **Check for warnings** — and know what their absence does and does not prove.

   Zero IL2xxx/IL3xxx warnings means **every reflection path that is annotated** is either unused by you
   or already handled. It does **not** mean the packages you reference do no reflection: a suppressed
   site produces zero warnings by construction, which is exactly what suppression is for. So a silent
   publish is conclusive for a **Clean** package and for a **Warns you** package, and proves nothing for
   a **Doesn't warn you** one.

7. **For any package in the "Doesn't warn you" column, test the binary, not the build log.** Run the
   published ahead-of-time output and exercise the paths you depend on. That is the only signal available
   to you for those packages, because the compile-time one has been turned off.

For a **Warns you** package, `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` propagate the warning
to your call site, so you can make the decision where you can see it. That propagation is what the
**Doesn't warn you** column is telling you that you will not get.


## Additional Packages

These packages ship from this repository and were absent from the matrix above. Status is read
directly from each project's `IsAotCompatible` property, the same source the sections above use.

| Package | AOT Status | Notes |
|---------|------------|-------|
| `Excalibur.Dispatch.Compat.MassTransit` | Clean |  |
| `Excalibur.Dispatch.Compat.MediatR` | Clean | `AddMediatRCompat` registers a fixed set of adapters and needs no reflection over consumer types, so the whole surface publishes clean. |
| `Excalibur.Dispatch.Migration` | **Not compatible** |  |
| `Excalibur.Dispatch.Transport.IbmMq` | **Not compatible** | NOT compatible. The IBM MQ managed client uses runtime reflection and dynamic assembly loading. |
| `Excalibur.Dispatch.Transport.Mqtt` | Warns you | Builder pattern, no reflection. The CloudEvents encoder serializes the payload through reflection-based JSON. Annotated paths: `ToTransportMessageAsync`, `AddCloudEventsForMqtt`. The rest of the surface publishes clean. |
| `Excalibur.Dispatch.Transport.Pulsar` | Clean |  |
| `Excalibur.AuditLogging.Abstractions` | Clean |  |
| `Excalibur.Compliance.MongoDb` | Clean |  |
| `Excalibur.Compliance.Pdf` | **Not compatible** |  |
| `Excalibur.Data.IdentityMap.SqlServer` | Clean | `UseSqlServer` no longer binds configuration for you, so it publishes clean. To read options from configuration, call `services.AddOptions<SqlServerIdentityMapOptions>().BindConfiguration("Section:Path")` alongside the registration — binding is reflective, so the trim/AOT warning belongs at your own call site where the trimmer can see it. |
| `Excalibur.Data.IdentityMap` | Clean |  |
| `Excalibur.Data.Spanner` | **Not compatible** | NOT compatible. Google.Cloud.Spanner.Data uses gRPC + reflection-based value conversion. |
| `Excalibur.EventSourcing.Handlers` | Clean |  |
| `Excalibur.EventSourcing.Oracle` | Doesn't warn you | Analysis reports 16 trim/AOT diagnostics on reflection paths in this package; the rest of the surface publishes clean |
| `Excalibur.Inbox.Oracle` | Doesn't warn you | `UseOracle` no longer binds configuration for you, so it publishes clean. To read options from configuration, call `services.AddOptions<OracleInboxOptions>().BindConfiguration("Section:Path")` alongside the registration — binding is reflective, so the trim/AOT warning belongs at your own call site where the trimmer can see it. |
| `Excalibur.MultiTenancy` | **Not compatible** | Row-discriminator decoration of open-generic IProjectionStore&lt;T&gt; uses reflective MakeGenericType over the DI descriptor set, so this composition |
| `Excalibur.Operations.Dashboard.EventSourcing` | Clean |  |
| `Excalibur.Operations.Dashboard.Spa` | Clean |  |
| `Excalibur.Operations.Dashboard` | Clean |  |
| `Excalibur.Outbox.Marten` | **Not compatible** |  |
| `Excalibur.Outbox.Oracle` | Warns you | Annotated paths: `EnqueueAsync`, `GetUnsentMessagesAsync`. The rest of the surface publishes clean. |
| `Excalibur.Saga.Oracle` | Warns you | Annotated paths: `AddOracleSagaStore`, `SaveSagaRequest`, `UseOracleSagaStore` and 1 more. The rest of the surface publishes clean. |
| `Excalibur.Security.AuditLogging` | Clean |  |
| `Excalibur.Testing.Containers` | **Not compatible** | Fixtures run under a test host, not a trimmed consumer app; reflection in Testcontainers is acceptable here. |
| `Excalibur.Workflows.Abstractions` | Clean |  |
| `Excalibur.Workflows.SqlServer` | Clean |  |
| `Excalibur.Workflows` | Doesn't warn you |  |
| `Excalibur.Dispatch.AspNetCore` | Warns you | Annotated paths: `AddDispatchAspNetCore`. The rest of the surface publishes clean. |

---

## Related Documentation

- [Native AOT Guide](native-aot.md) - Setup, source generators, and troubleshooting
- [Source Generators](source-generators.md) - Full generator reference
- [Package Guide](../package-guide.md) - Package selection guide
