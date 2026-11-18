---
sidebar_position: 3
title: Outbox Setup
description: Configure the transactional outbox pattern for reliable messaging
---

# Outbox Setup

The outbox pattern ensures reliable message delivery by storing messages in the same transaction as your domain changes. This guide covers configuration options for the Excalibur outbox.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Outbox
  dotnet add package Excalibur.Outbox.SqlServer  # or your provider
  ```
- Familiarity with [outbox pattern concepts](../patterns/outbox.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Why Use an Outbox?

Without an outbox:
```
1. Save aggregate ✅
2. Publish event  ❌ (network failure)
→ Inconsistent state: aggregate saved but event lost
```

With an outbox:
```
1. Save aggregate + outbox message (same transaction) ✅
2. Background processor publishes from outbox ✅
3. Mark message as processed ✅
→ Guaranteed delivery (at-least-once)
```

## Basic Setup

```csharp
services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
{
    outbox.UseSqlServer(opts => opts.ConnectionString(connectionString))
          .WithSqlServerDeadLetterQueue(dlq => dlq.ConnectionString = connectionString)
          .EnableBackgroundProcessing();
}));
```

:::warning The dead-letter decision is required -- there is no default
Every composition on this page that drains the outbox -- `EnableBackgroundProcessing()` or
`AddOutboxHostedService()` -- also needs an `IDeadLetterQueue`. The default processor refuses to resolve
without one, so that no deployment can silently discard a message whose delivery was exhausted. The
variants below omit it to keep the setting under discussion in view; add one of these to each:

```csharp
// Durable -- exhausted messages are kept for inspection and replay. SQL Server is the only durable
// queue the framework ships: on another provider, point this one at a SQL Server connection or
// supply your own IDeadLetterQueue implementation.
outbox.WithSqlServerDeadLetterQueue(dlq => dlq.ConnectionString = connectionString);

// Or discard, chosen deliberately rather than inherited.
services.AddSingleton<IDeadLetterQueue>(NullDeadLetterQueue.Instance);
```

`IDeadLetterQueue` and `NullDeadLetterQueue` are in `Excalibur.Dispatch.ErrorHandling`. Apply the
provider's packaged dead-letter schema before using the durable queue. See
[Dead Letter Handling](../patterns/outbox.md#dead-letter-handling).
:::

Alternatively, use the unified builder:

```csharp
services.AddExcalibur(excalibur =>
{
    excalibur.AddOutbox(outbox =>
    {
        outbox.UseSqlServer(opts => opts.ConnectionString(connectionString))
              .EnableBackgroundProcessing();
    });
});
```

## Configuration Options

### Fluent Builder API

```csharp
services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
{
    outbox.UseSqlServer(sql =>
    {
        sql.ConnectionString(connectionString)
           .SchemaName("Messaging")
           .TableName("OutboxMessages")
           .CommandTimeout(TimeSpan.FromSeconds(60));
    })
    .WithProcessing(processing =>
    {
        processing.BatchSize(100)
                  .PollingInterval(TimeSpan.FromSeconds(5))
                  .MaxRetryCount(5)
                  .RetryDelay(TimeSpan.FromMinutes(1))
                  .EnableParallelProcessing(4);
    })
    .EnableBackgroundProcessing();
}));
```

### Suggested profiles

These are tuning starting points, not measured capacity or latency guarantees. Measure the complete processing path; polling interval alone does not establish delivery latency.

There is no preset argument to pass. `AddOutbox` takes a configuration callback and nothing else, so a
profile is just the set of `WithProcessing` values below -- type out the one you want.

| Setting | Larger parallel batch | Moderate parallel batch | Sequential batch |
|---------|-----------------|----------|------------------|
| `BatchSize` | 1000 | 100 | 10 |
| `PollingInterval` | 100ms | 1s | 5s |
| `MaxRetryCount` | 3 | 5 | 10 |
| `RetryDelay` | 1 min | 5 min | 15 min |
| `EnableParallelProcessing` | 8 | 4 | omit (sequential) |

To drain sequentially, leave `EnableParallelProcessing` off rather than passing `1`: passing `1` turns the
parallel path *on* with a single worker, which is not the same thing as not enabling it.

Large batches, frequent polling:

```csharp
services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox
    .UseSqlServer(sql => sql.ConnectionString(connectionString))
    .WithProcessing(processing => processing
        .BatchSize(1000)
        .PollingInterval(TimeSpan.FromMilliseconds(100))
        .MaxRetryCount(3)
        .RetryDelay(TimeSpan.FromMinutes(1))
        .EnableParallelProcessing(maxDegreeOfParallelism: 8))
    .EnableBackgroundProcessing()));
```

Moderate batches and polling:

```csharp
services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox
    .UseSqlServer(sql => sql.ConnectionString(connectionString))
    .WithProcessing(processing => processing
        .BatchSize(100)
        .PollingInterval(TimeSpan.FromSeconds(1))
        .MaxRetryCount(5)
        .RetryDelay(TimeSpan.FromMinutes(5))
        .EnableParallelProcessing(maxDegreeOfParallelism: 4))
    .EnableBackgroundProcessing()));
```

Small batches, longer retry delay, sequential processing:

```csharp
services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox
    .UseSqlServer(sql => sql.ConnectionString(connectionString))
    .WithProcessing(processing => processing
        .BatchSize(10)
        .PollingInterval(TimeSpan.FromSeconds(5))
        .MaxRetryCount(10)
        .RetryDelay(TimeSpan.FromMinutes(15)))
    .EnableBackgroundProcessing()));
```

## Database Providers

### SQL Server

```csharp
outbox.UseSqlServer(sql =>
{
    sql.ConnectionString(connectionString)
       .SchemaName("Outbox")
       .TableName("Messages")
       .UseRowLocking(true);  // For high concurrency
});
```

### Postgres

```csharp
outbox.UsePostgres(pg =>
{
    pg.ConnectionString(connectionString)
       .SchemaName("outbox")
       .TableName("messages");
});
```

### Redis

```csharp
// With connection string (builder API)
outbox.UseRedis(redis =>
{
    redis.ConnectionString("localhost:6379")
         .KeyPrefix("outbox:")
         .Database(0);
});

// With existing ConnectionMultiplexer
outbox.UseRedis(redis =>
{
    redis.Multiplexer(existingMultiplexer)
         .KeyPrefix("outbox:");
});
```

`RedisOutboxOptions` properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ConnectionString` | `string` | `"localhost:6379"` | Redis connection string |
| `DatabaseId` | `int` | `0` | Redis database ID |
| `KeyPrefix` | `string` | `"outbox"` | Key prefix for outbox entries |
| `SentMessageTtlSeconds` | `int` | `604800` (7 days) | TTL for sent messages (0 = no expiration) |
| `ConnectTimeoutMs` | `int` | `5000` | Connection timeout in milliseconds |
| `SyncTimeoutMs` | `int` | `5000` | Sync operation timeout in milliseconds |
| `AbortOnConnectFail` | `bool` | `false` | Whether to abort on connect failure |
| `UseSsl` | `bool` | `false` | Whether to use SSL/TLS |
| `Password` | `string?` | `null` | Redis authentication password |

### MongoDB

```csharp
outbox.UseMongoDB(mongo =>
{
    mongo.ConnectionString("mongodb://localhost:27017")
         .DatabaseName("myapp");
});
```

The MongoDB outbox builder (`IMongoDBOutboxBuilder`) supports 4 connection overloads: `ConnectionString()`, `Client()`, `ClientFactory()`, and `BindConfiguration()`.

Key `MongoDbOutboxOptions` properties (set via builder or configuration binding):

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ConnectionString` | `string` | `"mongodb://localhost:27017"` | MongoDB connection string |
| `DatabaseName` | `string` | `"excalibur"` | Database name |
| `CollectionName` | `string` | `"outbox_messages"` | Collection name |
| `SentMessageTtlSeconds` | `int` | `604800` (7 days) | TTL for sent messages |

### Elasticsearch

```csharp
outbox.UseElasticSearch(options =>
{
    options.IndexName = "excalibur-outbox";
    options.DefaultBatchSize = 100;

    // Cover the entire claim-to-completion interval — see the note below.
    options.LeaseTimeoutSeconds = 300;
});
```

Key `ElasticsearchOutboxOptions` properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IndexName` | `string` | `"excalibur-outbox"` | Elasticsearch index name |
| `DefaultBatchSize` | `int` | `100` | Default batch size for operations |
| `RefreshPolicy` | `ElasticsearchRefreshPolicy` | `WaitFor` | Refresh policy applied after index **writes**. `None` avoids an explicit write refresh; measure its throughput and visibility tradeoffs. `Immediate` forces a refresh per write. Statistics refresh before reading, but counts can change concurrently. |
| `LeaseTimeoutSeconds` | `int` | `300` | How long a claimed message stays hidden from other pollers |
| `ProcessorId` | `string?` | `null` | Lease-owner identifier for diagnostics; generated per instance when unset |

**Sizing the lease.** When a poller claims a batch, each message is leased for `LeaseTimeoutSeconds` and
hidden from other pollers until it reaches a terminal state or the lease expires. Set this comfortably
longer than the expected interval from each message's claim to its durable completion update, including
remaining batch-claim work, queue and scheduling waits, dispatch and retry work, and completion marking.
Include operating margin. If the lease expires while a delivery is still in flight,
another poller may claim the same message and your handler sees a duplicate. Setting it too long has the
opposite cost — a poller that dies mid-delivery leaves its messages unclaimable until the lease runs out.

**Clock scope.** The Elasticsearch claim script compares the stored lease and stamps the new expiry
using the same node-side clock reading. Poller-host clocks do not decide that claim's eligibility.
This does not establish clock agreement across datastore nodes or eliminate clock adjustments. Verify
the clock and lease behavior of the provider and deployment you use.

`ProcessorId` records which poller holds a lease and is used for diagnostics only. Claim disjointness does
not depend on it. It is enforced by two mechanisms together: a compare-and-swap on the document's
concurrency tokens, which refuses a candidate that changed between the search and the claim, and the
node-side condition above, which refuses a lease that is still live. Two pollers sharing an identifier
cannot both acquire the same unchanged live lease. This is not a duplicate-free delivery guarantee:
an expired lease can be reclaimed while the prior worker is still executing, and successful effects can
be repeated if completion marking fails. Keep effects idempotent.

### Firestore

```csharp
outbox.UseFirestore(options =>
{
    options.ProjectId = "my-gcp-project";
    options.CollectionName = "outbox";
});
```

Key `FirestoreOutboxOptions` properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ProjectId` | `string?` | `null` | GCP project ID (required unless using emulator) |
| `CollectionName` | `string` | `"outbox"` | Firestore collection name |
| `EmulatorHost` | `string?` | `null` | Firestore emulator host for development |
| `MaxBatchSize` | `int` | `500` | Max batch size (Firestore limit: 500) |
| `CreateCollectionIfNotExists` | `bool` | `true` | Auto-create collection |

### Cosmos DB

```csharp
outbox.UseCosmosDb(cosmos =>
{
    cosmos.ConnectionString(connectionString)
          .DatabaseName("myapp")
          .ContainerName("outbox");
});
```

Key `CosmosDbOutboxOptions` properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DatabaseName` | `string?` | Required | Cosmos DB database name |
| `ContainerName` | `string` | `"outbox"` | Container name |
| `Connection.ConnectionString` | `string?` | Required | Cosmos DB connection string |
| `CreateContainerIfNotExists` | `bool` | `true` | Auto-create container |
| `ContainerThroughput` | `int` | `400` | Provisioned RU/s for container |
| `UseDirectMode` | `bool` | `true` | Use direct connection mode |

### DynamoDB

```csharp
outbox.UseDynamoDb(dynamo =>
{
    dynamo.Region(Amazon.RegionEndpoint.USEast1)
          .TableName("outbox");
});
```

Key `DynamoDbOutboxOptions` properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `TableName` | `string` | `"outbox"` | DynamoDB table name |
| `Connection.Region` | `string?` | Required (AWS) | AWS region |
| `Connection.ServiceUrl` | `string?` | `null` | Service URL (for local DynamoDB) |
| `CreateTableIfNotExists` | `bool` | `true` | Auto-create table |
| `EnableStreams` | `bool` | `true` | Enable DynamoDB Streams |
| `DefaultTimeToLiveSeconds` | `int` | `604800` (7 days) | TTL for items |

Cloud outbox query continuation tokens are opaque. The DynamoDB writer preserves String, Number and
Binary key values without reflection, including numeric precision and full binary contents. Its format
has changed from historical SDK-object JSON; consumers should retain the opaque string rather than
depending on that representation. An empty filtered result can still have a continuation token and
`HasMoreResults`. The current `GetPendingAsync` overload does not accept an incoming token, so emission
alone does not provide a resumable pagination API. Native verification of the closed cursor writer does
not establish Native AOT compatibility for the complete DynamoDB provider.

### In-Memory (Testing)

```csharp
outbox.UseInMemory();  // No persistence - for tests only
```

## Processing Configuration

### Batch Size

Controls how many messages are processed per iteration:

```csharp
.WithProcessing(p => p.BatchSize(100))
```

These are unverified tuning starting points; measure completed throughput, latency and memory with equivalent work.

| Scenario | Candidate Size |
|----------|------------------|
| Low latency | 10-50 |
| Standard workloads | 100-200 |
| High throughput | 500-1000 |
| Bulk operations | 1000+ |

### Polling Interval

How often the processor checks for new messages:

```csharp
.WithProcessing(p => p.PollingInterval(TimeSpan.FromSeconds(5)))
```

These are unverified starting points. Polling interval alone does not establish end-to-end delivery latency.

| Scenario | Candidate Interval |
|----------|---------------------|
| Real-time requirements | 100ms - 500ms |
| Standard applications | 1s - 5s |
| Batch processing | 10s - 60s |

### Parallel Processing

Enable concurrent message processing:

Enable concurrency only when ordering and recovery contracts permit it. Ordered claims alone
do not establish ordered completion.

```csharp
.WithProcessing(p => p.EnableParallelProcessing(4))
```

### Retry Configuration

Configure retry behavior for failed messages:

```csharp
.WithProcessing(p =>
{
    p.MaxRetryCount(5)
     .RetryDelay(TimeSpan.FromMinutes(1));
})
```

## Retention and cleanup

Whether stored entries are removed automatically depends on the store.

**The in-memory inbox and the in-memory deduplicator** run a periodic cleanup pass on a configurable interval, enabled by default. Expired entries are eligible for periodic cleanup; retained memory depends on arrival rate, retention and successful cleanup.

Successful sent completion already deletes the row on PostgreSQL and Oracle. The following properties
apply to retained sent entries on the other providers; deletion at completion is not native expiry.
See [Polling provider capabilities](../patterns/outbox.md#polling-provider-capabilities) for acquisition,
fencing, worker-mode and verification limits.

- **Expires on its own** — the datastore removes sent entries with **no host action**.
- **Cleanup you can call** — the store exposes a cleanup operation your host can invoke. **The framework schedules nothing**; calling it is your job.

| Provider | Expires on its own | Cleanup you can call |
|---|---|---|
| SQL Server | No | Yes |
| PostgreSQL, Oracle | Sent row deleted by successful completion, not TTL | Sent cleanup is a no-op because no sent row is retained |
| Marten, in-memory | No | Yes |
| Redis, MongoDB | **Yes** — native expiry, on by default (7 days) | Yes |
| Cosmos DB | **Yes** — container and per-document TTL, on by default (7 days) | Yes |
| DynamoDB | **Yes, on the default path.** When `CreateTableIfNotExists` is `true` (the default) the store creates the table **and enables TTL on it**. If you manage the table yourself, enable TTL on the expiry attribute or nothing is deleted | Yes |
| Firestore | **No, until you act.** The store writes an `expireAt` field but never creates a TTL policy — Firestore deletes nothing until you configure that policy on the field yourself | Yes |
| Elasticsearch | No | Yes |

**Every provider gives you a callable cleanup operation.** PostgreSQL and Oracle remove sent rows at
completion. Retained sent entries on SQL Server, Marten and in-memory accumulate until you remove them;
the other providers have the expiry prerequisites shown above.

### What this means for erasure

An erasure request is **not** satisfied by waiting for a retention window to expire. Some providers retain
sent entries until explicit cleanup; others remove them at successful completion or through configured TTL. Any personal data in a message payload remains readable in the outbox until that entry is removed. The framework provides no mechanism for rendering an existing outbox payload unreadable in place; the available paths today are explicit deletion of the affected entries, or a cleanup pass that covers them.

Do **not** treat a provider's native TTL as an erasure control. It is time-based, not subject-based: it cannot target one data subject, its default window is long, and on DynamoDB and Firestore it deletes nothing at all unless the store's TTL feature has been enabled.

### Scheduling your own cleanup

On a provider with no native expiry, run the cleanup operation from your own host — a background service, a cron job, or your database's own maintenance tooling — because nothing schedules it for you. Size the interval against your outbox volume and whatever retention your compliance obligations require.

## Background Processing

### Hosted Service

Enable automatic background processing:

```csharp
outbox.EnableBackgroundProcessing();
```

This registers an `IHostedService` that continuously processes the outbox.

### Manual Processing

For serverless or custom scenarios:

```csharp
// Don't enable background processing
outbox.UseSqlServer(opts => opts.ConnectionString(connectionString));

// Manually trigger processing
var processor = services.GetRequiredService<IOutboxProcessor>();
await processor.DispatchPendingMessagesAsync(CancellationToken.None);
```

## Multi-Instance Deployment

### Processor ID

Assign processor IDs for attribution and diagnostics. IDs alone do not prevent duplicate delivery;
correctness depends on provider claim/fencing behavior and idempotent effects. A machine name also
does not distinguish multiple processes on the same host:

```csharp
services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox
    .UseSqlServer(sql => sql.ConnectionString(connectionString))
    .WithProcessing(processing => processing.ProcessorId(Environment.MachineName))
    .EnableBackgroundProcessing()));
```

## Health Checks

Monitor outbox health:

```csharp
services.AddHealthChecks()
    .AddOutboxHealthCheck();
```

The health check examines whether the background service is running, its inactivity duration, and
failure-rate thresholds when failure counts are complete. It reports degraded or unhealthy status
according to the configured thresholds. It does not measure pending-message count or oldest-message age.

## Observability

### Metrics

```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Excalibur.Dispatch.BackgroundServices");
        metrics.AddMeter("Excalibur.Outbox.Store");
    });
```

Background-service instruments include:
- `excalibur.background_service.messages_processed` — Processed-message counter
- `excalibur.background_service.messages_failed` — Failed-message counter
- `excalibur.background_service.processing_duration` — Processing-cycle duration histogram, milliseconds
- `excalibur.background_service.processing_cycles` — Processing-cycle counter

Filter the service tags for outbox activity. Compute interval throughput from counter deltas or rates;
these instruments do not report queue depth or oldest-message age. Store-operation telemetry uses the
separate `Excalibur.Outbox.Store` meter.

### Logging

Outbox operations are logged automatically. Configure log levels:

```json
{
  "Logging": {
    "LogLevel": {
      "Excalibur.Outbox": "Information"
    }
  }
}
```

## Best Practices

| Practice | Reason |
|----------|--------|
| Measure candidate settings | The examples are tuning starting points |
| Set processor ID | Support attribution and diagnostics |
| Plan sent-message removal | Prevent unbounded table growth — see [Retention and cleanup](#retention-and-cleanup) for which providers expire rows themselves |
| Monitor pending count | Detect processing bottlenecks |
| Use appropriate batch size | Balance throughput vs. latency |

## Troubleshooting

### Messages not being processed

1. Verify `EnableBackgroundProcessing()` is called
2. Check logs for processing errors
3. Ensure database connection is valid

### High pending count

1. Identify the limiting resource; evaluate batch size or permitted parallelism while preserving ordering and recovery behavior
2. Check for slow downstream handlers
3. Monitor for retry storms

### Duplicate messages

Ensure your handlers are idempotent. The outbox guarantees at-least-once delivery.

A separate existence check followed by an effect is not sufficient: concurrent deliveries can both
pass the check. Use a stable message identity and an atomic deduplication decision coordinated with the
effect, such as a unique-key record and effect in the same database transaction. For external effects,
use the destination's idempotency facility or a recovery protocol appropriate to that destination.

## See Also

- [Outbox Pattern](../patterns/outbox.md) — Conceptual overview of the transactional outbox pattern
- [Inbox Pattern](../patterns/inbox.md) — Idempotent message processing with the inbox pattern
- [Event Store Setup](../configuration/event-store-setup.md) — Configure event stores and aggregate repositories
- [Worker Services](../deployment/worker-services.md) — Deploy dedicated background workers for outbox processing
