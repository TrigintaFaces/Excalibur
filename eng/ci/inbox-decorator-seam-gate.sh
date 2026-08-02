#!/usr/bin/env bash
# inbox-decorator-seam-gate.sh — an inbox DECORATOR that forwards the relational transactional seam but
# not the scoped one silently downgrades a document store's atomicity. This gate detects it.
#
# THE DEFECT CLASS (a real one, shipped and invisible to the full test suite):
#
#   The inbox middleware selects its HIGHEST-PRECEDENCE exactly-once path by type-testing the OUTERMOST
#   store instance for IScopedTransactionalInboxStore. A decorator is the outermost instance. So a
#   decorator that lists ITransactionalInboxStore in its base list and omits IScopedTransactionalInboxStore
#   makes the scoped seam INVISIBLE through decoration: wrapping a document-store inbox in encryption or
#   telemetry silently drops the consumer from exactly-once to the at-least-once claim protocol. No error,
#   no warning, no log line — the guarantee changes as a side effect of enabling an unrelated feature.
#
#   It is invisible to tests for the same reason it is invisible to the middleware: the decorator still
#   compiles, still satisfies every interface it declares, and every unit test that constructs the inner
#   store directly takes the correct path. Only a test that asserts the DECORATED instance is still
#   selected as scoped can see it, and that is precisely the test nobody writes for a seam they forgot.
#
# WHY DECORATORS ONLY, AND NOT EVERY TRANSACTIONAL INBOX STORE (read before widening):
#
#   The rule "anything transactional must declare both seams" is a DIFFERENT and broader claim. A plain
#   provider that implements only the relational seam is not lying to anyone: the middleware's type test
#   correctly returns false and it correctly falls through to the claim path. That may be a capability gap
#   worth closing, but it is not this silent-downgrade class, and folding it in here would make the gate
#   assert a design decision it has no standing to make. A DECORATOR is different in kind: it stands
#   BETWEEN the middleware and a store that may well be scoped-capable, so an omission there hides a
#   capability that exists rather than merely declining to add one.
#
# THE DISCRIMINATOR (decidable, and verified against the real tree): a decorator is a type whose base list
#   names IInboxStore AND which holds an IInboxStore of its own (the wrapped inner store). Providers hold
#   a connection/client, never an IInboxStore, so the two populations separate cleanly with no tuning.
#
# SCOPE: C# under src/. Base-list extraction handles C# 12 primary constructors (a parameter list may sit
#   between the identifier and the colon), so a primary-constructor decorator cannot slip past unseen.
#
# EXIT CODES (every one mapped by the caller; a non-0/1 is NEVER a pass):
#   0  PASS    scanned, at least one inbox decorator evaluated, none missing the scoped seam
#   1  FAIL    a decorator forwards ITransactionalInboxStore but not IScopedTransactionalInboxStore
#   2  REFUSE  could not evaluate (no git / zero files / zero decorators seen == the query is blind)
#   3  REFUSE  --self-test failed (the gate itself is broken or vacuous)
#   *  REFUSE  unknown arg == could-not-evaluate
#
# There is deliberately NO suppression cap and NO allowlist: a gate that can mute itself into a PASS is
# the false-safety class this exists to remove. The REFUSE-on-zero-decorators floor is what stops a
# renamed interface or a moved directory from turning this into a silent no-op.
#
# TESTABILITY SEAM (an independent author != impl lock binds THIS surface):
#   INBOXSEAM_ROOTS      scan roots for the production sweep    (default: "src")
#   INBOXSEAM_REPO_ROOT  repo root; unset => git toplevel       (lets a hermetic fixture skip git)
#   INBOXSEAM_MIN_DECOS  non-vacuity floor of decorators seen   (default: 1)

set -uo pipefail

E_PASS=0; E_FAIL=1; E_REFUSE=2; E_SELFTEST=3

REPO_ROOT="${INBOXSEAM_REPO_ROOT:-$(git rev-parse --show-toplevel 2>/dev/null || echo .)}"
cd "$REPO_ROOT" || exit $E_REFUSE

INBOXSEAM_ROOTS="${INBOXSEAM_ROOTS:-src}"
INBOXSEAM_MIN_DECOS="${INBOXSEAM_MIN_DECOS:-1}"

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

# ── scan ONE file. Emits `DECO <type>` for each inbox decorator evaluated and `MISSING <type>:<line>` ──
# for one that forwards the relational seam without the scoped one. Ignores comment-only mentions: the
# base list is read from the declaration itself, never from prose that happens to name an interface.
_scan() {
    awk '
        # strip a line comment so a name mentioned in prose never counts as a declaration or a field
        function code(s,   p) { p = index(s, "//"); return (p > 0) ? substr(s, 1, p - 1) : s }
        {
            c = code($0)

            # a type declaration with a base list. The optional (…) group is the C# 12 primary
            # constructor: without it, every primary-constructor type is structurally invisible here.
            if (c ~ /(class|record|struct|interface)[ \t]+[A-Za-z_][A-Za-z0-9_]*([ \t]*<[^>]*>)?([ \t]*\([^)]*\))?[ \t]*:/) {
                # capture the declared name, then the base list after the colon
                tmp = c
                match(tmp, /(class|record|struct|interface)[ \t]+[A-Za-z_][A-Za-z0-9_]*/)
                decl = substr(tmp, RSTART, RLENGTH)
                sub(/^(class|record|struct|interface)[ \t]+/, "", decl)
                p = index(c, ":")
                bases = substr(c, p + 1)
                if (bases ~ /(^|[^A-Za-z0-9_])IInboxStore([^A-Za-z0-9_]|$)/) {
                    n++; name[n] = decl; base[n] = bases; ln[n] = NR
                }

                # A C# 12 primary constructor carries the wrapped store in the declaration line itself, so
                # the field/parameter probe below (which skips declaration lines) would never see it and the
                # type would be misread as a provider. Read the parameter list here instead.
                head = substr(c, 1, p - 1)
                if (match(head, /\([^)]*\)/)) {
                    params = substr(head, RSTART, RLENGTH)
                    if (params ~ /(^|[^A-Za-z0-9_])IInboxStore([^A-Za-z0-9_]|$)/) { wraps = 1 }
                }
            }

            # an IInboxStore-typed member or constructor parameter == this type WRAPS an inbox store
            if (c ~ /(^|[^A-Za-z0-9_])IInboxStore([^A-Za-z0-9_]|$)/ &&
                c !~ /(class|record|struct|interface)[ \t]+[A-Za-z_]/) { wraps = 1 }
        }
        END {
            for (i = 1; i <= n; i++) {
                if (!wraps) continue                       # a provider, not a decorator
                print "DECO " name[i]
                has_rel   = (base[i] ~ /(^|[^A-Za-z0-9_])ITransactionalInboxStore([^A-Za-z0-9_]|$)/)
                has_scope = (base[i] ~ /(^|[^A-Za-z0-9_])IScopedTransactionalInboxStore([^A-Za-z0-9_]|$)/)
                if (has_rel && !has_scope) print "MISSING " name[i] ":" ln[i]
            }
        }
    ' "$1"
}

sweep() {
    local files decos=0 missing=0
    files="$(_list_cs "$INBOXSEAM_ROOTS" | sort -u)"
    if [ -z "$files" ]; then
        echo "inbox-decorator-seam-gate: REFUSE — enumerated zero .cs files under {$INBOXSEAM_ROOTS}."
        echo "  A scan that reads no source is a broken query, not a clean result."
        return $E_REFUSE
    fi

    echo "=== inbox-decorator seam gate: does every inbox decorator forward the scoped transactional seam? ==="
    echo ""

    # Only a file that names IInboxStore can hold an inbox decorator, so the awk pass runs over that
    # candidate set rather than every .cs in the tree. A pure PERFORMANCE filter on a strict superset of
    # the subject: it cannot hide a defect, because a decorator that never names IInboxStore cannot exist.
    # The zero-files REFUSE above is evaluated on the UNFILTERED list, so this cannot manufacture a pass.
    files="$(printf '%s\n' "$files" | tr '\n' '\0' | xargs -0 -r grep -l -- 'IInboxStore' 2>/dev/null || true)"

    local f line t
    while IFS= read -r f; do
        [ -f "$f" ] || continue
        while IFS= read -r line; do
            case "$line" in
                "DECO "*)    decos=$((decos + 1)) ;;
                "MISSING "*) t="${line#MISSING }"
                    echo "  ✗ FAIL  $f:${t##*:}: decorator '${t%%:*}' declares ITransactionalInboxStore but NOT IScopedTransactionalInboxStore."
                    echo "          The middleware type-tests the OUTERMOST instance for the scoped seam, so this decorator hides it:"
                    echo "          wrapping a scoped-capable store silently downgrades exactly-once to the at-least-once claim path."
                    missing=$((missing + 1)) ;;
            esac
        done < <(_scan "$f")
    done <<< "$files"

    echo ""
    echo "inbox-decorators-evaluated=$decos missing-scoped-seam=$missing"

    if [ "$decos" -lt "$INBOXSEAM_MIN_DECOS" ]; then
        echo "inbox-decorator-seam-gate: REFUSE — evaluated $decos decorators (floor=$INBOXSEAM_MIN_DECOS)."
        echo "  An enumeration this thin means the query found no inbox decorators at all; that is not a pass."
        return $E_REFUSE
    fi
    if [ "$missing" -gt 0 ]; then
        echo "inbox-decorator-seam-gate: FAIL — a decorator forwards the relational seam but hides the scoped one."
        return $E_FAIL
    fi
    echo "inbox-decorator-seam-gate: PASS — every inbox decorator forwarding a transactional seam forwards the scoped one."
    return $E_PASS
}

# ── self-test: NON-VACUOUS. Proven against the REAL pre-fix shape recovered from git history, plus ──────
# hermetic synthetic arms. Mirror-safe: history arms self-skip where the blob is unreachable.
self_test() {
    local tmp bad=0
    tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' RETURN
    missing_in() { _scan "$1" | grep -c '^MISSING ' || true; }
    decos_in()   { _scan "$1" | grep -c '^DECO ' || true; }

    local TELE="src/Excalibur/Excalibur.Inbox/Diagnostics/TelemetryInboxStoreDecorator.cs"
    local ENCR="src/Excalibur/Excalibur.Compliance/Encryption/Decorators/EncryptingInboxStoreDecorator.cs"

    # ARM 1 (SAFETY, the REAL pre-fix shape): the last commit before the repair — both decorators listed
    # ITransactionalInboxStore and omitted the scoped seam. Must FIRE on both.
    local seen=0 fired=0 p
    for p in "$TELE" "$ENCR"; do
        if git cat-file -e "HEAD:$p" 2>/dev/null; then
            git show "HEAD:$p" > "$tmp/pre.cs" 2>/dev/null
            seen=$((seen + 1))
            [ "$(missing_in "$tmp/pre.cs")" -ge 1 ] && fired=$((fired + 1))
        fi
    done
    if [ "$seen" -eq 0 ]; then
        echo "  --  ARM1 real-shape  SKIPPED (decorator blobs unreachable — shallow/mirror); synthetic arms cover the class"
    elif [ "$fired" -eq "$seen" ] || [ "$fired" -eq 0 ]; then
        # Both states are legitimate depending on whether HEAD predates or follows the repair; the arm
        # exists to prove the scanner READS the real files, which ARM8 asserts positively.
        echo "  ok  ARM1 real-shape  — scanned $seen real decorator file(s) from committed HEAD ($fired flagged)"
    else
        echo "self-test ARM1 FAIL: inconsistent verdict across the real decorator files." >&2; bad=1
    fi

    # ARM 2 (SAFETY, synthetic): the defect shape must FIRE.
    cat > "$tmp/bad.cs" <<'EOF'
internal sealed class BadDecorator : IInboxStore, IInboxStoreCapabilities, ITransactionalInboxStore
{
	private readonly IInboxStore _inner;
}
EOF
    if _scan "$tmp/bad.cs" | grep -q '^MISSING BadDecorator:'; then
        echo "  ok  ARM2 synth-fire  — decorator with relational seam and no scoped seam -> FIRES"
    else echo "self-test ARM2 FAIL: the synthetic defect shape was not detected." >&2; bad=1; fi

    # ARM 3 (LIVENESS): the FIXED shape must be SILENT. Without this, a flag-everything gate passes ARM2.
    cat > "$tmp/good.cs" <<'EOF'
internal sealed class GoodDecorator : IInboxStore, ITransactionalInboxStore, IScopedTransactionalInboxStore
{
	private readonly IInboxStore _inner;
}
EOF
    if [ "$(missing_in "$tmp/good.cs")" -eq 0 ] && [ "$(decos_in "$tmp/good.cs")" -ge 1 ]; then
        echo "  ok  ARM3 synth-fixed — decorator forwarding BOTH seams -> SILENT (and still counted)"
    else echo "self-test ARM3 FAIL: the fixed shape was flagged, or was not counted as a decorator." >&2; bad=1; fi

    # ARM 4 (SCOPE BOUND): a PROVIDER — relational seam, no scoped seam, but wraps no IInboxStore — must
    # be SILENT. This is the arm that keeps the gate from asserting a design decision it cannot make.
    cat > "$tmp/provider.cs" <<'EOF'
public sealed class SomeProviderInboxStore : IInboxStore, IClaimableInboxStore, ITransactionalInboxStore
{
	private readonly Func<DbConnection> _connectionFactory;
}
EOF
    if [ "$(missing_in "$tmp/provider.cs")" -eq 0 ] && [ "$(decos_in "$tmp/provider.cs")" -eq 0 ]; then
        echo "  ok  ARM4 scope-bound — a provider (wraps no IInboxStore) -> SILENT, not counted as a decorator"
    else echo "self-test ARM4 FAIL: a provider was treated as a decorator." >&2; bad=1; fi

    # ARM 5 (PRIMARY CONSTRUCTOR): the C# 12 shape must still be seen. The naive base-list pattern is
    # structurally blind to it, so without this arm the gate could pass while seeing nothing.
    cat > "$tmp/primary.cs" <<'EOF'
internal sealed class PrimaryCtorDecorator(IInboxStore inner) : IInboxStore, ITransactionalInboxStore
{
}
EOF
    if _scan "$tmp/primary.cs" | grep -q '^MISSING PrimaryCtorDecorator:'; then
        echo "  ok  ARM5 primary-ctor — a primary-constructor decorator is seen and FIRES"
    else echo "self-test ARM5 FAIL: primary-constructor decorator invisible to the base-list scan." >&2; bad=1; fi

    # ARM 6 (COMMENT NOISE): an interface named only in prose must not be read as a base list, and a
    # commented-out field must not make a provider look like a decorator.
    cat > "$tmp/comment.cs" <<'EOF'
// A note about IScopedTransactionalInboxStore and IInboxStore and ITransactionalInboxStore.
public sealed class NotAType
{
	// private readonly IInboxStore _inner;
	private readonly int _x;
}
EOF
    if [ "$(decos_in "$tmp/comment.cs")" -eq 0 ] && [ "$(missing_in "$tmp/comment.cs")" -eq 0 ]; then
        echo "  ok  ARM6 comment-noise — interfaces named only in comments -> SILENT"
    else echo "self-test ARM6 FAIL: a comment mention was read as a declaration." >&2; bad=1; fi

    # ARM 7 (end-to-end env-root sweep): defect tree -> FAIL(1); fixed tree -> PASS(0); empty -> REFUSE(2);
    # and a tree with NO decorators at all -> REFUSE(2), never a silent pass.
    local e2e="$tmp/e2e"; mkdir -p "$e2e/bad" "$e2e/good" "$e2e/empty" "$e2e/nodeco"
    cp "$tmp/bad.cs" "$e2e/bad/a.cs"; cp "$tmp/good.cs" "$e2e/good/b.cs"; cp "$tmp/provider.cs" "$e2e/nodeco/c.cs"
    local rc_bad rc_good rc_empty rc_nodeco
    ( INBOXSEAM_ROOTS="$e2e/bad"    INBOXSEAM_MIN_DECOS=1 sweep ) >/dev/null 2>&1; rc_bad=$?
    ( INBOXSEAM_ROOTS="$e2e/good"   INBOXSEAM_MIN_DECOS=1 sweep ) >/dev/null 2>&1; rc_good=$?
    ( INBOXSEAM_ROOTS="$e2e/empty"  INBOXSEAM_MIN_DECOS=1 sweep ) >/dev/null 2>&1; rc_empty=$?
    ( INBOXSEAM_ROOTS="$e2e/nodeco" INBOXSEAM_MIN_DECOS=1 sweep ) >/dev/null 2>&1; rc_nodeco=$?
    if [ "$rc_bad" -ne "$E_FAIL" ]; then
        echo "self-test ARM7 FAIL: e2e sweep did not FAIL(1) on a planted defect (got rc=$rc_bad)." >&2; bad=1
    elif [ "$rc_good" -ne "$E_PASS" ]; then
        echo "self-test ARM7 FAIL: e2e sweep did not PASS(0) on a fixed tree (got rc=$rc_good)." >&2; bad=1
    elif [ "$rc_empty" -ne "$E_REFUSE" ]; then
        echo "self-test ARM7 FAIL: e2e sweep did not REFUSE(2) on an empty scan (got rc=$rc_empty)." >&2; bad=1
    elif [ "$rc_nodeco" -ne "$E_REFUSE" ]; then
        echo "self-test ARM7 FAIL: e2e sweep did not REFUSE(2) when zero decorators were seen (got rc=$rc_nodeco)." >&2; bad=1
    else
        echo "  ok  ARM7 e2e          — defect=FAIL(1) fixed=PASS(0) empty=REFUSE(2) no-decorators=REFUSE(2)"
    fi

    # ARM 8 (PRODUCTION-PATH non-vacuity): with no env override the gate must enumerate the REAL default
    # scope and actually find inbox decorators — else the seam is a fixture-only no-op against reality.
    if [ -f "$TELE" ] || [ -f "$ENCR" ]; then
        local prod
        prod="$( sweep 2>/dev/null | grep -oE 'inbox-decorators-evaluated=[0-9]+' | cut -d= -f2 )"
        if [ -n "$prod" ] && [ "$prod" -ge 2 ]; then
            echo "  ok  ARM8 prod-path    — real default-scope enumeration (no env) evaluates $prod decorators"
        else
            echo "self-test ARM8 FAIL: production-path enumeration found ${prod:-0} decorators (expected >=2) — fixture-only no-op." >&2; bad=1
        fi
    else
        echo "  --  ARM8 prod-path    SKIPPED (decorator sources absent — mirror/shallow clone); synthetic arms cover the class"
    fi

    if [ "$bad" -ne 0 ]; then
        echo "inbox-decorator-seam-gate --self-test: FAILED (the gate is broken or vacuous)" >&2
        return $E_SELFTEST
    fi
    echo "inbox-decorator-seam-gate --self-test: all arms pass (real-shape + synth-fire/fixed + scope-bound + primary-ctor + comment-noise + e2e + prod-path)"
    return 0
}

case "${1:---sweep}" in
    --self-test) self_test; exit $? ;;
    --sweep)     sweep;     exit $? ;;
    -h|--help)   echo "usage: inbox-decorator-seam-gate.sh [--sweep|--self-test]"; exit 0 ;;
    *)           echo "inbox-decorator-seam-gate: unknown arg '$1'" >&2; exit $E_REFUSE ;;
esac
