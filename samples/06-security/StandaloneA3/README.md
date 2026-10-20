# Standalone A3 Sample

Demonstrates using `Excalibur.A3.Core` for lightweight grant management and authorization without any database, event sourcing, outbox, or Dispatch pipeline.

## What This Proves

- `AddExcaliburA3Core()` registers in-memory stores with zero infrastructure dependencies
- Grant CRUD (save, query, delete) works out of the box
- Framework authorization policies resolve activity-group permissions without a Dispatch pipeline
- ISP sub-interfaces (`IGrantQueryStore`) are accessible via `GetService(Type)`
- Activity group management works independently
- Atomic tenant revocation includes every grant type, preserves another tenant's grants, and records a retryable outcome
- Retrying the same operation ID preserves grants added after its first commit
- A new policy acquisition in the same scope reflects revocation and the later addition

## Dependencies

Only `Excalibur.A3.Core` (which transitively brings `A3.Abstractions`, `Domain`, `Dispatch.Abstractions`).

No database. No event sourcing. No outbox. No Dispatch pipeline. No CQRS commands.

The in-memory store is volatile. It demonstrates atomicity within one process, not durability across
restart. Production hosts should use a durable store and preserve revocation operation IDs when
retrying unknown outcomes. The ordinary `RevokeAllGrantsCommand` is confined to its authorized ambient
tenant; direct all-tenants store operations require explicit administrative authorization.
`SampleSubject` supplies a fixed local identity and tenant. Real hosts supply their authenticated
identity; the sample delegates permission evaluation to the framework's `IAuthorizationPolicyProvider`.

## Running

```bash
dotnet run --project samples/06-security/StandaloneA3
```

For a native Windows executable with trim/AOT diagnostics enabled:

```bash
dotnet publish samples/06-security/StandaloneA3 -c Release -r win-x64 -p:StandaloneA3NativeAot=true
```

The sample enables `PublishAot` within its executable project so it does not propagate to the
framework's compiler-analyzer projects. Native execution exercises the same tenant-isolation and
idempotent-retry assertions. This verifies the lightweight Core composition, not reflection-based
full-stack Dispatch registration or cloud grant providers.
