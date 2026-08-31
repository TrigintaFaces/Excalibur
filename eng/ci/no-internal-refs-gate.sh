#!/usr/bin/env bash
# no-internal-refs-gate — public-surface internal-reference scan (preventive gate).
#
# Implements the mechanical gate mandated by
#   .claude/rules/quality/no-internal-refs-in-public-surface.md
#
# A NuGet consumer integrates the compiled DLL + its .xml doc file + the published
# docs site + the shipped samples/README/CHANGELOG. None of those may leak an
# INTERNAL development identifier — a tracker id (bd-prefixed OR a bare beads id),
# a sprint ref (S999 / Sprint 999), an ADR ref (ADR-999), or a process noun
# (FORGE / ORACLE / task-2314 ...). The rule documents the CONTRACT/BEHAVIOR, not
# the work item that produced it.
#
# This gate greps the PUBLIC-SURFACE globs for those tokens and fails if any are
# found. INTERNAL sinks (commit msgs, .beads/*.jsonl, .claude/**, management/**,
# ADR files, tests/**, PublicAPI*.txt, and internal-visibility `///` comments under
# an Internal/ folder) are EXEMPT — ids are expected there.
#
# Exit codes:
#   0  no public-surface internal-ref leaks (clean)
#   1  leaks found (gate fail — rewrite the substance, drop the ref)
#   2  usage / environment error
#   3  --self-test failed (the gate itself is broken / vacuous)
#
# Usage:
#   no-internal-refs-gate.sh                # scan the whole tracked tree (git ls-files)
#   no-internal-refs-gate.sh --staged       # scan only staged public-surface files (pre-commit)
#   no-internal-refs-gate.sh --report-only  # advisory: print leaks but exit 0 (not blocking)
#   no-internal-refs-gate.sh --self-test
#
set -uo pipefail

# shellcheck source=/dev/null
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/gate-denominator.sh"

# ---------------------------------------------------------------------------
# Token patterns
# ---------------------------------------------------------------------------
# Directly-greppable internal-reference tokens (bare beads ids are handled
# separately via membership against .beads/issues.jsonl — a naive [a-z0-9]{6}
# grep matches real English words).
#   bd-xxx…              prefixed tracker id (id suffix is base36, 3-6 chars — the
#                        real distribution is 3/4/5/6-char, so a hardcoded {6} would
#                        miss ~72% of ids; the `bd-` prefix disambiguates short ids)
#   ADR-999 / ADR999     architecture-decision-record ref (needs a digit). CASE-INSENSITIVE, and the
#                        uppercase-only form is why: an ADR is cited in prose as `ADR-999` but on disk the
#                        file is `management/architecture/adr-999-some-title.md`, so every LINK to one --
#                        in a sample README, in a mirrored eng/ script's "See:" line -- spelled it
#                        lowercase and matched nothing. Five such links sat on the public surface while
#                        this arm reported clean. Same evasion the sprint arm already documents for a
#                        lowercase `s890`, and measured the same way: widening to case-insensitive
#                        produced no hit that was not a real leak.
#   R7.4 / R27.20a       REQUIREMENT ID from an internal spec, and the largest single class on this
#                        surface when it was added. A consumer reading `R7.4: Context-aware timeout
#                        handling` in IntelliSense has no document to resolve it in -- the specs these
#                        index are not published -- so it is a pointer into nothing, exactly like a
#                        tracker id, and it dates the code.
#                        THE DOT IS REQUIRED, and it is doing the same work the hyphen does for `EC-`:
#                        a bare `R2` is Cloudflare R2 (a storage product we could plausibly document),
#                        a register, or a revision label. `R<n>.<n>` is not ambiguous -- measured, all
#                        53 distinct tokens matching it on the public surface were requirement ids and
#                        none was a version string or a product name. If a legitimate `R<n>.<n>` term
#                        ever appears (a spec revision "R2.0" of an external standard), reword it or
#                        exempt the line; do NOT drop the dot to "widen coverage", which re-creates an
#                        unbounded false-positive surface a BLOCKING gate cannot survive.
#                        Case-insensitive on the same measured basis as the ADR and sprint arms:
#                        lowercase `r<n>.<n>` had ZERO hits, so it costs nothing today and closes the
#                        evasion before it is used.
#                        The trailing `[a-z]?` covers sub-part ids (`R27.20a`), which were in use.
#   Phase 8.3 / 9.1h-2   INTERNAL PLAN-PHASE id -- a numbered stage of a delivery plan the consumer has
#                        no access to. Same audience failure as a requirement id: `Architecture Boundary
#                        Validation (Phase 8.3)` printed by a mirrored script tells a reader nothing.
#                        THE DOT IS REQUIRED, and here it is doing MORE work than anywhere else in this
#                        pattern set, because the bare form is genuinely common English in our own docs.
#                        Measured on the public surface: the DOTTED form had 5 hits, every one an
#                        internal plan phase; the BARE form (`Phase 1`, `Phase 2`, ...) had 46+, every
#                        one legitimate tutorial prose in docs-site/ and samples/ ("Phase 2: add the
#                        handler"). A bare arm would red on ordinary documentation, and a BLOCKING gate
#                        that reds on ordinary documentation gets disabled. Do NOT drop the dot.
#   S999 / s999          sprint id (exactly 3 digits, word-bounded). Case-insensitive: a lowercase
#                        `s890` evaded the uppercase-only form and leaked. Measured at zero new hits
#                        across the whole public surface when this was widened, so it costs nothing
#                        today; the residual risk is a variable literally named `s400`, which would
#                        need rewording or a line-level exemption.
#   SA 32694 / SA #28375 inter-agent message ids. These matched NO pattern at all and leaked silently.
#                        `SA ruling` and `USA 1234` still do not match -- four digits are required.
#   Sprint 863           sprint prose prefix (needs a digit). The separator is deliberately loose:
#                        it was a single optional space-or-hyphen, so `Sprint: 329` -- the form a
#                        PowerShell .NOTES block writes -- was INVISIBLE to a gate whose whole job
#                        is finding this token. Found by someone fixing a leak it never reported.
#                        `sprints are useful` and `sprint to the finish` still do not match: a digit
#                        is required.
#   FORGE/ORACLE/...     phase callsigns / process nouns
#   task-2314 / msg 19752  OPCOM/mission process refs
#   FR-4a / NFR-3 / EC-P1 / AC-6 / AC4
#   AD-326-1 / AD-520.1  decision-record ref. A SEPARATE PREFIX FROM `ADR-`, and that is the whole
#                        reason it went unseen: `ADR-?[0-9]` cannot match `AD-326`, so nine of these
#                        sat in mirrored build scripts while this gate reported the public surface
#                        clean. Sub-parts join with either a dot or a hyphen, so both are accepted.
#                        THE HYPHEN IS REQUIRED, on the same reasoning as `EC-`: without it the arm
#                        would match any word ending in those two letters followed by a digit.
#                        MINI-SPEC REQUIREMENT IDS. A consumer cannot resolve any of these -- the
#                        specs they index are not published -- so in an XML doc they are noise that
#                        dates the code, exactly like a tracker id. They outnumbered the tracker ids
#                        on the public surface and matched no pattern at all.
#                        THE HYPHEN IS REQUIRED FOR `EC-`, and that is not cosmetic: bare `EC2` is
#                        AWS EC2 and appears in samples and docs as an ordinary term. `AC` is matched
#                        with or without the hyphen because `AC4`/`AC5` are used as acceptance-criteria
#                        ids in gate comments; if a legitimate `AC<digit>` term ever appears, reword it
#                        rather than loosening this -- a gate that reds on real prose gets disabled.
#                        The letter and dotted forms (`AC-P1.4`, `FR-P1.5`) are covered because they
#                        were NOT: the arm required a digit straight after the hyphen, so an id one
#                        line away from a flagged one shipped unnoticed. Found by hand, again.
#                        `EC-` keeps its REQUIRED hyphen while `AC`/`FR` do not -- making it optional
#                        for all three re-matches AWS `EC2`, which was measured and rejected.
#
# THE SEPARATOR AFTER `SA` IS REQUIRED. Without it the arm also matches `SA1402` and `SA1649`,
# which are StyleCop analyzer rule ids -- a suppression comment naming the rule it suppresses is
# not an internal reference, and four such lines were being counted as leaks. The real form the
# arm exists for always carries a separator (`SA 32694`, `SA #28375`), so requiring one loses no
# real hit. Same false-positive class the `bd-` note above records for command names.
#
# HALF THE CALLSIGNS ARE ORDINARY ENGLISH, AND ONE IS A SHIPPED PROVIDER NAME.
# ORACLE is a test oracle and a database we ship a provider for; SENTINEL is a sentinel
# value; COMPASS, FORGE and EXHIBIT are plain words. Matching those bare produced six hits
# on one script's own prose ("THE ORACLE IS THE WORKFLOW DIRECTORY") and two on another's
# ("SENTINEL-SHAPED") the moment eng/ came into scope. Those five now require an adjacent
# `phase` to count. The five that are not English words keep their bare match.
NIR_TOKEN_ERE='bd-[a-z0-9]{3,6}'\
'|\b[Aa][Dd][Rr]-?[0-9]+'\
'|\b[Ss][0-9]{3}\b'\
'|\bSA[ #-]+[0-9]{4,}'\
'|\b[Ss]print[[:space:]]*[:#-]?[[:space:]]*[0-9]+'\
'|\b(OVERWATCH|CRUCIBLE|BLUEPRINT|CHRONICLE|TRACEPOINT)\b'\
'|\b(COMPASS|FORGE|ORACLE|SENTINEL|EXHIBIT)[ -](phase|PHASE)\b'\
'|\b(phase|PHASE)[ -](COMPASS|FORGE|ORACLE|SENTINEL|EXHIBIT)\b'\
'|\bN?FR-?[A-Z]?[0-9]+(\.[0-9]+)*[a-z]?'\
'|\bEC-[A-Z]?[0-9]+(\.[0-9]+)*[a-z]?'\
'|\bAC-?[A-Z]?[0-9]+(\.[0-9]+)*[a-z]?'\
'|\bAD-[0-9]+([.-][0-9]+)*'\
'|\btask-[0-9]{3,}'\
'|\bmsg [0-9]{3,}'\
'|\b[Rr][0-9]+\.[0-9]+[a-z]?'\
'|\b[Pp]hase [0-9]+\.[0-9]+[a-z0-9-]*'

# ---------------------------------------------------------------------------
# Core (pure, testable) functions
# ---------------------------------------------------------------------------

# nir_bare_id_set
#   stdout: the set of bare beads ids (the [a-z0-9]{6} suffix of each tracked
#           issue id), one per line, sorted-unique. Empty if the jsonl is absent.
nir_bare_id_set() {
    local jsonl="${1:-.beads/issues.jsonl}"
    [ -f "$jsonl" ] || return 0
    # BARE (unprefixed) ids are admitted at length 6 ONLY. A bare 3-5-char base36 token
    # carries zero evidence of being an internal ref: `may` `512` `pan` `inf` `sup` are
    # real short bead ids AND ordinary words/numbers, so a bare (even word-bounded) grep
    # for them flagged 900+ legitimate prose lines. That is undecidable at the input, not
    # tunable — the information needed to tell `may`-the-word from `…-may`-the-id is not
    # present. So the bare arm covers length-6 ids only (base36 collision with English is
    # negligible at 6 chars). Shorter ids leak in their PREFIXED form, which the
    # `bd-[a-z0-9]{3,6}` arm of NIR_TOKEN_ERE already catches (`bd-may` is a leak; a bare
    # `may` in a sentence is the word). RESIDUAL GAP, BY CONSTRUCTION: a bare 3-5-char id
    # written WITHOUT the `bd-` prefix is out of this gate's reach. Do NOT re-widen this to
    # {3,6} to "recover coverage" — that re-creates the unbounded false-positive surface a
    # BLOCKING gate cannot survive (it reds on legitimate lines, gets disabled or `|| true`-d,
    # and then enforces nothing). Fixture 4 guards this length against re-widening.
    grep -oE '"id"[[:space:]]*:[[:space:]]*"[^"]+"' "$jsonl" 2>/dev/null \
        | sed -E 's/.*"([^"]+)"$/\1/; s/^.*-//' \
        | grep -E '^[a-z0-9]{6}$' \
        | sort -u
}

# nir_scannable_lines <file>
#   stdout: "lineno:content" for the lines of <file> that are part of the PUBLIC
#           surface. For .cs, only XML-doc (`///`) lines ship in the .xml; a plain
#           `//` comment or code line does not, so only `///` is scanned. For every
#           other file type, all lines are public.
nir_scannable_lines() {
    local f="$1"
    [ -f "$f" ] || return 0
    case "$f" in
        *.cs)
            # Two public surfaces in a .cs file, not one.
            #
            # 1. XML-doc lines ship in the .xml and surface in IntelliSense.
            # 2. STRING LITERALS. A user-facing exception message, log template, or
            #    analyzer/diagnostic message is compiled into the assembly and read by
            #    consumers verbatim -- the rule names those explicitly as public surface.
            #    Scanning only /// left an internal id inside a thrown message invisible
            #    to this gate while shipping it to every consumer who triggers the throw.
            #
            # 3. PLAIN `//` IMPLEMENTATION COMMENTS. They are packed into no .nupkg, but the
            #    source tree they live in is copied verbatim to a public repository, so a
            #    consumer who reads the code reads them. Restricting pass 1 to `///` measured
            #    a clean tree while several hundred implementation comments carried internal
            #    identifiers into that public copy. The `///` case is a subset of this one, so
            #    pass 1 now emits both.
            #
            # The literal pass still skips comment lines: they are already emitted by pass 1,
            # and re-emitting them would only duplicate hits.
            {
                grep -nE '//' "$f" 2>/dev/null
                grep -nE '"' "$f" 2>/dev/null | grep -vE '^[0-9]+:[[:space:]]*//'
            } | sort -u -t: -k1,1n
            ;;
        *.csproj|*.props|*.targets)
            # A build file reaches consumers two ways, and both are scanned.
            #
            # 1. PACKAGE METADATA — <Description>, <PackageReleaseNotes>, <PackageTags>,
            #    <Authors> — ships to the registry and installs with the package.
            # 2. THE FILE ITSELF. Build files live in the source tree that is copied verbatim
            #    to a public repository, so an MSBuild <!-- --> comment is read by anyone who
            #    clones it. Skipping comment blocks measured a clean tree over build files
            #    whose comments named internal work items.
            grep -n '.*' "$f" 2>/dev/null
            ;;
        *)    grep -n '.*' "$f" 2>/dev/null ;;
    esac
}

# nir_hits_in_file <file> <bare_id_ere>
#   stdout: "file:lineno:content" for each public-surface line carrying an internal
#           ref (a direct token OR a bare beads id). <bare_id_ere> may be empty.
nir_hits_in_file() {
    local f="$1" bare_ere="${2:-}" prefixed_ere="${3:-}"
    local ere="$NIR_TOKEN_ERE"
    # A MEMBERSHIP-BASED `bd-` ARM REPLACES THE LOOSE ONE WHEN THE TRACKER IS READABLE.
    #
    # `bd-[a-z0-9]{3,6}` matches our own CLI command names and ordinary hyphenated English:
    # bd-file, bd-status, bd-export, bd-flush, bd-harness, bd-dependent. Measured on the
    # scripts this gate newly covers, those were the majority of `bd-` hits — and a BLOCKING
    # gate that reds on `bd-file` gets disabled, after which it enforces nothing.
    #
    # A digit requirement was tried and rejected on the data: 17.5% of real ids (1893 of
    # 10809) contain no digit, so it would have dropped nearly a fifth of genuine leaks.
    # Membership against the tracker discriminates exactly -- measured 12/12 command names
    # excluded, 6/6 real ids still caught.
    #
    # RESIDUAL GAP, STATED: an id deleted from the tracker is no longer matched by this arm.
    # That is the same trade the bare arm already makes, and it fails toward a missed leak
    # rather than toward a gate nobody can leave enabled.
    [ -n "$prefixed_ere" ] && ere="${ere//bd-\[a-z0-9\]\{3,6\}/$prefixed_ere}"
    [ -n "$bare_ere" ] && ere="${ere}|${bare_ere}"

    local lines
    lines="$(nir_scannable_lines "$f")"
    [ -n "$lines" ] || return 0

    printf '%s\n' "$lines" \
        | grep -E "$ere" \
        | sed -E "s#^#${f}:#" \
        || true
}

# nir_is_exempt <path>
#   returns 0 (exempt) if the path is an internal sink that legitimately carries ids.
nir_is_exempt() {
    case "$1" in
        .beads/*|*/.beads/*) return 0 ;;
        .claude/*|*/.claude/*) return 0 ;;
        .dts/*|*/.dts/*) return 0 ;;

        # GATE SELF-TEST FIXTURES ARE EXEMPT, AND THE RULE REQUIRES THEM TO BE.
        #
        # A gate proves it is non-vacuous by planting the very tokens it hunts. Stripping
        # those planted tokens would close the leak by disabling its own detector -- so the
        # rule names this exemption explicitly. Recognised by filename: a *.test.sh, a
        # *.harness-lock.sh, or a *.fixture.sh exists only to feed a gate.
        #
        # This file is exempt for the same reason twice over: it DOCUMENTS the token shapes
        # it catches (an ADR ref, a sprint ref) and it carries its own planted fixtures.
        *.test.sh|*.harness-lock.sh|*.fixture.sh) return 0 ;;
        */no-internal-refs-gate.sh|no-internal-refs-gate.sh) return 0 ;;

        # eng/** and .github/workflows/** are NOT exempt -- they are PUBLIC SURFACE.
        #
        # They were exempt here, and that was the defect: the operator's recorded directive
        # states that anything placed in eng/ci IS PUBLISHED TO A PUBLIC REPOSITORY, and the
        # rule's own machine-readable surface list names `eng/**` and `.github/workflows/**`
        # among its INCLUDES. So this gate excluded, by construction, two of the exact trees
        # the rule it implements calls public -- it could not catch a leak there, only a
        # human could, and four separate tracker items record leaks found exactly that way.
        #
        # The rest of .github/ (issue templates, config) stays exempt: the rule's list names
        # the workflows subtree, not the whole directory.
        .github/workflows/*) return 1 ;;
        .github/*) return 0 ;;

        management/*) return 0 ;;
        docs/*) return 0 ;;                 # contributor-internal docs (not the published site)
        tests/*|*/tests/*) return 0 ;;
        */Internal/*) return 0 ;;           # internal-visibility seam XML docs
        */bin/*|*/obj/*|*/node_modules/*|*/.docusaurus/*) return 0 ;;
        *PublicAPI*.txt) return 0 ;;        # API signature baselines, not prose
        *) return 1 ;;
    esac
}

# ---------------------------------------------------------------------------
# Real run
# ---------------------------------------------------------------------------

# nir_public_pathspecs
#   stdout: the git pathspec arguments (one per line) selecting the public surface
#           and excluding every internal sink. Used with `git grep` (fast, indexed).
nir_public_pathspecs() {
    printf '%s\n' \
        'docs-site/' 'samples/' 'src/' 'CHANGELOG.md' 'README.md' \
        'eng/' '.github/workflows/' \
        ':(exclude,glob)**/.claude/**' ':(exclude,glob)**/.dts/**' \
        ':(exclude,glob)**/bin/**' ':(exclude,glob)**/obj/**' \
        ':(exclude,glob)**/node_modules/**' ':(exclude,glob)**/.docusaurus/**' \
        ':(exclude,glob)**/PublicAPI*.txt' \
        ':(exclude,glob)docs/**' ':(exclude,glob)tests/**' \
        ':(exclude,glob)management/**' \
        ':(exclude,glob)**/*.test.sh' ':(exclude,glob)**/*.harness-lock.sh' \
        ':(exclude,glob)**/*.fixture.sh' ':(exclude,glob)**/no-internal-refs-gate.sh' \
        ':(exclude,glob)**/package-lock.json' ':(exclude,glob)**/packages.lock.json'
        # AN `Internal/` DIRECTORY IS NOT AN INTERNAL SINK, and excluding it was a false
        # green over a real shipped surface. The C# compiler emits XML documentation for
        # `internal` members too: the generated .xml packed into the .nupkg carries the
        # `///` comments of every type under `Internal/`, and IntelliSense surfaces them.
        # Measured: the shipped Excalibur.Data.ElasticSearch.xml contained the `<summary>`
        # and `<remarks>` of its Internal seam interfaces verbatim. So the directory name
        # describes C# accessibility, not who can read the prose, and the per-file content
        # rule below (a .cs file is public via its `///` lines and its string literals) is
        # what decides the surface -- it applies under `Internal/` exactly as it does above.
        # GENERATED LOCKFILES ARE NOT PROSE, and scanning them as such is a category error.
        # A lockfile is machine-written dependency metadata; nobody reads it for documentation and
        # nothing in it is authored. Its base64 integrity hashes contain every letter-digit sequence
        # eventually: `sha512-…/ACI4MEsnoD…` matched the acceptance-criteria arm as `ACI4`. Tightening
        # the token pattern to dodge random base64 is unwinnable -- a long enough hash will collide
        # with any shape -- so the surface is excluded instead, which is also what it is.
}

# nir_keep_line "path:lineno:content"
#   A src/** hit is public only when the file is a README/.nuspec (all lines public)
#   or a .cs XML-doc (`///`) line. Non-src public files keep every line.
nir_keep_line() {
    local line="$1" path content
    path="${line%%:*}"
    case "$path" in
        src/*)
            case "$path" in
                # EVERY markdown file under src/ is public surface, not only README/ARCHITECTURE:
                # src/** is mirrored to a public repo, so a NOTES.md / guide beside the code ships
                # too, and the per-subsystem ARCHITECTURE.md guarantee contracts are MANDATED here.
                # The rule's surface list names `src/**/*.md` wholesale; keeping only two filenames
                # let a leak in any other src markdown through. Align the gate with the rule.
                *.md|*.nuspec) return 0 ;;
                *.cs)
                    content="${line#*:*:}"
                    # EVERY comment is public, not only the XML-doc ones, and a TRAILING
                    # comment counts: `///` ships in the .xml packed into the .nupkg, and any
                    # `//` ships in the source tree that is copied verbatim to a public
                    # repository. A leading-anchor test missed trailing comments, which is
                    # where several tenancy notes carried internal identifiers.
                    [[ "$content" == *"//"* ]] && return 0
                    # STRING LITERALS also ship -- compiled into the assembly and read
                    # verbatim by consumers as exception/log/diagnostic text. This branch
                    # MUST stay consistent with nir_scannable_lines(): that function decides
                    # which lines are OFFERED, this one decides which are KEPT, and a line
                    # class added there is silently discarded here unless it is added here
                    # too. One policy, two functions -- widening only the first produces a
                    # scanner that reports success while examining nothing new.
                    [[ "$content" == *'"'* && ! "$content" =~ ^[[:space:]]*// ]] && return 0
                    return 1 ;;
                # A build file is public twice over: its package metadata installs with the
                # package, and the file itself is copied verbatim to a public repository —
                # MSBuild comments included. Keeping only the metadata elements measured a
                # clean tree over build files whose comments named internal work items.
                *.csproj|*.props|*.targets) return 0 ;;
                # .resx <value> content ships compiled into the assembly and surfaces to
                # consumers verbatim (and, for compliance/report resources, to an external
                # auditor). The rule's machine-readable surface list names it (resource-value),
                # so keeping it here ALIGNS the gate with the rule rather than drifting from it.
                # A relocation of auditor-facing strings into a .resx must stay AS findable as a
                # .cs string literal, not less.
                *.resx) return 0 ;;
                # Everything else under src/ discards (non-comment code lines without literals).
                *) return 1 ;;
            esac ;;
        *) return 0 ;;
    esac
}

run_gate() {
    local staged="${1:-}" report_only="${2:-}"

    if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
        echo "no-internal-refs-gate: error — not inside a git work tree" >&2
        return 2
    fi

    local idset_file
    idset_file="$(mktemp)"
    nir_bare_id_set > "$idset_file"

    # Optional staged-scope: intersect the git-grep hits with the staged file set.
    local staged_set=""
    if [ "$staged" = "--staged" ]; then
        staged_set="$(mktemp)"
        git diff --cached --name-only --diff-filter=d 2>/dev/null | sed 's#^\./##' | sort -u > "$staged_set"
    fi

    local -a specs=(); mapfile -t specs < <(nir_public_pathspecs)

    # In --staged mode, narrow the SCAN ITSELF to the staged files. Previously the staged
    # set was applied only as a post-filter on the hits (below), so pass 2 still swept the
    # entire public surface with a ~10k-pattern fixed-string grep on EVERY commit. A commit
    # touching three files paid whole-tree cost, which is why this gate is invoked by
    # nothing: at that price it cannot live on the commit path. Exemption is per-path
    # (nir_is_exempt) rather than pathspec-derived, so narrowing here does not widen what
    # the gate accepts -- an internal sink staged for commit is still exempted by path.
    if [ "$staged" = "--staged" ]; then
        # An EMPTY staged set must return clean HERE. Falling through with an empty
        # `specs` array would leave `git grep -- ` with NO pathspec, which scans the
        # ENTIRE tree -- turning the cheapest possible case into the most expensive one,
        # silently. Nothing staged means nothing to scan.
        if [ ! -s "$staged_set" ]; then
            echo "✅ no-internal-refs-gate: no staged files to scan."
            rm -f "$idset_file" "$staged_set"
            return 0
        fi
        specs=(); mapfile -t specs < "$staged_set"
    fi

    # Pass 1 — direct tokens (small ERE, fast). Pass 2 — bare beads ids (fixed-string,
    # word-bounded, multi-pattern from the 8k-id set). Merge, then apply the src /// rule.
    local raw report total=0
    raw="$(mktemp)"; report="$(mktemp)"

    # The DENOMINATOR — how many public-surface files this invocation actually READ, printed in the
    # standard machine-readable form before any verdict. Without it a "public surface clean" is
    # indistinguishable from a pathspec that stopped resolving: both print the same green.
    # `git grep -lI ''` lists every text file the same pathspec selects, so it is the population the
    # token passes below are about to walk — not a proxy. Zero here is a REFUSE: the staged path
    # already returns early on an empty staged set, so a zero at this point means the scope broke.
    local nir_scanned
    nir_scanned="$(git grep -lI '' -- "${specs[@]}" 2>/dev/null | grep -c . || true)"
    case "$nir_scanned" in ''|*[!0-9]*) nir_scanned=0 ;; esac
    if ! gate_denominator "$nir_scanned" "public-surface file(s)"; then
        rm -f "$idset_file" "$report" "$raw" ${staged_set:+"$staged_set"}
        return 2
    fi

    # Pass 1 drops the loose `bd-` arm and gets it back as a MEMBERSHIP pass (1b), for the
    # reason recorded on nir_hits_in_file: `bd-[a-z0-9]{3,6}` matches our own command names
    # (bd-file, bd-status, bd-export) and hyphenated English (bd-harness, bd-dependent),
    # which were the majority of `bd-` hits once eng/ came into scope. Same fixed-string
    # mechanism as the bare-id pass, so it costs one more grep and no new machinery.
    local ere_nobd="${NIR_TOKEN_ERE#bd-\[a-z0-9\]\{3,6\}|}"
    git grep -nIE "$ere_nobd" -- "${specs[@]}" 2>/dev/null >> "$raw" || true

    # 1b — the `bd-` arm, DETERMINISTIC AND IDENTICAL IN EVERY ENVIRONMENT.
    #
    # This was membership against the tracker, which discriminated perfectly and was wrong
    # anyway: .beads/ is not mirrored, so on the public repository where this gate actually
    # runs the membership set was empty and the code fell back to the loose pattern. The gate
    # was therefore STRICTER on the mirror than locally -- it failed there on `bd-file`,
    # `bd-harness` and `bd-flush-guard`, none of which is a tracker id.
    #
    # A gate whose strictness depends on a file absent from the environment it guards is two
    # different gates. So the loose pattern runs everywhere and the tool names are excluded by
    # a COMMITTED allowlist, which travels with the gate.
    git grep -nIE 'bd-[a-z0-9]{3,6}' -- "${specs[@]}" 2>/dev/null >> "$raw" || true

    # The bare-id arm still needs the tracker by its nature -- a bare six-character token is
    # only distinguishable from an English word by membership. Where the tracker is absent
    # that arm finds nothing, which is a stated reduction in coverage rather than a behaviour
    # difference in the arm above.
    if [ -s "$idset_file" ]; then
        git grep -nIFw -f "$idset_file" -- "${specs[@]}" 2>/dev/null >> "$raw" || true
    fi

    # Tool names (bd-file, bd-harness, …) are not tracker ids. A line survives only if it
    # still carries an internal ref once every allowlisted token is removed from it.
    local allow_file="${NIR_ALLOWLIST:-$(dirname "${BASH_SOURCE[0]}")/no-internal-refs-allowlist.txt}"
    local allow_ere=""
    if [ -f "$allow_file" ]; then
        allow_ere="$(grep -vE '^[[:space:]]*(#|$)' "$allow_file" | paste -sd'|' -)"
    fi

    if [ -s "$raw" ]; then
        sort -u "$raw" | while IFS= read -r line; do
            [ -n "$line" ] || continue
            nir_keep_line "$line" || continue
            # `AC-6` IS BOTH OURS AND NIST'S, AND ONLY THE DOCUMENT CAN TELL YOU WHICH.
            #
            # NIST 800-53 really has AC-2, AC-3, AC-4, AC-5 and AC-6 (Least Privilege, Access
            # Enforcement, Information Flow Enforcement …). In a FedRAMP control mapping those are
            # EXTERNAL references a consumer can resolve and must act on -- stripping them would
            # corrupt a document submitted to an assessor. In one of our own comments the same
            # token is an internal acceptance criterion and is a leak. Measured: `AC-6` appears in
            # both senses in this repository.
            #
            # No pattern can separate them: the shapes are identical. The DOCUMENT's subject can --
            # a file mapping the standard says so, repeatedly. Measured on the six affected files:
            # 21, 12, 16, 16, 4 and 3 mentions of the standard; zero in the source files using the
            # token as an acceptance criterion.
            #
            # So the AC arm alone is suspended in a file that declares the standard. Every other
            # token class still applies there -- a tracker or sprint id in a compliance doc is still
            # a leak. The tempting alternative, allowlisting AC-2…AC-6 globally, would have blinded
            # the gate to real internal AC ids everywhere, including the one real leak found here.
            local _path="${line%%:*}"
            if grep -qiE 'NIST 800-53|FedRAMP' "$_path" 2>/dev/null; then
                local _rest="${line#*:*:}"
                _rest="$(printf '%s' "$_rest" | sed -E 's/\bAC-?[0-9]+[a-z]?//g')"
                printf '%s' "$_rest" | grep -qE "$ere_nobd|bd-[a-z0-9]{3,6}" || continue
            fi

            if [ -n "$allow_ere" ]; then
                local stripped="${line#*:*:}"
                stripped="$(printf '%s' "$stripped" | sed -E "s/(${allow_ere})//g")"
                printf '%s' "$stripped" | grep -qE "$ere_nobd|bd-[a-z0-9]{3,6}" || {
                    if [ -s "$idset_file" ]; then
                        printf '%s' "$stripped" | grep -qFwf "$idset_file" || continue
                    else
                        continue
                    fi
                }
            fi
            if [ -n "$staged_set" ]; then
                grep -qxF -- "${line%%:*}" "$staged_set" || continue
            fi
            printf '%s\n' "$line"
        done > "$report"
    fi

    # `grep -c` PRINTS "0" *and* EXITS 1 when nothing matches, so a `|| echo 0` fires as well and
    # the substitution yields the two-line string "0\n0". The `-eq 0` test below then dies with
    # "integer expected", the condition is false, and an EMPTY report — i.e. a CLEAN public surface —
    # takes the VIOLATIONS branch and blocks. A false RED on exactly the tree that should pass.
    # (Same idiom as the orphan-reaper defect, opposite direction: there it produced a false PASS.)
    total="$(grep -c . "$report" 2>/dev/null | head -1)"
    total="${total:-0}"
    case "$total" in ''|*[!0-9]*) total=0 ;; esac

    if [ "$total" -eq 0 ]; then
        echo "✅ no-internal-refs-gate: public surface clean (no tracker/sprint/ADR/process refs)."
        rm -f "$idset_file" "$report" "$raw" ${staged_set:+"$staged_set"}
        return 0
    fi

    # ---------------------------------------------------------------------------
    # RATCHET — so this gate can be WIRED TODAY instead of after a whole-tree cleanup.
    #
    # Widening the scan to eng/ and .github/workflows/ (the two trees the rule calls public
    # and this gate used to skip) surfaced a backlog that predates the gate being enforced.
    # Demanding it be emptied before anything is wired is how a gate stays unwired -- which
    # is exactly the state this one was in: written, correct, invoked by nothing, while four
    # separate tracker items recorded leaks a human had to find by hand.
    #
    # So: the existing population is recorded PER FILE WITH A COUNT, and the gate fails when
    # a file's count RISES or a file not in the baseline leaks at all. New leaks are blocked
    # from today; the recorded ones shrink as they are fixed. Counts, not line numbers, so an
    # unrelated edit above a leak does not fail the build.
    #
    # A baseline is a debt register, not an exemption: every entry is a leak we still owe.
    local base_file="${NIR_BASELINE:-$(dirname "${BASH_SOURCE[0]}")/no-internal-refs-baseline.txt}"
    if [ "$report_only" != "--report-only" ] && [ -f "$base_file" ]; then
        local cur_counts new_over=0 over_report
        cur_counts="$(mktemp)"; over_report="$(mktemp)"
        cut -d: -f1 "$report" | sort | uniq -c | awk '{print $1" "$2}' > "$cur_counts"
        while read -r cnt path; do
            [ -n "$path" ] || continue
            local allowed
            allowed="$(awk -v p="$path" '$2==p {print $1; exit}' "$base_file")"
            allowed="${allowed:-0}"
            if [ "$cnt" -gt "$allowed" ]; then
                printf '  %s — %s leak(s), baseline allows %s\n' "$path" "$cnt" "$allowed" >> "$over_report"
                new_over=$((new_over + 1))
            fi
        done < "$cur_counts"
        rm -f "$cur_counts"
        if [ "$new_over" -eq 0 ]; then
            local owed; owed="$(awk '{s+=$1} END{print s+0}' "$base_file")"
            echo "✅ no-internal-refs-gate: no NEW public-surface leaks."
            echo "   ${total} recorded leak(s) remain against a baseline of ${owed} — a debt register, not an exemption."
            echo "   Shrink it with: $0 --update-baseline (only ever downward; the gate reds if a count rises)."
            rm -f "$idset_file" "$report" "$raw" "$over_report" ${staged_set:+"$staged_set"}
            return 0
        fi
        echo "❌ no-internal-refs-gate: NEW public-surface internal-ref leak(s) in ${new_over} file(s):"
        echo ""
        cat "$over_report"
        echo ""
        echo "Per .claude/rules/quality/no-internal-refs-in-public-surface.md: rewrite the"
        echo "substance and DROP the internal ref. Document the contract/behavior, not the"
        echo "work item. Do NOT raise the baseline to make this pass — it only moves down."
        rm -f "$idset_file" "$report" "$raw" "$over_report" ${staged_set:+"$staged_set"}
        return 1
    fi

    echo "❌ no-internal-refs-gate: ${total} public-surface internal-ref leak(s):"
    echo ""
    cat "$report"
    echo ""
    echo "Per .claude/rules/quality/no-internal-refs-in-public-surface.md: rewrite the"
    echo "substance and DROP the internal ref (bd-/S###/Sprint #/ADR-#/callsign). Document"
    echo "the contract/behavior, not the work item. Move ids to an internal sink if needed."
    rm -f "$idset_file" "$report" "$raw" ${staged_set:+"$staged_set"}
    if [ "$report_only" = "--report-only" ]; then
        echo ""
        echo "(--report-only: advisory mode, exit 0 — flip to blocking once the tree is clean.)"
        return 0
    fi
    return 1
}

# ---------------------------------------------------------------------------
# Self-test (non-vacuous: MUST flag planted public leaks, MUST ignore exempt sinks)
# ---------------------------------------------------------------------------

self_test() {
    local tmp pass=1
    tmp="$(mktemp -d)"
    # shellcheck disable=SC2064
    trap "rm -rf '$tmp'" RETURN

    # Controlled bare-id set spanning the REAL id-length range (3-6 chars): 'zx9q7k'(6),
    # 'qz7k9'(5), 'ab12'(4), 'x7q'(3) are ids; 'basket' is a 6-char English word NOT an id.
    local idset="$tmp/idset.txt"
    printf '%s\n' 'zx9q7k' 'qz7k9' 'ab12' 'x7q' 'ab12cd' > "$idset"
    local bare_ere="\\b($(paste -sd'|' "$idset"))\\b"

    # --- Fixture 1: a PUBLIC docs-site page with every leak class ------------
    local pub_md="$tmp/public.md"
    cat > "$pub_md" <<'EOF'
# Feature
Fixed the retry loop (bd-abc123) in Sprint 863 per ADR-336.
Delivered under S861 by the FORGE phase, task-2314.
Tracked in bare id zx9q7k as well.
Short prefixed ids also leak: bd-ab12 (4-char) and bd-x7q (3-char).
Short bare ids leak too: qz7k9 (5-char) and x7q (3-char).
Decision records leak under their own prefix: AD-326-1 and AD-520.1.
Requirement ids leak too: implements R999.4 and R999.20a per the spec.
An ADR link spelled the way the FILE is named: see management/architecture/adr-999-a-title.md.
This line mentions a basket and a printout — no leak here.
A road called BROAD-2 and a load balancer LOAD-3 are not decision records.
Object storage on Cloudflare R2 and a CIDR2.4 mask and VAR2.0 are not requirement ids.
Internal plan phases leak as well: delivered under Phase 999.3 and Phase 999.1h-2.
But Phase 1 and Phase 2 of a tutorial are ordinary documentation, not plan ids.
EOF
    local h1
    h1="$(nir_hits_in_file "$pub_md" "$bare_ere")"
    # Covers every leak class AND the sub-6-char ids that the hardcoded {6} gate missed
    # (bd-ab12/bd-x7q prefixed via the ERE; qz7k9/x7q bare via the id set).
    for tok in 'bd-abc123' 'bd-ab12' 'bd-x7q' 'Sprint 863' 'ADR-336' 'S861' 'FORGE' 'task-2314' 'zx9q7k' 'qz7k9' 'AD-326-1' 'AD-520.1' 'R999.4' 'R999.20a' 'adr-999' 'Phase 999.3' 'Phase 999.1h-2'; do
        if ! printf '%s\n' "$h1" | grep -qF "$tok"; then
            echo "self-test FAIL: did not flag public-surface leak '$tok'" >&2; pass=0
        fi
    done
    # Non-vacuity: an English word equal in shape to a bare id but NOT in the id set
    # (and a plain word 'printout') must NOT be flagged.
    if printf '%s\n' "$h1" | grep -qiw 'basket'; then
        echo "self-test FAIL: flagged 'basket' (6-char word not in the id set) — false positive" >&2; pass=0
    fi
    if printf '%s\n' "$h1" | grep -q 'printout'; then
        echo "self-test FAIL: flagged 'printout' (contains 'print', not a sprint ref)" >&2; pass=0
    fi
    # The decision-record arm requires its hyphen for the same reason `EC-` does: drop it and the
    # pattern matches any word ending in those two letters before a digit. These are the words that
    # would break first, so the boundary is asserted rather than assumed.
    for word in 'BROAD-2' 'LOAD-3'; do
        if printf '%s\n' "$h1" | grep -qF "$word"; then
            echo "self-test FAIL: flagged '$word' — a word ending in AD is not a decision record" >&2; pass=0
        fi
    done
    # The requirement-id arm requires its DOT, and these are the terms that break first if it is dropped:
    # `R2` is Cloudflare's object store (a product we may legitimately name), and `CIDR2.4` / `VAR2.0`
    # prove the leading \b is doing its job — a word char before the R means no boundary, so a requirement
    # id can never be matched out of the middle of an identifier. Asserted rather than assumed, because a
    # BLOCKING gate that reds on the word "R2" in a storage doc gets disabled, after which it enforces
    # nothing. Whole-line check: this line must produce NO hit at all.
    if printf '%s\n' "$h1" | grep -qF 'Cloudflare R2'; then
        echo "self-test FAIL: flagged the R2/CIDR2.4/VAR2.0 line — a bare R<n>, or an R<n>.<n> inside a longer identifier, is not a requirement id" >&2; pass=0
    fi
    # The plan-phase arm's dot is load-bearing in the OTHER direction from every arm above: the bare
    # form is ordinary English that our own tutorials use dozens of times ("Phase 2: add the handler").
    # This is the line that must NOT flag, and it is the one that would break first if anyone widened
    # the arm to bare `Phase <n>`.
    if printf '%s\n' "$h1" | grep -qF 'ordinary documentation'; then
        echo "self-test FAIL: flagged bare 'Phase 1'/'Phase 2' tutorial prose — only the DOTTED plan-phase form is an internal id" >&2; pass=0
    fi

    # --- Fixture 2: .cs file — EVERY comment is public, code is not ----------
    #
    # An XML-doc line ships inside the package; a plain implementation comment ships in
    # the source tree that is copied verbatim to a public repository. Both are read by
    # consumers, so both arms below are LIVENESS arms. The SAFETY arm is the last one: a
    # line that is neither a comment nor a string literal carries no prose and must not be
    # flagged, or the gate reds on identifiers and gets turned off.
    local cs="$tmp/Widget.cs"
    cat > "$cs" <<'EOF'
/// <summary>Public doc leaking bd-def456 and Sprint 700.</summary>
public sealed class Widget
{
    // ordinary comment mentioning bd-nnnnnn — ships in the mirrored source tree
    private int _x; // Sprint 999 in a trailing comment, also mirrored
    private const int LimitS886 = 4;
}
EOF
    local h2
    h2="$(nir_hits_in_file "$cs" "$bare_ere")"
    if ! printf '%s\n' "$h2" | grep -qF 'bd-def456'; then
        echo "self-test FAIL: did not flag bd-def456 in a /// XML-doc line" >&2; pass=0
    fi
    if ! printf '%s\n' "$h2" | grep -qF 'bd-nnnnn'; then
        echo "self-test FAIL: did not flag an id in a plain // comment (mirrored source)" >&2; pass=0
    fi
    if ! printf '%s\n' "$h2" | grep -qF 'Sprint 999'; then
        echo "self-test FAIL: did not flag a sprint ref in a trailing // comment" >&2; pass=0
    fi
    if printf '%s\n' "$h2" | grep -qF 'private const'; then
        echo "self-test FAIL: flagged a plain code line (no prose there to leak)" >&2; pass=0
    fi

    # --- Fixture 3: exempt sinks (path classification) ----------------------
    for p in \
        "management/sprints/sprint-863-plan.md" \
        "tests/Unit/FooShould.cs" \
        ".beads/issues.jsonl" \
        ".claude/rules/x.md" \
        "src/Foo/Internal/Helper.cs" \
        "src/Foo/PublicAPI.Shipped.txt" \
        "docs-site/node_modules/pkg/readme.md" \
    ; do
        if ! nir_is_exempt "$p"; then
            echo "self-test FAIL: path '$p' should be EXEMPT but was not" >&2; pass=0
        fi
    done
    # And genuinely-public paths must NOT be exempt.
    for p in \
        "docs-site/docs/guide.md" \
        "samples/01/Program.cs" \
        "CHANGELOG.md" \
        "src/Foo/Widget.cs" \
    ; do
        if nir_is_exempt "$p"; then
            echo "self-test FAIL: public path '$p' was wrongly EXEMPT" >&2; pass=0
        fi
    done

    # --- Fixture 4: nir_bare_id_set admits length-6 ids ONLY (bare-arm FP fix) ---
    # A bare 3-5-char base36 token is undecidable vs prose (`may`/`512`/`pan`/`inf` are
    # real short ids AND ordinary words), so the bare arm covers length-6 only; shorter
    # ids leak in their `bd-` PREFIXED form, caught by NIR_TOKEN_ERE (Fixtures 1/5). This
    # fixture guards against a re-widening regression back to the false-positive surface.
    local jsonl="$tmp/issues.jsonl"
    cat > "$jsonl" <<'EOF'
{"id":"Excalibur.Dispatch-x7q","title":"three — bare arm must NOT admit"}
{"id":"Excalibur.Dispatch-ab12","title":"four — bare arm must NOT admit"}
{"id":"Excalibur.Dispatch-qz7k9","title":"five — bare arm must NOT admit"}
{"id":"Excalibur.Dispatch-zx9q7k","title":"six — admitted"}
{"id":"Excalibur.Dispatch-toolongid7","title":"seven — out of range, must be dropped"}
EOF
    local idout
    idout="$(nir_bare_id_set "$jsonl")"
    # length-6 IS admitted (the bare arm still works for collision-safe ids)
    if ! printf '%s\n' "$idout" | grep -qxF 'zx9q7k'; then
        echo "self-test FAIL: nir_bare_id_set dropped the length-6 id 'zx9q7k'" >&2; pass=0
    fi
    # 3-5-char ids must NOT enter the bare set (undecidable vs prose — the FP class)
    for id in 'x7q' 'ab12' 'qz7k9'; do
        if printf '%s\n' "$idout" | grep -qxF "$id"; then
            echo "self-test FAIL: nir_bare_id_set admitted the ${#id}-char id '$id' — FP re-widening; the bare arm is length-6 only" >&2; pass=0
        fi
    done
    # A 7+char suffix is not a valid bead id shape and must NOT enter the set.
    if printf '%s\n' "$idout" | grep -qxF 'toolongid7'; then
        echo "self-test FAIL: nir_bare_id_set admitted an out-of-range 7-char suffix" >&2; pass=0
    fi

    # ── ANTI-FALSE-POSITIVE ARM: real-shaped prose must NOT flag. The regression lock on
    # the FP class that made this gate unwirable — bare short ids matched as substrings of,
    # or as id-shaped words in, ordinary prose (`may`/`512` in "may fail"/"NVARCHAR(512)",
    # `ardle`/`amazo` inside "regardless"/"Amazon"). The prior synthetic-fixture suite passed
    # while the real repo red-flagged 900+ legitimate lines; without this arm there is no
    # regression lock on that class. Both directions are load-bearing (testing-patterns §3):
    local fpjsonl="$tmp/fp-issues.jsonl"
    cat > "$fpjsonl" <<'EOF'
{"id":"Excalibur.Dispatch-may","title":"3-char — the may-fail FP source"}
{"id":"Excalibur.Dispatch-512","title":"3-char pure-numeric — the NVARCHAR(512) FP source"}
{"id":"Excalibur.Dispatch-ardle","title":"5-char — substring of regardless"}
{"id":"Excalibur.Dispatch-amazo","title":"5-char — substring of Amazon"}
{"id":"Excalibur.Dispatch-tnx4qz","title":"6-char — admitted, collision-safe"}
EOF
    local fpidset fpbare
    fpidset="$(nir_bare_id_set "$fpjsonl")"
    fpbare="\\b($(printf '%s' "$fpidset" | paste -sd'|' -))\\b"
    # SAFETY: prose whose words CONTAIN the excluded short ids as substrings, plus a 6-char
    # id buried INSIDE a longer word (word-anchoring), must produce ZERO hits.
    local fpprose="$tmp/Prose.md"
    printf '%s\n' 'The handler may fail regardless of the Amazon region; NVARCHAR(512), and a tnx4qztail value.' > "$fpprose"
    if [ -n "$(nir_hits_in_file "$fpprose" "$fpbare")" ]; then
        echo "self-test FAIL: anti-FP arm flagged legitimate prose (may/regardless/Amazon/NVARCHAR(512)/tnx4qztail) — the false-positive class regressed" >&2; pass=0
    fi
    # LIVENESS: a WHOLE-WORD admitted 6-char id MUST still flag, or the SAFETY pass is vacuous
    # (a matcher that never matches is trivially FP-free and also enforces nothing).
    local fpprose2="$tmp/Prose2.md"
    printf '%s\n' 'Registration note tnx4qz applies to this sample.' > "$fpprose2"
    if [ -z "$(nir_hits_in_file "$fpprose2" "$fpbare")" ]; then
        echo "self-test FAIL: anti-FP liveness — whole-word 6-char id 'tnx4qz' did not flag; matching is off, so the SAFETY arm proves nothing" >&2; pass=0
    fi

    # --- Fixture 5: the COMPOSED pipeline, per src/ file class --------------
    #
    # Fixtures 1-4 call nir_hits_in_file, which applies nir_scannable_lines and the
    # token regex and NEVER calls nir_keep_line. That is the whole stage-2 policy:
    # which src/ file classes are public. A fixture placed on the stage-1 harness
    # therefore certifies a class the gate may discard downstream -- for .resx it
    # would report coverage that does not exist, which is the defect this block is
    # here to prevent rather than repeat.
    #
    # These cases run BOTH stages against a src/-relative path, because nir_keep_line
    # branches on `src/*` and a $tmp path silently takes its catch-all `return 0`.
    _nir_st_composed() {   # <file> <src-relative-path> -> kept, token-bearing lines
        local f="$1" srcpath="$2" l
        nir_scannable_lines "$f" | grep -E "$NIR_TOKEN_ERE" | while IFS= read -r l; do
            nir_keep_line "${srcpath}:${l}" && printf '%s\n' "$l"
        done
    }

    # .csproj <Description> ships to the package registry and is read there verbatim.
    # Two such leaks were found live on nuget.org, so this class MUST be flagged.
    local csproj="$tmp/Widget.csproj"
    cat > "$csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <Description>Widget support, see ADR-336 for the seam.</Description>
  </PropertyGroup>
</Project>
EOF
    if ! _nir_st_composed "$csproj" "src/Widget/Widget.csproj" | grep -qF 'ADR-336'; then
        echo "self-test FAIL: composed pipeline did not flag ADR-336 in a .csproj <Description> (ships to the package registry)" >&2; pass=0
    fi

    # .resx <value> ships compiled into the assembly and the rule's surface list names it
    # (resource-value). It carries consumer- and auditor-facing strings, so an internal ref in
    # a <value> MUST be flagged. This proves stage 2 (nir_keep_line) now KEEPS .resx — the
    # deferral this case used to pin is resolved (a relocated auditor string stays findable).
    local resx="$tmp/Strings.resx"
    cat > "$resx" <<'EOF'
<root>
  <data name="AuditNote"><value>Retention per ADR-336.</value></data>
</root>
EOF
    if ! _nir_st_composed "$resx" "src/Widget/Strings.resx" | grep -qF 'ADR-336'; then
        echo "self-test FAIL: composed pipeline did not flag ADR-336 in a .resx <value> (ships in the assembly; the rule names it resource-value)" >&2; pass=0
    fi

    # A .cs string literal is public surface (compiled in, read verbatim by consumers).
    # Fixture 2 proves stage 1 offers it; this proves stage 2 keeps it.
    local cslit="$tmp/Thrower.cs"
    cat > "$cslit" <<'EOF'
public sealed class Thrower
{
    public void Go() => throw new InvalidOperationException("blocked, see ADR-336");
}
EOF
    if ! _nir_st_composed "$cslit" "src/Widget/Thrower.cs" | grep -qF 'ADR-336'; then
        echo "self-test FAIL: composed pipeline did not flag ADR-336 in a .cs string literal (ships in the assembly)" >&2; pass=0
    fi

    # ── ANTI-DRIFT: the gate must KEEP every src/ surface the RULE's machine-readable include-list
    # names, or the two silently diverge — the exact failure that left four surfaces uncovered while
    # each side looked internally consistent. This derives the check FROM the rule rather than a
    # second hand-maintained list: it reads the rule's `src/**/*.<ext>` include entries and asserts
    # nir_keep_line keeps a representative of each. So when the rule gains a public surface, this arm
    # fails until the gate covers it. Reads a .claude path (absent on the public mirror) → SKIPS there;
    # a dev-time coherence guard, not a shipped check.
    local rule_file=".claude/rules/quality/no-internal-refs-in-public-surface.md"
    if [ -f "$rule_file" ]; then
        local exts ext repr shape
        exts="$(grep -oE 'path: "src/\*\*/\*\.[a-zA-Z]+"' "$rule_file" | grep -oE '\.[a-zA-Z]+"' | tr -d '"' | sort -u)"
        if [ -z "$exts" ]; then
            echo "self-test FAIL: anti-drift read ZERO src/ surface extensions from the rule — the include block moved or the parse broke (a drift-check that reads nothing is vacuous)" >&2; pass=0
        else
            while IFS= read -r ext; do
                [ -n "$ext" ] || continue
                repr="src/Widget/Probe${ext}"
                # .cs is public only on /// or string-literal lines; hand it a string literal.
                # .csproj is public only via package-metadata elements (not MSBuild comments);
                # hand it a <Description> so the drift-check probes the KEPT surface.
                case "$ext" in
                    .cs) shape='    throw new Exception("see ADR-336");' ;;
                    .csproj) shape='    <Description>see ADR-336</Description>' ;;
                    *)   shape='see ADR-336' ;;
                esac
                if ! nir_keep_line "${repr}:1:${shape}"; then
                    echo "self-test FAIL: the rule names src/**/*${ext} public, but nir_keep_line DISCARDS it — the gate has drifted from the rule (u16o4h class)" >&2; pass=0
                fi
            done <<< "$exts"
            [ "$pass" -eq 1 ] && printf '  anti-drift: nir_keep_line keeps every src/ surface the rule names (%s)\n' "$(printf '%s ' $exts)"
        fi
    else
        echo "  anti-drift: SKIPPED (rule file absent — mirror/shallow clone); local runs assert gate<->rule coherence"
    fi

    if [ "$pass" -eq 1 ]; then
        # Enumerate what is actually exercised. The previous message claimed "every
        # public leak class" while planting no .csproj and no .resx case, so it
        # certified coverage the gate did not have and could not acquire.
        echo "✅ no-internal-refs-gate self-test PASSED."
        echo "   covered: docs-site/.md · .cs XML-doc · .cs string literal · .csproj <Description> · .resx <value> · prefixed bd-ids · bare ids (length-6 only) · requirement ids (R<n>.<n>) · plan phases (Phase <n>.<n>) · lowercase adr- links · exempt-sink paths"
        echo "   drift-check: every src/ surface the rule's include-list names is KEPT by nir_keep_line (asserted below)."
        return 0
    fi
    echo "❌ no-internal-refs-gate self-test FAILED." >&2
    return 3
}

# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

usage() { sed -n '2,33p' "$0" | sed 's/^# \{0,1\}//'; }

main() {
    local staged="" report_only=""
    while [ $# -gt 0 ]; do
        case "$1" in
            --self-test)   self_test; exit $? ;;
            --staged)      staged="--staged"; shift ;;
            --report-only) report_only="--report-only"; shift ;;
            --update-baseline)
                # Regenerate the debt register from the current tree. Only ever run this
                # after FIXING leaks: it rewrites counts, so running it after introducing
                # one would launder the new leak into the baseline. The gate cannot detect
                # that for you -- the commit diff can, which is why the file is tracked.
                base="${NIR_BASELINE:-$(dirname "${BASH_SOURCE[0]}")/no-internal-refs-baseline.txt}"
                tmpr="$(mktemp)"
                run_gate "" "--report-only" 2>/dev/null | grep -E '^[A-Za-z0-9_.][^ ]*:[0-9]+:' > "$tmpr" || true
                {
                    echo "# no-internal-refs debt register — <count> <path>, one per file."
                    echo "# Every line is a public-surface leak we still owe. The gate fails when a"
                    echo "# count RISES or an unlisted file leaks. It only moves down."
                    echo "#"
                    echo "# THIS FILE IS THE INSTRUMENT, NOT THE WORK. It makes the remaining leaks"
                    echo "# bounded and visible and forbids new ones; it does not remove a single"
                    echo "# identifier. Do not read a green gate here as evidence that the"
                    echo "# strip-the-identifiers work is finished — that work is finished when this"
                    echo "# register is EMPTY."
                    echo "#"
                    echo "# Regenerate only after FIXING leaks, and only immediately before review:"
                    echo "# the counts describe the tree at the moment of generation and decay as"
                    echo "# soon as any listed file is edited."
                    cut -d: -f1 "$tmpr" | sort | uniq -c | awk '{print $1" "$2}' | sort -k2
                } > "$base"
                echo "baseline written: $base ($(grep -cE '^[0-9]' "$base") file(s), $(awk '/^[0-9]/{s+=$1} END{print s+0}' "$base") leak(s))"
                rm -f "$tmpr"
                exit 0 ;;
            -h|--help)     usage; exit 0 ;;
            *) echo "no-internal-refs-gate: unknown arg '$1'" >&2; usage; exit 2 ;;
        esac
    done
    run_gate "$staged" "$report_only"
    exit $?
}

main "$@"
