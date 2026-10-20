---
sidebar_position: 6
title: AOT Compatibility Matrix
description: Per-package Native AOT compatibility status for Excalibur
---

# AOT Compatibility Matrix

This page records known public trim/AOT warning contracts, internal suppressions and compatibility metadata for shipping packages. Use it when planning AOT-published applications.

The table identifies known constraints; it is not certification that every path in a package publishes or runs correctly under NativeAOT. A package marked *Not compatible* does not claim general AOT compatibility. Its constraints can come from a dependency or framework code.

For setup instructions and source generator usage, see the [Native AOT Guide](native-aot.md).

---

## How to Read This Matrix

The column describes known warning contracts when trim/AOT analysis is enabled, and constraints that metadata or suppressed diagnostics can hide. Validate the paths your application uses in an actual published binary.

| Status | What it means for your build | What to do |
|--------|------------------------------|------------|
| **Clean** | No public `RequiresDynamicCode` / `RequiresUnreferencedCode` annotation or internal suppression is recorded by these inventories. This does not guarantee a clean publish. | Publish and exercise your application's paths. |
| **Warns you** | Exported declarations carry trim/AOT warning annotations. Some are conservative interface contracts rather than reflective operations in every implementation. | Read the warning and use an appropriate alternative. A suppression requires evidence for the specific configuration. |
| **Doesn't warn you** | Reflection is present, but it is internal and **suppressed here**, so **your build will say nothing about it**. | Your compiler cannot help you for this package. Publish an ahead-of-time binary and exercise these paths before you ship. |
| **Not compatible** | `IsAotCompatible=false`. The package does not claim ahead-of-time compatibility — usually a blocking dependency, sometimes reflection in its own code. |
| **N/A** | Tooling package (analyzer, source generator) — runs at compile time, not at runtime. |

---

## Dispatch Packages

### Core

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch` | Warns you | Source-generated handler resolution via `PrecompiledHandlerRegistry`. Annotated declarations include `ContextValidationDispatchBuilderExtensions.UseContextValidation`, `ContextValidationServiceCollectionExtensions.AddContextValidation`, `DeliveryServiceCollectionExtensions.AddInboxOptions` and 38 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Abstractions` | Warns you | Interface methods carry warning contracts for their implementations. Annotated declarations include `IFencedOutboxStore.GetUnsentMessagesAsync`, `IOutboxDispatcher.RunOutboxDispatchAsync`, `IOutboxProcessor.DispatchPendingMessagesAsync` and 12 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Patterns` | Warns you | Annotated declarations include `ClaimCheck.ClaimCheckMessageSerializer`, `ClaimCheckServiceCollectionExtensions.AddClaimCheck`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Patterns.Azure` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddAzureBlobClaimCheck`, which reflects over the options type. The overload taking options in code avoids that configuration-binding path. Annotated declarations include `AzurePatternsServiceCollectionExtensions.AddAzureBlobClaimCheck`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Patterns.ClaimCheck.InMemory` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddInMemoryClaimCheck`, which reflects over the options type. The overload taking options in code avoids that configuration-binding path. Annotated declarations include `InMemoryClaimCheckServiceCollectionExtensions.AddInMemoryClaimCheck`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Patterns.Hosting.Json` | Doesn't warn you | |

### Middleware and Pipeline

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Caching` | Warns you | `CachingMiddleware` uses `RuntimeFeature.IsDynamicCodeSupported` branching. Review this package's warning contracts and internal suppressions for the paths you use. Annotated declarations include `CachingDispatchBuilderExtensions.UseCaching`, `CachingDispatchBuilderExtensions.WithCachingOptions`, `CachingServiceCollectionExtensions.AddCachePolicy` and 7 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Resilience.Polly` | Warns you | Polly v8 is AOT-compatible. Annotated declarations include `DispatchBuilderResilienceExtensions.UseResilience`, `PollyResilienceServiceCollectionExtensions.AddPollyResilience`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Validation.FluentValidation` | Warns you | Dual-path: `AotFluentValidatorResolver` + source-gen `IAotValidationDispatcher`. Annotated declarations include `FluentValidationServiceCollectionExtensions.WithFluentValidation`. Other paths still require their own trim/AOT validation. |

### Serialization

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Serialization.MemoryPack` | Warns you | MemoryPack uses source generation. Annotated declarations include `MemoryPack.MemoryPackSerializer`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Serialization.Avro` | **Not compatible** | Apache.Avro uses runtime code generation |
| `Excalibur.Dispatch.Serialization.MessagePack` | **Not compatible** | MessagePack reflection-based resolvers |
| `Excalibur.Dispatch.Serialization.Protobuf` | Warns you | Uses Google.Protobuf (v3.32.1+), which is AOT-compatible. Annotated declarations include `Protobuf.ProtobufSerializer`. Other paths still require their own trim/AOT validation. |

### Transport

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Transport.Abstractions` | Warns you | `CloudEventEncoderAdapter<TOutbound>` forwards to the registered `ICloudEventEncoder<TOutbound>`, which may serialize reflectively. Annotated declarations include `ICloudEventEncoder.ToTransportMessageAsync`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Transport.RabbitMQ` | Warns you | Builder registration includes the annotated CloudEvents paths listed here. Annotated declarations include `RabbitMqCloudEventsServiceCollectionExtensions.AddCloudEventsForRabbitMq`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Transport.AwsSqs` | Warns you | Builder registration includes the annotated CloudEvents paths listed here. Annotated declarations include `AwsCloudEventsServiceCollectionExtensions.AddCloudEventsForEventBridge`, `AwsCloudEventsServiceCollectionExtensions.AddCloudEventsForSns`, `AwsCloudEventsServiceCollectionExtensions.AddCloudEventsForSqs` and 2 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Transport.AzureServiceBus` | Warns you | `MessageDeserializerRegistry` typed pattern; `EventGridTransportSender` annotated. Annotated declarations include `AzureCloudEventsServiceCollectionExtensions.AddCloudEventsForEventHubs`, `AzureCloudEventsServiceCollectionExtensions.AddCloudEventsForServiceBus`, `AzureCloudEventsServiceCollectionExtensions.UseCloudEvents`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Transport.GooglePubSub` | **Not compatible** | Google Cloud SDK dependency uses reflection. `UseCloudEvents` and `AddCloudEventsForPubSub` are separately annotated: the bundled mapper serializes payloads reflectively |
| `Excalibur.Dispatch.Transport.Kafka` | **Not compatible** | Confluent.Kafka SchemaRegistry uses `Activator.CreateInstance`. `AddCloudEventsForKafka` is separately annotated: the bundled mapper serializes payloads reflectively |
| `Excalibur.Dispatch.Transport.Grpc` | Doesn't warn you | `GrpcJsonSerializerContext` source-gen JSON for all 10 transport types |

### Hosting

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Hosting.AspNetCore` | Warns you | Annotated declarations include `EndpointRouteBuilderExtensions.DispatchDeleteAction`, `EndpointRouteBuilderExtensions.DispatchGetAction`, `EndpointRouteBuilderExtensions.DispatchPostAction` and 4 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Hosting.AwsLambda` | Warns you | Annotated declarations include `AwsLambdaServiceCollectionExtensions.AddAwsLambdaServerless`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Hosting.AzureFunctions` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddAzureFunctionsServerless`, which reflects over the options type. The overload taking options in code avoids that configuration-binding path. Annotated declarations include `AzureFunctionsServiceCollectionExtensions.AddAzureFunctionsServerless`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Hosting.GoogleCloudFunctions` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddGoogleCloudFunctionsServerless`, which reflects over the options type. The overload taking options in code avoids that configuration-binding path. Annotated declarations include `GoogleCloudFunctionsServiceCollectionExtensions.AddGoogleCloudFunctionsServerless`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Hosting.Serverless.Abstractions` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddServerlessHosting`, which reflects over the options type. The overload taking options in code avoids that configuration-binding path. Annotated declarations include `ServerlessServiceCollectionExtensions.AddServerlessHosting`. Other paths still require their own trim/AOT validation. |

### Observability

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Observability` | Warns you | Uses `System.Diagnostics` (OTel-aligned). Review this package's warning contracts and internal suppressions for the paths you use. Annotated declarations include `ContextTraceEnricher.CreateContextOperationSpan`, `ContextTraceEnricher.EnrichActivity`, `DispatchBuilderObservabilityExtensions.UseObservability` and 2 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Observability.Aws` | Clean | |

### Security

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Security` | Warns you | Annotated declarations include `Argon2ServiceCollectionExtensions.AddPasswordHasher`, `SecurityExcaliburBuilderExtensions.UseSecurity`, `SecurityMiddlewareExtensions.AddDispatchSecurityMiddleware` and 7 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.Security.Aws` | **Not compatible** | AWS SDK v3 (`AWSSDK.Core`, `AWSSDK.SecretsManager`) is reflection-based and declares no trim or AOT properties, so the analyzer cannot see its reflection and reports nothing while a native publish fails at link time |
| `Excalibur.Security.Azure` | Clean | |

### Compliance

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Compliance.Abstractions` | Warns you | |
| `Excalibur.Compliance` | Warns you | The public paths listed here carry trim/AOT warning contracts. Annotated declarations include `EncryptionDecryptionService.ExportDecryptedAsync`, `GdprServiceCollectionExtensions.AddRetentionPoliciesFromAssembly`, `RegexDataMasker.MaskObject`. Other paths still require their own trim/AOT validation. |
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
| `Excalibur.AuditLogging.Postgres` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddPostgresAuditStore`, which reflects over the options type. The overload taking options in code avoids that configuration-binding path. Annotated declarations include `PostgresAuditServiceCollectionExtensions.AddPostgresAuditStore`. Other paths still require their own trim/AOT validation. |
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
| `Excalibur.Dispatch.Testing` | Warns you | Annotated declarations include `DispatchTestHarness.CreateAsyncScope`, `DispatchTestHarness.CreateScope`, `DispatchTestHarness.Dispatcher` and 1 more. Other paths still require their own trim/AOT validation. |
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
| `Excalibur.Domain` | Warns you | Annotated declarations include `BoundedContextServiceCollectionExtensions.AddBoundedContextEnforcement`, `DefaultBoundedContextValidator.#ctor`, `DefaultBoundedContextValidator.ValidateAsync` and 3 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.Application` | Warns you | Annotated declarations include `ExcaliburApplicationServiceCollectionExtensions.AddActivities`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Data.Abstractions` | Clean | |
| `Excalibur.Data` | Warns you | Annotated declarations include `Serialization.ExcaliburJsonSerializerOptions`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Data.InMemory` | Warns you | Review this package's warning contracts and internal suppressions for the paths you use. Annotated declarations include `InMemoryPersistenceProvider.InitializeAsync`, `InMemoryPersistenceProvider.PersistToDiskAsync`, `InMemoryServiceCollectionExtensions.AddExcaliburInMemory`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Data.SqlServer` | Warns you | Dapper itself is AOT-compatible. The dead-letter property bag uses a source-generated JSON context; configuration-binding registration can still warn. Annotated path: `AddSqlServerPersistence`. Annotated declarations include `SqlServerPersistenceExtensions.AddSqlServerPersistence`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Data.Postgres` | Warns you | Dapper itself is AOT-compatible. The dead-letter property bag uses a source-generated JSON context; configuration-binding registration can still warn. Annotated declarations include `PostgresPersistenceServiceCollectionExtensions.AddPostgresPersistence`, `PostgresPersistenceServiceCollectionExtensions.AddPostgresPersistenceFromSection`, `PostgresServiceCollectionExtensions.AddPostgresDeadLetterStore`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Data.MySql` | Doesn't warn you | |
| `Excalibur.Data.MongoDB` | Warns you | Review this package's warning contracts and internal suppressions for the paths you use. Annotated declarations include `MongoDbMaterializedViewStore.GetAsync`, `MongoDbMaterializedViewStore.SaveAsync`, `MongoDbMaterializedViewStore.SaveViewAndPositionAsync` and 9 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.Data.Redis` | Doesn't warn you | |
| `Excalibur.Data.ElasticSearch` | Warns you | Review this package's warning contracts and internal suppressions for the paths you use. Annotated declarations include `ElasticSearchMaterializedViewStore.GetAsync`, `ElasticSearchMaterializedViewStore.SaveAsync`, `ElasticSearchProjectionStore.GetByIdAsync` and 36 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.Data.ElasticSearch.Azure` | Warns you | `AddAzureKeyVaultCredentialStorage` is annotated, so the diagnostic surfaces at your call site rather than in this package's own build. Annotated declarations include `ElasticsearchAzureServiceCollectionExtensions.AddAzureKeyVaultCredentialStorage`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Data.DataProcessing` | Warns you | Annotated declarations include `DataProcessingBuilderExtensions.AddProcessorsFromAssembly`, `DataProcessingBuilderExtensions.AddRecordHandlersFromAssembly`, `DataProcessingServiceCollectionExtensions.AddDataProcessing`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Data.CosmosDb` | **Not compatible** | CosmosDB SDK uses `Expression.Compile()` |
| `Excalibur.Data.DynamoDb` | **Not compatible** | DynamoDB SDK reflection-based marshalling |
| `Excalibur.Data.Firestore` | **Not compatible** | Firestore SDK uses reflection |
| `Excalibur.Data.OpenSearch` | **Not compatible** | OpenSearch SDK dependency |

### Event Sourcing

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.EventSourcing.Abstractions` | Warns you | Annotated declarations include `IAtomicMaterializedViewStore.SaveViewAndPositionAsync`, `ICursorProjectionStore.QueryCursorAsync`, `IDistinctValuesProjectionStore.DistinctValuesAsync` and 21 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.EventSourcing` | Warns you | Annotated declarations include `CompositeSnapshotStrategy.ShouldCreateSnapshot`, `EncryptingProjectionStoreDecorator.GetByIdAsync`, `EncryptingProjectionStoreDecorator.QueryAsync` and 32 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.EventSourcing.InMemory` | Doesn't warn you | |
| `Excalibur.EventSourcing.SqlServer` | Warns you | Review this package's warning contracts and internal suppressions for the paths you use. Annotated declarations include `SqlServerMaterializedViewStore.GetAsync`, `SqlServerMaterializedViewStore.SaveAsync`, `SqlServerMaterializedViewStore.SaveViewAndPositionAsync` and 9 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.EventSourcing.Postgres` | Warns you | Review this package's warning contracts and internal suppressions for the paths you use. Annotated declarations include `PostgresMaterializedViewStore.GetAsync`, `PostgresMaterializedViewStore.SaveAsync`, `PostgresMaterializedViewStore.SaveViewAndPositionAsync` and 8 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.EventSourcing.MongoDB` | Doesn't warn you | Review this package's warning contracts and internal suppressions for the paths you use. |
| `Excalibur.EventSourcing.Redis` | Doesn't warn you | Review this package's warning contracts and internal suppressions for the paths you use. |
| `Excalibur.EventSourcing.Sqlite` | Warns you | Annotated path: the `IConfiguration`-binding overload of `UseSqlite`, which reflects over the options type. The overload taking options in code avoids that configuration-binding path. Annotated declarations include `SqliteEventSourcingServiceCollectionExtensions.UseSqlite`. Other paths still require their own trim/AOT validation. |
| `Excalibur.EventSourcing.AwsS3` | Doesn't warn you | Review this package's warning contracts and internal suppressions for the paths you use. |
| `Excalibur.EventSourcing.AzureBlob` | Doesn't warn you | Review this package's warning contracts and internal suppressions for the paths you use. |
| `Excalibur.EventSourcing.Gcs` | Doesn't warn you | Review this package's warning contracts and internal suppressions for the paths you use. |
| `Excalibur.EventSourcing.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |
| `Excalibur.EventSourcing.DynamoDb` | **Not compatible** | DynamoDB SDK dependency |
| `Excalibur.EventSourcing.Firestore` | **Not compatible** | Firestore SDK dependency |

### Outbox

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Outbox` | Warns you | Annotated declarations include `MessageOutbox.RunOutboxDispatchAsync`, `MultiTransportOutboxServiceCollectionExtensions.AddMultiTransportOutbox`, `OutboxExcaliburBuilderExtensions.AddOutbox` and 3 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.Outbox.InMemory` | Warns you | Review this package's warning contracts and internal suppressions for the paths you use. Annotated declarations include `InMemoryOutboxStore.EnqueueAsync`, `InMemoryOutboxStore.GetUnsentMessagesAsync`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Outbox.SqlServer` | Warns you | Review this package's warning contracts and internal suppressions for the paths you use. Annotated declarations include `SqlServerOutboxStore.EnqueueAsync`, `SqlServerOutboxStore.GetUnsentMessagesAsync`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Outbox.Postgres` | Warns you | Annotated declarations include `PostgresOutboxStore.EnqueueAsync`, `PostgresOutboxStore.GetUnsentMessagesAsync`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Outbox.Redis` | Warns you | Annotated declarations include `RedisOutboxStore.EnqueueAsync`, `RedisOutboxStore.GetUnsentMessagesAsync`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Outbox.ElasticSearch` | Warns you | Annotated declarations include `ElasticsearchOutboxStore.EnqueueAsync`, `ElasticsearchOutboxStore.GetUnsentMessagesAsync`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Outbox.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |
| `Excalibur.Outbox.DynamoDb` | **Not compatible** | DynamoDB SDK dependency |
| `Excalibur.Outbox.Firestore` | **Not compatible** | Firestore SDK dependency |
| `Excalibur.Outbox.MongoDB` | **Not compatible** | MongoDB driver dependency |

### Inbox

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Inbox` | Clean | |
| `Excalibur.Inbox.InMemory` | Clean | |
| `Excalibur.Inbox.SqlServer` | Doesn't warn you | `UseSqlServer` no longer binds configuration for you, so that registration does not perform reflective binding. To read options from configuration, call `services.AddOptions<SqlServerInboxOptions>().BindConfiguration("Section:Path")` alongside the registration — binding is reflective, so the trim/AOT warning belongs at your own call site where the trimmer can see it. |
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
| `Excalibur.Saga` | Warns you | Source-gen registry population via `IPostConfigureOptions` pattern. Annotated declarations include `SagaBase.RequestTimeoutAsync`, `SagaBase.SerializeTimeoutData`, `SagaCoordinator.HandleEventInternalAsync` and 3 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.Saga.SqlServer` | Warns you | Annotated declarations include `SagaBuilderSqlServerExtensions.UseSqlServer`, `SaveSagaRequest.#ctor`, `SqlServerSagaExtensions.AddSqlServerSagaStore` and 2 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.Saga.Postgres` | Warns you | Annotated declarations include `PostgresSagaExtensions.AddPostgresSagaStore`, `PostgresSagaStore.SaveAsync`, `SagaBuilderPostgresExtensions.UsePostgres` and 1 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.Saga.MongoDB` | Warns you | Annotated path: `SaveAsync` — saga state is serialized with a reflection-based serializer. Annotated declarations include `MongoDbSagaStore.SaveAsync`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Saga.DynamoDb` | **Not compatible** | Ships `Excalibur.Data.DynamoDb`, which is not AOT-compatible |
| `Excalibur.Saga.Firestore` | **Not compatible** | Ships `Excalibur.Data.Firestore`, which is not AOT-compatible |
| `Excalibur.Saga.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |

### CDC (Change Data Capture)

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Cdc` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddCdcHealthCheck`, which reflects over the options type. The overload taking options in code avoids that configuration-binding path. Annotated declarations include `CdcHealthChecksBuilderExtensions.AddCdcHealthCheck`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Cdc.SqlServer` | Warns you | Annotated declarations include `DataChangeHandlerCdcBuilderExtensions.AddDataChangeHandlersFromAssembly`, `DataChangeHandlerServiceCollectionExtensions.AddDataChangeHandlersFromAssembly`, `SqlServerCdcHealthChecksBuilderExtensions.AddCdcHealthCheck`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Cdc.Postgres` | Warns you | Annotated path: `UsePostgres` — its only overload. The annotation covers the configuration-binding path the builder exposes, so the call site warns even when you configure in code. Annotated declarations include `CdcBuilderPostgresExtensions.UsePostgres`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Cdc.MongoDB` | Doesn't warn you | |
| `Excalibur.Cdc.DynamoDb` | **Not compatible** | Ships `Excalibur.Data.DynamoDb`, which is not AOT-compatible |
| `Excalibur.Cdc.Firestore` | **Not compatible** | Ships `Excalibur.Data.Firestore`, which is not AOT-compatible |
| `Excalibur.Cdc.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |

### Leader Election

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.LeaderElection` | Warns you | Annotated declarations include `LeaderElectionWatchServiceCollectionExtensions.AddLeaderElectionWatcher`. Other paths still require their own trim/AOT validation. |
| `Excalibur.LeaderElection.InMemory` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddInMemoryLeaderElection`, which reflects over the options type. The overload taking options in code avoids that configuration-binding path. Annotated declarations include `InMemoryLeaderElectionServiceCollectionExtensions.AddInMemoryLeaderElection`. Other paths still require their own trim/AOT validation. |
| `Excalibur.LeaderElection.SqlServer` | Clean | `UseSqlServer` no longer binds configuration for you, so that registration does not perform reflective binding. To read options from configuration, call `services.AddOptions<SqlServerLeaderElectionOptions>().BindConfiguration("Section:Path")` alongside the registration — binding is reflective, so the trim/AOT warning belongs at your own call site where the trimmer can see it. |
| `Excalibur.LeaderElection.Postgres` | Clean | `UsePostgres` no longer binds configuration for you, so that registration does not perform reflective binding. To read options from configuration, call `services.AddOptions<PostgresLeaderElectionOptions>().BindConfiguration("Section:Path")` alongside the registration — binding is reflective, so the trim/AOT warning belongs at your own call site where the trimmer can see it. |
| `Excalibur.LeaderElection.Redis` | Doesn't warn you | |
| `Excalibur.LeaderElection.MongoDB` | Doesn't warn you | |
| `Excalibur.LeaderElection.Consul` | **Not compatible** | Consul SDK dependency |
| `Excalibur.LeaderElection.Kubernetes` | **Not compatible** | Kubernetes SDK dependency |

### Hosting

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Hosting` | Warns you | Annotated declarations include `DispatchHealthChecksBuilderExtensions.AddExcaliburHealthChecks`, `ExcaliburBuilderContextExtensions.ScanAssemblies`, `ExcaliburBuilderDispatchExtensions.AddDispatch`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Hosting.Web` | Clean | |
| `Excalibur.Hosting.Aws` | Clean | |
| `Excalibur.Hosting.AwsLambda` | Warns you | Annotated declarations include `ExcaliburAwsLambdaServiceCollectionExtensions.AddExcaliburAwsLambdaServerless`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Hosting.AzureFunctions` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddExcaliburAzureFunctionsServerless`, which reflects over the options type. The overload taking options in code avoids that configuration-binding path. Annotated declarations include `ExcaliburAzureFunctionsServiceCollectionExtensions.AddExcaliburAzureFunctionsServerless`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Hosting.GoogleCloudFunctions` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddExcaliburGoogleCloudFunctionsServerless`, which reflects over the options type. The overload taking options in code avoids that configuration-binding path. Annotated declarations include `ExcaliburGoogleCloudFunctionsServiceCollectionExtensions.AddExcaliburGoogleCloudFunctionsServerless`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Hosting.HealthChecks` | Warns you | Annotated declarations include `ExcaliburHealthChecksApplicationExtensions.UseExcaliburHealthChecks`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Hosting.Jobs` | **Not compatible** | Ships `Excalibur.Jobs`, which is not AOT-compatible |
| `Excalibur.Hosting.Observability` | Clean | |
| `Excalibur.Hosting.Logging.Serilog` | Clean | |

### A3 (Authentication, Authorization, Audit)

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.A3` | Warns you | Annotated declarations include `A3ServiceCollectionExtensions.AddA3DispatchServices`, `A3ServiceCollectionExtensions.AddExcaliburA3`, `AuditExcaliburBuilderExtensions.AddAudit` and 2 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.A3.Abstractions` | Clean | |
| `Excalibur.A3.AspNetCore` | Clean | |
| `Excalibur.A3.Core` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddRoles`, which reflects over the options type. The overload taking options in code avoids that configuration-binding path. Annotated declarations include `RoleServiceCollectionExtensions.AddRoles`. Other paths still require their own trim/AOT validation. |
| `Excalibur.A3.Governance` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddOrphanedAccessDetection`, which reflects over the options type. The overload taking options in code avoids that configuration-binding path. Annotated declarations include `OrphanedAccessGovernanceBuilderExtensions.AddOrphanedAccessDetection`. Other paths still require their own trim/AOT validation. |
| `Excalibur.A3.Governance.Abstractions` | Clean | |
| `Excalibur.A3.Policy.Cedar` | Clean | |
| `Excalibur.A3.Policy.Opa` | Clean | |

### Security and Compliance

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Security.Abstractions` | Clean | |
| `Excalibur.Compliance.SqlServer` | Warns you | Annotated path: the `IConfiguration`-binding overload of `AddSqlServerKeyEscrow`, which reflects over the options type. The overload taking options in code avoids that configuration-binding path. Annotated declarations include `SqlServerKeyEscrowServiceCollectionExtensions.AddSqlServerKeyEscrow`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Compliance.Postgres` | Clean | |
| `Excalibur.Caching` | **Not compatible** | HybridCache uses reflection |

### Jobs

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Jobs` | **Not compatible** | The projection-rebuild and outbox jobs drive reflective paths; the requirement is annotated at the members that do it |
| `Excalibur.Jobs.Abstractions` | Warns you | |
| `Excalibur.Jobs.Aws` | Clean | |
| `Excalibur.Jobs.Azure` | Clean | |
| `Excalibur.Jobs.Cdc` | **Not compatible** | Ships `Excalibur.Jobs`, which is not AOT-compatible |
| `Excalibur.Jobs.DataProcessing` | **Not compatible** | Ships `Excalibur.Jobs`, which is not AOT-compatible |
| `Excalibur.Jobs.GoogleCloud` | Clean | |
| `Excalibur.Jobs.Redis` | Warns you | Annotated declarations include `RedisJobCoordinator.DistributeJobAsync`, `RedisJobCoordinator.ReportJobCompletionAsync`. Other paths still require their own trim/AOT validation. |
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
| `Excalibur.Dispatch.RabbitMQ` | Warns you | The single entry point is annotated, so there is no unannotated path to avoid Annotated declarations include `DispatchRabbitMQServiceCollectionExtensions.AddDispatchRabbitMQ`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Aws` | Warns you | The single entry point is annotated, so there is no unannotated path to avoid Annotated declarations include `DispatchAwsServiceCollectionExtensions.AddDispatchAws`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Azure` | Warns you | The single entry point is annotated, so there is no unannotated path to avoid Annotated declarations include `DispatchAzureServiceCollectionExtensions.AddDispatchAzure`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Kafka` | **Not compatible** | Ships `Excalibur.Dispatch.Transport.Kafka`, which is not AOT-compatible |
| `Excalibur.Dispatch.Postgres` | Warns you | Annotated declarations include `DispatchPostgresServiceCollectionExtensions.AddDispatchWithPostgres`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.SqlServer` | Warns you | Registers an outbox, which serializes payloads reflectively. The single entry point is annotated, so there is no unannotated path to avoid Annotated declarations include `DispatchSqlServerServiceCollectionExtensions.AddDispatchWithSqlServer`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Postgres` | Warns you | Annotated declarations include `ExcaliburPostgresServiceCollectionExtensions.AddExcaliburPostgres`. Other paths still require their own trim/AOT validation. |
| `Excalibur.SqlServer` | Warns you | Registers an outbox, which serializes payloads reflectively. Both entry points are annotated, so there is no unannotated path to avoid Annotated declarations include `ExcaliburSqlServerServiceCollectionExtensions.AddExcaliburSqlServer`. Other paths still require their own trim/AOT validation. |

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
5. **Publish with AOT** for your target runtime (this example uses Linux x64):
   ```bash
   dotnet publish -c Release -r linux-x64 -p:PublishAot=true
   ```
6. **Check for warnings** — and know what their absence does and does not prove.

   Zero IL2xxx/IL3xxx warnings is useful evidence for the paths analyzed by the toolchain, but does
   not certify a package or application as AOT compatible. An annotation can be missing, a suppression
   can hide a warning, and an unexercised runtime path can still fail. For every package status, run
   the published native binary and exercise the application paths you depend on. Review suppressed
   paths especially carefully for a **Doesn't warn you** package.

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
| `Excalibur.Dispatch.Compat.MediatR` | Clean | `AddMediatRCompat` registers a fixed set of adapters and needs no reflection over consumer types, so no exported warning contract is recorded for that adapter registration. |
| `Excalibur.Dispatch.Migration` | **Not compatible** |  |
| `Excalibur.Dispatch.Transport.IbmMq` | **Not compatible** | NOT compatible. The IBM MQ managed client uses runtime reflection and dynamic assembly loading. |
| `Excalibur.Dispatch.Transport.Mqtt` | Warns you | Builder registration includes the annotated CloudEvents paths listed here. The CloudEvents encoder serializes the payload through reflection-based JSON. Annotated declarations include `MqttCloudEventsServiceCollectionExtensions.AddCloudEventsForMqtt`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Dispatch.Transport.Pulsar` | Clean |  |
| `Excalibur.AuditLogging.Abstractions` | Clean |  |
| `Excalibur.Compliance.MongoDb` | Clean |  |
| `Excalibur.Compliance.Pdf` | **Not compatible** |  |
| `Excalibur.Data.IdentityMap.SqlServer` | Clean | `UseSqlServer` no longer binds configuration for you, so that registration does not perform reflective binding. To read options from configuration, call `services.AddOptions<SqlServerIdentityMapOptions>().BindConfiguration("Section:Path")` alongside the registration — binding is reflective, so the trim/AOT warning belongs at your own call site where the trimmer can see it. |
| `Excalibur.Data.IdentityMap` | Clean |  |
| `Excalibur.Data.Spanner` | **Not compatible** | NOT compatible. Google.Cloud.Spanner.Data uses gRPC + reflection-based value conversion. |
| `Excalibur.EventSourcing.Handlers` | Clean |  |
| `Excalibur.EventSourcing.Oracle` | Doesn't warn you | Review this package's warning contracts and internal suppressions for the paths you use. |
| `Excalibur.Inbox.Oracle` | Doesn't warn you | `UseOracle` no longer binds configuration for you, so that registration does not perform reflective binding. To read options from configuration, call `services.AddOptions<OracleInboxOptions>().BindConfiguration("Section:Path")` alongside the registration — binding is reflective, so the trim/AOT warning belongs at your own call site where the trimmer can see it. |
| `Excalibur.MultiTenancy` | **Not compatible** | Row-discriminator decoration of open-generic IProjectionStore&lt;T&gt; uses reflective MakeGenericType over the DI descriptor set, so this composition |
| `Excalibur.Operations.Dashboard.EventSourcing` | Clean |  |
| `Excalibur.Operations.Dashboard.Spa` | Clean |  |
| `Excalibur.Operations.Dashboard` | Clean |  |
| `Excalibur.Outbox.Marten` | **Not compatible** |  |
| `Excalibur.Outbox.Oracle` | Warns you | Annotated declarations include `OracleOutboxStore.EnqueueAsync`, `OracleOutboxStore.GetUnsentMessagesAsync`. Other paths still require their own trim/AOT validation. |
| `Excalibur.Saga.Oracle` | Warns you | Annotated declarations include `OracleSagaExtensions.AddOracleSagaStore`, `OracleSagaExtensions.UseOracleSagaStore`, `OracleSagaStore.SaveAsync` and 2 more. Other paths still require their own trim/AOT validation. |
| `Excalibur.Security.AuditLogging` | Clean |  |
| `Excalibur.Testing.Containers` | **Not compatible** | Fixtures run under a test host, not a trimmed consumer app; reflection in Testcontainers is acceptable here. |
| `Excalibur.Workflows.Abstractions` | Clean |  |
| `Excalibur.Workflows.SqlServer` | Clean |  |
| `Excalibur.Workflows` | Doesn't warn you |  |
| `Excalibur.Dispatch.AspNetCore` | Warns you | Annotated declarations include `DispatchAspNetCoreServiceCollectionExtensions.AddDispatchAspNetCore`. Other paths still require their own trim/AOT validation. |

---

## Related Documentation

- [Native AOT Guide](native-aot.md) - Setup, source generators, and troubleshooting
- [Source Generators](source-generators.md) - Full generator reference
- [Package Guide](../package-guide.md) - Package selection guide
