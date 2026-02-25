# Git Hooks for Excalibur.Dispatch

## Overview
This directory contains canonical versions of Git hooks used for automated governance and quality control.

## Available Hooks

### pre-commit
Enforces ADR-050 namespace depth requirements before commits are accepted.

**What it does:**
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
