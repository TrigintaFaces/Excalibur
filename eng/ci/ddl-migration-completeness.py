#!/usr/bin/env python3
"""ddl-migration-completeness — an in-place edit to provisioning DDL must ship an upgrade path.

WHAT THIS EXISTS TO PREVENT
---------------------------
We ship the DDL a consumer provisions from. When a column is later added to that DDL by editing
the CREATE TABLE in place, a consumer who provisions FRESH is fine and a consumer who ALREADY RAN
the older version is broken -- and the break is silent in the worst possible way:

    CREATE TABLE IF NOT EXISTS / a sys.tables guard  ->  the table exists, so creation is SKIPPED
    the schema check probes table EXISTENCE only     ->  it PASSES on the legacy table
    every statement now names the new column         ->  the first call dies: invalid column name

The consumer gets a dead store AND a health check that told them it was fine. That is strictly
worse than shipping nothing, because the check converts a discoverable outage into a lie.

WHY NO EXISTING GATE CAN SEE THIS, AND WHY IT IS NOT A TUNING PROBLEM
--------------------------------------------------------------------
Every other DDL gate here compares CURRENT artifacts against each other. A fresh database always
matches the current script -- that is what "current" means -- so a same-instant comparison is
satisfied by definition, whatever the edit did to an existing database. The missing dimension is
TIME. The question can only be asked as a diff between a shipped artifact and its own PREVIOUS
COMMITTED CONTENT, which is what this gate does and what nothing else does:

    ddl-pack-completeness   is the DDL obtainable at all?      current tree vs current tree
    shipped-ddl-sweep       does shipped DDL declare the
                            columns the code writes?           current tree vs current tree
    THIS GATE               can a consumer who already ran
                            the OLD version reach the new?     committed history vs now

THE PROVISIONING SURFACE IS NOT THE .sql FILES. IT IS WIDER, AND THAT IS LOAD-BEARING.
--------------------------------------------------------------------------------------
A .sql-keyed census of this tree is STRUCTURALLY BLIND to the defect that produced this gate, not
merely narrow. Measured at the time of writing: 48 shipped .sql files under src, and 20 .cs files
across 10 packages that author CREATE TABLE as an interpolated string and execute it from an
auto-create path. The instance this gate was built for lives entirely in the second group -- a
tenant column and a changed PRIMARY KEY added in place to a C#-authored CREATE TABLE, in a package
that shipped no .sql at the time, so no .sql-keyed instrument could ever have surfaced it.

Both limbs are provisioning artifacts and both are diffed. A gate written against this defect that
could not see the defect would be the advertised-but-unwired shape, in the gate written to prevent
it.

THE THREE STATES -- REFUSE IS NOT A PASS
  0 PASS     no new unmigrated in-place delta, and no new column-blind schema verification
  1 FAIL     an in-place delta has no upgrade path, or a baseline entry is stale
  2 REFUSE   this checker could not evaluate -- unreadable history, an unparseable script,
             a missing baseline, or a table identity it could not resolve

REFUSE is separate from FAIL because the two demand opposite responses. A FAIL says "a defect is
here, fix it". A REFUSE says "I did not look" -- and reporting that as either a pass or a defect
sends the reader somewhere useless. The empty-population case is the one that matters most: a
history window in which nothing changed reports zero findings, which is indistinguishable from a
clean tree, so the number of artifacts EVALUATED is printed with every verdict and an empty window
REFUSEs rather than passing.

A refusal is scoped to the artifact that caused it and never to the run: one unreadable script must
not turn a narrow, nameable blind spot into total blindness. A refusal the baseline records is a
known, named gap and does not fail the run -- but it is still printed on the happy path, because an
admission that is only mentioned when it is new becomes invisible the moment it is recorded. An
unrecorded one exits 2. The tree has one genuine refusal today: a table name that is a method
parameter rather than a declared option, which no amount of widening resolves without dataflow this
checker deliberately does not attempt.

WHAT COUNTS AS AN UPGRADE PATH
------------------------------
For a table that gained or altered a column, key or constraint, the package must carry an
ALTER TABLE naming that table and that column. Coverage is evaluated against the CURRENT tree, not
against the commit that introduced the delta, and that is deliberate: the consumer-facing question
is whether an upgrade path exists TODAY, not whether it arrived in the same commit. It also makes
the baseline self-cleaning -- when a missing migration is finally written the finding disappears,
its baseline entry goes stale, and a stale entry is an error here, so the line must be removed
rather than left behind as a place the defect is quietly forgotten.

The escape hatch is deliberately narrow and deliberately visible in the diff:

    -- ddl-migration: provisioning-only <Table> - <reason>

It must name the table and carry a reason, so it cannot be a blanket, and it lives in the changed
artifact where a reviewer reading the change cannot miss it. A brand-new table needs no such
declaration and no migration: a script with no previous shipped content has nothing to migrate
FROM, and that case is detected structurally (absent at base, present now) rather than declared.

WHAT PRODUCES NO FINDING, BY CONSTRUCTION RATHER THAN BY RULE
------------------------------------------------------------
The comparison is over a PARSED column and constraint set, not over file text. So a comment-only
edit, a reflowed definition, an added SET ANSI_NULLS/QUOTED_IDENTIFIER, a removed batch separator
and a reordered column list all produce no delta at all -- there is no rule excluding them and
therefore no rule to get wrong. This is the liveness half, and it is the half that decides whether
anyone keeps a gate: one that fires on a comment change is suppressed within a week, at which
point the safety half protects nothing.

THE SECOND QUESTION: A VERIFICATION THAT CANNOT SEE A COLUMN CANNOT FAIL CLOSED
------------------------------------------------------------------------------
The migration check above is only half the defect. A store that advertises a fail-fast schema check
and implements it as a table-EXISTENCE probe certifies a legacy database as healthy -- it is
structurally incapable of noticing a missing column, so it reports success on precisely the
database that is broken. That is asked here rather than filed away, because it shares this gate's
population (the artifacts that author schema) and because the two are one failure: the in-place
edit opens the gap, and the existence-only check is what stops the consumer finding it.

USAGE
  ddl-migration-completeness.py                 scan committed history from the baseline's anchor
                                                to HEAD, plus the working tree, and the schema
                                                verifications. exit 0/1/2
  ddl-migration-completeness.py --base A [--head B]
                                                evaluate one explicit delta. --head defaults to the
                                                working tree. This is the form that proves the gate
                                                against a known historical commit.
  ddl-migration-completeness.py --self-test     prove this gate is non-vacuous (safety AND liveness)
"""

from __future__ import annotations

import os
import re
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(os.path.dirname(__file__))))

# Build output, not shipped source. A .sql copied into obj/ is not something a consumer provisions
# from, and counting it would compare a build artifact against itself.
_PRUNE = ("/obj/", "/bin/", "/node_modules/", "/.git/")

WORKING_TREE = ":working-tree:"


def repo_root() -> str:
    """Resolved per call, never captured at import.

    A module-level constant here would leave the self-test's arms scanning the REAL repository
    while believing they scanned a fixture. The arms would still go red and green in roughly the
    expected order, because a positive control passes just as happily against the wrong subject as
    the right one. The resolved root is printed with the result rather than assumed.
    """
    return os.environ.get("DDL_MIGRATION_REPO") or REPO


def baseline_path() -> str:
    """Resolved per call -- the same trap `repo_root` documents."""
    return os.environ.get("DDL_MIGRATION_BASELINE") or os.path.join(
        os.path.dirname(os.path.abspath(__file__)), "ddl-migration.baseline.txt"
    )


class Refuse(Exception):
    """Raised when the checker cannot evaluate. Never converted into a verdict.

    `reason` is deliberately SHORT and STABLE, because it becomes half of a baseline key. The full
    message names the revision the artifact was read at, which changes every run -- a key built
    from it would never match its own baseline entry, so every recorded gap would read as both
    unrecorded and stale on every run, and the baseline would be untenable within a day.
    """

    def __init__(self, message: str, reason: str | None = None) -> None:
        super().__init__(message)
        self.reason = reason or message


# ---------------------------------------------------------------------------------------------
# git access. Every call can fail, and a failure is REFUSE, never an empty answer -- an unreadable
# history read as "nothing changed" is the exact shape of a gate that reports clean by measuring
# nothing.
# ---------------------------------------------------------------------------------------------
def git(root: str, *args: str) -> str:
    try:
        proc = subprocess.run(
            ("git", "-C", root) + args,
            capture_output=True, text=True, encoding="utf-8", errors="replace",
        )
    except OSError as exc:
        raise Refuse(f"cannot run git: {exc}") from exc
    if proc.returncode != 0:
        raise Refuse(f"git {' '.join(args)} failed ({proc.returncode}): {proc.stderr.strip()}")
    return proc.stdout


def read_at(root: str, rev: str, path: str) -> str | None:
    """File content at a revision (or the working tree), or None when it did not exist there.

    'Did not exist' and 'could not be read' are different answers and are kept different: the first
    is the new-file case, which owes no migration, and the second is a REFUSE.
    """
    if rev == WORKING_TREE:
        full = os.path.join(root, path.replace("/", os.sep))
        if not os.path.isfile(full):
            return None
        try:
            with open(full, encoding="utf-8-sig", errors="replace") as fh:
                return fh.read()
        except OSError as exc:
            raise Refuse(f"cannot read {path} from the working tree: {exc}",
                         "unreadable") from exc
    proc = subprocess.run(
        ("git", "-C", root, "show", f"{rev}:{path}"),
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    if proc.returncode != 0:
        lowered = proc.stderr.lower()
        if "does not exist" in lowered or "exists on disk" in lowered:
            return None
        raise Refuse(f"cannot read {path} at {rev}: {proc.stderr.strip()}", "unreadable")
    return proc.stdout


# ---------------------------------------------------------------------------------------------
# DDL parsing. ONE parser, run over both limbs, because the DDL inside a C# interpolated literal is
# the same grammar as the DDL in a .sql file -- only the table identity is spelled differently.
# ---------------------------------------------------------------------------------------------

# An identifier part: bracketed, double-quoted, back-quoted, an interpolation hole, or bare. The
# hole is matched WHOLE (braces balanced by the pattern, not by dot-splitting) because
# `{_options.RegistrationsTableName}` contains a dot that is NOT a schema separator. Splitting a
# qualified name on every dot reads that as schema `{_options` and table `RegistrationsTableName}`,
# which matches no ALTER anywhere and turns every C#-authored table into a phantom finding.
_IDENT_PART = "(?:\\[[^\\]]+\\]|\"[^\"]+\"|`[^`]+`|\\{[^{}]*\\}|[A-Za-z_][A-Za-z0-9_$#]*)"
_QUALIFIED = _IDENT_PART + "(?:\\s*\\.\\s*" + _IDENT_PART + ")*"

_CREATE_TABLE = re.compile(
    "CREATE\\s+TABLE\\s+(?:IF\\s+NOT\\s+EXISTS\\s+)?(?P<name>" + _QUALIFIED + ")\\s*\\(",
    re.IGNORECASE,
)
_ALTER_TABLE = re.compile(
    "ALTER\\s+TABLE\\s+(?:IF\\s+EXISTS\\s+)?(?:ONLY\\s+)?(?P<name>" + _QUALIFIED + ")",
    re.IGNORECASE,
)

# A table-name default on an options type. Keyed off the declared default a consumer actually gets
# -- the same artifact-keyed approach the sibling packing gate uses -- rather than a hand-listed
# table inventory, which is the shape that has rotted on this tree before.
_TABLE_DEFAULT = re.compile(
    "public\\s+string\\s+(?P<prop>\\w+)\\s*\\{\\s*get;\\s*(?:set|init);\\s*\\}\\s*=\\s*"
    "\"(?P<name>[A-Za-z0-9_]+)\""
)

# The COMPOSED form: `public string FullFooTableName => $"{SchemaName}.{FooTableName}";`. Real stores
# interpolate the composed property into their DDL, not the leaf one, so a resolver that understood
# only the literal above would REFUSE on the majority of the C# limb -- an honest refusal, and a
# useless gate. One level of indirection is not an edge case here; it is the normal spelling.
_TABLE_EXPR = re.compile(
    'public\\s+string\\s+(?P<prop>\\w+)\\s*=>\\s*\\$?"(?P<body>(?:[^"\\\\]|\\\\.)*)"'
)

# Words that open a TABLE-level constraint rather than a column. Anything else at the head of a
# top-level item is a column name.
_CONSTRAINT_HEADS = {
    "CONSTRAINT", "PRIMARY", "UNIQUE", "FOREIGN", "CHECK", "INDEX", "KEY", "EXCLUDE", "PERIOD",
}

_LINE_COMMENT = re.compile("--[^\n]*|//[^\n]*")
_BLOCK_COMMENT = re.compile("/\\*.*?\\*/", re.DOTALL)

# The escape hatch, matched in the NEW content of a changed artifact. The reason is required by the
# pattern, not by a follow-up check, so a marker with nothing after the dash does not parse as a
# marker at all and the finding stands.
_PROVISIONING_ONLY = re.compile(
    "ddl-migration:\\s*provisioning-only\\s+(?P<table>[A-Za-z0-9_{}.\\[\\]\"]+)\\s*[-:]\\s*(?P<why>\\S.*)",
    re.IGNORECASE,
)


def strip_comments(text: str) -> str:
    """Remove line and block comments, in both SQL and C# spellings.

    This is what makes a comment-only edit produce no delta STRUCTURALLY. The alternative -- a rule
    that excludes comment changes -- is a rule, and rules about what to ignore are how a gate learns
    to ignore the thing that mattered.
    """
    return _LINE_COMMENT.sub(" ", _BLOCK_COMMENT.sub(" ", text))


def normalise(text: str) -> str:
    """Comment-free, whitespace-collapsed, case-folded: the comparison unit for a definition."""
    return re.sub("\\s+", " ", strip_comments(text)).strip().upper()


def table_key(qualified: str) -> str:
    """The bare table name -- schema and quoting discarded, interpolation holes preserved.

    Schema is discarded on purpose: a migration routinely qualifies differently from the CREATE
    (`[compliance].[LegalHolds]` against `compliance.legal_holds` against a bare name behind an
    option), and treating those as different tables would report a covered delta as uncovered.
    """
    depth = 0
    parts: list[str] = []
    current: list[str] = []
    for ch in qualified:
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
        if ch == "." and depth == 0:
            parts.append("".join(current))
            current = []
            continue
        current.append(ch)
    parts.append("".join(current))
    last = parts[-1].strip()
    if len(last) >= 2 and last[0] in "[\"`":
        last = last[1:-1]
    return last.strip()


def balanced_body(text: str, open_paren: int) -> str | None:
    """Text between `(` at open_paren and its matching `)`, or None when unbalanced.

    Unbalanced means unparseable, which is a REFUSE at the call site rather than a silent skip. A
    parser that shrugs at input it cannot read produces a clean report over the half it managed.
    """
    depth = 0
    for i in range(open_paren, len(text)):
        if text[i] == "(":
            depth += 1
        elif text[i] == ")":
            depth -= 1
            if depth == 0:
                return text[open_paren + 1 : i]
    return None


def split_top_level(body: str) -> list[str]:
    """Comma-separated items at depth 0. `DECIMAL(18,2)` and `PRIMARY KEY (a, b)` stay whole."""
    items: list[str] = []
    depth = 0
    current: list[str] = []
    for ch in body:
        if ch in "([":
            depth += 1
        elif ch in ")]":
            depth -= 1
        if ch == "," and depth == 0:
            items.append("".join(current))
            current = []
            continue
        current.append(ch)
    items.append("".join(current))
    return [i for i in (x.strip() for x in items) if i]


class Table:
    __slots__ = ("columns", "constraints")

    def __init__(self) -> None:
        self.columns: dict[str, str] = {}   # column name (upper) -> normalised definition
        self.constraints: set[str] = set()  # normalised table-level constraint text


def parse_tables(text: str, where: str) -> dict[str, Table]:
    """Every CREATE TABLE in a blob -> {table key: Table}. Raises Refuse on unparseable DDL.

    Doc-comment lines are dropped first: a `/// CREATE TABLE ...` in prose is documentation, and
    parsing it would invent a table no consumer provisions and no migration can ever cover -- a
    finding that cannot be fixed, which is how a gate earns its suppression.
    """
    text = "\n".join(l for l in text.splitlines() if not l.lstrip().startswith("///"))
    stripped = strip_comments(text)
    out: dict[str, Table] = {}
    for match in _CREATE_TABLE.finditer(stripped):
        body = balanced_body(stripped, match.end() - 1)
        if body is None:
            raise Refuse(f"unparseable CREATE TABLE (unbalanced parentheses) in {where}",
                         "unparseable CREATE TABLE")
        table = out.setdefault(table_key(match.group("name")), Table())
        for item in split_top_level(body):
            # The head token ends at whitespace OR at the opening parenthesis. Stopping only at
            # whitespace makes classification depend on how the author spaced the DDL: `UNIQUE (a,
            # b)` yields the head `UNIQUE` and is read as the table-level constraint it is, while
            # `UNIQUE(a, b)` yields `UNIQUE(a,` -- not a constraint keyword, so the whole
            # constraint is filed as a COLUMN named `UNIQUE(a,`. That is not a cosmetic
            # misreading. The constraint set then compares as EMPTY, so a key that gains a column
            # is reported as an altered column under a nonsense name, and an added constraint
            # whose leading keyword matches one already present is not reported AT ALL -- the
            # two collapse onto the same invented column name and the later one overwrites the
            # earlier. Whitespace is not part of what the schema means, so it must not decide
            # what the parser thinks it is looking at.
            head = re.match("[A-Za-z_\\[\"`{][^\\s(]*", item)
            if not head:
                continue
            word = head.group(0).strip("[]\"`").upper()
            if word in _CONSTRAINT_HEADS:
                table.constraints.add(normalise(item))
                continue
            table.columns[table_key(head.group(0)).upper()] = normalise(item)
    return out


def resolve(key: str, defaults: "Defaults", fields: dict[str, str], setvars: "dict[str, str] | None" = None) -> str | None:
    """`{_options.RegistrationsTableName}` -> `DataInventoryRegistrations`, or None if unresolvable.

    Resolution is scoped to the DECLARING TYPE, not to the package. Property names are not unique
    inside a package -- several options classes here declare a bare `TableName` -- so a package-wide
    map keyed on the property name alone silently returns whichever class was walked first. That is
    not a near miss: it attributes a real delta to the wrong table, then reports it as unmigrated
    because no ALTER names the table it invented. A confident finding about the wrong subject reads
    exactly like a working gate, which is why the declaring type is carried through.

    None becomes a REFUSE at the call site and never a guess.
    """
    if "$(" in key and setvars:
        for hole in re.findall(r"[$][(]([^)]+)[)]", key):
            value = setvars.get(hole.strip())
            if value is None:
                break
            key = key.replace("$(" + hole + ")", value)
    if "{" not in key:
        return key
    resolved = key
    for hole in re.findall("[{][^{}]*[}]", key):
        segments = [x.strip() for x in hole.strip("{}").split(".")]
        prop = segments[-1]
        owner = fields.get(segments[0]) if len(segments) > 1 else None
        value = None
        if owner and owner in defaults.by_type and prop in defaults.by_type[owner]:
            value = defaults.by_type[owner][prop]
        elif prop in defaults.unambiguous:
            value = defaults.unambiguous[prop]
        if value is None:
            return None
        resolved = resolved.replace(hole, value)
    return resolved


# ---------------------------------------------------------------------------------------------
# The provisioning surface
# ---------------------------------------------------------------------------------------------
def is_provisioning_path(path: str) -> bool:
    lowered = "/" + path.lower()
    if any(p in lowered for p in _PRUNE):
        return False
    if not lowered.startswith("/src/"):
        return False
    return lowered.endswith(".sql") or lowered.endswith(".cs")


def package_of(root: str, path: str) -> str | None:
    """Nearest ancestor directory holding a .csproj -- the unit a consumer actually installs."""
    parts = path.split("/")
    for cut in range(len(parts) - 1, 0, -1):
        directory = os.path.join(root, *parts[:cut])
        try:
            entries = os.listdir(directory)
        except OSError:
            continue
        for entry in entries:
            if entry.lower().endswith(".csproj"):
                return entry[: -len(".csproj")]
    return None


def package_dir(root: str, path: str) -> str | None:
    parts = path.split("/")
    for cut in range(len(parts) - 1, 0, -1):
        directory = os.path.join(root, *parts[:cut])
        try:
            entries = os.listdir(directory)
        except OSError:
            continue
        if any(e.lower().endswith(".csproj") for e in entries):
            return directory
    return None


def unescape_csharp(body: str) -> str:
    """C# string-literal escapes -> the characters they denote."""
    return body.replace('\\"', '"').replace('\\\\', '\\')


class Defaults:
    """Table-name defaults, per declaring type, plus the package-wide UNAMBIGUOUS subset.

    The unambiguous fallback exists for a hole that names no field (`{TableName}` inside a composed
    property). It carries a property only when every class in the package that declares it declares
    the SAME value -- so the fallback can never silently pick a side in a collision.
    """

    __slots__ = ("by_type", "unambiguous")

    def __init__(self, by_type: dict[str, dict[str, str]], unambiguous: dict[str, str]) -> None:
        self.by_type = by_type
        self.unambiguous = unambiguous


_CLASS_DECL = re.compile("(?:class|record|struct)\\s+(?P<name>\\w+)")


def _declaring_type(text: str, position: int) -> str:
    """The nearest enclosing type declaration before `position`, or "" when there is none."""
    name = ""
    for match in _CLASS_DECL.finditer(text, 0, position):
        name = match.group("name")
    return name


_FIELD_DECL = re.compile("(?P<type>\\w+)\\s+(?P<field>_\\w+)\\s*[;=)]")


def sqlcmd_vars(text: str) -> dict[str, str]:
    """SQLCMD variable name -> declared value, from the script's own `:setvar` lines.

    A T-SQL provisioning script parameterises its tables as $(OutboxTable) and declares the default
    beside them. Without this, a delta recorded while the script used those names keys on the literal
    text "$(OutboxTable)", while coverage indexes the ALTER under the plain name the script carries
    today -- so the two sides of the comparison never meet and NO migration can ever close the
    finding, however correct. Resolve the name the author parameterised, not the text they typed.
    """
    out: dict[str, str] = {}
    pat = r"^\s*:setvar\s+(?P<name>\w+)\s+\"?(?P<value>[^\"\r\n]+)\"?\s*$"
    for m in re.finditer(pat, text, re.MULTILINE):
        out[m.group("name")] = m.group("value").strip()
    return out


def field_types(text: str) -> dict[str, str]:
    """field name -> declared type, for the fields an interpolation hole names.

    `private readonly SqlServerLegalHoldStoreOptions _options;` is what tells the resolver WHICH
    class's `TableName` a `{_options.TableName}` means.
    """
    out: dict[str, str] = {}
    for match in _FIELD_DECL.finditer(text):
        out.setdefault(match.group("field"), match.group("type"))
    return out


def package_defaults(root: str, pkgdir: str) -> Defaults:
    """Declared table-name defaults across a package, attributed to the type that declares them.

    Two shapes, because both appear in shipped stores: a literal default, and a composed property
    built from other properties. The composed ones are resolved to a fixpoint so that
    `FullDiscoveredLocationsTableName` reaches an actual name rather than another hole.
    """
    literals: dict[str, dict[str, str]] = {}
    exprs: dict[str, dict[str, str]] = {}
    for dirpath, dirnames, filenames in os.walk(pkgdir):
        dirnames[:] = [d for d in dirnames if d not in ("obj", "bin")]
        for name in filenames:
            if not name.lower().endswith(".cs"):
                continue
            try:
                with open(os.path.join(dirpath, name), encoding="utf-8-sig", errors="replace") as fh:
                    text = fh.read()
            except OSError:
                continue
            for match in _TABLE_DEFAULT.finditer(text):
                owner = _declaring_type(text, match.start())
                literals.setdefault(owner, {}).setdefault(match.group("prop"), match.group("name"))
            for match in _TABLE_EXPR.finditer(text):
                owner = _declaring_type(text, match.start())
                # The composed body is C# source, so a quoted identifier reaches us escaped. Left
                # escaped, the table key resolves to a lone backslash -- a confident finding about a
                # table that does not exist, which reads exactly like a working gate.
                body = unescape_csharp(match.group("body"))
                exprs.setdefault(owner, {}).setdefault(match.group("prop"), body)

    by_type: dict[str, dict[str, str]] = {t: dict(v) for t, v in literals.items()}
    for owner, props in exprs.items():
        by_type.setdefault(owner, {})
        # Bounded, not "until nothing changes": a property that (directly or mutually) references
        # itself would otherwise spin forever, and a gate that hangs is read as a gate that passed
        # by whoever kills it. Anything still unresolved after the bound stays unresolved, which is
        # a REFUSE at the call site -- the honest answer, and never a guess.
        for _ in range(8):
            progressed = False
            for prop, body in props.items():
                if prop in by_type[owner]:
                    continue
                holes = re.findall("[{][^{}]*[}]", body)
                names = [h.strip("{}").split(".")[-1].strip() for h in holes]
                if any(n not in by_type[owner] for n in names):
                    continue
                value = body
                for hole, leaf in zip(holes, names):
                    value = value.replace(hole, by_type[owner][leaf])
                by_type[owner][prop] = value
                progressed = True
            if not progressed:
                break

    seen: dict[str, set[str]] = {}
    for props in by_type.values():
        for prop, value in props.items():
            seen.setdefault(prop, set()).add(value)
    unambiguous = {p: next(iter(v)) for p, v in seen.items() if len(v) == 1}
    return Defaults(by_type, unambiguous)


def package_alter_index(root: str, pkgdir: str, defaults: "Defaults") -> dict[str, str]:
    """table (upper) -> concatenated text of every ALTER TABLE on it, across the CURRENT package.

    Coverage is read off the tree as it stands, not off the commit that introduced the delta. The
    consumer-facing question is whether an upgrade path exists today; and reading it from the
    current tree is what lets a baseline entry go stale by itself once the migration is written.
    """
    index: dict[str, list[str]] = {}
    for dirpath, dirnames, filenames in os.walk(pkgdir):
        dirnames[:] = [d for d in dirnames if d not in ("obj", "bin")]
        for name in filenames:
            if not (name.lower().endswith(".sql") or name.lower().endswith(".cs")):
                continue
            try:
                with open(os.path.join(dirpath, name), encoding="utf-8-sig", errors="replace") as fh:
                    raw = fh.read()
                text = strip_comments(raw)
            except OSError:
                continue
            fields = field_types(raw)
            setvars = sqlcmd_vars(raw)
            # Each chunk is one ALTER TABLE and everything up to the next one, so a column named in
            # a DIFFERENT statement cannot be read as covering this table. A single window over the
            # whole file would let any ALTER anywhere vouch for any column anywhere.
            hits = list(_ALTER_TABLE.finditer(text))
            for i, match in enumerate(hits):
                end = hits[i + 1].start() if i + 1 < len(hits) else len(text)
                resolved = resolve(table_key(match.group("name")), defaults, fields, setvars)
                if resolved is None:
                    continue
                resolved = table_key(resolved)
                index.setdefault(resolved.upper(), []).append(text[match.start() : end])
    return {k: "\n".join(v) for k, v in index.items()}


# ---------------------------------------------------------------------------------------------
# Question 1 -- an in-place delta must have an upgrade path
# ---------------------------------------------------------------------------------------------
class Finding:
    __slots__ = ("key", "detail")

    def __init__(self, key: str, detail: str) -> None:
        self.key = key
        self.detail = detail


def deltas(old: Table, new: Table) -> list[tuple[str, str]]:
    """(kind, name) pairs an existing database would NOT pick up by re-running the CREATE."""
    out: list[tuple[str, str]] = []
    for column, definition in sorted(new.columns.items()):
        if column not in old.columns:
            out.append(("column-added", column))
        elif old.columns[column] != definition:
            out.append(("column-altered", column))
    for constraint in sorted(new.constraints - old.constraints):
        out.append(("constraint-added", constraint))
    return out


def constraint_columns(text: str) -> list[str]:
    """Column names inside a table-level constraint, so an ALTER can be matched against them."""
    body = re.search("\\(([^()]*)\\)", text)
    if not body:
        return []
    return [c.strip().strip("[]\"`").upper() for c in body.group(1).split(",") if c.strip()]


def evaluate_delta(root: str, path: str, before: str | None, after: str | None,
                   label: str) -> list[Finding]:
    """One artifact, one step of history. Raises Refuse when it cannot be evaluated."""
    if after is None or before is None:
        # Deleted, or brand new. A script with no previous shipped content has nothing to migrate
        # FROM, which is the legitimate case the bead's own census identified -- detected here
        # structurally rather than by a declaration someone has to remember to write.
        return []

    new_tables = parse_tables(after, f"{path} at {label}")
    if not new_tables:
        return []
    old_tables = parse_tables(before, f"{path} at its previous committed content")

    pkg = package_of(root, path)
    pkgdir = package_dir(root, path)
    if pkg is None or pkgdir is None:
        raise Refuse(f"cannot attribute {path} to a package (no .csproj above it)",
                     "unattributable to a package")
    defaults = package_defaults(root, pkgdir)
    alters = package_alter_index(root, pkgdir, defaults)
    fields = field_types(after)
    setvars = sqlcmd_vars(after)

    declared: dict[str, str] = {}
    for match in _PROVISIONING_ONLY.finditer(after):
        resolved = resolve(table_key(match.group("table")), defaults, fields, setvars)
        if resolved is not None:
            declared[resolved.upper()] = match.group("why").strip()

    findings: list[Finding] = []
    for raw_table, new_table in sorted(new_tables.items()):
        if raw_table not in old_tables:
            continue  # new table -- nothing to migrate from
        changes = deltas(old_tables[raw_table], new_table)
        if not changes:
            continue
        resolved = resolve(raw_table, defaults, fields, setvars)
        if resolved is not None:
            # A composed property expands to `schema.table`; the schema is dropped for the same
            # reason it is dropped in table_key -- a migration routinely qualifies differently.
            resolved = table_key(resolved)
        if resolved is None:
            raise Refuse(
                f"{path}: table identity {raw_table!r} has an interpolation this checker cannot "
                f"resolve to a name, so migration coverage for it cannot be evaluated",
                f"unresolvable table identity {raw_table!r}",
            )
        table = resolved.upper()
        if table in declared:
            continue
        window = alters.get(table, "")
        for kind, name in changes:
            if kind == "constraint-added":
                columns = constraint_columns(name)
                covered = bool(window) and all(
                    re.search("\\b" + re.escape(c) + "\\b", window, re.I) for c in columns
                ) and bool(columns)
                short = re.sub("\\s+", " ", name)[:60]
                key = f"{pkg}:{resolved}:constraint:{short}"
            else:
                covered = bool(re.search("\\b" + re.escape(name) + "\\b", window, re.I))
                key = f"{pkg}:{resolved}:{name}"
            if not covered:
                findings.append(Finding(key, f"{kind} in {path} ({label})"))
    return findings


# ---------------------------------------------------------------------------------------------
# Question 2 -- a schema verification that cannot see a column cannot fail closed
# ---------------------------------------------------------------------------------------------
# Probes that answer "does the TABLE exist". Every one of these is satisfied by a legacy table that
# is missing the new column, so a verification built only from these reports healthy on exactly the
# database that is broken.
_TABLE_PROBE = re.compile(
    "sys\\.tables|information_schema\\.tables|to_regclass|OBJECT_ID\\s*\\(|"
    "user_tables|all_tables|sqlite_master",
    re.IGNORECASE,
)
# The advertised surface: a member whose NAME promises the consumer a schema verification. Keyed on
# the promise rather than on a probe, because the defect is the gap between what is advertised and
# what is checked.
_VERIFY_SURFACE = re.compile("Verify\\w*Schema")

# Probes that answer "does the COLUMN exist". Any one of these makes the verification able to fail
# on a stale schema, which is the property being required.
_COLUMN_PROBE = re.compile(
    "sys\\.columns|information_schema\\.columns|pg_attribute|user_tab_columns|"
    "all_tab_columns|pragma\\s+table_info|COL_LENGTH\\s*\\(",
    re.IGNORECASE,
)


def check_verifications(root: str, out, err) -> tuple[int, list[str]]:
    """Files that author schema AND probe for it must probe at COLUMN level.

    Keyed on the artifact -- a file that both creates tables and queries a catalogue is a store's
    own provisioning path -- rather than on a method-name convention, which drifts.
    """
    findings: list[str] = []
    examined = 0
    src = os.path.join(root, "src")
    if not os.path.isdir(src):
        return 0, []
    for dirpath, dirnames, filenames in os.walk(src):
        dirnames[:] = [d for d in dirnames if d not in ("obj", "bin", ".git")]
        for name in filenames:
            if not name.lower().endswith(".cs"):
                continue
            full = os.path.join(dirpath, name)
            try:
                with open(full, encoding="utf-8-sig", errors="replace") as fh:
                    text = fh.read()
            except OSError:
                continue
            body = strip_comments(text)
            if not _CREATE_TABLE.search(body):
                continue
            # The population is the ADVERTISED check, not every file that happens to probe a
            # catalogue. A migrator or a table manager probes existence to decide whether to
            # CREATE, which is correct and complete; flagging those is crying wolf, and a gate that
            # cries wolf is suppressed, at which point it protects nothing. What the consumer is
            # promised -- and what certifies a broken database as healthy -- is a store that
            # advertises "verify the schema is present, else fail fast".
            if not _VERIFY_SURFACE.search(body):
                continue
            if not _TABLE_PROBE.search(body):
                continue
            examined += 1
            if not _COLUMN_PROBE.search(body):
                rel = os.path.relpath(full, root).replace(os.sep, "/")
                pkg = package_of(root, rel) or "?"
                findings.append(f"{pkg}:verify-is-existence-only:{os.path.basename(rel)}")
    print(f"verify: schema-probing provisioning files examined={examined}  "
          f"existence-only={len(findings)}", file=out)
    return examined, findings


# ---------------------------------------------------------------------------------------------
# Baseline
# ---------------------------------------------------------------------------------------------
def read_baseline(path: str) -> tuple[set[str], str | None] | None:
    """(entries, anchor) or None when unreadable.

    An unreadable baseline read as "no entries" turns every tracked gap into a fresh FAIL and the
    gate is reverted within the hour; read as "everything forgiven" it suppresses real regressions.
    Neither guess is acceptable, so the checker declines to measure.
    """
    anchor: str | None = None
    entries: set[str] = set()
    try:
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if line.lower().startswith("# anchor:"):
                    anchor = line.split(":", 1)[1].strip()
                    continue
                if not line or line.startswith("#"):
                    continue
                entries.add(line)
    except OSError:
        return None
    return entries, anchor


# ---------------------------------------------------------------------------------------------
# Scanning
# ---------------------------------------------------------------------------------------------
def changed_paths(root: str, before_rev: str, after_rev: str) -> list[str]:
    if after_rev == WORKING_TREE:
        raw = git(root, "status", "--porcelain", "--untracked-files=all")
        paths = []
        for line in raw.splitlines():
            if len(line) < 4:
                continue
            candidate = line[3:].strip().strip('"')
            if " -> " in candidate:
                candidate = candidate.split(" -> ", 1)[1]
            paths.append(candidate.replace("\\", "/"))
    else:
        raw = git(root, "diff", "--name-only", f"{before_rev}", f"{after_rev}")
        paths = [p.strip().replace("\\", "/") for p in raw.splitlines() if p.strip()]
    return sorted({p for p in paths if is_provisioning_path(p)})


def scan_step(root: str, before_rev: str, after_rev: str,
              label: str) -> tuple[list[Finding], int, list[str]]:
    """(findings, artifacts EVALUATED, refusal keys).

    The evaluated count is not diagnostics. Zero findings over zero artifacts is indistinguishable
    from zero findings over a clean tree, and only that count separates them -- so it is returned
    alongside the verdict rather than inferred from whether anything was reported. A brand-new
    script counts as evaluated: "there is no previous content to migrate from" is an observation,
    not the absence of one.

    A refusal is scoped to the ARTIFACT that caused it, never to the run. One unparseable script
    aborting the whole scan would convert a narrow, nameable coverage gap into total blindness --
    and a gate that stops looking because of one file it cannot read is the failure this gate's own
    three-state model exists to prevent.
    """
    findings: list[Finding] = []
    refusals: list[str] = []
    evaluated = 0
    for path in changed_paths(root, before_rev, after_rev):
        try:
            after = read_at(root, after_rev, path)
            if after is None or "CREATE TABLE" not in after.upper():
                continue
            before = read_at(root, before_rev, path)
            evaluated += 1
            findings.extend(evaluate_delta(root, path, before, after, label))
        except Refuse as exc:
            refusals.append(f"refuse:{path}:{exc.reason}")
    return findings, evaluated, refusals


def scan(out=sys.stdout, err=sys.stderr, base: str | None = None,
         head: str | None = None) -> int:
    root = repo_root()
    # The resolved subject is part of the finding, not context. A count with no stated subject
    # cannot be reproduced or challenged, and a positive control passes just as happily against the
    # wrong subject as the right one.
    print(f"scanning: {root}", file=out)

    bpath = baseline_path()
    loaded = read_baseline(bpath)
    if loaded is None:
        print(f"::error:: REFUSE - cannot read baseline: {bpath}", file=err)
        print("Nothing was measured. This is not a pass.", file=err)
        return 2
    baseline, anchor = loaded

    try:
        if base is not None:
            after_rev = WORKING_TREE if head is None else head
            steps = [(base, after_rev, f"{base}..{'working tree' if head is None else head}")]
            print(f"window: explicit delta {steps[0][2]}", file=out)
        else:
            if not anchor:
                print(f"::error:: REFUSE - baseline {os.path.basename(bpath)} declares no "
                      "'# anchor:' commit, so the history window is undefined.", file=err)
                print("Nothing was measured. This is not a pass.", file=err)
                return 2
            git(root, "rev-parse", "--verify", anchor + "^{commit}")
            revs = [r.strip() for r in
                    git(root, "rev-list", "--reverse", f"{anchor}..HEAD").splitlines() if r.strip()]
            steps = [(f"{r}~1", r, r[:9]) for r in revs]
            steps.append(("HEAD", WORKING_TREE, "working tree"))
            print(f"window: {anchor[:9]}..HEAD ({len(revs)} commit(s)) + the working tree", file=out)

        findings: list[Finding] = []
        refusals: list[str] = []
        evaluated = 0
        for before_rev, after_rev, label in steps:
            step_findings, step_evaluated, step_refusals = scan_step(
                root, before_rev, after_rev, label)
            evaluated += step_evaluated
            findings.extend(step_findings)
            refusals.extend(step_refusals)
    except Refuse as exc:
        print(f"::error:: REFUSE - {exc}", file=err)
        print("Nothing conclusive was measured. This is not a pass.", file=err)
        return 2

    seen: dict[str, str] = {}
    for finding in findings:
        seen.setdefault(finding.key, finding.detail)

    # A refusal the baseline already records is a KNOWN, NAMED coverage gap -- the gate ran, said
    # which artifact it could not evaluate, and that admission is committed where a reader can see
    # it. An unrecorded one is the gate discovering it is blind somewhere new, which must never be
    # absorbed into a pass. Refusals join `seen` so the same both-directions staleness applies:
    # when a table identity becomes resolvable, its line here goes stale and must be removed.
    unrecorded = sorted({r for r in refusals} - baseline)
    for key in sorted(set(refusals)):
        seen.setdefault(key, "could not be evaluated")

    verify_examined, verify_findings = check_verifications(root, out, err)
    for key in verify_findings:
        seen.setdefault(key, "schema verification probes table existence only")

    fresh = sorted(set(seen) - baseline)
    stale = sorted(baseline - set(seen))

    # Counted separately, never summed. A refusal reported inside a defect count is the
    # distinguishability failure the three-state model exists to remove: it sends a reader to
    # investigate healthy code, or teaches them to discount the number entirely.
    unmigrated = len(seen) - len(verify_findings) - len(set(refusals) & set(seen))
    print(f"migrations: artifacts evaluated={evaluated}  "
          f"unmigrated in-place deltas={unmigrated}  "
          f"verify gaps={len(verify_findings)}  not evaluated={len(set(refusals))}  "
          f"baselined={len(baseline)}  new={len(fresh)}  stale-baseline={len(stale)}", file=out)

    if unrecorded:
        print(f"::error:: REFUSE - {len(unrecorded)} artifact(s) could not be evaluated and are not "
              "recorded as known gaps:", file=err)
        for key in unrecorded:
            print(f"::error::   {key}", file=err)
        print(
            "\nThis is NOT a report of defects and NOT a pass. The checker could not read these,\n"
            "so it does not know whether they are broken. Either make the identity resolvable, or\n"
            f"record the gap in {os.path.basename(bpath)} so the blindness is committed where a\n"
            "reader can see it rather than absorbed into a green run.",
            file=err,
        )
        return 2

    if refusals:
        # Printed on the happy path too. A knowingly-unevaluated artifact that is only mentioned
        # when it is new becomes invisible the moment it is baselined, and an invisible admission
        # is not an admission.
        print(f"note: {len(set(refusals))} artifact(s) knowingly NOT evaluated (recorded in "
              f"{os.path.basename(bpath)}); their coverage is unknown, not clean.", file=out)

    if fresh:
        print(f"::error:: {len(fresh)} provisioning change(s) an upgrading consumer cannot follow:",
              file=err)
        for key in fresh:
            print(f"::error::   {key}  -- {seen[key]}", file=err)
        print(
            "\nA consumer who already provisioned from the previous version re-runs the create,\n"
            "the guard sees the table and skips, and the column is never added -- so the first\n"
            "call fails on their database and never on ours. Ship an ALTER TABLE for the table and\n"
            "column in the same package, or, when the change genuinely cannot reach an existing\n"
            "database, declare it in the changed artifact:\n\n"
            "  -- ddl-migration: provisioning-only <Table> - <why an existing database is unaffected>\n\n"
            f"A gap that is known and not yet closed belongs in {os.path.basename(bpath)}, with the\n"
            "issue that will close it.",
            file=err,
        )
        return 1

    if evaluated == 0 and verify_examined == 0:
        # Zero findings over a population of zero is indistinguishable from a clean tree, so it is
        # never reported as PASS -- and this is checked BEFORE the stale-baseline claim below,
        # because "these entries are no longer findings" is unfounded when nothing was measured.
        # A checker that prunes a real gap from the baseline on the strength of having looked at
        # nothing is worse than one that never ran.
        print("::error:: REFUSE - nothing was in scope: no provisioning artifact changed in the "
              "window and no schema-probing provisioning file exists.", file=err)
        print("A zero over an empty population is not a clean tree.", file=err)
        return 2

    if stale:
        # Checked in BOTH directions on purpose. A forgiveness list that only ever grows stops being
        # a record of known gaps and becomes the place regressions hide.
        #
        # Only in the anchored window. An explicit --base delta covers a slice of history, so almost
        # every baseline entry is legitimately absent from it -- enforcing staleness there would
        # demand deleting live entries on the strength of a window that never looked for them.
        if base is not None:
            print(f"note: {len(stale)} baseline entr(ies) not reproduced by this explicit delta - "
                  "staleness is only judged over the anchored window, so this is not a verdict.",
                  file=out)
        else:
            print(f"::error:: {len(stale)} baseline entr(ies) are no longer findings - prune them:",
                  file=err)
            for key in stale:
                print(f"::error::   {key}", file=err)
            return 1

    # The verdict states what was actually established, and never more. "Every change has an
    # upgrade path" would be FALSE on any tree with a recorded gap -- the numbers printed directly
    # above would contradict the sentence directly below them, and a verdict a reader can refute
    # from the same output teaches them to stop reading the output.
    if baseline:
        print(f"PASS - no NEW gap. {len(baseline)} recorded gap(s) remain open and are listed in "
              f"{os.path.basename(bpath)}; they are known debt, not a clean tree.", file=out)
    else:
        print("PASS - every in-place provisioning change in the window has an upgrade path, and "
              "every schema verification can see a column.", file=out)
    return 0


# ---------------------------------------------------------------------------------------------
# --self-test: prove the gate is NON-VACUOUS in both directions.
#
#   SAFETY   an in-place column addition with no ALTER is DETECTED
#   SAFETY   the same defect in C#-authored DDL is DETECTED   (the limb that carried the instance)
#   SAFETY   a changed PRIMARY KEY with no ALTER is DETECTED
#   SAFETY   an existence-only schema verification is DETECTED
#   SAFETY   a stale baseline entry is DETECTED
#   LIVENESS a column addition that DOES ship an ALTER is ALLOWED
#   LIVENESS a brand-new script is ALLOWED                    (nothing to migrate from)
#   LIVENESS a comment-only edit is ALLOWED
#   LIVENESS a SET-option / batch-separator edit is ALLOWED
#   LIVENESS a declared provisioning-only change is ALLOWED
#   LIVENESS a column-level verification is ALLOWED
#   REFUSE   an unparseable script does NOT report PASS
#   REFUSE   an empty population does NOT report PASS
#
# The liveness arms are the ones that would be forgotten, and they are the ones that decide whether
# this gate survives contact with a real tree: without them a gate hardcoded to `return 1` passes
# its own safety suite perfectly, fires on every comment edit, and is suppressed within a week -- at
# which point the safety arms guard nothing.
# ---------------------------------------------------------------------------------------------
_CSPROJ = "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n"

_V1_SQL = """CREATE TABLE IF NOT EXISTS example_table (
    id      UUID         NOT NULL PRIMARY KEY,
    payload VARCHAR(256) NOT NULL
);
"""

_V2_SQL_ADDS_COLUMN = """CREATE TABLE IF NOT EXISTS example_table (
    id        UUID         NOT NULL PRIMARY KEY,
    payload   VARCHAR(256) NOT NULL,
    tenant_id VARCHAR(255) NOT NULL DEFAULT '__untenanted__'
);
"""

_V2_SQL_COMMENT_ONLY = """-- Provisioning script for the example store.
CREATE TABLE IF NOT EXISTS example_table (
    -- the identity of the row
    id      UUID         NOT NULL PRIMARY KEY,
    payload VARCHAR(256) NOT NULL   -- the body
);
"""

_V2_SQL_SET_OPTIONS = """SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
CREATE TABLE IF NOT EXISTS example_table (
    id      UUID         NOT NULL PRIMARY KEY,
    payload VARCHAR(256) NOT NULL
);
"""

_V2_SQL_DECLARED = """-- ddl-migration: provisioning-only example_table - the column is computed on
-- read and no row is stored with it, so an existing database is unaffected.
CREATE TABLE IF NOT EXISTS example_table (
    id        UUID         NOT NULL PRIMARY KEY,
    payload   VARCHAR(256) NOT NULL,
    tenant_id VARCHAR(255) NOT NULL DEFAULT '__untenanted__'
);
"""

_MIGRATION_SQL = """ALTER TABLE example_table
    ADD COLUMN IF NOT EXISTS tenant_id VARCHAR(255) NOT NULL DEFAULT '__untenanted__';
"""

# The constraint limb. Written WITHOUT a space before the parenthesis on purpose: that is the
# spelling a head token stopping only at whitespace cannot classify, and the whole point is that
# it must be read the same as `UNIQUE (...)`. tenant_id is already a column in v1, so the ONLY
# delta between these two is the table-level key -- an arm that also moved a column would go red
# for the column and prove nothing about constraints.
_V1_SQL_CONSTRAINT = """CREATE TABLE IF NOT EXISTS example_table (
    id        UUID         NOT NULL,
    payload   VARCHAR(256) NOT NULL,
    tenant_id VARCHAR(255) NOT NULL,
    UNIQUE(id, payload)
);
"""

_V2_SQL_WIDENS_CONSTRAINT = """CREATE TABLE IF NOT EXISTS example_table (
    id        UUID         NOT NULL,
    payload   VARCHAR(256) NOT NULL,
    tenant_id VARCHAR(255) NOT NULL,
    UNIQUE(id, payload, tenant_id)
);
"""

# A SECOND key is added while the first stays. Both open with the same keyword, and the new one is
# written FIRST so that a parser which folds every constraint onto one invented column name keeps
# the LAST one and compares it against the unchanged v1 value -- reporting no delta at all. The
# addition is real and an upgrading database will not pick it up, so a silent pass here is the
# worst outcome the gate has: a schema change that reaches a consumer with nothing to apply.
_V2_SQL_ADDS_SECOND_CONSTRAINT = """CREATE TABLE IF NOT EXISTS example_table (
    id        UUID         NOT NULL,
    payload   VARCHAR(256) NOT NULL,
    tenant_id VARCHAR(255) NOT NULL,
    UNIQUE(id, tenant_id),
    UNIQUE(id, payload)
);
"""

# The rebuild idiom, which is how a key change is shipped on an engine with no ADD CONSTRAINT:
# rename the old table aside, create the new shape, copy the rows, drop the original. It is still
# an ALTER TABLE on the table in question, so it is matched the same way an in-place ALTER is.
_CONSTRAINT_MIGRATION_SQL = """ALTER TABLE example_table RENAME TO example_table_prior_shape;

CREATE TABLE example_table (
    id        UUID         NOT NULL,
    payload   VARCHAR(256) NOT NULL,
    tenant_id VARCHAR(255) NOT NULL,
    UNIQUE(id, payload, tenant_id)
);

INSERT INTO example_table (id, payload, tenant_id)
    SELECT id, payload, tenant_id FROM example_table_prior_shape;

DROP TABLE example_table_prior_shape;
"""

_UNPARSEABLE_SQL = """CREATE TABLE IF NOT EXISTS example_table (
    id      UUID         NOT NULL PRIMARY KEY,
    payload VARCHAR(256) NOT NULL
"""

# The C# limb, spelled the way the real stores spell it: the table name is an interpolated option
# whose default is declared elsewhere in the package, so the identity must be resolved before any
# ALTER can be matched against it.
_V1_CS = """namespace Fixture;
public sealed class StoreOptions
{
\tpublic string RegistrationsTableName { get; set; } = "ExampleRegistrations";
}
public sealed class Store
{
\tprivate readonly StoreOptions _options = new();
\tpublic string Ddl => $@"
\t\tCREATE TABLE {_options.RegistrationsTableName} (
\t\t\tTableName NVARCHAR(256) NOT NULL,
\t\t\tFieldName NVARCHAR(256) NOT NULL,
\t\t\tCONSTRAINT PK_Registrations PRIMARY KEY (TableName, FieldName)
\t\t)";
}
"""

_V2_CS_ADDS_COLUMN_AND_KEY = """namespace Fixture;
public sealed class StoreOptions
{
\tpublic string RegistrationsTableName { get; set; } = "ExampleRegistrations";
}
public sealed class Store
{
\tprivate readonly StoreOptions _options = new();
\tpublic string Ddl => $@"
\t\tCREATE TABLE {_options.RegistrationsTableName} (
\t\t\tTableName NVARCHAR(256) NOT NULL,
\t\t\tFieldName NVARCHAR(256) NOT NULL,
\t\t\tTenantId  NVARCHAR(255) NOT NULL DEFAULT '__untenanted__',
\t\t\tCONSTRAINT PK_Registrations PRIMARY KEY (TableName, FieldName, TenantId)
\t\t)";
}
"""

_VERIFY_EXISTENCE_ONLY_CS = """namespace Fixture;
public sealed class Store
{
	public const string Create = @"CREATE TABLE Widgets (Id INT NOT NULL PRIMARY KEY)";
	public Task VerifySchemaExistsAsync()
		=> Query(@"SELECT 1 FROM sys.tables WHERE name = 'Widgets'");
	private static Task Query(string sql) => Task.CompletedTask;
}
"""

_VERIFY_COLUMN_LEVEL_CS = """namespace Fixture;
public sealed class Store
{
	public const string Create = @"CREATE TABLE Widgets (Id INT NOT NULL PRIMARY KEY)";
	public Task VerifySchemaExistsAsync()
		=> Query(@"SELECT name FROM sys.columns WHERE object_id = OBJECT_ID('Widgets')");
	private static Task Query(string sql) => Task.CompletedTask;
}
"""

# A migrator: it authors DDL and probes table existence, and it advertises NO schema verification --
# it probes in order to decide whether to CREATE, which is correct and complete. This fixture is the
# reason the second question is keyed on the advertised surface rather than on the probe. Without
# this arm the predicate could be widened to every file that queries a catalogue, it would fire on
# every migrator and table manager in the tree, and it would be suppressed within a week.
_MIGRATOR_EXISTENCE_PROBE_CS = """namespace Fixture;
public sealed class Migrator
{
	public const string Create = @"CREATE TABLE Widgets (Id INT NOT NULL PRIMARY KEY)";
	public Task EnsureAsync()
		=> Query(@"IF OBJECT_ID('Widgets', 'U') IS NULL EXEC(@Create)");
	private static Task Query(string sql) => Task.CompletedTask;
}
"""


def _write(root: str, rel: str, body: str) -> None:
    full = os.path.join(root, rel.replace("/", os.sep))
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(body)


def _fixture_repo(tmp: str, v1: dict[str, str], v2: dict[str, str],
                  commit_v2: bool = True) -> tuple[str, str]:
    """A real two-commit git repository -> (root, anchor sha).

    A fixture that stubbed the history would test the checker against the shape of its own
    assumptions rather than against git, and the one thing this gate must get right is reading a
    file's PREVIOUS COMMITTED CONTENT. So the fixture commits v1, commits v2 on top, and the arms
    run in the DEFAULT anchored mode -- the same code path CI executes, including the per-commit
    walk and the working-tree step. An arm that exercised only the explicit-delta path would leave
    the mode CI actually runs unproven.
    """
    import shutil

    root = os.path.join(tmp, "repo")
    shutil.rmtree(root, ignore_errors=True)
    os.makedirs(root, exist_ok=True)
    for args in (
        ("init", "--quiet", "-b", "main"),
        ("config", "user.email", "gate@example.invalid"),
        ("config", "user.name", "gate"),
        ("config", "commit.gpgsign", "false"),
        ("config", "core.autocrlf", "false"),
    ):
        subprocess.run(("git", "-C", root) + args, capture_output=True, text=True, check=False)
    _write(root, "src/Example.Package/Example.Package.csproj", _CSPROJ)
    for rel, body in v1.items():
        _write(root, rel, body)
    subprocess.run(("git", "-C", root, "add", "-A"), capture_output=True, check=False)
    subprocess.run(("git", "-C", root, "commit", "--quiet", "-m", "v1"),
                   capture_output=True, check=False)
    anchor = subprocess.run(("git", "-C", root, "rev-parse", "HEAD"),
                            capture_output=True, text=True, check=False).stdout.strip()
    for rel in set(v1) - set(v2):
        os.remove(os.path.join(root, rel.replace("/", os.sep)))
    for rel, body in v2.items():
        _write(root, rel, body)
    if commit_v2:
        subprocess.run(("git", "-C", root, "add", "-A"), capture_output=True, check=False)
        # An identical v2 leaves nothing to commit and git declines -- which is exactly the empty
        # population the REFUSE arm needs, produced by the fixture's own mechanics rather than by a
        # special case in the harness.
        subprocess.run(("git", "-C", root, "commit", "--quiet", "-m", "v2"),
                       capture_output=True, check=False)
    return root, anchor


def self_test() -> int:
    import io
    import shutil
    import tempfile

    sql = "src/Example.Package/Scripts/001_Create.sql"
    migration = "src/Example.Package/Scripts/002_AddTenant.sql"
    cs = "src/Example.Package/Store.cs"
    verify = "src/Example.Package/Verifier.cs"

    # (label, v1, v2, baseline entries, expected exit, commit v2?)
    arms: list[tuple[str, dict[str, str], dict[str, str], set[str], int, bool]] = [
        ("SAFETY   in-place column add with no ALTER is detected",
         {sql: _V1_SQL}, {sql: _V2_SQL_ADDS_COLUMN}, set(), 1, True),
        ("SAFETY   the same defect in C#-authored DDL is detected",
         {cs: _V1_CS}, {cs: _V2_CS_ADDS_COLUMN_AND_KEY}, set(), 1, True),
        ("SAFETY   existence-only schema verification is detected",
         {sql: _V1_SQL, verify: _VERIFY_EXISTENCE_ONLY_CS},
         {sql: _V1_SQL, verify: _VERIFY_EXISTENCE_ONLY_CS}, set(), 1, True),
        # The population must be NON-empty for this arm to mean anything: a stale claim over a
        # window that measured nothing is unfounded, and the checker refuses it before it is made.
        # So the fixture makes a real, evaluated, finding-free change (a comment-only edit) and
        # carries a baseline entry nothing reproduces.
        ("SAFETY   a stale baseline entry is detected",
         {sql: _V1_SQL}, {sql: _V2_SQL_COMMENT_ONLY},
         {"Example.Package:example_table:tenant_id"}, 1, True),
        ("LIVENESS column add that DOES ship an ALTER is allowed",
         {sql: _V1_SQL}, {sql: _V2_SQL_ADDS_COLUMN, migration: _MIGRATION_SQL}, set(), 0, True),
        ("LIVENESS a brand-new script is allowed",
         {}, {sql: _V2_SQL_ADDS_COLUMN}, set(), 0, True),
        ("LIVENESS a comment-only edit is allowed",
         {sql: _V1_SQL}, {sql: _V2_SQL_COMMENT_ONLY}, set(), 0, True),
        ("LIVENESS a SET-option edit is allowed",
         {sql: _V1_SQL}, {sql: _V2_SQL_SET_OPTIONS}, set(), 0, True),
        ("LIVENESS a declared provisioning-only change is allowed",
         {sql: _V1_SQL}, {sql: _V2_SQL_DECLARED}, set(), 0, True),
        ("LIVENESS a column-level verification is allowed",
         {sql: _V1_SQL, verify: _VERIFY_COLUMN_LEVEL_CS},
         {sql: _V1_SQL, verify: _VERIFY_COLUMN_LEVEL_CS}, set(), 0, True),
        # A NON-EMPTY population is what makes this arm mean anything: the sql carries a real,
        # evaluated, finding-free edit, so a 0 here says "the migrator was seen and allowed" rather
        # than "nothing was looked at". Widen the second question to any catalogue probe and this
        # arm goes to 1.
        ("LIVENESS a migrator probing existence to decide is allowed",
         {sql: _V1_SQL, verify: _MIGRATOR_EXISTENCE_PROBE_CS},
         {sql: _V2_SQL_COMMENT_ONLY, verify: _MIGRATOR_EXISTENCE_PROBE_CS}, set(), 0, True),
        ("LIVENESS a baselined gap does not fail",
         {sql: _V1_SQL}, {sql: _V2_SQL_ADDS_COLUMN},
         {"Example.Package:example_table:TENANT_ID"}, 0, True),
        # The table-level constraint limb. A key is part of the shape a consumer provisioned, so a
        # change to it is exactly as unreachable by a re-run CREATE as a new column is, and the
        # three arms below hold the gate to that in both directions.
        ("SAFETY   a widened key with no ALTER is detected",
         {sql: _V1_SQL_CONSTRAINT}, {sql: _V2_SQL_WIDENS_CONSTRAINT}, set(), 1, True),
        ("SAFETY   an added key sharing a leading keyword is detected",
         {sql: _V1_SQL_CONSTRAINT}, {sql: _V2_SQL_ADDS_SECOND_CONSTRAINT}, set(), 1, True),
        ("LIVENESS a widened key that DOES ship its rebuild is allowed",
         {sql: _V1_SQL_CONSTRAINT},
         {sql: _V2_SQL_WIDENS_CONSTRAINT, migration: _CONSTRAINT_MIGRATION_SQL}, set(), 0, True),
        # The WORKING-TREE step, which is the path that matters most in practice: the person about
        # to introduce this defect has not committed yet, and a gate that only reads committed
        # history cannot stop them -- it can only report the break after it has already landed.
        # Every arm above commits v2, so without these two the step is exercised only over an empty
        # change set and its ability to find anything at all is unproven.
        ("SAFETY   an UNCOMMITTED in-place column add is detected",
         {sql: _V1_SQL}, {sql: _V2_SQL_ADDS_COLUMN}, set(), 1, False),
        ("LIVENESS an UNCOMMITTED add WITH its migration is allowed",
         {sql: _V1_SQL}, {sql: _V2_SQL_ADDS_COLUMN, migration: _MIGRATION_SQL}, set(), 0, False),
        ("REFUSE   an unparseable script is NOT a pass",
         {sql: _V1_SQL}, {sql: _UNPARSEABLE_SQL}, set(), 2, True),
        # The liveness half of the refusal mechanism. Without it the refuse-baseline is unproven:
        # a checker that ignored the baseline and refused unconditionally would pass the arm above
        # forever, and the gate could never be adopted on a tree that has one genuinely
        # unresolvable artifact. The key is asserted verbatim, which is also what proves the key is
        # STABLE -- one built from the revision it was read at would not match this line.
        ("LIVENESS a RECORDED refusal is allowed (and still announced)",
         {sql: _V1_SQL}, {sql: _UNPARSEABLE_SQL},
         {"refuse:src/Example.Package/Scripts/001_Create.sql:unparseable CREATE TABLE"}, 0, True),
        ("REFUSE   an empty population is NOT a pass",
         {sql: _V1_SQL}, {sql: _V1_SQL}, set(), 2, True),
    ]

    print("self-test: proving this gate can report FAIL, PASS and REFUSE")
    failures = 0
    tmp = tempfile.mkdtemp()
    saved = {k: os.environ.get(k) for k in ("DDL_MIGRATION_REPO", "DDL_MIGRATION_BASELINE")}
    try:
        for label, v1, v2, baseline_entries, want, commit_v2 in arms:
            root, anchor = _fixture_repo(tmp, v1, v2, commit_v2=commit_v2)
            fixture_baseline = os.path.join(tmp, "fixture.baseline.txt")
            with open(fixture_baseline, "w", encoding="utf-8") as fh:
                # A FIXTURE baseline, never the real one. Pointed at the real file, every arm would
                # report its real entries as 'stale' and produce right-looking verdicts computed
                # over the wrong subject.
                fh.write("# fixture\n")
                fh.write(f"# anchor: {anchor}\n")
                for entry in sorted(baseline_entries):
                    fh.write(entry + "\n")
            os.environ["DDL_MIGRATION_REPO"] = root
            os.environ["DDL_MIGRATION_BASELINE"] = fixture_baseline
            sink = io.StringIO()
            got = scan(out=sink, err=sink)
            if got == want:
                print(f"  ok    {label:<58} exit {got}")
            else:
                print(f"  FAIL  {label:<58} exit {got} (expected {want})")
                print("        " + sink.getvalue().replace("\n", "\n        ")[:1400])
                failures += 1
    finally:
        for key, value in saved.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value
        shutil.rmtree(tmp, ignore_errors=True)

    # The explicit-delta mode gets its own arms. Every arm above runs the anchored default, so
    # without these the --base path -- the form used to hold this gate against a known historical
    # commit -- would ship unproven, and a documented mode nobody exercises is a mode that has
    # quietly stopped working.
    tmp2 = tempfile.mkdtemp()
    saved2 = {k: os.environ.get(k) for k in ("DDL_MIGRATION_REPO", "DDL_MIGRATION_BASELINE")}
    try:
        explicit = [
            ("SAFETY   --base delta: column add with no ALTER is detected",
             {sql: _V1_SQL}, {sql: _V2_SQL_ADDS_COLUMN}, 1),
            ("LIVENESS --base delta: the same add WITH its migration is allowed",
             {sql: _V1_SQL}, {sql: _V2_SQL_ADDS_COLUMN, migration: _MIGRATION_SQL}, 0),
        ]
        for label, v1, v2, want in explicit:
            root, anchor = _fixture_repo(tmp2, v1, v2)
            fixture_baseline = os.path.join(tmp2, "fixture.baseline.txt")
            with open(fixture_baseline, "w", encoding="utf-8") as fh:
                fh.write("# fixture\n")
            os.environ["DDL_MIGRATION_REPO"] = root
            os.environ["DDL_MIGRATION_BASELINE"] = fixture_baseline
            sink = io.StringIO()
            got = scan(out=sink, err=sink, base=anchor, head="HEAD")
            if got == want:
                print(f"  ok    {label:<58} exit {got}")
            else:
                print(f"  FAIL  {label:<58} exit {got} (expected {want})")
                print("        " + sink.getvalue().replace("\n", "\n        ")[:1400])
                failures += 1
    finally:
        for key, value in saved2.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value
        shutil.rmtree(tmp2, ignore_errors=True)

    print("---")
    if failures == 0:
        print("self-test PASS - gate is non-vacuous in both directions.")
        return 0
    print(f"::error:: self-test FAILED ({failures} arm(s)). This gate cannot be trusted.",
          file=sys.stderr)
    return 1


def main(argv: list[str]) -> int:
    if argv and argv[0] == "--self-test":
        return self_test()
    base = head = None
    i = 0
    while i < len(argv):
        if argv[i] == "--base" and i + 1 < len(argv):
            base = argv[i + 1]
            i += 2
            continue
        if argv[i] == "--head" and i + 1 < len(argv):
            head = argv[i + 1]
            i += 2
            continue
        print(f"usage: {os.path.basename(__file__)} [--base REV [--head REV]] | [--self-test]",
              file=sys.stderr)
        return 2
    if head is not None and base is None:
        print("--head requires --base: a head with no base is not a delta.", file=sys.stderr)
        return 2
    return scan(base=base, head=head)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
