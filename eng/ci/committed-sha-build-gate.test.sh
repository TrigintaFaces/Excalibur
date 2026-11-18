#!/usr/bin/env bash
# committed-sha-build-gate.test.sh — non-vacuity proof. This is the CRUX of the bead, not a formality.
#
# "A gate that fails everything is as useless as one that fails nothing, and the liveness arm is the one
# that gets skipped." Both arms are asserted, and the SAFETY arm reproduces the real defect shape
# (c85dfbece): the impl is staged, the PublicAPI baseline entry is in the tree but withheld from the
# commit, so the local build is green and the committed SHA is not what was built.
#
# Hermetic: every arm runs in a throwaway git repo. Nothing touches this repository's index or worktree.
#
set -uo pipefail

unset GIT_INDEX_FILE GIT_DIR GIT_WORK_TREE GIT_OBJECT_DIRECTORY GIT_COMMON_DIR
GATE="${COMMITTED_SHA_GUARD:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/committed-sha-build-gate.sh}"
[ -x "$GATE" ] || chmod +x "$GATE" 2>/dev/null

pass=0
fail=0

report() { # name expected actual
    if [ "$3" -eq "$2" ]; then echo "  ok   $1 (exit $3)"; pass=$((pass + 1))
    else echo "  FAIL $1 — expected exit $2, got $3"; fail=$((fail + 1)); fi
}

remove_fixture() {
    local resolved temporary_root
    temporary_root="$(realpath -e -- "${TMPDIR:-/tmp}")" || exit 99
    resolved="$(realpath -e -- "${1:?fixture path required}")" || exit 99
    case "$resolved" in
        "$temporary_root"/tmp.*) rm -rf -- "$resolved" ;;
        *) echo "FATAL: refusing fixture cleanup outside the temporary root: $resolved" >&2; exit 99 ;;
    esac
}

new_repo() {
    local d; d="$(mktemp -d)"
    # `git -C "" <cmd>` does NOT fail -- it silently runs in the CURRENT directory, which for this
    # file is the real repository. So an empty $d here would stage the developer's own tree.
    [ -n "$d" ] && [ -d "$d" ] || { echo "  FATAL: mktemp -d produced no directory. Aborting." >&2; exit 99; }
    git -C "${d:?path is empty -- an empty -C runs in the CURRENT directory}" init -q 2>/dev/null
    git -C "${d:?path is empty -- an empty -C runs in the CURRENT directory}" config user.email t@t.t; git -C "${d:?path is empty -- an empty -C runs in the CURRENT directory}" config user.name t
    git -C "${d:?path is empty -- an empty -C runs in the CURRENT directory}" config commit.gpgsign false
    mkdir -p "$d/src/Widget"
    cat > "$d/src/Widget/Widget.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>
EOF
    echo "public class Seed {}" > "$d/src/Widget/Seed.cs"
    echo "#nullable enable" > "$d/src/Widget/PublicAPI.Unshipped.txt"
    git -C "${d:?path is empty -- an empty -C runs in the CURRENT directory}" add -A >/dev/null 2>&1
    git -C "${d:?path is empty -- an empty -C runs in the CURRENT directory}" commit -qm seed >/dev/null 2>&1
    printf '%s' "$d"
}

echo "[committed-sha-build-gate.test] running..."

# ── SAFETY: the c85dfbece shape — impl staged, baseline entry withheld ───────────────────────────────
R="$(new_repo)"
echo "public class Widget { public void New() {} }" > "$R/src/Widget/Widget.cs"
echo "Widget.New() -> void" >> "$R/src/Widget/PublicAPI.Unshipped.txt"   # in the TREE...
git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" add src/Widget/Widget.cs >/dev/null 2>&1                      # ...but NOT in the commit
( cd "$R" && bash "$GATE" --staged ) >"$R/out.txt" 2>&1; rc=$?
report "SAFETY: under-staged coupled set (impl staged, baseline withheld) is REJECTED" 1 "$rc"
if grep -q "PublicAPI.Unshipped.txt" "$R/out.txt" 2>/dev/null; then
    echo "  ok   SAFETY: failure output NAMES the missing file"; pass=$((pass + 1))
else
    echo "  FAIL SAFETY: failure output does NOT name the missing file (diagnosis speed is the point)"; fail=$((fail + 1))
fi
remove_fixture "$R"

# ── SAFETY: an UNTRACKED new file in a touched project is equally invisible to the commit ────────────
R="$(new_repo)"
echo "public class A { }" > "$R/src/Widget/A.cs"
echo "public class B { }" > "$R/src/Widget/B.cs"     # never `git add`ed at all
git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" add src/Widget/A.cs >/dev/null 2>&1
( cd "$R" && bash "$GATE" --staged ) >/dev/null 2>&1; rc=$?
report "SAFETY: untracked sibling in a touched project is REJECTED" 1 "$rc"
remove_fixture "$R"

# ── LIVENESS: the correctly-staged complete commit MUST pass (the arm that gets skipped) ─────────────
R="$(new_repo)"
echo "public class Widget { public void New() {} }" > "$R/src/Widget/Widget.cs"
echo "Widget.New() -> void" >> "$R/src/Widget/PublicAPI.Unshipped.txt"
git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" add src/Widget/Widget.cs src/Widget/PublicAPI.Unshipped.txt >/dev/null 2>&1
( cd "$R" && bash "$GATE" --staged ) >/dev/null 2>&1; rc=$?
report "LIVENESS: complete, correctly-staged commit is ACCEPTED" 0 "$rc"
remove_fixture "$R"

# ── LIVENESS: dirt in an UNRELATED project must not block an unrelated commit ────────────────────────
# Without this the gate becomes "you may never commit with a dirty tree", which is unusable in a
# multi-worker repo and would be switched off within a day.
R="$(new_repo)"
mkdir -p "$R/src/Other"
cat > "$R/src/Other/Other.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>
EOF
echo "public class O {}" > "$R/src/Other/O.cs"
git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" add -A >/dev/null 2>&1; git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" commit -qm other >/dev/null 2>&1
echo "public class Widget {}" > "$R/src/Widget/Widget.cs"
git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" add src/Widget/Widget.cs >/dev/null 2>&1
echo "// unrelated dirt" >> "$R/src/Other/O.cs"          # dirty, but a DIFFERENT project
( cd "$R" && bash "$GATE" --staged ) >/dev/null 2>&1; rc=$?
report "LIVENESS: dirt in an UNRELATED project does not block the commit" 0 "$rc"
remove_fixture "$R"

# ── LIVENESS: an empty index is not a failure ───────────────────────────────────────────────────────
R="$(new_repo)"
( cd "$R" && bash "$GATE" --staged ) >/dev/null 2>&1; rc=$?
report "LIVENESS: nothing staged is ACCEPTED" 0 "$rc"
remove_fixture "$R"

# ── ENV: an unresolvable ref must REFUSE, never pass ────────────────────────────────────────────────
R="$(new_repo)"
( cd "$R" && "$GATE" --sha deadbeefdeadbeef ) >/dev/null 2>&1; rc=$?
report "SAFETY: unresolvable ref REFUSES (exit 2, not a free pass)" 2 "$rc"
remove_fixture "$R"

# ── SAFETY: a coupled set with NO project in common — the shape that put a gate into HEAD ───────────
# The gate landed and its only caller, a .github composite action, stayed dirty. Neither file has a
# .csproj ancestor, so the project-input arm above cannot see this at all: the pre-change gate printed
# "no project-owned files staged — nothing to verify" and exited 0.
R="$(new_repo)"
mkdir -p "$R/eng/ci" "$R/.github/actions/x"
echo "echo gate" > "$R/eng/ci/some-verifier-gate.sh"
echo "run: bash eng/ci/some-verifier-gate.sh build.log" > "$R/.github/actions/x/action.yml"
git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" add -A >/dev/null 2>&1; git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" commit -qm wired >/dev/null 2>&1
echo "echo gate v2" >> "$R/eng/ci/some-verifier-gate.sh"
echo "run: bash eng/ci/some-verifier-gate.sh other.log" >> "$R/.github/actions/x/action.yml"
git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" add eng/ci/some-verifier-gate.sh >/dev/null 2>&1   # caller left dirty
( cd "$R" && bash "$GATE" --staged ) >"$R/out2.txt" 2>&1; rc=$?
report "SAFETY: coupled set split across NON-project files is REJECTED" 1 "$rc"
if grep -q "action.yml" "$R/out2.txt" 2>/dev/null; then
    echo "  ok   SAFETY: failure output NAMES the dirty file that references the staged one"; pass=$((pass + 1))
else
    echo "  FAIL SAFETY: failure output does not name the referencing file"; fail=$((fail + 1))
fi
remove_fixture "$R"

# ── LIVENESS: tracker DATA that merely mentions a filename must NOT block a commit ──────────────────
# Issue records quote filenames constantly. Measured on the real tree, staging one gate produced five
# candidate flags and every one of them was a .beads record. If those blocked, the gate would fire on
# almost every commit and be switched off — a safety arm that costs its own enforcement.
R="$(new_repo)"
mkdir -p "$R/eng/ci" "$R/.beads"
echo "echo gate" > "$R/eng/ci/some-verifier-gate.sh"
git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" add -A >/dev/null 2>&1; git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" commit -qm base >/dev/null 2>&1
echo "echo gate v2" >> "$R/eng/ci/some-verifier-gate.sh"
echo '{"id":"x","description":"see eng/ci/some-verifier-gate.sh for details"}' > "$R/.beads/issues.jsonl"
git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" add eng/ci/some-verifier-gate.sh >/dev/null 2>&1
( cd "$R" && bash "$GATE" --staged ) >"$R/out3.txt" 2>&1; rc=$?
report "LIVENESS: tracker data mentioning a staged file does NOT block" 0 "$rc"
remove_fixture "$R"

# ── PROVIDER SIBLINGS: advisory arm ─────────────────────────────────────────────────────────────────
# Provider twins never reference each other, so the reference arm cannot see a set split across them.
# These bind the structural signal AND the fact that it must not block.
sib_repo() {
    local d; d="$(mktemp -d)"
    git -C "${d:?path is empty -- an empty -C runs in the CURRENT directory}" init -q 2>/dev/null
    git -C "${d:?path is empty -- an empty -C runs in the CURRENT directory}" config user.email t@t.t; git -C "${d:?path is empty -- an empty -C runs in the CURRENT directory}" config user.name t
    git -C "${d:?path is empty -- an empty -C runs in the CURRENT directory}" config commit.gpgsign false
    mkdir -p "$d/src/Excalibur.Fam.SqlServer/Requests" "$d/src/Excalibur.Fam.Postgres/Requests"
    echo "a" > "$d/src/Excalibur.Fam.SqlServer/Requests/Save.cs"
    echo "a" > "$d/src/Excalibur.Fam.Postgres/Requests/Save.cs"
    git -C "${d:?path is empty -- an empty -C runs in the CURRENT directory}" add -A >/dev/null 2>&1; git -C "${d:?path is empty -- an empty -C runs in the CURRENT directory}" commit -qm seed >/dev/null 2>&1
    printf '%s' "$d"
}

R="$(sib_repo)"
echo "b" > "$R/src/Excalibur.Fam.SqlServer/Requests/Save.cs"
echo "b" > "$R/src/Excalibur.Fam.Postgres/Requests/Save.cs"
git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" add src/Excalibur.Fam.SqlServer/Requests/Save.cs >/dev/null 2>&1   # twin left dirty
( cd "$R" && bash "$GATE" --staged ) >"$R/sib1.txt" 2>&1; rc=$?
if grep -q "Excalibur.Fam.Postgres/Requests/Save.cs" "$R/sib1.txt" 2>/dev/null; then
    echo "  ok   SAFETY: a dirty provider sibling of a staged file is NAMED"; pass=$((pass + 1))
else
    echo "  FAIL SAFETY: dirty provider sibling was not named"; fail=$((fail + 1))
fi
# The ruling is explicit that this must NOT block: a binary collation is REQUIRED on SQL Server and
# WRONG on PostgreSQL, so a blocking parity arm would reject a correct provider-specific commit.
report "ADVISORY: naming a sibling does NOT block the commit" 0 "$rc"
remove_fixture "$R"

R="$(sib_repo)"
echo "b" > "$R/src/Excalibur.Fam.SqlServer/Requests/Save.cs"
echo "b" > "$R/src/Excalibur.Fam.Postgres/Requests/Save.cs"
git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" add -A >/dev/null 2>&1                                             # both staged
( cd "$R" && bash "$GATE" --staged ) >"$R/sib2.txt" 2>&1
if grep -q "PROVIDER SIBLINGS" "$R/sib2.txt" 2>/dev/null; then
    echo "  FAIL LIVENESS: siblings all staged but the arm still fired"; fail=$((fail + 1))
else
    echo "  ok   LIVENESS: all siblings staged -> arm is silent"; pass=$((pass + 1))
fi
remove_fixture "$R"

R="$(sib_repo)"
mkdir -p "$R/src/Excalibur.Solo.OnlyOne/Requests"
echo "x" > "$R/src/Excalibur.Solo.OnlyOne/Requests/A.cs"
git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" add -A >/dev/null 2>&1; git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" commit -qm solo >/dev/null 2>&1
echo "y" > "$R/src/Excalibur.Solo.OnlyOne/Requests/A.cs"
git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" add -A >/dev/null 2>&1
( cd "$R" && bash "$GATE" --staged ) >"$R/sib3.txt" 2>&1
if grep -q "PROVIDER SIBLINGS" "$R/sib3.txt" 2>/dev/null; then
    echo "  FAIL LIVENESS: single-provider family produced a sibling warning"; fail=$((fail + 1))
else
    echo "  ok   LIVENESS: single-provider family -> arm is silent"; pass=$((pass + 1))
fi
remove_fixture "$R"

# ── SHARED BUILD INPUT: a root props change maps to the SOLUTION, not to nothing ─────────────────────
#
# The defect: a shared build input is owned by no project, contributed NOTHING to the build set, and the
# gate reported "PASS - 1 project(s) compile" for a commit that made the root props XML-invalid.
#
# SCOPE OF THESE TWO ARMS, STATED SO IT IS NOT READ WIDER: they assert the MAPPING -- that a shared
# input reaches the solution and that an ordinary source file does not. They do NOT assert that MSBuild
# rejects the bad props; that half was proven against the real repository, where the gate now FAILS the
# actual commit it once passed (`--sha 277aba7b1b` -> "COMMITTED SHA DOES NOT COMPILE: Excalibur.sln",
# exit 1). A hermetic arm cannot cheaply prove it: a throwaway solution needs a cold restore, and a
# hand-written .sln that resolves zero projects exits 0 and makes BOTH arms look green.
props_repo() { # $1 = comment body for the root props
    local d; d="$(new_repo)"
    printf '<Project><!-- %s --></Project>\n' "$1" > "$d/Directory.Build.props"
    git -C "${d:?path is empty -- an empty -C runs in the CURRENT directory}" add -A >/dev/null 2>&1; git -C "${d:?path is empty -- an empty -C runs in the CURRENT directory}" commit -qm props >/dev/null 2>&1
    printf '%s' "$d"
}

R="$(props_repo 'a comment')"
( cd "$R" && bash "$GATE" --sha HEAD ) >"$R/si.txt" 2>&1; rc=$?
# This fixture has no solution file, which is the harder case: with nothing to map the shared input to,
# the gate must REFUSE rather than fall through to a pass. Exit 0 here would be the shipped defect --
# a change every project reads, judged by nothing.
if [ "$rc" -eq 0 ]; then
    echo "  FAIL SAFETY: a root props change PASSED with no solution to gate it (the shipped defect)"; fail=$((fail + 1))
else
    echo "  ok   SAFETY: a root props change is never silently passed (exit $rc)"; pass=$((pass + 1))
fi
if grep -q "touches no project the solution builds" "$R/si.txt" 2>/dev/null; then
    echo "  FAIL SAFETY: props-only commit still took the nothing-to-build exit"; fail=$((fail + 1))
else
    echo "  ok   SAFETY: props-only commit does NOT take the nothing-to-build exit"; pass=$((pass + 1))
fi
remove_fixture "$R"

# LIVENESS: the mapping must not fire for everything, or it is just "always build the solution".
R="$(new_repo)"
echo "public class Later {}" > "$R/src/Widget/Later.cs"
git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" add -A >/dev/null 2>&1; git -C "${R:?path is empty -- an empty -C runs in the CURRENT directory}" commit -qm src >/dev/null 2>&1
( cd "$R" && bash "$GATE" --sha HEAD ) >"$R/si2.txt" 2>&1
if grep -q "shared build input owned by no project" "$R/si2.txt" 2>/dev/null; then
    echo "  FAIL LIVENESS: an ordinary source change was treated as a shared build input"; fail=$((fail + 1))
else
    echo "  ok   LIVENESS: an ordinary source change does NOT map to the solution"; pass=$((pass + 1))
fi
remove_fixture "$R"

# Resolve a reference rather than infer ownership from a unique basename.
reference_case() {
    local label="$1" expected="$2" staged="$3" caller="$4" reference="$5" shadow="${6:-}" fixture rc
    fixture="$(new_repo)"
    mkdir -p "$fixture/$(dirname "$staged")" "$fixture/$(dirname "$caller")"
    printf 'original\n' > "$fixture/$staged"
    printf 'original\n' > "$fixture/$caller"
    if [ -n "$shadow" ]; then
        mkdir -p "$fixture/$(dirname "$shadow")"
        printf 'different target\n' > "$fixture/$shadow"
    fi
    git -C "${fixture:?}" add -A >/dev/null 2>&1
    git -C "${fixture:?}" commit -qm references >/dev/null 2>&1
    printf 'changed\n' >> "$fixture/$staged"
    printf '%s\n' "$reference" >> "$fixture/$caller"
    git -C "${fixture:?}" add "$staged" >/dev/null 2>&1
    (cd "$fixture" && bash "$GATE" --staged) >"$fixture/output.txt" 2>&1; rc=$?
    report "$label" "$expected" "$rc"
    if [ "$expected" -eq 1 ] && ! grep -q 'COUPLED SET SPLIT' "$fixture/output.txt"; then
        echo "  FAIL $label: refusal did not identify reference coupling"; fail=$((fail + 1))
    fi
    remove_fixture "$fixture"
}

reference_case "LIVENESS: unique CHANGELOG mention in nested prose is not root coupling" 0 CHANGELOG.md docs/guide.md 'See CHANGELOG.md for details.'
reference_case "SAFETY: relative root CHANGELOG reference still couples" 1 CHANGELOG.md docs/guide.md '[changes](../CHANGELOG.md)'
reference_case "SAFETY: root document resolves bare CHANGELOG beside itself" 1 CHANGELOG.md NOTES.md 'See CHANGELOG.md for details.'
reference_case "LIVENESS: repeated README basename resolves to another directory" 0 docs/a/README.md docs/b/guide.md '[local](README.md)' docs/b/README.md
reference_case "LIVENESS: full path tail from docs-site is not docs coupling" 0 docs/x/README.md NOTES.md '[site](docs-site/docs/x/README.md)' docs-site/docs/x/README.md
reference_case "LIVENESS: filename suffix is not an exact reference" 0 eng/ci/verify-gate.sh NOTES.md 'eng/ci/verify-gate.sh.bak'
reference_case "LIVENESS: external URL is not a repository reference" 0 eng/ci/verify-gate.sh NOTES.md 'https://example.invalid/eng/ci/verify-gate.sh'
reference_case "LIVENESS: existing local slash target takes precedence over root" 0 eng/ci/verify-gate.sh .github/actions/x/action.yml 'run: bash eng/ci/verify-gate.sh' .github/actions/x/eng/ci/verify-gate.sh
reference_case "SAFETY: normalized relative path still couples" 1 eng/ci/verify-gate.sh docs/guide.md '[gate](../eng/./ci/verify-gate.sh#details)'
reference_case "LIVENESS: relative escape above repository root is not coupling" 0 eng/ci/verify-gate.sh docs/guide.md '[outside](../../eng/ci/verify-gate.sh)'
reference_case "LIVENESS: absolute paths are outside the reference grammar" 0 eng/ci/verify-gate.sh NOTES.md '/eng/ci/verify-gate.sh'


# SDK SELECTION: hermetic muxers, never a real restore or compiler. Each host logs
# its real cwd, the target global.json pin and operation. The isolated PATH holds
# absolute utility wrappers plus ONE fake dotnet; no machine SDK can rescue a case.
sdk_case() { # label, local SDK, PATH SDK, target pin, dirty caller pin, expected exit, build host, optional temp fault
    local label="$1" local_sdk="$2" path_sdk="$3" target_pin="$4" caller_pin="$5" expected="$6" expected_host="$7"
    local temp_failure="${8:-false}" real_git
    local fixture helper executable rc bad_context builds expected_calls path_probes seen_worktree retained_worktree
    fixture="$(new_repo)"
    fixture="$(cd -P -- "$fixture" && pwd)" || return 99
    printf '{"sdk":{"version":"%s","rollForward":"disable","allowPrerelease":false}}\n' "$target_pin" > "$fixture/global.json"
    git -C "${fixture:?}" add global.json >/dev/null 2>&1
    git -C "${fixture:?}" commit -qm 'target SDK policy' >/dev/null 2>&1
    # The commit being gated changes a normal source project. Its parent already
    # contains the pin, avoiding the unrelated solution/shared-input mapping arm.
    printf 'public class Changed {}\n' > "$fixture/src/Widget/Changed.cs"
    git -C "${fixture:?}" add src/Widget/Changed.cs >/dev/null 2>&1
    git -C "${fixture:?}" commit -qm 'source change under target SDK' >/dev/null 2>&1
    printf '{"sdk":{"version":"%s","rollForward":"disable","allowPrerelease":false}}\n' "$caller_pin" > "$fixture/global.json"
    mkdir -p "$fixture/.dotnet" "$fixture/sdk-bin"

    # Wrappers preserve the real utility's location (not a copy/symlink with DLL
    # lookup side effects on Windows). The child PATH contains no host dotnet.
    for helper in bash env git grep sort dirname basename ls head mktemp rm timeout sed cat realpath; do
        executable="$(command -v "$helper")"
        if [ -z "$executable" ]; then
            echo "  FAIL $label: missing fixture utility $helper"; fail=$((fail + 1))
            remove_fixture "$fixture"; return
        fi
        printf '#!/bin/bash\nexec %q "$@"\n' "$executable" > "$fixture/sdk-bin/$helper"
        chmod +x "$fixture/sdk-bin/$helper"
    done
    # Normalize Windows paths just as the production gate does when available.
    if executable="$(command -v cygpath 2>/dev/null)"; then
        printf '#!/bin/bash\nexec %q "$@"\n' "$executable" > "$fixture/sdk-bin/cygpath"
        chmod +x "$fixture/sdk-bin/cygpath"
    fi

    cat > "$fixture/fake-muxer" <<'SDK_HOST'
#!/usr/bin/env bash
set -uo pipefail
host="$(dirname "$0")"
installed="$(cat "$host/installed-sdk")"
pin="$(sed -n 's/.*"version":"\([^"]*\)".*/\1/p' global.json)"
printf '%s|%s|%s|%s\n' "$host" "$PWD" "$pin" "$1" >> "$SDK_CALL_LOG"
if [ "$pin" != "$installed" ]; then
    echo 'A compatible .NET SDK was not found.' >&2
    exit 145
fi
case "$1" in
    --version) printf '%s\n' "$installed" ;;
    build) printf 'Build succeeded.\n' ;;
    *) exit 91 ;;
esac
SDK_HOST
    cp "$fixture/fake-muxer" "$fixture/.dotnet/dotnet"
    cp "$fixture/fake-muxer" "$fixture/sdk-bin/dotnet"
    chmod +x "$fixture/.dotnet/dotnet" "$fixture/sdk-bin/dotnet"
    printf '%s\n' "$local_sdk" > "$fixture/.dotnet/installed-sdk"
    printf '%s\n' "$path_sdk" > "$fixture/sdk-bin/installed-sdk"
    : > "$fixture/sdk-calls.log"
    : > "$fixture/sdk-git.log"
    : > "$fixture/sdk-delete.log"
    if [ "$temp_failure" = true ]; then
        # Deliberately return a dangerous stdout value with failure. The status
        # must be checked before constructing WT or installing a cleanup trap.
        printf '#!/bin/bash\nprintf "/\\n"\nexit 73\n' > "$fixture/sdk-bin/mktemp"
        printf '#!/bin/bash\nprintf "unexpected-delete\\n" >> "$SDK_DELETE_LOG"\nexit 97\n' > "$fixture/sdk-bin/rm"
        real_git="$(command -v git)"
        printf '#!/bin/bash\nprintf "%%s\\n" "$*" >> "$SDK_GIT_LOG"\nexec %q "$@"\n' "$real_git" > "$fixture/sdk-bin/git"
        chmod +x "$fixture/sdk-bin/mktemp" "$fixture/sdk-bin/rm" "$fixture/sdk-bin/git"
    fi
    (cd "$fixture" && unset DOTNET_ROOT DOTNET_ROOT_X64 DOTNET_ROOT_X86 &&
        SDK_CALL_LOG="$fixture/sdk-calls.log" SDK_GIT_LOG="$fixture/sdk-git.log" SDK_DELETE_LOG="$fixture/sdk-delete.log" PATH="$fixture/sdk-bin" bash "$GATE" --sha HEAD) > "$fixture/sdk-output.txt" 2>&1; rc=$?
    report "$label" "$expected" "$rc"
    if [ "$temp_failure" = true ]; then
        if [ -s "$fixture/sdk-calls.log" ] || [ -s "$fixture/sdk-delete.log" ] || grep -q '^worktree ' "$fixture/sdk-git.log"; then
            echo "  FAIL $label: failed temporary creation reached worktree, SDK or deletion operations"; fail=$((fail + 1))
        else
            echo "  ok   $label: no worktree, SDK, build or deletion operations"; pass=$((pass + 1))
        fi
        if grep -q 'cannot create owned temporary directory' "$fixture/sdk-output.txt"; then
            echo "  ok   $label: explicit temporary creation refusal"; pass=$((pass + 1))
        else
            echo "  FAIL $label: missing specific environment refusal"; fail=$((fail + 1))
        fi
        remove_fixture "$fixture"
        return
    fi

    bad_context=0; builds=0; expected_calls=0; path_probes=0; seen_worktree=0; retained_worktree=0
    while IFS='|' read -r host cwd pin operation; do
        # Every actual call must bind the target pin from the detached /wt checkout.
        case "$cwd" in
            */wt)
                seen_worktree=1
                [ ! -d "$cwd" ] || retained_worktree=1
                ;;
            *) bad_context=1 ;;
        esac
        [ "$cwd" != "$fixture" ] && [ "$pin" = "$target_pin" ] || bad_context=1
        if [ "$host" = "$fixture/sdk-bin" ] && [ "$operation" = --version ]; then
            path_probes=$((path_probes + 1))
        fi
        if [ "$operation" = build ]; then
            builds=$((builds + 1))
            [ "$host" = "$fixture/$expected_host" ] && expected_calls=$((expected_calls + 1))
        fi
    done < "$fixture/sdk-calls.log"
    report "$label: all probes/builds use target checkout and pin" 0 "$bad_context"
    report "$label: actual detached checkout was observed" 1 "$seen_worktree"
    report "$label: detached checkout directory removed after gate exit" 0 "$retained_worktree"
    if [ "$expected_host" = .dotnet ]; then
        report "$label: compatible local prevents any PATH-host probe" 0 "$path_probes"
    fi
    if [ "$expected" -eq 0 ]; then
        report "$label: exactly one selected-host build" 1 "$builds"
        report "$label: compatible host exclusively owns build" 1 "$expected_calls"
    else
        report "$label: no compilation after SDK refusal" 0 "$builds"
        if ! grep -q 'CANNOT EVALUATE' "$fixture/sdk-output.txt" || grep -q 'COMMITTED SHA DOES NOT COMPILE' "$fixture/sdk-output.txt"; then
            echo "  FAIL $label: SDK failure misclassified as source compilation"; fail=$((fail + 1))
        else
            echo "  ok   $label: environment refusal is explicit"; pass=$((pass + 1))
        fi
    fi
    if ! grep -q '|--version$' "$fixture/sdk-calls.log"; then
        echo "  FAIL $label: no actual resolution probe was observed"; fail=$((fail + 1))
    else
        echo "  ok   $label: actual resolution probe observed"; pass=$((pass + 1))
    fi
    remove_fixture "$fixture"
}

sdk_case 'LIVENESS: incompatible local falls back to compatible PATH host' 10.0.403 10.0.404 10.0.404 10.0.404 0 sdk-bin
sdk_case 'LIVENESS: compatible local retains preference over PATH host' 10.0.404 10.0.404 10.0.404 10.0.404 0 .dotnet
sdk_case 'SAFETY: target SDK controls resolution despite dirty caller pin' 10.0.403 10.0.404 10.0.404 10.0.403 0 sdk-bin
sdk_case 'SAFETY: all incompatible hosts refuse without compilation' 10.0.403 10.0.402 10.0.404 10.0.404 2 none
sdk_case 'SAFETY: failed temporary creation cannot reach cleanup or build' 10.0.404 10.0.404 10.0.404 10.0.404 2 none true

echo "[committed-sha-build-gate.test] $pass passed, $fail failed"
[ "$fail" -eq 0 ] || exit 1
exit 0
