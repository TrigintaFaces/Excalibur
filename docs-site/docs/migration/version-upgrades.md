---
sidebar_position: 4
title: Versioning Strategy
description: Versioning policy, release stages, deprecation rules, and how to stay informed about Excalibur releases.
---

# Versioning Strategy

## Current Status

Excalibur is in **pre-release**, targeting a first stable release of **10.0.0**. APIs may change between pre-release builds. Use pre-release versions for evaluation, early adoption, and feedback.

## Version Scheme

**The package major version matches the targeted .NET major version.** Because Excalibur single-targets `net10.0`, the first stable release is **10.0.0**. When the framework's development line moves to a newer runtime — for example `net11.0` — that line ships as `11.x`.

| Version Component | Meaning | Example |
|-------------------|---------|---------|
| **Major** (`10.x`, `11.x`, …) | The targeted .NET major version | `10.x` targets `net10.0`; `11.x` targets `net11.0` |
| **Minor** (`10.X.0`) | New features, backward compatible within the major | New middleware, new transport provider |
| **Patch** (`10.0.X`) | Bug fixes, backward compatible within the major | Fix null reference, correct calculation |

Minor and patch releases follow [Semantic Versioning 2.0.0](https://semver.org/) **within a major line**: they are always backward compatible. The major number is not an independent API-break counter — it tells you, at a glance, which .NET runtime the package targets, with zero ambiguity about framework compatibility. This mirrors how the .NET platform itself ships (`Microsoft.Extensions.*` and `Microsoft.AspNetCore.*` lock their major to the .NET major), so an Excalibur version reads like a first-party .NET package.

Each Excalibur package single-targets one .NET major. There is no multi-targeting of older runtimes, which lets the framework adopt current-runtime APIs and current third-party dependency majors without being pinned to what also resolves on an older .NET.

## Release Stages

| Stage | NuGet Tag | API Stability | Recommended For |
|-------|-----------|---------------|-----------------|
| **Alpha** | `10.0.0-alpha.N` | APIs may change between releases | Evaluation, early adoption, feedback |
| **Beta** | `10.0.0-beta.N` | APIs are feature-complete but may have minor adjustments | Integration testing, pre-production validation |
| **Release Candidate** | `10.0.0-rc.N` | APIs are frozen; only critical bug fixes | Final validation before production |
| **Stable** | `10.0.0` | Backward-compatible within the major line | Production use |

### What Pre-Release Means for You

- **You can build real applications** -- the framework is functionally complete with an extensive automated test suite
- **APIs may change** -- method signatures, interface shapes, and configuration patterns may evolve
- **No guaranteed upgrade path** between pre-release builds -- consult release notes before upgrading
- **Feedback is welcome** -- your input directly shapes the stable API surface

## Support Lifecycle

### How long a line is supported

**A major line is supported for as long as Microsoft supports the .NET major it targets.** Because the
package major *is* the .NET major, this needs no separate calendar of ours to drift out of date -- the
dates are the ones on the [Support](../support.md#supported-versions) page, and that page is the single
place they are stated.

**A newer line does not end the previous one.** When `11.x` ships, `10.x` keeps receiving fixes until
.NET 10 itself leaves support, the same way `Microsoft.Extensions.*` 8.0.x continues to be serviced
after 9.0 ships.

**Pre-releases are not covered by this.** An alpha or beta is supported only by the next pre-release:
there is no servicing of a superseded pre-release, and the remedy for a defect in one is a later build.
Until `10.0.0` ships, that is the whole support story -- everything in this section describes how the
stable line will be serviced once there is one.

### What lands on an older supported line

| Line | Bug fixes | Security fixes | New features |
|------|-----------|----------------|--------------|
| **Current major** | Yes | Yes | Yes |
| **Older, still-supported major** | Critical correctness only | Yes | No |

"Critical correctness" means a defect that loses, corrupts, mis-routes or mis-delivers data, or that
breaks a guarantee this documentation states. Every fix is written against the current line first and
backported only if it meets that bar or is a security fix. Everything else is carried by upgrading
within the major line, which is always backward compatible.

### Release cadence

**There is no fixed release calendar, and that is the policy rather than a gap in it.** Excalibur is
maintained by volunteers (see [Support](../support.md)); publishing a monthly cadence that would then
be missed is worse for you than publishing the rule actually followed.

| Release | When it is cut |
|---------|----------------|
| **Patch** (`10.0.Z`) | When a fix warrants one. A validated security fix is cut ahead of everything else |
| **Minor** (`10.Y.0`) | When a feature set is complete, tested and documented |
| **Major** (`11.0.0`) | Once per .NET major, following the .NET release |
| **Pre-release** | On demand while the next release is in development |

What you can plan against is the **ordering**, not the interval: security first, no feature release
blocks a fix, and nothing ships without the full test suite passing and the documentation for what
changed.

### Persistence schema

**Within a stable major line, no release requires you to change your database.** The storage schema --
event-store tables, outbox and inbox tables, saga and projection storage, and the document shapes used
by the non-relational providers -- is frozen at the release-candidate cut, at the same moment the API
is. A patch or minor release of `10.x` will not ask you to add a column, alter an index, or re-key a
document.

This is what makes "minor and patch are backward compatible" mean something operationally. An upgrade
that is source-compatible but needs a DBA is not a compatible upgrade, and a fix that cannot be made
without a schema change waits for the next major line rather than arriving in a patch.

| | Schema may change | What you do |
|---|---|---|
| **Between pre-releases** | Yes, in any build | Each change is listed under [Before you upgrade](../whats-new.md#before-you-upgrade) with the DDL |
| **Patch / minor within `10.x`** | **No** | Nothing |
| **At a new major line** | Yes | A migration script and a guide ship with the release |

**Who provisions the schema depends on the provider, and you should know which case you are in.** Some
providers ship their DDL inside the package under `scripts/` and never touch your database -- you apply
it, and a schema change is always something you did deliberately. Others create and maintain the tables
they own, and for those the reconciliation is part of the upgrade rather than something handed to you.
[Which packages ship scripts](#which-packages-ship-schema-scripts) names both sets.

**The commitment above holds in both cases**: within a stable major line you are never handed DDL to
apply, and a provider that maintains its own tables does not touch a schema it does not own.

### Package signing

Packages are **repository-signed by NuGet.org** and are **not author-signed** -- no code-signing
certificate has been obtained. If your supply-chain policy requires an author signature, that is the
state to plan against today. The reasoning and the criterion for changing it are in
[RELEASE.md](https://github.com/TrigintaFaces/Excalibur/blob/main/RELEASE.md#package-signing).

---

## Breaking Change Policy

Breaking changes are communicated through multiple channels:

1. **[What's New](../whats-new.md)** -- categorized changes for the release you are moving to, with
   everything that requires action from you collected under
   [Before you upgrade](../whats-new.md#before-you-upgrade). Until `10.0.0` ships, the pre-releases are
   documented cumulatively under a single heading rather than one entry per alpha.
2. **PublicAPI tracking** -- `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` files in each package track API surface changes
3. **GitHub Releases** -- Tagged releases with detailed notes
4. **Migration guides** -- For significant changes, dedicated migration documentation is provided

### During Pre-Release (Alpha/Beta)

Breaking changes may occur between any pre-release version. Review
[What's New](../whats-new.md) before upgrading for the cumulative picture, and the tagged release notes on
GitHub for what changed in a specific pre-release.

### After Stable Release

- Within a major line (a single .NET major), **minor and patch releases are always backward compatible**
- Breaking API changes are reserved for a new **major line**, which coincides with adopting a new .NET major
- Behavioral changes (same API, different behavior) are treated as breaking

### Drain in-flight messages before crossing a major boundary

**This is an obligation on you, and it cannot be met after the upgrade.**

A message already sitting on a queue or topic was written by the version that published it. When a
transport's wire format changes, the *reading* side of the old format is kept for the remainder of the
major line and removed at the next major. **A message written by `10.x` is therefore not guaranteed
readable by `11.0.0`.**

Within a stable major line this cuts one way only: reading stays compatible, because minor and patch
releases are backward compatible. **During pre-release it does not**, per the pre-release policy above --
a wire format may change between any two alphas, and each such change is listed in the release notes
with what it requires of you.

Concretely, this applies to the CloudEvents attribute naming on **AMQP 1.0** transports -- Azure Service
Bus and Azure Event Hubs. **Every released version writes the `ce-` prefix on those transports, and that
is the spelling sitting on your broker now.** A later `10.x` release will switch the write to the
`cloudEvents_` prefix that the AMQP 1.0 protocol binding assigns, and will go on reading `ce-` for the
remainder of the `10.x` line. `11.0.0` writes and reads the binding's spelling only.

**RabbitMQ is not affected and needs no drain for this.** The CloudEvents specification assigns no prefix
to AMQP 0-9-1, so that transport keeps the `ce-` spelling it uses today, on both the write and the read,
across the boundary.

**What you must do:** before deploying a major upgrade, let your consumers drain the queues and topics
the previous major wrote to, so that no message written by the old version is still in flight when the
new one starts reading. How long that takes depends on your queue depth and your
consumers' throughput, which only you can measure.

**Why we cannot do this for you:** whether your queues are empty is a fact about your infrastructure,
not about the package. The framework can guarantee that a version reads what it writes and what the
previous versions of its own major line wrote. It cannot know what is still queued on your broker.

This is the same shape as the idempotency obligation the delivery guarantees carry: the framework states
what it guarantees, and names the part that only you can hold.

## Deprecation Policy

Once stable, **a deprecated API keeps working for the remainder of the major line it was deprecated
in.** That is not a separate promise: minor and patch releases are backward compatible within a major
line, and removals are reserved for a new major line, so the deprecation window falls out of the
versioning policy rather than resting on a calendar this project does not commit to.

1. **Deprecation notice** -- The API is marked with `[Obsolete("Use X instead.")]` and documented in
   [What's New](../whats-new.md), naming the replacement
2. **It keeps working** -- for every remaining minor and patch release of that major line
3. **Removal** -- at the next major line, with a migration guide

During pre-release, deprecated APIs may be removed in any subsequent build. There is no deprecation
window before `10.0.0`.

## What Changed Between Versions

This page states the *policy*. For what actually changed in the release you are moving to:

1. **[Before you upgrade](../whats-new.md#before-you-upgrade)** -- the collected list of changes that
   require you to act, including schema columns to add and APIs that were removed. Start here.
2. **[Migration guides](index.md#upgrading-to-1000)** -- step-by-step guides for the changes that
   rewrite data you have already stored. Both current ones must be completed *before* you deploy the new
   package: [authorization grants require a tenant](authorization-tenant-required.md) and
   [Firestore and Elasticsearch inbox keys change shape](inbox-document-id-rekey.md).
3. **[GitHub Releases](https://github.com/TrigintaFaces/Excalibur/releases)** -- the per-release notes,
   with each breaking entry labelled and carrying its migration.

## Upgrading across pre-releases

Until `10.0.0` ships, **schema may change in any build**, so the upgrade is a procedure rather than a
package bump. The steps below are the order that keeps a running system correct; the reasoning behind
each is in the sections above.

1. **Read [Before you upgrade](../whats-new.md#before-you-upgrade) first.** It is the cumulative list of
   changes that require action, including DDL. Pre-releases are documented cumulatively, so reading only
   the newest entry is not enough if you are skipping builds.
2. **Bump every Excalibur package together.** They are versioned as one line; a mixed set is not a
   configuration anyone tests. A store package at a different version from the drain that uses it shows
   up at runtime as an unrecognised completion outcome, not as a build error.
3. **Take a backup that includes the outbox, the inbox, and the fence table** — the rollback in step 9
   depends on it, and the fence table is easy to miss because it is separate from the messages.
4. **Drain in-flight messages before crossing a major boundary.** See
   [Drain in-flight messages](#drain-in-flight-messages-before-crossing-a-major-boundary).
5. **Apply new schema scripts before starting the new version**, in filename order. Find them in the
   packages you already depend on — see
   [Which packages ship schema scripts](#which-packages-ship-schema-scripts). **Track which ones you have
   applied.** Most carry an existence guard and can be re-run safely, but not all do — the initial
   create-table scripts for several providers are plain DDL and will fail on a second run. Do not treat
   "re-run everything" as a safe default.
6. **Start one instance and let it validate.** Several stores refuse to start when their configuration
   and their physical schema disagree — a multi-tenant inbox store pointed at a single-tenant table
   fails fast rather than running a query with no tenant predicate. A startup failure here is the check
   working; it means step 5 was incomplete.
7. **Confirm the drain is accepted before scaling out.** A fenced outbox refuses writes from a superseded
   leadership tenure. If the new instance logs fence refusals rather than draining, resolve that before
   adding more instances.
8. **Then roll the rest.** During the roll, two package versions are live against one store. This is
   supported for schema-compatible builds and is exactly what step 1 tells you about when it is not.
9. **To roll back, reverse the order.** Reverting the package alone is sufficient only while the newer
   version has not yet written data. Once it has, complete the rollback by restoring the backup from
   step 3 — and restore the fence table with it, or the older build's tokens may sit below a high-water
   the newer one advanced.

### Which packages ship schema scripts

These packages carry their DDL inside the `.nupkg` under `scripts/`. **They never create tables at
runtime** — you apply the scripts, so a schema change is always deliberate.

| Package | Scripts |
|---|---|
| `Excalibur.AuditLogging.Postgres` | `001_CreateAuditSchema`, `002_NarrowTenantIdToPortableMaximum` |
| `Excalibur.AuditLogging.SqlServer` | `001_CreateAuditSchema` |
| `Excalibur.Cdc.Postgres` | `001_CreateCdcStateSchema` |
| `Excalibur.Cdc.SqlServer` | `001_CreateCdcStateSchema`, `002_CreateCdcIdempotencySchema` |
| `Excalibur.Compliance.Postgres` | `001_CreateComplianceSchema` … `004_ConvergeDefaultToUntenanted` |
| `Excalibur.Compliance.SqlServer` | `001_CreateComplianceSchema` … `007_ConvergeDefaultToUntenanted` (includes `002_CreateKeyEscrowSchema`) |
| `Excalibur.Data.DataProcessing` | `001_CreateDataProcessingSchema` |
| `Excalibur.Data.IdentityMap.SqlServer` | `CreateIdentityMapTable` |
| `Excalibur.Data.Postgres` | `001_CreateDeadLetterSchema`, `002_CreateActivityGroupSchema` |
| `Excalibur.Data.SqlServer` | `001_CreateDeadLetterSchema`, `002_CreateActivityGroupSchema` |
| `Excalibur.Dispatch` | `schema` (poison-message store) |
| `Excalibur.EventSourcing.Oracle` | `001_CreateSnapshotSchema` … `006_NarrowSnapshotTenantIdToPortableMaximum` |
| `Excalibur.EventSourcing.Postgres` | `001_CreateSnapshotSchema` … `008_CreateCursorMapSchema` |
| `Excalibur.EventSourcing.Sqlite` | `001_CreateEventStoreSchema`, `002_MakeEventAndSnapshotIdentityTenantScoped` |
| `Excalibur.EventSourcing.SqlServer` | `001_CreateEventStoreSchema` … `009_MakeSnapshotKeyFitTheIndexLimit` |
| `Excalibur.Inbox.Oracle` | `001_CreateInboxSchema` **or** `001_CreateInboxSchema.MultiTenant`, then `002_MigrateToMultiTenant`, `003_NarrowTenantIdToPortableMaximum` |
| `Excalibur.Inbox.Postgres` | as above |
| `Excalibur.Inbox.SqlServer` | as above |
| `Excalibur.Jobs.SqlServer` | `001_CreateJobCoordinationSchema` |
| `Excalibur.LeaderElection.Postgres` | `001_CreateLeaderElectionHealthSchema` |
| `Excalibur.LeaderElection.SqlServer` | `001_CreateLeaderElectionHealthSchema` |
| `Excalibur.Outbox.Oracle` | `001_CreateOutboxSchema`, `002_MakeOutboxTenantTotal`, `003_CarryTenantOnDeadLetters` |
| `Excalibur.Outbox.Postgres` | `001_CreateOutboxSchema`, `002_MakeOutboxTenantTotal`, `003_CarryTenantOnDeadLetters` |
| `Excalibur.Outbox.SqlServer` | `001_CreateOutboxSchema`, `002_NarrowTenantIdToPortableMaximum` |
| `Excalibur.Saga.Oracle` | `01-SagaSchema`, `01-SagaSchema.Upgrade`, `SagaTimeouts`, `SagaTimeouts.Upgrade` |
| `Excalibur.Saga.Postgres` | `01-SagaSchema`, `02-NarrowTenantIdToPortableMaximum` |
| `Excalibur.Saga.SqlServer` | `01-SagaSchema`, `02-SagaCorrelationIndex`, `02-SagaMonitoringSchema`, `03-NarrowTenantIdToPortableMaximum`, `SagaTimeouts`, `SagaTimeouts.Upgrade` |
| `Excalibur.Workflows.SqlServer` | `001_CreateWorkflowSignalInboxSchema`, `002_MakeWorkflowSignalInboxTenantTotal` |

**The inbox scripts are a choice, not a sequence.** Apply `001_CreateInboxSchema.sql` for a
single-tenant deployment or `001_CreateInboxSchema.MultiTenant.sql` for a multi-tenant one. Applying the
wrong one is caught at startup rather than at the first message.

**Every script uses the default schema, table, and index names.** Each carries a header naming the
options that control them — for example `SqlServerInboxOptions.SchemaName` and `.TableName`. If you
override a name in configuration, rename the objects in the script to match.

#### Where to find them

The `scripts/` folder is inside the package, not copied to your build output. Read it from the NuGet
global packages folder:

```bash
# Linux / macOS
ls ~/.nuget/packages/excalibur.outbox.sqlserver/<version>/scripts/
```

```powershell
# Windows
Get-ChildItem "$env:USERPROFILE\.nuget\packages\excalibur.outbox.sqlserver\<version>\scripts\"
```

Or extract them from the `.nupkg` directly — it is an ordinary zip archive.

#### Providers that manage their own storage

The document stores — **Cosmos DB** and **DynamoDB** — create the containers and tables they own on
first use, for the event store, snapshots, outbox, inbox, sagas, projections, and CDC state. There is no
script to apply and no upgrade step; provisioning and throughput settings stay yours.

One relational store is deliberately in between: the **SQL Server materialized-view store** exposes
`EnsureSchemaAsync`, which creates its view table and its position table when you call it. It is
idempotent and safe at startup, but it is something you invoke, not something that happens behind you.

#### Running your own migrations

For migrations you author — upcasting stored events, backfilling a new column, rewriting a projection's
storage — the SQL Server and PostgreSQL packages ship a migration runner. It executes scripts embedded as
resources in an assembly you supply, records what it has applied, serializes concurrently starting
instances behind a database lock, and reports when an already-applied script's checksum no longer
matches.

```csharp
services.AddSqlServerMigrator(options =>
{
    options.ConnectionString = connectionString;
    options.MigrationAssembly = typeof(Program).Assembly;
    options.MigrationNamespace = "MyApp.Migrations";
    options.AutoMigrateOnStartup = true;
});
```

```csharp
public sealed class UpgradeCheck(IMigrator migrator)
{
    public async Task<MigrationResult> ApplyAsync(CancellationToken cancellationToken)
    {
        var applied = await migrator.GetAppliedMigrationsAsync(cancellationToken);
        // Inspect `applied` before migrating if you want a dry run.

        return await migrator.MigrateAsync(cancellationToken);
    }
}
```

**Never edit a script that has already been applied.** The runner records a checksum for each one and
reports drift; an edited script no longer describes the database you have. Add a new script instead.

## Upgrade Best Practices

1. **Read [What's New](../whats-new.md)** -- Check for breaking changes and migration notes before upgrading
2. **Test before upgrading** -- Run your full test suite on the current version
3. **Upgrade in staging first** -- Validate in a non-production environment
4. **Back up persistence stores** -- Event stores, outbox tables, and saga stores before major upgrades
5. **Plan rollback** -- Always have a rollback strategy for production deployments. Reverting the
   package alone is only sufficient while the newer version has not yet written data. Once it has, the
   rollback is completed by restoring the backup from step 4: a version reads what it writes and what
   earlier versions of its own major line wrote, so an earlier version is not guaranteed to read data a
   later one produced.

## Subscribing to Updates

Stay informed about releases and changes:

- **GitHub Releases** -- Watch the [Excalibur repository](https://github.com/TrigintaFaces/Excalibur/releases) for release notifications
- **[What's New](../whats-new.md)** -- The change history for the release you are on, categorized by area
- **NuGet** -- Configure NuGet notifications for `Excalibur.Dispatch` and other packages you depend on

## See Also

- [Migration Overview](index.md) -- All migration guides
- [Before you upgrade](../whats-new.md#before-you-upgrade) -- What changed in this release, and what to do about it
- [From MediatR](from-mediatr.md) -- MediatR migration guide
- [Getting Started](../getting-started/index.md) -- New project setup from scratch
