# AOT Compatibility Matrix

**Source:** Generated via S807 Phase A3 (`bd-ic1urd`) on commit `6d70559e9+` · **Sprint:** 807
**Author:** BackendDeveloper / FORGE
**Scope:** All shipping packages in `src/`

This document catalogs per-package AOT publishability status for the Excalibur framework. The matrix is derived from each package's `<IsAotCompatible>` `.csproj` setting, combined with audit of `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` annotations.

## Summary

| Status | Count | Description |
|---|---:|---|
| ✅ AOT-compatible | 143 | `<IsAotCompatible>true</IsAotCompatible>` — reflection usage (if any) is protected by `[Requires*]` annotations with source-gen / `DynamicallyAccessedMembers` preservation |
| ❌ AOT-incompatible | 27 | `<IsAotCompatible>false</IsAotCompatible>` — hard reflection dependency from a third-party SDK or runtime feature |

**Total:** 170 shipping packages audited.

## AOT-incompatible packages (27)

These packages explicitly opt out of AOT compilation via `<IsAotCompatible>false</IsAotCompatible>` with a justification comment. Root causes categorized:

### Third-party SDK reflection dependencies (21)

| Package | Root cause |
|---|---|
| `Excalibur.Dispatch.ClaimCheck.AwsS3` | AWSSDK.S3 reflection-based request/response serialization |
| `Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage` | Google.Cloud.Storage.V1 reflection-based gRPC serialization |
| `Excalibur.Dispatch.Transport.GooglePubSub` | Google.Cloud.PubSub.V1 + `PubSubBatchSerializer` dynamic code |
| `Excalibur.Dispatch.Transport.Kafka` | Confluent.Kafka runtime code generation for serializer resolution |
| `Excalibur.Compliance.Aws` | AWS SDK reflection (S806-moved package) |
| `Excalibur.Cdc.CosmosDb` | Microsoft.Azure.Cosmos change-feed deserialization |
| `Excalibur.Data.CosmosDb` | Microsoft.Azure.Cosmos document serialization |
| `Excalibur.Data.DynamoDb` | AWSSDK.DynamoDBv2 document model serialization |
| `Excalibur.Data.Firestore` | Google.Cloud.Firestore attribute-based mapping |
| `Excalibur.Data.OpenSearch` | OpenSearch.Client query DSL + document serialization |
| `Excalibur.EventSourcing.CosmosDb` | Microsoft.Azure.Cosmos event deserialization |
| `Excalibur.EventSourcing.DynamoDb` | AWSSDK.DynamoDBv2 event document serialization |
| `Excalibur.EventSourcing.Firestore` | Google.Cloud.Firestore event conversion |
| `Excalibur.Inbox.CosmosDb` | Microsoft.Azure.Cosmos inbox message deserialization |
| `Excalibur.Outbox.CosmosDb` | Microsoft.Azure.Cosmos outbox message serialization |
| `Excalibur.Outbox.DynamoDb` | AWSSDK.DynamoDBv2 outbox document serialization |
| `Excalibur.Outbox.Firestore` | Google.Cloud.Firestore outbox conversion |
| `Excalibur.Outbox.MongoDB` | MongoDB.Driver reflection-based BSON serialization |
| `Excalibur.Saga.CosmosDb` | Microsoft.Azure.Cosmos saga state deserialization |
| `Excalibur.LeaderElection.Consul` | Consul client reflection usage |
| `Excalibur.LeaderElection.Kubernetes` | KubernetesClient reflection usage |

### Serialization format requiring runtime code generation (2)

| Package | Root cause |
|---|---|
| `Excalibur.Dispatch.Serialization.Avro` | Apache.Avro runtime schema compilation + `Activator.CreateInstance` for `ISpecificRecord` |
| `Excalibur.Dispatch.Serialization.MessagePack` | MessagePack reflection-based serialization |

### Framework-internal dynamic code (1)

| Package | Root cause |
|---|---|
| `Excalibur.Caching` | Adaptive TTL strategies use `Expression.Compile` for dynamic cache policy evaluation |

### Not applicable (3)

| Package | Reason |
|---|---|
| `Excalibur.Dispatch.Analyzers` | Roslyn analyzer — runs in compiler, not runtime |
| `Excalibur.Dispatch.SourceGenerators` | Source generator — runs at build time (netstandard2.0) |
| `Excalibur.Dispatch.SourceGenerators.Analyzers` | Source-generator analyzer — not applicable |

## AOT-compatible packages (143)

All other shipping packages are AOT-compatible. For packages that use reflection internally (visible via `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` annotation counts), the reflection is guarded at the API boundary — AOT consumers get compile-time warnings if they call those paths, and can opt into source-gen alternatives where provided.

### Top AOT-compatible packages by annotation count

Packages with the most `[Requires*]` annotations (each annotation guards a specific reflection-using code path; consumers not taking the reflection path are fully AOT-safe):

| Package | `[Requires*]` count |
|---|---:|
| `Excalibur.Dispatch` | 304 |
| `Excalibur.Dispatch.Transport.AwsSqs` | 44 |
| `Excalibur.Dispatch.Transport.AzureServiceBus` | 44 |
| `Excalibur.Security` | 36 |
| `Excalibur.Dispatch.Observability` | 36 |
| `Excalibur.Dispatch.Hosting.AspNetCore` | 30 |
| `Excalibur.Dispatch.Transport.GooglePubSub` (incompat) | 27 |
| `Excalibur.Dispatch.Transport.Kafka` (incompat) | 27 |
| `Excalibur.Dispatch.Abstractions` | 18 |
| `Excalibur.Dispatch.Caching` | 17 |

## Annotation inventory (from S807 Phase A1 audit)

| Annotation | Repo-wide count |
|---|---:|
| `[RequiresUnreferencedCode]` | 588 |
| `[RequiresDynamicCode]` | 437 |
| `[UnconditionalSuppressMessage]` (trim-related) | 1,132 |
| `[DynamicallyAccessedMembers]` | 308 |

## AOT publishing guidance for consumers

### Recommended consumer workflow

1. Identify which Excalibur packages your application uses.
2. Cross-reference against this matrix; confirm all referenced packages are ✅ AOT-compatible.
3. If any referenced package is ❌ AOT-incompatible, identify whether the dependency can be swapped (e.g., `Excalibur.Data.CosmosDb` → `Excalibur.Data.Postgres` for an alternative storage provider that is AOT-compatible).
4. Add `<PublishAot>true</PublishAot>` to your consumer project.
5. Publish with `dotnet publish -p:PublishAot=true` and review trim/AOT warnings. Framework annotations at `[Requires*]` sites will surface as warnings; suppress only after verifying the code path is not taken in your application.

### Consumer-provided source generators

Several Excalibur packages provide `JsonSerializerContext` source-gen types that consumers can extend or use directly to avoid reflection-based JSON serialization. Key examples:

- `Excalibur.Compliance` → `Excalibur.Compliance.Encryption.EncryptionJsonContext`
- `Excalibur.Compliance.SqlServer` → `SqlServerComplianceJsonContext`
- `Excalibur.Compliance.Postgres` → `PostgresComplianceJsonContext`
- `Excalibur.AuditLogging.*` → per-package JSON contexts

Consumers writing JSON-serialization hot paths should prefer `JsonSerializerContext` source-gen over reflection-based `JsonSerializer.Serialize<T>` calls whenever the type is known at compile time.

## Follow-up items (S808+)

1. **Phase A2 JsonSerializerContext expansion** (from S807 plan §A2, bd-xca14i) — Approximately 50-70 concrete-type `JsonSerializer.Serialize<T>` / `Deserialize<T>` call sites in shipping packages remain candidates for source-gen migration. Systematic migration deferred to a dedicated FORGE sprint.
2. **Windows AOT environment prerequisites** (bd-b2tgq9, closed S809 as environmental) — see [§ Windows AOT publish prerequisites](#windows-aot-publish-prerequisites) below. AOT publish on Windows requires Microsoft C++ Build Tools and the Windows SDK installed on the developer or CI machine; absence of `link.exe` / `vswhere.exe` is an environment prerequisite, not a framework defect.
3. **Audit `[UnconditionalSuppressMessage]` density** — 1,132 suppressions across the codebase; a periodic review should confirm each is paired with verified runtime AOT safety.

## Windows AOT publish prerequisites

AOT publish on Windows (`dotnet publish -c Release -r win-x64 --self-contained /p:PublishAot=true`) invokes the native linker `link.exe` as a final step after IL-to-native compilation. That linker and its accompanying Windows SDK libraries are **not** installed by the .NET SDK; they ship with the Microsoft Visual Studio Build Tools family. A developer machine or CI image that has only the .NET SDK installed will fail AOT publish at the link step with an error of the form:

```
error : Platform linker not found. To fix this problem, install Microsoft Visual Studio 2022 with the "Desktop development with C++" workload.
```

This is expected behaviour and not a framework defect.

### Required components

For Windows AOT publish to succeed, install either:

1. **Visual Studio 2022** (Community / Professional / Enterprise) with the **Desktop development with C++** workload, **or**
2. The standalone **Visual Studio Build Tools 2022** with:
   - `Microsoft.VisualStudio.Component.VC.Tools.x86.x64` (MSVC v143 build tools)
   - `Microsoft.VisualStudio.Component.Windows11SDK.22621` (Windows 11 SDK, or a Windows 10 equivalent)
   - `Microsoft.Component.MSBuild`

`vswhere.exe` (shipped with Visual Studio installer infrastructure in `C:\Program Files (x86)\Microsoft Visual Studio\Installer\`) is how the .NET AOT publish discovers the installed toolchain; its absence is the same root cause class as `link.exe` absence.

### Canonical verification gate

The project's authoritative AOT publish verification runs on Linux CI, which has the equivalent native toolchain (`clang` + `glibc` headers) available by default on the runner image. Windows AOT publish is a best-effort local capability; Linux CI remains the canonical gate because:

- The Linux runner image ships the native toolchain without extra setup.
- AOT correctness (trimmer warnings, runtime reflection fallbacks) is runtime-identical across Windows and Linux AOT targets — verifying on Linux covers both.
- Consumer deployments most commonly target Linux container images.

If a Windows-specific AOT regression is suspected, re-run the affected sample locally with the Build Tools installed per above, or raise a Beads issue tagged `aot-windows-regression` so the next COMPASS review can prioritise a Windows CI runner addition.

### Resolution status

`bd-b2tgq9` is closed as **environmental / documented**. The absence of a Windows AOT CI runner is accepted; Linux CI provides the canonical publish gate. No source-code fix is required. If future demand warrants a Windows AOT CI shard, that work is scoped by a new Bead, not by reopening this one.

## References

- [ADR-050: Namespace Consolidation Strategy](../../management/architecture/ADR-050-Namespace-Consolidation-Strategy.md)
- [S807 Sprint Plan](../../management/sprints/sprint-807-plan.md)
- COMPASS msg 2143 (S807 GUIDE)
- CLAUDE.md §AOT/Trimmer Safety
