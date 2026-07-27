# Git Hooks for Excalibur.Dispatch

## Overview
This directory contains canonical versions of Git hooks used for automated governance and quality control.

## Available Hooks

### pre-commit
Enforces namespace depth requirements **and** flushes the Beads tracker before commits are accepted.

> **RETIRED — the `bd-flush-guard` tracker backstop no longer exists.**
>
> `eng/ci/bd-flush-guard.sh` was removed with the daemon-era durability guards during the bd 1.1.0 (Dolt)
> migration. **Do not run it and do not rely on it** — the command fails, and the `bd export --no-auto-import`
> flag it used was also removed (`Error: unknown flag: --no-auto-import`).
>
> **There is currently no automatic check that a staged tracker reflects the Beads DB.** Verify closes
> reached committed HEAD yourself:
>
> ```bash
> git show HEAD:.beads/issues.jsonl | grep '<issue-id>'
> ```
>
> Read the **committed** blob, not `bd show` — the DB and the tracked file are separate surfaces and the
> export between them can fail without failing your command.

**Namespace depth — what it does:**
- Analyzes staged C# files for namespace depth violations
- Applies path-based depth limits (NS-001 and NS-001a):

| Path | Max Depth | Warning Level | Error Level |
|------|-----------|---------------|-------------|
| `src/` (production) | 5 | depth 5 | depth ≥6 |
| `tests/` (test code) | 7 | depth 6-7 | depth ≥8 |

- Blocks commits exceeding maximum depth for each path type
- Warns about namespaces at acceptable maximum
- Passes silently for optimal depths (≤4 for src/, ≤5 for tests/)

**See**: the header comment in `eng/hooks/pre-commit` for the per-gate detail.

### pre-push
Enforces `CHANGELOG.md [Unreleased]` update when pushing significant changes.

**What it does:**
- Computes the range of commits being pushed (`remote_sha..local_sha` or vs `origin/main` for new branches)
- Checks whether any "significant" paths changed:
  - `src/**`, `eng/ci/**`, `.github/workflows/**`
  - `Directory.Packages.props`, `Directory.Build.{props,targets}`
  - `templates/**`, `.editorconfig`, `RELEASE.md`, `CONTRIBUTING.md`
  - `management/architecture/adr-*.md`
- Excluded (never significant on their own): `/sprints/`, `/reports/`, `framework-governance.json`, `PublicAPI.*.txt`, `/.template.config/`
- If significant changes exist, requires both:
  1. `CHANGELOG.md` modified in the push range
  2. The `## [Unreleased]` block specifically differs between base and head

**Bypass options (use sparingly):**
- `SKIP_CHANGELOG_CHECK=1 git push …`
- Add `[skip changelog]` to any commit message in the push range
- `git push --no-verify`

**Why this exists:** We once accumulated four sprints of shipping changes, 36 dep bumps, 60 public-API promotions, and multiple source fixes without a single CHANGELOG entry. This hook enforces the update at push time so drift is caught before it compounds.

## Installation

Run the installer. **Do not copy hooks by hand** — see the warning below.

```bash
# From repository root
bash eng/hooks/install-hooks.sh
```

```powershell
# From repository root
.\eng\hooks\install-hooks.ps1
```

The installer does two things: it sets `core.hooksPath` to `eng/hooks`, and it copies the hooks into `.git/hooks` as a restore point.

:::warning Do not `cp` hooks into `.git/hooks` yourself

`core.hooksPath` **replaces** `.git/hooks` — git does not try one and fall back to the other. Measured, one variable, with a positive control:

```
hooksPath unset, hook in .git/hooks     -> FIRED
hooksPath set elsewhere (empty dir)     -> did NOT fire     <- no fallback
hook placed in the hooksPath dir        -> FIRED            <- control
```

So with `core.hooksPath = eng/hooks` in effect, **a hook copied into `.git/hooks` never runs.** Copying by hand leaves you believing you are protected while nothing executes.

:::

## Verification

```bash
# what git will actually execute
git config core.hooksPath          # expect: eng/hooks
bash "$(git config core.hooksPath)/pre-commit"
```

Verify against `core.hooksPath`, not against a fixed directory — the two are only the same when `hooksPath` is unset.

## Important Notes

✅ **`eng/hooks/` is version controlled and is what git executes** once the installer has set `core.hooksPath`. The file you review is the file that runs.

⚠️ **`core.hooksPath` is LOCAL git config.** It is not cloned. **A fresh clone has no `core.hooksPath` and an empty `.git/hooks`, so it runs no hooks at all** until someone runs the installer. Nothing invokes the installer automatically.

⚠️ **Do not assume CI is a backstop for a skipped hook.** Whether a given gate also runs in CI must be checked per gate — several run only from the pre-commit hook. **"CI will catch it" is a claim to verify, not a default.** Check the workflow that hosts the gate you care about, and check what it triggers on: a gate declared in a workflow that never fires on your integration path has not run.

## Defense-in-Depth Enforcement

Namespace depth is enforced at multiple layers:

| Layer | Enforcement | When | Bypass Possible? |
|-------|------------|------|------------------|
| **IDE/Editor** | Roslyn analyzers via `.editorconfig` | Real-time as you type | Yes (warnings can be ignored) |
| **Pre-Commit** | This Git hook | Before commit is created | Yes (`git commit --no-verify`) |
| **CI/CD** | Build pipeline validation | On push to remote | ❌ No (enforced for all PRs) |

This layered approach ensures violations cannot reach the main branch.

## Updating Hooks

1. Modify `eng/hooks/pre-commit` — it is the executed file, not a template
2. Test it: `bash eng/hooks/pre-commit`
3. Commit

**No reinstall step is needed for developers who already have `core.hooksPath` set** — they execute the tracked file, so your commit reaches them with the next pull. Re-run the installer only to refresh the `.git/hooks` restore point or to set `core.hooksPath` on a machine that lacks it.

## Troubleshooting

### Hook not running

```bash
# 1. Is core.hooksPath set? A fresh clone has none, and then NO hooks run.
git config core.hooksPath              # expect: eng/hooks

# 2. Is the hook present and executable AT THAT PATH?
ls -la "$(git config core.hooksPath)/pre-commit"
chmod +x "$(git config core.hooksPath)/pre-commit"
```

**Check `core.hooksPath` first.** The most common cause is not a missing file but an unset config — the hook exists, is executable, and is in a directory git is not reading.

### Hook runs but always passes

```bash
bash -x "$(git config core.hooksPath)/pre-commit" 2>&1 | less
```

A gate can also run and legitimately enforce nothing — read its output rather than its exit code. A gate that prints something like `INERT — nothing was enforced` is telling you it was evaluated against an empty set, which is not the same as passing.

### Need to bypass a hook temporarily

```bash
git commit --no-verify -m "…"
```

**Do not assume CI re-checks what you skipped.** Verify per gate; several exist only in the hook.

## Related Documentation

- Installer behaviour and the `core.hooksPath` rationale: `eng/hooks/install-hooks.sh` (header comment, carries the measurement)
