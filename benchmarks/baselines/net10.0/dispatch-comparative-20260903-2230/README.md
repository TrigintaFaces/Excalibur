# WarmPath epoch 2026-09-03 22:30

This is the run every current performance claim in the repository quotes -- the README table,
`docs-site/docs/performance/*`, and `docs-site/docs/migration/from-mediatr.md`.

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
Job=warmpath-inproc  Toolchain=InProcessEmitToolchain
```

## Why this sits beside `dispatch-comparative-20260903/` rather than replacing it

That directory holds the **15:59** run of the same day. Both are real; they measure different code.
Between them landed the change that stopped publishing a message context to handlers that never
read one, and it moved the standard path substantially:

| | 15:59 run | 22:30 run |
|---|---|---|
| `Dispatch: Single command handler` | 50.57 ns / 96 B | 30.49 ns / 24 B |

So the earlier directory is not stale data to be corrected -- it is the before side of that change,
and it is kept for that reason. Anything quoting a current figure should cite **this** directory.

## Contents

Only the joined report survived from this run; the per-class report files were not retained. The
joined report carries every row of the matrix, so no measurement is missing -- the per-class split
is. Committed here because until now these numbers lived solely in the gitignored
`BenchmarkDotNet.Artifacts/` at the repository root, which made every published claim that quotes
them unverifiable by anyone but the machine that ran them.
