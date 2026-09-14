#!/usr/bin/env bash
#
# Verify the docs site builds, without racing another build in the same working tree.
#
# WHY THIS EXISTS. `docusaurus build` writes into ONE output directory (default: build/). Several
# agents share this tree and each is instructed to build the site after changing docs, so two builds
# can be writing the same files at the same moment. The observed symptom is an ENOENT on a generated
# path that one process expects and the other has replaced -- two runs at the SAME commit on a clean
# tree, one exiting 0 and one exiting 1. That is not a content defect and re-running until green does
# not fix it; it hides it, because every green after a red then means nothing.
#
# Isolating the output makes the collision INEXPRESSIBLE rather than something everyone has to
# remember to coordinate.
#
# WHAT THIS DOES NOT FIX, stated rather than implied: the generated cache at docs-site/.docusaurus is
# still shared, and Docusaurus 3 exposes no flag to relocate it. This closes the output-directory
# collision, which is where the observed failure occurred. It is not a proof that concurrent builds
# are now safe in every respect.
#
# The default build/ is deliberately untouched: CI and the deploy job publish from it and must stay
# predictable.
#
set -uo pipefail

cd "$(dirname "$0")/.." || exit 2

# $$ is this shell's PID: unique per invocation, so two concurrent verifications cannot collide.
# Kept inside docs-site because --out-dir is resolved relative to the site directory.
OUT="build-verify-$$"
trap 'rm -rf "$OUT"' EXIT

# CI=true makes Docusaurus treat broken links as errors rather than warnings, matching what the
# deploy job does -- a verification that is more permissive than CI verifies the wrong thing.
CI=true DOCUSAURUS_STRICT_LINKS=true npx docusaurus build --out-dir "$OUT"
rc=$?

if [ "$rc" -ne 0 ]; then
	echo "::error::docs-site build FAILED (exit $rc). Output was isolated in $OUT; it has been removed."
	exit "$rc"
fi

echo "docs-site build: PASS (isolated output, no shared-directory collision possible)"
exit 0
