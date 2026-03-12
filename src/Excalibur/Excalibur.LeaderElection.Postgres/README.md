# Excalibur.LeaderElection.Postgres

PostgreSQL implementation of leader election for the Excalibur framework. Uses advisory locks (`pg_try_advisory_lock`) for distributed coordination with automatic failover and session-based locking.

## Features

- **Advisory lock-based leader election** -- lightweight, session-scoped locks that auto-release on connection loss
- **Health-based leader election** -- extends standard LE with health-aware candidate tracking and voluntary step-down
- **Factory pattern** -- create multiple independent leader elections with different lock keys
- **Telemetry integration** -- OpenTelemetry metrics and traces via `TelemetryLeaderElection` decorator
- **Health checks** -- ASP.NET Core health check integration

## Quick Start

```csharp
services.AddPostgresLeaderElection(options =>
{
    options.ConnectionString = "Host=localhost;Database=myapp;";
    options.LockKey = 12345;
});
```

Or using the builder pattern:

```csharp
services.AddExcaliburLeaderElection(builder =>
{
    builder.UsePostgres(options =>
    {
        options.ConnectionString = connectionString;
    });
});
```
