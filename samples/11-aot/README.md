# 11 - Native AOT

Demonstrates Native AOT compilation with Excalibur.Dispatch using source generators for zero-reflection handler resolution, JSON serialization, and result creation.

## Projects

| Project | Description |
|---------|-------------|
| [Excalibur.Dispatch.Aot.Sample](Excalibur.Dispatch.Aot.Sample/) | Full AOT sample with `PublishAot=true`, source-generated handlers, and `JsonSerializerContext` |

## What You'll Learn

- Configuring `PublishAot=true` and `TrimMode=full`
- Using `[AutoRegister]` for compile-time handler discovery
- Creating `JsonSerializerContext` for AOT-safe serialization
- Verifying zero IL2xxx/IL3xxx warnings

## Prerequisites

- .NET 10.0 SDK (or latest)
- `Excalibur.Dispatch.SourceGenerators` package referenced

## Quick Start

```bash
cd Excalibur.Dispatch.Aot.Sample
dotnet publish -c Release
./bin/Release/net10.0/publish/Excalibur.Dispatch.Aot.Sample
```

## Related Docs

- [Native AOT Guide](../../docs-site/docs/advanced/native-aot.md)
- [AOT Compatibility Matrix](../../docs-site/docs/advanced/aot-compatibility.md)
- [Source Generators](../../docs-site/docs/advanced/source-generators.md)
