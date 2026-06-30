# Excalibur.Dispatch.Migration.Analyzers

Roslyn diagnostic analyzers that help migrate code off the now-commercial **MediatR** (and
**MassTransit**, WS3) packages onto **Excalibur.Dispatch**.

These analyzers emit `EXMIG####` diagnostics flagging constructs that are mechanically portable to the
Excalibur.Dispatch compat surface (`Excalibur.Dispatch.Compat.MediatR`). Deterministic rewrites are
offered by the companion package **`Excalibur.Dispatch.Migration.CodeFixes`**; constructs with no
deterministic rewrite emit an informational diagnostic describing the manual step (never a silent skip).

| Diagnostic | Severity | Description |
|------------|----------|-------------|
| `EXMIG0001` | Info | `services.AddMediatR(...)` registration is portable to `AddMediatRCompat(...)`. |

Diagnostic category: `Migration`. Reserved id range: `EXMIG0001`–`EXMIG00NN` (release-tracked in
`AnalyzerReleases.Shipped.md` / `AnalyzerReleases.Unshipped.md`).
