# Excalibur.Data.SqlServer — A3 Authorization Store (Examples)

Examples only — adjust to your host’s composition.

Register services

```csharp
using Excalibur.Data.SqlServer;
using Excalibur.A3;

services.AddExcaliburSqlServices();

// IGrantStore, IActivityGroupStore registered via ServiceCollectionExtensions
```

Consume abstractions

```csharp
public sealed class GrantsController(IGrantStore grantStore)
{
    public async Task<IReadOnlyList<Grant>> GetUserGrants(string userId, CancellationToken ct)
        => await grantStore.GetAllGrantsAsync(userId, ct);
}
```

Notes
- Store implementations depend on `Excalibur.A3.Abstractions` only. No references to `Excalibur.A3` implementation remain.
- Store classes use inline Dapper SQL via `IDomainDb` for connection management.


## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.SqlServer` | Complete | Everything for SQL Server: ES + Outbox + Inbox + Saga + LE + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.SqlServer` for a production-ready SQL Server stack with a single package reference.

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
| `scripts/001_CreateDeadLetterSchema.sql` |
| `scripts/002_CreateActivityGroupSchema.sql` |
| `scripts/003_CreateGrantSchema.sql` |

Apply them in filename order before starting the application, and again — for any new ones — before
starting a new version after an upgrade.

The `scripts/` folder lives inside the package, not in your build output. To read it:

```bash
# the package folder, then the scripts inside it
dotnet nuget locals global-packages --list
ls ~/.nuget/packages/excalibur.data.sqlserver/<version>/scripts/
```

**The folder is authoritative.** If any list of scripts — including this one — disagrees with what is
actually in the package you installed, the package is right.

## Durable scheduled occurrences

Provision `scripts/004_CreateScheduleSchema.sql`, then
`scripts/005_CreateScheduleFailureSchema.sql`, then
`scripts/006_AddScheduleCronRecurrenceProfile.sql` before using `SqlServerScheduleStore`.
The store does not create or migrate its schema at runtime. Existing consumed
admissions without a captured budget require explicit administrative repair;
the provider refuses to invent a retry limit for that history.

The failure capability captures the automatic admission budget in the same database
transaction that grants the first owner. Later hosts cannot widen that budget.
Failures retain the due occurrence; terminal quarantine atomically disables it and
retains its original envelope, reason and consumed grant count. Counts describe
authority admissions, including abandoned grants, rather than proven handler calls.
Handler side effects remain at-least-once.

Authorized administration can query a bounded quarantine page filtered by the exact raw envelope tenant identifier and resume
an expected quarantine token using a nonempty recovery command ID. Identical command
replay returns its original receipt, including after restart or subsequent progress;
it does not grant another budget. Recovery creates a fresh generation with the retained
budget and original occurrence. Recovery receipts establish committed scheduler state,
not successful external side effects. Normal replacement or cancellation does not erase
unresolved failure provenance; recovered quarantine history remains retained separately.

Queries filter the retained raw envelope tenant identifier exactly: null, the literal
`__untenanted__`, empty string, case and trailing spaces remain distinct. This nullable
metadata is not a canonical ambient tenant partition or an authorization boundary.
Queries use ordinal `Guid.ToString("N")` exclusive cursors. Paging is weakly consistent
during inserts, not a database snapshot. The caller must authorize administration;
supplying an identifier is not authorization. Store-global administrative methods remain store-global.

Failure or recovery acknowledgement loss does not establish rollback. Reconcile the
exact retained occurrence/token/command before asserting success or retrying an operator
command. The provider does not automatically retry ambiguous commits. Database restore,
cloning or epoch rotation requires coordinated offline treatment of current schedules
and retained history; an old epoch token is not authority in the new epoch.


Captured unsettled work also blocks replacement when a failure write never committed.
Administrative cancellation quarantines it atomically; an unadmitted recovered cycle
may retain a count of zero. This records administrative intent, not proof that an
in-flight handler had no external effect.

Cron recurrences in transition-capable installed zones explicitly declare
`CronRecurrenceProfile.CronosCompatible` through the public scheduler. The row and retained
failure envelope persist its `CronRecurrenceProfileId`/`CronRecurrenceProfileVersion` pair.
Migration006 adds nullable columns without backfilling a policy for existing rows. Missing
or unknown declarations cannot authorize future transition-zone first admissions. Already
captured UTC work retains its existing authority, budget and failure/recovery obligations;
no row is silently deleted or reinterpreted. Apply explicit administrative repair to future
configuration after the captured work is settled. The store never applies migration006 itself.
