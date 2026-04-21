---
sidebar_position: 50
title: SDK Seam Pattern
description: How the framework insulates itself from third-party SDK minor-bump churn using internal test seams.
---

# SDK Seam Pattern

When a framework package calls into a third-party SDK (Azure, AWS, GCP, etc.) and the SDK's concrete client types are needed to test the framework's behavior, the framework does **not** fake the SDK client type directly. Instead, the package defines a small internal interface — a **seam** — and an internal sealed adapter that forwards to the SDK. Tests fake the seam; production wires the adapter.

This page is reference material for contributors extending the framework. Consumers of Excalibur do not see or use these seams.

:::info Where this is specified
The normative rule lives in [ADR-142 §D7](https://github.com/dean-excalibur/Excalibur.Dispatch/blob/main/management/architecture/adr-142-sprint-555-testing-infrastructure-consumer-dx.md). This page is an illustrated companion to that decision, not a substitute.
:::

## Why

Third-party SDK concrete clients (`SecretClient`, `ServiceBusClient`, `BlobClient`, etc.) expose non-virtual methods whose overload resolution can change silently across minor version bumps. FakeItEasy + Castle DynamicProxy bind to a specific signature at fake-creation time; when the SDK picks a different overload after a version bump, the fake is bypassed and the test either calls the real network or throws from an unconfigured invocation.

The S797 `AzureKeyVaultCredentialStore` / `Azure.Security.KeyVault.Secrets 4.8 → 4.10` regression (`bd-wy56o5`) is the canonical case. Pinning the SDK back is a suppress, not a fix. The seam is the fix.

## Shape

Every seam has the same shape:

1. **Interface** — `internal`, ≤ 5 methods, exposes exactly the call sites the framework code touches. Lives under `src/.../{Package}/Internal/`.
2. **Adapter** — `internal sealed`, forwards each interface method to the SDK. Constructor takes the SDK concrete type.
3. **Production ctor** — unchanged; constructs the default adapter internally from the SDK client.
4. **Test ctor** — `internal`; accepts the interface. Visible to the test project via `InternalsVisibleTo`.
5. **Field type** — the framework class holds the interface, not the SDK concrete.

Both pieces stay `internal`. Nothing is added to `PublicAPI.Shipped.txt` or `PublicAPI.Unshipped.txt`.

## Worked example — `ISecretClient`

```csharp
// src/Dispatch/Excalibur.Security.Azure/Internal/ISecretClient.cs
namespace Excalibur.Security.Azure.Internal;

internal interface ISecretClient
{
    Task<Response<KeyVaultSecret>> GetSecretAsync(
        string name,
        CancellationToken cancellationToken);
}
```

```csharp
// src/Dispatch/Excalibur.Security.Azure/Internal/SecretClientAdapter.cs
namespace Excalibur.Security.Azure.Internal;

internal sealed class SecretClientAdapter : ISecretClient
{
    private readonly SecretClient _client;

    public SecretClientAdapter(SecretClient client) => _client = client;

    public Task<Response<KeyVaultSecret>> GetSecretAsync(
        string name,
        CancellationToken cancellationToken)
        => _client.GetSecretAsync(name, cancellationToken: cancellationToken);
}
```

```csharp
// Production use — public ctor unchanged, seam hidden behind the default wiring
public AzureKeyVaultCredentialStore(
    IConfiguration configuration,
    ILogger<AzureKeyVaultCredentialStore> logger)
    : this(configuration, logger, secretClient: null) { }

// Internal test-visible ctor — no DI registration, no public surface
internal AzureKeyVaultCredentialStore(
    IConfiguration configuration,
    ILogger<AzureKeyVaultCredentialStore> logger,
    ISecretClient? secretClient)
{
    _secretClient = secretClient
        ?? new SecretClientAdapter(new SecretClient(new Uri(vaultUri), new DefaultAzureCredential()));
    // ...
}
```

```csharp
// Test — fakes OUR interface, not the SDK concrete
var secretClient = A.Fake<ISecretClient>();
A.CallTo(() => secretClient.GetSecretAsync(A<string>._, A<CancellationToken>._))
    .Returns(Task.FromResult(Response.FromValue(secret, A.Fake<Response>())));

var store = new AzureKeyVaultCredentialStore(config, logger, secretClient);
```

## Castle DynamicProxy IVT

The seam interface is `internal`, so Castle DynamicProxy (FakeItEasy) must be granted internals access. Every package that owns a seam adds:

```xml
<ItemGroup>
    <InternalsVisibleTo Include="Excalibur.Dispatch.{Package}.Tests" />
    <InternalsVisibleTo Include="DynamicProxyGenAssembly2" />
</ItemGroup>
```

Without the `DynamicProxyGenAssembly2` line, `A.Fake<IInternalSeam>()` throws `FakeCreationException` at the first call site.

## Naming

The canonical form is **`IXxx` + `XxxAdapter`** — interface is a contract, class adapts. The `I` prefix always disambiguates the seam from the bare-named SDK concrete at the CLR level; no carve-outs.

**Two naming shapes are in use**, both ADR-142 §D7 conformant:

1. **`IXxxClient` + `XxxClientAdapter`** — the default. Use when the seam sits directly in front of a single SDK client type and the framework's call sites read naturally as "client" operations.
2. **`I{DomainRole}` + `{DomainRole}Adapter`** — use when the seam describes the consumer's domain role rather than mirroring an SDK topology. Valid suffixes include `Store`, `Operations`, `Repository`. Introduced in S799 per COMPASS msg 1799.

The **naming test** (load-bearing): a reviewer who has never seen the third-party SDK should understand the consumer's domain purpose from the seam name + method list without inferring SDK sub-client topology. `ISchemaEvolutionOperations { MigrateAsync, VerifyVersionAsync, RollbackAsync }` passes. `IElasticsearchIndicesClient { CreateIndex, DeleteIndex, … }` fails — it reads as an SDK mirror.

| SDK concrete / domain | Seam interface | Adapter | Shape |
|---|---|---|---|
| `Azure.Security.KeyVault.Secrets.SecretClient` | `ISecretClient` | `SecretClientAdapter` | Client |
| `Azure.Messaging.ServiceBus.ServiceBusClient` | `IServiceBusClient` | `ServiceBusClientAdapter` | Client |
| Elasticsearch security audit writes (consumer `SecurityAuditor`) | `ISecurityAuditStore` | `SecurityAuditStoreAdapter` | Domain role (Store) |
| Elasticsearch index templates (consumer `IndexTemplateManager`) | `IIndexTemplateStore` | `IndexTemplateStoreAdapter` | Domain role (Store) |
| Elasticsearch component templates (consumer `IndexTemplateManager`) | `IComponentTemplateStore` | `ComponentTemplateStoreAdapter` | Domain role (Store) |
| Elasticsearch index lifecycle (consumer `IndexLifecycleManager`) | `IIndexLifecycleOperations` | `IndexLifecycleOperationsAdapter` | Domain role (Operations) |
| Elasticsearch projection event ingest (consumer `EventualConsistencyTracker`) | `IProjectionEventIngest` | `ProjectionEventIngestAdapter` | Domain role (verb) |
| Elasticsearch projection event lookup | `IProjectionEventLookup` | `ProjectionEventLookupAdapter` | Domain role (verb) |
| Elasticsearch projection event scan | `IProjectionEventScan` | `ProjectionEventScanAdapter` | Domain role (verb) |
| Elasticsearch projection index provisioning | `IProjectionIndexProvisioning` | `ProjectionIndexProvisioningAdapter` | Domain role (gerund) |

Do **not** name seams `IXxxAdapter`. The `Adapter` suffix is reserved for the concrete class. ADR-142 §D7 is a universal rule — if naming looks ambiguous in an editor, rely on the `I` prefix + namespace to disambiguate, not a naming carve-out. If the client shape reads as an SDK mirror, switch to the domain-role shape instead.

## Governance

The `NoConcreteSdkFakesGovernanceShould` conformance test scans for banned fake patterns (direct fakes of SDK concrete client types, reflection-based private-field injection into SDK-backed fields) and fails the build when a new offender lands. The debt baseline tracks pre-existing sites being drained progressively (seeded at 13 in S798; drained to 11 in S799); the `Debt_Baseline_Count_Only_Shrinks` ratchet prevents regressions.

### Per-adapter conformance smoke

Each seam should ship with a real-SDK passthrough smoke test — `{SeamName}AdapterConformanceShould.cs` under `tests/integration/Excalibur.Integration.Tests/DataElasticSearch/Conformance/` (or the transport-equivalent path). This is the canary that catches the SDK-minor-bump overload-resolution failure mode ADR-142 §D7 exists to prevent. Unit tests that fake the seam (`A.Fake<ISeam>()`) verify the consumer's logic; conformance smokes verify the adapter still forwards cleanly to the current SDK version. Shipping a seam without a conformance smoke is a pattern regression recorded as a non-blocking REVIEW_ARCH finding (S801 F1 `bd-dfrr2x` is the reference case).

New seams are added to the normal package; debt entries are retired individually as each seam lands.

## When a seam is the wrong answer

The seam pattern applies to third-party SDKs the framework calls and tests. It is not a replacement for:

- **`IOptions<T>`** — already the standard for configuration.
- **Consumer-pluggable abstractions** (`IEventStore`, `IOutboxPublisher`, etc.) — those are public contracts with deliberate DI stories. Seams are not.
- **Handcrafted test doubles for framework interfaces** — prefer `NullLogger<T>.Instance` over `A.Fake<ILogger<T>>` where the log output is not under test.

If a potential seam would expose more than five methods, re-scope it to the framework's actual call sites. Seams grow by one method per new call site only — never speculatively. **The ≤5 rule is a hard cap with no carve-outs** (reinforced S800 — an earlier domain-cluster exception was proposed in S799 for `IIndexTemplateStore` at 6 methods and was rejected in favour of splitting into `IIndexTemplateStore` (4) + `IComponentTemplateStore` (2)). If a seam cannot fit under the cap via an operation-axis, sub-client-axis, or domain-role split, the correct response is to pivot the workstream for that one seam (defer to a later sprint with a recorded reason) — not to exceed the cap.

## Further reading

- ADR-142 §D7 (repo path `management/architecture/adr-142-sprint-555-testing-infrastructure-consumer-dx.md`) — normative rule, rollout posture, anti-pattern list.
- [Testing Handlers](../testing/testing-handlers.md) — when to fake framework interfaces vs. use real implementations.
