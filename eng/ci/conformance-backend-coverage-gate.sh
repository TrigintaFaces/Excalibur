#!/usr/bin/env bash
# conformance-backend-coverage-gate.sh — a shipped store backend that no conformance suite verifies.
# This gate detects a NEW one.
#
# THE DEFECT CLASS:
#
#   Excalibur.Testing.Conformance publishes a kit per store contract. Each kit is the definition of
#   conformance we impose on any consumer who writes their own provider for that contract. When one of
#   OUR backends implements the contract and no suite derives that kit against it, three things are true
#   at once:
#
#     - the backend ships verified by nothing that speaks the published contract;
#     - the arms we require of a consumer are arms we do not run ourselves against that backend;
#     - and a defect in what the contract requires can sit in that backend indefinitely, because the
#       one artifact that would have caught it was never pointed at it.
#
#   It hides because nothing is red, and because the OBVIOUS census reports clean. The sibling census
#   (conformance-arm-census.sh) measures, per deriver, whether every arm of a kit is wired. That is a
#   different question, and it is answered only over the derivers that EXIST. A kit derived solely by an
#   in-memory store scores perfect arm coverage while every durable backend for that contract is
#   unverified. Perfect arm coverage over one deriver, and no deriver at all for eight backends, produce
#   the same green.
#
# WHAT THIS GATE ASSERTS: for each contract in the table below, every CONCRETE type under src/ whose
#   base list names that contract is bound by at least one suite that derives — transitively — the kit
#   published for that contract, or is a named, enumerated entry in the baseline.
#
# WHY IT BINDS BY TYPE NAME, AND WHAT COUNTS AS A BINDING:
#
#   A census keyed to filename or package cannot tell two backends apart, so a deriver could be credited
#   for an implementation it never touched. Binding on the named type is what makes "covered" mean the
#   thing that is actually covered. A suite binds backend N when its file contains any of:
#
#       new N(   new N{   new N<        direct construction
#       typeof(N)                       type reference
#       AddN(    AddN<                  a registration extension named for the backend
#       M(       M<                     a registration entry point RESOLVED from source (see below)
#
#   Every one is a whole-identifier match. `new NOptions(` does NOT bind N, and `AddOtherStore(` does not
#   bind N — that precision is what stops one backend's suite counting for its neighbour.
#
#   THE RESOLVED ENTRY POINT, AND WHY IT IS NOT A NAMING CONVENTION:
#
#   Several suites deliberately construct nothing, because a hand-built store is not the object a consumer
#   receives — they wire the backend through its own published registration and let the kit resolve it.
#   That registration is not always spelled `AddN`. In this tree one family ships no `Add<Backend>` at all
#   and is reachable only through a composed builder, and the builder call for one of its members differs
#   from its type name in casing, so any rule of the form "strip the vendor prefix" is wrong on first
#   contact.
#
#   So the entry point is RESOLVED, not guessed: a static method declared in the SAME package that
#   declares backend N, and whose own body names N, is recorded as an entry point for N. A suite calling
#   that method binds N. The map is read out of the source, so it stays correct when a family is renamed
#   and cannot be fooled by a convention that never held. The same-package restriction is what keeps it
#   honest — a general composition root in another package that happens to mention a default backend is
#   not that backend's entry point. And a method resolving to more than one backend OF THE SAME CONTRACT
#   is ambiguous and binds neither, because a binding that cannot say which backend it covered is exactly
#   the credited-for-another defect this gate exists to prevent.
#
#   A gate that recognised only construction would report a whole family as uncovered on the day it was
#   covered — and the obvious response to a false failure is to baseline the entry, which is how a gate
#   ends up certifying the very gap it exists to find.
#
# WHY DERIVATION IS TRANSITIVE (do not "simplify" this to a one-hop check):
#
#   A suite may reach a kit through an intermediate base — a shared fixture layer, a per-family
#   specialisation. A one-hop check sees the intermediate, not the kit, and reports a covered backend as
#   uncovered. The closure is computed over declarations under tests/ and inside the shipped package
#   together, so a kit's own self-test counts as a deriver exactly like an integration suite does.
#
# WHY ABSTRACT TYPES ARE NOT COUNTED AS BACKENDS:
#
#   An abstract type cannot be instantiated, so it is not something a consumer ever receives; its
#   concrete descendants are, and those are counted. Counting abstract bases would seed the ledger with
#   decorator base classes posing as shippable backends.
#
# WHY FILE DISCOVERY USES find/os.walk AND NOT git grep:
#
#   git grep is blind to a file that is not yet staged, so a brand-new suite added in the same change
#   that closes a gap reads as absent. The gate would then fail the very change that fixes it, and the
#   author's natural response is to baseline the entry. Walking the working tree is what the question
#   "is this backend verified?" is actually asking about.
#
# WHY A BASELINE, WHEN A SUPPRESSION CAP WOULD BE FALSE SAFETY (read before removing it):
#
#   A cap mutes an UNKNOWN quantity — it lets a gate absorb a defect it never named, which is the
#   false-safety class gates exist to remove. An enumerated baseline is the opposite: every exception is
#   written down by name, it cannot absorb anything it does not already list, and a newly-unverified
#   backend is a FAIL on its first appearance. This tree carries a large number of unverified backends
#   today; without the enumeration this gate would be red on arrival, could not be wired, and would
#   therefore detect nothing at all. An inert gate is not a stricter gate.
#
#   The ratchet: an entry that no longer names a live uncovered backend — because it became covered, or
#   because the type was renamed, deleted, or stopped declaring the contract — is a FAIL, not a silent
#   pass. So the list can only shrink, and cannot re-admit a gap under a recycled name.
#
# WHAT THIS GATE DOES NOT ASSERT (stated so a green is not over-read):
#
#   THE TOTAL IS A SURVEY OF THE TABLE, NOT OF THE TREE. Every count this gate prints -- including the
#   "N backend(s) examined" total -- is bounded by the contract->kit table below. A contract absent from
#   that table contributes nothing to any number here, and its backends are not reported uncovered: they
#   are not reported at all. Read the total as "backends of the contracts we listed", never as "backends".
#
#   That bound is not a preference, it is a precondition: the table pairs a contract with its PUBLISHED
#   kit and this gate refuses when a named kit is missing. So a contract for which NO kit ships cannot be
#   added, and the gate is structurally incapable of naming it. ICloudNativeOutboxStore -- a standalone
#   cloud-native outbox contract that does not extend the flat outbox contract and shares no member with
#   it -- was exactly this case until CloudNativeOutboxStoreConformanceTestKit shipped; it is now in the
#   table below. The general limit remains real for the next contract that ships with no kit: this gate
#   cannot name a gap whose kit was never published, and no census change conjures one.
#
#   This is the same defect class recorded in the baseline file's own history, where a census matched a
#   contract by name and could not see the backends that refined it before implementing it. That one was
#   fixed by computing the interface closure. A missing-kit contract cannot be fixed inside the gate at
#   all -- the fix is publishing the kit, as above -- so the total is written here as a survey of the
#   table, not of the tree, rather than left to be rediscovered.
#
#   That the deriving suite is non-vacuous. A suite may derive the kit and wire few of its arms, or wire
#   arms that early-return on a declined capability. Whether each arm is wired is the sibling census's
#   question. This gate proves a deriver EXISTS and names the backend; it does not prove that deriver's
#   arms can fail. Those are different properties, and only the first is decidable from the source tree.
#
#   And one residual over-credit, stated rather than papered over: a resolved entry point is matched by
#   NAME at the call site, and several families spell their builder call the same way in different
#   packages (one vendor name is shared by that vendor's event-store, inbox, leader-election and saga
#   registrations). Those resolve to different backends, and the ambiguity rule separates them per
#   contract — but it cannot tell which package's method a suite actually called. So a suite deriving one
#   contract's kit while configuring a DIFFERENT contract's backend from the same vendor would be
#   credited. Nothing in the tree does that, and a suite that configures a store it never exercises would
#   be odd on its own terms; it is recorded here because a limit that is written down can be argued with,
#   and one that is not gets inherited as precision the instrument never had.
#
# EXIT CODES (every one mapped by the caller; a non-0/1 is NEVER a pass):
#   0  PASS    scanned, backends found, every one covered or baselined; no stale baseline entry
#   1  FAIL    an unbaselined backend has no deriver, or a baseline entry is stale
#   2  REFUSE  could not evaluate (no src / no tests tree / a table kit missing / zero backends or zero
#              derivers seen == blind)
#   3  REFUSE  --self-test failed (the gate itself is broken or vacuous)
#   *  REFUSE  unknown arg == could-not-evaluate
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

SHIPPED_DIR="src/Excalibur/Excalibur.Testing.Conformance"
BASELINE_DEFAULT="$REPO_ROOT/eng/ci/conformance-backend-coverage-baseline.txt"

# ── The contract -> kit table ───────────────────────────────────────────────────────────────────────
# The pairing comes from a table and never from a naming convention. The published kit for a contract is
# not reliably "<contract minus the I> + ConformanceTestKit", and guessing it is how a family census
# reports two halves of one family as unrelated and calls the result clean. Each line is
# `IContract:KitTypeName`; both sides are verified to exist before any verdict is rendered, so a rename
# on either side REFUSES rather than silently passing.
CONTRACT_KITS_DEFAULT='ISagaStore:SagaStoreConformanceTestKit
IInboxStore:InboxStoreConformanceTestKit
IAuditStore:AuditStoreConformanceTestKit
ILeaderElection:LeaderElectionConformanceTestKit
IOutboxStore:OutboxStoreConformanceTestKit
IEventStore:EventStoreConformanceTestKit
ICloudNativeOutboxStore:CloudNativeOutboxStoreConformanceTestKit
ISnapshotStore:SnapshotStoreConformanceTestKit'

# ── Census ──────────────────────────────────────────────────────────────────────────────────────────
# One python pass over src/ and tests/. It emits tab-separated records and renders no verdict; the shell
# decides, so the exit-code contract stays in one readable place.
#
#   KITMISS <TAB> <kit>                     a kit named in the table is not declared in the package
#   IMPL    <TAB> <contract> <TAB> <name>   a concrete src/ type whose base list names the contract
#   COV     <TAB> <contract> <TAB> <name>   that type is bound by a suite deriving the paired kit
#   DER     <TAB> <kit>      <TAB> <name>   a concrete type reaching the kit (the positive control)
census() {
	local root="$1" table="$2"
	python3 - "$root" "$SHIPPED_DIR" "$table" <<'PYEOF'
import os, re, sys

# LF only. Text-mode stdout translates to CRLF on Windows, and a trailing CR makes every emitted name
# unmatchable by a whole-line compare downstream while the output still reads correctly by eye. That
# failure mode reports "nothing is covered" with total confidence, so it is fixed at the source here and
# defended again in the shell.
sys.stdout.reconfigure(newline="\n")

root, shipped_rel, table_raw = sys.argv[1], sys.argv[2], sys.argv[3]

pairs = []
for line in table_raw.splitlines():
    line = line.strip()
    if not line or line.startswith("#"):
        continue
    contract, _, kit = line.partition(":")
    if contract.strip() and kit.strip():
        pairs.append((contract.strip(), kit.strip()))

# class/record/struct NAME [<generics>] [(primary ctor)] : BASES {   |   ... where
#
# re.S so the base list may WRAP onto a later line. C# permits that and several provider families here
# do exactly it; a line-oriented scan reports those types as declaring nothing, which reads as clean.
# That is not a tuning preference — it is the difference between seeing a family and not seeing it.
#
# The primary-constructor group is optional, which makes this a strict superset of the naive pattern: a
# C# 12 primary constructor puts a parameter list between the identifier and the colon, and a pattern
# that requires the colon to follow the name is structurally blind to every such type.
DECL = re.compile(
    r'((?:public|internal|private|protected|sealed|abstract|partial|static|file|record)\s+)*'
    r'\b(?:class|record|struct)\s+(\w+)\s*(?:<[^>{]*>)?\s*(?:\([^)]*\))?\s*'
    r':\s*([^{;]*?)\s*(?:\{|where\s)',
    re.S)

# interface NAME [<generics>] : BASES {   |   ... where
#
# WHY A BACKEND IS MATCHED THROUGH THE INTERFACE CLOSURE AND NOT BY NAME:
#
#   A contract is frequently REFINED before it is implemented -- a fenced variant, a multi-transport
#   variant, a health-based variant -- and a backend then declares only the refinement. It IS an
#   implementor of the contract (the compiler says so; a consumer resolving the contract receives it),
#   but its base list never spells the contract's name. A gate matching the name literally cannot see
#   that backend AT ALL: it is not reported uncovered, it is absent from the census, and the totals it
#   is missing from read as a complete survey.
#
#   That is the failure this gate exists to prevent, turned on the gate itself -- certifying a gap it
#   is structurally incapable of seeing. It is also invisible in the output by construction: the
#   per-contract line says "examined 5" whether five is the population or a third of it.
#
#   So the closure is computed, exactly as it already is for kit derivation on the test side. A
#   concrete type declares contract C when any name in its base list IS C or REACHES C through
#   interface inheritance.
IFACE = re.compile(
    r'\binterface\s+(\w+)\s*(?:<[^>{]*>)?\s*'
    r':\s*([^{;]*?)\s*(?:\{|where\s)',
    re.S)


def strip_comments(text):
    """Comments first, so a type named only in documentation is never read as a declaration."""
    text = re.sub(r'/\*.*?\*/', ' ', text, flags=re.S)
    return re.sub(r'//[^\n]*', ' ', text)


def base_names(bases):
    """Leading identifier of each entry in a base list, generic arguments discarded."""
    out, depth, cur = [], 0, []
    for ch in bases:
        if ch == '<':
            depth += 1
        elif ch == '>':
            depth = max(0, depth - 1)
        elif ch == ',' and depth == 0:
            out.append(''.join(cur))
            cur = []
            continue
        cur.append(ch)
    out.append(''.join(cur))
    names = []
    for entry in out:
        m = re.search(r'([A-Za-z_]\w*)', entry.split('<')[0])
        if m:
            names.append(m.group(1))
    return names


def is_abstract(match):
    return re.search(r'\babstract\b', match.group(0)[:match.start(2) - match.start(0)]) is not None


def walk_cs(top):
    for dirpath, dirnames, filenames in os.walk(top):
        dirnames[:] = [d for d in dirnames if d not in ('obj', 'bin')]
        for f in filenames:
            if f.endswith('.cs'):
                yield os.path.join(dirpath, f)


def read(path):
    try:
        with open(path, encoding='utf-8', errors='replace') as fh:
            return fh.read()
    except OSError:
        return None


src_dir = os.path.join(root, 'src')
tests_dir = os.path.join(root, 'tests')
shipped_dir = os.path.join(root, *shipped_rel.split('/'))

# ---- src/ side: the concrete backends of each contract ---------------------------------------------
contracts = {c for c, _ in pairs}
impls = {c: set() for c in contracts}
backend_pkg = {}   # backend type name -> the package directory that declares it


def package_root(path, stop_at):
    """Nearest ancestor directory holding a project file. That is the package boundary."""
    d = os.path.dirname(os.path.abspath(path))
    stop = os.path.abspath(stop_at)
    while True:
        try:
            if any(f.endswith('.csproj') for f in os.listdir(d)):
                return d
        except OSError:
            pass
        parent = os.path.dirname(d)
        if parent == d or not d.startswith(stop):
            return None
        d = parent


iface_parents = {}   # interface name -> set of base interface names
candidates = []      # (concrete type name, its base names, declaring path)

for path in walk_cs(src_dir):
    text = read(path)
    if text is None:
        continue
    text = strip_comments(text)
    for m in IFACE.finditer(text):
        iface_parents.setdefault(m.group(1), set()).update(base_names(m.group(2)))
    for m in DECL.finditer(text):
        if is_abstract(m):
            continue
        candidates.append((m.group(2), set(base_names(m.group(3))), path))

_iface_cache = {}


def iface_reaches(name, target, guard=frozenset()):
    """Does interface `name` reach `target` through its own base list? Transitive and cycle-safe."""
    key = (name, target)
    if key in _iface_cache:
        return _iface_cache[key]
    if name in guard:
        return False
    inner = guard | {name}
    result = any(base == target or iface_reaches(base, target, inner)
                 for base in iface_parents.get(name, ()))
    _iface_cache[key] = result
    return result


for name, names, path in candidates:
    for c in contracts:
        if c in names or any(iface_reaches(n, c) for n in names):
            impls[c].add(name)
            backend_pkg.setdefault(name, package_root(path, src_dir))

# ---- src/ side: registration entry points, RESOLVED from source ------------------------------------
# A static method declared in the same package as backend N, whose body names N, is an entry point for
# N. Only packages that actually declare a backend are scanned, so this costs a handful of files.
METHOD = re.compile(r'\bstatic\b[^;{}]{0,240}?\b(\w+)\s*(?:<[^>(){}]*>)?\s*\(')


def method_bodies(text):
    """Yield (name, body) for each static method declaration. Comments are already stripped."""
    for m in METHOD.finditer(text):
        i, depth = m.end() - 1, 0
        while i < len(text):                      # walk the parameter list to its close
            if text[i] == '(':
                depth += 1
            elif text[i] == ')':
                depth -= 1
                if depth == 0:
                    break
            i += 1
        else:
            continue
        j = i + 1
        while j < len(text) and text[j] not in '{;=':
            j += 1                                # skip a where-clause / return annotation
        if j >= len(text):
            continue
        if text[j] == '{':                        # block body: brace-match it
            depth, k = 0, j
            while k < len(text):
                if text[k] == '{':
                    depth += 1
                elif text[k] == '}':
                    depth -= 1
                    if depth == 0:
                        break
                k += 1
            yield m.group(1), text[j:k]
        elif text.startswith('=>', j):            # expression body: up to its terminating ;
            k = text.find(';', j)
            yield m.group(1), text[j:k if k != -1 else len(text)]


entry_points = {}   # method name -> set of backend type names it registers
for pkg in {p for p in backend_pkg.values() if p}:
    local_backends = [b for b, p in backend_pkg.items() if p == pkg]
    bodies = {}                                   # method name -> its body text (package-local)
    for path in walk_cs(pkg):
        text = read(path)
        if text is None:
            continue
        for name, body in method_bodies(strip_comments(text)):
            bodies[name] = bodies.get(name, '') + '\n' + body

    # The public entry point usually delegates: the method a consumer calls is rarely the method that
    # performs the registration. So the naming is closed over package-local calls -- a method binds N if
    # its own body names N, or if it calls a package-local method that does. Confined to the package, so
    # a general composition root elsewhere never inherits a backend it merely defaults to.
    direct = {}
    for name, body in bodies.items():
        direct[name] = {b for b in local_backends
                        if re.search(r'(?<![A-Za-z0-9_])' + re.escape(b) + r'(?![A-Za-z0-9_])', body)}

    calls = {name: {c for c in re.findall(r'\b(\w+)\s*[(<]', body) if c in bodies and c != name}
             for name, body in bodies.items()}

    resolved = {}

    def bound(name, guard=frozenset()):
        if name in resolved:
            return resolved[name]
        if name in guard:
            return set()
        acc = set(direct.get(name, ()))
        inner = guard | {name}
        for callee in calls.get(name, ()):
            acc |= bound(callee, inner)
        if not (guard - {name}):                  # only memoise a fully-explored result
            resolved[name] = acc
        return acc

    for name in bodies:
        found = bound(name)
        if found:
            entry_points.setdefault(name, set()).update(found)

# ---- shipped package: do the table's kits still exist? ---------------------------------------------
shipped_types = set()
for path in walk_cs(shipped_dir):
    text = read(path)
    if text is None:
        continue
    for m in re.finditer(r'\b(?:class|record|struct)\s+(\w+)', strip_comments(text)):
        shipped_types.add(m.group(1))

for _, kit in pairs:
    if kit not in shipped_types:
        print("KITMISS\t%s" % kit)

# ---- test side: declaration index, then the derivation closure -------------------------------------
# The index spans tests/ AND the shipped package, so an intermediate base declared in either is followed
# and a kit's own self-test counts as a deriver.
parents = {}          # type name -> set of base names
concrete_files = {}   # concrete type name -> the file that declares it

for top in (tests_dir, shipped_dir):
    for path in walk_cs(top):
        text = read(path)
        if text is None:
            continue
        text = strip_comments(text)
        for m in DECL.finditer(text):
            name = m.group(2)
            parents.setdefault(name, set()).update(base_names(m.group(3)))
            if not is_abstract(m):
                concrete_files.setdefault(name, path)

_reach_cache = {}


def reaches(name, target, guard=frozenset()):
    """Does `name` reach `target` through its base chain? Transitive and cycle-safe."""
    key = (name, target)
    if key in _reach_cache:
        return _reach_cache[key]
    if name in guard:
        return False
    inner = guard | {name}
    result = False
    for base in parents.get(name, ()):
        if base == target or reaches(base, target, inner):
            result = True
            break
    _reach_cache[key] = result
    return result


_text_cache = {}


def deriver_text(path):
    if path not in _text_cache:
        t = read(path)
        _text_cache[path] = strip_comments(t) if t is not None else ''
    return _text_cache[path]


def binds(text, backend, entries):
    """Whole-identifier binding only. `new NOptions(` and `AddOtherStore(` must not match N."""
    n = re.escape(backend)
    alts = [r'\bnew\s+' + n + r'\s*[(<{]',
            r'\btypeof\s*\(\s*' + n + r'\s*\)',
            r'\bAdd' + n + r'\s*[(<]',
            # target-typed `new(...)`: invisible to `new N(` because the type name never appears at the
            # call site. Two shapes where N appears as the DECLARED type instead: a local/field
            # declaration (`N x = new(...)`) and an expression-bodied member returning N
            # (`N Foo(...) => new(...)`). Both require `new(` to follow within the same statement, so a
            # `;` between N and `new(` breaks the match rather than reaching across statements.
            r'\b' + n + r'\s*\??\s+\w+\s*=\s*new\s*[(<]',
            r'\b' + n + r'\s*\??\s+\w+\s*\([^;{}]*?\)\s*=>\s*new\s*[(<]']
    alts += [r'\b' + re.escape(e) + r'\s*[(<]' for e in entries]
    return re.search('|'.join(alts), text) is not None


for contract, kit in pairs:
    derivers = [(name, path) for name, path in concrete_files.items() if reaches(name, kit)]
    for name, _ in derivers:
        print("DER\t%s\t%s" % (kit, name))
    # An entry point resolving to more than one backend of THIS contract cannot say which one a caller
    # covered, so it binds neither -- that ambiguity is the credited-for-another defect in miniature.
    per_backend = {}
    for method, backends in entry_points.items():
        here = backends & impls[contract]
        if len(here) == 1:
            per_backend.setdefault(next(iter(here)), set()).add(method)
    for backend in sorted(impls[contract]):
        print("IMPL\t%s\t%s" % (contract, backend))
        entries = sorted(per_backend.get(backend, ()))
        for _, path in derivers:
            if binds(deriver_text(path), backend, entries):
                print("COV\t%s\t%s" % (contract, backend))
                break
PYEOF
}

# ── Evaluation ──────────────────────────────────────────────────────────────────────────────────────

run_gate() {
	local root="$1" baseline="$2" table="${3:-$CONTRACT_KITS_DEFAULT}"
	local src_dir="$root/src" tests_dir="$root/tests"

	if [ ! -d "$src_dir" ]; then
		echo "REFUSE: no src tree at $src_dir -- cannot evaluate." >&2
		return 2
	fi
	if [ ! -d "$tests_dir" ]; then
		echo "REFUSE: no tests tree at $tests_dir -- cannot evaluate." >&2
		return 2
	fi
	if [ ! -d "$root/$SHIPPED_DIR" ]; then
		echo "REFUSE: shipped conformance package not found at $SHIPPED_DIR -- cannot evaluate." >&2
		return 2
	fi

	local raw
	# tr -d is defence in depth against the CRLF trap the census already avoids at the source: a
	# reintroduced text-mode write would make every emitted name unmatchable and produce a confident,
	# total false FAIL.
	raw="$(census "$root" "$table" | tr -d '\r')" || {
		echo "REFUSE: the census failed to run -- cannot evaluate." >&2
		return 2
	}

	local missing
	missing="$(printf '%s\n' "$raw" | awk -F'\t' '$1=="KITMISS"{print $2}')"
	if [ -n "$missing" ]; then
		echo "REFUSE: these kits named in the contract table are not declared in $SHIPPED_DIR:" >&2
		printf '%s\n' "$missing" | sed 's/^/        /' >&2
		echo "        Either a kit was renamed or the package moved. A table that no longer names real" >&2
		echo "        kits cannot see its own subject, and its zero is blindness, not cleanliness." >&2
		return 2
	fi

	local total_impl total_der
	total_impl="$(printf '%s\n' "$raw" | awk -F'\t' '$1=="IMPL"' | wc -l | tr -d ' ')"
	total_der="$(printf '%s\n' "$raw" | awk -F'\t' '$1=="DER"{print $2"\t"$3}' | sort -u | wc -l | tr -d ' ')"

	if [ "$total_impl" -eq 0 ]; then
		echo "REFUSE: no concrete type under $src_dir declares any contract in the table. Either the" >&2
		echo "        contracts were renamed or the declaration match no longer discriminates; a zero" >&2
		echo "        here is blindness, not cleanliness." >&2
		return 2
	fi
	if [ "$total_der" -eq 0 ]; then
		echo "REFUSE: no suite resolves to any kit in the table -- the test-side scan found nothing and" >&2
		echo "        has therefore proven nothing. Reporting every backend uncovered off a blind scan" >&2
		echo "        would be a confident, total false FAIL." >&2
		return 2
	fi

	# Per-contract blindness floor. Every contract in the table is known to have implementors, so a zero
	# for one of them means the census stopped discriminating for that contract in particular — which a
	# whole-tree total would hide behind the contracts that still resolve.
	local c kit blind=""
	while IFS= read -r line; do
		[ -n "$line" ] || continue
		case "$line" in \#*) continue ;; esac
		c="${line%%:*}"
		if [ "$(printf '%s\n' "$raw" | awk -F'\t' -v c="$c" '$1=="IMPL" && $2==c' | wc -l | tr -d ' ')" -eq 0 ]; then
			blind="${blind}${c}"$'\n'
		fi
	done <<< "$table"
	if [ -n "$blind" ]; then
		echo "REFUSE: these contracts resolved zero concrete implementors -- the census is blind to them:" >&2
		printf '%s' "$blind" | sed 's/^/        /' >&2
		echo "        A contract with no implementors reports exactly the same clean as one fully covered." >&2
		return 2
	fi

	local baselined=""
	if [ -f "$baseline" ]; then
		baselined="$(tr -d '\r' < "$baseline" | sed 's/[[:space:]]*$//' | grep -vE '^[[:space:]]*(#|$)' || true)"
	fi

	local uncovered="" report="" rc=0
	while IFS= read -r line; do
		[ -n "$line" ] || continue
		case "$line" in \#*) continue ;; esac
		c="${line%%:*}"
		kit="${line##*:}"
		local impl cov unc n_impl n_cov n_unc n_der u
		impl="$(printf '%s\n' "$raw" | awk -F'\t' -v c="$c" '$1=="IMPL" && $2==c {print $3}' | sort -u | grep -v '^$' || true)"
		cov="$(printf '%s\n' "$raw" | awk -F'\t' -v c="$c" '$1=="COV" && $2==c {print $3}' | sort -u | grep -v '^$' || true)"
		unc="$(comm -23 <(printf '%s\n' "$impl" | grep -v '^$' || true) <(printf '%s\n' "$cov" | grep -v '^$' || true) || true)"
		n_impl="$(printf '%s\n' "$impl" | grep -c . || true)"
		n_cov="$(printf '%s\n' "$cov" | grep -c . || true)"
		n_unc="$(printf '%s\n' "$unc" | grep -c . || true)"
		n_der="$(printf '%s\n' "$raw" | awk -F'\t' -v k="$kit" '$1=="DER" && $2==k {print $3}' | sort -u | grep -c . || true)"
		report="${report}  ${c} -> ${kit}: examined ${n_impl}, covered ${n_cov}, uncovered ${n_unc} (kit derivers resolved: ${n_der})"$'\n'
		while IFS= read -r u; do
			[ -n "$u" ] || continue
			uncovered="${uncovered}${c}:${u}"$'\n'
		done <<< "$unc"
	done <<< "$table"

	local unbaselined="" b
	while IFS= read -r u; do
		[ -n "$u" ] || continue
		printf '%s\n' "$baselined" | grep -qxF "$u" || unbaselined="${unbaselined}${u}"$'\n'
	done <<< "$uncovered"

	# Shrink-only ratchet: a baseline entry that no longer names a live uncovered backend is stale.
	local stale=""
	if [ -n "$baselined" ]; then
		while IFS= read -r b; do
			[ -n "$b" ] || continue
			printf '%s\n' "$uncovered" | grep -qxF "$b" || stale="${stale}${b}"$'\n'
		done <<< "$baselined"
	fi

	if [ -n "$unbaselined" ]; then
		rc=1
		echo "FAIL: these backends implement a published contract, and no suite derives that contract's" >&2
		echo "      published kit against them. They ship verified by nothing that speaks the contract," >&2
		echo "      while the same arms are imposed on every consumer who writes their own provider." >&2
		echo >&2
		printf '%s' "$unbaselined" | sed 's/^/        /' >&2
		echo >&2
		echo "      Fix: add a suite deriving the paired kit whose file either constructs the named type" >&2
		echo "      (new X(...)) or calls that backend's own shipped registration extension (AddX(...))." >&2
		echo "      If it genuinely cannot be covered yet, add the exact Contract:TypeName line to" >&2
		echo "      $(basename "$baseline") WITH a reason -- that is a debt entry, not a dismissal." >&2
	fi

	if [ -n "$stale" ]; then
		rc=1
		echo "FAIL: stale baseline entries -- these no longer name a live uncovered backend and must be" >&2
		echo "      REMOVED. A baseline that keeps dead names can re-admit a gap under a recycled name." >&2
		echo "      (An entry goes stale when the backend becomes covered, is renamed, is deleted, or" >&2
		echo "      stops declaring the contract.)" >&2
		echo >&2
		printf '%s' "$stale" | sed 's/^/        /' >&2
	fi

	if [ "$rc" -eq 0 ]; then
		echo "PASS: every concrete backend of every contract in the table is bound by a suite deriving its"
		echo "      published kit, or is an enumerated baseline entry. No stale baseline entry."
	fi
	printf '%s' "$report"
	echo "  totals: ${total_impl} backend(s) examined, ${total_der} kit deriver(s) resolved, $(printf '%s' "$baselined" | grep -c . || true) baselined."
	return "$rc"
}

# ── Self-test ───────────────────────────────────────────────────────────────────────────────────────
# Proves the gate is non-vacuous. It must go RED on a planted uncovered backend and GREEN on a covered
# one. A gate whose green cannot be falsified is a success signal with nothing behind it; a gate that
# fails unconditionally satisfies every safety arm perfectly while detecting nothing at all.
#
# The fixture contract and kit are synthetic (Widget*), so nothing planted here names a real artifact.
self_test() {
	local tmp fails=0
	tmp="$(mktemp -d)"
	trap 'rm -rf "$tmp"' RETURN

	local TABLE='IWidgetStore:WidgetStoreConformanceTestKit'

	mk() { # mk <root> -- a minimal tree with the shipped kit present
		mkdir -p "$1/$SHIPPED_DIR/Conformance" "$1/src/Widget" "$1/tests/Widget.Tests"
		cat > "$1/$SHIPPED_DIR/Conformance/WidgetStoreConformanceTestKit.cs" <<'EOF'
public abstract class WidgetStoreConformanceTestKit { public virtual Task ArmA() => Task.CompletedTask; }
EOF
	}

	arm() { # arm <name> <expected-rc> <root> [baseline]
		local name="$1" want="$2" root="$3" got
		run_gate "$root" "${4:-/nonexistent-baseline}" "$TABLE" >/dev/null 2>&1
		got=$?
		if [ "$got" -ne "$want" ]; then
			echo "  self-test FAIL: $name -- expected exit $want, got $got" >&2
			fails=$((fails + 1))
		else
			echo "  self-test ok:   $name (exit $got)"
		fi
	}

	# ---- SAFETY 1: a planted uncovered backend must FAIL. -----------------------------------------
	# Its base list deliberately WRAPS onto a later line, so this arm also pins that the declaration
	# match is newline-insensitive; a line-oriented scan sees no backend here and reports clean.
	mk "$tmp/uncovered"
	cat > "$tmp/uncovered/src/Widget/SqlWidgetStore.cs" <<'EOF'
public sealed class SqlWidgetStore :
	IDisposable,
	IWidgetStore
{
}
EOF
	# A covered sibling keeps the test-side scan demonstrably able to resolve a deriver, so this arm is
	# exercising the uncovered backend and not tripping the blindness floor instead.
	cat > "$tmp/uncovered/src/Widget/MemoryWidgetStore.cs" <<'EOF'
public sealed class MemoryWidgetStore : IWidgetStore { }
EOF
	cat > "$tmp/uncovered/tests/Widget.Tests/MemoryWidgetStoreConformanceShould.cs" <<'EOF'
public sealed class MemoryWidgetStoreConformanceShould : WidgetStoreConformanceTestKit
{
	private readonly IWidgetStore _store = new MemoryWidgetStore();
}
EOF
	arm "planted uncovered backend FAILS" 1 "$tmp/uncovered"

	# ---- LIVENESS 2: the SAME tree, once that backend is covered, must PASS. -----------------------
	# Without this arm a gate that fails unconditionally — including one whose census matches nothing
	# and therefore calls everything uncovered — would satisfy arm 1 perfectly.
	cp -r "$tmp/uncovered" "$tmp/covered"
	cat > "$tmp/covered/tests/Widget.Tests/SqlWidgetStoreConformanceShould.cs" <<'EOF'
public sealed class SqlWidgetStoreConformanceShould : WidgetStoreConformanceTestKit
{
	private readonly IWidgetStore _store = new SqlWidgetStore();
}
EOF
	arm "the same backend, once covered, PASSES" 0 "$tmp/covered"

	# ---- LIVENESS 2b: a backend constructed ONLY via a C# 9 target-typed `new(...)` must still be SEEN.
	# The type name never appears at the call site, so `new N(` is structurally blind to it -- the same
	# shape of miss as the primary-constructor arm below, one call form later. Two sub-shapes: a field
	# declared with the backend's type initialized by target-typed new, and an expression-bodied factory
	# method whose declared return type is the backend and whose body is target-typed new.
	cp -r "$tmp/uncovered" "$tmp/targetnew"
	cat > "$tmp/targetnew/tests/Widget.Tests/SqlWidgetStoreConformanceShould.cs" <<'EOF'
public sealed class SqlWidgetStoreConformanceShould : WidgetStoreConformanceTestKit
{
	private static SqlWidgetStore NewStore() => new();
	private readonly SqlWidgetStore _store = new();
}
EOF
	arm "a target-typed new(...) construction is SEEN (covered -> PASS)" 0 "$tmp/targetnew"

	# ---- SAFETY 3: the binding is by TYPE. A deriver constructing a DIFFERENT backend must not be
	# credited for this one — the trap that lets a suite report a pass for an implementation it never
	# exercised.
	cp -r "$tmp/uncovered" "$tmp/wrongtype"
	cat > "$tmp/wrongtype/tests/Widget.Tests/SqlWidgetStoreConformanceShould.cs" <<'EOF'
public sealed class SqlWidgetStoreConformanceShould : WidgetStoreConformanceTestKit
{
	private readonly IWidgetStore _store = new SomeOtherWidgetStore();
}
EOF
	arm "a deriver binding a DIFFERENT backend is not coverage" 1 "$tmp/wrongtype"

	# ---- SAFETY 3b: a near-miss identifier must not bind. `new SqlWidgetStoreOptions(...)` carries the
	# backend's name as a prefix and constructs something else entirely.
	cp -r "$tmp/uncovered" "$tmp/nearmiss"
	cat > "$tmp/nearmiss/tests/Widget.Tests/SqlWidgetStoreConformanceShould.cs" <<'EOF'
public sealed class SqlWidgetStoreConformanceShould : WidgetStoreConformanceTestKit
{
	private readonly object _options = new SqlWidgetStoreOptions();
}
EOF
	arm "a near-miss identifier (XOptions) is not coverage" 1 "$tmp/nearmiss"

	# ---- LIVENESS 4: registration-extension binding counts. A suite that resolves the backend from its
	# own shipped registration constructs nothing, and a construction-only gate calls it uncovered.
	cp -r "$tmp/uncovered" "$tmp/viadi"
	cat > "$tmp/viadi/tests/Widget.Tests/SqlWidgetStoreKitConformanceShould.cs" <<'EOF'
public sealed class SqlWidgetStoreKitConformanceShould : WidgetStoreConformanceTestKit
{
	protected override void ConfigureProvider(IServiceCollection services) =>
		services.AddSqlWidgetStore(options => options.ConnectionString = "x");
}
EOF
	arm "registration-extension binding (AddX) IS coverage" 0 "$tmp/viadi"

	# ---- LIVENESS 5: TRANSITIVE derivation counts. A one-hop check sees the intermediate, not the kit,
	# and reports this covered backend as uncovered — a false FAIL.
	cp -r "$tmp/uncovered" "$tmp/transitive"
	cat > "$tmp/transitive/tests/Widget.Tests/WidgetKitFixtureBase.cs" <<'EOF'
public abstract class WidgetKitFixtureBase : WidgetStoreConformanceTestKit { }
EOF
	cat > "$tmp/transitive/tests/Widget.Tests/SqlWidgetStoreConformanceShould.cs" <<'EOF'
public sealed class SqlWidgetStoreConformanceShould : WidgetKitFixtureBase
{
	private readonly IWidgetStore _store = new SqlWidgetStore();
}
EOF
	arm "TRANSITIVE derivation through an intermediate IS coverage" 0 "$tmp/transitive"

	# ---- LIVENESS 5b: a RESOLVED entry point counts, including through a package-local delegation.
	# The public method a consumer calls is rarely the method that performs the registration, so this
	# arm plants exactly that shape: UseWidgetSql delegates to a private helper, and only the helper
	# names the backend. Without the package-local closure this reads as uncovered.
	cp -r "$tmp/uncovered" "$tmp/entrypoint"
	: > "$tmp/entrypoint/src/Widget/Widget.csproj"
	cat > "$tmp/entrypoint/src/Widget/WidgetBuilderExtensions.cs" <<'EOF'
public static class WidgetBuilderExtensions
{
	public static IWidgetBuilder UseWidgetSql(this IWidgetBuilder builder, Action<object> configure)
	{
		RegisterStoreAndOptions(builder.Services);
		return builder;
	}

	private static void RegisterStoreAndOptions(IServiceCollection services) =>
		services.AddSingleton<IWidgetStore, SqlWidgetStore>();
}
EOF
	cat > "$tmp/entrypoint/tests/Widget.Tests/SqlWidgetStoreKitConformanceShould.cs" <<'EOF'
public sealed class SqlWidgetStoreKitConformanceShould : WidgetStoreConformanceTestKit
{
	protected override void ConfigureProvider(IServiceCollection services) =>
		services.AddWidgets(w => w.UseWidgetSql(sql => sql.ConnectionString = "x"));
}
EOF
	arm "RESOLVED entry point via package-local delegation IS coverage" 0 "$tmp/entrypoint"

	# ---- SAFETY 5c: an entry point that resolves to TWO backends of the SAME contract cannot say which
	# one a caller covered, so it must bind NEITHER. A binding that cannot name its backend is the
	# credited-for-another defect wearing a registration method as a costume.
	cp -r "$tmp/entrypoint" "$tmp/ambiguous"
	cat > "$tmp/ambiguous/src/Widget/WidgetBuilderExtensions.cs" <<'EOF'
public static class WidgetBuilderExtensions
{
	public static IWidgetBuilder UseWidgetSql(this IWidgetBuilder builder, Action<object> configure)
	{
		RegisterStoreAndOptions(builder.Services);
		return builder;
	}

	private static void RegisterStoreAndOptions(IServiceCollection services)
	{
		services.AddSingleton<IWidgetStore, SqlWidgetStore>();
		services.AddSingleton<IWidgetStore, MemoryWidgetStore>();
	}
}
EOF
	arm "an AMBIGUOUS entry point binds neither backend (FAILS)" 1 "$tmp/ambiguous"

	# ---- SAFETY 5d: an entry point declared OUTSIDE the backend's own package must not bind it. A
	# general composition root elsewhere that merely mentions a default backend is not that backend's
	# registration, and treating it as one would credit every suite that touches the composition root.
	cp -r "$tmp/uncovered" "$tmp/foreignpkg"
	mkdir -p "$tmp/foreignpkg/src/Widget" "$tmp/foreignpkg/src/Composition"
	: > "$tmp/foreignpkg/src/Widget/Widget.csproj"
	: > "$tmp/foreignpkg/src/Composition/Composition.csproj"
	cat > "$tmp/foreignpkg/src/Composition/CompositionRoot.cs" <<'EOF'
public static class CompositionRoot
{
	public static IServiceCollection AddEverything(this IServiceCollection services) =>
		services.AddSingleton<IWidgetStore, SqlWidgetStore>();
}
EOF
	cat > "$tmp/foreignpkg/tests/Widget.Tests/SqlWidgetStoreKitConformanceShould.cs" <<'EOF'
public sealed class SqlWidgetStoreKitConformanceShould : WidgetStoreConformanceTestKit
{
	protected override void ConfigureProvider(IServiceCollection services) => services.AddEverything();
}
EOF
	arm "an entry point in a FOREIGN package does not bind (FAILS)" 1 "$tmp/foreignpkg"

	# ---- SAFETY 6: a C# 12 primary-constructor backend must be SEEN. A pattern that requires the colon
	# to follow the identifier is structurally blind to these, and blindness reads as clean.
	mk "$tmp/primaryctor"
	cat > "$tmp/primaryctor/src/Widget/MemoryWidgetStore.cs" <<'EOF'
public sealed class MemoryWidgetStore : IWidgetStore { }
EOF
	cat > "$tmp/primaryctor/src/Widget/PrimaryCtorWidgetStore.cs" <<'EOF'
public sealed class PrimaryCtorWidgetStore(string connection, int timeout) : IWidgetStore { }
EOF
	cat > "$tmp/primaryctor/tests/Widget.Tests/MemoryWidgetStoreConformanceShould.cs" <<'EOF'
public sealed class MemoryWidgetStoreConformanceShould : WidgetStoreConformanceTestKit
{
	private readonly IWidgetStore _store = new MemoryWidgetStore();
}
EOF
	arm "a primary-constructor backend is SEEN (uncovered -> FAIL)" 1 "$tmp/primaryctor"

	# ---- SAFETY 6b: an ABSTRACT type declaring the contract is not a backend and must not be ledgered.
	mk "$tmp/abstractonly"
	cat > "$tmp/abstractonly/src/Widget/MemoryWidgetStore.cs" <<'EOF'
public sealed class MemoryWidgetStore : IWidgetStore { }
EOF
	cat > "$tmp/abstractonly/src/Widget/WidgetStoreDecorator.cs" <<'EOF'
public abstract class WidgetStoreDecorator : IWidgetStore { }
EOF
	cat > "$tmp/abstractonly/tests/Widget.Tests/MemoryWidgetStoreConformanceShould.cs" <<'EOF'
public sealed class MemoryWidgetStoreConformanceShould : WidgetStoreConformanceTestKit
{
	private readonly IWidgetStore _store = new MemoryWidgetStore();
}
EOF
	arm "an abstract implementor is not a backend (PASSES)" 0 "$tmp/abstractonly"

	# ---- SAFETY 6c: a backend declaring only a REFINEMENT of the contract must be SEEN. A gate matching
	# the contract name literally in the base list does not report this backend uncovered -- it never
	# reports it at all, and the total it is missing from reads as a complete survey. The refinement is
	# declared in a different file from the backend, as it is in the real tree.
	mk "$tmp/subiface"
	cat > "$tmp/subiface/src/Widget/IFencedWidgetStore.cs" <<'EOF'
public interface IFencedWidgetStore : IWidgetStore { }
EOF
	cat > "$tmp/subiface/src/Widget/MemoryWidgetStore.cs" <<'EOF'
public sealed class MemoryWidgetStore : IWidgetStore { }
EOF
	cat > "$tmp/subiface/src/Widget/FencedWidgetStore.cs" <<'EOF'
public sealed class FencedWidgetStore : IFencedWidgetStore, IDisposable { }
EOF
	cat > "$tmp/subiface/tests/Widget.Tests/MemoryWidgetStoreConformanceShould.cs" <<'EOF'
public sealed class MemoryWidgetStoreConformanceShould : WidgetStoreConformanceTestKit
{
	private readonly IWidgetStore _store = new MemoryWidgetStore();
}
EOF
	arm "a backend declaring only a SUB-INTERFACE is SEEN (uncovered -> FAIL)" 1 "$tmp/subiface"

	# ---- LIVENESS 6d: the SAME sub-interface backend, once covered, PASSES. Without this arm a closure
	# that over-matched every type would satisfy 6c perfectly while making the gate unwirable.
	cp -r "$tmp/subiface" "$tmp/subifacecov"
	cat > "$tmp/subifacecov/tests/Widget.Tests/FencedWidgetStoreConformanceShould.cs" <<'EOF'
public sealed class FencedWidgetStoreConformanceShould : WidgetStoreConformanceTestKit
{
	private readonly IWidgetStore _store = new FencedWidgetStore();
}
EOF
	arm "a sub-interface backend, once covered, PASSES" 0 "$tmp/subifacecov"

	# ---- LIVENESS 7: a baselined backend PASSES, or the gate is unwirable on a tree carrying known debt.
	printf 'IWidgetStore:SqlWidgetStore\n' > "$tmp/bl-ok.txt"
	arm "a baselined uncovered backend PASSES" 0 "$tmp/uncovered" "$tmp/bl-ok.txt"

	# ---- SAFETY 8: a baseline entry for a NOW-COVERED backend is stale and must FAIL (the ratchet).
	arm "stale baseline entry (backend now covered) FAILS" 1 "$tmp/covered" "$tmp/bl-ok.txt"

	# ---- SAFETY 9: a baseline entry naming a type that does not exist is stale and must FAIL.
	printf 'IWidgetStore:SqlWidgetStore\nIWidgetStore:GhostWidgetStore\n' > "$tmp/bl-ghost.txt"
	arm "stale baseline entry (type does not exist) FAILS" 1 "$tmp/uncovered" "$tmp/bl-ghost.txt"

	# ---- SAFETY 10: the baseline must not absorb a NEW gap. With one entry listed, a second uncovered
	# backend still FAILS — the property that distinguishes an enumeration from a suppression cap.
	cp -r "$tmp/uncovered" "$tmp/second"
	cat > "$tmp/second/src/Widget/OtherWidgetStore.cs" <<'EOF'
public sealed class OtherWidgetStore : IWidgetStore { }
EOF
	arm "a baseline does NOT absorb a new uncovered backend" 1 "$tmp/second" "$tmp/bl-ok.txt"

	# ---- REFUSE 11: no kit in the shipped package == the table names nothing real. Must not pass.
	cp -r "$tmp/covered" "$tmp/nokit"
	rm -f "$tmp/nokit/$SHIPPED_DIR/Conformance/WidgetStoreConformanceTestKit.cs"
	arm "a missing kit REFUSES (not a pass)" 2 "$tmp/nokit"

	# ---- REFUSE 12: zero backends == the census cannot see its own subject. Must not pass.
	mk "$tmp/nobackend"
	cat > "$tmp/nobackend/tests/Widget.Tests/Placeholder.cs" <<'EOF'
public sealed class Placeholder : WidgetStoreConformanceTestKit { }
EOF
	arm "zero backends REFUSES (not a pass)" 2 "$tmp/nobackend"

	# ---- REFUSE 13: backends present but ZERO derivers resolved == the test-side scan is blind.
	# Reporting every backend uncovered off a blind scan would be a confident, total false FAIL.
	mk "$tmp/noderiver"
	cat > "$tmp/noderiver/src/Widget/SqlWidgetStore.cs" <<'EOF'
public sealed class SqlWidgetStore : IWidgetStore { }
EOF
	arm "backends present but zero derivers REFUSES" 2 "$tmp/noderiver"

	# ---- REFUSE 14: missing tests tree == cannot evaluate.
	mkdir -p "$tmp/notests/src"
	arm "a missing tests tree REFUSES (not a pass)" 2 "$tmp/notests"

	if [ "$fails" -gt 0 ]; then
		echo "SELF-TEST FAIL: $fails arm(s) failed." >&2
		return 3
	fi
	echo "SELF-TEST PASS: the gate goes red on a planted uncovered backend (including one visible only"
	echo "                through a wrapped base list or a primary constructor), goes green when that"
	echo "                same backend is covered by construction, by registration extension, or"
	echo "                transitively; refuses a wrong-type and a near-miss binding; ignores abstract"
	echo "                implementors; honours a baseline without letting it absorb a new gap; fails on"
	echo "                a stale entry; and refuses rather than passing whenever it is blind."
	return 0
}

# ── Entry ───────────────────────────────────────────────────────────────────────────────────────────

case "${1:-}" in
	"")
		run_gate "$REPO_ROOT" "$BASELINE_DEFAULT"
		exit $?
		;;
	--self-test)
		self_test
		exit $?
		;;
	-h|--help)
		sed -n '2,120p' "${BASH_SOURCE[0]}"
		exit 0
		;;
	*)
		echo "REFUSE: unknown argument '${1}'. Usage: $(basename "$0") [--self-test]" >&2
		exit 2
		;;
esac
