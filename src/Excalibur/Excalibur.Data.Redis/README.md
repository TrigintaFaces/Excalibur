# Excalibur.Data.Redis

Redis cache provider implementation for Excalibur data access layer

## Installation

```bash
dotnet add package Excalibur.Data.Redis
```

## Quick Start

```csharp
services.AddExcaliburRedis(redis => redis
    .ConnectionString("localhost:6379"));
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
