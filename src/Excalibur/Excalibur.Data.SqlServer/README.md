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
| `scripts/004_NarrowActivityGroupName.sql` |

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
