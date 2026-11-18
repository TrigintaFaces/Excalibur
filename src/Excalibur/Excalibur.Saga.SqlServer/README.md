# Excalibur.Saga.SqlServer

SQL Server implementation of saga state persistence for the Excalibur framework.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.SqlServer` | Complete | Everything for SQL Server: ES + Outbox + Inbox + Saga + LE + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.SqlServer` for a production-ready SQL Server stack with a single package reference.

## Installation

```bash
dotnet add package Excalibur.Saga.SqlServer
```

## Features

- `SqlServerSagaStore` - Dapper-based saga state persistence
- MERGE-based upsert for atomic save operations
- Connection factory pattern for multi-database scenarios
- Optimistic concurrency with a persisted version token
- NativeAOT compatibility depends on configured providers, payload metadata and dependencies; validate by publishing and executing the application’s native binary.
- NO Entity Framework Core dependency

## Usage

```csharp
// Register SQL Server saga store via ISagaBuilder
services.AddExcalibur(x => x.AddSagas(saga =>
{
    saga.UseSqlServer(sql =>
    {
        sql.ConnectionString = connectionString;
    });
}));

// Or register individually
services.AddSqlServerSagaStore(sql =>
{
    sql.ConnectionString = connectionString;
});

// Or with connection factory
services.AddSqlServerSagaStore(sp =>
    () => new SqlConnection(GetConnectionString(sp)));

// Or use with IDispatchBuilder
builder.UseSqlServerSagaStore(sql => { sql.ConnectionString = connectionString; });
```

## Configuration

```csharp
services.AddSqlServerSagaStore(sql =>
{
    sql.ConnectionString = connectionString;
    sql.SchemaName = "dispatch";
    sql.TableName = "sagas";
});

services.AddSqlServerSagaTimeoutStore(sql =>
{
    sql.ConnectionString = connectionString;
    sql.SchemaName = "dbo";
    sql.TableName = "SagaTimeouts";
});
```

## Database Schema

Run the SQL scripts in the package's `scripts/` directory to create required tables (defaults):
- `dispatch.sagas` - Saga state storage with concurrency control
- `dbo.SagaTimeouts` - Saga timeouts for delayed execution

`SagaTimeouts.sql` explicitly provisions the default `dbo.SagaTimeouts` table and its indexes; the executing user's default schema does not select the target. For a custom direct schema/table, explicitly provision the matching layout, configure `SqlServerSagaTimeoutStoreOptions.SchemaName` and `TableName`, and apply `07-SagaTimeoutFailureHolds.sql` with `@Target` set to that same object before enabling hosted preserving delivery. The existing selected-target migration and custom runtime paths are unchanged.

## State update authority

A matched update requires the persisted state type name and loaded version to match.
A different type name or stale version raises `ConcurrencyException` without changing the
stored row. Type-name matching is exact, including case and trailing spaces.

The stored type discriminator is currently the simple CLR state type name. This update
guard does not distinguish different state types that share that name across namespaces.

## Tenant isolation

Tenant identifiers are opaque values: case and trailing spaces are significant. Scoped
state loads, saves, monitoring queries, retention and timeout cancellation or retirement
compare both the tenant's byte length and its complete value. SQL Server string equality
alone pads trailing spaces, including with the shipped binary collation.

Keep tenant identifiers consistent at the application boundary; this package does not trim
or normalize them. The existing schema's primary key can reject two otherwise identical
saga keys whose tenant identifiers differ only by trailing spaces. Exact lookup predicates
prevent cross-tenant access; they do not change that primary-key restriction.

Explicit `ForAllTenants` monitoring and retention operations remain global. Timeout due
queries and claims also remain global so the processor can service every tenant. Timeout
retirement additionally requires the current claim token.

## Related Packages

- `Excalibur.Saga` - Core saga abstractions
- `Excalibur.Data.Abstractions` - Data access patterns

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
| `scripts/01-SagaSchema.sql` |
| `scripts/02-SagaCorrelationIndex.sql` |
| `scripts/02-SagaMonitoringSchema.sql` |
| `scripts/SagaTimeouts.sql` |

Apply them in filename order before starting the application, and again — for any new ones — before
starting a new version after an upgrade.

The `scripts/` folder lives inside the package, not in your build output. To read it:

```bash
# the package folder, then the scripts inside it
dotnet nuget locals global-packages --list
ls ~/.nuget/packages/excalibur.saga.sqlserver/<version>/scripts/
```

**The folder is authoritative.** If any list of scripts — including this one — disagrees with what is
actually in the package you installed, the package is right.

## Atomic saga transitions (primary SQL profile)

Select durable saga transitions explicitly and configure the selected SQL outbox preparer and payload metadata. The owned connection-string saga registration supports the shipped `dispatch.sagas` schema. A borrowed connection factory or unsupported table binding does not advertise this capability. Apply `01-SagaSchema.sql`, the selected outbox schema, `03-SagaDurableEffects.sql`, and `04-SagaTimeoutAuthority.sql` before starting writers. When selecting the SQL timeout store, also apply `SagaTimeouts.sql` before scripts 03 and 04. These scripts are packed in this package's `scripts/` directory; coordinate all participating writers during schema changes.

The transition commits saga state, processed-delivery evidence, immutable original command/event identities, and configured timer mutations in the same owned SQL transaction. Configured timer participation must match the authoritative SQL backend and supported table binding; an unsupported composition refuses before state commit. Completion cancels the configured incarnation's outstanding timers. A profile with no configured timeout store and no timer effects does not require a fabricated timer participant.

The existing outbox publisher drains the committed messages after the transaction. Delivery remains at least once: consumers must be idempotent. Private transition ordinals and receipts describe original intent membership, not broker acceptance or global delivery order. Consumer routing, partition/group keys and sequence metadata are preserved. Ordinary payload cleanup does not remove the retained transition evidence required to recognize the original committed delivery. Do not independently delete that evidence or rotate saga identities while its delivery may recur.

If commit acknowledgement is lost, the provider closes the uncertain owned transaction and checks the exact immutable transition receipt through an independent bounded, scope-serialized lookup. A confirmed receipt recognizes the original commit; an unresolved result remains unknown and must not be represented as rollback or regenerated effects. This profile does not certify every database restore/failover scenario or other saga-store providers.
## Ordinary saga timer recovery

For persisted ordinary saga timer journals, apply `05-SagaTimerRecoveryState.sql` to the saga state
store and `06-SagaTimerRecovery.sql` to the selected timeout backend before starting the new writers.
The scripts are packaged under `scripts/`; the framework does not create this schema at runtime.
Script06 defaults to `dbo.SagaTimeouts`. For a custom direct timeout table, set its explicit `@Target`
identifier in the script to match `SqlServerSagaTimeoutStoreOptions`. Views and synonyms are not receiver
backends. Registration follows the resolved physical table identity, so aliases for the same table share
authority and distinct tables do not. Stop participating writers while applying the schema.

The receiver applies the original ordered timer batch and its exact receipt in one SQL transaction.
Public cancellation joins that transaction protocol: a later per-ID cancellation suppresses only that
captured schedule, while cancel-all records a saga-wide cutoff. An older captured cancellation preserves
newer timers. The domain is the exact tenant and saga ID, not a separate island for each state type.
Timeout IDs remain opaque, including case and trailing spaces. Existing public timeout methods continue
to work against the ordinary payload schema without script06; coordinator journal recovery requires the
receiver schema. An ordinary coordinator pairs the configured timeout receiver before entering the
handler, including completion with no explicitly scheduled timer: completion can cancel existing timers.
Apply script06 before using that selected coordinator profile; missing receiver metadata fails with an
actionable schema error before the handler runs. Unregistered custom payload tables keep their existing
manual public-method path; selecting one for coordinator recovery requires its script06 registration.

Claims carry original timer authority. Queue-acceptance retirement removes the payload, not the retained
authority: a later cancellation can still revoke queued work. Exact batch replay recognizes its receipt
and cannot recreate a retired payload. A public schedule cannot reuse an ordinary recovery-tracked ID.
SQL atomic saga timers keep their existing membership/generation rules and participate in the same
receiver revision boundary when script06 is installed; this does not add a global delivery-order promise.

Failed or uncertain apply acknowledgements retain the original journal and IDs for reconciliation;
an unresolved submission is not a successful application certificate. Delivery remains at least once,
and timeout handlers must be idempotent. Do not independently delete receiver scopes, identity facts,
retired tokens or receipts while queued work or saga recovery can refer to them. Coordinate database
copy/restore and authority rotation across state, payload and retained evidence; this profile does not
certify arbitrary restores or establish full SQL-driver NativeAOT support.

## Recovering nonretryable timeout failures

The selected SQL timeout store exposes `ISagaTimeoutFailureStore` through its existing `GetService`.
Hosted preserving delivery validates that selected capability before pumping. New `SagaTimeouts.sql`
includes the columns; existing deployments must explicitly apply `07-SagaTimeoutFailureHolds.sql` to
the actual direct table selected by `SqlServerSagaTimeoutStoreOptions` before enabling the profile.
Set the script's `@Target` for custom direct tables. It is idempotent and packaged under `scripts/`;
the framework does not migrate storage automatically. Startup and migration require the shipped
`NEWID()` epoch defaults and zero initial hold/version state; incompatible complete custom profiles
are refused without rewriting their defaults. Optional ordinary-recovery06 is not required
for standalone nonowned timeouts. Owned timeouts require their actual original03/04 or06 authority.

Permanent, poison and unclassified failures can be held with their original payload and due date.
A committed hold excludes normal claims across processes; diagnostic reads and exact-tenant held
inventory retain the source for recovery. Only a current held receipt certifies that exclusion.
Before hold commit, crashes or storage uncertainty can still permit at-least-once redelivery.
Transient and uncertain acceptance classifications retain their existing behavior and budgets.

Use the returned opaque token for conditional `TryResumeHeldFailureAsync`, with one stable command
ID for reconciliation. Resume preserves the original ID/bytes/due and advances a separate delivery
cycle exactly once. It never inserts missing payloads. Cancellation/replacement and old tokens remain
effective; source-ended results release only the old local observation, without acknowledging delivery.
Conflicting operations and foreign target/tenant bindings are refused, not reported as source-ended.
The latest receipt is scoped to the live source row; cancellation/retirement removes it with the row,
and later independent cycles may retire old receipts. No global command ledger or new retention TTL.

Source epochs bind conservative configured server/catalog identity, the actual physical table lifetime
and a random row lifetime. Keep the target's `Excalibur.TimeoutFailureTarget` extended property with its
schema; do not rotate it independently while pending observations refer to it. Conservative alias changes
can require correcting configuration; this is not arbitrary restore/failover or physical-alias certification.
Existing manual schedule/read/claim/retire methods remain usable without07; that older schema cannot
certify hosted failure preservation. This feature does not certify full SQL-driver NativeAOT support.

For the registration-through-query/resume example, see the saga guide’s **Recovering held timeout deliveries** section. The selected store is validated before hosted delivery starts; custom stores without usable hold support cannot start that pump. In-memory hold state is process-local.
