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
Both OrderEventHandler and OrderAnalyticsHandler processed it.

--- Demo 3: Query Order ---
Order retrieved (source-generated serialization):
  {"id":"<guid>","customerId":"CUST-001","status":"Created",...}

--- Demo 4: Query Non-Existent Order ---
Order not found (as expected): Order <guid> not found

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

Discovers all message handlers at compile time:

```csharp
// Traditional reflection-based (NOT AOT compatible):
// services.AddHandlersFromAssembly(assembly); // Scans at runtime

// Source-generator-based (AOT compatible):
// The same API works because handlers are pre-discovered at compile time!
dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
```

**Generated output:** `obj/GeneratedFiles/.../PrecompiledHandlerRegistry.g.cs`

### 2. HandlerActivationGenerator

Creates handler instances without reflection:

```csharp
// Generated code creates handlers with DI:
internal static IActionHandler<CreateOrderCommand, Guid> CreateHandler(IServiceProvider sp)
    => new CreateOrderHandler(sp.GetRequiredService<IDispatcher>());
```

**Generated output:** `obj/GeneratedFiles/.../PrecompiledHandlerActivator.g.cs`

### 3. HandlerInvocationGenerator

Direct handler invocation without dictionary lookups:

```csharp
// Generated code invokes handlers directly:
if (message is CreateOrderCommand cmd)
    return await handler.HandleAsync(cmd, ct);
```

**Generated output:** `obj/GeneratedFiles/.../PrecompiledHandlerInvoker.g.cs`

### 4. StaticPipelineGenerator

Compiles middleware pipelines at build time:

```csharp
// Generated static pipeline for deterministic message types:
// Avoids runtime pipeline construction
```

**Generated output:** `obj/GeneratedFiles/.../StaticPipeline.g.cs`

### 5. DispatchInterceptorGenerator (C# 12)

Intercepts dispatch calls for compile-time resolution:

```csharp
// Generated interceptor redirects dispatch calls:
[InterceptsLocation(1, "...")]
internal static async Task<IMessageResult> Intercept_CreateOrderCommand(...)
```

**Generated output:** `obj/GeneratedFiles/.../DispatchInterceptors.g.cs`

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
│   └── OrderEventHandler.cs       # Event handlers (2)
├── Serialization/
│   └── AppJsonSerializerContext.cs # Source-generated JSON
└── README.md                      # This file
```

## Verification

After `dotnet publish -c Release`, verify:

1. **Publish succeeds**: The publish should complete without errors (warnings are expected -- see below)
2. **Native executable exists**: Check `bin/Release/net10.0/<rid>/publish/`
3. **Runs correctly**: Execute the native binary and verify all 6 demos produce expected output
4. **File size**: The native executable is typically 15-30 MB (varies by platform and framework version)

### About AOT Warnings

You will see IL2xxx (trim) and IL3xxx (AOT) warnings during publish. As of Sprint 736, the baseline is **~126 warnings** from the Dispatch framework itself. These originate from:

- Reflection-based fallback paths in the core dispatcher (used only when source generators aren't available)
- `Type.GetType()` calls in event serialization (being addressed in Wave 3)
- `JsonStringEnumConverter` without generic type parameter (being addressed in Wave 2)

These warnings do **not** prevent successful AOT compilation or runtime execution. The sample uses source-generator paths that bypass all reflection-based code.

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

- [ADR-292: AOT Wave 0-1 Decisions](../../../management/architecture/adr-292-aot-wave-0-1-decisions.md)
- [ADR-293: AOT Wave 2-3 Decisions](../../../management/architecture/adr-293-aot-wave-2-3-decisions.md)
- [Source Generators Guide](../../../docs-site/docs/source-generators/index.md)
- [Viewing Generated Code](../../../docs-site/docs/advanced/viewing-generated-code.md)
- [Microsoft AOT Documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
