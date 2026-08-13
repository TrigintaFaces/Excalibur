#!/usr/bin/env bash
# committed-sha-build-gate.sh — the commit must be buildable, not merely your TREE.
#
# THE MECHANISM, stated once because every misuse of this gate comes from getting it wrong:
#   `dotnet build --no-incremental` compiles the WORKING TREE. It does NOT compile the committed SHA.
#   When a commit under-stages a coupled set, the working tree still holds the missing file, the build
#   goes GREEN, and the resulting commit does not compile for anyone who checks it out. The build is
#   measuring a tree that will not exist after the commit. `--no-incremental` does not fix this and must
#   never be mistaken for it.
#
#   Evidence: five commits in one night landed an under-staged coupled set; THREE produced a committed
#   HEAD that does not compile. Four separate seats then "verified" one of them by READING its content.
#   Presence is not compilation. It was caught only when someone built the actual SHA.
#
# THE TWO SOUND METHODS, and this script implements both:
#
#   --staged   (DEFAULT, pre-commit, instant)
#       Verify `git status` is clean for ALL inputs of every project the commit touches. Only then is a
#       working-tree build the SHA. Concretely: if a file under a touched project is modified-but-unstaged
#       or untracked, the commit is a SUBSET of the tree that was built, so any green is unspendable.
#       This runs BEFORE the commit exists, which is the only moment a gate can still block it.
#
#   --sha [REF]   (post-commit / CI, authoritative)
#       `git worktree add --detach` at the ref and build there. Nothing from your tree can leak in.
#       Builds BOTH src and test projects, never --no-build: an interface change breaks consumer mocks
#       with CS0535, and `dotnet test --no-build` against a stale test DLL masks exactly that.
#
# The failure output NAMES the file. "Committed HEAD does not compile" costs an hour of bisecting;
# "…PublicAPI.Unshipped.txt is in your tree but not in the commit" costs a minute. That is the value.
#
# Exit: 0 = commit is coherent/builds · 1 = under-staged or does not build · 2 = cannot evaluate
#
# Usage: eng/ci/committed-sha-build-gate.sh [--staged | --sha [REF]] [--self-test]

set -uo pipefail

readonly E_OK=0
readonly E_FAIL=1
readonly E_ENV=2

MODE="--staged"
REF="HEAD"
case "${1:-}" in
    --self-test) exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/committed-sha-build-gate.test.sh" ;;
    --sha)       MODE="--sha"; REF="${2:-HEAD}" ;;
    --staged|"") MODE="--staged" ;;
    *) echo "[committed-sha-build-gate] unknown argument: $1" >&2; exit "$E_ENV" ;;
esac

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || true)"
[ -n "$REPO_ROOT" ] || { echo "[committed-sha-build-gate] CANNOT EVALUATE — not in a git repo." >&2; exit "$E_ENV"; }
cd "$REPO_ROOT" || exit "$E_ENV"

# Nearest ancestor directory holding a project file — the unit whose inputs must be staged together.
owning_project_dir() {
    local d; d="$(dirname "$1")"
    while [ "$d" != "." ] && [ "$d" != "/" ]; do
        if compgen -G "$d/*.csproj" >/dev/null 2>&1 || compgen -G "$d/*.fsproj" >/dev/null 2>&1; then
            printf '%s\n' "$d"; return 0
        fi
        d="$(dirname "$d")"
    done
    return 1
}

# ── MODE 1: staged coherence ────────────────────────────────────────────────────────────────────────
if [ "$MODE" = "--staged" ]; then
    mapfile -t STAGED < <(git diff --cached --name-only --diff-filter=ACMR 2>/dev/null)
    if [ "${#STAGED[@]}" -eq 0 ]; then
        echo "[committed-sha-build-gate] nothing staged — nothing to verify."
        exit "$E_OK"
    fi

    # Projects this commit touches.
    declare -A TOUCHED=()
    for f in "${STAGED[@]}"; do
        if p="$(owning_project_dir "$f")"; then TOUCHED["$p"]=1; fi
    done
    # NOTE: an empty TOUCHED set is NOT "nothing to verify" any more. It used to exit 0 here, which
    # made every eng/, .github/, hook, workflow and gate change invisible to this gate -- none of them
    # has a .csproj ancestor. A gate landed in HEAD without its caller and this check reported PASS.
    # The project-input arm below still needs TOUCHED; the reference-coupling arm does not.
    if [ "${#TOUCHED[@]}" -eq 0 ]; then
        echo "[committed-sha-build-gate] no project-owned files staged — project-input arm skipped."
    fi

    # Anything dirty-but-uncommitted: unstaged modifications + untracked files. Either makes the commit
    # a strict subset of the tree a local build measured.
    mapfile -t DIRTY < <( { git diff --name-only --diff-filter=ACMR 2>/dev/null
                            git ls-files --others --exclude-standard 2>/dev/null; } | LC_ALL=C sort -u)

    # A file that is BOTH staged and further modified is not an under-stage of a coupled set; it is a
    # partial stage of one file. Report it — the committed version still differs from the one built.
    found=0
    for d in "${DIRTY[@]}"; do
        [ -n "$d" ] || continue
        if p="$(owning_project_dir "$d")"; then
            if [ -n "${TOUCHED[$p]:-}" ]; then
                if [ "$found" -eq 0 ]; then
                    echo "[committed-sha-build-gate] UNDER-STAGED COUPLED SET — the commit is a SUBSET of your tree." >&2
                    echo "  Your local build compiled the working tree. These files are in that tree and NOT in the commit," >&2
                    echo "  so the committed SHA is not what you built:" >&2
                fi
                echo "    MISSING FROM COMMIT: $d   (project: $p)" >&2
                found=1
            fi
        fi
    done

    # ── REFERENCE COUPLING ─────────────────────────────────────────────────────────────────────────
    # The arm above can only see a coupled set INSIDE one project. Two of the three under-stages that
    # motivated this had no project in common at all:
    #   a gate staged while its only caller (a .github composite action) stayed dirty;
    #   a fix staged in one provider while its siblings stayed dirty.
    #
    # This arm asks a different, decidable question: does either side NAME the other? A caller names
    # the script it runs; a workflow names the action it uses; a doc names the file it documents. If a
    # dirty file mentions a staged file by name, or the reverse, the commit is a subset of a set that
    # references itself -- and the committed tree will not be coherent.
    #
    # Matching is on BASENAME, so it survives a path written relative to a different directory (the
    # caller that broke HEAD referenced "eng/ci/assert-compiled-not-skipped.sh" from .github/actions).
    # Only files git tracks as text are read; binaries and absent files are skipped.
    refs=0
    for st in "${STAGED[@]}"; do
        [ -n "$st" ] || continue
        st_base="$(basename "$st")"
        # A bare or very short name would match half the tree; require enough signal to be meaningful.
        [ "${#st_base}" -ge 6 ] || continue
        for d in "${DIRTY[@]}"; do
            [ -n "$d" ] || continue
            [ "$d" = "$st" ] && continue
            [ -f "$d" ] || continue
            # Tracker DATA is not a caller. Bead descriptions quote filenames constantly, so without
            # this every commit touching a gate would be flagged by the issue log that merely discusses
            # it. Measured on the real tree: staging one gate produced 5 candidate flags and ALL FIVE
            # were .beads records. Prose is deliberately NOT excluded -- a skill document that invokes a
            # gate IS a caller, which is precisely the coupling this arm exists to catch.
            case "$d" in
                .beads/*|.beads-backup-*/*|*/.beads/*) continue ;;
            esac
            if grep -Fqs -- "$st_base" "$d" 2>/dev/null; then
                if [ "$refs" -eq 0 ]; then
                    echo "[committed-sha-build-gate] COUPLED SET SPLIT — a file OUTSIDE this commit references a file INSIDE it." >&2
                    echo "  The committed tree will contain one half of a pair that refers to the other:" >&2
                fi
                echo "    NOT COMMITTED: $d   references the staged   $st" >&2
                refs=1
            fi
        done
    done

    # ── PROVIDER SIBLINGS (ADVISORY — NAMES, NEVER BLOCKS) ─────────────────────────────────────────
    # Provider implementations never reference each other: SaveSagaRequest.cs under Saga.SqlServer has
    # no textual mention of its Postgres or Oracle twin, so the reference arm above cannot see a set
    # split across them. Siblinghood is structural instead of semantic: two files are siblings when
    # their paths are identical EXCEPT the provider segment of an Excalibur.<Family>.<Provider>
    # directory. Nothing is invented per subsystem and there is no list to maintain.
    #
    # THIS MUST NOT BLOCK, and the counterexample is a real ruling: SQL Server MUST pin a binary
    # collation while PostgreSQL MUST NOT, because PG's default is already deterministic and pinning
    # can defeat index usage. That is a correct, deliberate, SqlServer-only change — a blocking parity
    # arm would reject it for being right. Divergence between providers is frequently intentional
    # (Oracle stores '' as NULL; ODP.NET binds positionally; only the SQL Server family defaults to
    # case-insensitive equality).
    #
    # So the question is NOT "why didn't you change the siblings?" It is "here are the siblings — did
    # you consider them?" You cannot fail to NOTICE, and you are never required to comply.
    sibs=0
    for st in "${STAGED[@]}"; do
        [ -n "$st" ] || continue
        # Split the path at the first Excalibur.<Family>.<Provider> directory component.
        if [[ "$st" =~ ^(.*/)?(Excalibur\.[A-Za-z0-9]+)\.([A-Za-z0-9]+)(/.*)$ ]]; then
            st_prefix="${BASH_REMATCH[1]}"
            st_family="${BASH_REMATCH[2]}"
            st_rest="${BASH_REMATCH[4]}"
            for d in "${DIRTY[@]}"; do
                [ -n "$d" ] || continue
                [ "$d" = "$st" ] && continue
                if [[ "$d" =~ ^(.*/)?(Excalibur\.[A-Za-z0-9]+)\.([A-Za-z0-9]+)(/.*)$ ]]; then
                    # Same family, same relative path inside the project, different provider.
                    if [ "${BASH_REMATCH[1]}" = "$st_prefix" ] \
                       && [ "${BASH_REMATCH[2]}" = "$st_family" ] \
                       && [ "${BASH_REMATCH[4]}" = "$st_rest" ]; then
                        if [ "$sibs" -eq 0 ]; then
                            echo "[committed-sha-build-gate] PROVIDER SIBLINGS NOT IN THIS COMMIT (advisory — not blocking):" >&2
                        fi
                        echo "    sibling of a staged file, still dirty: $d" >&2
                        sibs=1
                    fi
                fi
            done
        fi
    done
    if [ "$sibs" -ne 0 ]; then
        echo "  Provider divergence is often CORRECT, so this does not block. Confirm it is deliberate," >&2
        echo "  or stage them with the rest of the set." >&2
    fi

    if [ "$found" -ne 0 ] || [ "$refs" -ne 0 ]; then
        echo "  Fix: stage them (git add <file>), or move them out of this commit deliberately." >&2
        echo "  Note: --no-incremental does NOT address this. It rebuilds the same tree." >&2
        exit "$E_FAIL"
    fi

    echo "[committed-sha-build-gate] PASS — ${#TOUCHED[@]} touched project(s), no unstaged inputs; the commit matches the built tree."
    exit "$E_OK"
fi

# ── MODE 2: authoritative build of the actual SHA ───────────────────────────────────────────────────
SHA="$(git rev-parse --verify "$REF" 2>/dev/null || true)"
[ -n "$SHA" ] || { echo "[committed-sha-build-gate] CANNOT EVALUATE — cannot resolve ref: $REF" >&2; exit "$E_ENV"; }

# Resolve a muxer that can satisfy this repository's global.json pin, preferring a repo-local install.
# The probe worktree is a clean checkout, and the local SDK directory is untracked — so a bare `dotnet`
# there resolves no SDK at all and EVERY project "fails". That is an environment fault, not a property
# of the code, and it must never be counted as one.
REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || echo .)"
DOTNET=""
for candidate in \
    "$REPO_ROOT/.dotnet/dotnet.exe" "$REPO_ROOT/.dotnet/dotnet" \
    "${DOTNET_ROOT:-}/dotnet.exe" "${DOTNET_ROOT:-}/dotnet"; do
    if [ -x "$candidate" ]; then DOTNET="$candidate"; break; fi
done
[ -n "$DOTNET" ] || DOTNET="$(command -v dotnet 2>/dev/null || true)"
[ -n "$DOTNET" ] || { echo "[committed-sha-build-gate] CANNOT EVALUATE — no dotnet muxer found." >&2; exit "$E_ENV"; }

mapfile -t CHANGED < <(git diff-tree --no-commit-id --name-only -r "$SHA" 2>/dev/null)
declare -A PROJECTS=()
for f in "${CHANGED[@]}"; do
    if p="$(owning_project_dir "$f")"; then
        for proj in "$p"/*.csproj "$p"/*.fsproj; do
            [ -f "$proj" ] && PROJECTS["$proj"]=1
        done
    fi
done

if [ "${#PROJECTS[@]}" -eq 0 ]; then
    echo "[committed-sha-build-gate] $SHA touches no project — nothing to build."
    exit "$E_OK"
fi

WT="$(mktemp -d)/wt"
cleanup() { git worktree remove --force "$WT" >/dev/null 2>&1; rm -rf "$(dirname "$WT")" 2>/dev/null; }
trap cleanup EXIT

# `env -u` the per-invocation git variables before creating the worktree.
#
# A git hook exports GIT_INDEX_FILE (and often GIT_DIR / GIT_WORK_TREE) pointing at the CURRENT
# operation. A child `git worktree add` inherits them, tries to use that index for a different working
# tree, and fails. Measured on this repo, same SHA, same command, one variable:
#
#     GIT_INDEX_FILE set    ->  "git worktree add failed"   exit 2 (REFUSE)
#     GIT_INDEX_FILE unset  ->  worktree created, build ran, real verdict
#
# So this mode was unusable from inside a hook, and the failure presented as REFUSE — honest, but it
# meant the gate could never be wired at the one moment it could still block a commit. Unsetting them
# is safe here because the worktree is addressed by SHA and path, not by the caller's index.
if ! env -u GIT_INDEX_FILE -u GIT_WORK_TREE git worktree add --detach "$WT" "$SHA" >/dev/null 2>&1; then
    echo "[committed-sha-build-gate] CANNOT EVALUATE — git worktree add failed for $SHA." >&2
    exit "$E_ENV"
fi

fails=0
built=0
for proj in "${!PROJECTS[@]}"; do
    # Build in the WORKTREE copy — never the local path, or we are back to compiling our own tree.
    # BuildExamplesAndTests is required or test projects silently resolve to nothing to build.
    #
    # -nodeReuse:false is load-bearing, not tidiness. A reused MSBuild node persists across
    # invocations and can carry a global property with it, so a node started WITHOUT
    # BuildExamplesAndTests can serve this build and re-apply the Compile-Remove skip even though
    # we passed the flag — the gate would then hit its own REFUSE arm intermittently, or worse,
    # the skip set would appear to move between runs for no reason in the diff. A fresh node per
    # invocation makes the property set we pass the only property set in play.
    if [ -f "$WT/$proj" ]; then
        built=$((built + 1))
        (cd "$WT" && "$DOTNET" build "$proj" -c Release --no-incremental -nodeReuse:false -p:BuildExamplesAndTests=true -v q) >/tmp/csbg_build.log 2>&1
        build_rc=$?

        # The muxer failing to satisfy global.json is an ENVIRONMENT fault, not a compile error, and it
        # presents on every project at once. Reporting it as "does not compile" would be a FAIL the gate
        # did not earn, and it reads identically to a genuine break — so REFUSE instead, by the same
        # rule as the skipped-compile arm above.
        if grep -qE "A compatible .NET SDK was not found|The command could not be loaded" /tmp/csbg_build.log 2>/dev/null; then
            echo "[committed-sha-build-gate] CANNOT EVALUATE — $DOTNET cannot resolve the pinned SDK." >&2
            grep -E "Requested SDK version|global.json file|Install the" /tmp/csbg_build.log 2>/dev/null | head -3 | sed 's/^/      /' >&2
            echo "      Provision the pinned SDK (the repository's build script installs it locally)." >&2
            exit "$E_ENV"
        fi

        # A "successful" build of a project whose sources were REMOVED is a false green, and this repo
        # produces exactly that: Directory.Build.targets sets SkipProjectBuild for anything under
        # \tests\, \examples\ or \benchmarks\ and then does `<Compile Remove="**\*.cs" />`, so the
        # project compiles ZERO files and exits 0. We pass -p:BuildExamplesAndTests=true to prevent it,
        # but if that flag ever stops taking effect this gate would certify a project it never compiled
        # — the precise defect it exists to catch, committed by the gate itself. So: detect the skip and
        # REFUSE. A gate must be unable to report a PASS it did not earn.
        if grep -q "Skipping compile for" /tmp/csbg_build.log 2>/dev/null; then
            echo "[committed-sha-build-gate] CANNOT EVALUATE — $proj was SKIPPED, not compiled." >&2
            echo "      Directory.Build.targets removed its Compile items; the exit 0 means nothing." >&2
            echo "      (-p:BuildExamplesAndTests=true was passed and did not take effect.)" >&2
            exit "$E_ENV"
        fi

        if [ "$build_rc" -ne 0 ]; then
            echo "[committed-sha-build-gate] COMMITTED SHA DOES NOT COMPILE: $proj" >&2
            grep -E "error [A-Z]+[0-9]+" /tmp/csbg_build.log 2>/dev/null | head -5 | sed 's/^/      /' >&2
            # CS0535 = a consumer mock no longer implements a changed interface; name that class outright.
            if grep -q "CS0535" /tmp/csbg_build.log 2>/dev/null; then
                echo "      ^ CS0535: an implementor does not implement a changed interface member." >&2
                echo "        A 'dotnet test --no-build' would have MASKED this against a stale test DLL." >&2
            fi
            fails=$((fails + 1))
        fi
    fi
done

if [ "$built" -eq 0 ]; then
    echo "[committed-sha-build-gate] CANNOT EVALUATE — resolved 0 buildable projects in the worktree." >&2
    exit "$E_ENV"
fi

if [ "$fails" -ne 0 ]; then
    echo "[committed-sha-build-gate] FAIL — $fails of $built project(s) do not compile at $SHA." >&2
    exit "$E_FAIL"
fi

echo "[committed-sha-build-gate] PASS — $built project(s) compile at the COMMITTED sha $SHA (clean worktree)."
exit "$E_OK"
