#!/usr/bin/env bash
# shipped-ddl-sweep.sh — the DDL we SHIP must declare every column the code WRITES/READS.
#
# WHY THIS IS NOT AN f5-sweep ARM (read before "simplifying" it into one):
#
#   f5-sweep is a STALE-REFERENCE detector: it extracts contract tokens from changed src and
#   greps siblings for files still naming the OLD contract. It finds things that are PRESENT
#   and shouldn't be.
#
#   This defect is the exact inverse. `docs-site/docs/patterns/outbox.md` shipped a CREATE TABLE
#   that OMITTED error_message while SetOutboxMessageFailed.cs:67 writes it. Nothing is stale;
#   something is ABSENT. **You cannot find an absence by grepping for presence.** A token sweep
#   over docs-site/** is structurally incapable of seeing this class — it would report clean
#   forever. (the composed-control rule: a control sharing the query's filter
#   cannot detect the filter's blind spot.)
#
# WHY IT MATTERS MORE THAN THE TEST-FIXTURE CASE:
#
#   stale tests/** fixture DDL -> "Invalid column name" on the integration shard -> CI catches it.
#   stale docs-site/** DDL     -> nothing, ever. It runs on the CONSUMER's database, not ours.
#                                 It compiles, passes ~112k tests, ships, and fails the first time
#                                 a consumer follows our own instructions. There is no shard to go
#                                 red, because we never run it.
#
# WHY THE MAPPING IS EXPLICIT AND NOT INFERRED:
#
#   The table name is INTERPOLATED in the shipped code -- `UPDATE {outboxTableName}` -- so no src
#   file contains the literal `outbox_messages`. DDL->code cannot be matched by table name. Column
#   names ARE literal (`error_message = @ErrorMessage`), so the columns are comparable once you know
#   WHICH src owns a given shipped DDL. That mapping is declared below, auditably, by a human.
#
#   THE HONESTY PROPERTY THAT FALLS OUT: a shipped CREATE TABLE with no MAP entry is a REFUSE
#   (exit 2), never a PASS. Coverage gaps announce themselves instead of reporting clean. Adding a
#   new shipped DDL therefore forces a deliberate mapping decision -- the gate cannot silently
#   ignore a schema it was never taught about. (enforce-invariants-structurally.)
#
# WRITE/READ CLAUSES COVERED (the FencingToken/LeasedAt break lived in
# WHERE/SET/OUTPUT, so UPDATE-SET-only extraction misses it; cover every clause a column can hide in):
#   - UPDATE ... SET col = @Param            (param-assignment form)
#   - WHERE col = @Param                     (same param-assignment form, inside a read/write stmt)
#   - INSERT INTO {tbl} (colA, colB, ...)    (parenthesised column list)
#   - OUTPUT inserted.col / deleted.col      (SQL Server OUTPUT clause column refs)
#
# DELIBERATE NON-COVERAGE (documented, not silent — an unstated gap is the defect this gate stops):
#   - Bare `SELECT col, col FROM {tbl}` projection lists are NOT extracted. Bare identifier lists in
#     SELECT are indistinguishable from aliases/expressions/function args without a SQL parser, and a
#     false positive here makes the gate cry wolf and get ignored -- which is worse than no gate (SA's
#     own MAP note). A column read by a bare SELECT that is ALSO written (SET/INSERT) or filtered
#     (WHERE) is already covered via those clauses; a read-ONLY column not otherwise touched is the
#     residual gap. Tracked as a follow-up increment; raised to SA as a deliberate call, not an omission.
#
# EXIT CODES (declared; the call site maps every one -- a non-0/1 exit is NEVER
# a pass):
#   0  PASS    evaluated, every written/read column is declared in the shipped DDL
#   1  FAIL    evaluated, a written/read column is MISSING from a shipped DDL  -> block
#   2  REFUSE  could NOT evaluate (unmapped shipped DDL / no git / parse produced nothing / empty scan)
#   3  REFUSE  --self-test failed (the gate itself is broken or vacuous)
#   *  REFUSE  unknown == could-not-evaluate
#
# There is deliberately NO suppression cap. f5-sweep's F5_MAX_HITS_PER_TOKEN prints "clean" and
# exits 0 when every token exceeds the cap (observed: 180 tokens suppressed, verdict "clean"). A gate
# that mutes itself into a PASS is the bug this design exists to remove; do not add one here.
#
# TESTABILITY SEAM (the independent author!=impl lock binds THIS surface):
#   SHIPPED_DDL_DOC_ROOTS   where shipped CREATE TABLE lives   (default: "docs-site samples")
#   SHIPPED_DDL_SRC_ROOTS   bounds the find-fallback for src   (default: "src")
#   SHIPPED_DDL_MAP_FILE    optional file of MAP rows (same pipe format); REPLACES the built-in MAP,
#                           so a lock can point the whole sweep at a temp tree of planted fixtures
#                           WITHOUT touching the real repo (mirrors gate-wiring.sh's GATE_WIRING_* seam).
#   SHIPPED_DDL_MIN_COLS    non-vacuity floor (default: 3)

set -uo pipefail

# REPO_ROOT overridable (SHIPPED_DDL_REPO_ROOT) so a hermetic fixture run — Tests' lock and this
# gate's own .test.sh — can SKIP the git rev-parse. Under heavy AV, git subprocess spawns dominate,
# and a gate a commit/launch waits on must stay fast (an unaffordable gate is an unrun gate — this
# sprint's own inert-gate defect). Unset => production behavior unchanged (SA guard 1).
REPO_ROOT="${SHIPPED_DDL_REPO_ROOT:-$(git rev-parse --show-toplevel 2>/dev/null || echo .)}"
cd "$REPO_ROOT" || exit 2

E_PASS=0; E_FAIL=1; E_REFUSE=2; E_SELFTEST=3

# Reserved src-glob token marking a table as DECLARED not-framework-owned. Per-table only:
# there is deliberately no path/glob form, so a blanket directory exemption is inexpressible.
NOT_APPLICABLE='NOT-APPLICABLE'

# ── Overridable scan roots + map (the testability seam) ─────────────────────────────────────────
SHIPPED_DDL_DOC_ROOTS="${SHIPPED_DDL_DOC_ROOTS:-docs-site samples}"
SHIPPED_DDL_SRC_ROOTS="${SHIPPED_DDL_SRC_ROOTS:-src}"
SHIPPED_DDL_MAP_FILE="${SHIPPED_DDL_MAP_FILE:-}"

# ── MAP: shipped-DDL table  ->  the src writes that target THAT table ──────────────────────────
# Format: <table-regex-in-CREATE-TABLE>|<src glob>|<table-var regex>|<label>
#
# The <table-var> field is load-bearing and was learned the hard way. A provider tree writes
# SEVERAL tables (Excalibur.Outbox.Postgres writes outboxTableName, deadLetterTableName AND
# fenceTableName). Globbing the tree and unioning every `col = @param` in it compares the DLQ's
# columns against the outbox's DDL and reports 7 phantom missing columns. Scope extraction to the
# statement whose interpolated table var matches, or the gate cries wolf and gets ignored --
# which is worse than no gate.
#
# A shipped CREATE TABLE matching NO row here is a REFUSE. Add rows deliberately.
# Rows 3-19 were added alongside delimited-identifier support: until the extractor could see
# bracketed/quoted identifiers, every bracketed T-SQL table below was INVISIBLE here, so their
# absence looked like coverage.
#
# GLOBS ARE FILENAME-SCOPED, NOT DIRECTORY-WIDE, WHERE ONE DIRECTORY WRITES TWO TABLES.
# `EventSourcing.SqlServer/Requests/` writes BOTH Events and Snapshots through the same
# interpolated var (`qualifiedTable`); a `Requests/**` glob would union both column sets and
# manufacture drift on every run -- the cry-wolf failure this header warns about. Note
# IsErasedRequest.cs and GetCurrentVersionRequest.cs target Events but do not contain
# "Event" in the filename, so they are listed explicitly (the glob field is word-split).
MAP_ROWS=(
    # The docs spell these schema-qualified (public.outbox); the write path and the older docs
    # spell them bare. Matching is anchored (^re$), so an unqualified pattern silently stops
    # matching the moment a doc gains its schema prefix -- which is how these three entered the
    # REFUSE set as 'NEW gaps' without any schema actually changing. Accept either spelling.
    '(public[.])?outbox|src/Excalibur/Excalibur.Outbox.Postgres/**|outboxTableName|postgres outbox'
    'dbo[.]OutboxMessages|src/Excalibur/Excalibur.Outbox.SqlServer/Requests/*Outbox*.cs|tableName|sqlserver outbox'
    # The fence is a SEPARATE control table from OutboxMessages, written through its OWN variable
    # (fenceTableName) by exactly ONE file. Scoped to that file deliberately: a `Requests/*Outbox*.cs`
    # glob would match EnforceOutboxFenceRequest AND the OutboxMessages writers, union both column
    # sets, and manufacture drift on every run — the cry-wolf failure this header warns about.
    # Its shipped DDL arrived with the sample-README fix; before this row it matched NO map row, which
    # the gate correctly reported as a NEW REFUSE (a coverage gap on freshly-shipped consumer DDL).
    'dbo[.]OutboxFence|src/Excalibur/Excalibur.Outbox.SqlServer/Requests/EnforceOutboxFenceRequest.cs|fenceTableName|sqlserver outbox fence'

    # ── event store ──────────────────────────────────────────────────────────────────────
    # The docs spell the SQL Server Events table under three schema names ([EventSourcing],
    # [events], [dbo]); they are ALIASES of one table with one write path, not three tables.
    'EventSourcing[.]Events|src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/*Event*.cs src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/IsErasedRequest.cs src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/GetCurrentVersionRequest.cs|qualifiedTable|sqlserver event store'
    'events[.]Events|src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/*Event*.cs src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/IsErasedRequest.cs src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/GetCurrentVersionRequest.cs|qualifiedTable|sqlserver event store (events schema alias)'
    'dbo[.]Events|src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/*Event*.cs src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/IsErasedRequest.cs src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/GetCurrentVersionRequest.cs|qualifiedTable|sqlserver event store (dbo schema alias)'
    # Samples configure the SQL Server event store's table name to [dbo].[EventStoreEvents] (via options);
    # same single write path (qualifiedTable), same columns. Mapping it verifies every shipped sample DDL
    # for that table declares the tenant column the keyed store now writes.
    'dbo[.]EventStoreEvents|src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/*Event*.cs src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/IsErasedRequest.cs src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/GetCurrentVersionRequest.cs|qualifiedTable|sqlserver event store (sample custom table name)'
    'events|src/Excalibur/Excalibur.EventSourcing.Postgres/Requests/*Event*.cs src/Excalibur/Excalibur.EventSourcing.Postgres/Requests/IsErasedRequest.cs src/Excalibur/Excalibur.EventSourcing.Postgres/Requests/GetCurrentVersionRequest.cs|qualifiedTable|postgres event store'
    # The published DDL previously created [EventSourcing].[Snapshots] and [snapshots].[Snapshots],
    # neither of which any store reads: the SQL Server snapshot store defaults to schema "dbo",
    # table "EventStoreSnapshots". The published blocks now name the table the code actually uses,
    # so the map follows them. Restoring either old alias would re-map a table nothing reads.
    'dbo[.]EventStoreSnapshots|src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/*Snapshot*.cs|qualifiedTable|sqlserver snapshot store'
    'public[.]event_store_snapshots|src/Excalibur/Excalibur.EventSourcing.Postgres/Requests/*Snapshot*.cs|qualifiedTable|postgres snapshot store'
    'event_store_snapshots|src/Excalibur/Excalibur.EventSourcing.Postgres/Requests/*Snapshot*.cs|qualifiedTable|postgres snapshot store (unqualified)'
    'EVENTSTORESNAPSHOTS|src/Excalibur/Excalibur.EventSourcing.Oracle/Requests/*Snapshot*.cs|qualifiedTable|oracle snapshot store'

    # ── outbox / inbox / dead-letter ──────────────────────────────────────────────────────
    'dbo[.]inbox_messages|src/Excalibur/Excalibur.Inbox.SqlServer/SqlServerInboxStore.cs|_options[.]QualifiedTableName|sqlserver inbox'
    'dbo[.]DeadLetterQueue|src/Excalibur/Excalibur.Outbox.SqlServer/SqlServerDeadLetterQueue.cs|_options[.]QualifiedTableName|sqlserver dead-letter queue'
    'dbo[.]OutboxMessageTransports|src/Excalibur/Excalibur.Outbox.SqlServer/Requests/*Transport*.cs|tableName|sqlserver outbox transports'
    '(public[.])?outbox_dead_letters|src/Excalibur/Excalibur.Outbox.Postgres/**|deadLetterTableName|postgres dead-letter'
    '(public[.])?outbox_fence|src/Excalibur/Excalibur.Outbox.Postgres/**|fenceTableName|postgres outbox fence'

    # ── audit / compliance / data-access / cdc ────────────────────────────────────────────
    'audit[.]AuditEvents|src/Excalibur/Excalibur.AuditLogging.SqlServer/SqlServerAuditStore.cs|_options[.]FullyQualifiedTableName|sqlserver audit events'
    'audit[.]AuditAnnotations|src/Excalibur/Excalibur.AuditLogging.SqlServer/SqlServerAuditAnnotationStore.cs|_options[.]FullyQualifiedTableName|sqlserver audit annotations'
    'compliance[.]ErasureRequests|src/Excalibur/Excalibur.Compliance.SqlServer/Erasure/SqlServerErasureStore.cs|_options[.]FullRequestsTableName|sqlserver gdpr erasure requests'
    'dbo[.]IdentityMap|src/Excalibur/Excalibur.Data.IdentityMap.SqlServer/SqlServerIdentityMapStore.cs|_options[.]QualifiedTableName|sqlserver identity map'
    'Cdc[.]CdcProcessedEvents|src/Excalibur/Excalibur.Cdc.SqlServer/SqlServerCdcIdempotencyFilter.cs|_options[.]QualifiedTableName|sqlserver cdc idempotency'
    'Cdc[.]CdcProcessingState|src/Excalibur/Excalibur.Cdc.SqlServer/CdcStateStore.cs|_options[.]QualifiedTableName|sqlserver cdc state store'
    'DataProcessor[.]DataTaskRequests|src/Excalibur/Excalibur.Data.DataProcessing/Requests/*.cs|configuration[.]QualifiedTableName|data-processing task requests'

    # ── DECLARED not-framework-owned (see NOT_APPLICABLE above) ───────────────────────────
    # A CDC sample's legacy SOURCE database: the tables the demo reads FROM to show change
    # capture. The framework never writes them, so there is no write path to diff against.
    'dbo[.]LegacyCustomers|NOT-APPLICABLE|-|CDC sample legacy source DB; framework never writes it'
    'dbo[.]LegacyOrders|NOT-APPLICABLE|-|CDC sample legacy source DB; framework never writes it'
    'dbo[.]LegacyOrderItems|NOT-APPLICABLE|-|CDC sample legacy source DB; framework never writes it'
)

# If a fixture/override MAP file is supplied, it REPLACES the built-in rows. Blank/`#` lines ignored.
# This is what lets Tests' lock (and any future fixture) drive the whole sweep over a temp tree.
load_map() {
    [ -n "$SHIPPED_DDL_MAP_FILE" ] || return 0
    [ -f "$SHIPPED_DDL_MAP_FILE" ] || { echo "shipped-ddl-sweep: REFUSE — SHIPPED_DDL_MAP_FILE not found: $SHIPPED_DDL_MAP_FILE" >&2; return $E_REFUSE; }
    MAP_ROWS=()
    local line
    while IFS= read -r line; do
        line="${line%$'\r'}"                                   # CRLF-harden
        case "$line" in ''|'#'*|' '*'#'*) continue ;; esac
        [ -n "${line//[[:space:]]/}" ] || continue
        MAP_ROWS+=("$line")
    done < "$SHIPPED_DDL_MAP_FILE"
    [ "${#MAP_ROWS[@]}" -gt 0 ] || { echo "shipped-ddl-sweep: REFUSE — MAP file produced zero rows: $SHIPPED_DDL_MAP_FILE" >&2; return $E_REFUSE; }
}

# Non-vacuity floor. A table whose write-path parse yields fewer than this many columns is a
# REFUSE, not a PASS: the shipped outbox tables carry 20+ columns, so a 1-column extraction means
# the PARSER missed the statement, not that the code writes one column. Without this floor the
# gate reports "✓ ok — 1 written cols all declared" on a 24-column table and is worse than absent,
# because it manufactures the belief that the schema was checked. (Same class: f5-sweep prints "clean"
# and exits 0 when its suppression cap swallows every token; we answered the same class with
# -MinExpectedAssemblies 40.) An empty enumeration is not a clean result.
MIN_WRITTEN_COLS="${SHIPPED_DDL_MIN_COLS:-3}"

# ── list files under a set of globs/dirs: git-tracked for the real run, find-fallback for an ─────
# untracked temp fixture tree (git ls-files can't see planted fixtures). One helper, both modes,
# so the exact same code path the real sweep uses is the one Tests' lock exercises.
_list_files() {
    local pat got base
    for pat in $1; do
        base="${pat%%\*\*}"; base="${base%/}"
        [ -n "$base" ] || base="$pat"
        case "$pat" in
            /*|[A-Za-z]:[\\/]*)
                # Absolute path (a temp fixture tree). git ls-files here is a wasted, untracked
                # subprocess — and under AV real-time scan it is the end-to-end self-test/lock
                # bottleneck. Go straight to find so a hermetic lock runs fast.
                if [ -d "$base" ]; then find "$base" -type f 2>/dev/null
                elif [ -f "$pat" ]; then printf '%s\n' "$pat"; fi
                ;;
            *)
                got="$(git ls-files -- "$pat" 2>/dev/null)"
                if [ -n "$got" ]; then printf '%s\n' "$got"
                elif [ -d "$base" ]; then find "$base" -type f 2>/dev/null
                elif [ -f "$pat" ]; then printf '%s\n' "$pat"; fi
                ;;
        esac
    done
}

# ── parse: column names declared inside a CREATE TABLE block ───────────────────────────────────
# Reads a file, isolates the CREATE TABLE <tbl> ( ... ) body, emits one column identifier per line.
# Conservative: only lines whose FIRST token is a bare identifier followed by a type word.
ddl_columns() {
    local file="$1" tbl_re="$2"
    awk -v re="$tbl_re" '
        # Block-start detection must survive DELIMITED identifiers for the same reason the
        # table-name extractor must: `CREATE TABLE [EventSourcing].[Events]` never matches a
        # pattern written as `eventsourcing.events` while the brackets are still in the line,
        # and the mixed case defeats a case-sensitive compare. Normalise a copy of the line
        # (lowercase, delimiters stripped) and match the MAP pattern against that. Without
        # this the table IS found but parses to zero columns -> REFUSE, which is the same
        # blind spot one layer down.
        BEGIN { re = tolower(re) }
        { hdr = tolower($0); gsub(/[]["`]/, "", hdr) }
        hdr ~ /create[ \t]+table/ && hdr ~ re { inblk=1; next }
        inblk && /^[ \t]*\)/            { inblk=0 }
        inblk {
            line=$0
            sub(/--.*$/, "", line)                       # strip trailing comment
            gsub(/^[ \t]+|[ \t]+$/, "", line)
            if (line == "") next
            if (tolower(line) ~ /^(primary|foreign|unique|constraint|check|index|key)\b/) next
            n=split(line, f, /[ \t]+/)
            if (n < 2) next
            col=f[1]
            gsub(/[",;`\[\]]/, "", col)
            if (col ~ /^[A-Za-z_][A-Za-z0-9_]*$/) print tolower(col)
        }
    ' "$file" | sort -u
}

# ── parse: column names WRITTEN/READ by src SQL, SCOPED to one table ───────────────────────────
# Enters a statement at UPDATE/INSERT INTO/DELETE FROM/MERGE INTO/FROM {var} where <var> matches
# tvar, and leaves at the raw-string SQL block terminator. Inside, collects columns from FOUR
# clauses: `col = @Param|:Param` (SET + WHERE), the INSERT paren-list, and
# OUTPUT inserted./deleted.col. Unscoped extraction unions sibling tables' columns and manufactures
# false positives -- see the MAP note above, which is why entry is gated on the interpolated tvar.
src_written_columns() {
    local glob="$1" tvar="$2" blocktext joined
    blocktext="$(_list_files "$glob" | grep -E '\.cs$' | while IFS= read -r f; do
        [ -f "$f" ] || continue
        awk -v tv="$tvar" '
            {
                if ($0 ~ /(UPDATE|INSERT[ \t]+INTO|DELETE[ \t]+FROM|MERGE[ \t]+INTO|FROM)[ \t]*\{/ \
                    && $0 ~ ("\\{" tv "\\}")) inblk=1
                if (inblk) print
                if (inblk && $0 ~ /"""/) inblk=0
            }
        ' "$f" 2>/dev/null || true
    done)"

    joined="$(printf '%s' "$blocktext" | tr '\n' ' ')"

    {
        # SET / WHERE param form:  col = @Param | :Param
        printf '%s\n' "$blocktext" \
            | grep -oE '[A-Za-z_][A-Za-z0-9_]*[[:space:]]*=[[:space:]]*[@:][A-Za-z_]' \
            | sed -E 's/[[:space:]]*=.*$//'

        # INSERT paren-list: INSERT INTO {tvar} ( a, b, c )  -- may span lines (joined)
        printf '%s' "$joined" \
            | grep -oiE "INSERT[[:space:]]+INTO[[:space:]]*\{$tvar\}[[:space:]]*\([^)]*\)" \
            | sed -E 's/^[^(]*\(//; s/\).*$//' \
            | tr ',' '\n' \
            | sed -E 's/[[:space:]]//g; s/\{[^}]*\}//g; s/[]"\x60[]//g' \
            | grep -E '^[A-Za-z_][A-Za-z0-9_]*$'

        # MERGE INSERT clause: `INSERT ( a, b, c )` with NO `INTO {tvar}` -- in a MERGE the target is
        # already named by `MERGE INTO {tvar}`, so the WHEN-NOT-MATCHED INSERT drops the table name.
        # The enclosing block is already scoped to the mapped table (entry is gated on `{tvar}`), so any
        # bare INSERT column-list inside it belongs to that table. Without this, an upsert written as a
        # MERGE (the atomic insert-or-update idiom) contributes ZERO of its columns and the write-path
        # parse falls under the non-vacuity floor -> a spurious REFUSE on a table the code writes fully.
        # The `{...}` strip keeps an interpolated-suffix column (e.g. `Metadata{tenantInsertColumn}`)
        # as its literal name; a fully-interpolated column has no static name and is out of reach.
        printf '%s' "$joined" \
            | grep -oiE "INSERT[[:space:]]*\([^)]*\)" \
            | sed -E 's/^[^(]*\(//; s/\).*$//' \
            | tr ',' '\n' \
            | sed -E 's/[[:space:]]//g; s/\{[^}]*\}//g; s/[]"\x60[]//g' \
            | grep -E '^[A-Za-z_][A-Za-z0-9_]*$'

        # OUTPUT inserted.col / deleted.col
        printf '%s\n' "$blocktext" \
            | grep -oiE '(inserted|deleted)\.[A-Za-z_][A-Za-z0-9_]*' \
            | sed -E 's/^[^.]*\.//'
    } 2>/dev/null | tr 'A-Z' 'a-z' | sort -u
}

sweep() {
    load_map || return $E_REFUSE

    local rc=$E_PASS refused=0 checked=0 na_count=0
    local ddl_files
    # Enumerate once (fast: git-ls-files for tracked roots, find for absolute fixture roots — see
    # _list_files), then run a SINGLE batched grep over the whole file set instead of spawning one
    # grep per file. The per-file loop cost O(files) grep processes, each AV-real-time-scanned, which
    # made this sweep 11min+ in pre-commit over docs-site+samples. Batching via `xargs -0`
    # collapses that to ~one grep process — the same "one fast process" spirit as self-test ARM7 —
    # while preserving the EXACT file set (so the absolute-temp-root env-override self-test arms, which
    # `git grep` cannot see, stay green). `-r` (--no-run-if-empty) keeps an empty enumeration → REFUSE.
    ddl_files="$(_list_files "$SHIPPED_DDL_DOC_ROOTS" | sort -u \
                 | tr '\n' '\0' \
                 | xargs -0 -r grep -ilE 'create[ \t]+table' 2>/dev/null || true)"

    if [ -z "$ddl_files" ]; then
        echo "shipped-ddl-sweep: REFUSE — found no shipped CREATE TABLE under {$SHIPPED_DDL_DOC_ROOTS}."
        echo "  A repo that ships DDL and greps zero of it is a broken query, not a clean result."
        return $E_REFUSE
    fi

    echo "=== shipped-DDL sweep: does our published schema declare what our code writes/reads? ==="
    echo ""

    local f row tbl_re glob label matched
    while IFS= read -r f; do
        [ -n "$f" ] || continue
        local tables
        # Table-name extraction must accept DELIMITED identifiers, not just bare ones:
        #   T-SQL       CREATE TABLE [EventSourcing].[Events]
        #   Postgres    CREATE TABLE "public"."snapshots"
        # A bare-identifier class ([A-Za-z_]...) fails at the leading delimiter, and the failure
        # is not a clean miss -- on `CREATE TABLE IF NOT EXISTS [dbo].[X]` the optional
        # IF-NOT-EXISTS group stops participating and the literal `IF` is captured as the table
        # name, so the gate reports a phantom table and never checks the real one.
        #
        # Accept `[`, `]` and `"` in the identifier, then strip them so the extracted name
        # normalises to the same `schema.Table` shape MAP_ROWS matches against. (In a POSIX
        # bracket expression a literal `]` must come first.)
        tables="$(grep -oiE 'create[ \t]+table[ \t]+(if[ \t]+not[ \t]+exists[ \t]+)?[]["A-Za-z_][]["A-Za-z0-9_.]*' "$f" \
                  | awk '{print $NF}' | tr -d '[]"' | sort -u)"
        while IFS= read -r t; do
            [ -n "$t" ] || continue
            matched=0
            for row in "${MAP_ROWS[@]}"; do
                local rest tvar
                tbl_re="${row%%|*}";  rest="${row#*|}"
                glob="${rest%%|*}";   rest="${rest#*|}"
                tvar="${rest%%|*}";   label="${rest#*|}"
                if printf '%s' "$t" | grep -qiE "^${tbl_re}$"; then
                    matched=1
                    # ── The fourth state: NOT-APPLICABLE (a DECLARED non-promise) ─────────────
                    # Not every shipped CREATE TABLE is a promise about framework-owned schema.
                    # A sample's own fixture (e.g. a CDC demo's legacy source database) has no
                    # framework write path, so there is nothing to diff against -- mapping it
                    # would require a glob that matches nothing, which the floor below would
                    # then REFUSE on forever. A gate parked permanently at REFUSE gets switched
                    # off, which is the cry-wolf failure this file warns about at the top.
                    #
                    # The honesty property is preserved by CONSTRUCTION, not by trust:
                    #   * DECLARED, never inferred -- a human writes one row per table;
                    #   * PER-TABLE only -- there is deliberately no path/glob/directory form,
                    #     so "exempt docs-site" cannot be expressed and the gate's original
                    #     purpose cannot be deleted wholesale;
                    #   * a FORGOTTEN table still REFUSEs -- the default is unchanged;
                    #   * a reason is MANDATORY -- an exemption must be argued in writing;
                    #   * every declaration is PRINTED on every run -- exemptions can never
                    #     accumulate silently the way a suppression list does.
                    if [ "$glob" = "$NOT_APPLICABLE" ]; then
                        if [ -z "$label" ]; then
                            echo "  REFUSE  $t — declared $NOT_APPLICABLE with no reason."
                            echo "          An exemption without a stated reason is a suppression."
                            refused=1
                            continue
                        fi
                        na_count=$((na_count + 1))
                        echo "  — n/a   $t — not framework-owned: $label"
                        continue
                    fi
                    checked=$((checked + 1))
                    local dcols scols missing nd ns
                    dcols="$(ddl_columns "$f" "$tbl_re")"
                    scols="$(src_written_columns "$glob" "$tvar")"
                    nd="$(printf '%s' "$dcols" | grep -c . || true)"
                    ns="$(printf '%s' "$scols" | grep -c . || true)"
                    # Non-vacuity floor BEFORE any verdict: too few parsed columns means the parser
                    # missed the statement. Refusing here is what stops a parse failure from
                    # masquerading as a clean schema.
                    if [ "$nd" -eq 0 ] || [ "$ns" -lt "$MIN_WRITTEN_COLS" ]; then
                        echo "  REFUSE  $t ($label)"
                        echo "          ddl-cols=$nd src-written-cols=$ns (floor=$MIN_WRITTEN_COLS) — cannot evaluate."
                        echo "          A parse this thin means the extractor missed the write statement;"
                        echo "          reporting 'ok' here would certify a schema nobody checked."
                        refused=1
                        continue
                    fi
                    missing="$(comm -23 <(printf '%s\n' "$scols") <(printf '%s\n' "$dcols"))"
                    if [ -n "$missing" ]; then
                        echo "  ✗ FAIL  $t ($label) — $f"
                        echo "          the shipped DDL OMITS columns the code writes/reads:"
                        # One line PER missing column. `$missing` is newline-separated and must
                        # not be left unquoted here: word-splitting feeds every column as a
                        # separate positional arg, printf recycles the format across them, and
                        # the result pairs unrelated column names and prints globs where columns
                        # belong -- a drift report that misnames the drift. (Latent until
                        # delimited-identifier support made these tables visible; nothing
                        # reached this branch before.)
                        while IFS= read -r missing_col; do
                            [ -n "$missing_col" ] || continue
                            printf '            DRIFT %s.%s — named by src (%s), absent from shipped DDL %s\n' \
                                "$t" "$missing_col" "$glob" "$f"
                        done <<< "$missing"
                        echo "          a consumer following this DDL hits a missing-column error on first use."
                        rc=$E_FAIL
                    else
                        echo "  ✓ ok    $t ($label) — $(printf '%s' "$scols" | grep -c .) written/read cols all declared"
                    fi
                fi
            done
            if [ "$matched" -eq 0 ]; then
                echo "  REFUSE  $t — shipped DDL in $f has NO MAP entry."
                echo "          Not a pass. Add a MAP_ROWS entry naming the src tree that writes it."
                refused=1
            fi
        done <<< "$tables"
    done <<< "$ddl_files"

    echo ""
    echo "checked=$checked not-applicable=$na_count"
    if [ "$rc" -eq $E_FAIL ]; then
        echo "shipped-ddl-sweep: FAIL — published schema does not match shipped code."
        return $E_FAIL
    fi
    if [ "$refused" -eq 1 ]; then
        echo "shipped-ddl-sweep: REFUSE — at least one shipped DDL could not be evaluated (see above)."
        return $E_REFUSE
    fi
    # Non-vacuity: the run must have EVALUATED at least one real table. Declaring every
    # shipped table not-applicable would otherwise buy a green verdict having compared
    # nothing -- the exemption channel turned into a suppression list. `checked` counts
    # only genuinely evaluated tables, so n/a declarations can never satisfy this floor.
    if [ "$checked" -eq 0 ]; then
        if [ "$na_count" -gt 0 ]; then
            echo "shipped-ddl-sweep: REFUSE — every shipped table was declared $NOT_APPLICABLE ($na_count)."
            echo "  Zero tables were actually evaluated. A gate that compared nothing has not passed."
            return $E_REFUSE
        fi
        echo "shipped-ddl-sweep: REFUSE — enumerated zero tables. An empty enumeration is not a clean result."
        return $E_REFUSE
    fi
    echo "shipped-ddl-sweep: PASS — every mapped shipped DDL declares every column its code writes/reads."
    return $E_PASS
}

# ── self-test: NON-VACUOUS, every arm per testing-patterns §3 (SAFETY + LIVENESS + REFUSE) ──────
self_test() {
    local tmp bad=0
    tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' RETURN

    # ARM 1 (SAFETY, UPDATE SET): a planted missing column must be caught by the extractor.
    cat > "$tmp/doc.md" <<'EOF'
```sql
CREATE TABLE outbox_messages (
    message_id   TEXT NOT NULL,
    attempts     INT  NOT NULL
);
```
EOF
    mkdir -p "$tmp/src1"
    cat > "$tmp/src1/w.cs" <<'EOF'
var sql = $"""
   UPDATE {t} SET attempts = @RetryCount, error_message = @ErrorMessage
   WHERE message_id = @MessageId
   """;
EOF
    local dc sc miss
    dc="$(ddl_columns "$tmp/doc.md" 'outbox_messages')"
    sc="$(src_written_columns "$tmp/src1/**" 't')"
    miss="$(comm -23 <(printf '%s\n' "$sc") <(printf '%s\n' "$dc"))"
    if ! printf '%s\n' "$miss" | grep -qx 'error_message'; then
        echo "self-test ARM1 FAIL: planted missing UPDATE-SET column 'error_message' NOT detected." >&2
        echo "  ddl=[$(echo "$dc")] src=[$(echo "$sc")] missing=[$(echo "$miss")]" >&2
        bad=1
    else
        echo "  ok  ARM1 safety   — planted missing UPDATE-SET/WHERE column detected"
    fi
    # WHERE-clause coverage: message_id (read in WHERE) must appear in the src column set.
    if ! printf '%s\n' "$sc" | grep -qx 'message_id'; then
        echo "self-test ARM1b FAIL: WHERE-clause column 'message_id' was not extracted." >&2; bad=1
    else
        echo "  ok  ARM1b WHERE   — WHERE-clause column extracted"
    fi

    # ARM 2 (LIVENESS): a genuinely MATCHING pair must NOT be flagged.
    cat > "$tmp/good.md" <<'EOF'
```sql
CREATE TABLE outbox_messages (
    message_id    TEXT NOT NULL,
    attempts      INT  NOT NULL,
    error_message TEXT NULL
);
```
EOF
    dc="$(ddl_columns "$tmp/good.md" 'outbox_messages')"
    miss="$(comm -23 <(printf '%s\n' "$sc") <(printf '%s\n' "$dc"))"
    if [ -n "$miss" ]; then
        echo "self-test ARM2 FAIL: a MATCHING ddl/code pair was flagged (false positive): [$(echo "$miss")]" >&2
        bad=1
    else
        echo "  ok  ARM2 liveness — matching ddl/code pair NOT flagged (no false positive)"
    fi

    # ARM 3 (REFUSE is not PASS): an unmapped table must not be reported clean.
    printf 'CREATE TABLE totally_unmapped_t (\n  a INT\n);\n' > "$tmp/u.md"
    local t_out hit=0
    t_out="$(grep -oiE 'create[ \t]+table[ \t]+[A-Za-z_][A-Za-z0-9_.]*' "$tmp/u.md" | awk '{print $NF}')"
    for row in "${MAP_ROWS[@]}"; do
        printf '%s' "$t_out" | grep -qiE "^${row%%|*}$" && hit=1
    done
    if [ "$hit" -eq 1 ]; then
        echo "self-test ARM3 FAIL: an unmapped table matched the MAP — the refuse path is unreachable." >&2
        bad=1
    else
        echo "  ok  ARM3 refuse   — unmapped shipped DDL takes the REFUSE path, not PASS"
    fi

    # ARM 4 (INSERT paren-list): a column present ONLY in an INSERT column list must be
    # extracted — the seed's stated coverage gap, now closed.
    mkdir -p "$tmp/src4"
    cat > "$tmp/src4/i.cs" <<'EOF'
var sql = $"""
   INSERT INTO {t} (message_id, attempts, dispatched_at)
   VALUES (@MessageId, @Attempts, @DispatchedAt)
   """;
EOF
    local ic
    ic="$(src_written_columns "$tmp/src4/**" 't')"
    if ! printf '%s\n' "$ic" | grep -qx 'dispatched_at'; then
        echo "self-test ARM4 FAIL: INSERT paren-list column 'dispatched_at' NOT extracted." >&2
        echo "  src-cols=[$(echo "$ic")]" >&2
        bad=1
    else
        echo "  ok  ARM4 insert   — INSERT paren-list column extracted"
    fi

    # ARM 5 (OUTPUT clause): an OUTPUT inserted.col reference must be extracted.
    mkdir -p "$tmp/src5"
    cat > "$tmp/src5/o.cs" <<'EOF'
var sql = $"""
   UPDATE {t} SET leased_by = @LeasedBy
   OUTPUT inserted.fencing_token, inserted.leased_at
   WHERE id = @Id
   """;
EOF
    local oc
    oc="$(src_written_columns "$tmp/src5/**" 't')"
    if ! printf '%s\n' "$oc" | grep -qx 'fencing_token'; then
        echo "self-test ARM5 FAIL: OUTPUT-clause column 'fencing_token' NOT extracted." >&2
        echo "  src-cols=[$(echo "$oc")]" >&2
        bad=1
    else
        echo "  ok  ARM5 output   — OUTPUT-clause column extracted"
    fi

    # ARM 6 (END-TO-END env-root sweep): prove the WHOLE sweep() runs against overridable fixture
    # roots + a fixture MAP — the seam Tests' independent lock depends on. RED on omission (exit 1),
    # GREEN on match (exit 0), REFUSE on empty scan (exit 2). If this arm is vacuous, Tests' lock is.
    local e2e="$tmp/e2e"; mkdir -p "$e2e/docs" "$e2e/src"
    cat > "$e2e/docs/schema.md" <<'EOF'
```sql
CREATE TABLE outbox_messages (
    message_id   TEXT NOT NULL,
    attempts     INT  NOT NULL
);
```
EOF
    cat > "$e2e/src/w.cs" <<'EOF'
var sql = $"""
   UPDATE {t} SET attempts = @Attempts, error_message = @ErrorMessage
   WHERE message_id = @MessageId
   """;
EOF
    printf 'outbox_messages|%s/**|t|fixture outbox\n' "$e2e/src" > "$e2e/map"

    local rc_drift rc_ok rc_empty
    ( SHIPPED_DDL_DOC_ROOTS="$e2e/docs" SHIPPED_DDL_SRC_ROOTS="$e2e/src" \
      SHIPPED_DDL_MAP_FILE="$e2e/map" SHIPPED_DDL_MIN_COLS=1 sweep ) >/dev/null 2>&1
    rc_drift=$?

    # matching DDL -> PASS
    cat > "$e2e/docs/schema.md" <<'EOF'
```sql
CREATE TABLE outbox_messages (
    message_id    TEXT NOT NULL,
    attempts      INT  NOT NULL,
    error_message TEXT NULL
);
```
EOF
    ( SHIPPED_DDL_DOC_ROOTS="$e2e/docs" SHIPPED_DDL_SRC_ROOTS="$e2e/src" \
      SHIPPED_DDL_MAP_FILE="$e2e/map" SHIPPED_DDL_MIN_COLS=1 sweep ) >/dev/null 2>&1
    rc_ok=$?

    # empty doc root -> REFUSE (not pass)
    local empt="$tmp/empty"; mkdir -p "$empt"
    ( SHIPPED_DDL_DOC_ROOTS="$empt" SHIPPED_DDL_SRC_ROOTS="$e2e/src" \
      SHIPPED_DDL_MAP_FILE="$e2e/map" SHIPPED_DDL_MIN_COLS=1 sweep ) >/dev/null 2>&1
    rc_empty=$?

    if [ "$rc_drift" -ne "$E_FAIL" ]; then
        echo "self-test ARM6 FAIL: end-to-end sweep did not FAIL(1) on a planted omission (got rc=$rc_drift)." >&2; bad=1
    elif [ "$rc_ok" -ne "$E_PASS" ]; then
        echo "self-test ARM6 FAIL: end-to-end sweep did not PASS(0) on a matching pair (got rc=$rc_ok)." >&2; bad=1
    elif [ "$rc_empty" -ne "$E_REFUSE" ]; then
        echo "self-test ARM6 FAIL: end-to-end sweep did not REFUSE(2) on an empty scan (got rc=$rc_empty)." >&2; bad=1
    else
        echo "  ok  ARM6 e2e      — env-root sweep: omission=FAIL(1) match=PASS(0) empty=REFUSE(2)"
    fi

    # ARM 7 (PRODUCTION-PATH non-vacuity — SA guard 3, the load-bearing one): with NO env override,
    # the gate must still enumerate the REAL shipped surface and find DDL the built-in MAP covers.
    # Without this arm the test seam becomes a way to make the gate green while it does nothing
    # against reality (the f5-sweep MAX_HITS=0 / session-collision-guard-orphan vacuity class). We
    # assert ENUMERATION is non-vacuous (finds real shipped DDL incl. a mapped outbox table) — NOT
    # that the real sweep exits 0, because real drift must fail the GATE, not this self-test.
    # git grep (one process) not ls-files+xargs — fast even under AV real-time scan, so this arm
    # (and the .test.sh mutant runs that invoke --self-test) stay quick in pre-commit.
    local prod_ddl prod_outbox
    prod_ddl="$(git grep -liE 'create[ \t]+table' -- docs-site samples 2>/dev/null | head -1 || true)"
    if [ -z "$prod_ddl" ]; then
        echo "self-test ARM7 FAIL: production-path enumeration (no env set) found ZERO real shipped CREATE TABLE — the seam is a fixture-only no-op against reality (SA guard 3)." >&2
        bad=1
    else
        prod_outbox="$(git grep -liE 'create[ \t]+table[^;]*outbox' -- docs-site samples 2>/dev/null | head -1 || true)"
        if [ -z "$prod_outbox" ]; then
            echo "self-test ARM7 FAIL: real enumeration found shipped DDL but none matching the built-in MAP (outbox) — cannot prove the mapped path fires against reality (SA guard 3)." >&2
            bad=1
        else
            echo "  ok  ARM7 prod-path — real docs-site/samples enumeration (no env) finds mapped shipped DDL"
        fi
    fi

    if [ "$bad" -ne 0 ]; then
        echo "shipped-ddl-sweep --self-test: FAILED (the gate is broken or vacuous)" >&2
        return $E_SELFTEST
    fi
    echo "shipped-ddl-sweep --self-test: all arms pass (safety + where + liveness + refuse + insert + output + e2e + prod-path)"
    return 0
}

case "${1:---sweep}" in
    --self-test) self_test; exit $? ;;
    --sweep)     sweep;     exit $? ;;
    -h|--help)   echo "usage: shipped-ddl-sweep.sh [--sweep|--self-test]"; exit 0 ;;
    *)           echo "shipped-ddl-sweep: unknown arg '$1'" >&2; exit $E_REFUSE ;;
esac
