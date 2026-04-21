# BindConfiguration Patterns

**Beads:** `bd-1r741x` (P2)
**Location:** `samples/01-getting-started/BindConfigurationPatterns/`

This sample demonstrates the four patterns for driving Excalibur subsystem
configuration from `IConfiguration`:

| # | Pattern | When to use |
|---|---------|-------------|
| 1 | `appsettings.json` + `.BindConfiguration("Section")` on every builder | Preferred default for all subsystems |
| 2 | `appsettings.{Environment}.json` overrides | Per-environment deltas (Dev, Staging, Production) |
| 3 | Environment variables (`ConnectionStrings__EventStore`, `Outbox__BatchSize`) | Secrets, cloud deployments, containers |
| 4 | `IConfiguration.GetConnectionString(...)` | Connection strings (respects the ConnectionStrings section AND the `ConnectionStrings__Name` env var) |

## Running the sample

```bash
# (1) Default -- loads appsettings.Development.json
dotnet run

# (2) Production -- loads appsettings.Production.json
ASPNETCORE_ENVIRONMENT=Production dotnet run

# (3) Env-var override wins over both JSON files
ConnectionStrings__EventStore="Server=prod;Database=prod;..." dotnet run

# (4) Override any option in the Outbox section
Outbox__BatchSize=1000 dotnet run
```

Then hit `GET /` to see the resolved configuration (with the password masked).

## Binding in code

```csharp
services.AddExcalibur(excalibur =>
{
    excalibur
        .AddEventSourcing(es =>
        {
            es.UseSqlServer(sql =>
            {
                sql.ConnectionString(builder.Configuration.GetConnectionString("EventStore")!);
                sql.BindConfiguration("EventSourcing:Sql");
            });
        })
        .AddOutbox(outbox =>
        {
            outbox.UseSqlServer(sql =>
            {
                sql.ConnectionString(builder.Configuration.GetConnectionString("EventStore")!);
                sql.BindConfiguration("Outbox:Sql");
            });
        });
});
```

## Configuration precedence (.NET default)

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. User secrets (`Development` only)
4. Environment variables
5. Command-line arguments

Later sources override earlier ones, so env vars always win over JSON files.

## Common env-var overrides

| Purpose | Variable | Example |
|---------|----------|---------|
| Connection string | `ConnectionStrings__<Name>` | `ConnectionStrings__EventStore="Server=...;"` |
| Outbox batch size | `Outbox__BatchSize` | `Outbox__BatchSize=500` |
| ES schema | `EventSourcing__Sql__Schema` | `EventSourcing__Sql__Schema=audit` |
| Environment select | `ASPNETCORE_ENVIRONMENT` | `ASPNETCORE_ENVIRONMENT=Production` |

Double-underscore (`__`) is the conventional separator for nested config keys
on Linux and Windows containers because `:` is not portable in env-var names.

## Secret management

For production deployments, do NOT commit real secrets. Use:

- **Kubernetes secrets** mounted as env vars
- **Docker secrets** + `${VAR}` substitution in your deployment manifest
- **Azure Key Vault** via `AddAzureKeyVault(...)` configuration source
- **AWS Secrets Manager** via `AddSecretsManager(...)` configuration source
- **User secrets** (`dotnet user-secrets set ...`) for local development

Every one of these is consumed via the standard `IConfiguration` pipeline, so
the BindConfiguration calls in the sample keep working unchanged.
