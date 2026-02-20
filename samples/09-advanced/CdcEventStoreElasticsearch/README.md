# CDC + Event Store + Elasticsearch Sample

This advanced sample demonstrates a production-ready CQRS/Event Sourcing architecture using:

- **SQL Server #1 (Port 1433)**: CDC source database (legacy system simulation)
- **SQL Server #2 (Port 1434)**: Event Store for domain events and snapshots
- **Elasticsearch (Port 9200)**: Projections and materialized views

## Architecture

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
          │ CDC Polling Service                 │ Domain Events
          │ (Background Service)                ▲
          ▼                                     │
┌─────────────────────────────────────────────────────────────────────┐
│                    Anti-Corruption Layer (ACL)                       │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐  │
│  │ LegacyCustomer  │───▶│ CdcChangeHandler│───▶│ CustomerAggregate│ │
│  │    Adapter      │    │ (translate CDC  │    │ (domain logic)   │ │
│  │ (schema compat) │    │  to commands)   │    │                  │ │
│  └─────────────────┘    └─────────────────┘    └─────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                     READ SIDE (Projections)                         │
└─────────────────────────────────────────────────────────────────────┘

                   Domain Events
                        │
                        ▼
            ┌─────────────────────────────┐
            │ Projection Background Service│
            │ (Async Event Processing)     │
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

## Key Features

### Production-Grade CDC Processing

- Uses framework's `IDataChangeEventProcessorFactory` for real SQL Server CDC
- Automatic stale position recovery (handles backup restores, CDC cleanup jobs)
- Configurable recovery strategies (`FallbackToEarliest`, `FallbackToLatest`, etc.)
- Proper connection lifecycle management

### Anti-Corruption Layer (ACL)

- `LegacyCustomerAdapter`: Handles schema versioning (V1 → V2 column mapping)
- `CdcChangeHandler`: Translates CDC events to domain commands
- Decouples legacy system from domain model

### Event Sourcing

- Real `SqlServerEventStore` for event persistence
- Snapshot support for aggregate state optimization
- Outbox pattern for reliable event processing

### Elasticsearch Projections

- Real-time search projections (`CustomerSearchProjection`)
- Analytics/materialized views (`CustomerTierSummaryProjection`)
- Automatic index creation and management

## Quick Start

### 1. Start Infrastructure

```bash
cd samples/09-advanced/CdcEventStoreElasticsearch
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
    "CdcSource": "Server=localhost,1433;Database=LegacyDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True",
    "EventStore": "Server=localhost,1434;Database=EventStore;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True"
  },
  "Elasticsearch": {
    "Uri": "http://localhost:9200"
  },
  "CdcPolling": {
    "PollingInterval": "00:00:05",
    "BatchSize": 100,
    "StartImmediately": true,
    "InitialDelay": "00:00:02"
  },
  "Projections": {
    "PollingInterval": "00:00:01",
    "BatchSize": 100,
    "RebuildOnStartup": false
  }
}
```

## CDC Recovery Features

The sample demonstrates production-grade CDC recovery handling:

| Scenario | Detection | Recovery |
|----------|-----------|----------|
| Database backup restore | SQL Error 22037 | FallbackToEarliest |
| CDC cleanup job | SQL Error 22029 | FallbackToEarliest |
| CDC disabled/re-enabled | SQL Error 22911 | FallbackToEarliest |
| LSN out of range | SQL Error 22985 | FallbackToEarliest |

### Recovery Strategies

- `FallbackToEarliest`: Resume from earliest available LSN (recommended)
- `FallbackToLatest`: Skip to latest LSN (use with caution - data loss)
- `InvokeCallback`: Custom handling via callback
- `Throw`: Fail with exception (strictest)

## Infrastructure Commands

### View Logs

```bash
docker-compose logs -f
```

### Stop Services

```bash
docker-compose down
```

### Stop and Remove Volumes

```bash
docker-compose down -v
```

### Start with Kibana (Optional)

```bash
docker-compose --profile visualization up -d
# Access Kibana at http://localhost:5601
```

## Framework Components Used

| Component | Package | Purpose |
|-----------|---------|---------|
| `AddSqlServerEventSourcing` | `Excalibur.EventSourcing.SqlServer` | Event store, snapshots, outbox |
| `AddExcaliburSqlServices` | `Excalibur.Data.SqlServer` | SQL services, Dapper handlers |
| `AddCdcProcessor` | `Excalibur.Data.SqlServer` | CDC processor factory |
| `IDataChangeEventProcessorFactory` | `Excalibur.Data.SqlServer` | Creates CDC processors |
| `IDatabaseConfig` | `Excalibur.Data.SqlServer.Cdc` | CDC configuration |
| `CdcRecoveryOptions` | `Excalibur.Data.SqlServer.Cdc` | Stale position recovery |
| `ElasticSearchProjectionStore` | `Excalibur.Data.ElasticSearch` | Elasticsearch projections |

## Troubleshooting

### CDC Not Capturing Changes

1. Verify CDC is enabled on the database:
   ```sql
   SELECT name, is_cdc_enabled FROM sys.databases WHERE name = 'LegacyDb'
   ```

2. Verify CDC is enabled on the table:
   ```sql
   SELECT t.name, ct.capture_instance
   FROM sys.tables t
   JOIN cdc.change_tables ct ON t.object_id = ct.source_object_id
   ```

3. Check SQL Server Agent is running (required for CDC):
   ```bash
   docker logs excalibur-sqlserver-cdc
   ```

### Elasticsearch Connection Issues

1. Verify Elasticsearch is healthy:
   ```bash
   curl http://localhost:9200/_cluster/health
   ```

2. Check index creation:
   ```bash
   curl http://localhost:9200/_cat/indices
   ```

### Event Store Schema Issues

The framework auto-creates the schema on first use. If issues persist:

```sql
USE EventStore;
SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'eventsourcing';
```
