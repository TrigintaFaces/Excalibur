# Excalibur.Dispatch.Resilience.Polly

Polly-based resilience patterns for the Excalibur framework.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Dispatch.RabbitMQ` | Starter | Transport + Resilience + Observability |
| `Excalibur.Dispatch.Kafka` | Starter | Transport + Resilience + Observability |
| `Excalibur.Dispatch.Azure` | Starter | Transport + Resilience + Observability |
| `Excalibur.Dispatch.Aws` | Starter | Transport + Resilience + Observability |

> **Tip:** This package is automatically included when you install any transport starter metapackage.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Resilience.Polly
```

## Features

- Retry policies
- Circuit breaker
- Timeout handling
- Bulkhead isolation
- Fallback strategies

## Configuration

```csharp
services.AddDispatch(options =>
{
    options.UsePollyResilience(polly =>
    {
        polly.AddRetry(3);
        polly.AddCircuitBreaker(5, TimeSpan.FromSeconds(30));
        polly.AddTimeout(TimeSpan.FromSeconds(10));
    });
});
```

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
