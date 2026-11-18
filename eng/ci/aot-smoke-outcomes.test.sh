#!/usr/bin/env bash
# Exercise the actual named workflow run block; no publish or Native execution.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd -P)"
WORKFLOW="${1:-$HERE/../../.github/workflows/aot-validation.yml}"
ROOT="$(mktemp -d "${TMPDIR:-/tmp}/aot-smoke-outcomes-XXXXXX")"
ROOT="$(cd "$ROOT" && pwd -P)"
PARENT="$(cd "$(dirname "$ROOT")" && pwd -P)"
NAME="$(basename "$ROOT")"
cleanup() {
    local resolved
    resolved="$(cd "$ROOT" && pwd -P)" || return 2
    [ "$resolved" = "$PARENT/$NAME" ] && [[ "$NAME" == aot-smoke-outcomes-* ]] && [[ "$NAME" != */* ]] || return 2
    rm -rf -- "$resolved"
}
trap cleanup EXIT
awk '
/^      - name: Run AOT smoke test$/ { count++; active=1; next }
active && /^      - name:/ { active=0 }
active && $0 == "        run: |" { body=1; next }
active && body {
    if ($0 ~ /^          /) { print substr($0,11); lines++ }
    else if ($0 ~ /^[[:space:]]*$/) print ""
    else exit 2
}
END { if (count != 1 || lines == 0) exit 2 }
' "$WORKFLOW" > "$ROOT/run.sh"
BINARY="$ROOT/samples/10-aot/Excalibur.Dispatch.Aot.Sample/bin/Release/net10.0/linux-x64/publish/Excalibur.Dispatch.Aot.Sample"
mkdir -p "$(dirname "$BINARY")"
cat > "$BINARY" <<'FAKE'
#!/usr/bin/env bash
set -euo pipefail
for i in $(seq -w 1 12); do
    case "$CONTROL_CASE:$i" in
        failed:05) echo 'AOT_DEMO_FAIL:05' ;;
        missing:05) continue ;;
        skipped:05) echo 'AOT_DEMO_SKIP:05' ;;
        reordered:05) echo 'AOT_DEMO_PASS:06' ;;
        reordered:06) echo 'AOT_DEMO_PASS:05' ;;
        *) echo "AOT_DEMO_PASS:$i" ;;
    esac
done
[ "$CONTROL_CASE" != duplicate ] || echo 'AOT_DEMO_PASS:05'
exit 0
FAKE
chmod +x "$BINARY"
failures=0
for kind in healthy failed missing duplicate skipped reordered; do
    set +e
    (cd "$ROOT" && CONTROL_CASE="$kind" GITHUB_STEP_SUMMARY="$ROOT/summary.md" bash "$ROOT/run.sh") > "$ROOT/$kind.log" 2>&1
    rc=$?
    set -e
    expected=1
    [ "$kind" != healthy ] || expected=0
    if [ "$rc" -eq "$expected" ]; then
        echo "PASS: actual_workflow_${kind}_outcomes (exit $rc)"
    else
        echo "FAIL: actual_workflow_${kind}_outcomes expected $expected got $rc"
        cat "$ROOT/$kind.log"
        failures=$((failures + 1))
    fi
done
[ "$failures" -eq 0 ]
