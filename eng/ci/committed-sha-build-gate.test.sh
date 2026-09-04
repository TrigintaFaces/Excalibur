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

GATE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/committed-sha-build-gate.sh"
[ -x "$GATE" ] || chmod +x "$GATE" 2>/dev/null

pass=0
fail=0

report() { # name expected actual
    if [ "$3" -eq "$2" ]; then echo "  ok   $1 (exit $3)"; pass=$((pass + 1))
    else echo "  FAIL $1 — expected exit $2, got $3"; fail=$((fail + 1)); fi
}

new_repo() {
    local d; d="$(mktemp -d)"
    git -C "$d" init -q 2>/dev/null
    git -C "$d" config user.email t@t.t; git -C "$d" config user.name t
    git -C "$d" config commit.gpgsign false
    mkdir -p "$d/src/Widget"
    cat > "$d/src/Widget/Widget.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>
EOF
    echo "public class Seed {}" > "$d/src/Widget/Seed.cs"
    echo "#nullable enable" > "$d/src/Widget/PublicAPI.Unshipped.txt"
    git -C "$d" add -A >/dev/null 2>&1
    git -C "$d" commit -qm seed >/dev/null 2>&1
    printf '%s' "$d"
}

echo "[committed-sha-build-gate.test] running..."

# ── SAFETY: the c85dfbece shape — impl staged, baseline entry withheld ───────────────────────────────
R="$(new_repo)"
echo "public class Widget { public void New() {} }" > "$R/src/Widget/Widget.cs"
echo "Widget.New() -> void" >> "$R/src/Widget/PublicAPI.Unshipped.txt"   # in the TREE...
git -C "$R" add src/Widget/Widget.cs >/dev/null 2>&1                      # ...but NOT in the commit
( cd "$R" && bash "$GATE" --staged ) >"$R/out.txt" 2>&1; rc=$?
report "SAFETY: under-staged coupled set (impl staged, baseline withheld) is REJECTED" 1 "$rc"
if grep -q "PublicAPI.Unshipped.txt" "$R/out.txt" 2>/dev/null; then
    echo "  ok   SAFETY: failure output NAMES the missing file"; pass=$((pass + 1))
else
    echo "  FAIL SAFETY: failure output does NOT name the missing file (diagnosis speed is the point)"; fail=$((fail + 1))
fi
rm -rf "$R"

# ── SAFETY: an UNTRACKED new file in a touched project is equally invisible to the commit ────────────
R="$(new_repo)"
echo "public class A { }" > "$R/src/Widget/A.cs"
echo "public class B { }" > "$R/src/Widget/B.cs"     # never `git add`ed at all
git -C "$R" add src/Widget/A.cs >/dev/null 2>&1
( cd "$R" && bash "$GATE" --staged ) >/dev/null 2>&1; rc=$?
report "SAFETY: untracked sibling in a touched project is REJECTED" 1 "$rc"
rm -rf "$R"

# ── LIVENESS: the correctly-staged complete commit MUST pass (the arm that gets skipped) ─────────────
R="$(new_repo)"
echo "public class Widget { public void New() {} }" > "$R/src/Widget/Widget.cs"
echo "Widget.New() -> void" >> "$R/src/Widget/PublicAPI.Unshipped.txt"
git -C "$R" add src/Widget/Widget.cs src/Widget/PublicAPI.Unshipped.txt >/dev/null 2>&1
( cd "$R" && bash "$GATE" --staged ) >/dev/null 2>&1; rc=$?
report "LIVENESS: complete, correctly-staged commit is ACCEPTED" 0 "$rc"
rm -rf "$R"

# ── LIVENESS: dirt in an UNRELATED project must not block an unrelated commit ────────────────────────
# Without this the gate becomes "you may never commit with a dirty tree", which is unusable in a
# multi-worker repo and would be switched off within a day.
R="$(new_repo)"
mkdir -p "$R/src/Other"
cat > "$R/src/Other/Other.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>
EOF
echo "public class O {}" > "$R/src/Other/O.cs"
git -C "$R" add -A >/dev/null 2>&1; git -C "$R" commit -qm other >/dev/null 2>&1
echo "public class Widget {}" > "$R/src/Widget/Widget.cs"
git -C "$R" add src/Widget/Widget.cs >/dev/null 2>&1
echo "// unrelated dirt" >> "$R/src/Other/O.cs"          # dirty, but a DIFFERENT project
( cd "$R" && bash "$GATE" --staged ) >/dev/null 2>&1; rc=$?
report "LIVENESS: dirt in an UNRELATED project does not block the commit" 0 "$rc"
rm -rf "$R"

# ── LIVENESS: an empty index is not a failure ───────────────────────────────────────────────────────
R="$(new_repo)"
( cd "$R" && bash "$GATE" --staged ) >/dev/null 2>&1; rc=$?
report "LIVENESS: nothing staged is ACCEPTED" 0 "$rc"
rm -rf "$R"

# ── ENV: an unresolvable ref must REFUSE, never pass ────────────────────────────────────────────────
R="$(new_repo)"
( cd "$R" && "$GATE" --sha deadbeefdeadbeef ) >/dev/null 2>&1; rc=$?
report "SAFETY: unresolvable ref REFUSES (exit 2, not a free pass)" 2 "$rc"
rm -rf "$R"

# ── SAFETY: a coupled set with NO project in common — the shape that put a gate into HEAD ───────────
# The gate landed and its only caller, a .github composite action, stayed dirty. Neither file has a
# .csproj ancestor, so the project-input arm above cannot see this at all: the pre-change gate printed
# "no project-owned files staged — nothing to verify" and exited 0.
R="$(new_repo)"
mkdir -p "$R/eng/ci" "$R/.github/actions/x"
echo "echo gate" > "$R/eng/ci/some-verifier-gate.sh"
echo "run: bash eng/ci/some-verifier-gate.sh build.log" > "$R/.github/actions/x/action.yml"
git -C "$R" add -A >/dev/null 2>&1; git -C "$R" commit -qm wired >/dev/null 2>&1
echo "echo gate v2" >> "$R/eng/ci/some-verifier-gate.sh"
echo "run: bash eng/ci/some-verifier-gate.sh other.log" >> "$R/.github/actions/x/action.yml"
git -C "$R" add eng/ci/some-verifier-gate.sh >/dev/null 2>&1   # caller left dirty
( cd "$R" && bash "$GATE" --staged ) >"$R/out2.txt" 2>&1; rc=$?
report "SAFETY: coupled set split across NON-project files is REJECTED" 1 "$rc"
if grep -q "action.yml" "$R/out2.txt" 2>/dev/null; then
    echo "  ok   SAFETY: failure output NAMES the dirty file that references the staged one"; pass=$((pass + 1))
else
    echo "  FAIL SAFETY: failure output does not name the referencing file"; fail=$((fail + 1))
fi
rm -rf "$R"

# ── LIVENESS: tracker DATA that merely mentions a filename must NOT block a commit ──────────────────
# Issue records quote filenames constantly. Measured on the real tree, staging one gate produced five
# candidate flags and every one of them was a .beads record. If those blocked, the gate would fire on
# almost every commit and be switched off — a safety arm that costs its own enforcement.
R="$(new_repo)"
mkdir -p "$R/eng/ci" "$R/.beads"
echo "echo gate" > "$R/eng/ci/some-verifier-gate.sh"
git -C "$R" add -A >/dev/null 2>&1; git -C "$R" commit -qm base >/dev/null 2>&1
echo "echo gate v2" >> "$R/eng/ci/some-verifier-gate.sh"
echo '{"id":"x","description":"see eng/ci/some-verifier-gate.sh for details"}' > "$R/.beads/issues.jsonl"
git -C "$R" add eng/ci/some-verifier-gate.sh >/dev/null 2>&1
( cd "$R" && bash "$GATE" --staged ) >"$R/out3.txt" 2>&1; rc=$?
report "LIVENESS: tracker data mentioning a staged file does NOT block" 0 "$rc"
rm -rf "$R"

# ── PROVIDER SIBLINGS: advisory arm ─────────────────────────────────────────────────────────────────
# Provider twins never reference each other, so the reference arm cannot see a set split across them.
# These bind the structural signal AND the fact that it must not block.
sib_repo() {
    local d; d="$(mktemp -d)"
    git -C "$d" init -q 2>/dev/null
    git -C "$d" config user.email t@t.t; git -C "$d" config user.name t
    git -C "$d" config commit.gpgsign false
    mkdir -p "$d/src/Excalibur.Fam.SqlServer/Requests" "$d/src/Excalibur.Fam.Postgres/Requests"
    echo "a" > "$d/src/Excalibur.Fam.SqlServer/Requests/Save.cs"
    echo "a" > "$d/src/Excalibur.Fam.Postgres/Requests/Save.cs"
    git -C "$d" add -A >/dev/null 2>&1; git -C "$d" commit -qm seed >/dev/null 2>&1
    printf '%s' "$d"
}

R="$(sib_repo)"
echo "b" > "$R/src/Excalibur.Fam.SqlServer/Requests/Save.cs"
echo "b" > "$R/src/Excalibur.Fam.Postgres/Requests/Save.cs"
git -C "$R" add src/Excalibur.Fam.SqlServer/Requests/Save.cs >/dev/null 2>&1   # twin left dirty
( cd "$R" && bash "$GATE" --staged ) >"$R/sib1.txt" 2>&1; rc=$?
if grep -q "Excalibur.Fam.Postgres/Requests/Save.cs" "$R/sib1.txt" 2>/dev/null; then
    echo "  ok   SAFETY: a dirty provider sibling of a staged file is NAMED"; pass=$((pass + 1))
else
    echo "  FAIL SAFETY: dirty provider sibling was not named"; fail=$((fail + 1))
fi
# The ruling is explicit that this must NOT block: a binary collation is REQUIRED on SQL Server and
# WRONG on PostgreSQL, so a blocking parity arm would reject a correct provider-specific commit.
report "ADVISORY: naming a sibling does NOT block the commit" 0 "$rc"
rm -rf "$R"

R="$(sib_repo)"
echo "b" > "$R/src/Excalibur.Fam.SqlServer/Requests/Save.cs"
echo "b" > "$R/src/Excalibur.Fam.Postgres/Requests/Save.cs"
git -C "$R" add -A >/dev/null 2>&1                                             # both staged
( cd "$R" && bash "$GATE" --staged ) >"$R/sib2.txt" 2>&1
if grep -q "PROVIDER SIBLINGS" "$R/sib2.txt" 2>/dev/null; then
    echo "  FAIL LIVENESS: siblings all staged but the arm still fired"; fail=$((fail + 1))
else
    echo "  ok   LIVENESS: all siblings staged -> arm is silent"; pass=$((pass + 1))
fi
rm -rf "$R"

R="$(sib_repo)"
mkdir -p "$R/src/Excalibur.Solo.OnlyOne/Requests"
echo "x" > "$R/src/Excalibur.Solo.OnlyOne/Requests/A.cs"
git -C "$R" add -A >/dev/null 2>&1; git -C "$R" commit -qm solo >/dev/null 2>&1
echo "y" > "$R/src/Excalibur.Solo.OnlyOne/Requests/A.cs"
git -C "$R" add -A >/dev/null 2>&1
( cd "$R" && bash "$GATE" --staged ) >"$R/sib3.txt" 2>&1
if grep -q "PROVIDER SIBLINGS" "$R/sib3.txt" 2>/dev/null; then
    echo "  FAIL LIVENESS: single-provider family produced a sibling warning"; fail=$((fail + 1))
else
    echo "  ok   LIVENESS: single-provider family -> arm is silent"; pass=$((pass + 1))
fi
rm -rf "$R"

echo "[committed-sha-build-gate.test] $pass passed, $fail failed"
[ "$fail" -eq 0 ] || exit 1
exit 0
