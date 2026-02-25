---
sidebar_position: 8
title: Migrations
description: Schema migrations for event stores using the Excalibur Migration CLI
---

# Migrations

Excalibur provides a CLI tool (`excalibur-migrate`) for managing database schema migrations for event stores, snapshot stores, and related infrastructure.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Access to the target database (SQL Server or PostgreSQL)
- Familiarity with [event store setup](../configuration/event-store-setup.md)

## Installation

The migration CLI is distributed as a .NET global tool:

```bash
dotnet tool install --global Excalibur.Migrate.Tool
```

## Quick Start

```bash
# Check migration status
excalibur-migrate status -p sqlserver -c "Server=localhost;Database=myapp;..."

# Apply all pending migrations
excalibur-migrate up -p sqlserver -c "Server=localhost;Database=myapp;..."

# Generate SQL script for CI/CD
excalibur-migrate script -p sqlserver -c "Server=localhost;Database=myapp;..." -o migrations.sql
```

## Commands

### up

Apply all pending migrations to the database.

```bash
excalibur-migrate up [options]
```

**Example:**

```bash
excalibur-migrate up \
  --provider sqlserver \
  --connection "Server=localhost;Database=myapp;Trusted_Connection=True" \
  --verbose
```

### down

Roll back the migration history to a specific version.

```bash
excalibur-migrate down --to <migration-id> [options]
```

:::caution Record-Only Rollback
The `down` command removes entries from the migration history table — it does **not** execute
reverse SQL scripts. After rolling back, you are responsible for manually reverting any schema
changes (e.g., dropping tables or columns) or writing a new forward migration that undoes the
previous changes. This is the safest default because automatically reversing DDL statements
(especially data-destructive ones like `DROP TABLE`) is error-prone and dangerous in production.
:::

**Options:**

| Option | Description |
|--------|-------------|
| `--to`, `-t` | Target migration ID to roll back to (this migration remains applied) |

**Example:**

```bash
excalibur-migrate down \
  --provider sqlserver \
  --connection "Server=localhost;Database=myapp;Trusted_Connection=True" \
  --to 20260101_001_CreateEventStore
```

### status

Show migration status (applied and pending).

```bash
excalibur-migrate status [options]
```

**Example output:**

```
Migration Status
================

Applied migrations:
  [x] 20260101_001_CreateEventStore (applied: 2026-01-15 10:30:00)
  [x] 20260101_002_CreateSnapshotStore (applied: 2026-01-15 10:30:01)
  [x] 20260102_001_CreateOutboxTable (applied: 2026-01-20 14:45:00)

Pending migrations:
  [ ] 20260201_001_AddMaterializedViews
  [ ] 20260201_002_AddProjectionTables
```

### script

Generate a SQL script for pending migrations without applying them.

```bash
excalibur-migrate script --output <path> [options]
```

**Options:**

| Option | Description |
|--------|-------------|
| `--output`, `-o` | Output file path for the migration script |

**Example:**

```bash
excalibur-migrate script \
  --provider postgres \
  --connection "Host=localhost;Database=myapp;Username=postgres;Password=secret" \
  --output ./migrations/pending.sql
```

This command is useful for:
- CI/CD pipelines that require DBA review
- Generating scripts for manual application
- Auditing schema changes before deployment

## Global Options

All commands support these global options:

| Option | Short | Description | Required |
|--------|-------|-------------|----------|
| `--provider` | `-p` | Database provider (`sqlserver`, `postgres`) | Yes |
| `--connection` | `-c` | Database connection string | Yes |
| `--assembly` | `-a` | Assembly containing migration scripts | No |
| `--namespace` | `-n` | Namespace prefix for migration resources | No |
| `--verbose` | `-v` | Enable verbose output | No |

## Supported Providers

| Provider | Value | Package |
|----------|-------|---------|
| SQL Server | `sqlserver` | `Excalibur.EventSourcing.SqlServer` |
| PostgreSQL | `postgres` | `Excalibur.EventSourcing.Postgres` |

## Migration Scripts

Migration scripts are **.sql files embedded as resources** in a .NET assembly. The migrator discovers them by scanning the assembly's manifest resources for entries matching the pattern `{namespace}.*.sql`.

### How Script Discovery Works

1. The tool loads the assembly specified by `--assembly` (or the entry assembly if omitted)
2. It scans `Assembly.GetManifestResourceNames()` for resources that:
   - Start with the namespace prefix (`--namespace`, or the assembly name if omitted)
   - End with `.sql`
3. The migration ID is extracted from the resource name (everything between the namespace prefix and `.sql`)
4. Migrations are applied in alphabetical order by ID

:::tip Assembly Flag
When using the CLI tool, you must pass `--assembly` pointing to the DLL that contains your
embedded migration scripts. Without it, the tool looks in its own assembly (which has no user
migrations).
:::

### Project Setup

Add your migration scripts to a folder and embed them as resources in your `.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Migrations\**\*.sql" />
</ItemGroup>
```

Name files with a sortable prefix so they execute in the correct order:

```
Migrations/
  20260101_001_CreateEventStore.sql
  20260101_002_CreateSnapshotStore.sql
  20260102_001_CreateOutboxTable.sql
```

The embedded resource names will be `{AssemblyName}.Migrations.20260101_001_CreateEventStore.sql`, etc. Set `--namespace` to `{AssemblyName}.Migrations` (or the tool infers it from the assembly name).

### SQL Server Example

```sql
-- 20260101_001_CreateEventStore.sql
CREATE TABLE [dbo].[Events] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL,
    [AggregateId] NVARCHAR(256) NOT NULL,
    [AggregateType] NVARCHAR(256) NOT NULL,
    [EventType] NVARCHAR(256) NOT NULL,
    [EventData] NVARCHAR(MAX) NOT NULL,
    [Metadata] NVARCHAR(MAX) NULL,
    [Version] BIGINT NOT NULL,
    [OccurredAt] DATETIME2 NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Events] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Events_AggregateVersion] UNIQUE ([AggregateId], [Version])
);

CREATE INDEX [IX_Events_AggregateId] ON [dbo].[Events] ([AggregateId]);
CREATE INDEX [IX_Events_AggregateType] ON [dbo].[Events] ([AggregateType]);
CREATE INDEX [IX_Events_OccurredAt] ON [dbo].[Events] ([OccurredAt]);
```

### PostgreSQL Example

```sql
-- 20260101_001_create_event_store.sql
CREATE TABLE events (
    id BIGSERIAL PRIMARY KEY,
    aggregate_id VARCHAR(256) NOT NULL,
    aggregate_type VARCHAR(256) NOT NULL,
    event_type VARCHAR(256) NOT NULL,
    event_data JSONB NOT NULL,
    metadata JSONB NULL,
    version BIGINT NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_events_aggregate_version UNIQUE (aggregate_id, version)
);

CREATE INDEX ix_events_aggregate_id ON events (aggregate_id);
CREATE INDEX ix_events_aggregate_type ON events (aggregate_type);
CREATE INDEX ix_events_occurred_at ON events (occurred_at);
```

## Multi-Database Migrations

If your stores live on separate databases (see [Multi-Database Support](../data-providers/multi-database.md)), run the tool once per database with the appropriate `--provider`, `--connection`, and `--namespace`:

```bash
# Event store (SQL Server)
excalibur-migrate up \
  --provider sqlserver \
  --connection "$EVENT_STORE_CONNECTION" \
  --assembly ./MyApp.Migrations.dll \
  --namespace MyApp.Migrations.EventStore

# Saga store (SQL Server, different database)
excalibur-migrate up \
  --provider sqlserver \
  --connection "$SAGA_DB_CONNECTION" \
  --assembly ./MyApp.Migrations.dll \
  --namespace MyApp.Migrations.Sagas

# Projections (PostgreSQL)
excalibur-migrate up \
  --provider postgres \
  --connection "$PROJECTION_DB_CONNECTION" \
  --assembly ./MyApp.Migrations.dll \
  --namespace MyApp.Migrations.Projections
```

Use `--namespace` to partition scripts within a single assembly. Organize your migration folder by store:

```
Migrations/
  EventStore/
    20260101_001_CreateEventStore.sql
    20260101_002_CreateSnapshotStore.sql
  Sagas/
    20260101_001_CreateSagaStore.sql
  Projections/
    20260101_001_CreateProjectionTables.sql
```

Each subfolder becomes a distinct namespace prefix (e.g., `MyApp.Migrations.EventStore`), and each `--connection` targets its own database. The migration history table is created per-database, so there are no cross-database conflicts.

## CI/CD Integration

### GitHub Actions

```yaml
jobs:
  migrate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Install Migration Tool
        run: dotnet tool install --global Excalibur.Migrate.Tool

      - name: Run Migrations
        run: |
          excalibur-migrate up \
            --provider sqlserver \
            --connection "${{ secrets.DB_CONNECTION_STRING }}" \
            --assembly ./src/MyApp/bin/Release/net9.0/MyApp.dll \
            --verbose
```

### Azure DevOps

```yaml
steps:
  - task: UseDotNet@2
    inputs:
      version: '8.x'

  - script: dotnet tool install --global Excalibur.Migrate.Tool
    displayName: 'Install Migration Tool'

  - script: |
      excalibur-migrate up \
        --provider sqlserver \
        --connection "$(DbConnectionString)" \
        --assembly ./src/MyApp/bin/Release/net9.0/MyApp.dll \
        --verbose
    displayName: 'Apply Migrations'
```

### Script-Based Deployment

For environments requiring DBA approval:

```bash
# 1. Generate script
excalibur-migrate script \
  --provider sqlserver \
  --connection "$PROD_CONNECTION" \
  --output ./deploy/migrations.sql

# 2. Review script (manual step)

# 3. Apply via SQL client
sqlcmd -S prod-server -d myapp -i ./deploy/migrations.sql
```

## Programmatic Usage

For applications that need to run migrations on startup:

### SQL Server

```csharp
services.AddSqlServerEventStore(connectionString);
services.AddHostedService<SqlServerMigrationHostedService>();

// Or configure via options:
services.Configure<SqlServerMigratorOptions>(options =>
{
    options.AutoMigrateOnStartup = true;
    options.MigrationNamespace = "MyApp.Migrations";
});
```

### PostgreSQL

```csharp
services.AddPostgresEventStore(connectionString);
services.AddHostedService<PostgresMigrationHostedService>();

services.Configure<PostgresMigratorOptions>(options =>
{
    options.AutoMigrateOnStartup = true;
    options.MigrationNamespace = "MyApp.Migrations";
});
```

## Best Practices

### 1. Version Control Migrations

Always commit migration files to version control alongside code changes.

### 2. Use Descriptive Names

Name migrations with date prefix and clear description:

```
20260215_001_AddCustomerEmailIndex.sql
20260215_002_CreateMaterializedViewsTable.sql
```

### 3. Make Migrations Idempotent

Use `IF NOT EXISTS` or `CREATE OR ALTER` where possible:

```sql
-- SQL Server
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Events_AggregateId')
BEGIN
    CREATE INDEX [IX_Events_AggregateId] ON [dbo].[Events] ([AggregateId]);
END

-- PostgreSQL
CREATE INDEX IF NOT EXISTS ix_events_aggregate_id ON events (aggregate_id);
```

### 4. Test Rollback Workflow

Since `down` only removes history records (not schema changes), test your full rollback workflow in non-production environments:

```bash
# 1. Apply migrations
excalibur-migrate up -p sqlserver -c "$DEV_CONNECTION"

# 2. Roll back history to a known-good point
excalibur-migrate down -p sqlserver -c "$DEV_CONNECTION" --to 20260101_001

# 3. Manually revert schema changes (or apply a compensating migration)

# 4. Re-apply
excalibur-migrate up -p sqlserver -c "$DEV_CONNECTION"
```

### 5. Use Scripts in Production

For production deployments, prefer generating scripts over direct application:

```bash
# Generate and review
excalibur-migrate script -p sqlserver -c "$PROD_CONNECTION" -o migrations.sql

# Apply via approved process
```

## Troubleshooting

### Connection Failures

```bash
# Enable verbose output
excalibur-migrate status -p sqlserver -c "..." --verbose
```

### Missing Migrations

Ensure migrations are embedded as resources in your `.csproj` (see [Project Setup](#project-setup) above) and that `--assembly` points to the correct DLL.

### Provider Not Found

Verify the provider package is installed:

```bash
dotnet add package Excalibur.EventSourcing.SqlServer
# or
dotnet add package Excalibur.EventSourcing.Postgres
```

## Next Steps

- **[Event Store](event-store.md)** — Core event persistence
- **[Snapshots](snapshots.md)** — Snapshot stores and strategies
- **[Data Providers](../data-providers/index.md)** — Provider-specific features

## See Also

- [Multi-Database Support](../data-providers/multi-database.md) - Typed IDb interfaces for separate database connections
- [Event Versioning](./versioning.md) - Schema evolution and upcasting strategies for events
- [Event Store](./event-store.md) - Core event persistence and stream management
- [Event Sourcing Overview](./index.md) - Fundamental concepts and getting started guide
