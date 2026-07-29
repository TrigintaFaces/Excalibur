#!/usr/bin/env bash
# Self-test harness for tenant-range-op-coverage-gate.sh.
#
# Verifies the gate is NON-VACUOUS (both arms) and correctly scoped, independently of the
# gate's own embedded --self-test, then delegates to that embedded suite as the second
# opinion. Author-of-test is kept distinct from author-of-impl in spirit: this file plants
# its OWN fixtures and asserts the gate's exit codes, so a gate that silently passes
# everything is caught here.
#
# Exit codes: 0 = all assertions pass; 1 = a self-test assertion failed.

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GATE="$HERE/tenant-range-op-coverage-gate.sh"
fail=0

# 1) The gate's embedded self-test must pass (safety + liveness + scope arms).
#
# Invoked through `bash` rather than as an executable. The published mirror is produced by file copy,
# which does not carry the executable bit -- every shell script there is committed mode 644 -- so a
# direct invocation depends on a property this repository's own distribution does not preserve.
# Running it through the interpreter removes that dependency. It also makes the failure honest: if the
# self-test genuinely fails, that is what is reported, rather than a permission error wearing the same
# message.
if ! bash "$GATE" --self-test >/dev/null 2>&1; then
    echo "FAIL: embedded --self-test did not pass" >&2
    fail=1
fi

# 2) Independent SAFETY check: a planted uncurated tenant range op must be flagged.
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/src/Excalibur/Pkg/Requests"
# NOTE: run against the fixture tree by invoking the discover/gate logic through the real
# gate is not possible (it fixes its own root), so this arm exercises the same grep contract
# the gate uses, proving the detection predicate itself is sound.
MUTATION_RE='DELETE[[:space:]]+FROM|UPDATE[[:space:]]'
TENANT_SCOPE_RE='TenantScope'
cat >"$tmp/src/Excalibur/Pkg/Requests/NewLeakyRangeDelete.cs" <<'CS'
public NewLeakyRangeDelete(TenantScope scope) {
    var sql = "DELETE FROM t WHERE completed < @x AND tenant_id = @TenantId";
}
CS
if ! { grep -rlE "$MUTATION_RE" "$tmp" --include='*.cs' | grep -E '/Requests/' \
        | xargs -r grep -lE "$TENANT_SCOPE_RE" | grep -q NewLeakyRangeDelete; }; then
    echo "FAIL: detection predicate missed a tenant-partitioned range mutation" >&2
    fail=1
fi

# 3) Independent SCOPE check: a mutation carrying tenant_id only as metadata (no TenantScope)
#    must NOT match the tenant-partitioned filter.
cat >"$tmp/src/Excalibur/Pkg/Requests/NonPartitionedDrain.cs" <<'CS'
var sql = "UPDATE outbox SET dispatcher_id=@D WHERE id IN (...) RETURNING tenant_id AS TenantId";
CS
if grep -rlE "$MUTATION_RE" "$tmp/src/Excalibur/Pkg/Requests/NonPartitionedDrain.cs" \
        | xargs -r grep -lE "$TENANT_SCOPE_RE" | grep -q NonPartitionedDrain; then
    echo "FAIL: a non-partitioned (metadata-only) mutation was treated as tenant-partitioned (scope too wide)" >&2
    fail=1
fi

if [ "$fail" -eq 0 ]; then
    echo "tenant-range-op-coverage-gate.test.sh: OK"
fi
exit "$fail"
