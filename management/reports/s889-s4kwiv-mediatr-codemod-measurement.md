# S889 · s4kwiv — MediatR Migration Codemod Measurement

**Bead:** `s4kwiv` (P1) · **Lane G** · **Method:** in-repo non-trivial fixture (PM ruling, msg 30613) — deterministic, committable, CI-stable. Not an external OSS clone.

## What was measured

The MediatR migration codemod (4 Roslyn analyzers + their code-fixes, diagnostics `EXMIG0001`–`EXMIG0004`)
was run over a **single non-trivial fixture app** that exercises **every migration category at once**, to
record: **auto-fix rate**, the **exact manual-step (`EXMIG0002`) list**, and to confirm **no crash /
no silent skip**.

- **Fixture:** an order-processing MediatR app — a request handler and a notification handler (both using
  the legacy `HandleAsync` name), a portable `IPipelineBehavior`, the `AddMediatR` registration, the
  `using MediatR;` import, and one instance of every non-portable construct MediatR supports.
- **Harness:** `CSharpCompilation.WithAnalyzers(...).GetAnalyzerDiagnosticsAsync()` — runs the real
  analyzers over the fixture and collects only analyzer diagnostics (compiler errors excluded, so the
  stubbed fixture does not pollute the tally).
- **Committed test:** `tests/unit/Excalibur.Dispatch.Migration.Tests/MediatRMigrationCodemodMeasurementShould.cs`
  (2 facts, both green — the measurement is self-checking and re-runs in CI).

## Results

| Category | Diagnostic | Kind | Count |
|----------|-----------|------|-------|
| MediatR registration call (`AddMediatR`) | `EXMIG0001` | auto-fix | 1 |
| `using MediatR;` directive | `EXMIG0003` | auto-fix | 1 |
| Legacy `HandleAsync` handler signature | `EXMIG0004` | auto-fix | 2 |
| Non-portable construct | `EXMIG0002` | **manual** | 5 |
| **Total migration points** | | | **9** |

- **Auto-migrated:** 4 of 9 points (`EXMIG0001` ×1, `EXMIG0003` ×1, `EXMIG0004` ×2).
- **Manual steps:** 5 of 9 points (`EXMIG0002`).
- **Auto-fix rate: 4 / 9 ≈ 44.4%.**

### Exact `EXMIG0002` manual-step list (never silently skipped)

The compat shim deliberately does **not** cover these five MediatR constructs; each surfaces an
informational `EXMIG0002` so the remaining manual step is explicit:

1. `IRequestPreProcessor`
2. `IRequestPostProcessor`
3. `IRequestExceptionHandler`
4. `IRequestExceptionAction`
5. `IStreamPipelineBehavior`

### No crash / no silent skip

The analyzer run completed without throwing, and **every** planted migration point produced its
diagnostic — the 4 auto-fixable points AND all 5 non-portable constructs. Nothing was silently dropped.

## Notes

- The **per-category** detection/fix behaviour is independently locked by the sibling
  `*AnalyzerShould` / `*CodeFixShould` tests; this measurement proves behaviour on a realistic app where
  every category is present **together** (no category masks another).
- The auto-fix rate is a property of the fixture's construct mix (chosen to include one of every category,
  which over-weights the manual constructs relative to a typical app — a real app with many handlers and
  few exception-processors would score a substantially higher auto-fix rate, since handlers/registrations/
  usings are all auto-fixable).
- Any launch-narrative "ran on real OSS project X" credibility claim is a separate marketing artifact
  owned by ProductManager (run manually against a real app), decoupled from this CI-committed measurement.
