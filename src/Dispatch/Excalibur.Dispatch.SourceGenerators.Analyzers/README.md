# Excalibur.Dispatch.SourceGenerators.Analyzers

Roslyn diagnostic analyzers for Dispatch source generator consumers. Provides compile-time guidance for handler discovery, AOT compatibility, and performance optimization.

## Installation

This package is typically installed alongside `Excalibur.Dispatch.Generators`:

```bash
dotnet add package Excalibur.Dispatch.Generators
```

## Diagnostics

The analyzers provide compile-time diagnostics to help you:

- Ensure handlers are properly attributed for source generator discovery
- Identify AOT-incompatible patterns before publishing
- Suggest performance optimizations for handler registration

## Documentation

See the [source generators guide](https://github.com/TrigintaFaces/Excalibur) for detailed usage.

## License

This package is part of the Excalibur framework. See [LICENSE](..\..\..\LICENSE) for license details.
