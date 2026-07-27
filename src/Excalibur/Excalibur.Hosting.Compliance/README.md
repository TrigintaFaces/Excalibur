# Excalibur.Hosting.Compliance

`IExcaliburBuilder` bridge extensions for the Excalibur compliance (GDPR) subsystem.

## Purpose

This package provides bridge extension methods that let consumers configure
GDPR compliance services inside a single
`services.AddExcalibur(excalibur => ...)` composition root.

Keeping the bridge in a separate package (rather than inside `Excalibur.Hosting`)
avoids pulling the heavy compliance transitive dependencies — MongoDB.Driver,
Npgsql, QuestPDF — into every consumer that only wants the base Hosting surface.
This follows the Microsoft-first Package-Split pattern documented in CLAUDE.md
§NuGet Packaging.

## Usage

```csharp
services.AddExcalibur(excalibur => excalibur
    .AddDispatch(...)
    .AddGdprErasure(opts => opts.RetentionDays = 30));
```

## Related

- `Excalibur.Compliance` — the actual compliance service implementations
