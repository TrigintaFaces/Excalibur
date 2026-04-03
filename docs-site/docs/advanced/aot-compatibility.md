---
sidebar_position: 6
title: AOT Compatibility Matrix
description: Per-package Native AOT compatibility status for Excalibur
---

# AOT Compatibility Matrix

This page documents the Native AOT compatibility status for every shipping package. Use this as a reference when planning AOT-published applications.

**Summary:** 139 of 173 packages are AOT-compatible. 34 packages have documented blocking dependencies.

For setup instructions and source generator usage, see the [Native AOT Guide](native-aot.md).

---

## How to Read This Matrix

| Status | Meaning |
|--------|---------|
| **AOT-safe** | `IsAotCompatible=true`. Zero IL2xxx/IL3xxx warnings in PublishAot builds. |
| **Annotated** | Contains `[RequiresUnreferencedCode]` or `[RequiresDynamicCode]` on specific methods. Safe to use if you avoid the annotated paths. |
| **Not compatible** | `IsAotCompatible=false`. Has a blocking dependency that prevents AOT compilation. |
| **N/A** | Tooling package (analyzer, source generator) — runs at compile time, not at runtime. |

---

## Dispatch Packages

### Core

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch` | AOT-safe | Source-generated handler resolution via `PrecompiledHandlerRegistry` |
| `Excalibur.Dispatch.Abstractions` | AOT-safe | All interfaces and base types are trim-safe |
| `Excalibur.Dispatch.Patterns` | AOT-safe | |
| `Excalibur.Dispatch.Patterns.Azure` | AOT-safe | |
| `Excalibur.Dispatch.Patterns.ClaimCheck.InMemory` | AOT-safe | |
| `Excalibur.Dispatch.Patterns.Hosting.Json` | AOT-safe | |

### Middleware and Pipeline

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Caching` | AOT-safe | `CachingMiddleware` uses `RuntimeFeature.IsDynamicCodeSupported` branching |
| `Excalibur.Dispatch.Resilience.Polly` | AOT-safe | Polly v8 is AOT-compatible |
| `Excalibur.Dispatch.Validation.FluentValidation` | **Not compatible** | FluentValidation uses `Expression.Compile()` |

### Serialization

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Serialization.MemoryPack` | AOT-safe | MemoryPack uses source generation |
| `Excalibur.Dispatch.Serialization.Avro` | **Not compatible** | Apache.Avro uses runtime code generation |
| `Excalibur.Dispatch.Serialization.MessagePack` | **Not compatible** | MessagePack reflection-based resolvers |
| `Excalibur.Dispatch.Serialization.Protobuf` | **Not compatible** | protobuf-net uses `Expression.Compile()` |

### Transport

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Transport.Abstractions` | AOT-safe | |
| `Excalibur.Dispatch.Transport.RabbitMQ` | AOT-safe | Builder pattern, no reflection |
| `Excalibur.Dispatch.Transport.AwsSqs` | AOT-safe | Builder pattern, no reflection |
| `Excalibur.Dispatch.Transport.AzureServiceBus` | **Not compatible** | Azure SDK dependency uses reflection |
| `Excalibur.Dispatch.Transport.GooglePubSub` | **Not compatible** | Google Cloud SDK dependency uses reflection |
| `Excalibur.Dispatch.Transport.Kafka` | **Not compatible** | Confluent.Kafka SchemaRegistry uses `Activator.CreateInstance` |
| `Excalibur.Dispatch.Transport.Grpc` | **Not compatible** | gRPC code generation not AOT-safe |

### Hosting

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Hosting.AspNetCore` | AOT-safe | |
| `Excalibur.Dispatch.Hosting.AwsLambda` | AOT-safe | |
| `Excalibur.Dispatch.Hosting.AzureFunctions` | AOT-safe | |
| `Excalibur.Dispatch.Hosting.GoogleCloudFunctions` | AOT-safe | |
| `Excalibur.Dispatch.Hosting.Serverless.Abstractions` | AOT-safe | |

### Observability

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Observability` | AOT-safe | Uses `System.Diagnostics` (OTel-aligned) |
| `Excalibur.Dispatch.Observability.Aws` | AOT-safe | |

### Security

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Security` | AOT-safe | |
| `Excalibur.Dispatch.Security.Aws` | AOT-safe | |
| `Excalibur.Dispatch.Security.Azure` | AOT-safe | |

### Compliance

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.Compliance.Abstractions` | AOT-safe | |
| `Excalibur.Dispatch.Compliance` | **Not compatible** | Uses assembly scanning for data inventory |
| `Excalibur.Dispatch.Compliance.Aws` | **Not compatible** | AWS KMS SDK dependency |
| `Excalibur.Dispatch.Compliance.Azure` | AOT-safe | |
| `Excalibur.Dispatch.Compliance.Vault` | AOT-safe | |

### Audit Logging

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.AuditLogging` | AOT-safe | |
| `Excalibur.Dispatch.AuditLogging.Aws` | AOT-safe | |
| `Excalibur.Dispatch.AuditLogging.Datadog` | AOT-safe | |
| `Excalibur.Dispatch.AuditLogging.Elasticsearch` | AOT-safe | |
| `Excalibur.Dispatch.AuditLogging.GoogleCloud` | AOT-safe | |
| `Excalibur.Dispatch.AuditLogging.OpenSearch` | AOT-safe | |
| `Excalibur.Dispatch.AuditLogging.Postgres` | AOT-safe | |
| `Excalibur.Dispatch.AuditLogging.Sentinel` | AOT-safe | |
| `Excalibur.Dispatch.AuditLogging.Splunk` | AOT-safe | |
| `Excalibur.Dispatch.AuditLogging.SqlServer` | AOT-safe | |

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
| `Excalibur.Dispatch.Testing` | AOT-safe | |
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
| `Excalibur.Domain` | AOT-safe | |
| `Excalibur.Application` | AOT-safe | |
| `Excalibur.Data.Abstractions` | AOT-safe | |
| `Excalibur.Data` | AOT-safe | |
| `Excalibur.Data.InMemory` | AOT-safe | |
| `Excalibur.Data.SqlServer` | AOT-safe | Dapper is AOT-compatible |
| `Excalibur.Data.Postgres` | AOT-safe | |
| `Excalibur.Data.MySql` | AOT-safe | |
| `Excalibur.Data.MongoDB` | AOT-safe | |
| `Excalibur.Data.Redis` | AOT-safe | |
| `Excalibur.Data.ElasticSearch` | AOT-safe | |
| `Excalibur.Data.DataProcessing` | AOT-safe | |
| `Excalibur.Data.CosmosDb` | **Not compatible** | CosmosDB SDK uses `Expression.Compile()` |
| `Excalibur.Data.DynamoDb` | **Not compatible** | DynamoDB SDK reflection-based marshalling |
| `Excalibur.Data.Firestore` | **Not compatible** | Firestore SDK uses reflection |
| `Excalibur.Data.OpenSearch` | **Not compatible** | OpenSearch SDK dependency |

### Event Sourcing

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.EventSourcing.Abstractions` | AOT-safe | |
| `Excalibur.EventSourcing` | AOT-safe | |
| `Excalibur.EventSourcing.InMemory` | AOT-safe | |
| `Excalibur.EventSourcing.SqlServer` | AOT-safe | |
| `Excalibur.EventSourcing.Postgres` | AOT-safe | |
| `Excalibur.EventSourcing.MongoDB` | AOT-safe | |
| `Excalibur.EventSourcing.Redis` | AOT-safe | |
| `Excalibur.EventSourcing.Sqlite` | AOT-safe | |
| `Excalibur.EventSourcing.AwsS3` | AOT-safe | |
| `Excalibur.EventSourcing.AzureBlob` | AOT-safe | |
| `Excalibur.EventSourcing.Gcs` | AOT-safe | |
| `Excalibur.EventSourcing.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |
| `Excalibur.EventSourcing.DynamoDb` | **Not compatible** | DynamoDB SDK dependency |
| `Excalibur.EventSourcing.Firestore` | **Not compatible** | Firestore SDK dependency |

### Outbox

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Outbox` | AOT-safe | |
| `Excalibur.Outbox.InMemory` | AOT-safe | |
| `Excalibur.Outbox.SqlServer` | AOT-safe | |
| `Excalibur.Outbox.Postgres` | AOT-safe | |
| `Excalibur.Outbox.Redis` | AOT-safe | |
| `Excalibur.Outbox.ElasticSearch` | AOT-safe | |
| `Excalibur.Outbox.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |
| `Excalibur.Outbox.DynamoDb` | **Not compatible** | DynamoDB SDK dependency |
| `Excalibur.Outbox.Firestore` | **Not compatible** | Firestore SDK dependency |
| `Excalibur.Outbox.MongoDB` | **Not compatible** | MongoDB driver dependency |

### Inbox

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Inbox` | AOT-safe | |
| `Excalibur.Inbox.InMemory` | AOT-safe | |
| `Excalibur.Inbox.SqlServer` | AOT-safe | |
| `Excalibur.Inbox.Postgres` | AOT-safe | |
| `Excalibur.Inbox.Redis` | AOT-safe | |
| `Excalibur.Inbox.ElasticSearch` | AOT-safe | |
| `Excalibur.Inbox.MongoDB` | AOT-safe | |
| `Excalibur.Inbox.DynamoDb` | AOT-safe | |
| `Excalibur.Inbox.Firestore` | AOT-safe | |
| `Excalibur.Inbox.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |

### Saga

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Saga` | **Not compatible** | Uses `MakeGenericType` for saga resolution |
| `Excalibur.Saga.SqlServer` | AOT-safe | |
| `Excalibur.Saga.Postgres` | AOT-safe | |
| `Excalibur.Saga.MongoDB` | AOT-safe | |
| `Excalibur.Saga.DynamoDb` | AOT-safe | |
| `Excalibur.Saga.Firestore` | AOT-safe | |
| `Excalibur.Saga.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |

### CDC (Change Data Capture)

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Cdc` | AOT-safe | |
| `Excalibur.Cdc.SqlServer` | AOT-safe | |
| `Excalibur.Cdc.Postgres` | AOT-safe | |
| `Excalibur.Cdc.MongoDB` | AOT-safe | |
| `Excalibur.Cdc.DynamoDb` | AOT-safe | |
| `Excalibur.Cdc.Firestore` | AOT-safe | |
| `Excalibur.Cdc.CosmosDb` | **Not compatible** | CosmosDB SDK dependency |

### Leader Election

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.LeaderElection` | AOT-safe | |
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
| `Excalibur.Hosting` | AOT-safe | |
| `Excalibur.Hosting.Web` | AOT-safe | |
| `Excalibur.Hosting.Aws` | AOT-safe | |
| `Excalibur.Hosting.AwsLambda` | AOT-safe | |
| `Excalibur.Hosting.AzureFunctions` | AOT-safe | |
| `Excalibur.Hosting.GoogleCloudFunctions` | AOT-safe | |
| `Excalibur.Hosting.Serverless` | AOT-safe | |
| `Excalibur.Hosting.HealthChecks` | AOT-safe | |
| `Excalibur.Hosting.Jobs` | AOT-safe | |
| `Excalibur.Hosting.Observability` | AOT-safe | |
| `Excalibur.Hosting.Logging.Serilog` | AOT-safe | |

### A3 (Authentication, Authorization, Audit)

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.A3` | AOT-safe | |
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
| `Excalibur.Security` | **Not compatible** | Uses assembly scanning |
| `Excalibur.Compliance.SqlServer` | AOT-safe | |
| `Excalibur.Compliance.Postgres` | AOT-safe | |
| `Excalibur.Caching` | **Not compatible** | HybridCache uses reflection |

### Jobs

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Jobs` | AOT-safe | |
| `Excalibur.Jobs.Abstractions` | AOT-safe | |
| `Excalibur.Jobs.Aws` | AOT-safe | |
| `Excalibur.Jobs.Azure` | AOT-safe | |
| `Excalibur.Jobs.Cdc` | AOT-safe | |
| `Excalibur.Jobs.DataProcessing` | AOT-safe | |
| `Excalibur.Jobs.GoogleCloud` | AOT-safe | |
| `Excalibur.Jobs.Redis` | AOT-safe | |
| `Excalibur.Jobs.SqlServer` | AOT-safe | |

### Testing

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Testing` | AOT-safe | |
| `Excalibur.Testing.Conformance` | AOT-safe | |

### Tools

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Migrate.Tool` | AOT-safe | |

---

## Metapackages

| Package | AOT Status | Notes |
|---------|-----------|-------|
| `Excalibur.Dispatch.RabbitMQ` | AOT-safe | |
| `Excalibur.Dispatch.Aws` | AOT-safe | |
| `Excalibur.Dispatch.Azure` | AOT-safe | |
| `Excalibur.Dispatch.Kafka` | AOT-safe | Inherits Kafka annotation warnings |
| `Excalibur.Dispatch.Postgres` | AOT-safe | |
| `Excalibur.Dispatch.SqlServer` | AOT-safe | |
| `Excalibur.Postgres` | AOT-safe | |
| `Excalibur.SqlServer` | AOT-safe | |

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
| protobuf-net | `Expression.Compile()` | Protobuf serialization |
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

---

## Related Documentation

- [Native AOT Guide](native-aot.md) - Setup, source generators, and troubleshooting
- [Source Generators](source-generators.md) - Full generator reference
- [Package Guide](../package-guide.md) - Package selection guide
