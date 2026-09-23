# Excalibur.Outbox.MongoDB

MongoDB bridge package for the Excalibur outbox pattern.

## Installation

```bash
dotnet add package Excalibur.Outbox.MongoDB
```

## Usage

```csharp
services.AddExcalibur(x => x.AddOutbox(outbox => outbox.UseMongoDB(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "myapp";
})));
```

This bridge package provides:
- `IOutboxBuilder.UseMongoDB()` extension method (from `Excalibur.Data.MongoDB`)
- Transitive dependency on `Excalibur.Outbox` and `Excalibur.Data.MongoDB`

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
