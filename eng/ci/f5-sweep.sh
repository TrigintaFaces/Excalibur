#!/usr/bin/env bash
# f5-sweep — F-5 cross-project sibling-sweep mechanical pre-REVIEW gate.
#
# Implements the mechanical gate mandated by
#   .claude/rules/process/f5-cross-project-test-sweep.md  (S854 RETRO rec #1, bd-0i2jsu)
#
# On a type-contract change (a method/property/enum/sentinel/DTO-shape/schema-column
# change in src/**), tests that assert the OLD contract MUST be swept across the
# ENTIRE test tree — across ALL tests/** projects + consumer mocks — and triaged
# BEFORE REVIEW. The recurring footgun (S840->S854, 7+ sprints) is grepping only the
# obvious sibling test (or only the expected project) and letting the full-CI shard
# run find the rest, late.
#
# This script extracts type-contract tokens from the changed src/** lines and greps
# them across tests/**, listing every test file that references a changed token but
# was NOT itself changed in the same set — i.e. candidate stale siblings to triage.
#
# Exit codes:
#   0  no un-triaged sibling hits (or --report-only)
#   1  un-triaged sibling hits found (gate fail — triage each per the F-5 rule)
#   2  usage / environment error
#   3  --self-test failed (the gate itself is broken / vacuous)
#
# Usage:
#   f5-sweep.sh [--base <ref>] [--staged] [--working] [--all] [--report-only]
#   f5-sweep.sh --self-test
#
set -uo pipefail

# ---------------------------------------------------------------------------
# Configuration / tokens
# ---------------------------------------------------------------------------

# High-noise identifiers that are language/framework boilerplate, never a useful
# contract token to sweep on. Kept deliberately small (over-listing is safer than
# under-listing for a triage aid).
F5_STOPLIST='^(Task|ValueTask|Async|CancellationToken|String|Boolean|Int32|Int64|Guid|DateTime|DateTimeOffset|TimeSpan|Exception|Object|Nullable|List|Dictionary|IEnumerable|IReadOnlyList|IReadOnlyCollection|IReadOnlyDictionary|IList|ICollection|IDictionary|Func|Action|Type|Void|Get|Set|Add|New|The|And|For|Not|Null|True|False|This|Base|Override|Virtual|Public|Private|Internal|Protected|Static|Readonly|Const|Class|Struct|Record|Enum|Interface|Namespace|Using|Return|Await|Throw|Should|ShouldBe|ShouldBeNull|ShouldNotBeNull|ShouldBeTrue|ShouldBeFalse|Assert|Fact|Theory|Trait|System|Microsoft|Excalibur|Dispatch'\
'|ConfigureAwait|GetAwaiter|GetResult|ContinueWith|ToString|GetHashCode|Equals|Dispose|DisposeAsync|ToList|ToArray|ToDictionary|ToHashSet|AsSpan|AsMemory'\
'|AddSingleton|AddScoped|AddTransient|TryAddSingleton|TryAddScoped|TryAddTransient|TryAddEnumerable|TryAdd|AddOptions|AddLogging|Configure|PostConfigure|GetService|GetRequiredService|GetServices|BuildServiceProvider'\
'|Invoke|InvokeAsync|FromResult|FromException|CompletedTask|WhenAll|WhenAny|Delay|Yield|Run|Wait|Parse|TryParse|TryGetValue|ContainsKey|Create|CreateLogger|Build|Append|AppendLine|Format|Concat|Join'\
'|Contains|Select|SelectMany|Where|Any|All|First|FirstOrDefault|Single|SingleOrDefault|Last|LastOrDefault|Count|Sum|Max|Min|Cast|OfType|OrderBy|GroupBy|Distinct|Skip|Take|Aggregate|Reverse'\
'|StartsWith|EndsWith|Substring|Replace|Split|Trim|ToLower|ToUpper|ToLowerInvariant|ToUpperInvariant|Console|WriteLine|NameOf|TypeOf|SuppressFinalize|MemberwiseClone'\
'|InvalidOperationException|ArgumentNullException|ArgumentException|ArgumentOutOfRangeException|NotSupportedException|NotImplementedException|ObjectDisposedException|OperationCanceledException|TimeoutException|AggregateException'\
'|IsNullOrEmpty|IsNullOrWhiteSpace|ThrowIfNull|ThrowIfCancellationRequested|ToUnixTimeMilliseconds|ToUnixTimeSeconds|FromUnixTimeMilliseconds|FromUnixTimeSeconds|UtcNow|Now'\
'|Source|Target|Window|Value|Values|Name|Names|Result|Results|Context|Item|Items|Entry|Entries|Default|Defaults|Current|Previous|Next|Mode|State|Status|Count|Size|Length|Index|Path|Reason|Detail|Details|Data|Info|Config|Settings|Message|Messages|Event|Events|Request|Response'\
'|Handler|Provider|Factory|Manager|Service|Services|Client|Builder|Options|Args|Header|Headers|Body|Payload|Content|Field|Fields|Record|Records|Entity|Model|Query|Command|Action|Group|Batch|Buffer|Cache|Pool|Queue|Stream|Channel|Topic|Partition|Offset|Position|Token|Tokens|Label|Tag|Tags|Kind|Level|Code'\
'|Regex|GeneratedRegex|IsMatch|Match|Matches|Groups|Compile|Encoding|Convert|Buffer|Span|Memory|Array|Enumerable|Linq)$'

# ---------------------------------------------------------------------------
# Core (pure, testable) functions
# ---------------------------------------------------------------------------

# f5_extract_tokens
#   stdin : a unified git diff (or any text whose +/- lines hold changed code)
#   stdout: candidate type-contract tokens, one per line, sorted-unique
#
# Extracts from changed content lines (those starting with a single + or -) ONLY
# high-signal *contract* tokens — deliberately NOT every PascalCase identifier on a
# changed line (that drowns the report in usages). Sources:
#   1. double-quoted string literals that are SENTINEL-SHAPED — contain '_' / '$' / a
#      digit, or are SCREAMING_CASE — e.g. "$erased", "ALREADY_EXISTS", "wal_level".
#      Plain English words ("window", "source") are NOT contract tokens and are dropped
#      (bd-tak38o precision fix: they matched ~every test → 66k false positives).
#   2. declared type names                      (class|interface|enum|record|struct Name)
#   3. member NAMES in declaration context:
#        a. a method/property/field name on a line carrying a visibility/modifier
#           keyword (public/internal/protected/private/const/static/...)
#        b. a method name immediately preceding '(' — captures interface/abstract
#           signatures that have no visibility modifier (e.g. GetAllGrantsAsync(...))
#        c. an enum member: a leading PascalCase identifier followed by ',' or '='
#   4. multi-line signature-opener recovery: a ctor/method name on an UNCHANGED context
#      line whose parameter list has a changed line below it (the jv02p5+15sf7a miss — a
#      ctor-arity change whose type-name line is context, so target-typed `new(...)` call
#      sites were never swept).
# then drops the stoplist boilerplate.
f5_extract_tokens() {
    local input changed
    input="$(cat)"
    # changed content lines only (exclude the +++/--- file headers)
    changed="$(printf '%s\n' "$input" | grep -E '^[+-]' | grep -Ev '^(\+\+\+|---)' | sed -E 's/^[+-]//' || true)"

    {
        # 1) string-literal sentinels / column names / keys — SENTINEL-SHAPED ONLY
        #    (contains _ or $ or a digit, OR is SCREAMING_CASE). Drops plain English words.
        printf '%s\n' "$changed" \
            | grep -oE '"[A-Za-z_$][A-Za-z0-9_$]{2,}"' \
            | sed -E 's/^"//; s/"$//' \
            | grep -E '(_|\$|[0-9]|^[A-Z][A-Z0-9]+$)'

        # 2) declared type names
        printf '%s\n' "$changed" \
            | grep -oE '\b(class|interface|enum|record|struct)[[:space:]]+[A-Z][A-Za-z0-9_]{2,}' \
            | sed -E 's/^(class|interface|enum|record|struct)[[:space:]]+//'

        # 3a) member names on lines carrying a visibility/modifier keyword
        printf '%s\n' "$changed" \
            | grep -E '\b(public|internal|protected|private|const|static|abstract|virtual|override|sealed|readonly|async)\b' \
            | grep -oE '\b[A-Z][A-Za-z0-9_]{2,}\b'

        # 3b) method name immediately before '(' (interface/abstract signatures)
        printf '%s\n' "$changed" \
            | grep -oE '\b[A-Z][A-Za-z0-9_]{2,}[[:space:]]*\(' \
            | sed -E 's/[[:space:]]*\($//'

        # 3c) enum members: leading PascalCase identifier followed by ',' or '='
        printf '%s\n' "$changed" \
            | grep -oE '^[[:space:]]*[A-Z][A-Za-z0-9_]{2,}[[:space:]]*[,=]' \
            | grep -oE '[A-Z][A-Za-z0-9_]{2,}'

        # 4) multi-line signature opener recovery (root cause of the jv02p5+15sf7a misses).
        #    A ctor/method signature spanning multiple lines:
        #        public MyStore(            <- the TYPE/METHOD NAME line (often UNCHANGED context)
        #            IFoo foo,
        #    +       IBar bar,              <- the only CHANGED line is a parameter
        #            ILogger logger)
        #    Passes 1-3 only see CHANGED lines, so the name 'MyStore' is never extracted -> no token is
        #    swept -> EVERY sibling construction site is missed, including target-typed `new(...)` call
        #    sites that never spell the type. This stateful pass scans the RAW diff (with +/-/space
        #    prefixes and hunk context), tracks each open `Name(` signature, and emits Name when ANY
        #    line inside that signature was changed — recovering the name from unchanged-context openers.
        printf '%s\n' "$input" \
            | awk '
                /^(\+\+\+|---|@@)/ { insig=0; signame=""; sigchg=0; next }   # reset at hunk boundaries
                {
                    pfx=substr($0,1,1); content=substr($0,2)
                    changed=(pfx=="+" || pfx=="-")
                    if (insig==0) {
                        if (match(content, /[A-Za-z_][A-Za-z0-9_]*[ \t]*\(/)) {
                            tok=substr(content, RSTART, RLENGTH)
                            sub(/[ \t]*\($/, "", tok)
                            rest=substr(content, RSTART+RLENGTH-1)   # from the "(" onward
                            # Only a multi-line opener (no ")" closing it on this line) with a
                            # type/method-shaped PascalCase name.
                            if (tok ~ /^[A-Z][A-Za-z0-9_][A-Za-z0-9_]+$/ && index(rest, ")")==0) {
                                insig=1; signame=tok; sigchg=(changed ? 1 : 0); next
                            }
                        }
                    } else {
                        if (changed) sigchg=1
                        if (index(content, ")") > 0) {
                            if (sigchg && signame != "") print signame
                            insig=0; signame=""; sigchg=0
                        }
                    }
                }'
    } 2>/dev/null \
        | grep -Ev "$F5_STOPLIST" \
        | sort -u
}

# f5_find_siblings <token> <changed-files-list-file> <search-root>
#   Greps <search-root> (recursively, *.cs only) for the literal <token> and prints
#   every matching file that is NOT listed in <changed-files-list-file>.
#   Output: one "file" path per line.
f5_find_siblings() {
    local token="$1" changed_list="$2" root="$3"
    [ -d "$root" ] || return 0

    # -F literal, -w word-boundary, -I skip binaries, -l names only, -r recursive
    local hits
    hits="$(grep -rIlwF --include='*.cs' -- "$token" "$root" 2>/dev/null || true)"
    [ -n "$hits" ] || return 0

    local f norm
    while IFS= read -r f; do
        [ -n "$f" ] || continue
        norm="${f#./}"
        # exclude files that ARE in the changeset (already-triaged by construction)
        if grep -qxF -- "$norm" "$changed_list" 2>/dev/null; then
            continue
        fi
        printf '%s\n' "$norm"
    done <<< "$hits"
}

# ---------------------------------------------------------------------------
# Real run
# ---------------------------------------------------------------------------

f5_changed_src_files() {
    # Prints changed src/**/*.cs paths (excluding deletions) for the selected mode.
    local mode="$1" base="$2"
    {
        case "$mode" in
            committed) git diff "$base...HEAD" --name-only --diff-filter=d -- 'src/**/*.cs' ;;
            staged)    git diff --cached        --name-only --diff-filter=d -- 'src/**/*.cs' ;;
            working)   git diff                 --name-only --diff-filter=d -- 'src/**/*.cs' ;;
            all)
                git diff "$base...HEAD"  --name-only --diff-filter=d -- 'src/**/*.cs'
                git diff --cached        --name-only --diff-filter=d -- 'src/**/*.cs'
                git diff                 --name-only --diff-filter=d -- 'src/**/*.cs'
                ;;
        esac
    } 2>/dev/null | sed 's#^\./##' | sort -u
}

f5_changed_test_files() {
    # Test files already touched in this change set — triaged by construction.
    local mode="$1" base="$2"
    {
        case "$mode" in
            committed) git diff "$base...HEAD" --name-only --diff-filter=d -- 'tests/**/*.cs' ;;
            staged)    git diff --cached        --name-only --diff-filter=d -- 'tests/**/*.cs' ;;
            working)   git diff                 --name-only --diff-filter=d -- 'tests/**/*.cs' ;;
            all)
                git diff "$base...HEAD"  --name-only --diff-filter=d -- 'tests/**/*.cs'
                git diff --cached        --name-only --diff-filter=d -- 'tests/**/*.cs'
                git diff                 --name-only --diff-filter=d -- 'tests/**/*.cs'
                ;;
        esac
    } 2>/dev/null | sed 's#^\./##' | sort -u
}

f5_diff_text() {
    local mode="$1" base="$2"
    case "$mode" in
        committed) git diff "$base...HEAD" -- 'src/**/*.cs' ;;
        staged)    git diff --cached        -- 'src/**/*.cs' ;;
        working)   git diff                 -- 'src/**/*.cs' ;;
        all)
            git diff "$base...HEAD"  -- 'src/**/*.cs'
            git diff --cached        -- 'src/**/*.cs'
            git diff                 -- 'src/**/*.cs'
            ;;
    esac 2>/dev/null
}

run_sweep() {
    local mode="$1" base="$2" report_only="$3"

    if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
        echo "f5-sweep: error — not inside a git work tree" >&2
        return 2
    fi

    local changed_src
    changed_src="$(f5_changed_src_files "$mode" "$base")"
    if [ -z "$changed_src" ]; then
        echo "f5-sweep: no changed src/**/*.cs files for mode=$mode (base=$base) — nothing to sweep."
        return 0
    fi

    # Files considered already-triaged: the changed src + any changed test files.
    local changed_list
    changed_list="$(mktemp)"
    { printf '%s\n' "$changed_src"; f5_changed_test_files "$mode" "$base"; } \
        | grep -v '^$' | sort -u > "$changed_list"

    local tokens_file
    tokens_file="$(mktemp)"
    f5_diff_text "$mode" "$base" | f5_extract_tokens > "$tokens_file"

    echo "=== F-5 cross-project sibling sweep (mode=$mode, base=$base) ==="
    echo "Changed src files:"
    printf '  %s\n' $changed_src
    local ntok
    ntok="$(grep -c . "$tokens_file" || true)"
    echo "Contract tokens extracted: ${ntok}"
    echo ""

    if [ "$ntok" -eq 0 ]; then
        echo "✅ No contract tokens extracted from the changed src — nothing to sweep. F-5 clean."
        rm -f "$changed_list" "$tokens_file"
        return 0
    fi

    # Single batched pass: grep ALL tokens across tests/** at once (whole-word,
    # fixed-strings). The file list comes from `git ls-files` (the git index — no
    # filesystem walk over bin/obj/TestResults, tracked .cs only), so this is fast
    # and O(tree) not O(tokens*tree). Output lines: "path:token".
    local raw=""
    if [ -s "$tokens_file" ]; then
        raw="$(git ls-files -z -- 'tests/*.cs' 'tests/**/*.cs' 2>/dev/null \
                | xargs -0 grep -IowHF -f "$tokens_file" 2>/dev/null || true)"
    fi

    # Group hits by token, excluding files already in the changeset (triaged).
    local hit_report total_hits=0
    hit_report="$(mktemp)"
    if [ -n "$raw" ]; then
        # Normalize "path:token" -> drop files already in the changeset (triaged) ->
        # emit "token<TAB>path", sort by token. Single awk pass (no per-line subprocess —
        # the changed-file set is loaded as an exclusion map from changed_list).
        local pairs
        pairs="$(printf '%s\n' "$raw" \
            | awk -F: 'NF>=2 { tok=$NF; sub(/:[^:]*$/, "", $0); path=$0; sub(/^\.\//,"",path); print tok "\t" path }' \
            | sort -u \
            | awk -F'\t' -v cl="$changed_list" '
                BEGIN { while ((getline l < cl) > 0) excl[l]=1 }
                !($2 in excl) { print }')"
        if [ -n "$pairs" ]; then
            # Per-token hit cap: a token matching more than $cap test files is almost certainly
            # generic (not a real contract token) — SUPPRESS it (one-line note) so a leaked generic
            # token can't reproduce the bd-tak38o 66k-line blowout, and don't let it gate.
            # `pairs` is already grouped by token (sorted upstream). awk emits "actionable<TAB>suppressed".
            local cap="${F5_MAX_HITS_PER_TOKEN:-25}" summary
            summary="$(printf '%s\n' "$pairs" | awk -F'\t' -v cap="$cap" -v rep="$hit_report" '
                function flush(t) {
                    if (t == "") return
                    if (c > cap) {
                        printf("TOKEN %c%s%c — %d hits (> %d; likely generic, refine extraction) — SUPPRESSED\n", 39, t, 39, c, cap) >> rep
                        supp++
                    } else {
                        printf("TOKEN %c%s%c — un-triaged sibling test files:\n", 39, t, 39) >> rep
                        printf("%s", buf) >> rep
                        act += c
                    }
                }
                $1 != prev { flush(prev); prev = $1; c = 0; buf = "" }
                { c++; buf = buf "    " $2 "\n" }
                END { flush(prev); printf("%d\t%d", act, supp) }')"
            total_hits="${summary%%	*}"
            suppressed_tokens="${summary##*	}"
        fi
    fi

    if [ "${total_hits:-0}" -eq 0 ]; then
        echo "✅ No un-triaged sibling test references to changed contract tokens. F-5 clean."
        [ "${suppressed_tokens:-0}" -gt 0 ] && \
            echo "   (${suppressed_tokens} generic token(s) suppressed as >$cap hits — refine extraction if any was a real contract token.)"
        rm -f "$changed_list" "$tokens_file" "$hit_report"
        return 0
    fi

    cat "$hit_report"
    echo ""
    echo "⚠️  ${total_hits} un-triaged sibling hit(s) across tests/**."
    [ "${suppressed_tokens:-0}" -gt 0 ] && \
        echo "    (${suppressed_tokens} generic token(s) suppressed as >$cap hits — not counted; refine extraction if any was real.)"
    echo "    Per .claude/rules/process/f5-cross-project-test-sweep.md: triage EACH before REVIEW —"
    echo "    flip the stale assertion to the new contract (strengthen, never weaken), or document why unaffected."
    rm -f "$changed_list" "$tokens_file" "$hit_report"

    [ "$report_only" = "1" ] && return 0
    return 1
}

# ---------------------------------------------------------------------------
# Self-test (non-vacuous: MUST flag the footgun, MUST ignore triaged/unrelated)
# ---------------------------------------------------------------------------

self_test() {
    local tmp pass=1
    tmp="$(mktemp -d)"
    # shellcheck disable=SC2064
    trap "rm -rf '$tmp'" RETURN

    # --- Fixture 1: token extraction ---------------------------------------
    # Simulate a diff that renames a method (3-arg overload) and changes a sentinel.
    local diff_fixture
    diff_fixture="$(cat <<'EOF'
diff --git a/src/Foo/GrantStore.cs b/src/Foo/GrantStore.cs
--- a/src/Foo/GrantStore.cs
+++ b/src/Foo/GrantStore.cs
-    public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken ct);
+    public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, bool includeExpired, CancellationToken ct);
-    private const string ErasedMarker = "$erased";
+    private const string ErasedMarker = "$tombstone";
+    public ChangeFeedSource Source { get; init; }
+    private const string PollSetting = "window";
EOF
)"
    local tokens
    tokens="$(printf '%s\n' "$diff_fixture" | f5_extract_tokens)"

    # MUST extract the renamed member and the sentinel literals.
    if ! printf '%s\n' "$tokens" | grep -qx 'GetAllGrantsAsync'; then
        echo "self-test FAIL: did not extract member token 'GetAllGrantsAsync'" >&2; pass=0
    fi
    if ! printf '%s\n' "$tokens" | grep -qx '\$erased'; then
        echo "self-test FAIL: did not extract sentinel literal '\$erased'" >&2; pass=0
    fi
    # MUST drop stoplist boilerplate.
    if printf '%s\n' "$tokens" | grep -qx 'CancellationToken'; then
        echo "self-test FAIL: stoplist token 'CancellationToken' leaked through" >&2; pass=0
    fi
    # NON-VACUOUS IN BOTH DIRECTIONS (bd-tak38o): generic tokens MUST be dropped, else the
    # gate flags ~every test (the 66k-false-positive failure mode).
    if printf '%s\n' "$tokens" | grep -qx 'window'; then
        echo "self-test FAIL: generic string-literal 'window' leaked (not sentinel-shaped)" >&2; pass=0
    fi
    if printf '%s\n' "$tokens" | grep -qx 'Source'; then
        echo "self-test FAIL: generic stoplisted token 'Source' leaked through" >&2; pass=0
    fi

    # --- Fixture 1b: multi-line signature recovery (nwiqnp / jv02p5+15sf7a) -
    # A ctor whose NAME line is unchanged context and whose ONLY changed line is a parameter.
    # Passes 1-3 see no type token; pass 4 MUST recover 'MultiLineStore' so its (possibly
    # target-typed `new(...)`) construction sites get swept. A second signature wholly in
    # context (no +/- inside) MUST NOT be emitted (non-vacuity — don't flood every signature).
    local ml_fixture ml_tokens
    ml_fixture="$(cat <<'EOF'
diff --git a/src/Foo/MultiLineStore.cs b/src/Foo/MultiLineStore.cs
--- a/src/Foo/MultiLineStore.cs
+++ b/src/Foo/MultiLineStore.cs
@@ -10,7 +10,8 @@
     public MultiLineStore(
         IFoo foo,
+        IBar bar,
         ILogger logger)
     {
     }
@@ -40,6 +41,6 @@
     public UntouchedSignature(
         int alpha,
         int beta)
EOF
)"
    ml_tokens="$(printf '%s\n' "$ml_fixture" | f5_extract_tokens)"
    if ! printf '%s\n' "$ml_tokens" | grep -qx 'MultiLineStore'; then
        echo "self-test FAIL: pass 4 did not recover multi-line signature name 'MultiLineStore' from an unchanged-context opener (the jv02p5+15sf7a miss)" >&2; pass=0
    fi
    if printf '%s\n' "$ml_tokens" | grep -qx 'UntouchedSignature'; then
        echo "self-test FAIL: pass 4 emitted 'UntouchedSignature' whose signature had NO changed line (vacuous over-emission)" >&2; pass=0
    fi

    # --- Fixture 2: sibling detection (the real footgun) -------------------
    # tests/ProjA = the obvious sibling, updated alongside (triaged).
    # tests/ProjB = a SECOND project with a stale mock — the F-5 miss.
    # tests/ProjC = unrelated test, must not be flagged.
    mkdir -p "$tmp/tests/ProjA" "$tmp/tests/ProjB" "$tmp/tests/ProjC"
    cat > "$tmp/tests/ProjA/GrantStoreShould.cs" <<'EOF'
public class GrantStoreShould { void X() { store.GetAllGrantsAsync("u", true, ct); } }
EOF
    cat > "$tmp/tests/ProjB/GovernanceMock.cs" <<'EOF'
public class GovernanceMock { Task GetAllGrantsAsync(string u, CancellationToken ct) => Task.CompletedTask; }
EOF
    cat > "$tmp/tests/ProjC/UnrelatedShould.cs" <<'EOF'
public class UnrelatedShould { void Y() { svc.DoSomethingElse(); } }
EOF

    # changed-set lists ProjA (the obvious sibling) as already triaged.
    local changed_list="$tmp/changed.txt"
    printf '%s\n' "tests/ProjA/GrantStoreShould.cs" > "$changed_list"

    # Run sibling finder rooted at the fixture tree.
    local siblings
    siblings="$( cd "$tmp" && f5_find_siblings 'GetAllGrantsAsync' "$changed_list" 'tests' )"

    # MUST flag ProjB (stale, second-project, not in changeset).
    if ! printf '%s\n' "$siblings" | grep -q 'tests/ProjB/GovernanceMock.cs'; then
        echo "self-test FAIL: did NOT flag the stale second-project sibling tests/ProjB" >&2; pass=0
    fi
    # MUST NOT flag ProjA (triaged — in the changeset).
    if printf '%s\n' "$siblings" | grep -q 'tests/ProjA/'; then
        echo "self-test FAIL: flagged the already-triaged sibling tests/ProjA (false positive)" >&2; pass=0
    fi
    # MUST NOT flag ProjC (unrelated).
    if printf '%s\n' "$siblings" | grep -q 'tests/ProjC/'; then
        echo "self-test FAIL: flagged unrelated test tests/ProjC (false positive)" >&2; pass=0
    fi

    # --- Fixture 3: word-boundary (no substring false-positives) -----------
    mkdir -p "$tmp/tests/ProjD"
    cat > "$tmp/tests/ProjD/SubstringShould.cs" <<'EOF'
public class SubstringShould { void Z() { obj.GetAllGrantsAsyncCompat(); } }
EOF
    local subhits
    subhits="$( cd "$tmp" && f5_find_siblings 'GetAllGrantsAsync' "$changed_list" 'tests' )"
    if printf '%s\n' "$subhits" | grep -q 'tests/ProjD/'; then
        echo "self-test FAIL: word 'GetAllGrantsAsync' matched substring 'GetAllGrantsAsyncCompat'" >&2; pass=0
    fi

    if [ "$pass" -eq 1 ]; then
        echo "✅ f5-sweep self-test PASSED (non-vacuous: flags the stale 2nd-project sibling, ignores triaged/unrelated/substring)."
        return 0
    fi
    echo "❌ f5-sweep self-test FAILED." >&2
    return 3
}

# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

usage() {
    sed -n '2,40p' "$0" | sed 's/^# \{0,1\}//'
}

main() {
    # Default = committed change vs base (base...HEAD) — NOT the dirty working tree.
    # On a shared multi-worker tree, diffing the working tree pulls every agent's uncommitted
    # WIP and explodes the token set (bd-tak38o: 66,982 FP). The pre-REVIEW scope is the branch's
    # own committed change; use --staged / --working / --all only for local pre-commit checks.
    local mode="committed" base="" report_only="0"
    while [ $# -gt 0 ]; do
        case "$1" in
            --self-test) self_test; exit $? ;;
            --base)      base="$2"; shift 2 ;;
            --committed) mode="committed"; shift ;;
            --staged)    mode="staged"; shift ;;
            --working)   mode="working"; shift ;;
            --all)       mode="all"; shift ;;
            --report-only) report_only="1"; shift ;;
            -h|--help)   usage; exit 0 ;;
            *) echo "f5-sweep: unknown arg '$1'" >&2; usage; exit 2 ;;
        esac
    done

    # Default base = HEAD~1 (sweep the LAST commit's contract change — the per-bead/per-change scope
    # F-5 is meant for). Do NOT default to merge-base origin/main: on a long-lived branch that diffs
    # the ENTIRE branch (hundreds of commits → thousands of tokens, un-actionable — bd-tak38o). For a
    # whole-sprint sweep pass an explicit --base <sprint-base-sha>; for pre-commit use --staged.
    if [ -z "$base" ]; then
        base="$(git rev-parse HEAD~1 2>/dev/null || echo HEAD)"
    fi

    run_sweep "$mode" "$base" "$report_only"
    exit $?
}

main "$@"
