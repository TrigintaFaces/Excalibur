---
sidebar_position: 2
title: Package Matrix
description: Complete reference of all Excalibur.Dispatch and Excalibur packages organized by category.
---

# Package Matrix

All shipping packages organized by category. Use [Pick Your Stack](/docs/pick-your-stack) to find the right combination for your scenario.

## Metapackages

One-line setup for common scenarios. Each bundles multiple feature packages.

### Complete Provider Metapackages

| Package | What It Bundles | DI Entry Point |
|---------|----------------|----------------|
| `Excalibur.SqlServer` | Event sourcing, outbox, inbox, sagas, leader election, audit, compliance, data access (all SQL Server) | `AddExcaliburSqlServer()` |
| `Excalibur.Postgres` | Same as above, all PostgreSQL | `AddExcaliburPostgres()` |

### Experience Starter Metapackages

| Package | What It Bundles | DI Entry Point |
|---------|----------------|----------------|
| `Excalibur.Dispatch.SqlServer` | Core dispatch + SQL Server event sourcing + outbox + hosting | `AddExcaliburEventSourcing()` |
| `Excalibur.Dispatch.Postgres` | Core dispatch + Postgres event sourcing + outbox + hosting | `AddExcaliburEventSourcing()` |
| `Excalibur.Dispatch.RabbitMQ` | RabbitMQ transport + resilience + observability | `AddDispatchRabbitMQ()` |
| `Excalibur.Dispatch.Kafka` | Kafka transport + serialization | `AddDispatchKafka()` |
| `Excalibur.Dispatch.Azure` | Azure Service Bus + Azure Key Vault | `AddDispatchAzure()` |
| `Excalibur.Dispatch.Aws` | AWS SQS + AWS Secrets Manager | `AddDispatchAws()` |

---

## Core Dispatch

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch` | Core dispatcher, pipeline, middleware, routing, serialization |
| `Excalibur.Dispatch.Abstractions` | Interfaces: `IDomainEvent`, `IIntegrationEvent`, `IDispatcher`, `IMessageContext` |

## Middleware & Pipeline

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Caching` | Response caching with tag-based invalidation |
| `Excalibur.Dispatch.Observability` | OpenTelemetry metrics, tracing, PII-safe telemetry |
| `Excalibur.Dispatch.Observability.Aws` | AWS CloudWatch integration |
| `Excalibur.Dispatch.Resilience.Polly` | Polly retry, circuit breaker, timeout, bulkhead |
| `Excalibur.Dispatch.Validation.FluentValidation` | FluentValidation integration |
| `Excalibur.Dispatch.Patterns` | Outbox, inbox, dead letter, claim check patterns |
| `Excalibur.Dispatch.Patterns.Azure` | Azure-specific pattern implementations |
| `Excalibur.Dispatch.Patterns.ClaimCheck.InMemory` | In-memory claim check store |
| `Excalibur.Dispatch.Patterns.Hosting.Json` | JSON hosting for pattern configuration |

## Security & Compliance

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Security` | Encryption, signing, rate limiting, authentication |
| `Excalibur.Dispatch.Security.Aws` | AWS KMS key management |
| `Excalibur.Dispatch.Security.Azure` | Azure Key Vault key management |
| `Excalibur.Dispatch.Compliance` | GDPR erasure, compliance monitoring |
| `Excalibur.Dispatch.Compliance.Abstractions` | Compliance interfaces |
| `Excalibur.Dispatch.Compliance.Aws` | AWS compliance integration |
| `Excalibur.Dispatch.Compliance.Azure` | Azure compliance integration |
| `Excalibur.Dispatch.Compliance.Vault` | HashiCorp Vault compliance integration |
| `Excalibur.Compliance.SqlServer` | SQL Server compliance store |
| `Excalibur.Compliance.Postgres` | PostgreSQL compliance store |

## Audit Logging

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.AuditLogging` | Core audit logging framework |
| `Excalibur.Dispatch.AuditLogging.SqlServer` | SQL Server audit store |
| `Excalibur.Dispatch.AuditLogging.Postgres` | PostgreSQL audit store |
| `Excalibur.Dispatch.AuditLogging.Elasticsearch` | Elasticsearch audit store |
| `Excalibur.Dispatch.AuditLogging.Splunk` | Splunk SIEM exporter |
| `Excalibur.Dispatch.AuditLogging.Sentinel` | Microsoft Sentinel SIEM exporter |
| `Excalibur.Dispatch.AuditLogging.Datadog` | Datadog SIEM exporter |
| `Excalibur.Dispatch.AuditLogging.Aws` | AWS CloudWatch audit exporter |
| `Excalibur.Dispatch.AuditLogging.GoogleCloud` | Google Cloud audit exporter |

## Transports

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Transport.Abstractions` | Transport interfaces |
| `Excalibur.Dispatch.Transport.RabbitMQ` | RabbitMQ transport |
| `Excalibur.Dispatch.Transport.Kafka` | Apache Kafka transport |
| `Excalibur.Dispatch.Transport.AzureServiceBus` | Azure Service Bus transport |
| `Excalibur.Dispatch.Transport.AwsSqs` | AWS SQS transport |
| `Excalibur.Dispatch.Transport.GooglePubSub` | Google Cloud Pub/Sub transport |
| `Excalibur.Dispatch.Transport.Grpc` | gRPC transport |

## Serialization

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Serialization.MemoryPack` | MemoryPack binary serialization (opt-in) |
| `Excalibur.Dispatch.Serialization.MessagePack` | MessagePack binary serialization |
| `Excalibur.Dispatch.Serialization.Protobuf` | Protocol Buffers serialization |
| `Excalibur.Dispatch.Serialization.Avro` | Apache Avro serialization |

## Hosting

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Hosting.AspNetCore` | ASP.NET Core integration, minimal API bridge |
| `Excalibur.Dispatch.Hosting.AzureFunctions` | Azure Functions hosting |
| `Excalibur.Dispatch.Hosting.AwsLambda` | AWS Lambda hosting |
| `Excalibur.Dispatch.Hosting.GoogleCloudFunctions` | Google Cloud Functions hosting |
| `Excalibur.Dispatch.Hosting.Serverless.Abstractions` | Serverless hosting abstractions |
| `Excalibur.Dispatch.LeaderElection.Abstractions` | Leader election interfaces |
| `Excalibur.Dispatch.ClaimCheck.AwsS3` | AWS S3 claim check storage |
| `Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage` | Google Cloud Storage claim check |

## Source Generators & Analyzers

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.SourceGenerators` | Compile-time handler discovery, AOT support |
| `Excalibur.Dispatch.SourceGenerators.Analyzers` | Build-time code analysis |
| `Excalibur.Dispatch.Analyzers` | Additional Roslyn analyzers |

## Testing

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Testing` | `DispatchTestHarness`, `MessageContextBuilder` |
| `Excalibur.Dispatch.Testing.Shouldly` | Shouldly assertion extensions |
| `Excalibur.Testing` | Base testing utilities |
| `Excalibur.Testing.Conformance` | Conformance test kits for providers |

---

## Excalibur Domain & Data

| Package | Purpose |
|---------|---------|
| `Excalibur.Domain` | `AggregateRoot`, entities, domain building blocks |
| `Excalibur.Application` | Application layer abstractions |
| `Excalibur.Data.Abstractions` | `IDataRequest`, `IDb`, `IUnitOfWork` |
| `Excalibur.Data` | Core data access |
| `Excalibur.Data.SqlServer` | SQL Server data access (Dapper) |
| `Excalibur.Data.Postgres` | PostgreSQL data access |
| `Excalibur.Data.CosmosDb` | Azure Cosmos DB data access |
| `Excalibur.Data.DynamoDb` | AWS DynamoDB data access |
| `Excalibur.Data.MongoDB` | MongoDB data access |
| `Excalibur.Data.ElasticSearch` | Elasticsearch data access |
| `Excalibur.Data.Redis` | Redis data access |
| `Excalibur.Data.Firestore` | Google Firestore data access |
| `Excalibur.Data.InMemory` | In-memory data store (testing) |
| `Excalibur.Data.MySql` | MySQL data access |
| `Excalibur.Data.DataProcessing` | Background data processing |

## Event Sourcing

| Package | Purpose |
|---------|---------|
| `Excalibur.EventSourcing.Abstractions` | `IEventStore`, `ISnapshot` interfaces |
| `Excalibur.EventSourcing.SqlServer` | SQL Server event store |
| `Excalibur.EventSourcing.Postgres` | PostgreSQL event store |
| `Excalibur.EventSourcing.CosmosDb` | Cosmos DB event store |
| `Excalibur.EventSourcing.DynamoDb` | DynamoDB event store |
| `Excalibur.EventSourcing.MongoDB` | MongoDB event store |
| `Excalibur.EventSourcing.Firestore` | Firestore event store |
| `Excalibur.EventSourcing.Redis` | Redis event store |
| `Excalibur.EventSourcing.InMemory` | In-memory event store (testing) |

## Outbox

| Package | Purpose |
|---------|---------|
| `Excalibur.Outbox` | Outbox pattern core |
| `Excalibur.Outbox.SqlServer` | SQL Server outbox store |
| `Excalibur.Outbox.Postgres` | PostgreSQL outbox store |
| `Excalibur.Outbox.CosmosDb` | Cosmos DB outbox store |
| `Excalibur.Outbox.DynamoDb` | DynamoDB outbox store |
| `Excalibur.Outbox.MongoDB` | MongoDB outbox store |
| `Excalibur.Outbox.ElasticSearch` | Elasticsearch outbox store |
| `Excalibur.Outbox.Firestore` | Firestore outbox store |
| `Excalibur.Outbox.Redis` | Redis outbox store |
| `Excalibur.Outbox.InMemory` | In-memory outbox store (testing) |
| `Excalibur.Inbox` | Inbox pattern (idempotent consumer) |

## Sagas

| Package | Purpose |
|---------|---------|
| `Excalibur.Saga` | Saga/process manager abstractions |
| `Excalibur.Saga.SqlServer` | SQL Server saga store |
| `Excalibur.Saga.Postgres` | PostgreSQL saga store |
| `Excalibur.Saga.CosmosDb` | Cosmos DB saga store |
| `Excalibur.Saga.DynamoDb` | DynamoDB saga store |
| `Excalibur.Saga.MongoDB` | MongoDB saga store |
| `Excalibur.Saga.Firestore` | Firestore saga store |

## Leader Election

| Package | Purpose |
|---------|---------|
| `Excalibur.LeaderElection` | Leader election abstractions |
| `Excalibur.LeaderElection.SqlServer` | SQL Server leader election |
| `Excalibur.LeaderElection.Postgres` | PostgreSQL leader election |
| `Excalibur.LeaderElection.Redis` | Redis leader election |
| `Excalibur.LeaderElection.MongoDB` | MongoDB leader election |
| `Excalibur.LeaderElection.Consul` | Consul leader election |
| `Excalibur.LeaderElection.Kubernetes` | Kubernetes leader election |
| `Excalibur.LeaderElection.InMemory` | In-memory leader election (testing) |

## Change Data Capture (CDC)

| Package | Purpose |
|---------|---------|
| `Excalibur.Cdc` | CDC core abstractions |
| `Excalibur.Cdc.SqlServer` | SQL Server CDC |
| `Excalibur.Cdc.Postgres` | PostgreSQL CDC |
| `Excalibur.Cdc.CosmosDb` | Cosmos DB change feed |
| `Excalibur.Cdc.DynamoDb` | DynamoDB Streams |
| `Excalibur.Cdc.MongoDB` | MongoDB change streams |
| `Excalibur.Cdc.Firestore` | Firestore listeners |

## Jobs & Scheduling

| Package | Purpose |
|---------|---------|
| `Excalibur.Jobs` | Job scheduling core |
| `Excalibur.Jobs.Abstractions` | Job interfaces |
| `Excalibur.Jobs.SqlServer` | SQL Server job store |
| `Excalibur.Jobs.Redis` | Redis job store |
| `Excalibur.Jobs.Cdc` | CDC-triggered jobs |
| `Excalibur.Jobs.DataProcessing` | Data processing jobs |
| `Excalibur.Jobs.Aws` | AWS job infrastructure |
| `Excalibur.Jobs.Azure` | Azure job infrastructure |
| `Excalibur.Jobs.GoogleCloud` | Google Cloud job infrastructure |

## Authorization (A3)

| Package | Purpose |
|---------|---------|
| `Excalibur.A3` | Full A3 authorization (with database stores) |
| `Excalibur.A3.Core` | Core A3 authorization (in-memory, no DB) |
| `Excalibur.A3.Abstractions` | A3 interfaces |
| `Excalibur.A3.Governance` | Access governance, reviews, separation of duties |
| `Excalibur.A3.Governance.Abstractions` | Governance interfaces |
| `Excalibur.A3.Policy.Opa` | Open Policy Agent (OPA) HTTP adapter |
| `Excalibur.A3.Policy.Cedar` | Cedar policy engine HTTP adapter |

## Security & Caching (Excalibur)

| Package | Purpose |
|---------|---------|
| `Excalibur.Security` | Security infrastructure |
| `Excalibur.Security.Abstractions` | Security interfaces |
| `Excalibur.Caching` | Excalibur caching layer |

## Tools

| Package | Purpose |
|---------|---------|
| `Excalibur.Migrate.Tool` | Database migration CLI tool |

---

## Provider Coverage Matrix

Which providers support which features:

| Feature | SQL Server | Postgres | CosmosDB | DynamoDB | MongoDB | Firestore | Redis | In-Memory |
|---------|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| Event Store | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Outbox | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Sagas | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | | |
| Leader Election | ✓ | ✓ | | | ✓ | | ✓ | ✓ |
| CDC | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | | |
| Data Access | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Audit Logging | ✓ | ✓ | | | | | | |
| Compliance | ✓ | ✓ | | | | | | |
| **Complete Metapackage** | **✓** | **✓** | | | | | | |

---

## Next Steps

- [Pick Your Stack](../pick-your-stack.md) -- Find the right packages for your scenario
- [Package Guide](../package-guide.md) -- Detailed package descriptions and ownership
- [Getting Started](../getting-started/index.md) -- Build your first application
