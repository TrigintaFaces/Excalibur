# SQL Server Event Store Sample

This sample demonstrates **event sourcing with SQL Server** using the Excalibur framework.

## What This Sample Shows

1. **SQL Server Event Store** - Dapper-based event persistence
2. **Connection Factory Pattern** - Configurable connection creation
3. **Schema Scripts** - Database schema for events, snapshots, outbox
4. **Aggregate Lifecycle** - Create, modify, save, and reload aggregates
5. **Health Checks** - SQL Server health monitoring integration

## Prerequisites

- Docker Desktop (for SQL Server container)
- .NET 9.0 SDK

## Quick Start

### 1. Start SQL Server

```bash
cd samples/09-advanced/SqlServerEventStore
docker-compose up -d
```

Wait for the container to be healthy:

```bash
docker-compose ps
# Should show: sqlserver-eventstore   healthy
```

### 2. Create Database and Schema

```bash
# Create the database
docker exec -i sqlserver-eventstore /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YourStrong@Passw0rd' -C \
  -Q "CREATE DATABASE EventStore"

# Run the schema script
docker exec -i sqlserver-eventstore /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YourStrong@Passw0rd' -C \
  -d EventStore -i /eng/create-schema.sql
```

### 3. Run the Sample

```bash
dotnet run
```

### 4. Clean Up

```bash
docker-compose down      # Stop container
docker-compose down -v   # Stop and remove data
```

## Database Schema

The sample uses these tables in the `eventsourcing` schema:

### Events Table

```sql
CREATE TABLE eventsourcing.Events (
    Id              BIGINT IDENTITY(1,1) NOT NULL,
    StreamId        NVARCHAR(255)        NOT NULL,
    Version         BIGINT               NOT NULL,
    EventType       NVARCHAR(500)        NOT NULL,
    Payload         NVARCHAR(MAX)        NOT NULL,
    Metadata        NVARCHAR(MAX)        NULL,
    CreatedAt       DATETIME2(7)         NOT NULL,
    DispatchedAt    DATETIME2(7)         NULL,

    CONSTRAINT PK_Events PRIMARY KEY (Id),
    CONSTRAINT UQ_Events_Stream_Version UNIQUE (StreamId, Version)
);
```

### Snapshots Table

```sql
CREATE TABLE eventsourcing.Snapshots (
    Id              BIGINT IDENTITY(1,1) NOT NULL,
    StreamId        NVARCHAR(255)        NOT NULL,
    Version         BIGINT               NOT NULL,
    SnapshotType    NVARCHAR(500)        NOT NULL,
    Payload         NVARCHAR(MAX)        NOT NULL,
    CreatedAt       DATETIME2(7)         NOT NULL,

    CONSTRAINT PK_Snapshots PRIMARY KEY (Id)
);
```

### Outbox Table

```sql
CREATE TABLE eventsourcing.Outbox (
    Id              BIGINT IDENTITY(1,1) NOT NULL,
    MessageId       UNIQUEIDENTIFIER     NOT NULL,
    MessageType     NVARCHAR(500)        NOT NULL,
    Payload         NVARCHAR(MAX)        NOT NULL,
    Destination     NVARCHAR(255)        NULL,
    CreatedAt       DATETIME2(7)         NOT NULL,
    PublishedAt     DATETIME2(7)         NULL,
    RetryCount      INT                  NOT NULL DEFAULT 0,

    CONSTRAINT PK_Outbox PRIMARY KEY (Id)
);
```

## Configuration

### Connection String

```json
{
  "ConnectionStrings": {
    "EventStore": "Server=localhost,1433;Database=EventStore;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True"
  }
}
```

### Service Registration

```csharp
// Option 1: Simple registration with connection string
services.AddSqlServerEventSourcing(connectionString, registerHealthChecks: true);

// Option 2: Full configuration
services.AddSqlServerEventSourcing(options =>
{
    options.ConnectionString = connectionString;
    options.RegisterHealthChecks = true;
    options.EventStoreHealthCheckName = "eventstore-sqlserver";
    options.SnapshotStoreHealthCheckName = "snapshotstore-sqlserver";
    options.OutboxStoreHealthCheckName = "outbox-sqlserver";
});

// Option 3: Individual store registration
services.AddSqlServerEventStore(connectionString);
services.AddSqlServerSnapshotStore(connectionString);
services.AddSqlServerOutboxStore(connectionString);

// Option 4: Connection factory (advanced)
services.AddSqlServerEventStore(() => new SqlConnection(connectionString));
```

### Repository Registration

```csharp
services.AddExcaliburEventSourcing(builder =>
{
    builder.AddRepository<MyAggregate, Guid>(id => new MyAggregate(id));
});
```

## Expected Output

```
=================================================
  SQL Server Event Store Sample
=================================================

=== Demo 1: Create and Save an Aggregate ===

Creating new bank account: a1b2c3d4-...
Account opened with initial deposit of $1,000.00
Deposited: $500.00 (Paycheck)
Deposited: $250.00 (Birthday gift)
Withdrew: $200.00 (Groceries)

Saving aggregate to SQL Server event store...
Saved 4 events for aggregate a1b2c3d4-...

Current state after operations:
  Account Holder: John Doe
  Balance: $1,550.00
  Total Deposits: $1,750.00
  Total Withdrawals: $200.00
  Transaction Count: 4
  Version: 4

=== Demo 2: Load Aggregate from Event Store ===

Loading aggregate a1b2c3d4-... from SQL Server...
Successfully loaded aggregate!

Reconstructed state from 4 events:
  Account Holder: John Doe
  Balance: $1,550.00
  ...
```

## Registration Methods

| Method | Registers | Health Checks |
|--------|-----------|---------------|
| `AddSqlServerEventStore` | Event store only | No |
| `AddSqlServerSnapshotStore` | Snapshot store only | No |
| `AddSqlServerOutboxStore` | Outbox store only | No |
| `AddSqlServerEventSourcing` | All stores | Optional |

## Best Practices

1. **Connection Pooling** - SQL Server ADO.NET handles this automatically
2. **Schema Isolation** - Use the `eventsourcing` schema to avoid conflicts
3. **Health Checks** - Enable health checks for production monitoring
4. **Optimistic Concurrency** - The `(StreamId, Version)` unique constraint prevents conflicts
5. **Index Strategy** - The schema includes indexes for common query patterns

## Troubleshooting

### Connection Refused

Ensure SQL Server container is running and healthy:

```bash
docker-compose ps
docker-compose logs sqlserver
```

### Login Failed

Check the password in both `appsettings.json` and `docker-compose.yml` match.

### Schema Not Found

Run the `create-schema.sql` script against the EventStore database.

## Project Structure

```
SqlServerEventStore/
├── SqlServerEventStore.csproj  # Project file
├── Program.cs                   # Main sample with demos
├── appsettings.json             # Configuration
├── docker-compose.yml           # SQL Server container
├── Domain/
│   └── BankAccountAggregate.cs  # Domain aggregate and events
├── sql/
│   └── create-schema.sql        # Database schema
└── README.md                    # This file
```

## Related Samples

- [ExcaliburCqrs](../../01-getting-started/ExcaliburCqrs/) - In-memory event store
- [SnapshotStrategies](../SnapshotStrategies/) - Snapshot optimization patterns
- [EventUpcasting](../EventUpcasting/) - Event schema evolution
