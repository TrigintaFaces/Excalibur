# Excalibur.Jobs.Abstractions

Job abstractions for the Excalibur framework.

## Installation

```bash
dotnet add package Excalibur.Jobs.Abstractions
```

## Key Types

- `IBackgroundJob` - Background job interface (simple jobs)
- `IBackgroundJob<TContext>` - Background job with typed context data
- `IConfigurableJob<TConfig>` - Configurable job interface
- `IJobConfig` - Job configuration interface

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
