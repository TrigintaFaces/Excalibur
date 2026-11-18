# Excalibur.Dispatch.Aot.Sample

This sample demonstrates **Native AOT compilation** with Dispatch source generators for handler discovery, invocation and JSON serialization, without runtime code generation.

## Prerequisites

- .NET 10 SDK or later
- Platform-specific AOT toolchain:
  - **Windows:** Visual Studio with "Desktop development with C++" workload
  - **Linux:** `clang`, `zlib1g-dev` (Ubuntu/Debian: `sudo apt install clang zlib1g-dev`)
  - **macOS:** Xcode Command Line Tools (`xcode-select --install`)

## Quick Start

```bash
# Build and run (JIT mode - for development)
dotnet run

# Publish as native AOT executable
dotnet publish -c Release -p:NuGetLockFilePath=obj/aot-validation/packages.lock.json

# Run the native executable (path varies by OS)
# Windows:
./bin/Release/net10.0/win-x64/publish/Excalibur.Dispatch.Aot.Sample.exe
# Linux:
./bin/Release/net10.0/linux-x64/publish/Excalibur.Dispatch.Aot.Sample
# macOS (Apple Silicon):
./bin/Release/net10.0/osx-arm64/publish/Excalibur.Dispatch.Aot.Sample
```

> **Note:** You do NOT need to pass `-p:PublishAot=true` on the command line. `PublishAot` is already set in the `.csproj`. This is intentional -- passing it on the command line causes NETSDK1207 errors when source generator projects (targeting `netstandard2.0`) are in the dependency graph.

The separate project-local lock path keeps native RID dependencies out of the repository's normal
`packages.lock.json` files, so publishing this sample does not break a subsequent RID-neutral locked
restore. The repository's `eng/ci/Invoke-AotPublishValidation.ps1` selects this context automatically;
an explicitly configured `NuGetLockFilePath` is honored. Native lock files under `obj` are generated
verification artifacts, not a replacement for release dependency provenance.

## Native outbox authority control

Run the published native executable with `--outbox-authority-smoke`:

```powershell
./bin/Release/net10.0/win-x64/publish/Excalibur.Dispatch.Aot.Sample.exe --outbox-authority-smoke
```

Use the matching executable path for Linux or macOS. This mode refuses managed execution and
reflection-enabled JSON. It uses public `AddInMemoryOutboxStore()` registration, the framework payload
serializer and System.Text.Json codec with `AppJsonSerializerContext` metadata, and explicitly serialized
`StageMessageAsync` rows. There is no hand-written store, altered lease/retention policy, external broker,
or reflection-based fallback. The recording adapter only observes actual decoded payloads and simulates
an externally accepted send or an explicit send failure.

A real higher-token store claim advances its in-process high-water while the original batch remains
leased. The old completion then returns a real FenceRefused. Recreated scoped publishers and custom gate
wrappers share one opaque `ILeaderProcessingAuthorityDomain.AuthorityDomain`: the refused token remains
blocked across scopes, while a genuinely newer token publishes a newly staged row. Separate domains with
the same numeric token remain independent. Additional completion/failure handovers change the live gate
token before the original report, proving that old work still presents its captured token to the actual
store. Counters retain accepted sends and distinguish operation refusal from a delivery failure.

Staged producer objects are detached snapshots, not live views of storage. This control combines public
`IOutboxStoreAdmin.GetAllTenantsStatisticsAsync` population counts with exact accepted message identities
and fence assertions. It does not claim an individual terminal-row inspection API.

This is an in-process native consumer/control, not proof of a durable database fence, process-crash
recovery, external broker delivery or exactly-once messaging. Old leases are not shortened or reset; fresh
work demonstrates resumed progress. A 30-second cancellation budget bounds the scenario's operations.
The added project reference requires root's ordinary and native locked-restore evidence to be refreshed.
The control is only verified once official publish validation and the resulting native executable pass;
source inspection or a managed build is not a native pass.

## Expected Output

Run the published native executable with `--mqtt-cloud-events-smoke` to verify both CloudEvents MQTT
bindings through the production named sender and a real local MQTT 5 broker. Set `DISPATCH_MQTT_HOST`
to `127.0.0.1` and `DISPATCH_MQTT_PORT` to that broker's port. The control subscribes an independent
MQTT client on a unique topic, publishes a source-generated inventory payload in binary and structured
mode, and checks the received attributes, payload, topic, QoS, retain flag and correlation data. It has a
45-second cancellation bound and refuses managed execution or reflection-enabled JSON. The MQTT payload
declares its wire names explicitly with `JsonPropertyName`, so they are independent of naming policies.
The plaintext
connection is restricted to loopback; this is a local verification mode, not production TLS guidance.
Use a disposable broker because the control publishes retained messages on unique smoke topics.

Run the published executable with `--crypto-shred-smoke` to exercise real AES-GCM field encryption
for two tenants sharing a subject ID. The control requires native execution, checks explicit ownership
for both ordinary tenant names and the distinct default/untenanted partitions,
without an ambient tenant, rejects cross-tenant reads before and after destruction, and confirms that
destroying one tenant's key leaves the other tenant's plaintext readable. It uses the public external
destruction-recording API after observing completed key deletion. The in-memory key store and ledger
are demonstration fixtures, not durable production storage. This control does not verify the
ErasureService workflow, event-store replay or cloud providers.

When you run the sample (JIT or native), the first six demos produce output similar to this excerpt:

```
================================================
  Excalibur.Dispatch.Aot.Sample - Native AOT Demo
================================================

--- Demo 1: Create Order Command ---
Serializing command (source-generated):
  {"customerId":"CUST-001","items":[...]}
Order created: <guid>

--- Demo 2: Event with Multiple Handlers ---
(OrderCreatedEvent was dispatched by CreateOrderHandler)
Both OrderCreatedHandler and OrderAnalyticsHandler processed it.

--- Demo 3: Query Order ---
Order retrieved (source-generated serialization):
  {"id":"<guid>","customerId":"CUST-001","status":"Created",...}

--- Demo 4: Query Non-Existent Order ---
Order not found (as expected): Order '<guid>' was not found.

--- Demo 5: Serialization Round-Trip ---
Serialized:   {"customerId":"CUST-RT","items":[...]}
Deserialized: CustomerId=CUST-RT, Items=1
Round-trip match: True

--- Demo 6: InMemory Transport Registration ---
Transport registered: Name=demo, Type=InMemory
Transport running: True

Native CloudEvents: reflection-free payload serialization, structured-mode envelope round-trip, and a loud refusal for an uncovered payload type verified.

================================================
  AOT Verification Summary
================================================
```

All 12 default demos should complete without errors in both JIT and native AOT modes. The later demos cover event-sourced aggregates, event-store round trips, reconstruction, transport publish/subscribe, SOC 2 configuration binding and JSON report export.

Each required demo now checks its observable result before printing `AOT_DEMO_PASS:01` through
`AOT_DEMO_PASS:12`, in order. A failed check throws and reports `AOT_DEMO_FAIL`; missing, duplicate,
skipped or failed outcomes fail the official smoke step even if a process returns zero. The publish
analysis and runtime jobs cover the same main sample workload, not two independent package-coverage
proofs. These checks do not certify optional providers that the sample does not exercise.

The SOC 2 configuration demo checks the resolved system name, description, services, infrastructure and data types against the configured values before exporting a report. A mismatch throws and makes the process exit unsuccessfully, so a native smoke run cannot silently accept lost configuration values. This checks the sample's representative options; it does not certify every provider or optional configuration shape. The SQL-backed Quartz smoke below requires a separate invocation.

## CloudEvents under Native AOT

`CloudEventsSmoke.cs` encodes and decodes a real CloudEvent inside the published native executable,
using only the reflection-free surface: `CloudEventPayloadSerialization.CreateOptions` over this
application's source-generated `JsonSerializerContext`, then
`CloudEventSerializer.CreateWithMetadata(options)` and its metadata-aware formatter.
It asserts three things and fails the process on any of them — that the payload path actually emits the
payload, that the envelope survives a structured-mode round trip, and that a payload type absent from
the serialization context is **refused by name** rather than silently rendered.

**Why the sample carries this at all.** CloudEvents interoperability and Native AOT are both goals of
this framework, and a publish cannot report a warning about code it never analysed. Before this file
existed, nothing in the AOT samples referenced CloudEvents, so the trimmer removed the CloudEvents
assembly as unused and the publish reported zero warnings about a path it had not looked at. A zero
from an unreachable path is not a result.

**Formatter choice matters.** The current smoke uses `CreateWithMetadata`, which obtains payload
metadata explicitly rather than calling the SDK formatter's options-only serializer overloads.
Normal DI consumers select this path with `AddCloudEvents(AppJsonSerializerContext.Default)`.
The legacy `new CloudEventSerializer(options)` constructor retains the SDK formatter;
passing options containing a resolver to that constructor does not select the metadata-aware formatter.
Neither path certifies arbitrary custom converters or transport SDKs for Native AOT.

**Historical warning evidence remains historical.** Earlier smoke revisions used the SDK formatter
and recorded four member-specific `IL2026`/`IL3050` diagnostics against
`CloudNative.CloudEvents.SystemTextJson.JsonEventFormatter.EncodeStructuredModeData`.
That count describes those exact publications, not a standing expectation for this revised smoke.
Keep the original logs and baseline history; establish the current result by publishing and executing
this sample with reflection-based JSON disabled. The payload round trip and uncovered-type refusal
remain required controls, and the generated context must cover every payload type.

## AOT Configuration

### Handler context binding

`AddDispatch` selects the container-based `AotHandlerActivator` when dynamic code is unavailable.
Handlers that need the current context can implement `IMessageContextAware`; a generated or
explicitly registered context setter is the next choice. Plain writable `IMessageContext`
properties are also supported through cached, preserved property metadata. Native AOT permits
this bounded reflection; it does not require dropping context binding.

The runnable [native activation probe](../../../tests/harnesses/HandlerActivationAotProbe/README.md)
demonstrates interface, registered-setter and plain-property binding and verifies that context
references survive native compilation. Managed benchmark classes with `Aot` in their names do
not establish NativeAOT compatibility; publish and execute the native artifact separately.

The project is configured for full AOT compatibility in the `.csproj`:

```xml
<PropertyGroup>
    <!-- Enable AOT compilation -->
    <PublishAot>true</PublishAot>

    <!-- Enable full trimming (removes unused code) -->
    <TrimMode>full</TrimMode>

    <!-- Disable reflection-based JSON serialization -->
    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>

    <!-- Show trimming warnings during build -->
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
    <SuppressTrimAnalysisWarnings>false</SuppressTrimAnalysisWarnings>
</PropertyGroup>
```

## Source Generators in Action

Dispatch uses several source generators to enable AOT compatibility:

### 1. HandlerRegistrySourceGenerator

Discovers message handlers at compile time and emits a registration extension, so nothing scans
assemblies at run time:

```csharp
// Generated: AddDiscoveredHandlers() registers every handler found at compile time.
services.AddDispatch(dispatch => dispatch.AddDiscoveredHandlers());
```

**Generated output:** `obj/GeneratedFiles/.../PrecompiledHandlerRegistry.g.cs`,
`PrecompiledHandlerMetadata.g.cs`, `GeneratedHandlerRegistrationExtensions.g.cs`,
`GeneratedHandlerActivatorRegistrations.g.cs`, `PrecompiledDirectActionDispatch.g.cs`

`PrecompiledDirectActionDispatch.g.cs` is what removes reflection from handler *creation*: for each
discovered action type it resolves the handler with a closed-generic `GetRequiredService<T>()` call,
so those messages never go through a handler activator.

### 2. Handler activation for anything direct dispatch does not cover

Messages outside the generated direct-dispatch table fall back to `IHandlerActivator`. The activator
Dispatch registers by default compiles expressions, which Native AOT does not allow, so it throws
rather than guessing. Register the AOT activator to close that fallback:

```csharp
using Excalibur.Dispatch.Delivery.Handlers;

// Before AddDispatch: Dispatch registers its default activator only if none is present.
services.AddSingleton<IHandlerActivator, AotHandlerActivator>();
```

`AotHandlerActivator` resolves the handler from the container and applies the message context
through `IMessageContextAware`. It reflects over no handler member, so a handler reached this way
must implement `IMessageContextAware` to receive the context — property-injected context is not
available under Native AOT.

### 3. HandlerInvokerSourceGenerator

Emits typed invokers so a dispatched message reaches its handler without a reflective call:

**Generated output:** `obj/GeneratedFiles/.../HandlerInvokerRegistry.g.cs`

### 4. StaticPipelineGenerator

For supported call sites, forwards to the selected dispatcher overload while preserving its task, exceptions and cancellation. Middleware composition and execution remain runtime responsibilities:

**Generated output:** `obj/GeneratedFiles/.../StaticPipelines.g.cs`

### 5. MiddlewareInvokerInterceptorGenerator (C# 12 interceptors)

Intercepts middleware invocation for compile-time resolution:

**Generated output:** `obj/GeneratedFiles/.../MiddlewareInvokers.g.cs`

### 6. MessageResultExtractorGenerator

Registers a factory for every discovered result type, so dispatch results are constructed without
`MakeGenericType`:

**Generated output:** `obj/GeneratedFiles/.../ResultFactoryRegistry.g.cs`

## Source-Generated JSON Serialization

For AOT compatibility, use `System.Text.Json` source generation:

```csharp
[JsonSerializable(typeof(CreateOrderCommand))]
[JsonSerializable(typeof(OrderCreatedEvent))]
[JsonSerializable(typeof(OrderDto))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class AppJsonSerializerContext : JsonSerializerContext;

// Usage:
var json = JsonSerializer.Serialize(order, AppJsonSerializerContext.Default.OrderDto);
```

## Viewing Generated Files

Enable generated file output in your `.csproj`:

```xml
<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
<CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)GeneratedFiles</CompilerGeneratedFilesOutputPath>
```

Then check `obj/GeneratedFiles/Excalibur.Dispatch.SourceGenerators/` after building.

## Project Structure

```
Excalibur.Dispatch.Aot.Sample/
├── Excalibur.Dispatch.Aot.Sample.csproj    # AOT-enabled project file
├── Program.cs                     # Entry point with demos
├── Messages/
│   ├── CreateOrderCommand.cs      # Command with response
│   ├── GetOrderQuery.cs           # Query with explicit not-found exception
│   └── OrderCreatedEvent.cs       # Domain event
├── Handlers/
│   ├── CreateOrderHandler.cs      # Command handler
│   ├── GetOrderHandler.cs         # Query handler
│   └── OrderCreatedHandler.cs       # Event handlers (2)
├── Serialization/
│   └── AppJsonSerializerContext.cs # Source-generated JSON
├── CloudEventsSmoke.cs            # CloudEvents encode/decode, reflection-free
└── README.md                      # This file
```

## Verification

Publish for the deployment RID (for example `dotnet publish -c Release -r linux-x64`), then execute
the resulting native binary. Do not substitute `dotnet run` for Native AOT execution.

`PipelineVerification.cs` asserts that two contracts on one explicitly implemented handler dispatch
correctly, that configured middleware actually executes, that the exact AOT invoker pairs are registered,
and that deferred middleware factories use distinct asynchronously disposed dispatch scopes. It also
checks background acceptance, owned-scope disposal, and refusal of typed background responses in the
native executable. A failed assertion
terminates execution. The native run must print `Pipeline verification PASS; dynamic code supported: False`.

The remaining demos illustrate serialization, transport, event sourcing and compliance scenarios;
printed output alone is not a correctness assertion for every demonstrated capability. Review publish
warnings individually with the exercised path and exact artifact in mind. This sample does not suppress
trim analysis warnings or enable reflection-based JSON to hide missing generated metadata. There is no
blanket exemption for IL2xxx or IL3xxx warnings.

## Common Issues

### Missing Native Toolchain

**Windows** -- C++ build tools required:
```
error NETSDK1182: Publishing to native code is only supported on Windows when using Microsoft Visual Studio.
```
**Solution**: Install "Desktop development with C++" from Visual Studio Installer, then publish from a Developer Command Prompt.

**Linux** -- clang required:
```
error : Unable to find a compatible C compiler...
```
**Solution**: `sudo apt install clang zlib1g-dev` (Ubuntu/Debian) or `sudo dnf install clang zlib-devel` (Fedora).

**macOS** -- Xcode tools required:
```
error : Unable to find a compatible C compiler...
```
**Solution**: `xcode-select --install`

### NETSDK1207 (netstandard2.0 Conflict)

If you see:
```
error NETSDK1207: It's not possible to publish an application to a single-file and Native AOT simultaneously when targeting netstandard2.0
```
**Cause**: Passing `-p:PublishAot=true` on the command line cascades to source generator analyzer projects. **Solution**: Do NOT pass `-p:PublishAot=true` on the command line. It is already set in the `.csproj`.

### AOT Warnings from Dispatch Library

You may see AOT analysis warnings like:
```
IL3050: Using member which has 'RequiresDynamicCodeAttribute'...
```

Inspect the diagnostic's actual call site and dependency chain. A warning can originate in this sample, the framework or a dependency; generated alternatives do not establish that every dynamic path is unreachable. Successful compilation does not prove the published application can execute that path.

Prefer a supported generated or statically registered path, preserve the metadata it requires, and exercise the published native executable. Keep unresolved warnings and failed publication/execution results with the evidence. This sample deliberately enables trim analysis and removes inherited IL2026/IL3050 suppression; do not weaken those checks to obtain a pass. See [Microsoft's AOT warning guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/fixing-warnings).

### Trimming Warnings

Warnings such as `IL2026` and `IL2055` identify trimming requirements or analysis risks that need investigation for the selected path. They are not interchangeable diagnoses.

**Investigation**: Check supported source-generated alternatives and required metadata. Any narrowly scoped suppression needs a recorded, path-specific justification and native execution coverage; re-review it when code or dependencies change. A suppression does not supply missing native code or metadata.

### Missing JSON Types

If JSON serialization fails:
```
System.NotSupportedException: TypeInfo for type 'MyType' was not generated
```

**Solution**: Add `[JsonSerializable(typeof(MyType))]` to your `JsonSerializerContext`.

## CI Validation

This sample is the validation target for the AOT CI pipeline. The scripts:

- **`eng/ci/Invoke-AotPublishValidation.ps1`** -- Publishes this sample with AOT, parses IL warnings, groups by package
- **`eng/ci/Invoke-AotBuildAnalysis.ps1`** -- Static analysis across all `src/` packages for AOT readiness

Run locally:
```powershell
# Publish validation (same as CI)
pwsh eng/ci/Invoke-AotPublishValidation.ps1 -Configuration Release

# Static analysis
pwsh eng/ci/Invoke-AotBuildAnalysis.ps1
```

## Related Documentation

- [Source Generators Guide](../../../docs-site/docs/source-generators/index.md)
- [Viewing Generated Code](../../../docs-site/docs/advanced/viewing-generated-code.md)
- [Microsoft AOT Documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)

## DataProcessing acceptance

The executable also runs `DataProcessingSmoke`. It exercises the explicit configuration and fluent builder
registration paths, asserts all seven immutable option values, traverses an empty nonterminal page, and
checks handler execution, checkpoints, and async scope disposal. A mismatch throws and fails the executable;
a successful publish without running the binary is insufficient to validate this path. No SQL Server is
required for this in-memory source test. SQL restore recovery is covered separately by the integration suite.

## SQL-backed Quartz Native AOT smoke

Publish this sample as Native AOT and run the executable with `--quartz-sql-smoke`.
Set `PROCESSING_SQL` to a disposable SQL Server database. The smoke creates
`dbo.NativeTasks` and fresh Quartz tables, then executes the real `DataProcessingJob`
and both Excalibur job adapters. The context job uses source-generated JSON metadata;
SQL Server uses an explicit `SqlClientFactory.Instance`. No production database should
be used: this is a provisioning and execution example, not a schema-upgrade utility.

A successful run reports `NATIVE QUARTZ SQL` only after the jobs complete. This path
exercises SQL authentication; it does not establish support for every authentication
provider or all CDC/outbox serialization paths.


### Native Jobs configuration control

Run the published native executable with `--jobs-binding-smoke`. This command refuses managed execution and requires reflection-based JSON to remain disabled. It exercises the public CDC and data-processing scheduling and health registration paths, nested CDC options, defaults, validation and reload isolation. Each unique RAM scheduler remains in Created status and is shut down without starting; no job, database connection or CDC processing is invoked. This verifies configuration paths rather than claiming every Jobs/Quartz deployment path is NativeAOT-compatible.

### Native SQL execution control

Run the published executable with `--native-sql-paths-smoke` against a fresh, locally owned disposable
SQL Server container with SQL Server Agent enabled. Set `ARB_NATIVE_SQL` to its connection string and
`ARB_NATIVE_DISPOSABLE=1`. The database must already exist and be named `ARBNative_<Guid:N>`;
only localhost endpoints are accepted. The test account needs permission to enable CDC. Connection
strings are not printed.

The control creates a unique schema and enables CDC in that disposable database. It verifies actual
capture, row/schema mapping and resume; checkpoint reads, fencing and generic adapters; and public
task enqueue/discovery, a planned failure, durable Unicode cursor/count progress, scoped processor
disposal, recovery and successful deletion. Missing prerequisites fail rather than skip. A two-minute
token bounds the control. Remove the owned container afterward; the sample does not drop databases.

The public manager uses ownership-safe claim writes and does not invoke the legacy internal
`DeleteDataTask`, `UpdateDataTaskAttempts` or `UpdateDataTaskCompletedCount` request classes.
This native control does not claim execution of those three classes; their direct lifecycle tests
are separate. Native publishing and this smoke do not certify every authentication provider or
all framework packages. Existing warning baselines remain visible in the publish report.

## Native saga correlation smoke

After an official native publish, run the exact native executable with `--saga-correlation-smoke`. This bounded, infrastructure-free control requires actual NativeAOT and reflection JSON disabled, checks framed UUIDv8 golden values, literal delimiter separation and stable typed generic/array scopes. Running the managed DLL is refused; this arm does not certify all saga persistence or compensation paths.
