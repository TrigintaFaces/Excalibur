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

    -- NONCLUSTERED is required, not a preference: this triple is 1280 bytes, past SQL Server's
    -- 900-byte CLUSTERED key cap and inside the 1700-byte NONCLUSTERED one. Declared CLUSTERED,
    -- the table is created with only a warning and then rejects any row whose key exceeds 900
    -- bytes -- so a long external id fails on first write rather than at deployment.
    CONSTRAINT PK_IdentityMap PRIMARY KEY NONCLUSTERED (ExternalSystem, ExternalId, AggregateType),
    INDEX CIX_IdentityMap_External CLUSTERED (ExternalSystem, ExternalId),
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

## Database schema

**This package does not create its tables.** The DDL ships inside the `.nupkg` under `scripts/`, and
you apply it — so a schema change to your database is always something you did deliberately, never
something a version upgrade did to you while you were not looking.

| Script |
|---|
| `scripts/CreateIdentityMapTable.sql` |

Apply them in filename order before starting the application, and again — for any new ones — before
starting a new version after an upgrade.

The `scripts/` folder lives inside the package, not in your build output. To read it:

```bash
# the package folder, then the scripts inside it
dotnet nuget locals global-packages --list
ls ~/.nuget/packages/excalibur.data.identitymap.sqlserver/<version>/scripts/
```

**The folder is authoritative.** If any list of scripts — including this one — disagrees with what is
actually in the package you installed, the package is right.
