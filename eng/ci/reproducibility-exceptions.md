# Reproducibility: the one accepted exception, and what removes it

`verify-reproducible-build.py` asserts *same-runner clean-root reproducibility and unsigned
package-content equality*. That claim is **false today**, for a cause that is fully diagnosed,
upstream, already fixed upstream, and not yet available in any released package. This file records
the exception so the gate can run and report honestly without blocking a pipeline on a defect no
change in this repository can fix. `ci.yml` prints it next to the failure.

## The cause, measured rather than inferred

`Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator` emits every `[LoggerMessage]`
partial class into one `LoggerMessage.g.cs` and **does not order them**. The order varies per
compilation, so every assembly that uses `[LoggerMessage]` — nearly all of ours — differs byte-wise
between two builds of identical source.

Measured 2026-10-04 on `Excalibur.Dispatch`, three builds:

| arm | result |
|---|---|
| two worktrees, **different** paths | `Excalibur.Dispatch.dll` differs |
| same worktree, **same** path, rebuilt | **differs again** — so it is not path-dependence |
| `LoggerMessage.g.cs` from two builds | 492,742 bytes and 5,300 lines in **both**; `sort`ed content **byte-identical**; 70 namespace blocks in a different order |

The third arm is the discriminator: identical length with identical sorted content is a *permutation*,
not a content difference. Nothing else under `obj/` differed — 85 generated `.cs` files compared, one
differed, and that one was the generator's.

The compiler loaded `…/microsoft.extensions.logging.abstractions/10.0.12/analyzers/dotnet/roslyn4.4/cs/Microsoft.Extensions.Logging.Generators.dll`,
confirmed from the `/analyzer:` argument, so this is the current package's generator and not an SDK-bundled older copy.

## Upstream state

- `dotnet/runtime#119587` — the defect. Closed. No workaround documented.
- `dotnet/runtime#123428` — the fix, milestone **11.0.0**.
- `dotnet/runtime#133674` — the `release/10.0` backport, merged **2026-09-11**.

**The backport is NOT in `10.0.12`.** That is measured, not assumed: pinning the whole
`Microsoft.Extensions.*` family to `10.0.12` in a throwaway worktree and building twice still produced
a permuted `LoggerMessage.g.cs` and two different DLL hashes. The generator binary *did* change between
`10.0.10` and `10.0.12`, so the package moved and the ordering fix was simply not in it.

## What this exception does and does not cover

- **Covers:** a build-to-build difference in compiled assemblies attributable to generated-logging
  class ordering.
- **Does NOT cover:** anything else the gate reports. A differing `.nuspec`, a missing or extra package
  entry, a noncanonical entry path, a changed packaged script, an empty or unreadable package — all of
  those are ours and none of them is excused here.

The gate is therefore left **running and reporting its true verdict**; only its ability to block is
suspended. A pass is never manufactured.

## Removal condition — a green run, not a date

1. A `10.0.x` servicing release of `Microsoft.Extensions.Logging.Abstractions` carries
   `dotnet/runtime#133674`. Verify by pinning it and building one project twice: `LoggerMessage.g.cs`
   must be byte-identical across the two builds.
2. Bump the `Microsoft.Extensions.*` pins in `Directory.Packages.props`.
3. Delete `continue-on-error` from the `Verify independent build outputs` step in `ci.yml`, delete this
   file, and delete the reference to it.

`ci.yml` emits a warning if the gate ever **passes** while this exception is still in place, so the
exception cannot quietly outlive the defect.

Tracked: see the release backlog entry naming `dotnet/runtime#119587`.
