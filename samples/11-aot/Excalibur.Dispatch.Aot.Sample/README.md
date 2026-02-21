# Excalibur.Dispatch.Aot.Sample

This sample demonstrates **Native AOT compilation** with Dispatch source generators. It shows how to build a fully AOT-compatible message dispatching application with zero runtime reflection.

## Prerequisites

- .NET 10 SDK or later
- C++ Build Tools (for AOT compilation on Windows)

## Quick Start

```bash
# Build and run (JIT mode - for development)
dotnet run

# Publish as native AOT executable
dotnet publish -c Release

# Run the native executable
./bin/Release/net10.0/win-x64/publish/Excalibur.Dispatch.Aot.Sample.exe
```

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

1. **No warnings**: Build should complete without trimming warnings
2. **Native executable**: Check `bin/Release/net10.0/<rid>/publish/`
3. **Runs correctly**: Execute the native binary and verify output

## Common Issues

### Missing C++ Build Tools

On Windows, AOT compilation requires Visual Studio C++ build tools and proper PATH configuration:
```
error NETSDK1182: Publishing to native code is only supported on Windows when using Microsoft Visual Studio.
```
Or:
```
'vswhere.exe' is not recognized...
```

**Solution**:
1. Install "Desktop development with C++" workload from Visual Studio Installer
2. Run `dotnet publish` from a Visual Studio Developer Command Prompt
3. Or ensure `vswhere.exe` is in your PATH (usually in `C:\Program Files (x86)\Microsoft Visual Studio\Installer\`)

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

## Related Documentation

- [Source Generators Guide](../../../docs-site/docs/source-generators/index.md)
- [Viewing Generated Code](../../../docs-site/docs/advanced/viewing-generated-code.md)
- [Microsoft AOT Documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)


