#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
#
# Ratchet on container images pulled at a MUTABLE tag.
#
# WHY. An image tag containing "latest" is a promise, not an identity: the registry can point it at
# different bytes tomorrow, so a build that passed today can fail tomorrow with nothing changed here,
# and the artifact we tested is not necessarily the artifact anyone else gets. Most of these live
# under samples/ and templates/, which SHIP -- a consumer following a sample runs the image it names,
# and that is the one class of staleness we cannot fix for them after the fact.
#
# WHY A RATCHET AND NOT A BAN. There are dozens already, spread across dozens of files, and several
# have no obvious immutable replacement (a vendor that publishes only :latest). Failing the build on
# all of them would mean either a risky mass edit or a gate everyone routes around, and a gate people
# route around is worse than no gate. So: the count may FALL freely, and may not RISE. Adding a new
# mutable-tag image becomes a deliberate act with a visible cost, which is the property that matters.
#
# LOCALLY BUILT IMAGES ARE NOT COUNTED, and this is the load-bearing distinction. `hello-dispatch:latest`
# is produced by `docker build -t` in this repository; it is an output, not a dependency, and it
# cannot drift because nothing external publishes it. Counting it would inflate the baseline with
# entries nobody can ever fix and make the number stop meaning "supply-chain exposure". A pulled
# image is identified by having a registry path or host -- a "/" or a "." before the tag.
#
# EXIT CODES -- distinct on purpose.
#   0  at or below the baseline
#   1  ABOVE the baseline (a new mutable-tag dependency was introduced)
#   3  REFUSE: the population could not be measured, which is not the same as it being clean
set -uo pipefail

BASELINE_FILE="eng/ci/mutable-image-tag-baseline.txt"
SELF_TEST=0
[ "${1:-}" = "--self-test" ] && SELF_TEST=1

# A pulled image reference at a mutable tag. The leading segment must contain "/" or "." so that a
# locally built name (no registry path) does not match. Anchored on the tag containing "latest".
PATTERN='[a-z0-9][a-z0-9._-]*[./][a-z0-9._/-]*:[A-Za-z0-9._-]*latest[A-Za-z0-9._-]*'

count_mutable() {
    local root="${1:-.}"
    if [ "$root" = "." ]; then
        git ls-files '*.cs' '*.yml' '*.yaml' '*Dockerfile*' 2>/dev/null \
            | xargs grep -hoE "$PATTERN" 2>/dev/null \
            | grep -vE '^https?' | wc -l | tr -d '[:space:]'
    else
        grep -rhoE "$PATTERN" "$root" 2>/dev/null \
            | grep -vE '^https?' | wc -l | tr -d '[:space:]'
    fi
}

if [ "$SELF_TEST" -eq 1 ]; then
    tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
    fail=0

    # LIVENESS: a real pulled mutable tag IS counted. Without this the gate could match nothing and
    # report a clean zero forever.
    printf 'image: mcr.microsoft.com/mssql/server:2022-latest\n' > "$tmp/a.yml"
    n="$(count_mutable "$tmp")"
    [ "$n" = "1" ] && echo "  ok  : LIVENESS -- a pulled mutable tag is counted" \
        || { echo "  FAIL: a pulled mutable tag was NOT counted (got $n)"; fail=1; }

    # SAFETY: a LOCALLY BUILT image is not counted. If this regressed, the baseline would silently
    # absorb build outputs and stop measuring supply-chain exposure at all.
    printf 'image: hello-dispatch:latest\ndocker build -t aot-sample:latest .\n' > "$tmp/b.yml"
    rm -f "$tmp/a.yml"
    n="$(count_mutable "$tmp")"
    [ "$n" = "0" ] && echo "  ok  : SAFETY -- a locally built :latest is NOT counted" \
        || { echo "  FAIL: a locally built image was counted (got $n) -- the baseline would measure the wrong thing"; fail=1; }

    # SAFETY: an immutable tag is not counted, or the ratchet could never fall.
    printf 'image: docker.elastic.co/elasticsearch/elasticsearch:9.0.0\n' > "$tmp/c.yml"
    rm -f "$tmp/b.yml"
    n="$(count_mutable "$tmp")"
    [ "$n" = "0" ] && echo "  ok  : SAFETY -- a pinned version tag is NOT counted" \
        || { echo "  FAIL: a pinned tag was counted (got $n)"; fail=1; }

    [ "$fail" -eq 0 ] && { echo "SELF-TEST: the mutable-image-tag ratchet is non-vacuous."; exit 0; }
    echo "SELF-TEST FAILED" >&2; exit 1
fi

if [ ! -f "$BASELINE_FILE" ]; then
    echo "::error::REFUSE: baseline $BASELINE_FILE is missing. Without it this gate cannot tell an improvement from a regression, and a ratchet with no baseline passes everything." >&2
    exit 3
fi
baseline="$(tr -d '[:space:]' < "$BASELINE_FILE")"
case "$baseline" in ''|*[!0-9]*)
    echo "::error::REFUSE: baseline '$baseline' is not a number." >&2; exit 3 ;;
esac

current="$(count_mutable .)"
case "$current" in ''|*[!0-9]*)
    echo "::error::REFUSE: could not measure the current population (got '$current'). Not measured is not the same as clean." >&2; exit 3 ;;
esac

echo "mutable-tag images pulled from a registry: $current (baseline $baseline)"

if [ "$current" -gt "$baseline" ]; then
    echo "::error::A new container image was added at a MUTABLE tag: $current now, baseline $baseline. A 'latest' tag is a promise the registry can re-point, so the image tested is not necessarily the image anyone else gets -- and most of these ship inside samples and templates. Pin it to an immutable version, or raise the baseline deliberately and say why." >&2
    echo "current offenders:" >&2
    git ls-files '*.cs' '*.yml' '*.yaml' '*Dockerfile*' 2>/dev/null \
        | xargs grep -noE "$PATTERN" 2>/dev/null | grep -vE ':https?' | sort -u | head -40 >&2
    exit 1
fi

if [ "$current" -lt "$baseline" ]; then
    echo "::notice::Mutable-tag images fell from $baseline to $current. Lower the baseline in $BASELINE_FILE to lock the improvement in."
fi
exit 0
