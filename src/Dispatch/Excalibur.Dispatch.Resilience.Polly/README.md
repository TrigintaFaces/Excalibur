# Excalibur.Dispatch.Resilience.Polly

Polly-based resilience patterns for the Dispatch messaging framework.

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
