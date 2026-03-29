---
sidebar_position: 3
title: Pick Your Stack
description: Find the right Excalibur packages for your scenario — from simple dispatching to full enterprise deployments.
---

# Pick Your Stack

Not sure which packages you need? Follow this guide to find the right combination for your scenario.

```mermaid
graph TD
    START[What are you building?] --> SIMPLE[Simple dispatcher]
    START --> ES[Event-sourced app]
    START --> DIST[Distributed system]
    START --> SERVERLESS[Serverless function]
    START --> FULL[Full enterprise stack]

    SIMPLE --> |Install| PKG_DISPATCH[Excalibur.Dispatch]

    ES --> DB{Which database?}
    DB --> SQL[SQL Server]
    DB --> PG[PostgreSQL]
    DB --> NOSQL[NoSQL]

    SQL --> |Quick start| PKG_SQL_STARTER[Excalibur.Dispatch.SqlServer]
    SQL --> |Production| PKG_SQL_FULL[Excalibur.SqlServer]
    PG --> |Quick start| PKG_PG_STARTER[Excalibur.Dispatch.Postgres]
    PG --> |Production| PKG_PG_FULL[Excalibur.Postgres]

    ES --> TRANSPORT{Need messaging?}
    TRANSPORT --> RMQ[Excalibur.Dispatch.RabbitMQ]
    TRANSPORT --> KAFKA[Excalibur.Dispatch.Kafka]
    TRANSPORT --> AZURE[Excalibur.Dispatch.Azure]
    TRANSPORT --> AWS_PKG[Excalibur.Dispatch.Aws]
```

---

## Simple Message Dispatcher

**Replacing MediatR or building a basic command/query pipeline.**

```xml
<PackageReference Include="Excalibur.Dispatch" />
```

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseValidation();
});
```

That's it. No other packages needed. See the [Getting Started](getting-started/index.md) guide.

---

## Event-Sourced Application

### Which database?

import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

<Tabs>
<TabItem value="sqlserver" label="SQL Server" default>

**Quick start** — event sourcing + outbox + hosting:

```xml
<PackageReference Include="Excalibur.Dispatch.SqlServer" />
```

**Production** — adds inbox, sagas, leader election, audit logging, compliance:

```xml
<PackageReference Include="Excalibur.SqlServer" />
```

```csharp
services.AddExcaliburSqlServer(sql =>
{
    sql.ConnectionString = connectionString;
    sql.UseLeaderElection = true;
    sql.UseAuditLogging = true;
});
```

</TabItem>
<TabItem value="postgres" label="PostgreSQL">

**Quick start** — event sourcing + outbox + hosting:

```xml
<PackageReference Include="Excalibur.Dispatch.Postgres" />
```

**Production** — adds inbox, sagas, leader election, audit logging, compliance:

```xml
<PackageReference Include="Excalibur.Postgres" />
```

```csharp
services.AddExcaliburPostgres(pg =>
{
    pg.ConnectionString = connectionString;
    pg.UseLeaderElection = true;
    pg.UseAuditLogging = true;
});
```

</TabItem>
<TabItem value="nosql" label="CosmosDB / DynamoDB / MongoDB">

Install individually — no full-stack metapackage yet:

```xml
<PackageReference Include="Excalibur.EventSourcing.CosmosDb" />
<!-- or -->
<PackageReference Include="Excalibur.EventSourcing.DynamoDb" />
<!-- or -->
<PackageReference Include="Excalibur.EventSourcing.MongoDB" />

<!-- Add if you need outbox -->
<PackageReference Include="Excalibur.Outbox.InMemory" />
<!-- Add hosting -->
<PackageReference Include="Excalibur.Hosting.Web" />
```

</TabItem>
</Tabs>

---

## Adding a Message Transport

Choose a transport for publishing integration events between services:

<Tabs>
<TabItem value="rabbitmq" label="RabbitMQ" default>

```xml
<PackageReference Include="Excalibur.Dispatch.RabbitMQ" />
```

```csharp
services.AddDispatchRabbitMQ(rmq =>
{
    rmq.ConnectionString = "amqp://guest:guest@localhost";
});
```

Bundles: transport + resilience + observability.

</TabItem>
<TabItem value="kafka" label="Apache Kafka">

```xml
<PackageReference Include="Excalibur.Dispatch.Kafka" />
```

```csharp
services.AddDispatchKafka(kafka =>
{
    kafka.BootstrapServers = "localhost:9092";
});
```

Bundles: transport + serialization.

</TabItem>
<TabItem value="azure" label="Azure Service Bus">

```xml
<PackageReference Include="Excalibur.Dispatch.Azure" />
```

```csharp
services.AddDispatchAzure(azure =>
{
    azure.ConnectionString = "Endpoint=sb://...";
});
```

Bundles: transport + Azure Key Vault integration.

</TabItem>
<TabItem value="aws" label="AWS SQS">

```xml
<PackageReference Include="Excalibur.Dispatch.Aws" />
```

```csharp
services.AddDispatchAws(aws =>
{
    aws.Region = "us-east-1";
});
```

Bundles: transport + AWS Secrets Manager integration.

</TabItem>
<TabItem value="googlepubsub" label="Google Pub/Sub">

```xml
<PackageReference Include="Excalibur.Dispatch.Transport.GooglePubSub" />
<PackageReference Include="Excalibur.Dispatch.Resilience.Polly" />
<PackageReference Include="Excalibur.Dispatch.Observability" />
```

No metapackage yet — install individually.

</TabItem>
</Tabs>

---

## Distributed System with Sagas

Start with the event-sourcing stack above, then add saga persistence:

```xml
<!-- Already have Excalibur.SqlServer or Excalibur.Postgres? Sagas are included. -->

<!-- If using individual packages, add: -->
<PackageReference Include="Excalibur.Saga.SqlServer" />
<!-- or -->
<PackageReference Include="Excalibur.Saga.Postgres" />
```

---

## Serverless Functions

<Tabs>
<TabItem value="azure-func" label="Azure Functions" default>

```xml
<PackageReference Include="Excalibur.Dispatch.Hosting.AzureFunctions" />
```

</TabItem>
<TabItem value="aws-lambda" label="AWS Lambda">

```xml
<PackageReference Include="Excalibur.Dispatch.Hosting.AwsLambda" />
```

</TabItem>
<TabItem value="gcf" label="Google Cloud Functions">

```xml
<PackageReference Include="Excalibur.Dispatch.Hosting.GoogleCloudFunctions" />
```

</TabItem>
</Tabs>

---

## Full Enterprise Production Stack

Complete provider + transport — everything you need:

```xml
<!-- Database provider (pick one) -->
<PackageReference Include="Excalibur.SqlServer" />

<!-- Transport (pick one) -->
<PackageReference Include="Excalibur.Dispatch.RabbitMQ" />

<!-- Cross-cutting concerns (opt-in) -->
<PackageReference Include="Excalibur.Dispatch.Security" />
<PackageReference Include="Excalibur.Dispatch.Validation.FluentValidation" />
<PackageReference Include="Excalibur.Dispatch.Caching" />
```

```csharp
services.AddExcaliburSqlServer(sql =>
{
    sql.ConnectionString = connectionString;
    sql.UseLeaderElection = true;
    sql.UseAuditLogging = true;
});

services.AddDispatchRabbitMQ(rmq =>
{
    rmq.ConnectionString = "amqp://guest:guest@localhost";
});

services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseValidation().WithFluentValidation();
    dispatch.UseCaching();
    dispatch.UseSecurity(builder.Configuration);
});
```

---

## Package Tiering

| Tier | Purpose | Examples |
|------|---------|---------|
| **Feature** | Single concern, opt-in | `Excalibur.EventSourcing.SqlServer`, `Excalibur.Inbox.SqlServer` |
| **Starter** | Get up and running quickly | `Excalibur.Dispatch.SqlServer`, `Excalibur.Dispatch.RabbitMQ` |
| **Complete** | Everything for a provider | `Excalibur.SqlServer`, `Excalibur.Postgres` |

Starters bundle the most common features. Complete metapackages bundle everything for a database provider so you don't miss anything.

---

## Next Steps

- [Package Guide](package-guide.md) — Full package reference with all 119+ packages
- [Getting Started](getting-started/index.md) — Build your first Dispatch application
- [Core Concepts](core-concepts/index.md) — Understand the architecture
