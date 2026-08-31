#!/usr/bin/env bash
# sqlserver-ddl-hygiene.sh — two properties of shipped SQL Server DDL that fail SILENTLY.
#
# Both defects below shipped. Neither produced a failing build, a failing test, or a runtime
# exception on our machines, because both are properties of a .sql file we hand to a consumer
# and never execute ourselves.
#
# PROPERTY 1 — A TENANT COLUMN MUST PIN A BINARY COLLATION
#
#   SQL Server's server default collation is typically case-INSENSITIVE, under which
#   'Acme' = 'acme' is TRUE. Every tenant-scoped read is written as `TenantId = @TenantId`, so
#   an unpinned tenant column makes that predicate match MORE ROWS THAN IT SHOULD — one tenant
#   reads another tenant's rows whenever two tenant identifiers differ only by case.
#
#   Nothing errors. The query succeeds and returns the wrong set. This is the failure mode the
#   whole tenant-isolation design exists to prevent, arriving through storage rather than code.
#
#   Measured before the fix: dead-letter messages, whose rows carry the failed message BODY,
#   declared TenantId with no COLLATE while its store filtered on it in three statements.
#   A scoped read for 'acme' returned a row owned by 'Acme'. Every sibling store pinned it.
#
# PROPERTY 2 — A FILE CONTAINING A FILTERED INDEX MUST SET QUOTED_IDENTIFIER ON
#
#   SQL Server refuses to create a filtered index (CREATE INDEX ... WHERE) unless
#   QUOTED_IDENTIFIER is ON. sqlcmd — the standard way to run a .sql file — defaults it OFF.
#
#   Measured before the fix: 001_CreateAuditSchema.sql through sqlcmd produced 4 errors and
#   ZERO filtered indexes. With the setting: 0 errors, 4 indexes. Six shipped files were
#   affected. A consumer whose script runner does not check exit status gets a database
#   silently missing its most selective indexes, and discovers it as unexplained query cost
#   months later.
#
# WHY A GATE AND NOT A UNIT TEST
#
#   Neither property is observable from managed code. The artifact under test is a .sql file
#   shipped to a consumer, and the defect is in what the file DOES NOT SAY. A test that ran the
#   scripts against a container would catch property 2 only if it asserted index existence
#   afterwards, and would miss property 1 entirely unless it happened to seed two tenants whose
#   identifiers differed by case. Reading the declaration is the direct measurement.
#
# THE THREE STATES — REFUSE IS NOT A PASS
#   0 PASS     every scanned file satisfies both properties
#   1 FAIL     a file violates one of them
#   2 REFUSE   could not build a population to scan, so NOTHING was measured
#   3 REFUSE   --self-test failed (this gate is broken or vacuous)
#
#   REFUSE exists because the failure mode of a scanner is to go green over an empty
#   population: "0 violations" is trivially true across 0 files and reads exactly like a clean
#   tree. An empty scan is therefore a hard REFUSE, never a pass.
#
#   There is deliberately NO suppression cap and no allowlist. A gate that can mute itself into
#   a PASS is the defect this design exists to remove.
#
# SCOPE — SQL SERVER ONLY, AND THE EXCLUSION IS THE POINT
#
#   Postgres compares varchar case-sensitively already, and Oracle has neither the collation
#   syntax nor QUOTED_IDENTIFIER. Applying either property to them would produce false failures
#   and train the team to ignore this gate. Dialect is detected from CONTENT (bracketed
#   identifiers, NVARCHAR, GO batch separators), not from a path convention, so a SQL Server
#   script that lands outside a *.SqlServer directory is still covered.

set -uo pipefail

E_PASS=0; E_FAIL=1; E_REFUSE=2; E_SELFTEST=3

# Resolved INSIDE run_scan, not here. A top-level assignment is evaluated once at script load,
# so a self-test arm that exports SQLDDL_REPO_ROOT before calling the function would have no
# effect and would silently measure the REAL repository instead of its fixture — every arm
# returning the real verdict while appearing to test a planted one. That is a self-test that
# cannot fail for the right reason, which is worse than no self-test.
resolve_roots() {
    REPO_ROOT="${SQLDDL_REPO_ROOT:-$(git rev-parse --show-toplevel 2>/dev/null || echo .)}"
    SCAN_ROOTS="${SQLDDL_SCAN_ROOTS:-src samples docs-site}"
}

# ── comment stripping ───────────────────────────────────────────────────────────────────────
#
# Every predicate below runs against the file with its COMMENTS BLANKED, never the raw text.
#
# This is not tidiness. A SQL Server script commonly carries a commented-out schema for another
# dialect, and this repo's poison-message script carries exactly that: a Postgres CREATE TABLE
# inside a /* */ block, whose tenant column correctly has no COLLATE because Postgres compares
# varchar case-sensitively. Scanning raw text reported that inert, correct line as a
# tenant-isolation violation — a false positive on the gate's own flagship property, which is
# the fastest way to train a team to ignore a gate.
#
# Lines are BLANKED rather than removed so reported line numbers still match the real file.
# T-SQL block comments nest, so depth is tracked rather than matched pairwise.
strip_comments() {
    awk '
        {
            line = $0; out = ""; i = 1; n = length(line)
            while (i <= n) {
                two = substr(line, i, 2)
                if (depth == 0 && two == "--") { break }
                if (two == "/*") { depth++; i += 2; continue }
                if (two == "*/" && depth > 0) { depth--; i += 2; continue }
                if (depth == 0) { out = out substr(line, i, 1) }
                i++
            }
            print out
        }
    ' "$1" 2>/dev/null
}

# ── markdown fence extraction ───────────────────────────────────────────────────────────────
#
# docs-site ships DDL too, inside ```sql fences, and a CREATE TABLE in a doc is not
# documentation — it is code a consumer pastes into their own production database. It is also
# the only schema artifact whose drift we cannot fix for them afterwards.
#
# Each fence is extracted and judged SEPARATELY, never the file as a whole. One page routinely
# shows the SQL Server form and the PostgreSQL form of the same table; judging the file would
# apply T-SQL rules to the Postgres block and report its correctly-uncollated tenant column as
# a violation. Per-fence is the only way this stays true.
#
# Line numbers are preserved by blanking everything outside the fence, so a reported location
# still points at the real line in the real file.
extract_fence() {
    local file="$1" want="$2"
    awk -v want="$want" '
        BEGIN { inf = 0; idx = 0 }
        /^[ 	]*```/ {
            if (inf) { inf = 0 } else { idx++; inf = (idx == want) ? 1 : 2 }
            print ""; next
        }
        { if (inf == 1) print $0; else print "" }
    ' "$file" 2>/dev/null
}

fence_count() {
    awk '/^[ 	]*```/ { n++ } END { print int(n / 2) }' "$1" 2>/dev/null
}

# ── dialect + property predicates ───────────────────────────────────────────────────────────

# A file is SQL Server if it uses T-SQL constructs no other dialect has.
is_sqlserver() {
    local f="$1"
    grep -qiE 'NVARCHAR|\[dbo\]|^\s*GO\s*$|sys\.(objects|columns|indexes)' "$f" 2>/dev/null
}

# A filtered index is an index carrying a WHERE. It appears in TWO forms and this predicate must
# see both, because SQL Server's QUOTED_IDENTIFIER requirement does not care which one you wrote:
#
#   STANDALONE   CREATE [UNIQUE] [NON]CLUSTERED INDEX ix ON t (cols) WHERE pred
#   INLINE       CREATE TABLE t ( ..., INDEX ix (cols) WHERE pred )
#
# The inline form has NO `CREATE` before `INDEX` — it is a table element, not a statement — so a
# predicate that enters its scan on /CREATE ... INDEX/ is blind to it BY CONSTRUCTION, not by
# tuning. It cannot be fixed by loosening the WHERE match, because the scan never starts.
#
# That blindness shipped: the compliance schema declared an inline filtered index on LegalHolds and
# set QUOTED_IDENTIFIER nowhere, and this gate reported the file clean while sqlcmd refused to
# create the table at all. It is the same shape as the unindented-WHERE miss recorded below — a
# pattern that encoded how the author happened to write the DDL rather than what the DDL does.
has_filtered_index() {
    local f="$1"
    awk '
        BEGIN { IGNORECASE = 1; instmt = 0; found = 0 }
        /CREATE[ \t]+(UNIQUE[ \t]+)?(NONCLUSTERED[ \t]+|CLUSTERED[ \t]+)?INDEX/ { instmt = 1 }
        instmt && /^[ \t]*(GO|;)[ \t]*$/ { instmt = 0 }
        # WHERE may begin the line, so it cannot require leading whitespace. Requiring it made
        # this predicate blind to any script that puts the filter on its own unindented line —
        # the gate would have reported those files clean forever.
        instmt && /(^|[ \t])WHERE([ \t]|$)/ { found = 1 }

        # INLINE form, single line: INDEX ix (cols) WHERE pred. Matched as one complete shape —
        # name, parenthesised column list, then WHERE — so it cannot bind a WHERE belonging to a
        # later statement the way a sticky in-scan flag could.
        /(^|[ \t])INDEX[ \t]+[^(]+\([^)]*\)[ \t]*WHERE([ \t]|$)/ { found = 1 }

        # INLINE form, wrapped: the filter sits on a continuation line. Entered only on a table
        # element that opens and closes its column list on one line, and released at the end of the
        # table body or batch, so an unrelated WHERE cannot be captured.
        /(^|[ \t])INDEX[ \t]+[^(]+\([^)]*\)[ \t]*$/ { inidx = 1; next }
        inidx && /(^|[ \t])WHERE([ \t]|$)/ { found = 1; inidx = 0 }
        inidx && /^[ \t]*(GO|\)[ \t]*;?)[ \t]*$/ { inidx = 0 }
        END { exit(found ? 0 : 1) }
    ' "$f" 2>/dev/null
}

sets_quoted_identifier() {
    grep -qiE '^[ \t]*SET[ \t]+QUOTED_IDENTIFIER[ \t]+ON' "$1" 2>/dev/null
}

# Tenant column DECLARATIONS only — a line declaring TenantId with a type. Comments and
# predicates that merely mention the word are not declarations and must not be scanned,
# or the gate cries wolf on its own documentation.
#
# The optional ADD / ALTER COLUMN prefix is load-bearing, not defensive. A migration declares
# its tenant column on a continuation line — `ALTER TABLE x` then `    ADD [TenantId] ...` —
# so a pattern anchored straight at the identifier is blind to every ALTER path in the repo,
# including the dead-letter upgrade whose missing collation is why this gate exists. It would
# have passed that column unpinned.
tenant_declarations() {
    grep -nE '^[ \t]*((ADD|ALTER[ \t]+COLUMN)[ \t]+)?\[?[Tt]enant_?[Ii]d\]?[ \t]+(\[?[Nn][Vv][Aa][Rr][Cc][Hh][Aa][Rr]\]?|\[?[Vv][Aa][Rr][Cc][Hh][Aa][Rr]\]?)' "$1" 2>/dev/null
}

declaration_pins_collation() {
    printf '%s\n' "$1" | grep -qiE 'COLLATE[ \t]+Latin1_General_BIN2'
}

# ── the scan ────────────────────────────────────────────────────────────────────────────────

run_scan() {
    local files fails=0 scanned=0 checked_tenant=0 checked_filtered=0

    resolve_roots

    # Bound the population BEFORE the per-unit work, or this gate is unaffordable and therefore
    # unrun. Measured: docs-site carries 2747 markdown files holding 15728 fences, and judging
    # every fence spawns several processes each — tens of thousands of subprocess launches, which
    # under real-time AV scanning takes long enough that nobody waits for it.
    #
    # The filter admits any file that CREATES A TABLE, CREATES AN INDEX, or ALTERS A TABLE.
    #
    # An earlier version admitted only CREATE TABLE, reasoning that both properties are
    # properties of a table definition. That was WRONG and it silently shrank coverage: a
    # migration script alters a tenant column and creates indexes without ever creating a table,
    # and an index-only script exists for exactly that purpose. Six shipped files were excluded,
    # including this repository's own multi-tenant migration — precisely the files where both
    # properties matter most. The tell was that adding a scan root made the filtered-index count
    # go DOWN, which no correct filter can do.
    files="$(
        for r in $SCAN_ROOTS; do
            [ -d "$REPO_ROOT/$r" ] || continue
            find "$REPO_ROOT/$r" -type f \( -name '*.sql' -o -name '*.md' \) 2>/dev/null
        done | sort -u | tr '\n' '\0' \
             | xargs -0 -r grep -ilE 'create[ \t]+table|create[ \t]+([a-z]+[ \t]+)*index|alter[ \t]+table' 2>/dev/null
    )"

    if [ -z "$files" ]; then
        echo "sqlserver-ddl-hygiene: REFUSE — found no DDL under {$SCAN_ROOTS}."
        echo "  A repo that ships DDL and scans none of it is a broken query, not a clean result."
        return $E_REFUSE
    fi

    echo "=== SQL Server DDL hygiene: tenant collation + filtered-index prerequisites ==="
    echo ""

    # One UNIT of judgement: a whole .sql file, or a single fence from a .md page. Both are
    # judged identically, which is what keeps a doc's DDL held to the same standard as a
    # shipped script.
    check_unit() {
        local content="$1" label="$2"

        is_sqlserver "$content" || return 0
        scanned=$((scanned + 1))

        # Predicates run against the comment-stripped copy (see strip_comments).
        local stripped; stripped="$(mktemp)"
        strip_comments "$content" > "$stripped"

        # Property 2 — filtered index requires QUOTED_IDENTIFIER
        if has_filtered_index "$stripped"; then
            checked_filtered=$((checked_filtered + 1))
            if sets_quoted_identifier "$stripped"; then
                echo "  ok    $label — filtered index, sets QUOTED_IDENTIFIER"
            else
                echo "  FAIL  $label"
                echo "        contains a FILTERED index but never sets QUOTED_IDENTIFIER ON."
                echo "        Under sqlcmd every filtered index here fails with Msg 1934 and is ABSENT."
                fails=$((fails + 1))
            fi
        fi

        # Property 1 — a declared tenant column pins a binary collation
        local decls; decls="$(tenant_declarations "$stripped")"
        if [ -n "$decls" ]; then
            while IFS= read -r d; do
                [ -n "$d" ] || continue
                checked_tenant=$((checked_tenant + 1))
                if declaration_pins_collation "$d"; then
                    echo "  ok    $label:${d%%:*} — tenant column pins Latin1_General_BIN2"
                else
                    echo "  FAIL  $label:${d%%:*}"
                    echo "        tenant column declared without COLLATE Latin1_General_BIN2."
                    echo "        Under a case-insensitive server default 'Acme' = 'acme', so a"
                    echo "        scoped read returns another tenant's rows. Nothing errors."
                    fails=$((fails + 1))
                fi
            done <<< "$decls"
        fi

        rm -f "$stripped"
    }

    while IFS= read -r f; do
        [ -n "$f" ] || continue
        local rel="${f#$REPO_ROOT/}"

        case "$f" in
            *.md)
                # Judge each fence on its own. A page routinely shows the SQL Server form and
                # the PostgreSQL form of the same table; judging the page as a whole would apply
                # T-SQL rules to the Postgres block and report its correctly-uncollated tenant
                # column as a violation.
                local n i frag
                n="$(fence_count "$f")"
                [ -n "$n" ] && [ "$n" -gt 0 ] 2>/dev/null || continue
                i=1
                while [ "$i" -le "$n" ]; do
                    frag="$(mktemp)"
                    extract_fence "$f" "$i" > "$frag"
                    check_unit "$frag" "$rel (fence $i)"
                    rm -f "$frag"
                    i=$((i + 1))
                done
                ;;
            *)
                check_unit "$f" "$rel"
                ;;
        esac
    done <<< "$files"

    echo ""
    echo "  sql-server files scanned=$scanned  tenant declarations=$checked_tenant  filtered-index files=$checked_filtered"

    # Liveness: a scan that measured NOTHING is not a pass, even over a non-empty file list.
    if [ "$checked_tenant" -eq 0 ] && [ "$checked_filtered" -eq 0 ]; then
        echo "sqlserver-ddl-hygiene: REFUSE — scanned $scanned file(s) and evaluated no property."
        echo "  Either the predicates stopped matching or the population is wrong. Not a pass."
        return $E_REFUSE
    fi

    if [ "$fails" -gt 0 ]; then
        echo "::error::sqlserver-ddl-hygiene: FAIL — $fails violation(s)."
        return $E_FAIL
    fi

    echo "sqlserver-ddl-hygiene: PASS — every tenant column pins a binary collation, and every"
    echo "  file with a filtered index sets QUOTED_IDENTIFIER."
    return $E_PASS
}

# ── self-test: prove the gate is NON-VACUOUS in both directions ─────────────────────────────
#
# The liveness arms are the ones that would be forgotten, and the ones that matter: without
# them a gate hardcoded to `return 1` passes its own safety test forever, and a gate whose
# predicates match nothing reports a clean tree indefinitely.

self_test() {
    local tmp rc failures=0
    tmp="$(mktemp -d)"
    trap 'rm -rf "$tmp"' RETURN

    mk() { mkdir -p "$(dirname "$tmp/$1")"; cat > "$tmp/$1"; }

    arm() {
        local label="$1" want="$2" root="$3"
        ( SQLDDL_REPO_ROOT="$tmp" SQLDDL_SCAN_ROOTS="$root" run_scan ) >/dev/null 2>&1
        rc=$?
        if [ "$rc" -eq "$want" ]; then
            echo "  ok    $label (exit $rc)"
        else
            echo "  FAIL  $label — got $rc, wanted $want"
            failures=$((failures + 1))
        fi
    }

    # SAFETY 1: unpinned tenant column is detected
    mk bad1/a.sql <<'EOF'
CREATE TABLE [dbo].[Thing] (
    [Id] [nvarchar](32) NOT NULL,
    [TenantId] [nvarchar](255) NOT NULL DEFAULT '__untenanted__',
);
GO
EOF
    arm "SAFETY   unpinned tenant column detected" 1 bad1

    # SAFETY 2: filtered index without QUOTED_IDENTIFIER is detected
    mk bad2/b.sql <<'EOF'
CREATE TABLE [dbo].[Thing] ([Id] [nvarchar](32) NOT NULL, [Corr] [nvarchar](64) NULL);
GO
CREATE NONCLUSTERED INDEX [IX_Thing_Corr] ON [dbo].[Thing] ([Corr])
WHERE [Corr] IS NOT NULL
GO
EOF
    arm "SAFETY   filtered index without QUOTED_IDENTIFIER detected" 1 bad2

    # SAFETY 3: the INLINE form — a filtered index declared as a CREATE TABLE element, with no
    # `CREATE` before `INDEX`. This is the shape the gate was blind to while it shipped.
    mk bad3/c.sql <<'EOF'
CREATE TABLE [compliance].[Holds] (
    [HoldId] [uniqueidentifier] NOT NULL PRIMARY KEY,
    [TenantId] [nvarchar](255) COLLATE Latin1_General_BIN2 NOT NULL,
    [IsActive] [bit] NOT NULL,
    [ExpiresAt] [datetimeoffset] NULL,
    INDEX IX_Holds_ExpiresAt (IsActive, ExpiresAt) WHERE IsActive = 1 AND ExpiresAt IS NOT NULL
);
GO
EOF
    arm "SAFETY   INLINE filtered index without QUOTED_IDENTIFIER detected" 1 bad3

    # SAFETY 4: the inline form with the filter on a continuation line.
    mk bad4/d.sql <<'EOF'
CREATE TABLE [compliance].[Holds] (
    [HoldId] [uniqueidentifier] NOT NULL PRIMARY KEY,
    [TenantId] [nvarchar](255) COLLATE Latin1_General_BIN2 NOT NULL,
    [IsActive] [bit] NOT NULL,
    INDEX IX_Holds_Active (IsActive)
        WHERE IsActive = 1
);
GO
EOF
    arm "SAFETY   INLINE filtered index, wrapped filter, detected" 1 bad4

    # LIVENESS 3: an inline index with NO filter must NOT be treated as filtered. This is the arm
    # that stops the new inline rules from crying wolf on every table that declares an index.
    mk good3/e.sql <<'EOF'
CREATE TABLE [compliance].[Holds] (
    [HoldId] [uniqueidentifier] NOT NULL PRIMARY KEY,
    [TenantId] [nvarchar](255) COLLATE Latin1_General_BIN2 NOT NULL,
    [IsActive] [bit] NOT NULL,
    INDEX IX_Holds_Tenant (TenantId, IsActive)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Other')
BEGIN
    SELECT 1 WHERE 1 = 0;
END
GO
EOF
    arm "LIVENESS inline NON-filtered index is not flagged" 0 good3

    # LIVENESS 1: a correct file is ALLOWED
    mk good1/c.sql <<'EOF'
SET QUOTED_IDENTIFIER ON;
GO
CREATE TABLE [dbo].[Thing] (
    [Id] [nvarchar](32) NOT NULL,
    [TenantId] [nvarchar](255) COLLATE Latin1_General_BIN2 NOT NULL,
    [Corr] [nvarchar](64) NULL
);
GO
CREATE NONCLUSTERED INDEX [IX_Thing_Corr] ON [dbo].[Thing] ([Corr])
WHERE [Corr] IS NOT NULL
GO
EOF
    arm "LIVENESS correct file is allowed" 0 good1

    # LIVENESS 2: a POSTGRES file with an unpinned tenant column is ALLOWED.
    # This is the arm that keeps the gate from crying wolf across dialects.
    mk good2/d.sql <<'EOF'
CREATE TABLE IF NOT EXISTS public.things (
    id varchar(32) NOT NULL,
    tenant_id varchar(200) NOT NULL DEFAULT '__untenanted__'
);
CREATE INDEX ix_things_corr ON public.things (corr) WHERE corr IS NOT NULL;
EOF
    arm "LIVENESS postgres file is not judged by T-SQL rules" 2 good2

    # LIVENESS 3: an unpinned tenant column INSIDE A COMMENT is ALLOWED.
    # This is the arm that would be forgotten. A SQL Server script commonly carries a
    # commented-out schema for another dialect; judging it produces a false failure on the
    # gate's flagship property, which is how a gate gets ignored. Regression-locks the real
    # false positive this gate produced against the poison-message script's Postgres block.
    mk good3/f.sql <<'EOF'
SET QUOTED_IDENTIFIER ON;
GO
CREATE TABLE [dbo].[Thing] (
    [Id] [nvarchar](32) NOT NULL,
    [TenantId] [nvarchar](255) COLLATE Latin1_General_BIN2 NOT NULL
);
GO
-- PostgreSQL equivalent, commented out. varchar compares case-sensitively there, so the
-- absence of a COLLATE clause is correct and must not be reported.
/*
CREATE TABLE IF NOT EXISTS public.things (
    tenant_id VARCHAR(255) NOT NULL DEFAULT '__untenanted__'
);
*/
EOF
    arm "LIVENESS commented-out unpinned tenant column is allowed" 0 good3

    # SAFETY 3: an INDEX-ONLY file is still judged.
    # Regression-locks a real soundness bug: an earlier population filter admitted only files
    # containing CREATE TABLE, which silently excluded six shipped scripts — every migration and
    # index-only script, including this repo's own multi-tenant migration. A gate that quietly
    # narrows its own population reports a clean tree it never looked at.
    mk bad3/g.sql <<'EOF'
CREATE NONCLUSTERED INDEX [IX_Thing_Corr] ON [dbo].[Thing] ([Corr])
WHERE [Corr] IS NOT NULL
GO
EOF
    arm "SAFETY   index-only file (no CREATE TABLE) is still judged" 1 bad3

    # SAFETY 4: an ALTER-only migration that adds an unpinned tenant column is still judged.
    mk bad4/h.sql <<'EOF'
ALTER TABLE [dbo].[Thing]
    ADD [TenantId] [nvarchar](255) NOT NULL DEFAULT '__untenanted__';
GO
EOF
    arm "SAFETY   alter-only migration is still judged" 1 bad4

    # REFUSE 1: empty population is not a pass
    mkdir -p "$tmp/empty"
    arm "REFUSE   empty population is not a pass" 2 empty

    # REFUSE 2: T-SQL files that declare no tenant column and no filtered index evaluate
    # NOTHING, which must not read as clean.
    mk none/e.sql <<'EOF'
CREATE TABLE [dbo].[Plain] ([Id] [nvarchar](32) NOT NULL);
GO
EOF
    arm "REFUSE   scanned files but evaluated no property" 2 none

    echo "  ---"
    if [ "$failures" -eq 0 ]; then
        echo "  self-test PASS — gate is non-vacuous in both directions."
        return 0
    fi
    echo "::error::  self-test FAILED ($failures arm(s)). This gate cannot be trusted." >&2
    return 1
}

case "${1:-}" in
    --self-test)
        echo "sqlserver-ddl-hygiene --self-test: proving this gate can report FAIL, PASS and REFUSE"
        if self_test; then exit 0; else exit $E_SELFTEST; fi
        ;;
    *)
        run_scan
        exit $?
        ;;
esac
