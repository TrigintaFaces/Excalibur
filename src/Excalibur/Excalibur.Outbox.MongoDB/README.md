# Excalibur.Outbox.MongoDB

MongoDB bridge package for the Excalibur outbox pattern.

## Installation

```bash
dotnet add package Excalibur.Outbox.MongoDB
```

## Usage

```csharp
services.AddExcaliburOutbox(outbox => outbox.UseMongoDB(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "myapp";
}));
```

This bridge package provides:
- `IOutboxBuilder.UseMongoDB()` extension method (from `Excalibur.Data.MongoDB`)
- Transitive dependency on `Excalibur.Outbox` and `Excalibur.Data.MongoDB`
