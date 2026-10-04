# Excalibur.Dispatch.Aot.Sample

This sample demonstrates **Native AOT compilation** with Dispatch source generators. It shows how to build a fully AOT-compatible message dispatching application with zero runtime reflection.

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
dotnet publish -c Release

# Run the native executable (path varies by OS)
# Windows:
./bin/Release/net10.0/win-x64/publish/Excalibur.Dispatch.Aot.Sample.exe
# Linux:
./bin/Release/net10.0/linux-x64/publish/Excalibur.Dispatch.Aot.Sample
# macOS (Apple Silicon):
./bin/Release/net10.0/osx-arm64/publish/Excalibur.Dispatch.Aot.Sample
```

> **Note:** You do NOT need to pass `-p:PublishAot=true` on the command line. `PublishAot` is already set in the `.csproj`. This is intentional -- passing it on the command line causes NETSDK1207 errors when source generator projects (targeting `netstandard2.0`) are in the dependency graph.

## Expected Output

When you run the sample (JIT or native), you should see output similar to:

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
Order not found (as expected): System.InvalidOperationException: Order '<guid>' was not found.

--- Demo 5: Serialization Round-Trip ---
Serialized:   {"customerId":"CUST-RT","items":[...]}
Deserialized: CustomerId=CUST-RT, Items=1
Round-trip match: True

--- Demo 6: InMemory Transport Registration ---
Transport registered: Name=demo, Type=InMemory
Transport running: True

================================================
  AOT Verification Summary
================================================
```

All 6 demos should complete without errors in both JIT and native AOT modes.

## AOT Configuration

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

These warnings come from the Dispatch library itself, not from your AOT sample code. The core library contains some dynamic code paths that are used as fallbacks when source generators aren't available. In practice:
- Source generators handle the AOT-compatible paths
- The dynamic fallbacks are only used in non-AOT scenarios
- These warnings don't prevent successful AOT compilation

### Trimming Warnings

If you see warnings like `IL2026`, `IL2055`, etc., you have code that uses reflection incompatibly with trimming.

**Solution**: Use source generation patterns or suppress with `[UnconditionalSuppressMessage]` if safe.

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
