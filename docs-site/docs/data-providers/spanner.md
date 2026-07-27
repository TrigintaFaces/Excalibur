---
sidebar_position: 14
title: Spanner
description: Google Cloud Spanner data-provider foundation — connection provider, retryable-transaction execution, and validated options. Persistence stores are not yet available on Spanner.
---

# Spanner Provider

`Excalibur.Data.Spanner` is the **connection foundation** for [Google Cloud Spanner](https://cloud.google.com/spanner): a connection provider, a retryable-transaction executor that replays Spanner's `ABORTED` conflicts, the per-commit limit constants, and validated options — all over the official `Google.Cloud.Spanner.Data` driver. It is the layer that persistence stores would build on; those stores are **not yet available** (see below).

The package is **opt-in**: install it and register the foundation once via its `AddSpannerDataProvider` extension.

:::note Foundation package — not the full store set
Unlike the SQL Server, PostgreSQL, and Oracle providers, `Excalibur.Data.Spanner` does **not** yet ship event-store, outbox, inbox, or saga stores. It provides only the provider foundation those stores depend on: connection creation, retryable-transaction execution, commit-limit constants, and options. Spanner is not a Dapper target — it uses the mutation API rather than sequences/auto-increment and has no pessimistic locking (`FOR UPDATE ... SKIP LOCKED`), so concurrency is optimistic and write conflicts surface as retryable `ABORTED` transactions.
:::

## Before You Start

- **.NET 10.0**
- A reachable Spanner instance, or the official [Spanner emulator](https://cloud.google.com/spanner/docs/emulator) for local development and integration tests
- A Google Cloud project, Spanner instance, and database (Spanner is addressed by the three-part path `projects/{project}/instances/{instance}/databases/{database}`)

## Installation

```bash
dotnet add package Excalibur.Data.Spanner
```

**Dependencies:** `Google.Cloud.Spanner.Data`, `Microsoft.Extensions.Options`

> **AOT:** This package is **not** AOT-compatible — `Google.Cloud.Spanner.Data` uses gRPC and reflection-based value conversion.

## Registration

Register the connection provider and its validated options once per application. The individual `Excalibur.*.Spanner` store registrations depend on this call:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddSpannerDataProvider(options =>
{
    options.ProjectId = "my-project";
    options.InstanceId = "my-instance";
    options.DatabaseId = "my-database";

    // Local development / integration tests: target the Spanner emulator.
    // Equivalent to setting the SPANNER_EMULATOR_HOST environment variable.
    // options.EmulatorHost = "localhost:9010";
});
```

Options are validated at startup (`ValidateOnStart`), so a misconfigured provider fails fast rather than surfacing as a runtime connection error.

### Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ProjectId` | `string` | `""` (required) | The Google Cloud project id that owns the Spanner instance. |
| `InstanceId` | `string` | `""` (required) | The Spanner instance id. |
| `DatabaseId` | `string` | `""` (required) | The Spanner database id. |
| `EmulatorHost` | `string?` | `null` | Emulator endpoint (`host:port`) to target instead of the production service. When set, takes precedence over the `SPANNER_EMULATOR_HOST` environment variable. |
| `MaxAbortRetries` | `int` | `5` | Maximum number of times an `ABORTED` transaction is retried before failing. Must be non-negative. |
| `AbortRetryBaseDelayMilliseconds` | `int` | `25` | Base backoff between `ABORTED` retries, in milliseconds. The effective delay grows exponentially with the attempt number. Must be non-negative. |
| `DatabasePath` | `string` | *(computed)* | Read-only. The fully-qualified database path `projects/{ProjectId}/instances/{InstanceId}/databases/{DatabaseId}`. |

## Connection Provider

`AddSpannerDataProvider` registers `ISpannerConnectionProvider`, which the store packages resolve to open connections and run work under Spanner's optimistic-concurrency model:

```csharp
public interface ISpannerConnectionProvider
{
    // Creates a new, unopened connection for the configured database (caller owns disposal).
    SpannerConnection CreateConnection();

    // Opens a connection and runs the operation, replaying it with exponential backoff
    // when Spanner reports the transaction as retryable, up to MaxAbortRetries.
    Task<T> ExecuteInRetryableTransactionAsync<T>(
        Func<SpannerConnection, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);
}
```

Because Spanner has no pessimistic row locks, the loser of a concurrent write observes an `ABORTED` transaction; `ExecuteInRetryableTransactionAsync` replays it rather than losing the update.

## Commit Limits

`SpannerCommitLimits` exposes the hard per-commit ceilings Spanner enforces, so stores can chunk large writes to stay within them:

| Constant | Value | Description |
|----------|-------|-------------|
| `MaxMutationsPerCommit` | `80000` | Maximum mutations (column writes + secondary-index entries) permitted in one commit. |
| `MaxCommitSizeBytes` | `104857600` (100 MiB) | Maximum serialized size, in bytes, permitted in one commit. |

## What's Next

- [Data Providers](./index.md) — Overview of the persistence providers
- [Event Sourcing](../event-sourcing/index.md) — Aggregates, event stores, and snapshots
- [Outbox Pattern](../patterns/outbox.md) — Reliable message publishing
