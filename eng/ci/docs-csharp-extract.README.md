# docs-csharp-extract

Standalone extractor + **tier-1 phantom-API detector** for C# snippets embedded in the
documentation surface. Python 3, stdlib only, no external dependencies, deterministic.

**Why it exists:** C# snippets in `docs/`, `docs-site/`, and `README*` files are never
compiled, so phantom APIs — types/methods a doc claims exist but that were renamed,
removed, or never shipped — rot silently. This tool is the cheap first line of defence:
it extracts every C# block and, without a full compiler, flags framework types a snippet
references that do not exist in the repo's real public surface.

> This tool is the extractor + tier-1 resolver. It is wired into CI (not `settings.json`,
> not pre-commit) through the **diff-scoped** wrapper `docs-csharp-phantom-gate.sh`, which
> gates only snippets a diff touched. Its `--json` output is also the contract the
> downstream tier-2 full-compile gate consumes.

---

## Usage

```bash
# Gate mode (default): phantom-scan tier-1 blocks; exit 1 on any phantom, 0 when clean.
python3 eng/ci/docs-csharp-extract.py

# Emit the per-block JSON records array (no phantom gating). This is the contract the
# tier-2 full-compile gate consumes.
python3 eng/ci/docs-csharp-extract.py --json

# Scope the DOC scan to a directory (e.g. a test fixture). The real public surface is
# still resolved from --repo, so scoping the docs does not weaken symbol resolution.
python3 eng/ci/docs-csharp-extract.py --root path/to/docs

# Resolve the public surface from a specific repo root (default: current directory).
python3 eng/ci/docs-csharp-extract.py --repo /path/to/repo
```

| Flag | Meaning |
|------|---------|
| `--root <dir>` | Root for the **doc walk** only (default: `--repo`). Scopes which docs are scanned; does not affect symbol resolution. |
| `--repo <dir>` | Repo root used to build the **real public surface** (default: cwd). |
| `--json` | Emit the block records array to stdout; skip phantom gating (always exit 0). |
| `--gate-files <file>` | Newline-delimited repo-relative doc paths. Gate ONLY blocks in those files (**whole-file** scope). Symbol resolution stays repo-wide. |
| `--gate-lines <file>` | `relpath:linenumber` entries (one per changed line). Gate only blocks whose fenced span intersects a changed line (**hunk** scope). Takes precedence over `--gate-files`. This is what the CI gate uses so editing an unrelated line never re-flags a pre-existing snippet. |

The doc walk covers `docs-site/**/*.md(x)`, `docs/**/*.md`, and repo-wide `**/README*.md`,
skipping `node_modules`, `bin`, `obj`, `.git`, `.dts`, `.claude`.

---

## The marker convention — tier-1 vs tier-2

Every extracted C# block is classified into one of two tiers:

| Tier | Meaning | How a block opts in |
|------|---------|---------------------|
| **tier-1 (`resolve`)** | Phantom-API resolution only (this tool). The **default** for any C# block. | (nothing — default) |
| **tier-2 (`compile`)** | Full compilation. Owned by the downstream full-compile gate, **not** this tool. | Info-string carries the whitespace-delimited token `runnable` (case-sensitive), e.g. ` ```csharp runnable `. Secondary signal: the first in-fence line is exactly `// compile-check`. |
| **`ignore`** | Deliberate teaching placeholder — excluded from phantom gating by declaration. | Info-string carries `ignore` or `no-compile`, e.g. ` ```csharp ignore `. Secondary signal: first in-fence line `// no-compile`. Takes precedence over `runnable`. |

A block is C# when its fence info-string language is `csharp` or `cs`.

```csharp
// This block is TIER-1 (resolve). It is phantom-scanned, not compiled.
using Excalibur.Dispatch;
var dispatcher = services.GetRequiredService<IDispatcher>();
```

````
```csharp runnable
// This block is TIER-2 (compile). The `runnable` token opts it into full
// compilation, which the tier-2 gate performs — this tool only classifies it.
```
````

---

## The JSON record contract (consumed by the tier-2 compile gate)

`--json` emits a single JSON array; one record per extracted C# block. **Field names are
the contract — do not rename them.**

```json
[
  {
    "file": "docs-site/docs/handlers.md",
    "startLine": 28,
    "lang": "csharp",
    "tier": "resolve",
    "code": "using Excalibur.Dispatch.Delivery;\n..."
  }
]
```

| Field | Meaning |
|-------|---------|
| `file` | Repo-relative path of the containing markdown file. |
| `startLine` | 1-based line of the opening fence. |
| `endLine` | 1-based line of the closing fence (block span = `startLine`..`endLine`; used for hunk scoping). |
| `lang` | Always `"csharp"`. |
| `tier` | `"resolve"` (tier-1), `"compile"` (tier-2), or `"ignore"` (opted out). |
| `code` | The raw block text (fence lines excluded). |

---

## Tier-1 phantom scan — what it does, and its limits

For every tier-1 block that **establishes framework context** (contains a
`using Excalibur.*;` / `using Dispatch.*;`), the tool extracts identifiers used in a
**type position** (`new X`, `: X`, `<X>`, `Type name = …`, `X.StaticMember`) and flags any
that:

- do **not** appear in the real public surface (`src/**/PublicAPI*.txt` + a lightweight
  grep of `public … (class|interface|record|struct|enum) <Name>` across `src/**/*.cs`), **and**
- are **not** declared inside the snippet itself (local example types are never phantoms), **and**
- are **not** in the built-in BCL / framework-external denylist, **and**
- are **not** a generic type parameter (`TMessage`, `TResponse`).

On a hit it prints `file:line: phantom API 'X' — not found in public surface` and exits
non-zero. Clean → exit 0. Always ends with a summary:
`N blocks (T1 resolve / T2 compile) across M files; K phantom(s)`.

### Deliberately conservative (low false-positive posture)

A phantom detector that cries wolf gets ignored, so the tool errs toward **not** flagging:

- Snippets with no framework `using` are not scanned at all.
- Bare `Name(` and instance `.Name(` calls are **not** treated as types (they catch method
  definitions and BCL calls like `.ConfigureAwait(`, `.AddSeconds(` — pure noise).
- Chained members (`DateTime.UtcNow.Foo`) are excluded via lookbehind.

### Known limitation — the tier-1 ceiling

Without a real symbol table for each doc's *example ecosystem*, tier-1 **cannot**
distinguish a genuine framework phantom from a consumer's own **placeholder domain type**
that the example references but declares elsewhere (`IOrderRepository`, `OrderCreatedEvent`,
`MyCommand`, `UnitTestBase`, or a third-party type such as `BsonBinaryReader`). These
surface as flags and are **expected false positives** for a resolve-only pass. Treat the
gate output as *candidates to triage*, not a hard build-breaker, until tier-2
(full compilation) provides real resolution. That is precisely the tier-1/tier-2
boundary: tier-1 is a cheap smoke screen for obviously-nonexistent framework types; tier-2
compiles `runnable` blocks and is the authoritative check.

**How the CI gate lives with the tier-1 ceiling:** `docs-csharp-phantom-gate.sh` runs this
tool **hunk-scoped** — it gates only the snippets a diff actually touched (`--gate-lines`) —
so the pre-existing placeholder false positives in untouched docs never block a build. A new
or edited snippet that references a genuine framework phantom fails; an intentional
placeholder opts out with a ` ```csharp ignore ` fence. The pre-existing false-positive
backlog is tracked separately, not carried by the gate.

---

## Self-test

`docs-csharp-extract.test.sh` is a non-vacuous self-test with four arms:

- **SAFETY** — a planted phantom (`using Excalibur.Dispatch;` + `new
  ITotallyFakeDispatcherXyz()`) is flagged: gate exits 1 and prints the phantom name.
- **LIVENESS** — a real API (`IDispatcher`, verified present in a `PublicAPI*.txt`) is
  **not** flagged: gate exits 0. (A detector that flagged everything would fail this arm.)
- **CLASSIFY** — `csharp runnable` ⇒ tier-2 (`compile`); plain `csharp` ⇒ tier-1 (`resolve`).
- **IGNORE** — a ` ```csharp ignore ` block is excluded from gating (exits 0, phantom not reported).

```bash
bash eng/ci/docs-csharp-extract.test.sh
```

## The CI gate — `docs-csharp-phantom-gate.sh`

The diff-scoped wrapper that wires this tool into CI (`documentation-validation` job in
`.github/workflows/ci.yml`), **not** pre-commit. It resolves the changed doc files vs the
base ref, extracts their changed line ranges (`git diff -U0`), and fails only on a phantom
in a snippet the diff **touched** — pre-existing phantoms in untouched docs are tolerated.
Its own non-vacuous self-test (`docs-csharp-phantom-gate.test.sh`) proves safety, liveness,
diff-scope, ignore-marker, no-op, and hunk arms.

```bash
bash eng/ci/docs-csharp-phantom-gate.sh        # gate (diff vs origin/main)
bash eng/ci/docs-csharp-phantom-gate.test.sh   # prove the gate can fail
```
