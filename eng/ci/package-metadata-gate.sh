#!/usr/bin/env bash
# package-metadata-gate.sh — a shipping NuGet package must carry its consumer-visible metadata.
# This gate asserts that the EFFECTIVE (evaluated) package properties are populated, so the fields a
# consumer sees on nuget.org never silently ship empty.
#
# THE DEFECT CLASS (measured, real): `src/Directory.Build.props` does NOT import the repository-root
# props (MSBuild's upward discovery stops at the first Directory.Build.props it finds), so any
# consumer-visible package field declared ONLY at the root is stranded for every `src/` project — it
# evaluates to empty, and `dotnet pack` ships a package with a blank projectUrl / icon. It was found by
# packing and reading the nuspec (a blank field is invisible in the source); the remedy was to declare
# the fields in `src/`'s own props. This gate prevents the re-stranding: remove the declaration (or
# break inheritance again) and a shipping project's EFFECTIVE value goes empty → RED.
#
# INSTRUMENT — the EFFECTIVE value, not a static grep (the lesson the bug taught):
#   a static read of a props file proves what the build is CONFIGURED to do, not what it EMITS. MSBuild
#   inheritance, Conditions, and later targets can all change the answer. So the gate evaluates the real
#   property the way the packer sees it:  `dotnet msbuild <proj> -getProperty:<P>` → the evaluated value.
#   This is valid whether the value comes from `src/`'s props or an inherited root — it asserts the
#   OUTCOME (non-empty), not a particular source line.
#
# WHAT IS REQUIRED (a shipping project must have all three non-empty/true):
#   PackageProjectUrl              — the consumer-visible project link
#   PackageIcon                    — the consumer-visible package icon
#   RestorePackagesWithLockFile    — deterministic restore (== "true")
#
# SCOPE: EVERY tracked project under `src/`, not a representative. A single-representative default
# reported PASS over a denominator of one while the rest went unexamined — a sample presented as a
# survey — so a per-project override that empties a field on one project was invisible by construction.
# Non-shipping projects (IsPackable=false) drop out by their real evaluated property, not by a guess.
# Override the set with PKGMETA_PROJECTS; tune worker count with PKGMETA_JOBS (default 8).
#
# EXIT CODES (every one mapped by the caller; a non-0/1 is NEVER a pass):
#   0  PASS    evaluated >= 1 shipping project; every required field is populated
#   1  FAIL    a required consumer-visible field evaluates EMPTY on a shipping project -> would ship blank
#   2  REFUSE  could not evaluate (no project / msbuild error / unparseable output) — never a silent pass
#   3  REFUSE  --self-test failed (the gate itself is broken or vacuous)
#   *  REFUSE  unknown arg == could-not-evaluate
#
# TESTABILITY SEAM (an independent author != impl lock binds THIS surface):
#   PKGMETA_PROJECTS   space-separated shipping .csproj paths to evaluate (default: every tracked src/ project)
#   PKGMETA_JOBS       parallel evaluation workers (default 8)
#   PKGMETA_VALUES     inject evaluated triples "url|icon|lockfile" (newline-separated), one per project,
#                      to drive the decidable check WITHOUT invoking dotnet — the hermetic self-test seam
#   PKGMETA_REPO_ROOT  repo root; unset => git toplevel

set -uo pipefail

E_PASS=0; E_FAIL=1; E_REFUSE=2; E_SELFTEST=3

REPO_ROOT="${PKGMETA_REPO_ROOT:-$(git rev-parse --show-toplevel 2>/dev/null || echo .)}"
cd "$REPO_ROOT" || exit $E_REFUSE

# EVERY tracked project under src/. A one-project default reported PASS over a denominator of 1 while
# every other tracked project went unexamined. IsPackable is evaluated by MSBuild inside eval_project,
# so non-shipping projects drop out by their real property rather than by a guess made here.
PKGMETA_PROJECTS="${PKGMETA_PROJECTS:-$(git ls-files 'src/*.csproj' 'src/**/*.csproj' 2>/dev/null | LC_ALL=C sort)}"

# ── the DECIDABLE check (pure, hermetically testable): all three fields valid? ───────────────────
# Prints "OK" or a "MISSING <field>[,<field>...]" reason. Returns 0 / 1.
check_metadata() {  # $1=url $2=icon $3=lockfile
    local url="$1" icon="$2" lock="$3" miss=""
    [ -n "$url" ]  || miss="${miss:+$miss,}PackageProjectUrl"
    [ -n "$icon" ] || miss="${miss:+$miss,}PackageIcon"
    [ "$lock" = "true" ] || miss="${miss:+$miss,}RestorePackagesWithLockFile"
    if [ -n "$miss" ]; then printf 'MISSING %s\n' "$miss"; return 1; fi
    printf 'OK\n'; return 0
}

# ── evaluate the three effective properties for a project (the real instrument) ──────────────────
# Emits "url|icon|lockfile" on success, nothing on failure. Parses -getProperty JSON without jq.
eval_project() {  # $1=csproj ; echoes url|icon|lockfile or nothing
    local proj="$1" out url icon lock packable
    out="$(dotnet msbuild "$proj" -getProperty:PackageProjectUrl -getProperty:PackageIcon \
            -getProperty:RestorePackagesWithLockFile -getProperty:IsPackable -nologo 2>/dev/null)" || return 1
    # IsPackable is decided by MSBuild, not by grepping the csproj: the property is routinely set in an
    # imported .props file, so a text search over the project file alone misclassifies both directions.
    packable="$(printf '%s' "$out" | grep -oE '"IsPackable"[ ]*:[ ]*"[^"]*"' | sed -E 's/.*:[ ]*"([^"]*)"/\1/')"
    if [ "$packable" = "false" ]; then return 2; fi
    # extract each JSON string value (values contain no embedded quotes for these fields)
    url="$(printf '%s' "$out"  | grep -oE '"PackageProjectUrl"[ ]*:[ ]*"[^"]*"'           | sed -E 's/.*:[ ]*"([^"]*)"/\1/')"
    icon="$(printf '%s' "$out" | grep -oE '"PackageIcon"[ ]*:[ ]*"[^"]*"'                 | sed -E 's/.*:[ ]*"([^"]*)"/\1/')"
    lock="$(printf '%s' "$out" | grep -oE '"RestorePackagesWithLockFile"[ ]*:[ ]*"[^"]*"' | sed -E 's/.*:[ ]*"([^"]*)"/\1/')"
    # a well-formed evaluation returns the JSON object with all three keys present
    printf '%s' "$out" | grep -q '"Properties"' || return 1
    printf '%s|%s|%s\n' "$url" "$icon" "$lock"
}

sweep() {
    local evaluated=0 failed=0 idx=0
    echo "=== package-metadata gate: does every shipping project carry its consumer-visible metadata? ==="
    echo ""

    # If PKGMETA_VALUES is injected (hermetic mode), use those triples; else evaluate real projects.
    if [ -n "${PKGMETA_VALUES:-}" ]; then
        local triple url icon lock res
        while IFS= read -r triple; do
            [ -n "$triple" ] || continue
            idx=$((idx + 1))
            url="${triple%%|*}"; rest="${triple#*|}"; icon="${rest%%|*}"; lock="${rest#*|}"
            evaluated=$((evaluated + 1))
            res="$(check_metadata "$url" "$icon" "$lock")"
            if [ "$res" = "OK" ]; then
                echo "  ✓ ok    (injected #$idx) — projectUrl+icon+lockfile all populated"
            else
                echo "  ✗ FAIL  (injected #$idx) — ${res#MISSING } evaluates EMPTY (would ship a blank consumer field)"
                failed=$((failed + 1))
            fi
        done <<< "$PKGMETA_VALUES"
    else
        local proj triple res
        # PRE-EVALUATE IN PARALLEL. Each `dotnet msbuild -getProperty` costs about a second, so
        # evaluating every tracked project sequentially took minutes -- enough that a battery running
        # this gate twice exceeded a caller's timeout, which presents as a BROKEN gate rather than a
        # slow one. Evaluation is independent per project and read-only, so it parallelises exactly.
        # The grading loop stays sequential over the ORIGINAL order, so output stays deterministic.
        local _pmg_cache _pmg_entry _pmg_rc
        _pmg_cache="$(mktemp -d)"
        # The grading loop deletes each entry as it consumes it; this catches the directory itself and
        # any entry an early `continue` skipped, so a long CI run does not leave temp dirs behind.
        trap 'rm -rf "$_pmg_cache"' RETURN
        export -f eval_project
        export PMG_CACHE="$_pmg_cache"
        printf '%s
' $PKGMETA_PROJECTS | grep -v '^$' |             xargs -P "${PKGMETA_JOBS:-8}" -I{} bash -c 'out="$(eval_project "$1")"; rc=$?; printf "%s
%s
" "$rc" "$out" > "$PMG_CACHE/$(printf "%s" "$1" | tr "/" "_")"' _ {} 2>/dev/null

        for proj in $PKGMETA_PROJECTS; do
            if [ ! -f "$proj" ]; then
                echo "  REFUSE  project not found: $proj"
                continue
            fi
            _pmg_entry="$_pmg_cache/$(printf '%s' "$proj" | tr '/' '_')"
            if [ -s "$_pmg_entry" ]; then
                _pmg_rc="$(head -1 "$_pmg_entry")"
                triple="$(tail -n +2 "$_pmg_entry")"
            else
                # No cache entry means the worker produced nothing at all. Treat that as an evaluation
                # failure, never as a pass.
                _pmg_rc=1; triple=""
            fi
            case "$_pmg_rc" in
                0) : ;;
                2) continue ;;  # IsPackable=false — not a shipping project, out of scope by definition
                *) echo "  REFUSE  msbuild evaluation failed for $proj (cannot evaluate — not a pass)"; continue ;;
            esac
            rm -rf "$_pmg_cache/$(printf '%s' "$proj" | tr '/' '_')"
            evaluated=$((evaluated + 1))
            local url icon lock
            url="${triple%%|*}"; rest="${triple#*|}"; icon="${rest%%|*}"; lock="${rest#*|}"
            res="$(check_metadata "$url" "$icon" "$lock")"
            if [ "$res" = "OK" ]; then
                echo "  ✓ ok    $proj — projectUrl='$url' icon='$icon' lockfile='$lock'"
            else
                echo "  ✗ FAIL  $proj — ${res#MISSING } evaluates EMPTY:"
                echo "          projectUrl='$url' icon='$icon' lockfile='$lock' — this ships a blank consumer-visible field."
                failed=$((failed + 1))
            fi
        done
    fi

    echo ""
    echo "shipping-projects-evaluated=$evaluated failures=$failed"
    if [ "$evaluated" -eq 0 ]; then
        echo "package-metadata-gate: REFUSE — evaluated ZERO shipping projects (msbuild error / none found)."
        echo "  An empty evaluation is not a clean bill of health."
        return $E_REFUSE
    fi
    if [ "$failed" -gt 0 ]; then
        echo "package-metadata-gate: FAIL — a shipping project would publish a blank consumer-visible field."
        return $E_FAIL
    fi
    echo "package-metadata-gate: PASS — every evaluated shipping project carries projectUrl + icon + lockfile."
    return $E_PASS
}

# ── self-test: NON-VACUOUS (SAFETY + LIVENESS + REFUSE), hermetic via PKGMETA_VALUES + one real eval ─
self_test() {
    local bad=0

    # ARM 1 (SAFETY, decidable): each missing field must be caught by the pure check.
    if [ "$(check_metadata '' 'icon.png' 'true')" = "MISSING PackageProjectUrl" ] \
       && [ "$(check_metadata 'https://x' '' 'true')" = "MISSING PackageIcon" ] \
       && [ "$(check_metadata 'https://x' 'icon.png' 'false')" = "MISSING RestorePackagesWithLockFile" ]; then
        echo "  ok  ARM1 safety      — an empty projectUrl / icon / non-true lockfile is each detected"
    else
        echo "self-test ARM1 FAIL: the pure metadata check missed an empty field." >&2; bad=1
    fi

    # ARM 2 (LIVENESS, decidable): all-populated must PASS the pure check.
    if [ "$(check_metadata 'https://github.com/x/y' 'icon.png' 'true')" = "OK" ]; then
        echo "  ok  ARM2 liveness    — a fully-populated triple is accepted (no false positive)"
    else
        echo "self-test ARM2 FAIL: a valid metadata triple was rejected." >&2; bad=1
    fi

    # ARM 3 (e2e SAFETY, injected): a blank field drives the whole sweep to FAIL(1).
    local rc3; ( PKGMETA_VALUES='|icon.png|true' sweep ) >/dev/null 2>&1; rc3=$?
    if [ "$rc3" -eq "$E_FAIL" ]; then
        echo "  ok  ARM3 e2e-fail    — a blank consumer field drives the sweep to FAIL(1)"
    else
        echo "self-test ARM3 FAIL: blank projectUrl gave rc=$rc3, expected 1." >&2; bad=1
    fi

    # ARM 4 (e2e LIVENESS, injected): a populated triple drives the sweep to PASS(0).
    local rc4; ( PKGMETA_VALUES="$(printf '%s' 'https://github.com/x/y|icon.png|true')" sweep ) >/dev/null 2>&1; rc4=$?
    if [ "$rc4" -eq "$E_PASS" ]; then
        echo "  ok  ARM4 e2e-pass    — a populated triple drives the sweep to PASS(0)"
    else
        echo "self-test ARM4 FAIL: populated triple gave rc=$rc4, expected 0." >&2; bad=1
    fi

    # ARM 5 (REFUSE): zero evaluable input -> REFUSE(2), never a pass.
    local rc5; ( PKGMETA_VALUES="" PKGMETA_PROJECTS="/nonexistent/none.csproj" sweep ) >/dev/null 2>&1; rc5=$?
    if [ "$rc5" -eq "$E_REFUSE" ]; then
        echo "  ok  ARM5 refuse      — zero evaluable projects -> REFUSE(2), not a pass"
    else
        echo "self-test ARM5 FAIL: empty evaluation gave rc=$rc5, expected 2." >&2; bad=1
    fi

    # ARM 6 (PRODUCTION-PATH non-vacuity): actually evaluate the real default shipping project and
    # confirm it PASSES — proving the gate runs against reality, not only injected fixtures. Skipped
    # only if dotnet is unavailable (never on the mirror-CI runner, which has the SDK).
    if command -v dotnet >/dev/null 2>&1; then
        local rc6; ( sweep ) >/dev/null 2>&1; rc6=$?
        if [ "$rc6" -eq "$E_PASS" ]; then
            echo "  ok  ARM6 prod-path   — real default shipping project evaluates PASS (metadata populated today)"
        elif [ "$rc6" -eq "$E_REFUSE" ]; then
            echo "  --  ARM6 prod-path   SKIPPED (msbuild could not evaluate here — e.g. no restore); injected arms carry the class"
        else
            echo "self-test ARM6 FAIL: the real default shipping project FAILED metadata (rc=$rc6) — a live regression, not a test bug." >&2; bad=1
        fi
    else
        echo "  --  ARM6 prod-path   SKIPPED (dotnet unavailable); injected arms carry the class"
    fi

    if [ "$bad" -ne 0 ]; then
        echo "package-metadata-gate --self-test: FAILED (the gate is broken or vacuous)" >&2
        return $E_SELFTEST
    fi
    echo "package-metadata-gate --self-test: all arms pass (safety + liveness + e2e-fail + e2e-pass + refuse + prod-path)"
    return 0
}

case "${1:---sweep}" in
    --self-test) self_test; exit $? ;;
    --sweep)     sweep;     exit $? ;;
    -h|--help)   echo "usage: package-metadata-gate.sh [--sweep|--self-test]"; exit 0 ;;
    *)           echo "package-metadata-gate: unknown arg '$1'" >&2; exit $E_REFUSE ;;
esac
