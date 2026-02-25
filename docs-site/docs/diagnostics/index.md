# Analyzer Diagnostics

Excalibur ships Roslyn analyzers that detect common issues at compile time. These diagnostics appear in your IDE and build output.

## Diagnostic Reference

| ID | Title | Severity | Category |
|----|-------|----------|----------|
| [DISP001](./DISP001.md) | Handler Not Discoverable | Warning | Handlers |
| [DISP002](./DISP002.md) | Missing AutoRegister Attribute | Info | Handlers |
| [DISP003](./DISP003.md) | Reflection Without AOT Annotation | Warning | Compatibility |
| [DISP004](./DISP004.md) | Optimization Hint | Info | Performance |

## Installation

The analyzers are included automatically when you reference `Excalibur.Dispatch.SourceGenerators.Analyzers`:

```bash
dotnet add package Excalibur.Dispatch.SourceGenerators.Analyzers
```

Or use the combined source generators package which includes analyzers:

```bash
dotnet add package Excalibur.Dispatch.SourceGenerators
```

## Suppressing Diagnostics

To suppress a specific diagnostic, use a `#pragma` directive:

```csharp
#pragma warning disable DISP001
// Your code here
#pragma warning restore DISP001
```

Or suppress project-wide in your `.csproj`:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);DISP001</NoWarn>
</PropertyGroup>
```

## See Also

- [Source Generators Getting Started](../source-generators/getting-started.md)
- [Advanced Source Generators](../advanced/source-generators.md)
