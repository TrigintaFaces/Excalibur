#!/usr/bin/env bash
# sql-predicate-gate.sh — a declared SQL predicate fragment that the SQL string never
# interpolates is a SILENTLY-ABSENT tenant filter. This gate detects that class.
#
# THE DEFECT CLASS (the one that shipped a live cross-tenant DELETE):
#
#   A request builder declares a conditional SQL-fragment variable —
#       var tenantPredicate = scope.IsScoped ? " AND TENANTID = :TenantId" : string.Empty;
#   — and the emitted SQL string never places {tenantPredicate} into the statement. The
#   declaration is present and correct; only its ABSENCE from the command string is wrong,
#   so the DELETE/SELECT/UPDATE runs with NO tenant filter and touches every tenant's rows.
#
# WHY IT IS TRIPLE-BLIND (and therefore needs a gate, not a rule):
#   COMPILER  a declared string that is assigned is not an error; at most an unused-var warning,
#             and none at all once it is referenced anywhere.
#   TESTS     the unscoped path frequently has no conformance arm, and the scoped path is
#             unaffected by the missing interpolation.
#   GREP      searching for the variable name FINDS its declaration and returns a hit — the
#             obvious verification CONFIRMS the bug. You have to check the string, not the name.
#
# THE DETECTOR — PER VARIABLE, not per file. This is load-bearing:
#
#   A file-level count (declared>0 AND interpolated==0) is VACUOUS: one correctly-interpolated
#   fragment in a file masks a defective sibling in the same file (declared=2, interpolated=1 ->
#   both > 0 -> silent, missing the un-interpolated one). So the unit of judgement is the
#   individual declared fragment variable:
#
#     for each declared SQL-PREDICATE-FRAGMENT variable V in a file:
#         FIRE if {V} appears NOWHERE in the file AND V is never concatenated/appended into a string.
#
# WHAT COUNTS AS A "PREDICATE FRAGMENT" (derived, not a hardcoded name list):
#
#   A declaration whose right-hand side is a PLAIN (non-interpolated) SQL clause fragment — NOT a
#   $"..."/$"""...""" assembled command (its own WHERE/=@ would otherwise self-flag). Concretely
#   the RHS contains NO  $"  AND carries a plain "..." literal reading as a clause fragment:
#       - a bound predicate    "  AND|OR|WHERE <col> = @Param " / " ... = :Param "   (the defect shape)
#       - a column injection   " , TenantId"                    (conditional INSERT-column addition)
#   Both are conditional SQL fragments whose silent absence removes a tenant term. The bound-comparison
#   requirement (an identifier `= @`/`= :` after the connective) is what distinguishes a SQL predicate
#   from English prose (" and the ...") so the detector does not fire on comments or message strings.
#
# "USED" (so a correct file stays silent — no crying wolf):
#   V is used-in-SQL if {V} is interpolated anywhere, OR V is placed into a string by
#   concatenation / StringBuilder.Append / string.Format / .Replace. Only a fragment that is
#   built and then placed NOWHERE is a finding.
#
# DELIBERATE NON-COVERAGE (stated, not silent — an unstated gap is the defect this gate exists to stop):
#   * NEVER-FILTERED-AT-ALL: a statement against a tenant-bearing table that declares no predicate
#     at all is a DIFFERENT class — it needs per-statement parsing plus schema knowledge of which
#     tables are tenant-bearing (a schema fact, not a syntactic one). This gate does not claim it.
#   * A fragment assembled via a helper method call rather than an inline quoted literal is not
#     classified as a fragment (the detector keys on the inline quoted SQL clause — the observed shape).
#   * A defective statement built entirely by string concatenation is a detection blind spot here;
#     concatenation is treated as "used" to avoid false positives on correct concat-style builders.
#
# EXIT CODES (every one is mapped by the caller; a non-0/1 is NEVER a pass):
#   0  PASS    scanned, at least one predicate fragment evaluated, every one is placed into SQL
#   1  FAIL    a declared predicate fragment is never interpolated/placed -> silently-absent filter
#   2  REFUSE  could not evaluate (no git / zero .cs files / zero fragments found == parser blind)
#   3  REFUSE  --self-test failed (the gate itself is broken or vacuous)
#   *  REFUSE  unknown arg == could-not-evaluate
#
# There is deliberately NO suppression cap and NO allowlist: a gate that can mute itself into a
# PASS is the false-safety class this exists to remove.
#
# TESTABILITY SEAM (an independent author != impl lock binds THIS surface):
#   SQLPRED_SRC_ROOTS   scan roots for the production sweep     (default: "src")
#   SQLPRED_REPO_ROOT   repo root; unset => git toplevel        (lets a hermetic fixture skip git)
#   SQLPRED_MIN_FRAGS   non-vacuity floor of fragments found    (default: 1)
#
# PERFORMANCE: a batched prefilter (one grep over the whole file set) narrows thousands of source
# files to the handful that carry a plain SQL clause literal; only those are parsed, one awk process
# each. Per-file process spawns are the cost under an on-access AV scanner, so the slow path is never
# taken over the whole tree.

set -uo pipefail

E_PASS=0; E_FAIL=1; E_REFUSE=2; E_SELFTEST=3

REPO_ROOT="${SQLPRED_REPO_ROOT:-$(git rev-parse --show-toplevel 2>/dev/null || echo .)}"
cd "$REPO_ROOT" || exit $E_REFUSE

SQLPRED_SRC_ROOTS="${SQLPRED_SRC_ROOTS:-src}"
SQLPRED_MIN_FRAGS="${SQLPRED_MIN_FRAGS:-1}"

# The prefilter that decides which files are worth parsing. It MUST match every shape classify()
# accepts, or a real fragment would be filtered out before it is ever examined (a control that
# shares the query's blind spot). Kept in lock-step with classify() below.
PREFILTER='"[ \t]*(AND|OR|WHERE)[ \t]+[A-Za-z_][A-Za-z0-9_.]*[ \t]*=[ \t]*[@:]|"[ \t]*,[ \t]*[A-Za-z_][A-Za-z0-9_]*[ \t]*"'

# ── list *.cs under the scan roots: git-tracked for the real run, find-fallback for an untracked ──
# temp fixture tree (git ls-files cannot see planted fixtures). One helper, both modes, so the exact
# path the real sweep uses is the one the independent lock exercises. Absolute roots skip git (fast
# under AV real-time scan, where git subprocess spawns dominate).
_list_cs() {
    local pat base got
    for pat in $1; do
        base="${pat%%\*\*}"; base="${base%/}"; [ -n "$base" ] || base="$pat"
        case "$pat" in
            /*|[A-Za-z]:[\\/]*)
                if [ -d "$base" ]; then find "$base" -type f -name '*.cs' 2>/dev/null
                elif [ -f "$pat" ]; then printf '%s\n' "$pat"; fi ;;
            *)
                got="$(git ls-files -- "$pat" 2>/dev/null | grep -E '\.cs$' || true)"
                if [ -n "$got" ]; then printf '%s\n' "$got"
                elif [ -d "$base" ]; then find "$base" -type f -name '*.cs' 2>/dev/null
                elif [ -f "$pat" ]; then printf '%s\n' "$pat"; fi ;;
        esac
    done
}

# ── scan ONE file: emit `FRAG <name>` for every classified predicate fragment and `FINDING <name>` ─
# for every fragment that is never placed into SQL. One awk process per file, no per-name greps.
_scan() {
    awk '
        function classify(buf) {
            if (buf ~ /\$"/) return 0                                             # assembled command, not a fragment
            if (buf !~ /"/)  return 0
            if (buf ~ /"[ \t]*(AND|OR|WHERE)[ \t]+[A-Za-z_][A-Za-z0-9_.]*[ \t]*=[ \t]*[@:]/) return 1  # bound predicate
            if (buf ~ /"[ \t]*,[ \t]*[A-Za-z_][A-Za-z0-9_]*[ \t]*"/) return 1                            # column injection
            return 0
        }
        { full = full "\n" $0; lines[NR] = $0 }
        END {
            collecting = 0; name = ""; buf = ""
            for (i = 1; i <= NR; i++) {
                line = lines[i]
                if (!collecting) {
                    if (match(line, /(^|[^.A-Za-z0-9_])(var|const[ \t]+string|string)[ \t]+[A-Za-z_][A-Za-z0-9_]*[ \t]*=/)) {
                        hdr = substr(line, RSTART, RLENGTH)
                        n = hdr; sub(/[ \t]*=$/, "", n); sub(/.*[ \t]/, "", n)
                        name = n; buf = line; collecting = 1
                        if (line ~ /;/) { if (classify(buf)) frag[name] = 1; collecting = 0; buf = "" }
                    }
                } else {
                    buf = buf " " line
                    if (line ~ /;/) { if (classify(buf)) frag[name] = 1; collecting = 0; buf = "" }
                }
            }
            if (collecting && classify(buf)) frag[name] = 1
            for (v in frag) {
                print "FRAG " v
                # "used-in-SQL" is judged by PRECISE, non-bridgeable signals only. An earlier form
                # keyed on a bare method name (Format|Append|...) with a wildcard bridge to the var,
                # which a stray `OracleTableName.Format(...)` elsewhere in the file matched — masking
                # the real un-interpolated fragment as "used". The signals below cannot bridge:
                #   {V}            interpolated into a $"..." string
                #   + V  /  V +    string concatenation (incl. sql += V)
                #   (V   /  , V    passed as a call argument (StringBuilder.Append(V), string.Join(",",V))
                used = 0
                if      (full ~ ("\\{" v "\\}"))                     used = 1
                else if (full ~ ("\\+=?[ \t]*" v "[^A-Za-z0-9_]"))   used = 1
                else if (full ~ ("[^A-Za-z0-9_]" v "[ \t]*\\+"))     used = 1
                else if (full ~ ("[(,][ \t]*" v "[^A-Za-z0-9_]"))    used = 1
                if (!used) print "FINDING " v
            }
        }
    ' "$1"
}

# thin wrappers over _scan (single classifier, one source of truth) for the self-test arms
fragment_decls() { _scan "$1" | awk '/^FRAG /{print $2}' | sort -u; }
is_used() { _scan "$1" | grep -qx "FRAG $2" && ! _scan "$1" | grep -qx "FINDING $2"; }

sweep() {
    local files candidates frag_total=0 findings=0 scanned=0
    files="$(_list_cs "$SQLPRED_SRC_ROOTS" | sort -u)"
    if [ -z "$files" ]; then
        echo "sql-predicate-gate: REFUSE — enumerated zero .cs files under {$SQLPRED_SRC_ROOTS}."
        echo "  A scan that reads no source is a broken query, not a clean result."
        return $E_REFUSE
    fi

    # Batched prefilter: one grep over the whole set -> only files carrying a plain SQL clause literal.
    candidates="$(printf '%s\n' "$files" | tr '\n' '\0' | xargs -0 -r grep -lE "$PREFILTER" 2>/dev/null || true)"

    echo "=== SQL predicate gate: is every declared predicate fragment actually placed into its SQL? ==="
    echo ""

    local f v line
    if [ -n "$candidates" ]; then
        while IFS= read -r f; do
            [ -f "$f" ] || continue
            local hit=0
            while IFS= read -r line; do
                case "$line" in
                    "FRAG "*)    frag_total=$((frag_total + 1)); hit=1 ;;
                    "FINDING "*) v="${line#FINDING }"
                        echo "  ✗ FAIL  $f"
                        echo "          declared predicate fragment '$v' is NEVER interpolated ({$v}) or"
                        echo "          concatenated into any SQL string — the filter it builds is silently absent."
                        echo "          A statement using this builder runs WITHOUT the predicate '$v' represents."
                        findings=$((findings + 1)) ;;
                esac
            done < <(_scan "$f")
            [ "$hit" -eq 1 ] && scanned=$((scanned + 1))
        done <<< "$candidates"
    fi

    echo ""
    echo "files-with-fragments=$scanned predicate-fragments-evaluated=$frag_total findings=$findings"

    # Non-vacuity floor: zero fragments found across the scan means the parser missed them or the
    # scope is wrong — refusing here stops a parser-blind run from masquerading as a clean result.
    if [ "$frag_total" -lt "$SQLPRED_MIN_FRAGS" ]; then
        echo "sql-predicate-gate: REFUSE — found $frag_total predicate fragments (floor=$SQLPRED_MIN_FRAGS)."
        echo "  An enumeration this thin means the detector saw no SQL builders; that is not a pass."
        return $E_REFUSE
    fi
    if [ "$findings" -gt 0 ]; then
        echo "sql-predicate-gate: FAIL — a declared predicate fragment is never placed into its SQL."
        return $E_FAIL
    fi
    echo "sql-predicate-gate: PASS — every declared predicate fragment is interpolated or concatenated into SQL."
    return $E_PASS
}

# ── self-test: NON-VACUOUS, every arm (SAFETY + LIVENESS + PER-VARIABLE + REFUSE + e2e) ──────────
# Fixtures are synthetic and self-contained (mirror-safe): they do NOT depend on git history
# surviving the public-mirror copy.
self_test() {
    local tmp bad=0
    tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' RETURN

    # ARM 1 (SAFETY): a declared predicate fragment never interpolated must be caught.
    mkdir -p "$tmp/a"
    cat > "$tmp/a/Bad.cs" <<'EOF'
var tenantPredicate = scope.IsScoped ? " AND TENANTID = :TenantId" : string.Empty;
var sql = $"""
   DELETE FROM {qualifiedTable}
   WHERE AGGREGATEID = :AggregateId
     AND VERSION < :Version
   """;
EOF
    if fragment_decls "$tmp/a/Bad.cs" | grep -qx 'tenantPredicate' && ! is_used "$tmp/a/Bad.cs" tenantPredicate; then
        echo "  ok  ARM1 safety      — declared-but-never-interpolated fragment detected"
    else
        echo "self-test ARM1 FAIL: predicate fragment 'tenantPredicate' not detected as unused." >&2; bad=1
    fi

    # ARM 2 (LIVENESS): the SAME fragment, interpolated, must NOT be flagged (no false positive).
    mkdir -p "$tmp/b"
    cat > "$tmp/b/Good.cs" <<'EOF'
var tenantPredicate = scope.IsScoped
    ? " AND TENANTID = :TenantId"
    : " AND TENANTID IS NULL";
var sql = $"""
   DELETE FROM {qualifiedTable}
   WHERE AGGREGATEID = :AggregateId
     AND VERSION < :Version{tenantPredicate}
   """;
EOF
    if fragment_decls "$tmp/b/Good.cs" | grep -qx 'tenantPredicate' && is_used "$tmp/b/Good.cs" tenantPredicate; then
        echo "  ok  ARM2 liveness    — interpolated fragment NOT flagged (no false positive)"
    else
        echo "self-test ARM2 FAIL: a correctly interpolated fragment was flagged or lost." >&2; bad=1
    fi

    # ARM 3 (LIVENESS, assembled-command): the $"""...""" command var must NOT self-classify as a
    # fragment despite containing WHERE / = :param.
    if fragment_decls "$tmp/b/Good.cs" | grep -qx 'sql'; then
        echo "self-test ARM3 FAIL: the assembled command 'sql' was misclassified as a fragment." >&2; bad=1
    else
        echo "  ok  ARM3 no-selfflag — assembled command not misclassified as a fragment"
    fi

    # ARM 4 (PER-VARIABLE, the masking case): a file with ONE interpolated + ONE non-interpolated
    # fragment must FIRE on the non-interpolated one. A per-file count would go silent here.
    mkdir -p "$tmp/c"
    cat > "$tmp/c/Mixed.cs" <<'EOF'
var goodPredicate = scope.IsScoped ? " AND TENANTID = :TenantId" : " AND TENANTID IS NULL";
var badPredicate  = scope.IsScoped ? " AND STATUS = :Status" : string.Empty;
var sql = $"""
   SELECT * FROM {qualifiedTable}
   WHERE AGGREGATEID = :AggregateId{goodPredicate}
   """;
EOF
    if ! is_used "$tmp/c/Mixed.cs" badPredicate && is_used "$tmp/c/Mixed.cs" goodPredicate; then
        echo "  ok  ARM4 per-var     — non-interpolated sibling FIRES while interpolated sibling is silent"
    else
        echo "self-test ARM4 FAIL: per-variable masking case not handled (a correct sibling masked a defect)." >&2; bad=1
    fi

    # ARM 4b (REGRESSION — no method-name bridge): a stray Format/Append method call elsewhere in the
    # file must NOT mask an un-interpolated fragment as "used". This is the exact real-defect shape:
    # DeleteSnapshotsOlderThanRequest declared `var qualifiedTable = OracleTableName.Format(...)` above
    # the un-interpolated `tenantPredicate` — a bare-method-name "used" heuristic bridged across it.
    mkdir -p "$tmp/d"
    cat > "$tmp/d/Bridge.cs" <<'EOF'
var qualifiedTable = OracleTableName.Format(schema, table);
var tenantPredicate = scope.IsScoped ? " AND TENANTID = :TenantId" : string.Empty;
var sql = $"""
   DELETE FROM {qualifiedTable}
   WHERE AGGREGATEID = :AggregateId
     AND VERSION < :Version
   """;
EOF
    if ! is_used "$tmp/d/Bridge.cs" tenantPredicate; then
        echo "  ok  ARM4b no-bridge  — a stray .Format(...) call does not mask an un-interpolated fragment"
    else
        echo "self-test ARM4b FAIL: a method-name bridge masked the real-defect fragment as used." >&2; bad=1
    fi

    # ARM 4c (LIVENESS, concat + arg forms): fragments placed by += concatenation or as a call
    # argument (string.Join) must NOT be flagged — the correct non-interpolation use patterns.
    mkdir -p "$tmp/e"
    cat > "$tmp/e/Uses.cs" <<'EOF'
var appendPredicate = scope.IsScoped ? " AND A = :A" : string.Empty;
var joinPredicate   = scope.IsScoped ? " AND B = :B" : string.Empty;
sql += appendPredicate;
var whereClause = string.Join(" ", new[] { baseClause, joinPredicate });
EOF
    if is_used "$tmp/e/Uses.cs" appendPredicate && is_used "$tmp/e/Uses.cs" joinPredicate; then
        echo "  ok  ARM4c concat/arg  — += concatenation and call-argument uses are recognised (no false positive)"
    else
        echo "self-test ARM4c FAIL: a += concat or call-argument use was flagged as unused." >&2; bad=1
    fi

    # ARM 5 (end-to-end env-root sweep): the seam the independent lock binds.
    #   planted defect tree -> FAIL(1); clean tree -> PASS(0); empty root -> REFUSE(2).
    local e2e="$tmp/e2e"; mkdir -p "$e2e/bad" "$e2e/good" "$e2e/empty"
    cp "$tmp/a/Bad.cs"  "$e2e/bad/Bad.cs"
    cp "$tmp/b/Good.cs" "$e2e/good/Good.cs"
    local rc_bad rc_good rc_empty
    ( SQLPRED_SRC_ROOTS="$e2e/bad"   SQLPRED_MIN_FRAGS=1 sweep ) >/dev/null 2>&1; rc_bad=$?
    ( SQLPRED_SRC_ROOTS="$e2e/good"  SQLPRED_MIN_FRAGS=1 sweep ) >/dev/null 2>&1; rc_good=$?
    ( SQLPRED_SRC_ROOTS="$e2e/empty" SQLPRED_MIN_FRAGS=1 sweep ) >/dev/null 2>&1; rc_empty=$?
    if [ "$rc_bad" -ne "$E_FAIL" ]; then
        echo "self-test ARM5 FAIL: e2e sweep did not FAIL(1) on a planted defect (got rc=$rc_bad)." >&2; bad=1
    elif [ "$rc_good" -ne "$E_PASS" ]; then
        echo "self-test ARM5 FAIL: e2e sweep did not PASS(0) on a clean tree (got rc=$rc_good)." >&2; bad=1
    elif [ "$rc_empty" -ne "$E_REFUSE" ]; then
        echo "self-test ARM5 FAIL: e2e sweep did not REFUSE(2) on an empty scan (got rc=$rc_empty)." >&2; bad=1
    else
        echo "  ok  ARM5 e2e         — env-root sweep: defect=FAIL(1) clean=PASS(0) empty=REFUSE(2)"
    fi

    # ARM 6 (PRODUCTION-PATH non-vacuity): with NO env override the gate must enumerate the REAL src
    # and find predicate fragments to evaluate — else the seam is a fixture-only no-op against reality.
    local prod_frags real_file
    prod_frags=0
    while IFS= read -r real_file; do
        [ -f "$real_file" ] || continue
        if _scan "$real_file" | grep -q '^FRAG '; then prod_frags=1; break; fi
    done <<< "$(git grep -lE "$PREFILTER" -- 'src/**/*.cs' 2>/dev/null | head -40)"
    if [ "$prod_frags" -eq 1 ]; then
        echo "  ok  ARM6 prod-path   — real src enumeration (no env) finds predicate fragments to evaluate"
    else
        echo "self-test ARM6 FAIL: production-path enumeration found ZERO real predicate fragments — fixture-only no-op." >&2; bad=1
    fi

    if [ "$bad" -ne 0 ]; then
        echo "sql-predicate-gate --self-test: FAILED (the gate is broken or vacuous)" >&2
        return $E_SELFTEST
    fi
    echo "sql-predicate-gate --self-test: all arms pass (safety + liveness + no-selfflag + per-var + e2e + prod-path)"
    return 0
}

case "${1:---sweep}" in
    --self-test) self_test; exit $? ;;
    --sweep)     sweep;     exit $? ;;
    -h|--help)   echo "usage: sql-predicate-gate.sh [--sweep|--self-test]"; exit 0 ;;
    *)           echo "sql-predicate-gate: unknown arg '$1'" >&2; exit $E_REFUSE ;;
esac
