# Excalibur.LeaderElection.InMemory

In-memory leader election implementation for Excalibur framework. Suitable for single-process scenarios, testing, and development.

## Installation

```bash
dotnet add package Excalibur.LeaderElection.InMemory
```

## Quick Start

```csharp
services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le => le.UseInMemory()));
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
