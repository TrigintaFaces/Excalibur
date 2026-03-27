# Standalone A3 Sample

Demonstrates using `Excalibur.A3.Core` for lightweight grant management and authorization without any database, event sourcing, outbox, or Dispatch pipeline.

## What This Proves

- `AddExcaliburA3Core()` registers in-memory stores with zero infrastructure dependencies
- Grant CRUD (save, query, delete) works out of the box
- Authorization checks (grant existence) work without a Dispatch pipeline
- ISP sub-interfaces (`IGrantQueryStore`) are accessible via `GetService(Type)`
- Activity group management works independently

## Dependencies

Only `Excalibur.A3.Core` (which transitively brings `A3.Abstractions`, `Domain`, `Dispatch.Abstractions`).

No database. No event sourcing. No outbox. No Dispatch pipeline. No CQRS commands.

## Running

```bash
dotnet run --project samples/06-security/StandaloneA3
```
