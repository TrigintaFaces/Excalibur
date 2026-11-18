---
sidebar_position: 6
title: Leader Election
description: Coordinate distributed workloads with leader election
---

# Leader Election

Leader election coordinates which application instance should perform a shared workload. Excalibur provides pluggable election for background processing, outbox publishing and scheduled tasks. Protect shared writes with resource-side fencing; local leadership alone cannot exclude stale work through arbitrary pauses or network partitions.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.LeaderElection
  dotnet add package Excalibur.LeaderElection.SqlServer  # or Redis/Postgres provider
  ```
- A distributed storage backend (SQL Server or Redis) for lease management
- Familiarity with [Dispatch hosting](../deployment/index.md)

## Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.LeaderElection.Abstractions` | Core interfaces: `ILeaderElection`, `ILeaderElectionFactory`, `IHealthBasedLeaderElection` |
| `Excalibur.LeaderElection` | Registration, telemetry decorator, and health check |
| `Excalibur.LeaderElection.SqlServer` | SQL Server-based leader election |
| `Excalibur.LeaderElection.Postgres` | PostgreSQL advisory lock-based leader election |
| `Excalibur.LeaderElection.Redis` | Redis-based leader election |
| `Excalibur.LeaderElection.Consul` | Consul-based leader election |
| `Excalibur.LeaderElection.Kubernetes` | Kubernetes lease-based leader election |
| `Excalibur.LeaderElection.InMemory` | In-memory leader election (testing/development) |

## When to Use Leader Election

| Scenario | Why Leader Election |
|----------|---------------------|
| Outbox message publishing | Prevent duplicate message sends |
| Scheduled job processing | Keep a cron job to one instance at a time — see the caveat below before treating that as *exactly* once |
| Cache warming | Single instance warms cache |
| Event projection updates | Prevent duplicate projections |
| Singleton background services | Only one active instance |

:::warning Local leadership is not a protected-write guarantee

Local proof expiry and server-side ownership are separate events. Redis TTLs, SQL Server and PostgreSQL session locks, and Consul sessions have different expiry and cleanup rules. A process pause, partition or delayed SDK call prevents any general promise that two processes cannot act on stale leadership within a fixed wall-clock window.

Pin `CurrentLeadership` once per work batch, carry its acquisition fencing token into every protected write, and require the resource to reject stale tokens **atomically with the write**. An `IsLeader` check, or `ValidateTokenAsync` followed by a separate write, leaves a race. Do not replace old work's token with a newly acquired token. Durable idempotency may also be needed to make retries safe; election does not provide exactly-once effects by itself.

:::

:::note Outbox draining is fenced automatically
Registering an `ILeaderElection` provider makes the [outbox](../patterns/outbox.md#multi-instance-leader-fenced-processing)
drain **single-active (leader-fenced) by default** — the multi-instance signal is the registration itself,
so no separate opt-in is needed. Two conditions fail fast at startup rather than draining unfenced: a
store that cannot enforce a fencing high-water mark, and an election that is resolvable while nothing
fences the drain. A genuine single-active-writer deployment opts out of both explicitly with
`AsSingleWriter()`. How
durably a superseded leader stays fenced varies by store — see
[Multi-Instance (Leader-Fenced) Processing](../patterns/outbox.md#multi-instance-leader-fenced-processing)
before relying on it, particularly on SQL Server. The manual `ILeaderElection` API below is for
coordinating your **own** workloads (jobs, projections, cache warming).
:::

## Core Concepts

### The Leader Election Pattern

```
Instance A --+-- Acquires Lock --> Becomes Leader --> Processes Work
             |
Instance B --+-- Waits ----------> Standby ---------> Ready to Take Over
             |
Instance C --+-- Waits ----------> Standby ---------> Ready to Take Over

If Instance A fails:
Instance B --> Acquires Lock --> Becomes Leader --> Processes Work
```

### Leader Responsibilities

1. **Acquire Leadership**: Obtain exclusive lock
2. **Renew Leadership**: Keep lock alive with heartbeats
3. **Perform Work**: Execute the singleton workload
4. **Release Leadership**: Clean up on shutdown

## The ILeaderElection Interface

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Dispatch.LeaderElection;

public interface ILeaderElection : IAsyncDisposable
{
    // Permanent completion of queued notifications, separate from resource disposal.
    Task NotificationsCompleted { get; }

    /// <summary>
    /// Event raised when this instance becomes the leader.
    /// </summary>
    event EventHandler<LeaderElectionEventArgs>? BecameLeader;

    /// <summary>
    /// Event raised when this instance loses leadership.
    /// </summary>
    event EventHandler<LeaderElectionEventArgs>? LostLeadership;

    /// <summary>
    /// Event raised when the leader changes (any instance).
    /// </summary>
    event EventHandler<LeaderChangedEventArgs>? LeaderChanged;

    /// <summary>
    /// Event raised when this instance fails to acquire leadership, either by losing the
    /// acquisition race or because an error occurred during the attempt.
    /// </summary>
    event EventHandler<LeaderElectionAcquisitionFailedEventArgs>? AcquisitionFailed;

    /// <summary>
    /// Gets the unique identifier for this election participant.
    /// </summary>
    string CandidateId { get; }

    /// <summary>
    /// Gets a value indicating whether this instance is currently the leader.
    /// </summary>
    bool IsLeader { get; }

    // Read once per work batch and carry its fence into protected writes.
    Leadership? CurrentLeadership { get; }

    /// <summary>
    /// Gets the current leader's identifier.
    /// </summary>
    string? CurrentLeaderId { get; }

    /// <summary>
    /// Starts participating in leader election.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops participating and relinquishes leadership if held.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}
```

## Notification Lifetime and Shutdown

Committed local acquisition and loss events are queued transition history; remote-holder and acquisition observations may be coalesced. Event arrival is not permission to write: a tenure may already have ended before its callback runs. Read `CurrentLeadership` when starting a work batch and enforce its pinned fencing token at the protected resource.

`StopAsync` relinquishes participation and leaves the instance restartable after successful cleanup. A subsequent start or acquisition may also wait for prior ownership notifications to drain successfully. Stop does not complete `NotificationsCompleted` or wait for consumer callbacks. Permanent `DisposeAsync` seals producers and joins owned resource operations; callback delivery has a separate lifetime. `NotificationsCompleted` is the same task from construction onward and succeeds after permanent disposal and all admitted synchronous event invocations have returned. A fatal notification-dispatch failure faults the task; faulted completion does not prove every queued callback ran.

Await that task during final teardown, before disposing dependencies used by your callbacks. Observe resource-disposal failures and notification failures separately: a successful resource disposal does not mean every subscriber succeeded. A notification failure faults the permanent task. If disposal throws, still observe `NotificationsCompleted` and retain both failures if both operations fail.

Redis, SQL Server, PostgreSQL and Consul queue provider diagnostics on the same serialized delivery lane as notifications. One pending diagnostic is retained, so messages may coalesce; use a separate durable audit mechanism when every record must be preserved. A blocked logger can delay callbacks and the ownership-notification drain required for restart, while authority deadlines and owned resource cleanup remain independent. Logger exceptions are reported through `NotificationsCompleted`. Keep borrowed logging dependencies alive until that task settles.

Callbacks may stop or dispose their own election. They must not await their own `NotificationsCompleted` task or restart that election from its active callback: doing so would wait for that callback to finish. Start from the same active notification domain is refused with `InvalidOperationException`. Operations on an independent election remain allowed. Completion covers synchronous delegate invocations, not `async void` continuations or tasks launched by subscribers; application-owned asynchronous work needs its own cancellation and join.

For a DI-owned election, coordinate final draining in the application lifetime owner rather than disposing a shared election from each consumer. `await using` alone joins resource disposal, not notification completion.

## Registration

### Builder API (Recommended)

Use the fluent builder pattern to configure leader election with your chosen provider:

```csharp
using Microsoft.Extensions.DependencyInjection;

// SQL Server with health checks. Fencing tokens are already on by default — see "Fencing tokens" below.
services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le
    .UseSqlServer(sql => sql.ConnectionString(connectionString).LockResource("my-app-leader"))
    .WithHealthChecks()));

// Postgres (5 canonical connection overloads)
services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le
    .UsePostgres(pg =>
    {
        pg.ConnectionString("Host=localhost;Database=myapp;")
          .LockKey(12345);
    })
    .WithHealthChecks()));

// Redis
services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le
    .UseRedis(redis =>
    {
        redis.ConnectionString("localhost:6379")
             .LockKey("myapp:leader");
    })
    .WithHealthChecks()));

// Consul
services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le
    .UseConsul(consul =>
    {
        consul.Address("http://localhost:8500")
              .SessionTtl(TimeSpan.FromSeconds(30))
              .ResourceName("order-processor");
    })));

// Kubernetes
services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le
    .UseKubernetes()));

// In-memory (testing/development)
services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le
    .UseInMemory()));

// Configure options via the builder
services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le
    .UseRedis(redis =>
    {
        redis.ConnectionString("localhost:6379")
             .LockKey("myapp:leader");
    })
    .WithOptions(opts =>
    {
        opts.LeaseDuration = TimeSpan.FromSeconds(30);
        opts.RenewInterval = TimeSpan.FromSeconds(10);
    })
    .WithHealthChecks()));
```

The builder automatically registers `LeaderElectionOptions` with `ValidateDataAnnotations` and `ValidateOnStart`.

### Configuration Headroom and Runtime Deadlines

`LeaderElectionOptionsValidator` checks positive timing values and the strict configuration relation:

```text
RenewInterval + GracePeriod + 1s (reserved headroom) < LeaseDuration
```

The builder registers startup validation. The Redis, SQL Server, PostgreSQL and Consul provider constructors also validate their effective timing options. Reserved headroom is not a measured bound on clock skew, network delay or process suspension, and passing validation does not establish exclusive ownership.

For these four providers, the local leadership proof has an independent monotonic deadline of `RenewInterval + GracePeriod` from the conservative **start** of the latest successful ownership request. Acquisition and renewal replies received after expiry cannot publish or revive that tenure. Successful ownership verification advances the deadline; ambiguous failures do not. An independent timer clears local authority while an SDK operation remains pending. A paused process cannot execute that timer until it resumes, so this is not a hard real-time guarantee.

Normal renewals and ambiguous-failure retries follow serialized request-start cadence using `RenewInterval` and `RetryInterval`, respectively. Response latency is not added to the configured interval. Definitive ownership loss and classified permanent failures retire earlier. An unrelated cancellation exception is not automatically an owned shutdown request. `GracePeriod` is not a wait before declaring another candidate dead.

| Provider | Ownership verification and remote lifetime |
|----------|-------------------------------------------|
| Redis | Conditional renewal compares a private per-acquisition owner value before extending the key's `LeaseDuration` TTL. Release compares that same value before deleting; a public candidate ID is not the ownership proof. |
| SQL Server | The original dedicated connection must still hold the exact session-owned application lock in `Exclusive` mode. `LeaseDuration` is not a SQL Server lock TTL. Verification never reconnects to create a substitute session. |
| PostgreSQL | The original dedicated backend must hold the exact exclusive bigint advisory lock in the configured database and single-bigint namespace. A different advisory lock on that backend is insufficient. Session locks have no `LeaseDuration` TTL. |
| Consul | A successful session renewal and consistent KV read must match the captured session, nonce and candidate. Remote lifetime uses the separate `SessionTTL`; server expiry is not a strict upper-bound timer. A separate bounded read-only monitor cannot refresh the local proof or block the renewal lane. |

The default local budget is `5s + 5s = 10s`; with the reserved one-second configuration margin it fits below the nominal 15-second lease. This arithmetic is configuration headroom, not a failover latency promise or a proof that stale writes are impossible.

Stop clears local authority before joining owned SDK operations and cleaning up the captured old tenure. Canceling a Stop wait cancels the caller's wait, not cleanup ownership. A hung operation may leave cleanup pending while leadership is already false. Restart requires successful cleanup and prior notification drain. Transition notifications are ordered history, delivered outside the state gate; stopping does not join consumer callbacks, so a callback can stop its election without joining itself.

### Pre-Built Options

For scenarios where options are constructed externally (e.g., from configuration or testing):

```csharp
var options = new LeaderElectionOptions
{
    LeaseDuration = TimeSpan.FromSeconds(30),
    RenewInterval = TimeSpan.FromSeconds(10)
};
services.AddExcalibur(excalibur => excalibur.AddLeaderElection(options));
```

:::note

The pre-built options overload uses `Options.Create()` and does not itself run host startup validation. Redis, SQL Server, PostgreSQL and Consul still validate their effective options when their provider is constructed. Prefer the builder path for startup validation and configure the complete timing budget explicitly.
:::

### Available Builder Extensions

| Method | Package | Purpose |
|--------|---------|---------|
| `UseInMemory()` | `Excalibur.LeaderElection.InMemory` | Testing and development |
| `UseRedis(Action<IRedisLeaderElectionBuilder>)` | `Excalibur.LeaderElection.Redis` | Redis-based leader election |
| `UseRedisFactory()` | `Excalibur.LeaderElection.Redis` | Redis factory for multiple elections |
| `UseSqlServer(conn, lock)` | `Excalibur.LeaderElection.SqlServer` | SQL Server-based leader election |
| `UseSqlServerFactory(conn)` | `Excalibur.LeaderElection.SqlServer` | SQL Server factory for multiple elections |
| `UsePostgres(Action<IPostgresLeaderElectionBuilder>)` | `Excalibur.LeaderElection.Postgres` | PostgreSQL advisory lock-based leader election |
| `UsePostgresFactory(opts)` | `Excalibur.LeaderElection.Postgres` | PostgreSQL factory for multiple elections |
| `UseConsul(Action<ILeaderElectionConsulBuilder>)` | `Excalibur.LeaderElection.Consul` | Consul session-based leader election |
| `UseKubernetes(opts?)` | `Excalibur.LeaderElection.Kubernetes` | Kubernetes Lease-based leader election |
| `WithHealthChecks()` | `Excalibur.LeaderElection` | Registers health check integration |
| `WithFencingTokens()` | `Excalibur.LeaderElection` | Registers fencing support for a consumer-supplied `ILeaderElection`. Built-in providers already fence by default — see "Fencing tokens" below |
| `WithoutFencingTokens()` | `Excalibur.LeaderElection` | Opts a built-in provider out of its on-by-default fencing-token provider |
| `WithOptions(configure)` | `Excalibur.LeaderElection` | Configures `LeaderElectionOptions` |

:::note

Previous standalone `AddRedisLeaderElection` methods have been removed. Use the builder API shown above for all new registrations.
:::

<div id="factory-vs-single" />

## When to Use Factory vs Single Registration

Excalibur offers two ways to register leader election: a **single registration** (`ILeaderElection`) and a **factory** (`ILeaderElectionFactory`). Choose based on how many independent leadership scopes your application needs.

### Single Registration

Register a single `ILeaderElection` when your application has **one leadership scope** -- all background services share the same leader/follower state:

```csharp
// One leader election for the entire application
services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le
    .UseSqlServer(sql => sql.ConnectionString(connectionString).LockResource("my-app-leader"))
    .WithHealthChecks()));
```

Use cases:
- **Simple background worker**: One app instance runs all background tasks
- **Application-level singleton**: "This instance is the active one"
- **Small services**: A microservice with 1-2 background jobs that should all run on the same instance

```csharp
public class MyWorker : BackgroundService
{
    private readonly ILeaderElection _leaderElection;

    public MyWorker(ILeaderElection leaderElection)
    {
        _leaderElection = leaderElection;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _leaderElection.StartAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_leaderElection.IsLeader)
                    await DoWorkAsync(ct);

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        finally
        {
            await _leaderElection.StopAsync(CancellationToken.None);
        }
    }
}
```

### Factory Registration

Register `ILeaderElectionFactory` when your application has **multiple independent leadership scopes** -- different resources can have different leaders across your instances:

```csharp
// Factory for creating per-resource elections
services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le
    .UseSqlServerFactory(sql => sql.ConnectionString(connectionString))));
```

Use cases:
- **Independent workloads**: Outbox publishing, projection updates, and cleanup jobs should each have their own leader (Instance A leads outbox, Instance B leads projections)
- **Shared resource protection**: Multiple resources each need exactly one writer
- **Fine-grained load distribution**: Spread leadership across instances instead of concentrating all work on one node

```csharp
public class DistributedWorker : BackgroundService
{
    private readonly ILeaderElectionFactory _factory;

    public DistributedWorker(ILeaderElectionFactory factory)
    {
        _factory = factory;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Each resource gets its own independent election
        var outboxElection = _factory.CreateElection("outbox-processor", candidateId: null);
        var projectionElection = _factory.CreateElection("event-projector", candidateId: null);
        var cleanupElection = _factory.CreateElection("cleanup-job", candidateId: null);

        var elections = new[] { outboxElection, projectionElection, cleanupElection };
        await Task.WhenAll(elections.Select(e => e.StartAsync(ct)));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (outboxElection.IsLeader)
                    await ProcessOutboxAsync(ct);

                if (projectionElection.IsLeader)
                    await ProcessProjectionsAsync(ct);

                if (cleanupElection.IsLeader)
                    await RunCleanupAsync(ct);

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        finally
        {
            await Task.WhenAll(elections.Select(e => e.StopAsync(CancellationToken.None)));
        }
    }
}
```

### Decision Matrix

| Question | Single | Factory |
|----------|--------|---------|
| How many leadership scopes? | 1 | 2+ |
| Can different instances lead different workloads? | No -- one leader does all | Yes -- each resource has its own leader |
| Registration complexity | Lower | Slightly higher |
| Lock count in the backend | 1 | 1 per resource |
| Load distribution | Concentrated on leader | Spread across instances |

### Shared Resource Scenarios

The factory pattern is especially valuable when multiple independent resources require exclusive access. Each resource gets its own election, so leadership is distributed naturally:

**Scenario: E-commerce platform with multiple background processors**

```csharp
public class ECommerceWorkerService : BackgroundService
{
    private readonly ILeaderElectionFactory _factory;
    private readonly IServiceProvider _services;

    public ECommerceWorkerService(
        ILeaderElectionFactory factory,
        IServiceProvider services)
    {
        _factory = factory;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Each processor contends independently for its resource lock
        var orderOutbox = _factory.CreateElection("order-outbox", candidateId: null);
        var inventorySync = _factory.CreateElection("inventory-sync", candidateId: null);
        var priceUpdater = _factory.CreateElection("price-updater", candidateId: null);
        var reportGenerator = _factory.CreateElection("daily-reports", candidateId: null);

        var elections = new[]
        {
            orderOutbox, inventorySync, priceUpdater, reportGenerator
        };

        await Task.WhenAll(elections.Select(e => e.StartAsync(ct)));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // With 3 instances, leadership distributes across them:
                // Instance A: order-outbox, daily-reports
                // Instance B: inventory-sync
                // Instance C: price-updater
                if (orderOutbox.IsLeader)
                    await ProcessOrderOutboxAsync(ct);

                if (inventorySync.IsLeader)
                    await SyncInventoryAsync(ct);

                if (priceUpdater.IsLeader)
                    await UpdatePricesAsync(ct);

                if (reportGenerator.IsLeader)
                    await GenerateReportsAsync(ct);

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        finally
        {
            await Task.WhenAll(elections.Select(e => e.StopAsync(CancellationToken.None)));
        }
    }
}
```

**Scenario: Multi-tenant background processing**

When each tenant's data should be processed by exactly one instance:

```csharp
public class TenantProcessorService : BackgroundService
{
    private readonly ILeaderElectionFactory _factory;
    private readonly ITenantRegistry _tenants;

    public TenantProcessorService(
        ILeaderElectionFactory factory,
        ITenantRegistry tenants)
    {
        _factory = factory;
        _tenants = tenants;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var tenantIds = await _tenants.GetActiveTenantIdsAsync(ct);
        var elections = tenantIds
            .Select(id => (
                TenantId: id,
                Election: _factory.CreateElection($"tenant-{id}", candidateId: null)))
            .ToList();

        await Task.WhenAll(elections.Select(e => e.Election.StartAsync(ct)));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                foreach (var (tenantId, election) in elections)
                {
                    if (election.IsLeader)
                        await ProcessTenantAsync(tenantId, ct);
                }

                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
        finally
        {
            await Task.WhenAll(elections.Select(e => e.Election.StopAsync(CancellationToken.None)));
        }
    }
}
```

:::tip Fail-closed tenant scoping
The example above builds the per-tenant lease name (`$"tenant-{id}"`) by hand. When you already have an
ambient [`ITenantContext`](../multi-tenancy.md), prefer the opt-in tenant-scoped extensions, which qualify the
lease with the current tenant and **fail closed** when no tenant is resolved — so a missing tenant can never
silently collapse into an unscoped, cross-tenant lease:

```csharp
using Excalibur.Dispatch.LeaderElection;

// Lease is scoped to the ambient tenant: "outbox-drain:{tenantId}".
// Throws TenantRequiredException if no tenant is resolved.
ILeaderElection election = factory.CreateTenantScopedElection(
    tenantContext, resourceName: "outbox-drain", candidateId: null);

// Health-based variant:
IHealthBasedLeaderElection healthElection = factory.CreateTenantScopedHealthBasedElection(
    tenantContext, resourceName: "outbox-drain", candidateId: null);
```

`ITenantContext.TenantScopedResourceName("outbox-drain")` composes the `"{resourceName}:{tenantId}"` name
directly if you want to scope a lease name yourself.
:::

## Basic Usage

### Event-Based Leadership Pattern

```csharp
public class OutboxProcessor : BackgroundService
{
    private readonly ILeaderElection _leaderElection;
    private readonly IOutboxStore _outbox;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        ILeaderElection leaderElection,
        IOutboxStore outbox,
        ILogger<OutboxProcessor> logger)
    {
        _leaderElection = leaderElection;
        _outbox = outbox;
        _logger = logger;

        // Subscribe to leadership events
        _leaderElection.BecameLeader += (_, args) =>
            _logger.LogInformation("Became leader: {CandidateId}", args.CandidateId);

        _leaderElection.LostLeadership += (_, args) =>
            _logger.LogInformation("Lost leadership: {CandidateId}", args.CandidateId);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Start participating in leader election
        await _leaderElection.StartAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Only process if we're the leader
                if (_leaderElection.IsLeader)
                {
                    await _outbox.ProcessPendingMessagesAsync(ct);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        finally
        {
            // Stop participating on shutdown
            await _leaderElection.StopAsync(CancellationToken.None);
        }
    }
}
```

### Scheduled Job Runner

```csharp
public class ScheduledJobRunner : BackgroundService
{
    private readonly ILeaderElection _leaderElection;
    private readonly ILogger<ScheduledJobRunner> _logger;

    public ScheduledJobRunner(
        ILeaderElection leaderElection,
        ILogger<ScheduledJobRunner> logger)
    {
        _leaderElection = leaderElection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _leaderElection.StartAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_leaderElection.IsLeader)
                {
                    _logger.LogInformation("Running scheduled jobs as leader");
                    await RunScheduledJobsAsync(ct);
                }

                await Task.Delay(TimeSpan.FromMinutes(1), ct);
            }
        }
        finally
        {
            await _leaderElection.StopAsync(CancellationToken.None);
            _logger.LogInformation("Stopped leader election");
        }
    }

    private Task RunScheduledJobsAsync(CancellationToken ct)
    {
        // Execute scheduled jobs
        return Task.CompletedTask;
    }
}
```

## Implementations

### SQL Server Leader Election

Uses database locks for coordination:

```csharp
// Installation
dotnet add package Excalibur.LeaderElection.SqlServer

// Configuration
builder.Services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le =>
{
    le.UseSqlServer(sql =>
    {
        sql.ConnectionString(connectionString)
           .LockResource("my-app-leader");
    });
}));
```

SQL Server implementation features:
- Uses `sp_getapplock` for distributed locking
- Automatic heartbeat renewal
- Graceful handoff on shutdown
- Works with existing SQL Server infrastructure

### PostgreSQL Leader Election

Uses Postgres advisory locks for coordination:

```csharp
// Installation
dotnet add package Excalibur.LeaderElection.Postgres

// Builder API (recommended — 5 canonical connection overloads)
builder.Services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le
    .UsePostgres(pg =>
    {
        pg.ConnectionString("Host=localhost;Database=myapp;")
          .LockKey(12345);
    })));

// Or with NpgsqlDataSource (Azure, custom pooling)
builder.Services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le
    .UsePostgres(pg => pg.DataSource(npgsqlDataSource).LockKey(42))));
```

PostgreSQL implementation features:
- Uses `pg_try_advisory_lock` for distributed locking (session-level)
- Lock automatically released when connection closes
- Health-based leader election with candidate health tracking table
- Factory pattern for multiple independent elections
- AOT-safe with `[GeneratedRegex]` SQL identifier validation and `JsonSerializerContext`
- `ValidateDataAnnotations` + `ValidateOnStart` on all options
- `IValidateOptions<LeaderElectionOptions>` cross-property validator (see [Configuration headroom and runtime deadlines](#configuration-headroom-and-runtime-deadlines))

#### Health-Based Election (Postgres)

PostgreSQL supports health-based leader election where unhealthy leaders automatically step down:

```csharp
// Register health-based election with builder API
builder.Services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le
    .UsePostgres(pg =>
    {
        pg.ConnectionString("Host=localhost;Database=myapp;")
          .LockKey(12345);
    })
    .WithHealthChecks()
    .WithOptions(opts =>
    {
        opts.LeaseDuration = TimeSpan.FromSeconds(30);
        opts.RenewInterval = TimeSpan.FromSeconds(10);
    })));
```

Health-based options:

| Option | Default | Description |
|--------|---------|-------------|
| `SchemaName` | `"public"` | Schema for the health tracking table |
| `TableName` | `"leader_election_health"` | Health tracking table name |
| `AutoCreateTable` | `true` | Auto-create schema and table on first start |
| `StepDownWhenUnhealthy` | `true` | Leader voluntarily releases lock when unhealthy |
| `HealthExpirationSeconds` | `60` | Stale health record expiration |
| `CommandTimeoutSeconds` | `5` | SQL command timeout |

### Redis Leader Election

Uses Redis for coordination:

```csharp
// Installation
dotnet add package Excalibur.LeaderElection.Redis

// Configuration via builder API
builder.Services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le =>
{
    le.UseRedis(redis =>
    {
        redis.ConnectionString("localhost:6379")
             .LockKey("myapp:leader")
             .Database(0);
    })
    .WithOptions(opts =>
    {
        opts.LeaseDuration = TimeSpan.FromSeconds(30);
        opts.RenewInterval = TimeSpan.FromSeconds(10);
    });
}));
```

Redis implementation features:
- Uses `SET NX EX` for atomic lock acquisition
- Lua scripts for atomic operations
- Measure acquisition and renewal latency with the configured Redis deployment and intended check frequency.

### Consul Leader Election

Uses Consul sessions for coordination:

```bash
dotnet add package Excalibur.LeaderElection.Consul
```

```csharp
builder.Services.AddConsulLeaderElection(consul =>
{
    consul.Address("http://localhost:8500")
          .SessionTtl(TimeSpan.FromSeconds(30))
          .ResourceName("order-processor");
});
```

#### Naming the resource

`ResourceName` names the resource this application contends for, and makes a single `ILeaderElection` for
it resolvable from the container. **Without it, the registration provides an `ILeaderElectionFactory`
only** — every election must then be created from that factory explicitly, and the outbox drain is not
fenced. With it, the outbox drain is fenced on the named election automatically.

The name is combined with the key prefix (`LockKey`) to form the Consul KV key:

```csharp
builder.Services.AddConsulLeaderElection(consul =>
{
    consul.Address("http://consul:8500")
          .Token("my-acl-token")
          .LockKey("orders-service/leader")   // key prefix
          .ResourceName("outbox-drain");      // resource under that prefix
});
```

:::caution The name must be unique to your application

A Consul KV key is shared across **every** application pointed at the same Consul cluster. That is why the
name is required rather than defaulted: a framework-chosen name would put unrelated applications into
contention for one lock, each stalling the other's work. Choose a name that is unique to this application
across the cluster.
:::

If you prefer to name resources at the call site instead — or need several independent leadership scopes —
leave `ResourceName` off and register each election explicitly:

```csharp
// Factory-only registration, then a singleton election per resource
builder.Services.AddConsulLeaderElection(consul => consul.Address("http://localhost:8500"));
builder.Services.AddConsulLeaderElectionForResource("my-processor");
```

### Kubernetes Leader Election

Uses Kubernetes Lease objects for coordination:

```csharp
// Installation
dotnet add package Excalibur.LeaderElection.Kubernetes

// Configuration with hosted service
builder.Services.AddExcaliburKubernetesLeaderElectionHostedService(
    "my-processor",
    options =>
    {
        options.LeaseDuration = TimeSpan.FromSeconds(15);
        options.RenewInterval = TimeSpan.FromSeconds(10);
    });
```

Kubernetes implementation features:
- Uses native Kubernetes Lease objects
- Auto-detects in-cluster vs local kubeconfig
- Integrates as a hosted service for automatic lifecycle management

### In-Memory (Testing)

For unit tests and local development:

```csharp
builder.Services.AddInMemoryLeaderElection();
```

:::note

`AddInMemoryLeaderElection` registers `ILeaderElectionFactory`, not `ILeaderElection` directly.
Use the factory to create election instances for specific resources:

```csharp
var factory = serviceProvider.GetRequiredService<ILeaderElectionFactory>();
var election = factory.CreateElection("my-resource", candidateId: null);
```
:::

## Configuration Options

```csharp
public class LeaderElectionOptions
{
    /// <summary>
    /// How long a lease is valid before it expires.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How often to renew the lease (should be less than LeaseDuration).
    /// </summary>
    public TimeSpan RenewInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Retry interval when not leader.
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Grace period before declaring leadership lost.
    /// </summary>
    public TimeSpan GracePeriod { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Optional configured identity; the provider resolves an unset value.
    /// </summary>
    public string? InstanceId { get; set; }

    /// <summary>
    /// Enable health-based leader election.
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    /// Minimum health score (0.0 to 1.0) required to become or remain leader.
    /// </summary>
    public double MinimumHealthScore { get; set; } = 0.8;

    /// <summary>
    /// Automatically step down when health drops below MinimumHealthScore.
    /// </summary>
    public bool StepDownWhenUnhealthy { get; set; } = true;

    /// <summary>
    /// Custom metadata for this candidate (e.g., region, version).
    /// </summary>
    public IDictionary<string, string> CandidateMetadata { get; } = new Dictionary<string, string>();
}
```

## Patterns and Best Practices

### Graceful Shutdown

Release leadership cleanly on application shutdown:

```csharp
public class LeaderAwareService : BackgroundService
{
    private readonly ILeaderElection _leaderElection;
    private readonly ILogger<LeaderAwareService> _logger;

    public LeaderAwareService(
        ILeaderElection leaderElection,
        ILogger<LeaderAwareService> logger)
    {
        _leaderElection = leaderElection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _leaderElection.StartAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_leaderElection.IsLeader)
                {
                    await DoWorkAsync(ct);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        finally
        {
            // Always stop on shutdown to release leadership
            await _leaderElection.StopAsync(CancellationToken.None);
            _logger.LogInformation("Released leadership on shutdown");
        }
    }
}
```

### Handling Leadership Loss

React appropriately when leadership is lost using events:

```csharp
public class LeadershipAwareProcessor : BackgroundService
{
    private readonly ILeaderElection _leaderElection;
    private readonly IWorkQueue _queue;
    private volatile bool _isLeader;

    public LeadershipAwareProcessor(ILeaderElection leaderElection, IWorkQueue queue)
    {
        _leaderElection = leaderElection;
        _queue = queue;

        // Track leadership state via events
        _leaderElection.BecameLeader += (_, _) => _isLeader = true;
        _leaderElection.LostLeadership += (_, _) => _isLeader = false;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _leaderElection.StartAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_isLeader)
                {
                    var item = await _queue.DequeueAsync(ct);

                    // Check leadership before processing
                    if (!_isLeader)
                    {
                        await _queue.RequeueAsync(item);
                        continue;
                    }

                    await ProcessItemAsync(item, ct);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
            }
        }
        finally
        {
            await _leaderElection.StopAsync(CancellationToken.None);
        }
    }
}
```

### Observing Acquisition Failures

`BecameLeader` and `LostLeadership` only tell you when leadership *changes*. A follower that repeatedly loses
the acquisition race — or an instance hitting backend errors on every attempt — never raises either event, so
contention and connectivity problems can stay invisible. The `AcquisitionFailed` event closes that gap: it is
raised whenever this instance fails to acquire leadership, either by losing the race to another candidate or
because the acquisition attempt threw.

```csharp
_leaderElection.AcquisitionFailed += (_, args) =>
{
    // args.CandidateId   — this instance's candidate ID
    // args.ResourceName  — the contended resource
    // args.Reason        — short, human-readable failure reason
    // args.Exception     — the error, or null when it was simply a lost race
    // args.Timestamp     — when the failure occurred (UTC)

    if (args.Exception is not null)
        _logger.LogWarning(args.Exception,
            "Leader acquisition error for {Resource}: {Reason}", args.ResourceName, args.Reason);
};
```

:::note Cadence

`AcquisitionFailed` fires **per failed acquisition attempt** — that is, roughly once per poll while this
instance is a standby follower — **not** once per leadership state transition. Expect it to fire steadily on
every non-leader instance under normal operation; treat a raised `Exception` (rather than a plain lost race)
as the actionable signal.

:::

When the telemetry decorator is active (`Excalibur.LeaderElection` with observability), each failure is also
recorded on the leader-election acquisitions counter with a `result=failed` tag, so you can alert on failed
acquisitions without wiring the event yourself.

### Multiple Leader Elections

Use the factory for multiple independent elections. See [When to Use Factory vs Single](#when-to-use-factory-vs-single-registration) above for guidance on choosing between single registration and factory.

```csharp
// Register factory for multiple lock resources
builder.Services.AddSqlServerLeaderElectionFactory(connectionString);

public class MultiResourceLeaderService : BackgroundService
{
    private readonly ILeaderElectionFactory _factory;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Create separate leader elections for each resource
        // candidateId: null uses the default InstanceId from LeaderElectionOptions
        var outboxElection = _factory.CreateElection("outbox-processor", candidateId: null);
        var projectorElection = _factory.CreateElection("event-projector", candidateId: null);
        var cleanupElection = _factory.CreateElection("cleanup-job", candidateId: null);

        var elections = new[] { outboxElection, projectorElection, cleanupElection };

        // Start all elections
        await Task.WhenAll(elections.Select(e => e.StartAsync(ct)));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Process each workload if we're leader for it
                if (outboxElection.IsLeader)
                    await ProcessOutboxAsync(ct);

                if (projectorElection.IsLeader)
                    await ProcessProjectionsAsync(ct);

                if (cleanupElection.IsLeader)
                    await RunCleanupAsync(ct);

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        finally
        {
            await Task.WhenAll(elections.Select(e => e.StopAsync(CancellationToken.None)));
        }
    }
}
```

### Health Checks

A built-in health check is provided in the `Excalibur.LeaderElection` package. Register it with the standard ASP.NET Core health checks builder:

```csharp
builder.Services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le =>
{
    le.UseSqlServer(sql => sql.ConnectionString(connectionString).LockResource("my-resource"));
}));

builder.Services.AddHealthChecks()
    .AddLeaderElectionHealthCheck();
```

The built-in `LeaderElectionHealthCheck` reports:
- **Healthy**: This instance is the leader, or a valid leader is observed
- **Degraded**: No leader is detected, but the service is running
- **Unhealthy**: An exception occurs when querying leader election state

It is provider-agnostic and works with any `ILeaderElection` implementation (SQL Server, Redis, Consul, Kubernetes).

## Common Use Cases

### Outbox Pattern

Ensure only one instance publishes outbox messages:

```csharp
public class OutboxPublisher : BackgroundService
{
    private readonly ILeaderElection _leaderElection;
    private readonly IOutboxStore _outbox;
    private readonly IMessageBus _messageBus;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _leaderElection.StartAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_leaderElection.IsLeader)
                {
                    var messages = await _outbox.GetPendingAsync(100, ct);

                    foreach (var message in messages)
                    {
                        await _messageBus.PublishAsync(message, ct);
                        await _outbox.MarkAsPublishedAsync(message.Id, ct);
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            }
        }
        finally
        {
            await _leaderElection.StopAsync(CancellationToken.None);
        }
    }
}
```

### Scheduled Tasks

Run scheduled jobs on exactly one instance:

```csharp
public class DailyReportGenerator : BackgroundService
{
    private readonly ILeaderElection _leaderElection;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _leaderElection.StartAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Wait until midnight
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddDays(1);
                await Task.Delay(nextRun - now, ct);

                // Only the leader generates the report
                if (_leaderElection.IsLeader)
                {
                    await GenerateDailyReportAsync(ct);
                }
            }
        }
        finally
        {
            await _leaderElection.StopAsync(CancellationToken.None);
        }
    }
}
```

### Event Projection Processing

**Do not write this loop.** Asynchronous projections already run in a hosted processor that gates itself
on leadership, and it does so by *presence*: register leader election and the projection host picks it up
from the container. There is nothing to wire between them.

```csharp
services.AddExcalibur(excalibur => excalibur
    .AddLeaderElection(le => le
        .UseSqlServer(sql => sql
            .ConnectionString(connectionString)
            .LockResource("my-app-projections"))
        .WithHealthChecks())
    .AddEventSourcing(es => es
        .AddProjection<OrderSummary>(p => p
            .Async()                                  // runs in the hosted processor
            .When<OrderPlaced>((proj, e) => proj.Total = e.Amount)
            .When<OrderShipped>((proj, e) => proj.ShippedAt = e.ShippedAt))));
```

With no leader election registered the processor runs unconditionally, which is what you want in a
single-instance deployment. Register it and only the leader applies events.

**Why the checkpoint is not sufficient on its own, and why this needs leadership at all.** It is tempting
to assume two instances can both read the subscription and let the checkpoint sort them out. They cannot:
each reader applies a whole batch *before* either contests the checkpoint, so the work is duplicated even
though the mark is not. The checkpoint guards the mark, not the work. That is the reason the gate exists,
and it is the part a hand-rolled loop usually gets wrong.

There is no non-leader-gated alternative: a stored projection is only ever written by the host, so
leader election is how you stop two instances applying the same subscription. A caller that needs to
observe its own write does that at the read instead — see
[Reading Your Own Writes](../event-sourcing/projections.md#reading-your-own-writes-session-consistency).

## Health-Based Elections

`IHealthBasedLeaderElection` extends standard elections with health awareness. Unhealthy leaders automatically step down, and only healthy candidates can become leader.

```csharp
var election = _electionFactory.CreateHealthBasedElection(
    resourceName: "critical-processor",
    candidateId: null);

await election.StartAsync(ct);

// Report health status
await election.UpdateHealthAsync(
    isHealthy: true,
    metadata: new Dictionary<string, string>
    {
        ["cpu"] = "45%",
        ["memory"] = "2.1GB",
        ["queue_depth"] = "12"
    },
    ct);

// Query all candidates' health
var candidates = await election.GetCandidateHealthAsync(ct);

foreach (var candidate in candidates)
{
    // candidate.CandidateId
    // candidate.IsHealthy
    // candidate.HealthScore (0.0 to 1.0)
    // candidate.IsLeader
    // candidate.LastUpdated
    // candidate.Metadata
}
```

### CandidateHealth

Each candidate exposes health information:

| Property | Type | Description |
|----------|------|-------------|
| `CandidateId` | `string` | Unique candidate identifier |
| `IsHealthy` | `bool` | Whether the candidate is healthy |
| `HealthScore` | `double` | Score from 0.0 to 1.0 |
| `IsLeader` | `bool` | Whether this candidate is the current leader |
| `LastUpdated` | `DateTimeOffset` | When health was last reported |
| `Metadata` | `IDictionary<string, string>` | Custom health metadata |

### In-memory election lifetimes

Dispose each directly constructed `InMemoryLeaderElection` when its owner finishes, preferably with
`await using`. `StopAsync` removes the running membership but keeps the instance restartable; it does
not release the instance's resource bucket. The last owner to dispose releases that bucket, including
when an election was never started. Owners sharing a resource keep the same bucket until all dispose.

This bounds candidate buckets by resource names held by undisposed instances. It does not reclaim
abandoned instances automatically. Fencing-token history remains in the shared state after bucket
retirement so recreating a resource cannot reuse an earlier token; this is not a bound on all shared-state
memory. Use a shared state with the intended test or application lifetime.

### Making timestamps deterministic in your tests

`CandidateHealth.LastUpdated` and the acquisition instant on `CurrentLeadership` come from the
`TimeProvider` the election was constructed with. Supply your own and those timestamps become yours to
control:

```csharp
using Microsoft.Extensions.Time.Testing;

var clock = new FakeTimeProvider();

await using var election = new InMemoryLeaderElection(
    resourceName: "orders-processor",
    options: Options.Create(new LeaderElectionOptions { InstanceId = "candidate-1" }),
    logger: NullLogger<InMemoryLeaderElection>.Instance,
    sharedState: new InMemoryLeaderElectionSharedState(),
    timeProvider: clock);

await election.StartAsync(ct);

var registeredAt = (await election.GetCandidateHealthAsync(ct)).Single().LastUpdated;
// registeredAt == clock.GetUtcNow()

clock.Advance(TimeSpan.FromMinutes(5));
await election.UpdateHealthAsync(isHealthy: true, metadata: null, ct);

var updatedAt = (await election.GetCandidateHealthAsync(ct)).Single().LastUpdated;
// updatedAt - registeredAt == 5 minutes, with no waiting
```

The provider is optional on every election; omit it and the system clock is used. The Consul and Kubernetes
elections take it as the last constructor parameter in the same way.

:::note
This controls **timestamps the election records**, not lease or session expiry, which the coordination
store enforces on its own clock. A fake provider will not make Consul or Kubernetes expire a lease early.
:::

This covers the election's events too. The `Timestamp` on `BecameLeader`, `LostLeadership`,
`LeaderChanged` and `AcquisitionFailed` is stamped from the same provider, so an event's `Timestamp` and
the properties above agree on when a transition happened:

```csharp
election.BecameLeader += (_, e) =>
{
    // e.Timestamp == clock.GetUtcNow()
};
```

`DefaultLeaderElectionWatcher` takes the provider as an optional last constructor parameter and stamps
`LeaderChangeEvent.ChangedAt` from it, so a watch loop is deterministic on the same clock.

## Fencing tokens

A fencing token is a strictly monotonic number minted on each leadership acquisition. Passing it to a shared resource lets that resource **reject a write from a stale leader** — one that was paused (GC, network partition) long enough to lose leadership without noticing — because the stale leader's token is lower than the token the resource has already seen. Fencing tokens are the standard defence against the split-brain write hazard.

### SQL Server and PostgreSQL Fencing Transactions

The SQL Server and PostgreSQL fencing providers use their own connections with ambient transaction enlistment disabled for token issuance, reads and validation. A caller's business rollback must not roll back infrastructure fencing state. This includes first-use sequence creation: transactional DDL rolled back with the caller could remove the sequence and let a later provider reuse an already issued token.

Business writes keep their ordinary transaction semantics; this autonomous fencing boundary does not commit the caller's work. Tokens may contain gaps and must not be reused for the same resource. The election's owned infrastructure workers likewise suppress the ambient business transaction while preserving diagnostic context. Protected application writes still need atomic resource-side fencing; an autonomous token mint does not make an unrelated write atomic.

### Fencing is on by default

Every built-in provider — Consul, Kubernetes, MongoDB, Postgres, Redis, SQL Server, and in-memory — registers its own arbitrated `IFencingTokenProvider` automatically as part of `Use*()` / `Add*LeaderElection()`. You do not need to call `WithFencingTokens()` or `Add*FencingTokenProvider()` yourself:

```csharp
using Microsoft.Extensions.DependencyInjection;

// Fencing tokens are already active here — no extra call needed.
services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le
    .UseKubernetes(opts => opts.Namespace("default"))));
```

The default exists because the alternative failure mode is silent: a leader election with no fencing lets a stalled ex-leader's writes land after a new leader has already taken over, with no exception anywhere to notice. Opt out only for a deployment that genuinely does not need fencing:

```csharp
services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le
    .UseKubernetes(opts => opts.Namespace("default"))
    .WithoutFencingTokens()));
```

After opting out, `IFencingTokenProvider` does not resolve (`GetService<IFencingTokenProvider>()` returns `null`), and the election runs in non-fencing mode.

A consumer-supplied `ILeaderElection` — one that is not a built-in provider — still uses the explicit opt-in shown below; `Add*FencingTokenProvider()` and `WithFencingTokens()` remain available for that case, and composing them with a built-in provider's default registration is harmless (each uses `TryAdd`, so whichever registers first wins):

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le
    .WithFencingTokens()));

services.AddFencingTokenSupport<MyCustomFencingTokenProvider>();
```

Each backend ships a matching manual registration, for the consumer-supplied scenario above or for overriding the default provider with your own:

| Extension | Package |
|-----------|---------|
| `AddConsulFencingTokenProvider()` | `Excalibur.LeaderElection.Consul` |
| `AddKubernetesFencingTokenProvider()` | `Excalibur.LeaderElection.Kubernetes` |
| `AddMongoDbFencingTokenProvider()` | `Excalibur.LeaderElection.MongoDB` |
| `AddPostgresFencingTokenProvider(connectionString)` | `Excalibur.LeaderElection.Postgres` |
| `AddRedisFencingTokenProvider()` | `Excalibur.LeaderElection.Redis` |
| `AddSqlServerFencingTokenProvider(connectionString)` | `Excalibur.LeaderElection.SqlServer` |
| `AddInMemoryFencingTokenProvider()` | `Excalibur.LeaderElection.InMemory` |

`IFencingTokenProvider` exposes authoritative reads and validation. The six self-minting elections (Consul, MongoDB, Postgres, Redis, SQL Server and in-memory) consume `IFencingTokenIssuer`, which extends that reader with `IssueTokenAsync`. Kubernetes exposes only the reader: leadership transitions advance its native Lease counter.

Supplying your own mint-capable reader via `TryAddSingleton<IFencingTokenProvider, T>()` before `Use*()` / `Add*LeaderElection()` selects that implementation for the default issuer projection too. A selected reader without mint capability cannot satisfy a self-minting election. Independently registering `IFencingTokenIssuer` bypasses that projection; the application must ensure both services refer to the same authority. See [the issuer migration guide](../migration/fencing-token-issuer.md) for custom registrations and ownership.

:::note MongoDB fences durably by default
MongoDB leader election defaults to a **durable per-resource fencing counter** — a separate, TTL-free collection — when you don't supply your own provider. This is deliberate: a token stored in the lock document itself would be reset when the lock document is deleted on graceful release or expired by its TTL index, letting a stale token from a restarted instance validate as current (split-brain). The counter is separate and TTL-free; deleting the counter collection or restoring older history can still reset it. Restart/restore durability under the chosen deployment policy is UNVERIFIED here.
:::

:::note In-memory fencing is process-local
The in-memory provider mints its fencing token from an in-process counter, because the election, the provider, and the counter are all in the same process. It fences a stale in-process leader against a live one in the same process, but it is not a substitute for cross-process protection — do not use in-memory leader election to protect anything shared between processes.
:::

### Token exhaustion fails closed

Because tokens must never wrap or repeat — a reused value would let a stale leader validate as current — a provider that cannot mint the next monotonic token throws `FencingTokenExhaustedException` (namespace `Excalibur.Dispatch.LeaderElection.Fencing`) and **fails closed**: it refuses to mint, so leadership cannot be granted or renewed, and a leader that hits exhaustion mid-tenure relinquishes rather than continue with an unsafe token.

Exhaustion is practically unreachable for the 64-bit self-minting domains. Kubernetes instead checks its 32-bit `Lease.spec.leaseTransitions` during the native ownership transition and refuses an acquisition that would overflow; its reader can still report the last authoritative positive mark.

Validation compares a positive presented token with the currently recorded positive mark using `>=`. It does not prove that the presented value was issued, nor that it remains current after the read returns. Pin the token from `Leadership` for the tenure and enforce it atomically at the protected resource. Deleting, resetting or rolling back the authority's counter history can admit stale tokens; this API does not add a durable tenure identity across such resets.

## Troubleshooting

### Lock Not Released

If a process crashes without releasing the lock, the lease will automatically expire after `LeaseDuration`. Another instance will then acquire leadership.

### Split Brain

Configure appropriate timeouts to prevent split brain:

```csharp
builder.Services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le =>
{
    le.UseSqlServer(sql =>
    {
        sql.ConnectionString(connectionString)
           .LockResource("my-resource");
    });
}));

// Configure lease timing via options
builder.Services.Configure<LeaderElectionOptions>(options =>
{
    // Lease duration must be longer than renewal interval
    options.LeaseDuration = TimeSpan.FromSeconds(30);
    options.RenewInterval = TimeSpan.FromSeconds(10);

    // Account for network latency and clock skew
    // Renewal should happen at least 2-3 times before expiry
});
```

## Next Steps

- **[Event Sourcing](../event-sourcing/index.md)** - Use with event projections
- **[CQRS](../cqrs/index.md)** - Coordinate read model updates
- **[Dispatch Introduction](../intro.md)** - Background service patterns

## See Also

- [Kubernetes Deployment](../deployment/kubernetes.md) - Kubernetes deployment with leader election
- [Resilience with Polly](../operations/resilience-polly.md) - Circuit breakers and retry policies
- [Patterns Overview](../patterns/index.md) - Architectural patterns for distributed systems
