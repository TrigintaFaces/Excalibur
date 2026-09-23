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
  # NOTE: there is deliberately NO "public interface with zero implementations" arm here.
  # It was advertised in this comment and in microsoft-first.md §B and implemented in neither.
  # A regex cannot see an implementor in another assembly, a generic implementation, or one reached
  # through a base class, so it would return a confident zero. Use the semantic tool instead:
  #   python3 .claude/tools/csharp-lsp/lsp-query.py --solution Excalibur.sln \
  #           --control <known-good file:line:col> --query <file:line:col> --method implementation
  # The --control is mandatory: an empty result is otherwise indistinguishable from an un-indexed server.
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
    # The REFUSE arm. An empty scope must exit 2, not 0 — this is the arm that proves the scanner
    # cannot report a green it did not earn, and it is the one this self-test previously lacked.
    refuse_rc=0; "$0" --files >/dev/null 2>&1 || refuse_rc=$?
    [ "$refuse_rc" -eq 2 ] || { echo "SELF-TEST FAIL: empty scope exited $refuse_rc, expected REFUSE (2)"; exit 1; }
    echo "$out" | grep -q 'noncsprng-crypto' && echo "$out" | grep -q 'discarded-crypto' \
      && ! (scan_files "$tmp/Good.cs" | grep -q .) \
      && { echo "SELF-TEST PASS (4 of 9 arms have planted fixtures: static-mutable-registry,"
           echo "  noncsprng-crypto, discarded-crypto, base64url-handroll; the other 5 are UNEXERCISED)"
           echo "SELF-TEST PASS (empty scope REFUSES with exit 2)"; exit 0; }
    echo "SELF-TEST FAIL"; exit 1 ;;
  --files)
    shift
    # REFUSE, not PASS. An empty scope means "I measured nothing", which is not a clean result.
    [ "$#" -eq 0 ] && { echo "REFUSE: --files given no files; nothing was examined." >&2; exit 2; }
    scan_files "$@"; exit 0 ;;
  --base)
    base="${2:-origin/main}"
    mapfile -t files < <(git diff --name-only "$base"...HEAD -- '*.cs' 2>/dev/null)
    # REFUSE, not PASS. `--base origin/main` from a local tree at origin/main examines NOTHING, and
    # exiting 0 there published a green nobody earned. A caller that genuinely wants "scan whatever is
    # here" should use --all; a caller that wants a diff must supply a base that has one.
    [ "${#files[@]}" -eq 0 ] && {
      echo "REFUSE: no changed .cs vs $base — nothing was examined. Use --all to scan the tree." >&2
      exit 2
    }
    echo "WWMD smell scan (ADVISORY) — changed .cs vs $base:"
    scan_files "${files[@]}"
    echo "-- triage each with the two-prong test (microsoft-first.md §C); not a verdict --"; exit 0 ;;
  --all|*)
    echo "WWMD smell scan (ADVISORY) — src/**:"
    scan_files src 2>/dev/null
    echo "-- triage each with the two-prong test (microsoft-first.md §C); not a verdict --"; exit 0 ;;
esac
