# ActivitySource Naming Convention

## Convention

Pattern: `excalibur.{package-shortname}`

| Package | ActivitySource Name | Meter Name |
|---------|-------------------|------------|
| Excalibur.Dispatch | `Excalibur.Dispatch` | `Excalibur.Dispatch` |
| Excalibur.Outbox | `Excalibur.Dispatch.BackgroundServices` | `Excalibur.Dispatch.BackgroundServices` |
| Excalibur.Outbox.Store | -- | `Excalibur.Outbox.Store` |
| Transport packages | `Excalibur.Dispatch.Transport.{Name}` | `Excalibur.Dispatch.Transport.{Name}` |

## Registration

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource("Excalibur.Dispatch")
        .AddSource("Excalibur.Dispatch.BackgroundServices"))
    .WithMetrics(m => m
        .AddMeter("Excalibur.Dispatch")
        .AddMeter("Excalibur.Dispatch.BackgroundServices"));
```

## Lifecycle

- **Static `{ get; } = new(...)`**: Library telemetry constants (process-lifetime)
- **DI `IMeterFactory.Create(...)`**: Dynamic names, per-transport instances
