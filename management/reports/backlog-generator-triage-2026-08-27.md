# Backlog triage — why the release count does not fall

Measured 2026-08-27. All 484 open `release` beads read (title + description), no sampling.
Every population figure below was measured in-session, not recalled.

## The question

> "Why does it seem like for weeks we have been opening more beads than we close and every bead has
> more or different work to do. Are we just thrashing?"

## The answer

**Not thrashing. Re-counting.**

85% of the corpus (412/484) belongs to nine mechanisms. But those mechanisms are **not producing new
defects in new code.** They are already detected, already fully enumerated, in committed files, and
exempted:

| baseline | population | status |
|---|---:|---|
| `eng/ci/aot-suppression-baseline.json` | 1,258 | grandfathered |
| `tests/architecture/Boundary.Tests/public-option-liveness-baseline.txt` | 719 | grandfathered |
| `eng/ci/ide0052-baseline.txt` | 53 | grandfathered |
| `eng/ci/gate-wiring-baseline.txt` | 14 | grandfathered |
| `eng/ci/conformance-backend-coverage-baseline.txt` | 12 | grandfathered |
| `eng/ci/conformance-fork-baseline.txt` | 11 | grandfathered |
| `eng/ci/unconditional-skip-baseline.txt` | 7 | grandfathered |
| + 9 more (ddl-parity, ddl-migration, ddl-tables, shipped-ddl-sweep.refuse, architecture-evidence, smoke-test-coverage, container-image-pinning, mutable-image-tag) | | |

Three detectors are switched **off**: `CA1812` and `CA1823` in `src/Directory.Build.props` NoWarn, and
`EnableAotAnalyzer` set **nowhere in the repository** — so IL3050/IL3051 are undetectable in exactly the
52 packages that honestly declared `IsAotCompatible=false`.

**So the 15-30 beads/day are not new damage.** They are agents re-grepping a static, already-enumerated,
exempted population and filing each hit as a fresh bead. 719 baselined options can emit 719 beads. The
population is fixed; the bead count tracks how many people grepped it that day.

This also explains "every bead is wider than filed": a bead filed from a census inherits the census's
predicate, and the predicate is always narrower than the defect.

## The number that should gate a release

```
BLOCKS  157  (32.4%)   consumer observes it: build break, host won't start, wrong/lost data,
                       or a shipped surface making a false claim about security/durability/tenancy
DEBT    324  (66.9%)   real, but only we see it
STALE     3  ( 0.6%)   premise moot at HEAD
```

62% of BLOCKS sit in three generators: PARITY 35, UNWIRED 31, CONVENTION 31.

## Generators

| # | Mechanism | n | BLOCKS | P1 | already exists but unenforced? |
|---|---|---:|---:|---:|---|
| G2 | UNWIRED — surface declared, no reader | 88 | 31 | 9 | yes — the liveness baseline calls its 719 entries "the work queue... none justified" |
| G1 | PARITY — N impls, no binding conformance arm | 65 | **35** | 0 | yes — 49 kits ship; 8 internal bases compete; 17 of 43 kits lack the wiring arm |
| G6 | CONVENTION — invariant remembered, not constructed | 63 | 31 | 0 | yes — ADR-348 made one violation inexpressible, then stopped at that family |
| G8 | INERT-INSTRUMENT — a check that cannot fail | 47 | 2 | 1 | partly |
| G4 | DOCS — prose asserting what code doesn't do | 41 | 9 | 1 | yes — the snippet gate is diff-scoped, so 401 pre-existing are exempt forever |
| G9 | ARTIFACT — SQL/templates/nupkg never run as a consumer would | 34 | 15 | 0 | partly — sweeps bound to `src/**`, compare presence not type |
| G3 | TWINS — one truth, N hand-maintained copies | 27 | 12 | 0 | no |
| G5 | AOT — annotation honesty, detector disabled | 26 | 11 | **6** | the detector is simply off |
| G7 | REINVENT — hand-rolled BCL primitive | 21 | **0** | 0 | no |
| GP | DECISION — a product question in a defect queue | 24 | 0 | 0 | n/a |
| G0 | genuine one-offs | 48 | 11 | 0 | n/a |

## Three actions, in order

1. **Convert the 16 baselines from records into shrinking ratchets; re-arm the three analyzers.**
   One owner per baseline with a burn-down target. **Forbid filing a bead for anything already inside a
   baseline — work the file, not the hit.** Days, not sprints. It is the only action that changes the
   INFLOW, and it collapses 6 of the 17 P1s, which are one root cause with six symptoms.

2. **Delete the unwired surface — do not wire it — before the RC freeze.** 88 beads, 31 BLOCKS, ~60
   close as moot. Deletion is mechanical and reduces surface; wiring 719 properties is months and adds
   it. The ~28 security-shaped ones need individual judgement: implement the protection or remove the
   option AND the doc that promises it, never quietly delete.

3. **Make the shipped conformance kit mandatory and its arms unable to pass by silence.** Highest
   BLOCKS density in the corpus. **Expect the count to RISE first** — the arms will start finding what
   they were built to find. Do it after (1) so those findings land in a tracker that is not drowning.

**Not in the top three despite feeling urgent:** G7 (21 beads, 0 BLOCKS) and G8 (47 beads, 2 BLOCKS) —
the two lowest-leverage-per-BLOCKS groups.

**Schedule two hours of product rulings for GP.** 24 beads no engineer can close: whether a zero-config
`AddDispatch` yields an empty pipeline, whether handler discovery defaults Scoped or Transient, whether
the data-processing queue carries a tenant dimension. They will still be open in six months otherwise.

## Confidence

The moot-vs-individual estimates are from reading descriptions, not from checking each bead against
HEAD — treat as +/-30%. The G3/G9 boundary for shipped-SQL beads is a judgement call defensible either
way; merged, G3 is ~40. The measured figures (484, the baselines and their sizes, 49 kits vs 8 internal
bases, the analyzer settings) are not estimates.

**One caveat that a naive grep gets wrong:** 40 of 484 descriptions carry a stale-premise marker
("PREMISE PARTIALLY REFUTED", "ALREADY SATISFIED AT HEAD"). That is **not** 40 stale beads — in nearly
all of them the author re-measured, corrected the premise in the body, and stated the surviving live
remainder. That is the tracker working. Only 3 look genuinely moot.
