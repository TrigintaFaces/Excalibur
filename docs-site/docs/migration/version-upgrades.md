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

## Pending changes after alpha.21

These changes describe the development line after `10.0.0-alpha.21`; they do not announce a published release. Rebuild consumers against the next package set and review its release notes before upgrading.

- **Leader identity configuration:** `LeaderElectionOptions.InstanceId` is nullable and unset by default. Providers resolve the configured value, an environment identity, or a generated identifier. Read the election's `CandidateId` for the resolved identity. Replace assignments to `KubernetesLeaderElectionOptions.CandidateId` with inherited `InstanceId`; the election's `CandidateId` property remains available. A generated identifier is not stable across process restarts.
- **Erasure construction:** direct `ErasureService` callers must supply the required `IKeyMintLedger` representing their key mint history. The required `IErasureRetentionRegistry` remains required; the optional trailing retention declaration ledger does not replace either dependency. Null is refused. Use the framework's DI registration with the appropriate durable stores and ledgers.
- **Telemetry constructors:** `TelemetryLeaderElection` and `TelemetryLeaderElectionFactory` now accept an optional trailing `TimeProvider`. Existing four-argument source calls remain valid after recompilation, but the CLR constructor signatures changed, so existing binaries must be rebuilt. Preserve borrowed logging and instrumentation through notification completion; resource disposal and notification delivery have separate lifetimes. See [leader-election lifecycle guidance](../leader-election/index.md#notification-lifetime-and-shutdown).
- **Custom election implementations:** implement the stable `NotificationsCompleted` task required by `ILeaderElection`. Stop/restart leaves it pending; permanent disposal seals new publication. The terminal task also joins admitted synchronous invocations; queued callbacks may still run after resource disposal. Observe resource and notification failures separately during final teardown.
- **Erasure conformance overrides:** rename `UpdateStatusAsync_SecondClaimOfTheSameRequest_ShouldBeRefused` to `ClaimForExecutionAsync_SecondClaimOfTheSameRequest_ShouldBeRefused`, and `UpdateStatusAsync_ToInProgress_ShouldSetExecutedAt` to `ClaimForExecutionAsync_ShouldSetExecutedAt`. These controls exercise explicit execution claims rather than a status-update shortcut.
- **Custom dead-letter and outbox stores:** implement `IDeadLetterStore.ReleaseReplayClaimAsync` and return the typed replay outcome from `MarkAsReplayedAsync`. Replay only after `DeadLetterReplayOutcome.Claimed`; release an unsuccessful replay's claim. Fenced outbox completion now returns `OutboxCompletionOutcome`: only `Applied` means the row was mutated. Preserve atomic claims, exact owner/fence checks and tenant scope; a completed task or a default enum value is not success.
- **Message context collections:** replace removed `IMessageContext.GetHeaders` and `IMessageContext.GetAttributes` shortcuts with the intended data path. Use `Items` for request-local scratch data and `SetPropagatedItem` for explicitly bounded cross-process propagation. The separate `IMessageMetadata.GetAttributes` extension remains a read-only metadata view.
- **CloudEvents payload metadata:** replace `CloudEventOptions.PayloadTypeInfoResolver` configuration with registration-time `AddCloudEvents(PayloadJsonContext.Default)`. Direct serializer consumers can use `CloudEventSerializer.CreateWithMetadata(CloudEventPayloadSerialization.CreateOptions(PayloadJsonContext.Default))`. The legacy `CloudEventSerializer` constructor retains the SDK formatter; options carrying a resolver do not select the metadata-aware formatter. Supply generated metadata for every payload type. The parameterless registration overload selects the reflective path and carries trimming/NativeAOT warnings; it is not the generated-metadata alternative.
- **Custom scheduler and sharding implementations:** preserve the required `ScheduleExecutionLease.AdmissionAttempt` history across claims and implement `ITenantShardMap.GetShardById`. These members carry execution-budget and physical-shard identity semantics; do not synthesize a success result or reset admission history to satisfy the new signatures.
- **Custom erasure stores:** implement explicit execution claims and identity-bearing fenced writes returning `ErasureWriteOutcome`. Only `Applied` reports a durable mutation. Background-query stores must provide `GetHoldDeferredRequestsAsync`, and legal-hold query implementations must honor the supplied `asOf` boundary. Update provider conformance tests and retain tenant, claim, hold-history and durable-ledger guarantees.
- **Saga compensation:** recording a pending compensation changes in-memory state; durability starts at the state save. It does not atomically record an external effect or supply automatic durable retries for failed compensations. Use idempotent actions and explicit host recovery for crash windows; the saga's buffered command/event helpers do not make direct external calls atomic.

## SQL schema script paths in earlier alpha packages

Earlier packages in the following ranges contain SQL schema entries at `scripts//<name>.sql`, with two
slashes, rather than the documented `scripts/<name>.sql`. Tooling that expects the documented path can
miss the files, and strict package readers can reject the malformed entry. The schema scripts are
needed to provision the tables the providers use.

| Package | Measured affected versions | Corrected version verified |
| --- | --- | --- |
| `Excalibur.AuditLogging.SqlServer` | `10.0.0-alpha.4` through `10.0.0-alpha.14` | `10.0.0-alpha.15` |
| `Excalibur.Cdc.SqlServer` | `10.0.0-alpha.6` through `10.0.0-alpha.14` | `10.0.0-alpha.15` |
| `Excalibur.Data.SqlServer` | `10.0.0-alpha.9` through `10.0.0-alpha.14` | `10.0.0-alpha.15` |
| `Excalibur.Compliance.Postgres` | `10.0.0-alpha.9` through `10.0.0-alpha.14` | `10.0.0-alpha.15` |
| `Excalibur.Outbox.Postgres` | `10.0.0-alpha.9` through `10.0.0-alpha.14` | `10.0.0-alpha.15` |

**Remedy:** upgrade to `10.0.0-alpha.15` or a later fixed release, then obtain the scripts from the
corrected package and follow that provider's schema setup instructions. Do not rely on ZIP path
normalization or edit a published package as an upgrade strategy. Upgrading the package does not itself
apply its SQL scripts to an existing database.

These ranges were established by inspecting every published 10.x package from `10.0.0-alpha.4` through
`10.0.0-alpha.15` for each package above. Earlier 10.x versions outside the listed ranges contained no SQL
entries. Selected first and last 3.x packages also contained no SQL entries; intervening 3.x versions
were not assessed. This inspection verifies package entry names, not successful database provisioning.

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

**There is a third case, and it is neither of those: storage the framework reads and writes but does not
provision.** Projection state is the one that exists today — no package ships a script for a projection
table, and no provider creates one, so the schema is yours entirely and a change to what the framework
expects there arrives as a row in [Before you upgrade](../whats-new.md#before-you-upgrade) rather than as a
script. Do not go looking for a script that does not exist.

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
2. **Bump every Excalibur package together inside each application.** Use one coordinated release set;
   mixing an old store package with a newer drain is not a supported upgrade shortcut. It can fail at
   runtime as an unrecognised completion outcome rather than as a build error. This is separate from
   whether two hosts running different complete release sets may share a store during a roll.
3. **Take a backup that includes the outbox, the inbox, and the fence table** — the rollback in step 9
   depends on it, and the fence table is easy to miss because it is separate from the messages.
4. **Drain in-flight messages before crossing a major boundary.** See
   [Drain in-flight messages](#drain-in-flight-messages-before-crossing-a-major-boundary).
5. **Observe the selected provider's offline or quiescence requirements before applying schema.**
   Unless the exact release/provider procedure explicitly permits old and new writers together, drain
   and stop the old writers first. Additive DDL or schema compatibility alone does not establish that a
   mixed-version runtime is safe, and some providers' DDL commits implicitly. Then apply the new scripts
   before starting the new version, in filename order. Find them in the packages you already depend on —
   see [Which packages ship schema scripts](#which-packages-ship-schema-scripts). **Track which ones you
   have applied.** Most carry an existence guard and can be re-run safely, but not all do — the initial
   create-table scripts for several providers are plain DDL and will fail on a second run. Do not treat
   "re-run everything" as a safe default.
6. **Start one instance and let it validate.** Several stores refuse to start when their configuration
   and their physical schema disagree — a multi-tenant inbox store pointed at a single-tenant table
   fails fast rather than running a query with no tenant predicate. A startup failure here is the check
   working; it means step 5 was incomplete.
7. **Confirm the drain is accepted before scaling out.** A fenced outbox refuses writes from a superseded
   leadership tenure. If the new instance logs fence refusals rather than draining, resolve that before
   adding more instances.
8. **Scale or roll according to that release/provider procedure.** Two hosts may run different release
   sets against one store only where the procedure explicitly supports both binaries, stored message
   envelopes, schema and concurrent writers. Do not infer that support from schema compatibility alone.
   Where no such rolling procedure is specified, keep old writers stopped, validate the first new
   instance, then scale the coordinated new release set. This preserves provider-supported rolling
   upgrades without promising one for every prerelease change.
9. **To roll back, reverse the order.** Reverting the package alone is sufficient only while the newer
   version has not yet written data. Once it has, complete the rollback by restoring the backup from
   step 3 — and restore the fence table with it, or the older build's tokens may sit below a high-water
   the newer one advanced.

### Which packages ship schema scripts

These packages carry their DDL inside the `.nupkg` under `scripts/`. **By default they never create
tables at runtime** — you apply the scripts, so a schema change is always deliberate. (The compliance
stores expose an `AutoCreateSchema` option, default `false`; turning it on moves those packages to
runtime provisioning.)

:::caution The package is the authority, not this table
**List the `scripts/` folder of the version you are installing and apply what is actually there.**
This table is a convenience and it has drifted behind the packages before — a hand-maintained index of
a growing set will do that again. If a row here disagrees with the folder, the folder is right.

Two packages keep their SQL in a differently-named source folder but still ship it at `scripts/` in the
package, so the folder listing finds them either way.
:::

| Package | Scripts |
|---|---|
| `Excalibur.AuditLogging.Postgres` | `001_CreateAuditSchema` |
| `Excalibur.AuditLogging.SqlServer` | `001_CreateAuditSchema` |
| `Excalibur.Cdc.Postgres` | `001_CreateCdcStateSchema` |
| `Excalibur.Cdc.SqlServer` | `001_CreateCdcStateSchema`, `002_CreateCdcIdempotencySchema` |
| `Excalibur.Compliance.Postgres` | `001_CreateComplianceSchema` |
| `Excalibur.Compliance.SqlServer` | `001_CreateComplianceSchema`, `002_CreateKeyEscrowSchema` |
| `Excalibur.Data.DataProcessing` | `001_CreateDataProcessingSchema` |
| `Excalibur.Data.IdentityMap.SqlServer` | `CreateIdentityMapTable` |
| `Excalibur.Data.Postgres` | `001_CreateDeadLetterSchema`, `002_CreateActivityGroupSchema`, `003_CreateGrantSchema` |
| `Excalibur.Data.SqlServer` | `001_CreateDeadLetterSchema`, `002_CreateActivityGroupSchema`, `003_CreateGrantSchema` |
| `Excalibur.Dispatch` | `schema` (poison-message store) |
| `Excalibur.EventSourcing.Oracle` | `001_CreateSnapshotSchema`, `002_CreateEventStoreSchema`, `003_CreateSubscriptionCheckpointSchema` |
| `Excalibur.EventSourcing.Postgres` | `001_CreateSnapshotSchema`, `002_CreateEventStoreSchema`, `003_CreateCursorMapSchema`, `004_CreateSubscriptionCheckpointSchema` |
| `Excalibur.EventSourcing.Sqlite` | `001_CreateEventStoreSchema`, `002_CreateSubscriptionCheckpointSchema` |
| `Excalibur.EventSourcing.SqlServer` | `001_CreateEventStoreSchema`, `002_CreateSnapshotSchema`, `003_CreateCursorMapSchema`, `004_CreateSubscriptionCheckpointSchema` |
| `Excalibur.Inbox.Oracle` | `001_CreateInboxSchema` **or** `001_CreateInboxSchema.MultiTenant` — pick one at provisioning time |
| `Excalibur.Inbox.Postgres` | `001_CreateInboxSchema` **or** `001_CreateInboxSchema.MultiTenant` — pick one at provisioning time |
| `Excalibur.Inbox.SqlServer` | `001_CreateInboxSchema` **or** `001_CreateInboxSchema.MultiTenant` — pick one at provisioning time |
| `Excalibur.Jobs.SqlServer` | `001_CreateJobCoordinationSchema` |
| `Excalibur.LeaderElection.Postgres` | `001_CreateLeaderElectionHealthSchema` |
| `Excalibur.LeaderElection.SqlServer` | `001_CreateLeaderElectionHealthSchema` |
| `Excalibur.Outbox.Oracle` | `001_CreateOutboxSchema` |
| `Excalibur.Outbox.Postgres` | `001_CreateOutboxSchema` |
| `Excalibur.Outbox.SqlServer` | `001_CreateOutboxSchema` |
| `Excalibur.Saga.Oracle` | `01-SagaSchema`, `SagaTimeouts` |
| `Excalibur.Saga.Postgres` | `01-SagaSchema` |
| `Excalibur.Saga.SqlServer` | `01-SagaSchema`, `02-SagaCorrelationIndex`, `02-SagaMonitoringSchema`, `SagaTimeouts` |
| `Excalibur.Workflows.SqlServer` | `001_CreateWorkflowSignalInboxSchema` |

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


## Unreleased: next-alpha outbox and serialization migration

This section describes pending development changes, not a published alpha or a release certification.
Rebuild custom providers, decorators and builders against the complete next package set. An additive public
API inventory with zero removals can still contain source-breaking interface changes and changed behavior.

### Custom implementations: required interface additions

Existing implementations of these interfaces must implement the new required member:

| Existing interface | Required addition |
|---|---|
| `IClaimScopedOutboxStore` | `MarkSentAsync(messageId, claimIdentity, cancellationToken)` returning `ValueTask<OutboxCompletionOutcome>` |
| `IFencedClaimScopedOutboxStore` | `MarkSentAsync(messageId, OutboxWriteAuthority, cancellationToken)` returning `ValueTask<OutboxCompletionOutcome>` |
| `IFencedDeadLetterableOutboxStore` | `MarkDeadLetteredAsync(messageId, reason, attemptCount, OutboxWriteAuthority, cancellationToken)` returning `ValueTask<OutboxCompletionOutcome>` |
| `IElasticSearchOutboxBuilder` | `SourceSerializer(IJsonTypeInfoResolver)` returning the builder |

These are required members on existing interfaces, not default implementations. Do not satisfy them with
an adapter that discards acquisition or tenure evidence, calls an ID-only legacy write, or returns `Applied`
without its own durable mutation. The new `IClaimScopedDeadLetterableOutboxStore` is a separate capability
facet of the owning outbox store, not a separately registered tenant store.

### Claims, typed outcomes and tenant provenance

Capture the persisted message ID and opaque `DispatcherId` immediately after acquisition, before application
or transport callbacks. Completion must use that immutable receipt; a stable process ID or a map overwritten
by a later acquisition is insufficient. Preserve exact acquisition identity, including significant characters,
and live-lease checks. Fenced routes additionally require the captured positive tenure in `OutboxWriteAuthority`.

Decide authority and the resulting write in the same atomic operation. For valid fenced authority, judge the
fence before row-level outcomes. `Applied` alone means the mutation occurred; `ClaimLost`, `AlreadyTerminal`
and `MessageNotFound` do not mean success. A row-level refusal stops writes for that row; `FenceRefused` ends
admission for the captured tenure. `Unknown` (including the enum default) is not applied. Return refusals as
typed outcomes; propagate infrastructure failures and cancellation rather than converting them to success.
Do not classify a completed mutation by a later unguarded read.

Completion addresses the owning store's row by globally persisted ID and acquisition. Preserve or copy the
stored tenant provenance during terminal movement. Do not add an ambient tenant predicate that prevents a
cross-tenant worker from completing its claimed row; tenant-facing downstream work establishes its scope
from the recorded owner. The primary store still needs the row-partition capability required by multi-tenancy.
See [outbox ownership and completion](../patterns/outbox.md) and the
[PostgreSQL claim guide](../patterns/postgres-outbox-claims.md).

### Worker admission and staging-only hosts

Custom stores and decorators must expose capabilities through the owning store's `IServiceProvider.GetService`
seam. A concrete cast or a standalone DI registration cannot substitute for a capability the owner withholds.
Decorators must preserve honest absence as well as exact argument, outcome and exception forwarding.

Plain publishing needs `IClaimScopedOutboxStore`; its elected fenced route also needs `IFencedOutboxStore` and
`IFencedClaimScopedOutboxStore`. `OutboxProcessor` additionally requires the selected terminal capability:
`IClaimScopedDeadLetterableOutboxStore` for plain processing or `IFencedDeadLetterableOutboxStore` for fenced
processing, even with explicitly selected `NullDeadLetterQueue`. A publisher route that never invokes that
terminal operation does not acquire the processor's extra requirement. Missing capabilities fail worker
composition rather than falling back to weaker writes. `AsSingleWriter()` removes the leadership-fencing
requirement, not acquisition ownership: one process can reclaim its own expired lease.

A staging-only host does not acquire or complete rows. Configuring leader election alone does not activate
a worker; validate these requirements in the host that actually drains, including manually constructed
publishers/processors. The existing direct-use legacy methods retain their contracts, but do not satisfy the
stronger framework-worker capability contract.

### Publishing results

`PublishingResult.IsSuccess` now requires both zero failed rows and `Outcome == PublishingOutcome.Completed`.
Check the operation verdict rather than `FailureCount == 0` or `SuccessRate` alone. `TenureRefused` and
`Interrupted` can accompany transport acceptances already counted as successes; neither fabricates failed
rows for work never attempted. Unknown outcomes do not imply success. If refusal and interruption both occur,
refusal takes precedence while actual errors remain available. Counts do not establish durable settlement,
and a refused or interrupted operation does not roll back earlier transport effects. Keep consumers idempotent.

### Authority-domain identity

Custom gate wrappers can implement `ILeaderProcessingAuthorityDomain` to share one stable, nonnull reference
identity for the same monotonic token sequence. This identity is neither authorization nor a token. Do not
reuse it across unrelated elections or reset a sequence while retaining it; the framework does not dispose
it. Without the capability, a gate's own object reference identifies its domain. See the
[outbox guide](../patterns/outbox.md) and the existing
authority sample controls in `samples/10-aot/Excalibur.Dispatch.Aot.Sample/OutboxAuthoritySmoke.cs` (source reference; the checked public mirror did not expose this file, so this is not a published-control link).

### Oracle: borrowed staging and exclusively owned workers

Staging retains its borrowed `IDb` connection and caller-transaction ownership. Worker claims and strict
completion require a fresh exclusively owned connection and private transaction, so a worker commit cannot
commit unrelated caller work. `UseOracle` connection-string configuration supplies the worker source from
the originally configured string. A factory-only staging configuration supplies an explicit
`WorkerConnectionFactory(Func<IServiceProvider, CancellationToken, ValueTask<OracleConnection>>)` when it
also needs workers; a borrowed staging factory is not reinterpreted as an ownership guarantee or cloned for
credentials. Worker admission fails if the required owned route is unavailable; staging-only use remains valid.

Return a new, closed, unenlisted connection targeting the same staging database on each successful callback.
Never return a cached, borrowed, DI-owned or already-owned connection. Successful return transfers connection
ownership to the framework; the callback retains responsibility for resources it fails before returning.
The callback's DI scope remains alive through connection disposal. Explicit worker configuration survives
later primary selection and takes precedence over string-derived defaults. Oracle fences the resolved
physical-table scope; this does not introduce per-document fencing-token state. See the
[Oracle provider reference](../data-providers/oracle.md).

### SQL Server: explicit generated header metadata

For trimming/NativeAOT callers, select the SQL Server overload with a required `OutboxMetadataSerialization`
argument. The six entry points are the outbox builder's `UseSqlServer`, three `AddSqlServerOutboxStore`
overloads (including connection-factory and typed-database forms), and two Dispatch-builder
`UseSqlServerOutboxStore` overloads. Their legacy counterparts retain `RequiresUnreferencedCode` and
`RequiresDynamicCode`; registering an optional service later cannot erase a static warning.

Construct metadata with an options-aware factory, for example
`new OutboxMetadataSerialization(options => new ApplicationHeaderContext(options))`.
Each constructed store invokes the factory once, binds its own canonical options and freezes them. Construct
the generated context with those supplied options, not the context's differently bound `Default` instance.
Preserve canonical settings and supply metadata for every actual header value type, including nested
object-valued types. A null/wrongly bound resolver, altered canonical settings or missing metadata fails;
there is no implicit reflection fallback on this selected path. Captured mutable factory state remains the
application's responsibility. Payload materialization is a separate codec choice.

`AddOutboxMetadataSerialization` and `WithMetadataSerialization` register an optional common default without
registering workers/stores. Re-registering the same instance is idempotent; conflicting, keyed, implementation-type
or opaque factory registrations are rejected without invoking factories or building an intermediate provider.
An explicit provider argument wins for that store without changing the common default. Existing SQL aliases
still select one store; these overloads do not create independently named SQL stores in one service provider.
The managed legacy path retains its annotated reflection alternative.

See [generated SQL header metadata](../patterns/outbox.md#generated-sql-server-header-metadata) for complete
registration examples. The source sample's `--native-sql-paths-smoke` control (`samples/10-aot/Excalibur.Dispatch.Aot.Sample/README.md`, “Native SQL execution control”; that section was not present in the checked public mirror)
is a separate CDC/data-task control, not a public outbox-header-metadata execution proof. The pinned
`Microsoft.Data.SqlClient` 7.1.1 has an accepted upstream NativeAOT limitation: full SQL Server-to-Elasticsearch
native publication and execution are **not certified**. The alpha's bounded diagnostic policy is recorded in
[the inline NativeAOT limitation record](https://github.com/TrigintaFaces/Excalibur/blob/main/eng/ci/inline-native-aot-known-diagnostics.json);
it is not a successful native publication or a general AOT waiver. Header metadata alone does not certify
every provider, transport, authentication mode, payload codec or deployment as NativeAOT-compatible.

### Dispatch and Elasticsearch serializer metadata

`SystemTextJsonSerializer.Create(JsonSerializerContext)` borrows an explicit context without modifying or
disposing it; it adds no framework reflection fallback. Supply all payload and runtime polymorphic metadata.
The legacy options/default constructors retain trimming/dynamic-code annotations. Missing or wrong-type
metadata fails; custom contexts and converters retain their own AOT obligations. See
[serializer configuration](../middleware/serialization-providers.md).

For framework-owned Elasticsearch clients, `IElasticSearchOutboxBuilder.SourceSerializer` supplies application
metadata alongside the framework's persisted-document metadata. For a borrowed client, configure its serializer
at original construction with `ElasticsearchOutboxSerializer.Create(settings, additionalTypes)`; the store does
not replace that serializer or own its transport. Null `additionalTypes` intentionally supplies only the
framework's built-in metadata. Supply generated metadata for custom header values and other shared-client
documents; framework metadata takes precedence. An explicitly selected reflection resolver remains a managed
alternative with its own trimming/dynamic-code requirements. Missing metadata is an error, not permission to
drop values or stringify them. See the [Elasticsearch provider reference](../data-providers/elasticsearch.md)
and [AOT migration guidance](../advanced/aot-migration-guide.md).


### Next alpha (unreleased): workflow payload registration boundary

`AddWorkflows()` retains its ordinary JIT runtime behavior but now declares its reflection-based payload
requirements. If you supplied `WorkflowOptions.PayloadTypeInfoResolver` after that call, replace the pair
with `services.AddWorkflows(WorkflowPayloadContext.Default)` to use the non-null, annotation-free metadata
route. Cover your activity, workflow and signal payload types in that generated context; unknown metadata
fails instead of falling back to reflection. Keep unrelated `Configure<WorkflowOptions>` settings.

Repeated identical explicit resolvers are allowed; contradictory explicit resolver registrations or a
conflicting non-null options resolver fail loudly. A parameterless call before or after the explicit one
cannot switch the provider back to reflection. Options are composed once per provider and frozen; existing
providers do not change when the source service collection is later extended. No public workflow execution
signature was removed. This is an unreleased change, not a claim that a new alpha has been published.
