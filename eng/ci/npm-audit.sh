#!/usr/bin/env bash
# npm dependency audit across every lockfile in the repo.
#
# The npm half of the audit story. Its .NET counterpart is NuGetAudit in Directory.Build.props, and
# this deliberately matches its disposition: findings do NOT fail ordinary builds, because an advisory
# published overnight against an untouched package would break work that has nothing to do with it.
# That is NuGet's own documented caution and it applies identically here.
#
# So the layering is:
#   PR          Dependency Review blocks NEWLY INTRODUCED vulnerable deps (security.yml, high+)
#   nightly     this script fails on anything high+ already present
#   continuous  Dependabot opens update PRs (dependabot.yml, npm ecosystem, all three lockfiles)
#
# Exit codes follow the convention used by the sibling gates:
#   0  every directory audited, nothing at or above the threshold
#   1  the property is FALSE: at least one directory has a finding at or above the threshold
#   2  the property could not be EVALUATED: npm missing, or a directory could not be audited
set -uo pipefail

readonly EXIT_FOUND=1
readonly EXIT_ENV=2
AUDIT_LEVEL="${NPM_AUDIT_LEVEL:-high}"

# Every directory with a lockfile. Derived from the repo rather than hardcoded, so a fourth lockfile
# is covered the day it lands instead of the day someone remembers this file.
discover_dirs() {
    git ls-files '*package-lock.json' | grep -v node_modules | xargs -r -n1 dirname
}

# Reads an `npm audit --json` document and prints "crit high mod low" counts.
counts_from_json() {
    python3 -c '
import json,sys
try: m=json.load(sys.stdin).get("metadata",{}).get("vulnerabilities",{})
except Exception: sys.exit(9)
print(m.get("critical",0), m.get("high",0), m.get("moderate",0), m.get("low",0))
'
}

# The threshold decision, in ONE place. Both the real path and the self-test call THIS -- a self-test
# that re-implements the comparison proves only that two copies of the logic agree, and would pass
# while the shipped path was wrong.
# Returns EXIT_FOUND when the counts breach AUDIT_LEVEL, 0 otherwise.
exceeds_threshold() { # crit high mod low
    local crit="$1" high="$2" mod="$3" low="$4"
    case "$AUDIT_LEVEL" in
        critical) [ "$crit" -gt 0 ] ;;
        high)     [ $((crit + high)) -gt 0 ] ;;
        moderate) [ $((crit + high + mod)) -gt 0 ] ;;
        *)        [ $((crit + high + mod + low)) -gt 0 ] ;;
    esac && return "$EXIT_FOUND"
    return 0
}

# --- exceptions ------------------------------------------------------------------------------------
#
# WHY A COUNT IS NOT ENOUGH ANY MORE.
#
# The threshold logic above answers "how many high findings are there", which is the right question
# right up until a finding has NO FIX. An advisory whose newest published version is still affected
# cannot be upgraded away, and the only responses a count-based gate leaves are to lower the
# threshold or suppress the directory -- both of which stop the gate detecting anything at all, in
# exchange for a problem it was never able to solve.
#
# So findings are matched per ADVISORY, and an advisory may be excepted individually. An exception is
# not a suppression: it carries an expiry and a stated premise, and the gate fails when either lapses.
# Default is deny -- an advisory nobody has written down is RED.
EXCEPTIONS_FILE="${NPM_AUDIT_EXCEPTIONS:-$(dirname "${BASH_SOURCE[0]}")/npm-audit-exceptions.json}"

# Distinct advisories at or above a level, one per line: id, package, severity, title.
#
# npm reports one entry per AFFECTED PACKAGE, so a single advisory deep in a dependency tree is
# counted once for every package that transitively reaches it -- two advisories in one package
# presented as twenty findings. Advisory objects are the nested `via` entries; plain strings are
# chain links back to them. Deduplicating by advisory is what makes an exception expressible: there
# is no honest way to write down "except those twenty".
advisories_from_json() { # level < json
    python3 -c '
import json,sys,re
RANK={"info":0,"low":1,"moderate":2,"high":3,"critical":4}
want=RANK.get(sys.argv[1],3)
try: doc=json.load(sys.stdin)
except Exception: sys.exit(9)
if "vulnerabilities" not in doc and "metadata" not in doc: sys.exit(9)
seen={}
for node in doc.get("vulnerabilities",{}).values():
    for v in node.get("via",[]):
        if not isinstance(v,dict): continue
        if RANK.get(v.get("severity",""),0) < want: continue
        m=re.search(r"(GHSA-[0-9a-z-]+)", v.get("url","") or "")
        key=m.group(1) if m else ("URL:"+(v.get("url") or "?"))
        seen[key]=(v.get("name") or v.get("module_name") or "?", v.get("severity","?"), (v.get("title") or "").replace("\t"," "))
for k in sorted(seen):
    p,s,t=seen[k]
    print("%s\t%s\t%s\t%s" % (k,p,s,t))
' "$1"
}

# Validates EVERY entry independently of any audit result, so a lapsed exception fails on its own
# terms rather than waiting for someone to notice. Prints violations; returns non-zero if any.
check_exceptions() {
    python3 -c '
import json,sys,glob,datetime,os
path=sys.argv[1]; today=datetime.date.fromisoformat(sys.argv[2])
if not os.path.exists(path): sys.exit(0)          # no file is not an error; it means deny-everything
try: doc=json.load(open(path,encoding="utf-8"))
except Exception as e:
    print("::error::the npm audit exceptions file could not be read (%s). It is not treated as empty: an unreadable allowlist is refused." % e, file=sys.stderr); sys.exit(2)
bad=0
for e in doc.get("exceptions",[]):
    who="%s in %s" % (e.get("advisory","?"), e.get("directory","?"))
    exp=e.get("expires")
    if not exp:
        print("::error::%s has no expiry. An exception without an end date is a permanent one." % who, file=sys.stderr); bad+=1; continue
    if datetime.date.fromisoformat(exp) < today:
        print("::error::%s EXPIRED on %s. Re-assess it: upgrade if a fix now exists, or renew it with a fresh reachability check." % (who,exp), file=sys.stderr); bad+=1
    for g in e.get("absent_globs",[]):
        hits=glob.glob(g,recursive=True)
        if hits:
            print("::error::%s assumed no file matching %s existed, and %d now do (e.g. %s). Its premise is broken; the exception no longer holds." % (who,g,len(hits),hits[0]), file=sys.stderr); bad+=1
sys.exit(1 if bad else 0)
' "$EXCEPTIONS_FILE" "$(date -u +%Y-%m-%d)"
}

# Is this advisory excepted for this directory? Directory-scoped: the same advisory may be tolerable
# where it is unreachable and intolerable where it is not.
is_excepted() { # advisory-id dir
    python3 -c '
import json,sys,os
path,adv,d=sys.argv[1],sys.argv[2],sys.argv[3]
if not os.path.exists(path): sys.exit(1)
try: doc=json.load(open(path,encoding="utf-8"))
except Exception: sys.exit(1)
for e in doc.get("exceptions",[]):
    if e.get("advisory")==adv and e.get("directory")==d: sys.exit(0)
sys.exit(1)
' "$EXCEPTIONS_FILE" "$1" "$2" 2>/dev/null
}

# Records which exceptions actually matched, so main() can report the ones that did not.
MATCHED_EXCEPTIONS=""

audit_dir() {
    local dir="$1" json counts crit high mod low advs id pkg sev title unexcepted=0
    # npm audit exits non-zero WHEN IT FINDS THINGS, so a non-zero exit is not itself an error.
    # Only unparseable output means the audit could not be evaluated.
    json="$(cd "$dir" && npm audit --json 2>/dev/null)" || true
    if ! counts="$(printf '%s' "$json" | counts_from_json)"; then
        printf '::error::npm audit produced no parseable result for %s -- the audit did not run.\n' "$dir" >&2
        return "$EXIT_ENV"
    fi
    read -r crit high mod low <<<"$counts"
    printf '  %-56s critical=%s high=%s moderate=%s low=%s\n' "$dir" "$crit" "$high" "$mod" "$low"

    if ! advs="$(printf '%s' "$json" | advisories_from_json "$AUDIT_LEVEL")"; then
        printf '::error::npm audit output for %s could not be read as advisories -- not a clean result.\n' "$dir" >&2
        return "$EXIT_ENV"
    fi
    [ -n "$advs" ] || return 0

    while IFS=$'\t' read -r id pkg sev title; do
        [ -n "$id" ] || continue
        if is_excepted "$id" "$dir"; then
            printf '    excepted  %s  %s (%s) -- %s\n' "$id" "$pkg" "$sev" "$title"
            MATCHED_EXCEPTIONS="${MATCHED_EXCEPTIONS}${id}|${dir}"$'\n'
        else
            printf '::error::%s  %s (%s) in %s -- %s\n' "$id" "$pkg" "$sev" "$dir" "$title" >&2
            unexcepted=$((unexcepted + 1))
        fi
    done <<<"$advs"

    [ "$unexcepted" -eq 0 ] || return "$EXIT_FOUND"
    return 0
}

main() {
    command -v npm >/dev/null 2>&1 || { printf '::error::npm not found; the audit could not run.\n' >&2; exit "$EXIT_ENV"; }

    # Exceptions are validated FIRST and independently of any audit result. An expired entry, or one
    # whose premise has been broken, fails here even if npm never runs -- otherwise a lapsed
    # exception would sit unnoticed for as long as the finding it covers stays quiet.
    check_exceptions || { printf '::error::the npm audit exception list is not in good standing (above). Fix the entries before trusting this gate.\n' >&2; exit "$EXIT_FOUND"; }

    local dirs found=0 unevaluated=0 n=0 stale
    dirs="$(discover_dirs)"
    [ -n "$dirs" ] || { printf '::error::no package-lock.json found. An audit over zero lockfiles is not a clean audit.\n' >&2; exit "$EXIT_ENV"; }

    printf 'npm audit (threshold: %s)\n' "$AUDIT_LEVEL"
    # Every directory is audited before reporting. Stopping at the first finding would report one
    # problem when there are three, and the count is what tells you whether it is getting better.
    while IFS= read -r dir; do
        n=$((n + 1))
        audit_dir "$dir"
        case $? in
            "$EXIT_FOUND") found=$((found + 1)) ;;
            "$EXIT_ENV")   unevaluated=$((unevaluated + 1)) ;;
        esac
    done <<<"$dirs"

    printf '%s lockfile(s) audited.\n' "$n"
    [ "$unevaluated" -eq 0 ] || { printf '::error::%s director(ies) could not be audited. Not a clean result.\n' "$unevaluated" >&2; exit "$EXIT_ENV"; }

    # A STALE exception is reported and fails. Leaving one behind is how an allowlist stops being
    # read: entries accumulate, nobody can tell which are load-bearing, and the file becomes a place
    # findings go rather than a set of live claims. The right response to a fixed advisory is to
    # delete its entry, and this is what asks for that.
    stale="$(python3 -c '
import json,sys,os
path=sys.argv[1]
matched=set(l for l in sys.argv[2].split(chr(10)) if l)
if not os.path.exists(path): sys.exit(0)
try: doc=json.load(open(path,encoding="utf-8"))
except Exception: sys.exit(0)
for e in doc.get("exceptions",[]):
    k="%s|%s" % (e.get("advisory"), e.get("directory"))
    if k not in matched: print("%s (%s) in %s" % (e.get("advisory"), e.get("package"), e.get("directory")))
' "$EXCEPTIONS_FILE" "$MATCHED_EXCEPTIONS")"
    if [ -n "$stale" ]; then
        printf '::error::exception(s) present that matched no finding. If the advisory is fixed, delete the entry; if the audit no longer reaches it, the entry is misdirected:\n' >&2
        printf '%s\n' "$stale" | sed 's/^/  /' >&2
        exit "$EXIT_FOUND"
    fi

    [ "$found" -eq 0 ]       || { printf '::error::%s director(ies) have vulnerabilities at or above %s that are not excepted. Run `npm audit fix`, or `npm audit` in the directory to see them.\n' "$found" "$AUDIT_LEVEL" >&2; exit "$EXIT_FOUND"; }
    printf 'npm audit: clean at %s and above across %s lockfile(s).\n' "$AUDIT_LEVEL" "$n"
}

# --self-test drives the decision logic with canned audit documents. It needs no network and no
# install, and it proves the gate can FAIL -- a gate only ever observed passing is not evidence.
self_test() {
    local fails=0
    check() { # name expected-exit json -- drives counts_from_json AND exceeds_threshold, the shipped ones
        local got rc=0
        got="$(printf '%s' "$3" | counts_from_json)" || { printf 'SELF-TEST: FAIL -- %s: unparseable\n' "$1" >&2; fails=$((fails+1)); return; }
        read -r c h m l <<<"$got"
        exceeds_threshold "$c" "$h" "$m" "$l" || rc=$?
        if [ "$rc" = "$2" ]; then printf 'SELF-TEST: PASS -- %s\n' "$1"
        else printf 'SELF-TEST: FAIL -- %s (expected %s, got %s)\n' "$1" "$2" "$rc" >&2; fails=$((fails+1)); fi
    }

    AUDIT_LEVEL=high
    check "clean tree is GREEN"                      0 '{"metadata":{"vulnerabilities":{"critical":0,"high":0,"moderate":0,"low":0}}}'
    check "a CRITICAL is RED"                        1 '{"metadata":{"vulnerabilities":{"critical":1,"high":0,"moderate":0,"low":0}}}'
    check "a HIGH is RED"                            1 '{"metadata":{"vulnerabilities":{"critical":0,"high":1,"moderate":0,"low":0}}}'
    check "a MODERATE alone is GREEN at high"        0 '{"metadata":{"vulnerabilities":{"critical":0,"high":0,"moderate":9,"low":0}}}'

    # Unparseable output must be distinguishable from a clean audit: "npm printed nothing" and
    # "npm found nothing" are the same string to a naive reader, and only one of them is a pass.
    if printf 'not json' | counts_from_json >/dev/null 2>&1
    then printf 'SELF-TEST: FAIL -- garbage parsed as a result\n' >&2; fails=$((fails+1))
    else printf 'SELF-TEST: PASS -- unparseable output is REFUSED, not read as clean\n'; fi

    # The discovery half: the gate must actually find the repo's lockfiles.
    local n; n="$(discover_dirs | grep -c .)"
    if [ "$n" -ge 1 ]; then printf 'SELF-TEST: PASS -- discovery finds %s lockfile(s)\n' "$n"
    else printf 'SELF-TEST: FAIL -- discovery found no lockfiles; the gate would audit nothing\n' >&2; fails=$((fails+1)); fi

    # --- the exception machinery -------------------------------------------------------------------
    # Every arm below drives the SHIPPED functions against a temporary allowlist. The point is not
    # that exceptions work; it is that they still FAIL in each of the ways they are supposed to,
    # because an allowlist that cannot say no is the thing this design exists to avoid being.
    # The scratch directory is CWD-RELATIVE, matching the form real entries use
    # ("docs-site/**/*.icns"). An absolute temp path was tried first and quietly tested
    # nothing on one platform: the shell hands bash a POSIX path, the allowlist stores it
    # verbatim as JSON, and the interpreter that reads it back resolves that string against
    # a different root -- so the planted file was invisible and the arm passed by not
    # looking. Relative paths have no second root to disagree about.
    local tmp=".npm-audit-selftest"; rm -rf "$tmp"; mkdir -p "$tmp"
    local ADV='{"vulnerabilities":{"pkg-a":{"severity":"high","via":[{"name":"image-size","severity":"high","title":"loop","url":"https://github.com/advisories/GHSA-TEST-0001"}]},
                                   "pkg-b":{"severity":"high","via":["pkg-a"]}}}'
    arm() { if [ "$2" = "$3" ]; then printf 'SELF-TEST: PASS -- %s\n' "$1"; else printf 'SELF-TEST: FAIL -- %s (expected %s, got %s)\n' "$1" "$3" "$2" >&2; fails=$((fails+1)); fi; }

    # One advisory reached through two packages must be reported ONCE, or it cannot be excepted by id.
    local n_adv; n_adv="$(printf '%s' "$ADV" | advisories_from_json high | grep -c .)"
    arm "one advisory reached via two packages is ONE finding, not two" "$n_adv" "1"

    local far; far="$(printf '%s' "$ADV" | advisories_from_json critical | grep -c .)"
    arm "a high advisory is below the critical threshold" "$far" "0"

    EXCEPTIONS_FILE="$tmp/e.json"

    # DEFAULT DENY: no file at all must not read as "everything is fine".
    rm -f "$EXCEPTIONS_FILE"
    is_excepted GHSA-TEST-0001 docs-site; arm "with no allowlist, nothing is excepted" "$?" "1"

    printf '{"exceptions":[{"advisory":"GHSA-TEST-0001","package":"image-size","directory":"docs-site","expires":"2999-01-01","absent_globs":["%s/nope-*.icns"]}]}\n' "$tmp" >"$EXCEPTIONS_FILE"
    is_excepted GHSA-TEST-0001 docs-site; arm "a listed advisory is excepted in its own directory" "$?" "0"
    is_excepted GHSA-TEST-0001 other-dir; arm "the same advisory is NOT excepted in another directory" "$?" "1"
    is_excepted GHSA-TEST-9999 docs-site; arm "an unlisted advisory is never excepted" "$?" "1"
    check_exceptions >/dev/null 2>&1;     arm "a live, in-date entry with an intact premise passes" "$?" "0"

    # EXPIRY: a date in the past is RED, not quietly ignored.
    printf '{"exceptions":[{"advisory":"GHSA-TEST-0001","package":"image-size","directory":"docs-site","expires":"2000-01-01"}]}\n' >"$EXCEPTIONS_FILE"
    check_exceptions >/dev/null 2>&1; arm "an EXPIRED entry fails" "$?" "1"

    # NO EXPIRY AT ALL: a permanent exception is refused outright.
    printf '{"exceptions":[{"advisory":"GHSA-TEST-0001","package":"image-size","directory":"docs-site"}]}\n' >"$EXCEPTIONS_FILE"
    check_exceptions >/dev/null 2>&1; arm "an entry with NO expiry fails" "$?" "1"

    # BROKEN PREMISE: the file it assumed absent now exists.
    printf '{"exceptions":[{"advisory":"GHSA-TEST-0001","package":"image-size","directory":"docs-site","expires":"2999-01-01","absent_globs":["%s/planted-*.icns"]}]}\n' "$tmp" >"$EXCEPTIONS_FILE"
    check_exceptions >/dev/null 2>&1; arm "premise intact while the file is absent" "$?" "0"
    : >"$tmp/planted-1.icns"
    check_exceptions >/dev/null 2>&1; arm "a BROKEN premise (the file now exists) fails" "$?" "1"

    # An unreadable allowlist is refused rather than read as empty -- empty would mean deny-all here,
    # which looks safe and would mask a corrupted file behind a plausible failure.
    printf 'not json' >"$EXCEPTIONS_FILE"
    check_exceptions >/dev/null 2>&1; arm "an unparseable allowlist is REFUSED, not read as empty" "$?" "2"

    rm -rf "$tmp"

    # The real allowlist must itself be in good standing, or the gate ships already-broken.
    EXCEPTIONS_FILE="$(dirname "${BASH_SOURCE[0]}")/npm-audit-exceptions.json"
    check_exceptions >/dev/null 2>&1; arm "the repository's own allowlist is in good standing" "$?" "0"

    [ "$fails" -eq 0 ] || { printf 'SELF-TEST: %s arm(s) failed\n' "$fails" >&2; exit 1; }
    printf 'SELF-TEST: all arms passed\n'
}

case "${1:-}" in
    --self-test) self_test ;;
    *)           main ;;
esac
