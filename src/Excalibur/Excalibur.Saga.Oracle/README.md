# Excalibur.Saga.Oracle

Oracle Database implementation of the Excalibur saga store and saga timeout store.

Provides durable saga state persistence with optimistic-concurrency control (numeric `Version`
compare-and-swap) using Oracle as the backing store, mirroring `Excalibur.Saga.SqlServer`.

## Schema

This package provisions two independent tables, and neither store creates its table at runtime.
A deployment that uses saga state but not durable timeouts needs only the first.

### Saga state

The saga store reads and writes `DISPATCH.SAGAS`. Create it before the first saga is started:

```
scripts/01-SagaSchema.sql
```

```sh
sqlplus user/password@//host:1521/service @01-SagaSchema.sql
```

Two properties of that table are worth knowing before you provision it:

- **The tenant discriminator is part of the primary key**, which is `(TenantId, SagaId)`. Sagas are
  correlated by a business key such as an order id, so two tenants can legitimately hold the same
  `SagaId`; keyed on `SagaId` alone, one tenant's saga would overwrite the other's. A saga that is
  genuinely not tenant-scoped stores a reserved sentinel, never `NULL` — on Oracle especially, since
  Oracle stores the empty string as `NULL` and a `NULL` tenant is unaddressable by any equality
  predicate.
- **`CompletedAt` drives retention** and is indexed for the purge. It is `TIMESTAMP WITH TIME ZONE`
  while `CreatedUtc` and `UpdatedUtc` are bare `TIMESTAMP`: the latter two are stamped server-side in
  UTC, whereas `CompletedAt` is a consumer-supplied instant whose offset must survive the round trip.

The schema and table names default to `DISPATCH` and `SAGAS` and are configurable through
`OracleSagaStoreOptions`. If you change either, edit the script to match before running it.

### Saga timeouts

The saga timeout store queries `DISPATCH.SAGATIMEOUTS`. Create it before first use — the store does
not create it for you. The DDL ships inside this package under `scripts/`:

```
scripts/SagaTimeouts.sql
```

Run it against your target schema:

```sh
sqlplus user/password@//host:1521/service @SagaTimeouts.sql
```

`DueAt`, `ScheduledAt`, and `ClaimedAt` are `TIMESTAMP WITH TIME ZONE`, not bare `TIMESTAMP`: the store
reads and writes `DateTimeOffset`, and a bare `TIMESTAMP` drops the offset, so the instant would change
across a round trip.

The schema and table names default to `DISPATCH` and `SAGATIMEOUTS` and are configurable through
`OracleSagaTimeoutStoreOptions`. If you change either, edit the script to match before running it.

## Usage

```csharp
services.AddExcalibur(x => x.AddSagas(saga =>
{
    saga.UseOracle(oracle =>
    {
        oracle.ConnectionString(connectionString)
              .SchemaName("DISPATCH")
              .TableName("SAGAS");
    });
}));
```

Oracle folds a zero-length string to `NULL`, so identity columns are normalized to a non-empty
sentinel at the store boundary and paging uses ANSI `OFFSET ... ROWS FETCH NEXT ... ROWS ONLY`.

## Driver license

This package depends on `Oracle.ManagedDataAccess.Core`, Oracle's own ODP.NET Core driver. It is
**not** distributed under an OSI-approved open-source license. Its `LICENSE.txt` opens:

> Your use of this Program is governed by the Oracle Free Distribution, Hosting, and Use Terms and
> Conditions set forth below, unless you have received this Program (alone or as part of another
> Oracle product) under an Oracle license agreement (including but not limited to the Oracle Master
> Agreement), in which case your use of this Program is governed solely by such license agreement
> with Oracle.

Excalibur redistributes no Oracle software and asserts nothing about your eligibility on your
behalf. Referencing this package makes NuGet install the driver into your application, so the
obligations are yours. Read the terms shipped in the driver package before you deploy, and confirm
your deployment is covered -- the free terms carry conditions that the MIT and PostgreSQL licenses
of Excalibur's other database drivers do not.

If those terms do not suit you, no other Excalibur provider carries them: the SQL Server, MySQL and
SQLite drivers are MIT and Npgsql is the PostgreSQL license. Every dependency's license is listed in
`THIRD-PARTY-NOTICES.md` in the repository.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.

## Persisted ordinary timer recovery

The ordinary saga coordinator retains its original timer IDs and intent journal with state/dedup, then conditionally applies it through the selected timeout store. For this profile apply the shipped STATE migration `02-SagaTimerRecoveryState.sql` and TIMEOUT migration `03-SagaTimerRecovery.sql` after the base state/timeout scripts, before deploying nonce-aware state/coordinator binaries. Fresh `01-SagaSchema.sql` already includes the state fields. There is no automatic schema migration. Quiesce old writers during rollout; concurrent binaries that omit lifetime/receiver metadata are not certified.

Script03 defaults to `DISPATCH.SAGATIMEOUTS`. Adapt its owner, payload-table and index names for a configured direct table; register that actual table incarnation. Views/synonyms are not recovery participants. A dropped/recreated or unregistered target is not silently adopted by an old journal. All metadata and physical mutations share one Oracle transaction and one exact tenant+saga GUID domain; state type does not create a separate cancellation namespace.

`ISagaTimerRecoveryStore` is an optional facet through the selected `ISagaTimeoutStore`/`IServiceProvider`. The journal never changes IDs or silently refreshes its captured cancellation revision on an uncertain acknowledgement. Exact replay uses retained receipt; public cancel records durable cutoffs; new untracked public IDs remain usable. A tracked retired/cancelled ID cannot be reallocated through raw Schedule. Claim retirement retains authority, and original queued-delivery metadata is checked independently of physical payload presence. This is at-least-once orchestration, not exactly-once handler execution.

Existing standalone public Schedule/Cancel/Claim/GetDue/retirement operations retain their no03 behavior for a selected direct table with zero owned receiver columns, including a distinct custom table when owner-level receiver tables already exist for another payload table. Partial selected receiver columns fail actionably. A registered selected table also refuses if all owned columns disappear; dropping columns never downgrades retained authority to legacy behavior. Shared metadata does not enroll an unrelated table; explicitly selecting it for coordinator recovery still requires script03 and that actual table registration. Enabling the new ordinary coordinator with a configured Oracle timeout store requires complete state02/receiver03; missing schema is refused before timer-owning state commits. Existing claim lease clock, skip-locked cursor, claim token and batch bound remain; diagnostic GetDue does not claim rows. This timer guarantee does not repair ordinary command/event postcommit dispatch gaps or advertise SQL Server's separate atomic mixed-effects profile for Oracle.

Compact scopes, ID facts, retired tokens and last receipts remain after payload retirement and state purge under normal provider storage pressure. No TTL, hidden global lifetime quota or automatic eviction is introduced. Metadata is bounded per fact, not bounded lifetime cardinality. No Oracle NativeAOT or cloud-provider runtime certification is inferred from source review; run the actual provider controls before a release claim.


## Retained timeout delivery failures

Hosted timeout delivery requires the selected store's `ISagaTimeoutFailureStore` capability and usable
storage before the pump starts. Apply packaged `04-SagaTimeoutFailureHolds.sql` explicitly to the actual
direct timeout table selected by `OracleSagaTimeoutStoreOptions`. Edit the migration's owner/table
literals for a custom table; fresh `SagaTimeouts.sql` already includes these fields. Quiesce writers for
migration: Oracle DDL commits implicitly. There is no automatic runtime migration. The migration
persists legacy row epochs once, validates existing reserved-field defaults, and refuses incompatible
defaults rather than rewriting consumer definitions. Existing standalone manual operations keep their
no04 path when the hold profile is absent; partial/incompatible profile metadata fails actionably.

Permanent, poison and unclassified/Unknown dispatch failures are held with their original timeout ID,
payload, tenant, saga and due timestamp. An unsuccessful result without canonical typed failure evidence
is Unknown; diagnostic text is not a retry classifier. Bounded reason metadata is separate from payload.
Normal claims exclude held rows; diagnostic `GetDueTimeoutsAsync` continues to expose retained payload.
No failure is certified as delivered merely because a hold was attempted. Transient/Uncertain retry
behavior remains separate; this capability does not introduce a total transient retry budget.

The selected store exposes a bounded exact-tenant held query and explicit expected-token resume.
Establish the authorized current tenant as for other saga-store operations. For example, with an existing
service provider, cancellation token, authorized tenant ID and an operator-selected timeout ID:

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;
using Microsoft.Extensions.DependencyInjection;

public static class OracleHeldTimeoutRecovery
{
    public static async Task<SagaTimeoutHoldReceipt> InspectAsync(IServiceProvider services, string tenantId, string timeoutId, CancellationToken cancellationToken)
    {
        using var tenantScope = TenantContextHolder.BeginScope(tenantId);
        var failures = SelectedFailures(services);
        await failures.ValidateFailureStorageAsync(cancellationToken);
        var page = await failures.GetHeldFailuresAsync(tenantId, null, 100, null, cancellationToken);
        return page.Items.Single(item => item.Timeout.TimeoutId == timeoutId);
    }

    // Call only after inspecting the receipt and authorizing this exact recovery.
    // Retain the expected token and commandId before the call, including across acknowledgment loss.
    public static async ValueTask<SagaTimeoutHoldReceipt?> ResumeAuthorizedAsync(IServiceProvider services, string tenantId,
        SagaTimeoutHoldToken expectedToken, Guid commandId, CancellationToken cancellationToken)
    {
        using var tenantScope = TenantContextHolder.BeginScope(tenantId);
        var failures = SelectedFailures(services);
        await failures.ValidateFailureStorageAsync(cancellationToken);
        return await failures.TryResumeHeldFailureAsync(expectedToken, commandId, cancellationToken);
    }

    private static ISagaTimeoutFailureStore SelectedFailures(IServiceProvider services)
    {
        var selected = services.GetRequiredService<ISagaTimeoutStore>();
        return ((IServiceProvider)selected).GetService(typeof(ISagaTimeoutFailureStore))
            as ISagaTimeoutFailureStore
            ?? throw new InvalidOperationException("Selected timeout store lacks hold support.");
    }
}
```

A null resume result means the expected state was refused; it grants no lease and inserts nothing.
Retain the same command ID and expected token when reconciling an acknowledgment loss. Successful
resume preserves original ID/bytes/due and changes the delivery cycle once; normal claiming later
issues a fresh lease. Cancellation, replacement or newer held state cannot be undone with an old token.
Receipts are bounded to the retained timer lifetime/latest cycle; no global receipt ledger or indefinite
payload retention after explicit cancellation is promised. Query cursors are target/partition/filter-bound,
exclusive and weakly consistent, ordered by Oracle binary sort keys; they are not authorization grants.

Source observations bind the configured logical service/descriptor, `DB_NAME`/`CON_NAME`, owner/table
`OBJECT_ID`, exact timer address and persisted row lifetime. Delivery cycle is independent. Replica-specific
`DB_UNIQUE_NAME`, host/instance identity and segment `DATA_OBJECT_ID` are not new hold bindings. Different
configuration aliases may conservatively reject old observations; arbitrary alias repointing/database
cloning or restore is not certified by this binding. Existing ordinary receiver03 registration rules are
unchanged and have their own physical-target constraints; this does not broaden their failover/reorg
contract. No global HA, physical exactly-once or NativeAOT certification is implied.

Only committed hold state excludes claims across hosts/restart. A crash or storage failure before the
first hold commit retains the existing at-least-once window. The worker's bounded local unresolved
observation/backpressure is not durable success. Hold outcomes distinguish actual Held, a live source
needing a fresh claim, definitive original-source/cycle end under the same bound authority, and rejected
inputs/target. A real hold committed by another operation can resolve the same source without claiming
that the caller's operation committed.

For the registration-through-query/resume example, see the saga guide’s **Recovering held timeout deliveries** section. The selected store is validated before hosted delivery starts; custom stores without usable hold support cannot start that pump. In-memory hold state is process-local.
