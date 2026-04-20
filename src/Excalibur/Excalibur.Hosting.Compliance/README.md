# Excalibur.Hosting.Compliance

`IExcaliburBuilder` bridge extensions for the Excalibur compliance (GDPR) subsystem.

## Purpose

This package provides bridge extension methods that let consumers configure
GDPR compliance services inside a single
`services.AddExcalibur(excalibur => ...)` composition root, per ADR-321.

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

- ADR-321 — Single composition root
- ADR-324 — Compliance package placement decision
- `Excalibur.Compliance` — the actual compliance service implementations
  (physical package rename to `Excalibur.Compliance` deferred to S795 per
  ADR-324 §Open-Questions §2)
