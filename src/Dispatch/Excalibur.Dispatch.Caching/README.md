# Excalibur.Dispatch.Caching

Caching middleware and extensions for the Dispatch messaging framework.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Caching
```

## Features

- Query result caching
- Distributed cache support
- Cache invalidation patterns
- Memory and Redis cache providers

## Configuration

```csharp
services.AddDispatch(options =>
{
    options.UseCaching(cache =>
    {
        cache.UseDistributedCache();
        cache.DefaultExpiration = TimeSpan.FromMinutes(5);
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
