# Git Hooks for Excalibur.Dispatch

## Overview
This directory contains canonical versions of Git hooks used for automated governance and quality control.

## Available Hooks

### pre-commit
Enforces ADR-050 namespace depth requirements **and** flushes the Beads tracker before commits are accepted.

**Beads tracker flush guard (bd-xlnd5e / S849 I1):**
- When `.beads/issues.jsonl` is staged (integration/close commits), the hook runs `eng/ci/bd-flush-guard.sh`
  to make the tracked tracker reflect the Beads DB **before** git snapshots it, then re-stages it.
- This closes the recurring daemon-mtime race (`bd sync --flush-only` refusing with *"JSONL is newer than
  database"* or silently writing nothing) that left fresh closes un-persisted in committed HEAD across
  S842–S848. See `.claude/rules/process/bd-flush-then-commit.md` (clause 6).
- The helper is **data-loss-safe**: it uses `bd export --no-auto-import` (never a blind `import`, which would
  revert fresh closes), guards against an on-disk `.jsonl` that is *ahead* of the DB (foreign change →
  aborts the commit LOUD, exit 2), bounded-retries transient contention, and fails LOUD (exit 1) on a
  genuine failure rather than shipping a desynced tracker. It never pipes the flush through an exit-masking
  `head`/`tail` (`no-pipe-masked-commit.md`).
- Runs **before** the C#-only early-exit so a tracker-only commit is still flushed.
- The helper is standalone and reusable: the integrator may run `bash eng/ci/bd-flush-guard.sh` directly.
  Knobs: `BD_FLUSH_ATTEMPTS` (3), `BD_FLUSH_BACKOFF` (2s), `BD_JSONL_PATH`, `BD_BIN`, `PYTHON_BIN`.

**Namespace depth (ADR-050) — what it does:**
- Analyzes staged C# files for namespace depth violations
- Applies path-based depth limits (NS-001 and NS-001a):

| Path | Max Depth | Warning Level | Error Level |
|------|-----------|---------------|-------------|
| `src/` (production) | 5 | depth 5 | depth ≥6 |
| `tests/` (test code) | 7 | depth 6-7 | depth ≥8 |

- Blocks commits exceeding maximum depth for each path type
- Warns about namespaces at acceptable maximum
- Passes silently for optimal depths (≤4 for src/, ≤5 for tests/)

**See**: `.git/hooks/README.md` for detailed documentation

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

**Why this exists:** Pre-S811 we accumulated 4 sprints (S808–S811) of shipping changes, 36 dep bumps, 60 public-API promotions, and multiple source fixes without a single CHANGELOG entry. This hook enforces the update at push time so drift is caught before it compounds.

## Installation

### Manual Installation

Copy the hook to your local `.git/hooks/` directory:

```bash
# From repository root
cp eng/hooks/pre-commit .git/hooks/pre-commit
chmod +x .git/hooks/pre-commit
```

### Automated Installation (Windows PowerShell)

Run the installation script:

```powershell
# From repository root
.\eng\hooks\install-hooks.ps1
```

### Automated Installation (Bash/Git Bash)

```bash
# From repository root
bash eng/hooks/install-hooks.sh
```

## Verification

Test the hook installation:

```bash
# Should display namespace validation output
bash .git/hooks/pre-commit
```

## Important Notes

⚠️ **Hooks in `.git/hooks/` are NOT version controlled** - Each developer must install them locally.

✅ **Canonical versions in `eng/hooks/` ARE version controlled** - Updates to hooks can be tracked and distributed.

📋 **Installation is recommended but not mandatory** - CI gates will catch violations even if pre-commit is bypassed.

## Defense-in-Depth Enforcement

ADR-050 namespace depth is enforced at multiple layers:

| Layer | Enforcement | When | Bypass Possible? |
|-------|------------|------|------------------|
| **IDE/Editor** | Roslyn analyzers via `.editorconfig` | Real-time as you type | Yes (warnings can be ignored) |
| **Pre-Commit** | This Git hook | Before commit is created | Yes (`git commit --no-verify`) |
| **CI/CD** | Build pipeline validation | On push to remote | ❌ No (enforced for all PRs) |

This layered approach ensures violations cannot reach the main branch.

## Updating Hooks

To update hooks for all developers:

1. Modify the canonical version in `eng/hooks/pre-commit`
2. Test thoroughly: `bash eng/hooks/pre-commit`
3. Commit the changes
4. Notify team to reinstall: `cp eng/hooks/pre-commit .git/hooks/pre-commit`
5. Consider updating `install-hooks.*` scripts if behavior changes

## Troubleshooting

### Hook not running
```bash
# Check if hook exists and is executable
ls -la .git/hooks/pre-commit

# If not executable:
chmod +x .git/hooks/pre-commit
```

### Hook runs but always passes
```bash
# Verify hook logic is correct
bash -x .git/hooks/pre-commit 2>&1 | less
```

### Need to bypass hook temporarily
```bash
# NOT RECOMMENDED - CI will still enforce
git commit --no-verify -m "Emergency commit"
```

## Related Documentation

- Hook Usage: `.git/hooks/README.md` (after installation)
