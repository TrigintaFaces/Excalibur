#!/usr/bin/env python3
"""Census behind conformance-arm-skip-gate.sh. Emits tab-separated records; renders no verdict.

The shell caller owns the exit-code contract, so this program never decides anything: it reports
what it found and what it could not resolve, and an unresolvable subject is reported as such rather
than omitted. An omission would read to the caller exactly like a clean result.

Usage: conformance-arm-skip-census.py <repo-root> <kit-subdir>
"""

import os
import re
import sys
import json
import pathlib
import subprocess
import tempfile


def walk(directory):
    for base, dirs, files in os.walk(directory):
        dirs[:] = [d for d in dirs if d not in ("obj", "bin", ".git")]
        for name in files:
            if name.endswith(".cs"):
                yield os.path.join(base, name)


def read(path):
    with open(path, encoding="utf-8", errors="replace") as handle:
        return handle.read()


# A type declaration, with its base list. The optional parameter group is what makes this see a
# primary-constructor declaration: a pattern requiring the colon to follow the identifier cannot
# match one at all, and a census run with that shape reports a confident zero over every type
# declared that way. The base list is matched non-greedily up to the body so it may wrap lines.
DECL = re.compile(
    r"\b(?:class|record|struct|interface)\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?:<[^>{;]*>)?\s*"
    r"(?:\([^)]*\))?\s*(?::\s*([^{;]+?))?\s*(?=\{|;|\bwhere\b)",
    re.S,
)

# A kit reports a declined arm through its skip hook, naming the arm and either the capability it
# wanted or nothing at all.
# Invocation/base-list syntax is read by PowerShell's fingerprinted coherent Roslyn pair.
# This is a direct syntax census, not semantic inheritance or all-configuration proof.


def syntax_census(paths, kits):
    helper = pathlib.Path(__file__).with_name("conformance-arm-skip-syntax.ps1")
    with tempfile.TemporaryDirectory(prefix="conformance-syntax-") as directory:
        temporary = pathlib.Path(directory).resolve()
        if temporary.parent != pathlib.Path(tempfile.gettempdir()).resolve() or not temporary.name.startswith("conformance-syntax-"):
            raise ValueError("temporary request directory escaped its expected root")
        request = pathlib.Path(directory) / "request.json"
        request.write_text(json.dumps({"paths": paths, "kits": kits}), encoding="utf-8")
        result = subprocess.run(["pwsh", "-NoProfile", "-File", str(helper), "-RequestPath", str(request)],
                                capture_output=True, text=True, timeout=90,
                                cwd=pathlib.Path(__file__).resolve().parents[2])
        # The predicate is "the helper produced a complete payload", NOT "the helper stayed quiet".
        # stderr is not a verdict: a pwsh process that LoadFroms two assemblies and runs Add-Type has
        # several legitimate ways to emit a benign line, and any one of them used to refuse every arm
        # of this gate. Completeness is what matters and it is checked below -- roster identity, the
        # Roslyn pair, field types, candidate-count coherence. stderr is still reported on a real
        # failure, where it is the most useful thing we have.
        if result.returncode:
            raise ValueError("syntax helper failed: " + (result.stderr or result.stdout)[-1500:])
        payload = json.loads(result.stdout)
        files = payload["files"]
        by_path = {entry["Path"]: entry for entry in files}
        if len(files) != len(paths) or len(by_path) != len(paths) or set(by_path) != set(paths):
            raise ValueError("syntax helper input/output roster differs")
        # The Roslyn pair IS the toolchain identity -- it cannot be short without LoadFrom having
        # thrown first, so this half is self-enforcing. There is no SDK clause: no SDK is resolved.
        if len(payload["roslyn"]) != 2:
            raise ValueError("syntax toolchain identity is incomplete")
        for entry in files:
            if entry["CandidateCount"] < len(entry["Calls"]):
                raise ValueError("syntax candidate count is inconsistent")
            for field in ("Calls", "Derivations", "Refusals"):
                if not isinstance(entry[field], list):
                    raise ValueError("syntax result field is not a list: " + field)
        return payload, by_path

NEW = re.compile(r"\bnew\s+([A-Z][A-Za-z0-9_]*)\s*[(\{<]")

# THE SAME CONSTRUCTION, WITH THE TYPE ON THE OTHER SIDE OF THE `=`.
#
# A target-typed `new()` is the identical act -- the suite constructs the store -- written the way C#
# has preferred since it gained the form, and the reader above cannot see it because it looks to the
# RIGHT of `new` for a name that is, by construction, on the LEFT:
#
#     private readonly PropagationDelayingOutboxStore _store = new(Propagation);
#                      ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^ here                 ^^^^^ not here
#
# Left unread, such a suite resolves no store and the gate REFUSES over it -- a refusal that indicts
# a well-formed suite for using a language feature, which is the instrument's gap wearing the costume
# of a finding, exactly as the synchronous-factory gap below was.
#
# THIS CANNOT MANUFACTURE A PASS, and the reason is the filter rather than the pattern: a candidate
# becomes a store only if it names a DECLARED type whose closure contains the contract. A loose
# capture (`Lock _gate = new();`, `Dictionary<string, Entry> _x = new();`) fails that test and is
# discarded; a capture that survives it really is a store the suite constructs -- precisely as sound
# as `new PropagationDelayingOutboxStore(...)`, because it IS that, spelled differently. Unioned
# rather than used as a fallback, matching the stated intent that a suite constructing several
# stores is judged over each of them.
NEW_TARGET_TYPED = re.compile(
    r"\b([A-Z][A-Za-z0-9_]*)\s*(?:<[^;=()]*>)?\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*new\s*[(\{]")

# A suite may OBTAIN its store through the provider's own registration extension instead of
# constructing it -- services.AddOracleInboxStore(...) then a keyed resolve. That is not a lesser
# route: it is the one a consumer actually takes, and a suite exercising it tests more than one that
# news the type up. Binding still happens on a CONCRETE name, so the precision the `new` route buys
# is preserved: AddOracleInboxStore yields OracleInboxStore, which must itself be a declared type
# whose closure contains the contract, exactly as a constructed store must. A suite that registers a
# neighbour's provider is judged over that provider too, which is correct -- it really does use it.
ADDS = re.compile(r"\.Add([A-Z][A-Za-z0-9_]*)\s*[(<]")

# A provider reached through a COMPOSED BUILDER rather than a dedicated registration extension.
# `.AddSqlServerSagaStore(...)` works under ADDS above because the name minus "Add" IS the store's
# type name. Four providers ship no such extension -- their only public path is the composed builder,
# `AddExcalibur(x => x.AddSagas(saga => saga.UseCosmosDb(...)))` -- and for those, "Use" minus its verb
# yields a provider name and not a type. The store is the provider name joined to the contract's name
# without its leading I: UseCosmosDb + ISagaStore -> CosmosDbSagaStore.
#
# This CANNOT fabricate a derivation, which is what makes guessing a name acceptable here: every
# candidate still has to be a type declared in the tree whose interface closure contains the contract,
# and a composition that names nothing real is dropped by that filter exactly as any other miss is.
# So the rule can only ever ADD a derivation the tree already supports, never invent one.
USES = re.compile(r"\.Use([A-Z][A-Za-z0-9_]*)\s*[(<]")


def derivations(src, kit):
    """(total, private) count of declarations in one file whose base list names the kit.

    A file whose every derivation is on a PRIVATE nested class is a probe: a targeted fixture for
    the kit's own tests, whose store is a hand-built fake and which frequently resolves no store at
    all. It is not a provider claiming to cover the contract, and demanding a store of it would put
    a refusal in front of every run for a subject that does not exist.

    The test is deliberately a narrow NEGATIVE. The obvious positive form — "does this file declare
    a public top-level class deriving the kit?" — is blind to a base list that wraps onto the next
    line and to extra modifiers, and that blindness would demote real provider suites to probes,
    which HIDES findings. Over-reporting a subject is strictly better than dropping one.
    """
    total = len(re.findall(r"class\s+[A-Za-z0-9_]+[^;{]*:[^;{]*\b" + re.escape(kit) + r"\b", src))
    private = len(re.findall(
        r"private\s[^;{]*class\s+[A-Za-z0-9_]+[^;{]*:[^;{]*\b" + re.escape(kit) + r"\b", src))
    return total, private

# The kit's contract, read from its own store-factory return type. A table would go stale the day a
# kit is added, and a naming convention is wrong on first contact with a composite contract name.
#
# There are TWO factory shapes in the shipped kits and a reader that knows only one is blind to the
# other. Most kits expose an awaitable arm-facing accessor; some expose a plain synchronous factory and
# never wrap it. A kit of the second shape declares skip sites the census can read and a contract it
# cannot, which surfaces as a refusal over a kit that is perfectly well formed -- the instrument's gap
# wearing the costume of a finding.
#
# The awaitable form is tried over the whole file FIRST and the synchronous form only where it finds
# nothing, so this is strictly additive: every kit that resolves today resolves to exactly the same
# contract. The fallback is anchored on `abstract` because a kit's required factory is abstract by
# construction, which keeps it off the `protected virtual X CreateSomething()` conveniences that sit
# beside it in the same file and would otherwise be read as the contract.
CONTRACT = re.compile(r"Task<\s*(I[A-Za-z0-9_]*)\s*\??\s*>\s+Create[A-Za-z0-9_]*Async")
CONTRACT_SYNC = re.compile(r"\babstract\s+(I[A-Za-z0-9_]*)\s*\??\s+Create[A-Za-z0-9_]*\s*\(")


def collect_declarations(*trees):
    """name -> {'bases': [...], 'body': str} over every tree given."""
    decls = {}
    for tree in trees:
        if not os.path.isdir(tree):
            continue
        for path in walk(tree):
            src = read(path)
            for match in DECL.finditer(src):
                name = match.group(1)
                flat = re.sub(r"<[^<>]*>", "", match.group(2) or "")
                bases = [b.strip() for b in flat.split(",") if b.strip()]
                body = ""
                start = src.find("{", match.end() - 1)
                if start != -1:
                    depth, index = 0, start
                    while index < len(src):
                        if src[index] == "{":
                            depth += 1
                        elif src[index] == "}":
                            depth -= 1
                            if depth == 0:
                                break
                        index += 1
                    body = src[start:index + 1]
                existing = decls.get(name)
                if existing:
                    # a partial or duplicated declaration contributes, never replaces
                    existing["bases"] = sorted(set(existing["bases"]) | set(bases))
                    existing["body"] += body
                else:
                    decls[name] = {"bases": bases, "body": body}
    return decls


def closure(decls, name, seen=None):
    """Every base and interface reachable from a type's base list.

    One hop is not enough. A backend here names a composite interface and reaches its contract only
    through it, so a one-hop check reads a covered store as unrelated to the kit that covers it.
    """
    if seen is None:
        seen = set()
    declaration = decls.get(name)
    if not declaration:
        return seen
    for base in declaration["bases"]:
        if base not in seen:
            seen.add(base)
            closure(decls, base, seen)
    return seen


# A hook answering from the INSTANCE's own type returns whatever that instance implements, so it
# narrows nothing. This is the shipped default, and it is carried as a default interface member on the
# contract itself -- so it is reached by walking bases, not by reading the store.
REFLECTIVE_HOOK = re.compile(
    r"IsInstanceOfType\s*\(\s*this\s*\)|IsAssignableFrom\s*\(\s*GetType\s*\(\s*\)\s*\)")

# A hook forwarding to the type it wraps answers whatever that type answers, so a decorator does not
# narrow by being a decorator.
DELEGATING_HOOK = re.compile(r"\.GetService\s*\(\s*serviceType\s*\)")

# A hook answering from a fixed set of named types. This is the ONLY shape that can narrow.
ENUMERATING_HOOK = re.compile(r"serviceType\s*==\s*typeof\s*\(")


def hook_body(body):
    """The capability-resolution hook's own body, or None when this type implements no hook.

    Scoped to the member, not the whole type: a type can name a capability anywhere else entirely --
    another method, an XML doc -- and reading that as "the hook answers for it" is how a real
    narrowing hides. An abstract declaration is not an implementation, so it returns None and the
    walk continues to the bases.
    """
    match = re.search(r"\bGetService\s*\(\s*Type\b", body)
    if not match:
        return None
    rest = body[match.end():]
    positions = [(rest.find(token), token) for token in ("{", "=>", ";")]
    positions = sorted((at, token) for at, token in positions if at != -1)
    if not positions:
        return None
    at, token = positions[0]
    if token == ";":
        return None  # abstract declaration; the implementation is elsewhere
    if token == "=>":
        end = rest.find(";", at)
        return rest[at:end if end != -1 else len(rest)]
    depth = 0
    for index in range(at, len(rest)):
        if rest[index] == "{":
            depth += 1
        elif rest[index] == "}":
            depth -= 1
            if depth == 0:
                return rest[at:index + 1]
    return rest[at:]


def narrows(hook, capability):
    """True only on POSITIVE evidence that this hook cannot return the capability.

    A red build must not rest on a guess, so every shape this parser does not positively recognise as
    an enumerating hook is read as reaching the capability. That is the same direction the gate takes
    for a skip site naming no capability: under-report rather than fabricate a finding.
    """
    if re.search(r"\b" + re.escape(capability) + r"\b", hook):
        return False
    if REFLECTIVE_HOOK.search(hook) or DELEGATING_HOOK.search(hook):
        return False
    if ENUMERATING_HOOK.search(hook):
        return True
    # A hook naming neither the instance, nor a type, nor a store to forward to has nothing it could
    # return -- the constant-null hook, which is shipped as a default member on several contracts and
    # is the most complete narrowing there is.
    return not re.search(r"\bthis\b|typeof\s*\(|\.GetService\s*\(", hook)


def narrows_lookup(decls, name, capability, seen=None):
    """True when the type, or a base it inherits the hook from, cannot return the capability.

    A type implementing no hook anywhere in its chain reaches every capability it declares, because
    the contract's own default answers for the instance -- so the arm binds.
    """
    if seen is None:
        seen = set()
    if name in seen:
        return False
    seen.add(name)
    declaration = decls.get(name)
    if not declaration:
        return False
    hook = hook_body(declaration["body"])
    if hook is not None:
        return narrows(hook, capability)
    return any(narrows_lookup(decls, base, capability, seen) for base in declaration["bases"])


def main():
    root, kit_subdir = sys.argv[1], sys.argv[2]
    kit_dir = os.path.join(root, kit_subdir)
    decls = collect_declarations(os.path.join(root, "src"), os.path.join(root, "tests"))
    records = []

    kits = {}
    kit_paths = sorted(path for path in walk(kit_dir)
                       if os.path.basename(path)[:-3].endswith("ConformanceTestKit")) if os.path.isdir(kit_dir) else []
    kit_names = [os.path.basename(path)[:-3] for path in kit_paths]
    tests_dir = os.path.join(root, "tests")
    # Raw mentions select a superset for direct base-list spelling. Roslyn decides whether a
    # mention is actually a derivation; aliases/disabled candidates explicitly refuse.
    suite_files = []
    if os.path.isdir(tests_dir):
        for path in walk(tests_dir):
            source = read(path)
            if any(name in source for name in kit_names):
                suite_files.append(path)
    suite_files.sort()
    paths = sorted(set(kit_paths + suite_files))
    try:
        toolchain, syntax = syntax_census(paths, kit_names)
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        print("UNPARSED\ttooling\t" + str(error).replace("\n", " ").replace("\r", " ").replace("\t", " "))
        print("COUNTS\t0\t0\t0")
        return
    records.append("SYNTAX\t" + toolchain["parserHost"] + "\t" + ",".join(a["sha256"] for a in toolchain["roslyn"]))
    for path, entry in syntax.items():
        for refusal in entry["Refusals"]:
            records.append("UNPARSED\t%s\t%s" % (path, refusal))
    if os.path.isdir(kit_dir):
        for path in sorted(walk(kit_dir)):
            name = os.path.basename(path)[:-3]
            if not name.endswith("ConformanceTestKit"):
                continue
            src = read(path)
            parsed = syntax[path]
            sites = []
            for call in parsed["Calls"]:
                arm = call["Arm"]
                if call["DynamicArmKind"]:
                    location = "%s:%d:%d" % (os.path.relpath(path, root).replace("\\", "/"), call["Line"], call["Column"])
                    arm = "<dynamic@" + location + ">"
                    records.append("DYNAMIC_ARM\t%s\t%s\t%s" % (name, location, call["DynamicArmKind"]))
                sites.append((arm, call["Capability"]))
            if not parsed["CandidateCount"] and not parsed["Refusals"]:
                continue
            contract = CONTRACT.search(src) or CONTRACT_SYNC.search(src)
            kits[name] = {
                "contract": contract.group(1) if contract else None,
                "sites": sites,
                "candidate_count": parsed["CandidateCount"],
            }

    suites_seen = 0

    for kit, info in sorted(kits.items()):
        if info["contract"] is None:
            records.append("NOCONTRACT\t%s" % kit)
            continue
        contract = info["contract"]

        derivers = []
        probes = []
        for path in suite_files:
            src = read(path)
            derivations = [d for d in syntax[path]["Derivations"] if d["Kit"] == kit]
            if not derivations:
                continue
            suite = os.path.basename(path)[:-3]
            total, private = len(derivations), sum(d["PrivateNested"] for d in derivations)
            # A probe is reported, never silently dropped, and never judged: it constructs no store
            # by design. Note the guard is `total > 0` -- a file this parse cannot read at all falls
            # through as a DERIVER, so a parse failure can only ever over-report a subject.
            if total > 0 and total == private:
                probes.append(suite)
                continue
            derivers.append((suite, src))
        for suite in sorted(probes):
            records.append("PROBE\t%s\t%s" % (kit, suite))
        if not derivers:
            records.append("NODERIVER\t%s" % kit)
            continue

        for arm, capability in info["sites"]:
            if capability is None:
                records.append("UNNAMED\t%s\t%s" % (kit, arm))
        capability_sites = sorted({(a, c) for a, c in info["sites"] if c})

        for suite, src in derivers:
            suites_seen += 1
            # The store is a type the suite CONSTRUCTS whose closure contains the contract. Binding
            # on the constructed type is what stops one suite's store being credited to its
            # neighbour. A suite constructing several is judged over each of them, because a store
            # that hides a capability it has is a finding whichever of them it is.
            composed = set()
            if contract.startswith("I") and len(contract) > 1 and contract[1].isupper():
                # Resolved case-INSENSITIVELY against declared types, because the builder verb and the
                # type it reaches do not always agree on the casing of an acronym: .UseCosmosDb pairs
                # with CosmosDbSagaStore, while .UseMongoDB pairs with MongoDbSagaStore. Requiring an
                # exact match would silently refuse that provider and report the oracle as underivable,
                # which is a verdict about our naming rather than about the suite. Matching is still
                # anchored on a type that EXISTS and whose closure carries the contract, so tolerance
                # here widens nothing: an unmatched composition is dropped exactly as before.
                folded = {name.casefold(): name for name in decls}
                for provider in USES.findall(src):
                    resolved = folded.get((provider + contract[1:]).casefold())
                    if resolved:
                        composed.add(resolved)
            candidates = (set(NEW.findall(src)) | set(NEW_TARGET_TYPED.findall(src))
                          | set(ADDS.findall(src)) | composed)
            stores = sorted(
                n for n in candidates
                if n in decls and contract in (closure(decls, n) | {n})
            )
            if not stores:
                records.append("NOSTORE\t%s\t%s" % (kit, suite))
                continue
            for store in stores:
                held = closure(decls, store) | {store}
                for arm, capability in capability_sites:
                    if capability not in held:
                        verdict = "TOLERABLE"
                    elif narrows_lookup(decls, store, capability):
                        verdict = "LIE"
                    else:
                        verdict = "BINDS"
                    records.append(
                        "%s\t%s\t%s\t%s\t%s" % (verdict, suite, arm, capability, store)
                    )

    site_count = sum(k["candidate_count"] for k in kits.values())
    records.append("COUNTS\t%d\t%d\t%d" % (len(kits), site_count, suites_seen))
    print("\n".join(records))


if __name__ == "__main__":
    main()
