# Excalibur.Dispatch.Analyzers

Roslyn analyzers that enforce Excalibur framework conventions, async best practices, and .NET design guidelines at compile time.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Analyzers
```

## Diagnostic Rules

| Rule    | Severity | Description |
|---------|----------|-------------|
| DISP101 | Warning  | DI extension class should be in `Microsoft.Extensions.DependencyInjection` namespace |
| DISP102 | Warning  | Extension class should not use interface-style 'I' prefix |
| DISP103 | Warning  | CancellationToken should not have default value in interface methods |
| DISP104 | Warning  | Namespace should not contain '.Core.' segment |
| DISP105 | Warning  | Missing `ConfigureAwait(false)` in library code |
| DISP106 | Warning  | Blocking call (`.Result`/`.Wait()`/`.GetResult()`) in async method |

### DISP101: DI Extension Namespace

ServiceCollection extension methods must live in the `Microsoft.Extensions.DependencyInjection` namespace for IntelliSense discoverability. This follows Microsoft's convention for all first-party packages.

### DISP102: Extension Class Naming

Static extension classes must not use an interface-style 'I' prefix (e.g., `IDispatcherExtensions`). The 'I' prefix is reserved for interfaces per .NET Framework Design Guidelines.

### DISP103: CancellationToken in Interfaces

Framework interface methods must require `CancellationToken` parameters without default values. Optional CancellationToken hides cancellation support and creates non-cancellable call chains.

### DISP104: Core Namespace Segment

Namespaces in the Excalibur framework must not contain a `.Core.` segment (e.g., `Dispatch.Core.Messaging`). Use a direct namespace path instead.

### DISP105: ConfigureAwait in Library Code

All `await` expressions in framework/library code must use `ConfigureAwait(false)`. Without it, continuations may be scheduled on the caller's synchronization context, causing deadlocks.

### DISP106: Blocking Calls in Async Methods

Async methods must not use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`. These blocking patterns cause thread pool starvation in high-throughput dispatch pipelines. Use `await` instead.

## Related Packages

- **Excalibur.Dispatch.SourceGenerators.Analyzers** - Handler discovery, AOT compatibility, and message type validation (DISP001-DISP006)

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
