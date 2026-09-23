# Excalibur.Data.Postgres — A3 Authorization Store (Examples)

Examples only — adjust to your host’s composition.

Register services

```csharp
using Excalibur.Data.Postgres.DependencyInjection;
using Excalibur.A3;

services.AddPostgresDataExecutors(() => new NpgsqlConnection(connString));

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
| `Excalibur.Postgres` | Complete | Everything for PostgreSQL: ES + Outbox + Inbox + Saga + LE + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.Postgres` for a production-ready PostgreSQL stack with a single package reference.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
