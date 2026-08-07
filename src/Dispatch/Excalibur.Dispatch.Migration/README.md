# Excalibur.Dispatch.Migration

Roslyn analyzers **and** their code fixes for migrating off the now-commercial **MediatR** (and
**MassTransit**) packages onto **Excalibur.Dispatch**.

Install this one package to get both the diagnostics and the rewrites that resolve them:

```xml
<PackageReference Include="Excalibur.Dispatch.Migration" Version="..." PrivateAssets="all" />
```

`PrivateAssets="all"` is the usual choice for analyzer packages — the migration tooling is a
build-time concern for your project, not something your own consumers should inherit.

## What it does

The analyzers emit `EXMIG####` diagnostics on constructs that are mechanically portable to the
Excalibur.Dispatch compat surface (`Excalibur.Dispatch.Compat.MediatR`). Where a deterministic
rewrite exists, a code fix applies it. Where one does not, the diagnostic describes the manual step
rather than skipping silently.

| Diagnostic | Severity | Description |
|------------|----------|-------------|
| `EXMIG0001` | Info | `services.AddMediatR(...)` registration is portable to `AddMediatRCompat(...)`. |

Diagnostic category: `Migration`. Reserved id range: `EXMIG0001`–`EXMIG00NN`, release-tracked in the
analyzer project's `AnalyzerReleases.Shipped.md` / `AnalyzerReleases.Unshipped.md`.

## Why one package

A diagnostic and its fix are one deliverable. Shipping the analyzers and the code fixes as separate
packages meant a consumer could install the diagnostics and have no way to apply them — so this
package carries both assemblies, the way `Microsoft.CodeAnalysis.NetAnalyzers` and
`StyleCop.Analyzers` do.

The contributing projects are not published on their own, so there is exactly one delivery path and
a diagnostic cannot arrive twice.
