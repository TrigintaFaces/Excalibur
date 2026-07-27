# Excalibur.Security Namespace-vs-Folder Policy

> **Beads:** bd-58hysd | **Sprint:** S822 | **Status:** Documented

## Summary

`Excalibur.Security` uses folders for **file organization** without creating sub-namespaces, except for two folders that represent distinct API sub-surfaces.

## Policy Rule

**Default:** New folders in `Excalibur.Security` use the **root namespace** `Excalibur.Security`. Folders are organizational — they group related files without fragmenting the consumer's `using` directives.

**Exception:** A sub-namespace is warranted only when the folder represents a **distinct API surface** that consumers may want to import independently from the root namespace. Currently this applies to:

- `Diagnostics/` -> `Excalibur.Security.Diagnostics` (health checks, event IDs, telemetry constants)
- `EventStores/` -> `Excalibur.Security.EventStores` (security event store implementations)

## Current Mapping

| Folder | Namespace | Rationale |
|--------|-----------|-----------|
| `/` (root) | `Excalibur.Security` | Core types, options, extensions |
| `Authentication/` | `Excalibur.Security` | Same concern surface as root |
| `Auditing/` | `Excalibur.Security` | Same concern surface as root |
| `Encryption/` | `Excalibur.Security` | Same concern surface as root |
| `RateLimiting/` | `Excalibur.Security` | Same concern surface as root |
| `Signing/` | `Excalibur.Security` | Same concern surface as root |
| `Validation/` | `Excalibur.Security` | Same concern surface as root |
| `Diagnostics/` | `Excalibur.Security.Diagnostics` | Distinct API surface (telemetry) |
| `EventStores/` | `Excalibur.Security.EventStores` | Distinct API surface (stores) |
| `Middleware/` | `Microsoft.Extensions.DependencyInjection` | Standard DI extension convention |

## When to Add a Sub-Namespace

Before adding a sub-namespace to a new folder, answer:

1. **Would consumers import this independently?** If a consumer uses `Excalibur.Security.NewThing` types without ever importing `Excalibur.Security`, it warrants a sub-namespace.
2. **Does it represent a distinct API surface?** Infrastructure types (diagnostics, stores) are consumed differently from domain security types.
3. **Would flat namespace cause naming collisions?** If new types conflict with existing root-namespace types, a sub-namespace resolves ambiguity.

If none apply, use `Excalibur.Security` and organize with folders only.

## Consistency with Other Packages

This pattern aligns with how Microsoft organizes packages like `Microsoft.Extensions.Logging` (root namespace) with `Microsoft.Extensions.Logging.Abstractions` as a separate package/namespace only when truly independent.
