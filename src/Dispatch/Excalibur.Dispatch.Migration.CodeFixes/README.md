# Excalibur.Dispatch.Migration.CodeFixes

Roslyn **code-fix providers** that apply the automatic rewrites for the `EXMIG####` diagnostics emitted
by [`Excalibur.Dispatch.Migration.Analyzers`](../Excalibur.Dispatch.Migration.Analyzers/README.md), as
part of migrating off the now-commercial **MediatR** / **MassTransit** packages onto
**Excalibur.Dispatch**.

| Diagnostic | Code-fix |
|------------|----------|
| `EXMIG0001` | Rewrite `services.AddMediatR(...)` → `services.AddMediatRCompat(...)` (preserves assembly-scan args). |
| `EXMIG0003` | Swap `using MediatR;` → `using Excalibur.Dispatch.Compat.MediatR;` (idempotent; no duplicate/orphaned usings). |
| `EXMIG0004` | Rewrite a handler's `HandleAsync` → `Handle` where the signature delta is deterministic. |

Code-fixes operate in-IDE / via batch fixers. Constructs with no deterministic rewrite get an
informational diagnostic (`EXMIG0002`) describing the manual step instead — never a silent skip.
