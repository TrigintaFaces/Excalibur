# Excalibur.Data.Spanner

Google Cloud Spanner data-provider foundation for Excalibur. Provides the non-Dapper adapter base that the
forthcoming `Excalibur.*.Spanner` persistence stores (event store, outbox, inbox, saga) will build on — those
stores are not yet shipped in this package.

Spanner is not a Dapper target: it uses the mutation API rather than sequences/auto-increment, and has no
pessimistic locking (`FOR UPDATE ... SKIP LOCKED`). Concurrency is optimistic — write conflicts surface as
retryable `ABORTED` transactions.

## What this package provides

- `SpannerOptions` — project/instance/database addressing, emulator endpoint, and abort-retry tuning,
  validated at startup (`ValidateOnStart`).
- `ISpannerConnectionProvider` — creates connections and runs work under
  `ExecuteInRetryableTransactionAsync`, replaying `ABORTED` transactions with exponential backoff.
- `SpannerCommitLimits` — the Spanner per-commit ceilings (80,000 mutations / 100 MiB) the stores chunk against.
- `AddSpannerDataProvider(...)` — one-time DI registration the store packages depend on.

## Usage

```csharp
services.AddSpannerDataProvider(o =>
{
    o.ProjectId = "my-project";
    o.InstanceId = "my-instance";
    o.DatabaseId = "my-database";
    // o.EmulatorHost = "localhost:9010"; // local development / integration tests
});
```

Set `EmulatorHost` (or the `SPANNER_EMULATOR_HOST` environment variable) to target the official Spanner
emulator.
