# Excalibur.Compliance.SqlServer

SQL Server implementation of IKeyEscrowService for the Excalibur framework. Provides key escrow storage with Shamir's Secret Sharing for split-knowledge key recovery using Dapper.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.SqlServer` | Complete | Everything for SQL Server: ES + Outbox + Inbox + Saga + LE + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.SqlServer` for a production-ready SQL Server stack with a single package reference.

## Installation

```bash
dotnet add package Excalibur.Compliance.SqlServer
```

## Quick Start

```csharp
services.AddSqlServerErasureStore(options =>
    options.ConnectionString = connectionString);
```

## Schema

This package ships the scripts below under `scripts/`, in the order they must be applied. Which you
need depends on what you register. Run the create scripts on a new database; run the migrations only
when you are upgrading a database provisioned by an earlier version.

| Script | Kind | Required when you use | Affects |
|---|---|---|---|
| `001_CreateComplianceSchema.sql` | create | the erasure, data-inventory and legal-hold stores | `ErasureRequests`, `ErasureCertificates`, `DataInventoryRegistrations`, `DiscoveredDataLocations`, `LegalHolds` |
| `002_CreateKeyEscrowSchema.sql` | create | key escrow (`AddSqlServerKeyEscrow`) | `KeyEscrow`, `RecoveryTokens`, `KeyEscrowWrap` |
| `003_MakeComplianceTenantTotal.sql` | migration | upgrading an existing erasure / legal-hold database | `ErasureRequests`, `LegalHolds` |
| `004_MakeDataInventoryTenantTotal.sql` | migration | upgrading an existing data-inventory database | `DataInventoryRegistrations`, `DiscoveredDataLocations` |
| `005_MakeEscrowTenantTotal.sql` | migration | upgrading an existing key-escrow database | `KeyEscrow` |
| `006_MakeInventoryKeysFitTheIndexLimit.sql` | migration | any data-inventory database provisioned before this version | `DataInventoryRegistrations`, `DiscoveredDataLocations` |

The create scripts make the `compliance` schema if it is absent, and every statement in every script
is guarded, so all of them are safe to re-run and safe to apply to a database that already holds some
of the tables.

```sh
sqlcmd -S server -d database -i 001_CreateComplianceSchema.sql
sqlcmd -S server -d database -i 002_CreateKeyEscrowSchema.sql
```

#### If you provisioned the data-inventory tables before this version

Run `004_MakeDataInventoryTenantTotal.sql` and then `006_MakeInventoryKeysFitTheIndexLimit.sql`, in
that order. The second one is a repair, not a new feature, and it applies whether or not you use
multiple tenants: both inventory tables were created with primary keys wider than SQL Server's
900-byte index limit. `CREATE TABLE` only warned, so the tables exist and work — until a row's key
values get long enough, at which point the insert is refused with `Msg 1946`. That depends on your
data rather than on your schema, so it survives a smoke test and shows up on a real registration.

`006` moves each natural key off the clustered index and keeps it enforced, and it states inside the
file what that trades. Run it with the compliance stores stopped: it rebuilds clustered indexes and
holds a schema-modification lock for the size of the data.

One consequence outlives the migration. `DiscoveredDataLocations` gains an indexed computed column,
and SQL Server then refuses any `INSERT` or `UPDATE` from a session whose `QUOTED_IDENTIFIER` is
`OFF` — including `sqlcmd`, which defaults it off. The application is unaffected, because the client
turns it on when it connects. Ad-hoc repair, bulk import and ETL against that table are not: set
`QUOTED_IDENTIFIER ON` first, or the write is rejected with an error that names the setting and not
the cause.

#### If you are upgrading a database that already holds escrowed keys

Run `005_MakeEscrowTenantTotal.sql`, and do not write your own migration for that table. The tenant
term on `KeyEscrow` is not only a column: the escrow service feeds it into the authenticated
encryption of the key material and reads it back out of the column to decrypt. Rewriting the stored
term therefore invalidates the ciphertext, which cannot be re-authenticated without the master key —
the row still looks correct and the key can never be recovered again. The shipped script closes the
column without rewriting any stored term, and says so where it does it.

### Erasure, data inventory and legal holds

These stores verify their schema on startup and throw if it is absent, so a missing table is
reported before any request is recorded.

Setting `AutoCreateSchema = true` makes a store create its own tables on first use instead. That
is a convenience for development: it requires the application's own credentials to hold DDL
rights, which production deployments usually withhold deliberately, and it puts schema changes
outside whatever change control governs the database. On the erasure and legal-hold surfaces that
is rarely the right trade — prefer the script.

### Key escrow

Key escrow behaves differently, and the difference matters: it does **not** verify its schema on
startup and has no `AutoCreateSchema` option. If the tables are missing you will not find out at
startup — you will find out on the first escrow write, as `Invalid object name`.

Run `002_CreateKeyEscrowSchema.sql`, then escrow one key and recover it, before you rely on escrow
to protect a key you cannot afford to lose. An escrow you have never recovered from is a backup
you have never restored.

If you override `SchemaName` or any table name, rename the corresponding objects in the script to
match.

### The encryption provider, and where the master key comes from

Escrow encrypts every key it stores before writing it, so `AddSqlServerKeyEscrow` needs an
`IEncryptionProvider` registered. If none is, the host now refuses to start and says so. It does
not start and fail later, because "later" for escrow means recovery, and recovery is the moment
you no longer have the thing you were protecting.

```csharp
services.AddComplianceEncryption(/* ... */);   // register this
services.AddSqlServerKeyEscrow(options => { /* ... */ });
```

**The provider behind it must outlive the process.** This is the part that is easy to get wrong,
because the wrong choice works perfectly in development:

| Key source | Survives restart | Use for |
|---|---|---|
| `InMemoryKeyManagementProvider` | **No** | development and tests only |
| AWS KMS, Azure Key Vault, HashiCorp Vault | Yes | production |

A key held in memory is gone at the next restart, and everything escrowed under it becomes
unreadable. Nothing reports this at the time — escrow keeps accepting writes, and the loss only
becomes visible when someone tries to recover.

Cloud key services do not hand out key material; that non-export property is the reason to use
one. They are supported through **envelope encryption** instead: the framework generates a
single-use data key, encrypts your payload with it locally, and asks the key service to wrap that
data key. Only the wrapped form is stored, and unwrapping it requires reaching the key service. A
provider supplies this by implementing `IKeyWrappingProvider`, which the AWS, Azure and Vault
providers do.

Whichever you choose, escrow one key and recover it before you rely on escrow — an escrow you
have never recovered from is a backup you have never restored.

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
