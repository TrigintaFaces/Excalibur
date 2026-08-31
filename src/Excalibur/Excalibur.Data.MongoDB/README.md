# Excalibur.Data.MongoDB

MongoDB database provider implementation for Excalibur data access layer

## Installation

```bash
dotnet add package Excalibur.Data.MongoDB
```

## Quick Start

```csharp
services.AddExcaliburMongoDb(mongo => mongo
    .ConnectionString("mongodb://localhost:27017")
    .DatabaseName("myapp"));
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
