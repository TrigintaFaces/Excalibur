# CDC Job with Quartz.NET Sample

This sample demonstrates using `CdcJob` from `Excalibur.Jobs` for **scheduled CDC processing** via Quartz.NET instead of a custom background service.

## Architecture

This sample uses the same architecture as the `CdcEventStoreElasticsearch` sample but with **Quartz.NET job scheduling** instead of a background service:

```
┌─────────────────────────────────────────────────────────────────────┐
│                     WRITE SIDE (Event Sourcing)                     │
└─────────────────────────────────────────────────────────────────────┘

SQL Server #1 (Legacy DB)         SQL Server #2 (Event Store)
Port 1433                          Port 1434
┌───────────────────┐             ┌───────────────────────────┐
│  LegacyCustomers  │             │  eventsourcing.Events     │
│  (CDC enabled)    │             │  eventsourcing.Snapshots  │
└─────────┬─────────┘             └─────────────┬─────────────┘
          │                                     │
          │ CdcJob (Quartz.NET)                 │ Domain Events
          │ (Cron-scheduled)                    ▲
          ▼                                     │
┌─────────────────────────────────────────────────────────────────────┐
│                    Anti-Corruption Layer (ACL)                       │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐  │
│  │ LegacyCustomer  │───▶│  CdcChangeHandler│───▶│ CustomerAggregate│ │
│  │    Adapter      │    │ (translate CDC   │    │ (domain logic)   │ │
│  │ (schema compat) │    │  to commands)    │    │                  │ │
│  └─────────────────┘    └─────────────────┘    └─────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                     READ SIDE (Projections)                         │
└─────────────────────────────────────────────────────────────────────┘

                   Domain Events
                        │
                        ▼
            ┌─────────────────────────────┐
            │ Projection Service           │
            │ (Event Processing)           │
            └──────────────┬──────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      Elasticsearch Cluster                          │
│                          Port 9200                                  │
│  ┌───────────────────────┐    ┌──────────────────────────────────┐  │
│  │ CustomerSearchProjection│   │ CustomerTierSummaryProjection   │  │
│  │ (full-text search)     │   │ (analytics/materialized view)   │  │
│  └───────────────────────┘    └──────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

## Materialized Views

This sample demonstrates creating **materialized views** in Elasticsearch from domain events:

### CustomerSearchProjection (Full-Text Search)

Denormalized customer data optimized for search and display:

```json
{
  "id": "guid",
  "customerId": "guid",
  "externalId": "CUST-001",
  "name": "Alice Johnson",
  "email": "alice@example.com",
  "phone": "+1-555-0101",
  "orderCount": 5,
  "totalSpent": 1250.00,
  "tier": "Silver",
  "isActive": true,
  "tags": ["high-value", "first-order"]
}
```

### CustomerTierSummaryProjection (Analytics)

Aggregated metrics by customer tier for dashboards:

```json
{
  "id": "SILVER",
  "tier": "Silver",
  "customerCount": 150,
  "activeCount": 142,
  "totalOrders": 1250,
  "totalRevenue": 187500.00,
  "averageSpend": 1250.00
}
```

### How Materialized Views Work

1. **CdcJob** processes CDC changes from SQL Server #1 and creates domain events
2. Domain events are stored in SQL Server #2 (Event Store) with outbox pattern
3. **ProjectionBackgroundService** polls for undispatched events
4. Events are processed through projection handlers to update Elasticsearch
5. Events are marked as dispatched after successful projection update

This pattern provides:
- **Eventual consistency** between write and read models
- **Query optimization** through denormalized data structures
- **Analytics** through pre-aggregated summaries
- **Full-text search** without impacting the write model

## Key Differences from Background Service Sample

| Aspect | Background Service | Quartz.NET CdcJob |
|--------|-------------------|-------------------|
| Scheduling | Fixed polling interval | Cron expressions |
| Concurrency | Single instance | `[DisallowConcurrentExecution]` |
| Health Checks | Manual implementation | Built-in via `JobHealthCheck` |
| Configuration | Custom options class | `Jobs:CdcJob` section |
| Multiple DBs | Manual iteration | `DatabaseConfigs` collection |
| Recovery | Custom error handling | Framework-managed |

## Quick Start

### 1. Start Infrastructure

```bash
cd samples/13-jobs/CdcJobQuartz
docker-compose up -d
```

### 2. Wait for Containers to be Healthy

```bash
docker-compose ps
# All services should show "healthy" status
```

### 3. Initialize Databases

```bash
cd scripts
chmod +x setup-databases.sh
./setup-databases.sh
```

### 4. Run the Sample

```bash
cd ..
dotnet run
```

### 5. Insert Test Data (Optional)

In a separate terminal:

```bash
cd scripts
./insert-test-data.sh
```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "EventStore": "Server=localhost,1434;Database=EventStore;...",
    "CdcSource": "Server=localhost,1433;Database=LegacyDb;...",
    "LegacyDbCdc": "Server=localhost,1433;Database=LegacyDb;...",
    "LegacyDbState": "Server=localhost,1434;Database=EventStore;..."
  },
  "Jobs": {
    "CdcJob": {
      "JobName": "CdcProcessor",
      "JobGroup": "CDC",
      "CronSchedule": "0/5 * * * * ?",
      "DegradedThreshold": "00:05:00",
      "UnhealthyThreshold": "00:10:00",
      "Disabled": false,
      "DatabaseConfigs": [
        {
          "DatabaseName": "LegacyDb",
          "DatabaseConnectionIdentifier": "LegacyDbCdc",
          "StateConnectionIdentifier": "LegacyDbState",
          "StopOnMissingTableHandler": false,
          "CaptureInstances": ["dbo_LegacyCustomers"]
        }
      ]
    }
  }
}
```

### Configuration Reference

| Setting | Description | Default |
|---------|-------------|---------|
| `JobName` | Unique job identifier | Required |
| `JobGroup` | Quartz job group | "Default" |
| `CronSchedule` | Cron expression for scheduling | Required |
| `DegradedThreshold` | Time before health check reports degraded | 5 min |
| `UnhealthyThreshold` | Time before health check reports unhealthy | 10 min |
| `Disabled` | Skip job execution | false |
| `DatabaseConfigs` | Array of database configurations | Required |

### DatabaseConfig Reference

| Setting | Description |
|---------|-------------|
| `DatabaseName` | Friendly name for logging |
| `DatabaseConnectionIdentifier` | Connection string name for CDC source |
| `StateConnectionIdentifier` | Connection string name for state storage |
| `StopOnMissingTableHandler` | Fail if no handler for a CDC table |
| `CaptureInstances` | CDC capture instance names to process |
| `BatchTimeInterval` | Polling interval in milliseconds |
| `QueueSize` | Internal queue size |
| `ProducerBatchSize` | Events to fetch per poll |
| `ConsumerBatchSize` | Events to process per batch |

## Cron Schedule Examples

| Expression | Description |
|------------|-------------|
| `0/5 * * * * ?` | Every 5 seconds |
| `0 0/1 * * * ?` | Every 1 minute |
| `0 0 * * * ?` | Every hour |
| `0 0 6 * * ?` | Daily at 6 AM |
| `0 0 6 ? * MON-FRI` | Weekdays at 6 AM |

## Framework Components Used

| Component | Package | Purpose |
|-----------|---------|---------|
| `CdcJob` | `Excalibur.Jobs` | Quartz.NET CDC processing job |
| `CdcJobConfig` | `Excalibur.Jobs` | Job configuration model |
| `AddExcaliburJobHost` | `Excalibur.Jobs` | Quartz.NET + Excalibur integration |
| `AddCdcProcessor` | `Excalibur.Data.SqlServer` | CDC processor factory |
| `AddSqlServerEventSourcing` | `Excalibur.EventSourcing.SqlServer` | Event store |

## Health Checks

CdcJob automatically integrates with ASP.NET Core health checks:

```csharp
// Health check is registered automatically by AddExcaliburJobHost
// Access via /health endpoint when using ASP.NET Core

// Manual configuration:
CdcJob.ConfigureHealthChecks(healthChecks, configuration, loggerFactory);
```

Health statuses:
- **Healthy**: Job executed within `DegradedThreshold`
- **Degraded**: Job executed within `UnhealthyThreshold` but exceeded `DegradedThreshold`
- **Unhealthy**: Job hasn't executed within `UnhealthyThreshold`

## Troubleshooting

### CdcJob Not Executing

1. Check Quartz scheduler is running:
   ```
   info: Quartz.Core.QuartzScheduler[0]
         Scheduler started.
   ```

2. Verify job is configured:
   - Check `Jobs:CdcJob:CronSchedule` is valid
   - Check `Jobs:CdcJob:Disabled` is `false`

3. Check connection string names match:
   - `DatabaseConnectionIdentifier` must match a `ConnectionStrings` entry
   - `StateConnectionIdentifier` must match a `ConnectionStrings` entry

### CDC Not Capturing Changes

See troubleshooting in the `CdcEventStoreElasticsearch` sample.

## Comparison: When to Use What

| Use Case | Recommended Approach |
|----------|---------------------|
| Real-time CDC (< 5 second latency) | Background Service |
| Batch processing (hourly/daily) | CdcJob with Quartz.NET |
| Multiple databases | CdcJob (built-in support) |
| Production monitoring | CdcJob (health checks) |
| Serverless (Azure Functions) | `IOutboxProcessor` directly |
