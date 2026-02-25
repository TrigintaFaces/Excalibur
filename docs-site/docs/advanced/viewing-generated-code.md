---
sidebar_position: 5
title: Viewing Generated Code
description: How to inspect source generator output in different IDEs
---

# Viewing Generated Code

Source generators produce code at compile time. This guide explains how to view and debug generated files in different development environments.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A project using Excalibur.Dispatch source generators:
  ```bash
  dotnet add package Excalibur.Dispatch.SourceGenerators.Analyzers
  ```
- Familiarity with [source generators](../source-generators/getting-started.md)

## MSBuild Configuration

Add these properties to your `.csproj` to persist generated files to disk:

```xml
<PropertyGroup>
    <!-- Emit generated files to disk -->
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>

    <!-- Location for generated files (defaults to obj/Generated) -->
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\GeneratedFiles</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

After building, generated files appear in:
```
obj/
└── Debug/
    └── net8.0/
        └── GeneratedFiles/
            └── Excalibur.Dispatch.SourceGenerators/
                ├── PrecompiledHandlerRegistry.g.cs
                ├── SourceGeneratedHandlerActivator.g.cs
                ├── SourceGeneratedHandlerInvoker.g.cs
                └── ...
```

## Visual Studio

### Method 1: Solution Explorer (Recommended)

1. Expand your project in Solution Explorer
2. Expand **Dependencies** → **Analyzers** → **Excalibur.Dispatch.SourceGenerators**
3. Generated files appear as children of the generator

*The generated files appear under the Analyzers node in Solution Explorer.*

### Method 2: Generated Files on Disk

1. Add MSBuild configuration above
2. Build the project
3. Navigate to `obj/Debug/net8.0/GeneratedFiles/`
4. Open files directly

### Method 3: Go to Definition

1. Right-click on a generated type (e.g., `PrecompiledHandlerRegistry`)
2. Select **Go to Definition** (F12)
3. Visual Studio opens the generated source

### Debugging Generated Code

1. Set `EmitCompilerGeneratedFiles` to `true`
2. Open the generated `.g.cs` file
3. Set breakpoints normally
4. Run with debugging (F5)

:::tip
Generated code supports full debugging including step-through, watch expressions, and conditional breakpoints.
:::

## JetBrains Rider

### Method 1: Generated Files Node

1. In Solution Explorer, expand your project
2. Find the **Generated** folder under the project
3. Expand **Excalibur.Dispatch.SourceGenerators**
4. Browse generated files

### Method 2: Navigate to Sources

1. Place cursor on a generated type
2. Press **Ctrl+Click** or **Ctrl+B**
3. Rider navigates to the generated source

### Method 3: Find Usages

1. Right-click on a generated type
2. Select **Find Usages** (Alt+F7)
3. See all references to generated code

### Rider Settings

For better source generator experience:

1. **File** → **Settings** → **Build, Execution, Deployment** → **Toolset and Build**
2. Enable **Use RoslynAnalyzers**
3. Enable **Include source generators in code analysis**

## VS Code with C# Dev Kit

### Method 1: Explorer View

1. Install [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) extension
2. Open project folder
3. After build, navigate to `obj/GeneratedFiles/` in Explorer

### Method 2: OmniSharp Configuration

Create or edit `.omnisharp.json` in your solution root:

```json
{
    "RoslynExtensionsOptions": {
        "EnableAnalyzersSupport": true,
        "EnableImportCompletion": true
    },
    "FormattingOptions": {
        "EnableEditorConfigSupport": true
    }
}
```

### Method 3: Go to Definition

1. Hover over a generated type
2. **Ctrl+Click** to navigate
3. VS Code shows generated source in read-only mode

### Recommended Extensions

- **C# Dev Kit** - Full C# language support
- **C#** - OmniSharp-based language support
- **Solution Explorer** - Visual Studio-like project tree

## File Naming Conventions

Dispatch generators use consistent naming:

| Generator | Output File | Description |
|-----------|-------------|-------------|
| HandlerRegistrySourceGenerator | `PrecompiledHandlerRegistry.g.cs` | Handler registrations |
| HandlerActivationGenerator | `SourceGeneratedHandlerActivator.g.cs` | Handler activation |
| HandlerInvocationGenerator | `SourceGeneratedHandlerInvoker.g.cs` | Handler invocation |
| MessageTypeSourceGenerator | `PrecompiledHandlerMetadata.g.cs` | Handler metadata |
| StaticPipelineGenerator | `StaticPipelines.g.cs` | Static pipelines |
| DispatchInterceptorGenerator | `DispatchInterceptors.g.cs` | C# 12 interceptors |
| MiddlewareDecompositionAnalyzer | `MiddlewareDecomposition.g.cs` | Middleware analysis |
| CachePolicySourceGenerator | `CacheInfoRegistry.g.cs` | Cache policies |
| ServiceRegistrationSourceGenerator | `GeneratedServiceCollectionExtensions.g.cs` | DI registrations |

## When Files Regenerate

Generated files are recreated when:

- Project is built (full or incremental)
- Source files change that affect generator input
- Generator package version changes
- Clean/rebuild is performed

:::note Incremental Generators
Dispatch uses incremental generators that only regenerate when relevant source changes. This ensures fast build times even with many generators.
:::

## Troubleshooting

### Files Not Appearing

1. **Check package reference:**
   ```xml
   <PackageReference Include="Excalibur.Dispatch.SourceGenerators" Version="..." OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
   ```

2. **Clean and rebuild:**
   ```bash
   dotnet clean
   dotnet build
   ```

3. **Check build output:**
   ```bash
   dotnet build -v:detailed 2>&1 | grep -i generator
   ```

### Files Are Empty

1. Ensure handler interfaces are from `Excalibur.Dispatch.Abstractions`
2. Verify classes are public and non-abstract
3. Check for compilation errors that block generator execution

### IDE Not Showing Generated Files

1. Restart IDE after adding MSBuild configuration
2. Ensure project builds successfully
3. Check IDE-specific settings (see sections above)

### Stale Generated Code

If generated code seems outdated:

1. Clean solution: `dotnet clean`
2. Delete `obj/` and `bin/` folders
3. Rebuild: `dotnet build`
4. Restart IDE

## Comparing Generated Output

To compare generated output across builds:

```bash
# Save current state
cp -r obj/Debug/net8.0/GeneratedFiles ./generated-before

# Make changes and rebuild
dotnet build

# Compare
diff -r ./generated-before obj/Debug/net8.0/GeneratedFiles
```

## CI/CD Considerations

For build verification:

```yaml
# GitHub Actions example
- name: Build and verify generators
  run: |
    dotnet build -p:EmitCompilerGeneratedFiles=true
    ls -la obj/Debug/net8.0/GeneratedFiles/Excalibur.Dispatch.SourceGenerators/
```

Generated files should NOT be committed to source control - they're build artifacts.

## Related Documentation

- [Source Generators](./source-generators.md) - Full generator documentation
- [Performance Overview](../performance/index.md) - Performance benefits
- [Deployment](./deployment.md) - AOT deployment guide

## See Also

- [Source Generators Getting Started](../source-generators/getting-started.md) — Step-by-step guide to enabling and configuring source generators
- [Source Generators](./source-generators.md) — Full reference for all 11 Dispatch source generators
- [Native AOT](./native-aot.md) — Native AOT compilation guide that relies on source-generated code
