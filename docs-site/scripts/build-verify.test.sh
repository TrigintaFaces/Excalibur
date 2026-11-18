#!/usr/bin/env bash
# Named failure/success controls: bash docs-site/scripts/build-verify.test.sh
set -euo pipefail
SOURCE="$(cd "$(dirname "$0")" && pwd)/build-verify.sh"
ROOT="$(mktemp -d "${TMPDIR:-/tmp}/build-verify-test-XXXXXX")"
ROOT="$(cd "$ROOT" && pwd -P)"
ROOT_PARENT="$(cd "$(dirname "$ROOT")" && pwd -P)"
ROOT_NAME="$(basename "$ROOT")"
cleanup() {
    local resolved
    resolved="$(cd "$ROOT" && pwd -P)" || return 2
    [ "$resolved" = "$ROOT_PARENT/$ROOT_NAME" ] && [[ "$ROOT_NAME" == build-verify-test-* ]] && [[ "$ROOT_NAME" != */* ]] || return 2
    rm -rf -- "$resolved"
}
trap cleanup EXIT
mkdir -p "$ROOT/site/scripts" "$ROOT/bin"
cp "$SOURCE" "$ROOT/site/scripts/build-verify.sh"
cat > "$ROOT/bin/npx" <<'FAKE'
#!/usr/bin/env bash
set -euo pipefail
[ "$CI" = true ] && [ "$DOCUSAURUS_STRICT_LINKS" = true ]
[ "$#" -eq 4 ] && [ "$1" = docusaurus ] && [ "$2" = build ] && [ "$3" = --out-dir ]
mkdir -p "$4"
printf 'controlled build output\n' > "$4/evidence.txt"
printf '%s\n' "$PWD/$4" > "$CONTROL_OUTPUT"
exit "$CONTROL_EXIT"
FAKE
chmod +x "$ROOT/bin/npx"
export PATH="$ROOT/bin:$PATH"
export CONTROL_OUTPUT="$ROOT/selected-output.txt"
export CONTROL_EXIT=37
set +e
bash "$ROOT/site/scripts/build-verify.sh" > "$ROOT/failure.log" 2>&1
rc=$?
set -e
[ "$rc" -eq 37 ] || { cat "$ROOT/failure.log"; echo "FAIL: child exit37 was not propagated"; exit 1; }
failed_output="$(cat "$CONTROL_OUTPUT")"
[ -f "$failed_output/evidence.txt" ] || { echo "FAIL: failed output was removed"; exit 1; }
grep -F -- "Output retained at $failed_output." "$ROOT/failure.log" > /dev/null
printf 'PASS: failing_build_preserves_output_and_propagates_exit\n'
export CONTROL_EXIT=0
bash "$ROOT/site/scripts/build-verify.sh" > "$ROOT/success.log" 2>&1
success_output="$(cat "$CONTROL_OUTPUT")"
[ ! -e "$success_output" ] || { echo "FAIL: successful output was not cleaned"; exit 1; }
[ -f "$failed_output/evidence.txt" ] || { echo "FAIL: later success removed prior failed output"; exit 1; }
printf 'PASS: successful_build_cleans_only_its_output\n'
