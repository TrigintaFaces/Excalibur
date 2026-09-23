# Analyzer Diagnostics

Excalibur ships Roslyn analyzers that detect common issues at compile time. These diagnostics appear in your IDE and build output.

## Diagnostic Reference

| ID | Title | Severity | Category |
|----|-------|----------|----------|
| [DISP001](./DISP001.md) | Handler Not Discoverable | Warning | Handlers |
| [DISP002](./DISP002.md) | Missing AutoRegister Attribute | Info | Handlers |
| [DISP003](./DISP003.md) | Reflection Without AOT Annotation | Warning | Compatibility |
| [DISP004](./DISP004.md) | Optimization Hint | Info | Performance |
| [DISP005](./DISP005.md) | Handler Should Be Sealed | Warning | Handlers |
| [DISP006](./DISP006.md) | Message Type Missing Dispatch Interface | Warning | Handlers |

## Migration Diagnostics (EXMIG)

The `Excalibur.Dispatch.Migration.Analyzers` / `Excalibur.Dispatch.Migration.CodeFixes` packages ship
migration-tooling diagnostics that detect MediatR constructs and offer mechanical code-fixes when
porting to the [`Excalibur.Dispatch.Compat.MediatR`](../migration/from-mediatr.md#drop-in-compatibility-shim)
shim. The `EXMIG####` range does not overlap with `DISP###`.

| ID | Title | Severity | Category |
|----|-------|----------|----------|
| [EXMIG0001](./EXMIG0001.md) | MediatR registration is portable | Info | Migration |
| [EXMIG0002](./EXMIG0002.md) | Construct requires a manual migration step | Info | Migration |
| [EXMIG0003](./EXMIG0003.md) | `using MediatR;` directive is swappable | Info | Migration |
| [EXMIG0004](./EXMIG0004.md) | Handler signature differs from compat shape | Warning | Migration |

## Compliance Diagnostics (EXCMP)

`Excalibur.Compliance.Abstractions` ships diagnostics for field-encryption annotations the framework cannot
honour. They report, at build time, the same problems the framework would refuse at run time — and one it
would never notice at all ([EXCMP003](./EXCMP003.md)). They arrive with the package; there is nothing extra
to install.

| ID | Title | Severity | Category |
|----|-------|----------|----------|
| [EXCMP001](./EXCMP001.md) | Annotation on a type that cannot carry ciphertext | Warning | Encryption |
| [EXCMP002](./EXCMP002.md) | Annotation on a property without a getter and a setter | Warning | Encryption |
| [EXCMP003](./EXCMP003.md) | Annotation on a property the framework never inspects | Warning | Encryption |
| [EXCMP004](./EXCMP004.md) | Erasable personal data encrypted under a surviving purpose | Warning | Encryption |

## Installation

The analyzers are included automatically when you reference `Excalibur.Dispatch`:

```bash
dotnet add package Excalibur.Dispatch
```

Or use the combined source generators package which includes analyzers:

```bash
dotnet add package Excalibur.Dispatch
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
