# Excalibur.Dispatch.Caching

Caching middleware and extensions for the Excalibur framework.

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
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
