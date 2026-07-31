#!/usr/bin/env bash
# Cosmos emulator INFRASTRUCTURE pre-check — REFUSE, never skip.
#
# ⚠ SCOPE, STATED FIRST BECAUSE IT IS NARROWER THAN THE NAME SUGGESTS.
#
# This checks that the RUNNER can pull the image, start the container, and serve the gateway over raw
# HTTP. It does NOT exercise the path the tests take. Every fixture in this repo reaches the emulator
# through Testcontainers' CosmosDbBuilder and a CosmosClient; this script uses docker and curl and
# touches neither.
#
# So a PASS here means "the runner can host the emulator", NOT "the suite's fixtures can use it". Those
# are different claims, and the second is the one the shard depends on. A green from this script must
# never be cited as evidence that the Cosmos tests can run -- proving a path nothing under test takes is
# the same defect as a mocked test certifying a broken provider, and this file is not exempt from it just
# because it is a gate.
#
# The REFUSE direction is still worth having and is sound: if the runner cannot start the container at
# all, nothing downstream can work, and the shard should terminate with a status distinct from both pass
# and ordinary test failure rather than skip its Cosmos tests and report green.
#
# The correct instrument is a probe that goes through CosmosDbBuilder + CosmosClient, so the gate and the
# fixtures share a path. That is tracked; it is not this script.
#
# READINESS IS NOT SUFFICIENCY -- the half this script CAN test. This project has measured an emulator
# that reports ready and cannot create a database, and one that advertises an endpoint the client cannot
# reach. A port that answers proves neither, so the gateway is exercised rather than just probed.
#
# Exit codes are deliberately distinct so a REFUSE is never read as a test failure:
#   0  usable
#   3  REFUSE — infrastructure missing or unusable
set -uo pipefail

# --- self-test -------------------------------------------------------------------------------------
# The REFUSE path is the whole point of this gate, so it must be proven REACHABLE and proven to carry a
# status distinct from a test failure. A gate whose refusal has never been observed is an assumption.
if [ "${1:-}" = "--self-test" ]; then
    st_tmp="$(mktemp -d)"; trap 'rm -rf "$st_tmp"' EXIT
    st_fail=0

    # SAFETY: with no docker on PATH the gate must REFUSE with 3 -- not 0 (a false pass) and not 1
    # (indistinguishable from an ordinary test failure, which is how a missing emulator gets misread
    # as a product defect).
    # Emptying PATH must not hide the interpreter itself: invoking `bash` by name under an empty PATH
    # yields 127 from the SELF-TEST, not from the gate, and reads exactly like the gate misbehaving.
    # Resolve the shell to an absolute path first, so the only thing removed is `docker`.
    st_bash="$(command -v bash)"
    out="$(PATH="$st_tmp" "$st_bash" "$0" 2>&1)"; rc=$?
    if [ "$rc" -eq 3 ] && printf '%s' "$out" | grep -q 'REFUSE'; then
        echo "  safety:   no docker -> exit 3 + REFUSE, distinct from pass(0) and test-failure(1) — PASS"
    else
        echo "  safety:   expected exit 3 with REFUSE, got exit $rc — FAIL"; st_fail=1
    fi

    # LIVENESS: the gate must be CAPABLE of exiting 0. Proven structurally -- a success path exists and
    # is reachable -- because asserting it by running a real emulator would make this arm unrunnable on
    # a box without docker, and an unrunnable arm is the skip-gated-test defect in a new costume.
    if grep -q '^exit 0$' "$0" && grep -q 'gateway serves the account document' "$0"; then
        echo "  liveness: a success path exists and exits 0 — PASS"
    else
        echo "  liveness: no reachable success path; a gate that can only refuse is as broken as one that can only pass — FAIL"; st_fail=1
    fi

    # SAFETY: readiness alone must never be sufficient -- the data-plane check is the lesson from an
    # image that reported ready and could not create a database.
    if grep -q 'account document' "$0"; then
        echo "  safety:   readiness is not treated as sufficiency (data plane is exercised) — PASS"
    else
        echo "  safety:   gate passes on a port answering, which was measured insufficient — FAIL"; st_fail=1
    fi

    if [ "$st_fail" -eq 0 ]; then echo "SELF-TEST PASS (safety + liveness, non-vacuous)"; exit 0; fi
    echo "SELF-TEST FAIL"; exit 2
fi

# The version is ANCHORED, and here it is anchored by digest.
#
# The invariant is "anchor the version", NOT "digests good, tags bad". A version tag is a perfectly good
# anchor and is the right choice for code we SHIP, because a consumer pinned to a digest can never
# receive a fix. The absent anchor is the defect; the form of the anchor is a context decision.
#
# A digest is chosen for THIS file specifically because it is CI-side: a gate's job is to reproduce the
# conditions its evidence was measured under, and a floating reference would let a later run silently
# validate a different image than the one anyone measured.
IMAGE="mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator@sha256:a8b93e25520e999d867ed3949e7de7f4ff3ddab23ca95fa6f90230de5dd9729b"
NAME="cosmosdb-readiness-gate-$$"
READY_TIMEOUT="${COSMOS_READY_TIMEOUT:-300}"

refuse() { echo "::error::REFUSE — $*" >&2; cleanup; exit 3; }
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT

command -v docker >/dev/null 2>&1 || refuse "docker is not available on this runner, so no Cosmos emulator can be provisioned."
docker info >/dev/null 2>&1 || refuse "the docker daemon is not responding on this runner."

echo "Starting Cosmos emulator ($NAME)..."
if ! docker run -d --name "$NAME" -P "$IMAGE" >/dev/null 2>&1; then
    refuse "the Cosmos emulator container could not be started from the pinned image."
fi

# 8081 is the gateway / data plane; 8080 reports health. Poll health, then prove the data plane.
health_port="$(docker port "$NAME" 8080/tcp 2>/dev/null | head -1 | sed 's/.*://')"
gw_port="$(docker port "$NAME" 8081/tcp 2>/dev/null | head -1 | sed 's/.*://')"
[ -n "${gw_port:-}" ] || refuse "the emulator exposed no gateway port; the container is running but unreachable."

deadline=$(( $(date +%s) + READY_TIMEOUT ))
ready=0
while [ "$(date +%s)" -lt "$deadline" ]; do
    if [ -n "${health_port:-}" ] && curl -fsS --max-time 5 "http://127.0.0.1:${health_port}/" 2>/dev/null | grep -qi '"ready"[[:space:]]*:[[:space:]]*true'; then
        ready=1; break
    fi
    # Fall back to the account document on the gateway: if it serves, the data plane is up.
    if curl -fsS --max-time 5 -k "http://127.0.0.1:${gw_port}/" >/dev/null 2>&1; then
        ready=1; break
    fi
    if ! docker ps -q --filter "name=$NAME" | grep -q .; then
        echo "--- emulator container exited; last 40 log lines ---" >&2
        docker logs --tail 40 "$NAME" 2>&1 >&2 || true
        refuse "the Cosmos emulator container exited before becoming ready."
    fi
    sleep 5
done

if [ "$ready" -ne 1 ]; then
    echo "--- emulator did not report ready; last 40 log lines ---" >&2
    docker logs --tail 40 "$NAME" 2>&1 >&2 || true
    refuse "the Cosmos emulator did not become ready within ${READY_TIMEOUT}s on this runner."
fi

# The account document is what the SDK reads first, and what a mis-advertised endpoint breaks.
if ! curl -fsS --max-time 15 "http://127.0.0.1:${gw_port}/" >/dev/null 2>&1; then
    refuse "the emulator reported ready but its gateway did not serve the account document — readiness without a usable data plane is exactly the case this gate exists to catch."
fi

echo "INFRASTRUCTURE OK: the runner started the emulator and its gateway serves the account document (port ${gw_port})."
echo "NOT PROVEN by this step: that the suite's fixtures can reach it. They go through CosmosDbBuilder + CosmosClient; this step does not."
exit 0
