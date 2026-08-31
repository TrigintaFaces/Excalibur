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
SKIP = re.compile(
    r"SkipArm\(\s*nameof\(\s*([A-Za-z_][A-Za-z0-9_]*)\s*\)\s*,\s*"
    r"(?:typeof\(\s*([A-Za-z_][A-Za-z0-9_]*)\s*\)|null)\s*,",
    re.S,
)

NEW = re.compile(r"\bnew\s+([A-Z][A-Za-z0-9_]*)\s*[(\{<]")

# A suite may OBTAIN its store through the provider's own registration extension instead of
# constructing it -- services.AddOracleInboxStore(...) then a keyed resolve. That is not a lesser
# route: it is the one a consumer actually takes, and a suite exercising it tests more than one that
# news the type up. Binding still happens on a CONCRETE name, so the precision the `new` route buys
# is preserved: AddOracleInboxStore yields OracleInboxStore, which must itself be a declared type
# whose closure contains the contract, exactly as a constructed store must. A suite that registers a
# neighbour's provider is judged over that provider too, which is correct -- it really does use it.
ADDS = re.compile(r"\.Add([A-Z][A-Za-z0-9_]*)\s*[(<]")


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
CONTRACT = re.compile(r"Task<\s*(I[A-Za-z0-9_]*)\s*\??\s*>\s+Create[A-Za-z0-9_]*Async")


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
    if os.path.isdir(kit_dir):
        for path in sorted(walk(kit_dir)):
            name = os.path.basename(path)[:-3]
            if not name.endswith("ConformanceTestKit"):
                continue
            src = read(path)
            sites = [(m.group(1), m.group(2)) for m in SKIP.finditer(src)]
            if not sites:
                continue
            contract = CONTRACT.search(src)
            kits[name] = {
                "contract": contract.group(1) if contract else None,
                "sites": sites,
            }

    tests_dir = os.path.join(root, "tests")
    suite_files = sorted(walk(tests_dir)) if os.path.isdir(tests_dir) else []
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
            if not re.search(r":[^;{]*\b" + re.escape(kit) + r"\b", src):
                continue
            suite = os.path.basename(path)[:-3]
            total, private = derivations(src, kit)
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
            candidates = set(NEW.findall(src)) | set(ADDS.findall(src))
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

    site_count = sum(len(k["sites"]) for k in kits.values())
    records.append("COUNTS\t%d\t%d\t%d" % (len(kits), site_count, suites_seen))
    print("\n".join(records))


if __name__ == "__main__":
    main()
