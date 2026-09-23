# Excalibur.Operations.Dashboard.EventSourcing

Event-sourcing add-on for the [Excalibur Operations Dashboard](https://www.nuget.org/packages/Excalibur.Operations.Dashboard).

Adds the **projection / CDC-lag** read panel: per-subscription checkpoint lag measured against the
global event-stream head (`lag = max(0, head − checkpoint)`). Read-only, fail-open (reports
`configured: false` when no event store or checkpoint store is registered — never a 500).

## Why a separate package

The base `Excalibur.Operations.Dashboard` package depends only on abstractions. Projection-lag needs a
concrete event-sourcing dependency, so it ships here as an **opt-in** package (the Microsoft package-split
pattern) — consumers who don't use event sourcing never pull the event-store implementation into their
dashboard.

## Usage

```csharp
services.AddDashboard()
        .AddProjectionLagDashboard();
```

The panel then appears in capability discovery under `projections` and serves
`GET {prefix}/api/projections/lag`.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
