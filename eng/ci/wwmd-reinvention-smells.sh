#!/usr/bin/env bash
# WWMD BCL-reinvention smell scanner (ADVISORY — human-triaged, false positives expected).
# Surfaces candidate bucket-(b) reinventions for the reviewer to run the two-prong test against
# (.claude/rules/quality/microsoft-first.md → BCL-Reinvention Catalogue). NEVER auto-blocks.
#
# Usage:
#   eng/ci/wwmd-reinvention-smells.sh --base origin/main   # scan changed *.cs vs base (CI/PR diff)
#   eng/ci/wwmd-reinvention-smells.sh --files a.cs b.cs     # scan an explicit file list (pre-commit staged)
#   eng/ci/wwmd-reinvention-smells.sh --all                # scan all src/**/*.cs
#   eng/ci/wwmd-reinvention-smells.sh --self-test          # prove the scanner is non-vacuous
set -uo pipefail

# name|regex  — each smell maps to a catalogue capability. Tuned for signal; expect FPs.
SMELLS=(
  'static-mutable-registry|public static .*(Dictionary|ConcurrentDictionary|List)<'
  'pool-over-concurrentbag|class .*Pool.*|:.*ConcurrentBag<'
  'reflection-await|MethodInfo|\.GetAwaiter\(\)\.GetResult\(\)'
  'timer-polling|new (System\.Threading\.)?Timer\(|Task\.Delay\([^)]*\).*while|while.*Task\.Delay\('
  'handrolled-retry|for *\(.*attempt|retryCount\+\+|Math\.Pow\(2'
  'noncsprng-crypto|Guid\.NewGuid\(\)\.ToByteArray|new Random\(|Random\.Shared.*(key|secret|token|share|salt|nonce|iv)'
  'discarded-crypto|_ = .*(Reconstruct|Decrypt|Verify|Recover)\('
  'base64url-handroll|\.TrimEnd\(.=.\)\.Replace\(.\+.,|Replace\(./.,'
  'json-reparse-clone|JsonNode\.Parse\(.*ToJsonString\(\)'
)

scan_files() { # $@ = files
  local hit=0
  for entry in "${SMELLS[@]}"; do
    local name="${entry%%|*}" rx="${entry#*|}"
    while IFS= read -r line; do
      [ -n "$line" ] && { printf '  [%s] %s\n' "$name" "$line"; hit=1; }
    done < <(grep -RIn --include='*.cs' -E "$rx" "$@" 2>/dev/null | grep -viE '/tests?/|Tests?\.cs|/samples/')
  done
  # zero-impl public interface heuristic: a new `public interface IXxx` whose name has no `: IXxx` implementor in src
  return $hit
}

case "${1:---all}" in
  --self-test)
    tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
    cat > "$tmp/Bad.cs" <<'CS'
public static Dictionary<string, Type> Registry = new();
var share = Guid.NewGuid().ToByteArray();
_ = ShamirSecretSharing.Reconstruct(shares);
var s = base64.TrimEnd('=').Replace('+','-');
CS
    cat > "$tmp/Good.cs" <<'CS'
services.TryAddSingleton<IFoo, Foo>();
var b = RandomNumberGenerator.GetBytes(32);
var ok = Base64Url.EncodeToString(span);
CS
    out="$(scan_files "$tmp/Bad.cs"; scan_files "$tmp/Good.cs")"
    echo "$out"
    echo "$out" | grep -q 'noncsprng-crypto' && echo "$out" | grep -q 'discarded-crypto' \
      && ! (scan_files "$tmp/Good.cs" | grep -q .) \
      && { echo "SELF-TEST PASS (matches planted smells, ignores clean BCL usage)"; exit 0; }
    echo "SELF-TEST FAIL"; exit 1 ;;
  --files)
    shift
    [ "$#" -eq 0 ] && { exit 0; }
    scan_files "$@"; exit 0 ;;
  --base)
    base="${2:-origin/main}"
    mapfile -t files < <(git diff --name-only "$base"...HEAD -- '*.cs' 2>/dev/null)
    [ "${#files[@]}" -eq 0 ] && { echo "no changed .cs vs $base"; exit 0; }
    echo "WWMD smell scan (ADVISORY) — changed .cs vs $base:"
    scan_files "${files[@]}"
    echo "-- triage each with the two-prong test (microsoft-first.md §C); not a verdict --"; exit 0 ;;
  --all|*)
    echo "WWMD smell scan (ADVISORY) — src/**:"
    scan_files src 2>/dev/null
    echo "-- triage each with the two-prong test (microsoft-first.md §C); not a verdict --"; exit 0 ;;
esac
