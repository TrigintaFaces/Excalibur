# Excalibur.Dispatch.Compat.MediatR.SourceGenerators

Source generator for **Excalibur.Dispatch.Compat.MediatR**. It emits AOT-safe canonical
wrapper-actions, adapter-handlers, and registration code that bridge the MediatR-source-compatible
surface onto `Excalibur.Dispatch` (see ADR-341 §9).

This project is **not published as its own NuGet package** — it is `IsPackable=false` and ships as an
**analyzer asset inside the `Excalibur.Dispatch.Compat.MediatR` package** (ADR-341 §9, Option B2).
Consumers reference it transitively via that package; there is no direct dependency to add.

> Part of the Excalibur.Dispatch MediatR migration tooling. Not affiliated with or endorsed by the
> MediatR project; "MediatR" is a trademark of its respective owner. This is a source-compatibility
> shim to assist migration.
