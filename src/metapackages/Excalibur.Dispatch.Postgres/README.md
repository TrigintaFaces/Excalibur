# Excalibur.Dispatch.Postgres

Experience metapackage that bundles Excalibur.Dispatch with PostgreSQL event sourcing and outbox for a single-package setup.

## Quick Start

```csharp
// The metapackage bundles the dependencies; registration uses each package's own entry point.
services.AddDispatch();
services.AddExcalibur(excalibur => excalibur
    .AddEventSourcing(es => es.UsePostgres(pg => pg.ConnectionString("Host=...")))
    .AddOutbox(outbox => outbox.UsePostgres(pg => pg.ConnectionString("Host=..."))));
```

This registers: Dispatch core, PostgreSQL event store, snapshot store, and outbox.

## Included Packages

- `Excalibur.Dispatch`
- `Excalibur.EventSourcing.Postgres`
- `Excalibur.Outbox.Postgres`

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
