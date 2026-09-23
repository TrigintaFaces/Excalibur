# Excalibur.Data.IdentityMap.SqlServer

SQL Server implementation of the Excalibur aggregate identity map store.

## Setup

### 1. Create the table

```sql
CREATE TABLE [dbo].[IdentityMap] (
    ExternalSystem  NVARCHAR(128) NOT NULL,
    ExternalId      NVARCHAR(256) NOT NULL,
    AggregateType   NVARCHAR(256) NOT NULL,
    AggregateId     NVARCHAR(256) NOT NULL,
    CreatedAt       DATETIMEOFFSET NOT NULL CONSTRAINT DF_IdentityMap_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIMEOFFSET NOT NULL CONSTRAINT DF_IdentityMap_UpdatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_IdentityMap PRIMARY KEY CLUSTERED (ExternalSystem, ExternalId, AggregateType),
    INDEX IX_IdentityMap_AggregateId (AggregateType, AggregateId)
);
```

### 2. Register the provider

```csharp
services.AddIdentityMap(identity =>
{
    identity.UseSqlServer(sql =>
    {
        sql.ConnectionString(connectionString)
           .SchemaName("dbo")
           .TableName("IdentityMap");
    });
});
```

## Features

- **No TVPs required** -- batch lookups use parameterized IN clauses
- **Configurable batch size** -- default 100, automatically chunks larger batches
- **MERGE-based upsert** -- atomic bind operations
- **Conflict detection** -- TryBind detects duplicate key violations and returns existing mappings

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
