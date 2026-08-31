#!/usr/bin/env bash
# tenant-range-op-coverage-gate — completeness guard for tenant-scoped range mutations.
#
# A range DELETE/UPDATE (one that can touch more than a single keyed row) against a
# tenant-partitioned table MUST isolate by tenant, or it reaches another tenant's rows.
# On a DELETE/UPDATE the omission destroys or exposes data rather than merely widening
# a read. The per-request conformance test asserts each such statement emits a
# sanctioned tenant predicate; THIS gate is its load-bearing companion: it proves the
# conformance set is COMPLETE, so a newly-added range mutation on a tenant table cannot
# escape the check by simply not being in the list.
#
# It discovers every data-request source file that (a) contains a range DELETE/UPDATE
# and (b) references a tenant column, then fails if any discovered file is absent from
# the curated coverage manifest below. A curated set with no completeness guard rots the
# moment someone adds a new op — this gate is what makes the curated approach sound.
#
# Two curated classes:
#   RANGE_DESTRUCTIVE — a range DELETE/UPDATE whose tenant isolation is a WHERE predicate.
#                       These are covered by the conformance test (emitted-predicate whitelist).
#   KEYED_UPSERT      — an upsert (MERGE / INSERT..ON CONFLICT) whose match/conflict key
#                       INCLUDES the tenant column, so tenant isolation is structural in the
#                       key, not a separate predicate. Safe by construction; listed here so
#                       the discovery set is fully accounted for.
#
# A discovered tenant-aware mutation file in neither list => the set is incomplete =>
# triage it: add to RANGE_DESTRUCTIVE (and the conformance test) or, if it is a
# tenant-keyed upsert, to KEYED_UPSERT.
#
# Exit codes:
#   0  every discovered tenant-aware mutation is curated
#   1  a discovered tenant-aware mutation is absent from the manifest (gate fail)
#   2  usage / environment error
#   3  --self-test failed (the gate itself is broken / vacuous)
#
# Usage:
#   tenant-range-op-coverage-gate.sh
#   tenant-range-op-coverage-gate.sh --self-test

set -uo pipefail

# shellcheck source=/dev/null
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/gate-denominator.sh"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SEARCH_ROOT_DEFAULT="src/Excalibur"

# --- curated coverage manifest (basenames, provider-agnostic) --------------------------
# RANGE_DESTRUCTIVE: tenant isolation is a WHERE predicate -> conformance-test asserts the
# emitted fragment is one of the sanctioned safe shapes.
RANGE_DESTRUCTIVE=(
    "DeleteSnapshotsRequest.cs"
    "DeleteSnapshotsOlderThanRequest.cs"
    "EraseEventsRequest.cs"
    "PurgeCompletedSagasRequest.cs"
    # Isolates by a WHERE tenant predicate, exactly as this list requires: it takes a
    # KeyedTenantPartition and interpolates
    #     AND COALESCE(TenantId, @UntenantedSentinel) = @TenantId
    # into its DELETE (EventSourcing.SqlServer and .Postgres both — the manifest keys on basename,
    # so this one entry accounts for both). Uncurated until now ONLY because the gate could not see
    # its isolation type; recognising the type is what surfaced it for curation.
    "DeleteEventsUpToVersionRequest.cs"
)
# KEYED_UPSERT: tenant column is part of the MERGE match / ON CONFLICT key -> isolation is
# structural in the key. Not a range predicate; listed so discovery is fully accounted for.
KEYED_UPSERT=(
    "SaveSnapshotRequest.cs"
    "SaveSagaRequest.cs"
)

# A file is "tenant-PARTITIONED" if it flows a `TenantScope` — the isolation seam every
# tenant-partitioned data request takes (required since the tenant-scope hardening). This is
# deliberately NOT "names a tenant column": a store that merely carries `tenant_id` as
# passenger metadata (e.g. the outbox, which stamps tenant onto messages and is drained
# GLOBALLY across tenants by a trusted processor) is not tenant-partitioned and must not be
# flagged. TenantScope-presence is the structural marker for "isolation matters here".
#
# ── TWO TYPES CARRY THE SEAM, AND MATCHING ONLY ONE FAILS CORRECT CODE ───────────────────────────
# `KeyedTenantPartition` is the second isolation type and it must be recognised here. Matching only
# `TenantScope` FAILED two correctly-scoped range deletes at committed HEAD —
# EventSourcing.{SqlServer,Postgres}/Requests/DeleteEventsUpToVersionRequest.cs — each of which takes
# `KeyedTenantPartition tenant` and interpolates
#     AND COALESCE(TenantId, @UntenantedSentinel) = @TenantId
# directly into its DELETE. The isolation was structural; the gate simply could not see the type.
#
# ACCEPTING IT IS A STRENGTHENING, NOT A LOOSENING, and that is why this is safe:
# `KeyedTenantPartition` is DERIVED from `TenantScope` (`FromScope`, KeyedTenantPartition.cs:106),
# shares `TenantScope.UntenantedSentinel`, and — unlike `TenantScope.None` — has NO TERM-LESS
# inhabitant (:10, :17: "always yields a concrete, non-null tenant term"). Both of its inhabitants
# bind a value: `Scoped` binds a real tenant, `Untenanted` binds the reserved sentinel. A range op
# flowing a KeyedTenantPartition therefore CANNOT emit an empty predicate, while one flowing a
# `TenantScope` can, because `TenantScope.None` deliberately emits no term at all. The
# stricter type was the one being rejected.
#
# The negative control is untouched and still governs: a file that merely CARRIES `tenant_id` as
# passenger metadata flows NEITHER type and is still FAILED. This widens the set of recognised
# isolation SEAMS; it does not admit "names a tenant column".
#
# WHY A FALSE POSITIVE HERE WAS THE URGENT HALF: this gate's own failure text recommends
# NON_PARTITIONED_EXEMPT. Applying that to these files would have declared a tenant-partitioned
# table non-partitioned — converting a false alarm into a PERMANENT blind spot on a range DELETE,
# which is strictly worse than the red. A gate that fails correct work teaches the room its RED is
# noise, and the next person under tag pressure routes around it.
TENANT_SCOPE_RE='TenantScope|KeyedTenantPartition'
# A file carries a range mutation if it issues a DELETE or a SET-bearing UPDATE.
MUTATION_RE='DELETE[[:space:]]+FROM|UPDATE[[:space:]]'

# Tenant-STORE packages: their /Requests/ hold the tenant-partitionable tables (events,
# snapshots, sagas). A range mutation in one of these that flows NO TenantScope is the
# forgotten-member case the completeness guard must catch one level up: the primary
# discovery keys on TenantScope-presence, so an op that omits TenantScope ENTIRELY would
# not be discovered and would silently escape. This backstop closes that gap by asserting
# every range mutation in these packages' request files either flows a TenantScope or is
# explicitly exempted (a non-partitioned derived/position table) with a reason.
TENANT_STORE_PKG_RE='Excalibur\.(EventSourcing|Saga)\.(SqlServer|Postgres|Oracle)/'
# Request files in the tenant-store packages that carry a range mutation but are NOT tenant-
# partitioned (derived/position/projection tables with no tenant column) — exempt WITH REASON.
# Empty today: every range mutation in these packages' request files flows a TenantScope.
NON_PARTITIONED_EXEMPT=(
    # e.g. "SomeProjectionCheckpointRequest.cs"  # derived read-model position, no tenant column
)

is_curated() {
    local base="$1" name
    for name in "${RANGE_DESTRUCTIVE[@]}" "${KEYED_UPSERT[@]}"; do
        [ "$base" = "$name" ] && return 0
    done
    return 1
}

is_non_partitioned_exempt() {
    local base="$1" name
    for name in "${NON_PARTITIONED_EXEMPT[@]}"; do
        [ "$base" = "$name" ] && return 0
    done
    return 1
}

# Backstop discovery: request-file range mutations in the tenant-store packages that flow
# NO TenantScope (the forgotten-member case the primary TenantScope-keyed pass is blind to).
discover_scopeless_in_tenant_pkgs() {
    local root="$1"
    grep -rlE "$MUTATION_RE" "$root" --include='*.cs' 2>/dev/null \
        | grep -E '/Requests/' \
        | grep -E "$TENANT_STORE_PKG_RE" \
        | while IFS= read -r f; do
            if ! grep -qE "$TENANT_SCOPE_RE" "$f"; then
                printf '%s\n' "$f"
            fi
        done | sort -u
}

# Discover tenant-aware mutation request files under a search root (default: real tree).
discover() {
    local root="$1"
    # request files that contain a mutation AND reference a tenant column
    grep -rlE "$MUTATION_RE" "$root" --include='*.cs' 2>/dev/null \
        | grep -E '/Requests/' \
        | while IFS= read -r f; do
            if grep -qE "$TENANT_SCOPE_RE" "$f"; then
                printf '%s\n' "$f"
            fi
        done | sort -u
}

run_gate() {
    local root="$1" status=0 f base
    local -a uncovered=()
    local discovered=0
    while IFS= read -r f; do
        [ -z "$f" ] && continue
        discovered=$((discovered + 1))
        base="$(basename "$f")"
        if ! is_curated "$base"; then
            uncovered+=("$f")
            status=1
        fi
    done < <(discover "$root")

    # The DENOMINATOR — what the discovery pass EXAMINED, not only what it flagged. Without it a
    # discovery pattern that stops matching (a renamed request type, a reformatted signature) makes
    # this gate return 0 over an empty set, which is indistinguishable from a fully curated tree.
    # Zero discovered tenant-aware range mutations is a REFUSE, never a pass.
    gate_denominator "$discovered" "tenant-aware range mutation(s) discovered under $root" || return 2

    if [ "${#uncovered[@]}" -gt 0 ]; then
        echo "FAIL: tenant-aware range mutation(s) not in the coverage manifest:" >&2
        printf '  %s\n' "${uncovered[@]}" >&2
        echo "Triage each: add to RANGE_DESTRUCTIVE (+ the conformance test) if it isolates by a" >&2
        echo "WHERE tenant predicate, or to KEYED_UPSERT if the tenant column is in its match key." >&2
        status=1
    fi

    # Backstop: a range mutation in a tenant-store package's /Requests/ that flows NO
    # TenantScope at all. Not caught by the pass above (which only sees TenantScope-flowing
    # ops). Must flow a TenantScope, or be exempted as a non-partitioned table with a reason.
    local -a scopeless=()
    while IFS= read -r f; do
        [ -z "$f" ] && continue
        base="$(basename "$f")"
        is_non_partitioned_exempt "$base" || scopeless+=("$f")
    done < <(discover_scopeless_in_tenant_pkgs "$root")

    if [ "${#scopeless[@]}" -gt 0 ]; then
        echo "FAIL: range mutation(s) in a tenant-store package that flow NO TenantScope:" >&2
        printf '  %s\n' "${scopeless[@]}" >&2
        echo "A range DELETE/UPDATE on a tenant-partitionable table (events/snapshots/sagas) MUST take" >&2
        echo "a TenantScope so isolation is structural. Add the TenantScope parameter, or — if this table" >&2
        echo "is genuinely not tenant-partitioned — add the file to NON_PARTITIONED_EXEMPT with a reason." >&2
        status=1
    fi

    return "$status"
}

self_test() {
    local tmp rc
    tmp="$(mktemp -d)" || { echo "self-test: mktemp failed" >&2; return 3; }
    trap 'rm -rf "$tmp"' RETURN

    # LIVENESS: a tree whose only tenant-partitioned mutation IS curated -> gate PASSES.
    mkdir -p "$tmp/live/Pkg/Requests"
    cat >"$tmp/live/Pkg/Requests/DeleteSnapshotsRequest.cs" <<'CS'
// curated range op — flows a TenantScope (isolation seam)
public DeleteSnapshotsRequest(string aggregateId, TenantScope scope) {
    var sql = "DELETE FROM t WHERE AggregateId = @AggregateId AND TenantId = @TenantId";
}
CS
    if ! run_gate "$tmp/live" >/dev/null 2>&1; then
        echo "self-test FAIL: gate flagged a curated op (false positive / liveness arm)" >&2
        return 3
    fi

    # SAFETY: a planted NEW tenant-partitioned range op NOT in the manifest -> gate FAILS.
    cp -r "$tmp/live" "$tmp/bad"
    cat >"$tmp/bad/Pkg/Requests/PurgeEverythingRequest.cs" <<'CS'
// a new, uncurated range delete on a tenant-partitioned store (takes a TenantScope)
public PurgeEverythingRequest(TenantScope scope) {
    var sql = "DELETE FROM t WHERE CompletedAt < @Threshold AND tenant_id = @TenantId";
}
CS
    if run_gate "$tmp/bad" >/dev/null 2>&1; then
        echo "self-test FAIL: gate PASSED with an uncurated tenant range op (vacuous / safety arm)" >&2
        return 3
    fi

    # NEGATIVE CONTROL: a mutation that only CARRIES tenant_id as metadata (no TenantScope —
    # e.g. the globally-drained outbox) is not tenant-partitioned -> must NOT be flagged.
    cp -r "$tmp/live" "$tmp/oob"
    cat >"$tmp/oob/Pkg/Requests/ReserveOutboxMessages.cs" <<'CS'
// outbox drain: stamps/projects tenant_id as passenger metadata, drained across all tenants;
// carries no isolation-scope type, so it is not tenant-partitioned and is out of scope.
var sql = "UPDATE outbox SET dispatcher_id = @D WHERE id IN (...) RETURNING tenant_id AS TenantId";
CS
    if ! run_gate "$tmp/oob" >/dev/null 2>&1; then
        echo "self-test FAIL: gate flagged a non-partitioned (metadata-only) mutation (scope too wide)" >&2
        return 3
    fi

    # BACKSTOP SAFETY: a range mutation in a tenant-store package's /Requests/ that flows NO
    # TenantScope at all -> gate FAILS (the forgotten-member case one level up).
    local pkg="$tmp/backstop/src/Excalibur.EventSourcing.SqlServer/Requests"
    mkdir -p "$pkg"
    cat >"$pkg/NewScopelessPurgeRequest.cs" <<'CS'
// a new range delete on the snapshots table that FORGOT to take an isolation scope
public NewScopelessPurgeRequest(string aggregateId) {
    var sql = "DELETE FROM EventStoreSnapshots WHERE CreatedAt < @Cutoff";
}
CS
    if run_gate "$tmp/backstop" >/dev/null 2>&1; then
        echo "self-test FAIL: gate PASSED a tenant-store range op that flows no scope (backstop vacuous)" >&2
        return 3
    fi

    # REGRESSION LIVENESS: the exact shape this gate FALSELY FAILED at committed HEAD — a range
    # DELETE in a tenant-store package that flows a `KeyedTenantPartition` (not a `TenantScope`)
    # and interpolates the COALESCE-to-sentinel tenant predicate into its WHERE. This is verbatim
    # the structure of EventSourcing.{SqlServer,Postgres}/Requests/DeleteEventsUpToVersionRequest.cs.
    # RED against the pre-fix single-type regex; GREEN after. Without this arm the widening is
    # unproven and the false positive returns the moment someone "simplifies" the pattern back.
    # The fixture is named for an ALREADY-CURATED entry (DeleteSnapshotsRequest.cs) on purpose. The
    # manifest keys on BASENAME, so naming this fixture after the real file would make the arm pass
    # merely by being curated — vacuous, and it would prove nothing about type recognition. Using a
    # curated name isolates the ONE variable under test: the isolation TYPE, TenantScope vs
    # KeyedTenantPartition. Pre-fix the type is invisible, the file reads as scopeless in a
    # tenant-store package, and the backstop FAILS it; post-fix it is recognised and curated -> GREEN.
    local kpkg="$tmp/keyed/src/Excalibur.EventSourcing.SqlServer/Requests"
    mkdir -p "$kpkg"
    cat >"$kpkg/DeleteSnapshotsRequest.cs" <<'CS'
// range delete scoped by KeyedTenantPartition — the isolation type with NO term-less inhabitant
public DeleteSnapshotsRequest(KeyedTenantPartition tenant, string aggregateId) {
    const string tenantPredicate = " AND COALESCE(TenantId, @UntenantedSentinel) = @TenantId";
    var sql = $"DELETE FROM dbo.EventStoreSnapshots WHERE AggregateId = @AggregateId{tenantPredicate}";
}
CS
    if ! run_gate "$tmp/keyed" >/dev/null 2>&1; then
        echo "self-test FAIL: gate flagged a KeyedTenantPartition-scoped range op — the isolation type is not recognised (false-positive regression)" >&2
        return 3
    fi

    # SAFETY, paired with the arm above: the widening must not have turned into "mentions a tenant
    # word". A range op in a tenant-store package that merely names TenantId in its SQL, while
    # flowing NEITHER isolation type, must still FAIL. This is the arm that fails if someone
    # widens the regex to something like 'Tenant' to make a red go away.
    local mpkg="$tmp/keyed-neg/src/Excalibur.EventSourcing.SqlServer/Requests"
    mkdir -p "$mpkg"
    cat >"$mpkg/ScopelessButMentionsTenantRequest.cs" <<'CS'
// names TenantId in the SQL but takes NO isolation type — still not structurally scoped
public ScopelessButMentionsTenantRequest(string aggregateId) {
    var sql = "DELETE FROM EventStoreSnapshots WHERE CreatedAt < @Cutoff AND TenantId = @TenantId";
}
CS
    if run_gate "$tmp/keyed-neg" >/dev/null 2>&1; then
        echo "self-test FAIL: gate PASSED a range op that only MENTIONS TenantId with no isolation type (widening admitted a tenant WORD, not a SEAM)" >&2
        return 3
    fi

    echo "self-test OK: safety (uncurated -> RED), liveness (curated -> GREEN), scope (non-tenant -> ignored), backstop (scopeless-in-pkg -> RED), keyed-liveness (KeyedTenantPartition -> GREEN), keyed-safety (tenant word without a seam -> RED)"
    return 0
}

# ── TEST SEAM: --root <dir> ─────────────────────────────────────────────────────────────────────
# WHY THIS EXISTS, and it is not a convenience. Without it this gate has no enabling point: the
# search root is anchored to the script's own location (:41) and every argument but --self-test is
# rejected, so an external test CANNOT drive the real gate over a fixture tree. The only thing an
# external test could do was RE-DECLARE the gate's greps and assert against the copy.
#
# That is exactly what happened, and it drifted inside ONE HOUR:
#     gate  TENANT_SCOPE_RE='TenantScope|KeyedTenantPartition'   <- the KeyedTenantPartition fix
#     test  TENANT_SCOPE_RE='TenantScope'                        <- the copy, stale, nothing red
# The test file was certifying a predicate the gate no longer used. A re-implementation is
# guaranteed to drift; the only question was when. The duplication was FORCED by the missing seam,
# not chosen — so the fix belongs here, in the gate, not in the test.
#
# A test that duplicates the matcher it tests cannot detect a widening error in the gate. It can
# only detect that the gate stopped matching what the TEST says — which is a different, useless
# question, and it answers it in the wrong direction.
#
# PRODUCTION IS UNCHANGED AND CANNOT REACH THIS. The no-argument path below still anchors to
# REPO_ROOT and scans SEARCH_ROOT_DEFAULT; CI invokes it with no arguments. `--root` cannot loosen a
# real run — it can only point the gate at a tree the caller supplies, which is the whole point:
# arms drive the REAL greps, the REAL discovery filter, and the REAL manifests over their own
# fixtures, and every duplicated regex in the test file gets deleted.
main() {
    if [ "${1:-}" = "--self-test" ]; then
        self_test; exit $?
    fi
    if [ "${1:-}" = "--root" ]; then
        # Test-only. A missing or non-directory root is a usage error, never a silent pass: a seam
        # that quietly scanned nothing would hand back exit 0 and read as "no violations found".
        [ -n "${2:-}" ] && [ -d "$2" ] || {
            echo "usage: $(basename "$0") --root <existing-directory>" >&2
            exit 2
        }
        cd "$2" || exit 2
        run_gate "."
        exit $?
    fi
    if [ $# -gt 0 ]; then
        echo "usage: $(basename "$0") [--self-test | --root <dir>]" >&2
        exit 2
    fi
    cd "$REPO_ROOT" || exit 2
    run_gate "$SEARCH_ROOT_DEFAULT"
    exit $?
}

main "$@"
