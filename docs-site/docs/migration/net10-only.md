---
sidebar_position: 99
title: Migrating to .NET 10 (Sprint 797)
description: Sprint 797 collapsed Excalibur.Dispatch to .NET 10.0 exclusively. This guide explains what changed, why, and how to update consumer projects.
---

# Migrating to .NET 10 Only

As of Sprint 797 (April 2026), Excalibur.Dispatch targets **.NET 10.0 exclusively**. Every shipping NuGet package (Dispatch core, Excalibur domain/event-sourcing, transports, hosting providers, compliance, samples, and templates) dropped `net8.0` and `net9.0` from `<TargetFrameworks>` in a single sprint.

This page is forward-looking guidance for consumers. For the sprint's architectural rationale see [ADR-108 (.NET 10 multi-targeting)](https://github.com/thomaswdean/Excalibur.Dispatch/blob/main/management/architecture/adr-108-net10-multi-targeting.md) and the [Sprint 797 review](https://github.com/thomaswdean/Excalibur.Dispatch/blob/main/management/sprints/sprint-797-review.md).

## What changed

- **Target framework:** `<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>` → `<TargetFramework>net10.0</TargetFramework>` across every shipping project.
- **Dependencies refreshed:** `Microsoft.Extensions.*` → `10.0.6`, `Azure.Functions.Worker` → `2.51.0`, `OpenTelemetry` → `1.15.2`, `Polly` → `8.6.6`, Elastic.Transport → `0.16.0`, Elasticsearch client → `9.3.4`, plus the AWS/Azure/GCP SDK families.
- **Preprocessor cleanup:** 705 deletions of dead `#if NET8_0` / `#if NET9_0` branches. `IsExternalInit.cs` in `Excalibur.Dispatch.SourceGenerators` retains its `#if NETSTANDARD2_0` polyfill because Roslyn source generators must target `netstandard2.0` for compatibility with the Roslyn SDK — this is TFM polyfill, not feature gating.
- **PublicAPI baselines:** Per-TFM `PublicAPI.Shipped.txt` variance removed. Each shipping package now owns a single baseline.
- **CI shard matrix:** 10 shards execute once per build instead of once per TFM. Per-TFM shard multipliers retired.
- **Azure Functions Worker:** Upgraded to v2 SDK. `FunctionContextProxy` now matches the abstract `FunctionContext` base per `AzureFunctionsHostProvider` contract.
- **Azure Key Vault:** An internal `ISecretClient` seam was introduced in `Excalibur.Security.Azure` so tests can fake secret operations without reflection. Consumer APIs are unchanged; the seam is `internal`.

## Why .NET 10 only

1. **.NET 9 STS lifecycle ends May 2026.** Continuing to ship .NET 9 targets after end-of-support would leave consumers on an unsupported runtime for security patches.
2. **.NET 10 is the current LTS** (November 2025 → November 2028). Framework-as-NuGet with single-target LTS aligns with Microsoft's own pattern for new libraries (`Microsoft.Extensions.*` 10.x, `Azure.*` SDKs).
3. **AOT and trimming gains.** .NET 10 ships improvements to `System.Text.Json` source generators, trim analysis, `System.Threading.Lock`, and UTF-8 literals. Multi-targeting below .NET 10 blocked access to these.
4. **Greenfield invariant.** The framework is pre-release with no shipped consumers. The migration window is architecturally optimal; breaking the TFM contract is free today and expensive later.

## Required consumer changes

### 1. Update your project TFM

```xml
<!-- Before -->
<TargetFramework>net8.0</TargetFramework>

<!-- After -->
<TargetFramework>net10.0</TargetFramework>
```

If your project currently multi-targets (`<TargetFrameworks>net8.0;net10.0</TargetFrameworks>`), drop the `net8.0` entry. Excalibur.Dispatch 10.x packages no longer ship `net8.0` or `net9.0` assets, so your `net8.0` build will fail to resolve.

### 2. Install .NET 10 SDK

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) on every development machine and every CI runner. Update `global.json` if present:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

Update Docker base images:

```dockerfile
# Before
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# After
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
```

Update CI runner setup actions (`actions/setup-dotnet` and equivalents) to pin `dotnet-version: 10.0.x`.

### 3. Serverless runtime identifiers

| Platform | Old | New |
|----------|-----|-----|
| AWS Lambda | `dotnet8` / `dotnet9` | `dotnet10` |
| Google Cloud Functions | `dotnet9` | `dotnet10` |
| Azure Functions | Worker v1 (`Microsoft.Azure.Functions.Worker` 1.x) | Worker v2 (`2.51.0+`) |

Project templates (`dotnet new dispatch-api`, `dotnet new dispatch-worker`, `dotnet new dispatch-functions`) generate `.csproj` and Dockerfiles against .NET 10 only. The `--Framework` option has been narrowed to `net10.0`.

### 4. Dependency upgrades (optional but recommended)

If your project directly references any of the packages Dispatch also pulls in (for example, you register your own `OpenTelemetry` pipeline or configure Azure Functions Worker independently), align your versions with the Dispatch floor:

| Package family | Minimum |
|----------------|---------|
| `Microsoft.Extensions.*` | `10.0.6` |
| `Microsoft.Azure.Functions.Worker*` | `2.51.0` |
| `Azure.Security.KeyVault.Secrets` | `4.10.0` |
| `OpenTelemetry*` | `1.15.2` |
| `Polly` | `8.6.6` |
| `Elastic.Transport` | `0.16.0` |
| `Elastic.Clients.Elasticsearch` | `9.3.4` |

No public Excalibur.Dispatch API signatures changed as part of the dep refresh.

## No code changes required

Sprint 797 was a targeting-and-dependency sprint, not a behavior sprint. No public API was added, removed, renamed, or resignatured. The [Sprint 797 architect review](https://github.com/thomaswdean/Excalibur.Dispatch/blob/main/management/sprints/sprint-797-review.md) verifies `PublicAPI.Unshipped.txt` is empty on every shipping project; no new public symbols were introduced by the AOT audit or the `ISecretClient` seam (the seam is internal).

## Rollback

There is no rollback path within the 10.x release line. Consumers who cannot move to .NET 10 should pin to the last pre-S797 release and stay there. Because Sprint 797 lands before the framework's first stable release, no downstream consumers are pinned today.

## Related

- [Support Policy](../support.md) — current .NET version support matrix
- [AOT Migration Guide](../advanced/aot-migration-guide.md) — publishing consumer apps with `PublishAot=true`
- [Project Templates](../getting-started/project-templates.md) — `dotnet new dispatch-*` options on .NET 10
