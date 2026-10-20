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
- Framework-owned claims, attempt budgets, backoff scheduling and dead-letter transitions

## Usage

```csharp
services.AddDispatch(dispatch => dispatch.UseOutbox());
services.AddExcalibur(x => x.AddOutbox(outbox => outbox
    .UseSqlServer(sql => sql.ConnectionString(connectionString))
    .WithProcessing(processing => processing
        .BatchSize(100)
        .MaxRetryCount(3)
        .RetryDelay(TimeSpan.FromSeconds(10)))
    .EnableBackgroundProcessing()));
```

## Transactional Outbox Pattern

Handlers explicitly buffer events through `IOutboxWriter`. The middleware stages them after successful
handling, and the opt-in hosted processor claims and delivers eligible messages. Register handlers and
the selected transport as appropriate for your application. A shared business-data transaction requires
the corresponding transaction integration; buffering alone does not make business writes atomic.

`BatchSize` sets producer and consumer batch defaults. `MaxRetryCount` excludes the initial attempt:
three retries means four total attempts; zero means one. `RetryDelay` supplies the default fixed backoff
calculator for this processor. Backoff-capable stores retain the later of that schedule and their failure
floor. A consumer-supplied `IBackoffCalculator`, including the Polly adapter, takes precedence.

Explicit `Configure`/`PostConfigure` callbacks for delivery and background options override builder
defaults regardless of registration order. Inspect those resolved options for effective diagnostics.
For `int.MaxValue` retries, the initial attempt makes the default total unrepresentable; configure a
positive `OutboxDeliveryOptions.MaxAttempts` total explicitly or choose fewer retries. Delivery options
are validated when consumed, so a custom publisher can own an independent policy.

Runtime payload serialization may require source-generated serializer configuration for trimming or
Native AOT. The local runtime controls do not certify every provider under Native AOT.

## Related Packages

- `Excalibur.Outbox.SqlServer` - SQL Server outbox implementation
- `Excalibur.Dispatch.Abstractions` - Message and outbox interfaces

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
