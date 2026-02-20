# Excalibur.Outbox

Transactional outbox pattern implementation for reliable message delivery.

## Installation

```bash
dotnet add package Excalibur.Outbox
```

## Features

- `IOutboxStore` - Outbox message persistence abstraction
- `OutboxOptions` - Configuration for outbox behavior
- TypeForwarders for backward compatibility with existing code
- AOT-compatible with full Native AOT support

## Usage

```csharp
// Register outbox services
services.AddExcaliburOutbox(options =>
{
    options.BatchSize = 100;
    options.MaxRetries = 3;
});
```

## Transactional Outbox Pattern

The outbox pattern ensures reliable message delivery by:
1. Storing messages in the same transaction as business data
2. Publishing messages asynchronously via a background processor
3. Handling retries and dead-letter scenarios

## Related Packages

- `Excalibur.Outbox.SqlServer` - SQL Server outbox implementation
- `Excalibur.Dispatch.Abstractions` - Message and outbox interfaces

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
