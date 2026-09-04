#!/usr/bin/env python3
"""docs-csharp-member-gate.py -- resolve MEMBER references in doc C# snippets against src/.

The sibling tool `docs-csharp-extract.py` resolves TYPE names: it catches a snippet that
names a class that does not exist. It never looks at what a snippet does WITH a type, so a
fabricated property or method on a REAL type resolves clean. That is the shape the
compliance drift took -- the type was right and its whole option surface was invented --
and it is what this gate closes.

Three verdicts, and the third is not decoration:

  PASS   (exit 0)  every member reference the gate could bind resolved in src/
  FAIL   (exit 1)  at least one member reference bound to a real type and did not exist
  REFUSE (exit 2)  the gate could not measure (no extractor, no symbol table, bad input).
                   A REFUSE is NOT a PASS. A gate that cannot see must not report green.

Ambiguity is a fourth BUCKET, not a verdict: when a simple type name has more than one
declaration in src/, the reference is reported as AMBIGUOUS and counted separately. It is
neither a hit nor a miss, because the gate cannot tell which declaration the snippet meant.
Two same-named nested classes have already produced a false phantom this way.

DELIBERATELY NOT WIRED INTO CI YET. On the doc surface as it stands this gate reports 327
unresolved member references, so wiring it now would turn every branch red and block every
other lane -- and the only way to make it green today would be to weaken it, which is worse
than having no gate. It is a reporting tool until that backlog is burned down. The wiring
decision belongs to whoever owns that call, not to this file. Run it by hand:

    python3 eng/ci/docs-csharp-member-gate.py              # report + verdict
    python3 eng/ci/docs-csharp-member-gate.py --json       # machine-readable findings
    python3 eng/ci/docs-csharp-member-gate.py --self-test  # prove every verdict is reachable

When it is wired, the sibling `docs-csharp-phantom-gate.sh` shows the shape that works: scope
it to the lines a diff touched, so a new snippet is held to the bar without the pre-existing
backlog blocking anyone.

Stdlib only, deterministic, no network, no compiler.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import tempfile

# ---------------------------------------------------------------------------
# Scope -- printed on every run, alongside the exclusions. A docs gate that
# silently covers less than a reader assumes is the defect class it exists to catch.
# ---------------------------------------------------------------------------

SCOPE = [
    "fenced ```csharp / ```cs blocks in docs/**, docs-site/**, and **/README*.md",
    "member references whose receiver type this gate can bind WITHOUT a compiler:",
    "  - an object initializer            new T { Member = ... }",
    "  - a local bound to a construction  var x = new T(...);  then  x.Member",
    "  - an options lambda parameter      .Configure<T>(o => o.Member)",
    "  - a qualified static/enum access   T.Member",
    "  - a chain through the above        o.Member.Nested  (walked while each hop's",
    "                                     declared type is itself declared in src/)",
    "resolution target: type + member declarations parsed from src/**/*.cs",
    "  (public AND internal; base types and interfaces are merged transitively)",
]

EXCLUSIONS = [
    "XML doc comments in src/** -- the /// surface is not scanned by this gate",
    ".resx <value> content -- not scanned",
    ".csproj package metadata (Description, PackageReleaseNotes) -- not scanned",
    "samples/** -- not scanned (the doc walk covers docs/, docs-site/, README*)",
    "non-csharp fences (bash, json, yaml, mermaid, text) -- not scanned",
    "```csharp ignore / ```csharp no-compile blocks -- opted out by declaration",
    "TYPE existence -- owned by the sibling docs-csharp-extract.py, not repeated here",
    "members of BCL / third-party types (string, TimeSpan, HttpClient, ...) -- a chain",
    "  stops as soon as a hop's declared type is not declared in src/",
    "receivers this gate cannot bind (a builder lambda parameter whose type comes from",
    "  an extension method's signature, a var from a method call) -- skipped, not flagged",
    "members reached only through a type that is AMBIGUOUS in src/ -- bucketed, not flagged",
    "method ARITY and parameter types -- a member name that exists is accepted; a wrong",
    "  overload is out of reach without a compiler",
    "receivers on a CURATED external-collision denylist (a src/ type sharing its simple name",
    "  with a BCL / ASP.NET / third-party one) -- skipped. The list is curated, so a collision",
    "  nobody has hit yet is still bindable and would read as a phantom. Known incompleteness.",
]

SKIP_DIRS = {"node_modules", "bin", "obj", ".git", ".dts", ".claude", "packages", "TestResults"}

# ---------------------------------------------------------------------------
# Receiver types this gate must NOT bind
# ---------------------------------------------------------------------------
#
# A simple type name in a snippet can name a BCL / ASP.NET / third-party type that merely
# SHARES a name with something in src/. Binding it resolves the members against the wrong
# declaration and every one of them reads as a phantom. Measured here: 58 findings against
# an `Excalibur.Dispatch` HealthCheckOptions for snippets that meant the ASP.NET Core type
# of the same name, and 46 against a `Results` that meant the minimal-API one.
#
# The sibling extractor already curates this population, so its list is imported rather
# than copied -- one list, one place to fix. EXTERNAL_COLLISIONS below extends it with
# names measured as colliding here that the type-level tool has no reason to carry.
#
# This is a CURATED list, which makes it a known and permanent incompleteness: a collision
# nobody has hit yet is still bindable and will read as a phantom. That limitation is
# printed in the gate's own EXCLUSIONS so a reader never mistakes silence for coverage.

EXTERNAL_COLLISIONS = {
    "HealthCheckOptions",     # Microsoft.AspNetCore.Diagnostics.HealthChecks
    "HealthCheckResult", "HealthReport", "HealthStatus",
    "RetryPolicy",            # Polly
    "Histogram", "Counter", "UpDownCounter",   # System.Diagnostics.Metrics
    "Log", "LoggerConfiguration",              # Serilog
    "Data", "DataRow", "DataTable",
    "TestServer", "WebApplicationFactory",
    "ConnectionFactory", "IModel", "IConnection",   # RabbitMQ.Client
    "ServiceBusClient", "ServiceBusMessage",        # Azure SDK
    "ProducerConfig", "ConsumerConfig",             # Confluent.Kafka
    # Test-project types that SHARE a name with a src/ type. The symbol table only reads
    # src/**, so the test double is invisible and the docs that describe it resolve against
    # the wrong declaration. Measured: Tests.Shared.Infrastructure.WaitHelpers (whose
    # AwaitSignalAsync is real) against Excalibur.Dispatch.Testing.Polling.WaitHelpers, and
    # Tests.Shared.TestDoubles.TestMessageContext against a private nested class of the same
    # name inside MessageContextBuilder.
    "WaitHelpers", "TestMessageContext",
}


# Members contributed by EXTERNAL extension methods, which are invisible to a src/-only symbol
# table. `options.Connection.ShouldNotBeNull()` is a Shouldly call on a framework type -- the
# receiver binds correctly and the member is still not declared anywhere under src/.
EXTERNAL_MEMBER_RE = re.compile(r"^(?:Should[A-Z]\w*|Be[A-Z]\w*)$")
EXTERNAL_MEMBERS = {
    "Select", "Where", "Any", "All", "First", "FirstOrDefault", "Single", "SingleOrDefault",
    "ToList", "ToArray", "Count", "OrderBy", "OrderByDescending", "Sum", "Contains",
    "ConfigureAwait", "AsTask", "GetAwaiter", "Should",
}


def _external_denylist(extractor_path):
    """Import the sibling extractor's curated BCL denylist rather than duplicating it."""
    names = set(EXTERNAL_COLLISIONS)
    try:
        import importlib.util
        spec = importlib.util.spec_from_file_location("_docs_csharp_extract", extractor_path)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        names |= set(getattr(mod, "BCL_DENYLIST", ()))
    except Exception:
        # Not fatal: the local set still applies. The banner reports what was loaded so a
        # reader can see whether the shared list was in play for this run.
        return names, False
    return names, True

# ---------------------------------------------------------------------------
# C# symbol table
# ---------------------------------------------------------------------------

TYPE_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?:(?:public|internal|protected|private|sealed|abstract|static|partial|readonly|ref|file|new|unsafe)\s+)*"
    r"(class|interface|struct|record|enum)\s+"
    r"(?:class\s+|struct\s+)?"          # `record class Foo` / `record struct Foo`
    r"([A-Za-z_]\w*)"
    r"\s*(<[^>{(]*>)?"                  # generic parameter list
    r"\s*(\([^)]*\))?"                  # positional record parameters
    r"\s*(:\s*[^{]+)?",                 # base list
)

# A declared type token, tolerating ONE level of generic nesting (`Task<IReadOnlyList<T>>`).
# A flat `<[^>]*>` stops at the first `>` and then fails, which silently dropped every
# member whose type was a nested generic -- measured on an interface whose only method
# returns `Task<IReadOnlyList<...>>` and which therefore read as having no members at all.
_TYPE_TOK = r"[A-Za-z_][\w\.]*(?:\s*<[^<>]*(?:<[^<>]*>[^<>]*)*>)?(?:\?|\[\])*"

# Trailing `$` matters: a property whose brace opens on the FOLLOWING line
# (`public int QueueSize` / newline / `{`) has no `{` to anchor on, and every such
# property was being dropped. A field cannot end a line bare -- it needs `;` or `=` --
# so admitting end-of-line here does not let fields through as properties.
PROP_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?:(?:public|internal|protected|private|static|virtual|override|abstract|sealed|new|required|readonly|unsafe|extern|partial)\s+)*"
    r"(" + _TYPE_TOK + r")\s+"
    r"([A-Za-z_]\w*)\s*(?:=>|\{|$)"
)

METHOD_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?:(?:public|internal|protected|private|static|virtual|override|abstract|sealed|new|async|required|readonly|unsafe|extern|partial)\s+)*"
    r"(" + _TYPE_TOK + r")\s+"
    r"([A-Za-z_]\w*)\s*(?:<[^>()]*>)?\s*\("
)

FIELD_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?:(?:public|internal|protected|private|static|readonly|const|volatile|new|required|unsafe)\s+)+"
    r"(" + _TYPE_TOK + r")\s+"
    r"([A-Za-z_]\w*)\s*(?:=|;|,)"
)

# An extension method declares itself a member of its RECEIVER type: `context.SetItem(...)`
# is written by every consumer exactly as an instance call, and a gate that only reads members
# declared inside the type body reports each one as a phantom. Measured on IMessageContext,
# whose SetItem/GetItem/RemoveItem/ContainsItem/CreateChildContext all live in static
# extension classes -- 15 false phantoms across the testing docs.
EXT_METHOD_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?:(?:public|internal)\s+)(?:(?:static|unsafe|partial|async)\s+)+"
    r"(" + _TYPE_TOK + r")\s+"
    r"([A-Za-z_]\w*)\s*(?:<[^>()]*>)?\s*\(\s*this\s+"
    r"(?:(?:ref|in|scoped)\s+)*"
    r"(" + _TYPE_TOK + r")\s+[A-Za-z_]\w*"
)

ENUM_MEMBER_RE = re.compile(r"^\s*(?:\[[^\]]*\]\s*)*([A-Za-z_]\w*)\s*(?:=[^,]*)?,?\s*(?://.*)?$")

RECORD_PARAM_RE = re.compile(r"([A-Za-z_][\w\.<>\[\]\?,\s]*?)\s+([A-Za-z_]\w*)\s*(?:=[^,]*)?$")

# A declared type name reduced to the simple name the doc snippet would write.
SIMPLE_RE = re.compile(r"([A-Za-z_]\w*)")

KEYWORD_TYPES = {
    "var", "void", "return", "new", "if", "else", "for", "foreach", "while", "do", "switch",
    "case", "using", "namespace", "public", "private", "internal", "protected", "static",
    "readonly", "const", "get", "set", "init", "value", "this", "base", "throw", "await",
    "yield", "try", "catch", "finally", "lock", "in", "out", "ref", "params", "where",
    "operator", "implicit", "explicit", "delegate", "event", "add", "remove", "when", "is",
    "as", "typeof", "nameof", "sizeof", "default", "checked", "unchecked", "goto", "break",
    "continue", "partial", "record", "class", "struct", "interface", "enum", "global",
}


class TypeInfo:
    __slots__ = ("name", "kind", "members", "member_types", "bases", "paths", "partial")

    def __init__(self, name, kind):
        self.name = name
        self.kind = kind
        self.members = set()
        self.member_types = {}
        self.bases = set()
        self.paths = []
        self.partial = True   # cleared as soon as a non-partial declaration is seen


def _simple_type_name(raw):
    """Reduce a declared type expression to the simple name a snippet would bind on.

    `System.Collections.Generic.List<Foo>` -> `List`; `Foo?` -> `Foo`; `Foo[]` -> `Foo`.
    A generic like `List<Foo>` reduces to `List`, which is not in src/, so the chain stops
    there rather than guessing at the element type. That is deliberate.
    """
    raw = raw.strip()
    if "<" in raw:
        raw = raw.split("<", 1)[0]
    raw = raw.rstrip("?[] ")
    if "." in raw:
        raw = raw.rsplit(".", 1)[1]
    m = SIMPLE_RE.match(raw)
    return m.group(1) if m else ""


def _strip_noise(line):
    """Remove line comments and string literals so their contents never parse as code."""
    out = []
    i = 0
    n = len(line)
    while i < n:
        c = line[i]
        if c == "/" and i + 1 < n and line[i + 1] == "/":
            break
        if c == '"':
            i += 1
            while i < n:
                if line[i] == "\\":
                    i += 2
                    continue
                if line[i] == '"':
                    i += 1
                    break
                i += 1
            out.append('""')
            continue
        out.append(c)
        i += 1
    return "".join(out)


def build_symbol_table(repo):
    """Parse src/**/*.cs into {simple type name: TypeInfo}. Empty result => REFUSE."""
    types = {}
    ext_methods = []
    src = os.path.join(repo, "src")
    if not os.path.isdir(src):
        return types, 0
    files = 0
    for dirpath, dirnames, filenames in os.walk(src):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            try:
                with open(path, "r", encoding="utf-8", errors="replace") as fh:
                    raw_lines = fh.read().split("\n")
            except OSError:
                continue
            files += 1
            rel = os.path.relpath(path, repo).replace(os.sep, "/")
            _parse_file(raw_lines, rel, types, ext_methods)
    # Attach extension methods once every type is known (the receiver may be declared in a
    # file walked after the extension class).
    for recv, member, decl_type in ext_methods:
        info = types.get(recv)
        if info is not None:
            info.members.add(member)
            info.member_types.setdefault(member, decl_type)
        elif TYPE_PARAM_RE.match(recv):
            UNIVERSAL_EXT_MEMBERS.add(member)
    return types, files


def _parse_file(raw_lines, rel, types, ext_methods=None):
    """Brace-depth scan: attribute each member line to the innermost enclosing type."""
    stack = []          # (TypeInfo, depth_at_open)
    depth = 0
    pending = None      # a TypeInfo whose opening brace has not been seen yet
    for idx, raw in enumerate(raw_lines):
        line = _strip_noise(raw)
        if not line.strip():
            depth += line.count("{") - line.count("}")
            continue

        if ext_methods is not None:
            m_ext = EXT_METHOD_RE.match(line)
            if m_ext:
                recv = _simple_type_name(m_ext.group(3))
                if recv and recv not in KEYWORD_TYPES:
                    ext_methods.append(
                        (recv, m_ext.group(2), _simple_type_name(m_ext.group(1))))

        current = stack[-1][0] if stack else None
        if current is not None and pending is None:
            _record_member(current, line)

        m = TYPE_RE.match(line)
        if m and pending is None:
            kind, name, _generics, positional, bases = (
                m.group(1), m.group(2), m.group(3), m.group(4), m.group(5))
            info = types.get(name)
            if info is None:
                info = TypeInfo(name, kind)
                types[name] = info
            if rel not in info.paths:
                info.paths.append(rel)
            if not re.search(r"\bpartial\b", line):
                info.partial = False
            if bases:
                for b in bases[1:].split(","):
                    b = _simple_type_name(b)
                    if b and b not in KEYWORD_TYPES:
                        info.bases.add(b)
            if positional:
                _record_positional(info, positional[1:-1])
            # A NESTED type is reachable through its enclosing type exactly like a member
            # (`Outer.Inner.Value`). None of the member regexes match a type declaration
            # whose brace opens on the next line, so without this every nested type read as
            # a phantom member of its parent -- measured on two nested constant classes.
            if stack:
                enclosing = stack[-1][0]
                enclosing.members.add(name)
                enclosing.member_types.setdefault(name, name)
            pending = info

        opens = line.count("{")
        closes = line.count("}")
        if pending is not None and opens > 0:
            stack.append((pending, depth))
            pending = None
        elif pending is not None and ";" in line:
            pending = None      # `record Foo(int A);` -- no body
        depth += opens - closes
        while stack and depth <= stack[-1][1]:
            stack.pop()


def _record_positional(info, params):
    for part in _split_top_level(params):
        m = RECORD_PARAM_RE.match(part.strip())
        if m:
            info.members.add(m.group(2))
            info.member_types[m.group(2)] = _simple_type_name(m.group(1))


def _split_top_level(text):
    out, buf, depth = [], [], 0
    for ch in text:
        if ch in "<([{":
            depth += 1
        elif ch in ">)]}":
            depth -= 1
        if ch == "," and depth == 0:
            out.append("".join(buf))
            buf = []
            continue
        buf.append(ch)
    if buf:
        out.append("".join(buf))
    return out


def _record_member(info, line):
    if info.kind == "enum":
        m = ENUM_MEMBER_RE.match(line)
        if m and m.group(1) not in KEYWORD_TYPES:
            info.members.add(m.group(1))
            info.member_types[m.group(1)] = info.name
        return
    for rx in (PROP_RE, METHOD_RE, FIELD_RE):
        m = rx.match(line)
        if not m:
            continue
        decl_type, member = m.group(1), m.group(2)
        if member in KEYWORD_TYPES or decl_type in ("return", "new", "throw", "await"):
            continue
        if member == info.name:      # constructor
            continue
        info.members.add(member)
        info.member_types.setdefault(member, _simple_type_name(decl_type))
        return


# Every C# type inherits System.Object, whose public members are callable on any receiver --
# including an interface reference, which implicitly derives from object. Without this the gate
# reports a correct `message.GetType()` as an unresolved member, because object is not declared
# in src/ and so never appears in any type's base chain.
OBJECT_MEMBERS = {"GetType", "ToString", "Equals", "GetHashCode"}

# An extension method whose receiver is an unconstrained TYPE PARAMETER -- `AsSingleChunk<T>(this T
# data)` -- is callable on every receiver, so it cannot be attached to any one declared type. The
# attach loop below drops it (the receiver resolves to no type), and every documented call then reads
# as a phantom member of whatever the local happened to be.
TYPE_PARAM_RE = re.compile(r"^T[A-Z0-9]*$")
UNIVERSAL_EXT_MEMBERS = set()


def resolve_member(types, type_name, member, _seen=None):
    """(status, declaring_type, member_type) where status is found|missing|open.

    `open` means a base type is not declared in src/ (BCL or third-party), so the member
    set is not knowable here and a miss cannot be asserted.
    """
    if _seen is None:
        _seen = set()
    if type_name in _seen:
        return "missing", None, None
    _seen.add(type_name)
    if member in OBJECT_MEMBERS or member in UNIVERSAL_EXT_MEMBERS:
        return "open", None, None
    info = types.get(type_name)
    if info is None:
        return "open", None, None
    if member in info.members:
        return "found", info, info.member_types.get(member)
    for base in sorted(info.bases):
        status, decl, mt = resolve_member(types, base, member, _seen)
        if status in ("found", "open"):
            return status, decl, mt
    return "missing", info, None


# ---------------------------------------------------------------------------
# Snippet analysis
# ---------------------------------------------------------------------------

NEW_INIT_RE = re.compile(r"\bnew\s+([A-Za-z_]\w*)\s*(?:<[^>{;]*>)?\s*(?:\([^;{]*\))?\s*\{")
# Group 1 is the DECLARED type (`var` when inferred), 2 the variable, 3 the constructed type.
# An explicit declared type must win over the constructed one: `IMessageContext ctx = new
# MessageContextBuilder()...Build()` declares an IMessageContext, and binding it to the builder
# reported every IMessageContext member on it as a phantom.
LOCAL_NEW_RE = re.compile(r"\b(var|[A-Za-z_][\w\.<>\[\]\?]*)\s+([A-Za-z_]\w*)\s*=\s*new\s+([A-Za-z_]\w*)")
CONFIGURE_RE = re.compile(
    r"\.(?:Configure|PostConfigure|AddOptions|ConfigureAll|PostConfigureAll)\s*<\s*([A-Za-z_]\w*)\s*>\s*\(\s*(?:\(\s*)?([A-Za-z_]\w*)\s*\)?\s*=>")
LOCAL_TYPE_DECL_RE = re.compile(r"^\s*([A-Za-z_]\w*)\s+([A-Za-z_]\w*)\s*=\s*[^=]")
ACCESS_RE = re.compile(r"(?<![\w\.])([A-Za-z_]\w*)((?:\s*\.\s*[A-Za-z_]\w*)+)")
LOCAL_TYPE_DEF_RE = re.compile(
    r"\b(?:class|interface|struct|record|enum)\s+([A-Za-z_]\w*)")


def _balanced_initializer(code, open_idx):
    depth = 0
    for i in range(open_idx, len(code)):
        if code[i] == "{":
            depth += 1
        elif code[i] == "}":
            depth -= 1
            if depth == 0:
                return code[open_idx + 1:i]
    return ""


def _initializer_assignments(body):
    """Top-level `Name =` targets of an object initializer (nested ones are skipped here;
    the outer scan finds them on their own `new T {` match)."""
    out = []
    depth = 0
    i = 0
    n = len(body)
    while i < n:
        ch = body[i]
        if ch in "{([":
            depth += 1
        elif ch in "})]":
            depth -= 1
        elif (depth == 0 and ch == "=" and i + 1 < n
              and body[i + 1] not in "=>"          # `==` is a comparison, `=>` a lambda arrow
              and (i == 0 or body[i - 1] not in "=!<>+-*/%&|^")):
            j = i - 1
            while j >= 0 and body[j].isspace():
                j -= 1
            k = j
            while k >= 0 and (body[k].isalnum() or body[k] == "_"):
                k -= 1
            name = body[k + 1:j + 1]
            if name and not name[0].isdigit():
                out.append((name, body[:i].count("\n")))
        i += 1
    return out


def analyse_block(block, types, denylist=frozenset()):
    """Yield findings for one extracted C# block."""
    code = "\n".join(_strip_noise(l) for l in block["code"].split("\n"))
    start = block["startLine"]
    local_types = set(LOCAL_TYPE_DEF_RE.findall(code))

    var_types = {}
    for m in LOCAL_NEW_RE.finditer(code):
        declared = _simple_type_name(m.group(1))
        var_types[m.group(2)] = (
            declared if declared != "var" and declared in types else m.group(3))
    for m in CONFIGURE_RE.finditer(code):
        var_types[m.group(2)] = m.group(1)
    for line in code.split("\n"):
        m = LOCAL_TYPE_DECL_RE.match(line)
        if m and m.group(1) in types and m.group(2) not in var_types:
            var_types[m.group(2)] = m.group(1)

    findings = []

    def check(type_name, member, line_no, via):
        if type_name in local_types or type_name in denylist:
            return
        if member in EXTERNAL_MEMBERS or EXTERNAL_MEMBER_RE.match(member):
            return
        info = types.get(type_name)
        if info is None:
            return                               # not our surface -- excluded, not flagged
        if len(info.paths) > 1 and not info.partial:
            findings.append(("ambiguous", type_name, member, line_no, via,
                             sorted(info.paths)))
            return
        status, decl, _mt = resolve_member(types, type_name, member)
        if status == "missing":
            findings.append(("missing", type_name, member, line_no, via,
                             (decl.paths if decl else info.paths)))

    # 1. object initializers: new T { Member = ... }
    for m in NEW_INIT_RE.finditer(code):
        tname = m.group(1)
        body = _balanced_initializer(code, m.end() - 1)
        base_line = start + code[:m.start()].count("\n")
        for member, off in _initializer_assignments(body):
            check(tname, member, base_line + off, "initializer")

    # 2/3/4. chained accesses off a bound receiver, or off a type name directly
    for m in ACCESS_RE.finditer(code):
        root = m.group(1)
        if root in KEYWORD_TYPES:
            continue
        chain = [p.strip() for p in m.group(2).split(".") if p.strip()]
        line_no = start + code[:m.start()].count("\n")
        if root in var_types:
            cur = var_types[root]
            via = "local"
        elif root in types and root not in local_types and root not in denylist:
            cur = root
            via = "static"
        else:
            continue
        for hop in chain:
            if cur is None or cur not in types or cur in denylist:
                break
            check(cur, hop, line_no, via)
            status, _decl, mt = resolve_member(types, cur, hop)
            if status != "found":
                break
            cur = mt
            via = "chain"

    return findings


# ---------------------------------------------------------------------------
# Driver
# ---------------------------------------------------------------------------

def extract_blocks(repo, root, extractor, gate_files=None, gate_lines=None):
    cmd = [sys.executable, extractor, "--repo", repo, "--json"]
    if root:
        cmd += ["--root", root]
    # Diff scoping is the extractor's, not ours -- it already implements both, and the sibling
    # phantom gate drives it exactly this way. Forwarding beats reimplementing the hunk walk.
    if gate_files:
        cmd += ["--gate-files", gate_files]
    if gate_lines:
        cmd += ["--gate-lines", gate_lines]
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True)
    except OSError as exc:
        return None, "cannot run the extractor (%s)" % exc
    if proc.returncode != 0:
        return None, "extractor exited %d" % proc.returncode
    try:
        return json.loads(proc.stdout), None
    except ValueError as exc:
        return None, "extractor emitted unparseable JSON (%s)" % exc


def print_banner(repo, root, files, type_count, block_count, scanned, deny_count, deny_shared):
    print("docs-csharp-member-gate -- member resolution for C# snippets in the doc surface")
    print("")
    print("SCOPE (what this gate covers):")
    for s in SCOPE:
        print("  " + s)
    print("")
    print("EXCLUSIONS (what this gate does NOT cover -- read before trusting a green):")
    for s in EXCLUSIONS:
        print("  " + s)
    print("")
    print("INPUTS: repo=%s doc-root=%s" % (repo, root or repo))
    print("        symbol table: %d types from %d .cs files under src/" % (type_count, files))
    print("        snippets: %d C# block(s) extracted, %d scanned (ignore-marked excluded)"
          % (block_count, scanned))
    print("        external-collision denylist: %d name(s), shared list from the sibling "
          "extractor %s" % (deny_count, "LOADED" if deny_shared else "NOT loaded (local set only)"))
    print("")


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--repo", default=os.getcwd(), help="repo root (default: cwd)")
    ap.add_argument("--root", default=None, help="doc walk root (default: --repo)")
    ap.add_argument("--json", action="store_true", help="emit findings as JSON")
    ap.add_argument("--gate-files", default=None,
                    help="restrict the scan to the doc files listed in this file (diff scoping)")
    ap.add_argument("--gate-lines", default=None,
                    help="restrict the scan to the path:line entries in this file (hunk scoping)")
    ap.add_argument("--max-report", type=int, default=60,
                    help="display cap for findings; NEVER affects the reported counts")
    ap.add_argument("--self-test", action="store_true",
                    help="run the built-in PASS / FAIL / REFUSE / AMBIGUOUS arms")
    args = ap.parse_args(argv)

    if args.self_test:
        return self_test(args.repo)

    repo = os.path.abspath(args.repo)
    extractor = os.path.join(os.path.dirname(os.path.abspath(__file__)), "docs-csharp-extract.py")
    if not os.path.isfile(extractor):
        print("REFUSE: sibling extractor not found at %s" % extractor, file=sys.stderr)
        return 2

    types, files = build_symbol_table(repo)
    if not types:
        print("REFUSE: the src/ symbol table is EMPTY (repo=%s). An empty table cannot "
              "distinguish 'no phantom members' from 'nothing was parsed', so this gate "
              "reports REFUSE rather than a green it did not earn." % repo, file=sys.stderr)
        return 2

    # An EMPTY scope file is not an empty scope -- the extractor falls back to walking the whole
    # doc surface, so a caller that meant "nothing changed" gets a full-tree verdict wearing a
    # diff-scoped invocation. Measured: `--gate-files <empty>` scanned 5056 blocks and failed.
    # Refuse instead. A caller with nothing to gate must exit before invoking, not pass an empty list.
    for flag, path in (("--gate-files", args.gate_files), ("--gate-lines", args.gate_lines)):
        if path and os.path.exists(path) and os.path.getsize(path) == 0:
            print("EXAMINED: 0 doc block(s)")
            print("REFUSE: %s names an empty list. The extractor widens an empty scope to the whole"
                  " doc surface, so this would report a full-tree verdict as a diff-scoped one." % flag)
            return 2

    blocks, err = extract_blocks(repo, args.root, extractor, args.gate_files, args.gate_lines)
    if blocks is None:
        print("REFUSE: %s" % err, file=sys.stderr)
        return 2

    denylist, deny_shared = _external_denylist(extractor)
    scanned = [b for b in blocks if b.get("tier") != "ignore"]
    findings = []
    for b in scanned:
        findings.extend((b["file"], f) for f in analyse_block(b, types, denylist))

    missing = [f for f in findings if f[1][0] == "missing"]
    ambiguous = [f for f in findings if f[1][0] == "ambiguous"]

    if args.json:
        json.dump([
            {"file": fl, "status": k, "type": t, "member": mem, "line": ln,
             "boundVia": via, "declaredIn": paths}
            for fl, (k, t, mem, ln, via, paths) in findings
        ], sys.stdout, indent=2)
        print("")
    else:
        print_banner(repo, args.root, files, len(types), len(blocks), len(scanned),
                     len(denylist), deny_shared)
        # Counts are computed from the full lists; the display cap below never feeds a count.
        shown = 0
        for fl, (kind, tname, member, line, via, paths) in missing + ambiguous:
            if shown >= args.max_report:
                break
            shown += 1
            label = "UNRESOLVED" if kind == "missing" else "AMBIGUOUS "
            where = paths[0] if len(paths) == 1 else "%d declarations: %s" % (
                len(paths), ", ".join(paths))
            print("%s %s:%d: %s.%s -- resolved against %s (bound via %s)"
                  % (label, fl, line, tname, member, where, via))
        hidden = len(missing) + len(ambiguous) - shown
        if hidden > 0:
            print("... %d further finding(s) not displayed (--max-report=%d). "
                  "The counts below are computed from the FULL list, not this display."
                  % (hidden, args.max_report))
        print("")
        print("RESULT: %d unresolved member reference(s), %d ambiguous, across %d scanned block(s)"
              % (len(missing), len(ambiguous), len(scanned)))
        print("        ambiguous = the receiver type name has >1 declaration in src/; the gate")
        print("        cannot tell which one the snippet meant, so it is neither hit nor miss.")
        print("VERDICT: %s" % ("FAIL" if missing else "PASS"))

    return 1 if missing else 0


# ---------------------------------------------------------------------------
# Self-test -- every verdict must be reachable, or the gate is decoration
# ---------------------------------------------------------------------------

# A real, UNAMBIGUOUS framework type used as the live control. If this type or member ever
# stops existing the LIVENESS arm goes red, which is the correct signal: the control is
# load-bearing, not cosmetic.
CONTROL_TYPE = "Soc2Options"
CONTROL_MEMBER = "DefaultTestSampleSize"
PHANTOM_MEMBER = "TotallyFakeThresholdXyz"
# A real type name that genuinely has >1 declaration in src/ -- exercises the AMBIGUOUS bucket.
AMBIGUOUS_TYPE = "AuditEvent"


def _fixture(tmp, name, snippet):
    d = os.path.join(tmp, name, "docs")
    os.makedirs(d, exist_ok=True)
    p = os.path.join(d, name + ".md")
    with open(p, "w", encoding="utf-8") as fh:
        fh.write("# fixture\n\n```csharp\n" + snippet + "\n```\n")
    return os.path.join(tmp, name)


def _run(repo, root, max_report=60):
    argv = ["--repo", repo, "--root", root, "--max-report", str(max_report)]
    import io as _io
    buf, err = _io.StringIO(), _io.StringIO()
    so, se = sys.stdout, sys.stderr
    sys.stdout, sys.stderr = buf, err
    try:
        rc = main(argv)
    finally:
        sys.stdout, sys.stderr = so, se
    return rc, buf.getvalue() + err.getvalue()


def self_test(repo):
    repo = os.path.abspath(repo)
    ok = True

    types, _files = build_symbol_table(repo)
    ctrl = types.get(CONTROL_TYPE)
    if ctrl is None or CONTROL_MEMBER not in ctrl.members:
        print("SELF-TEST REFUSE: control %s.%s not present in the symbol table -- the gate "
              "cannot be calibrated against this repo." % (CONTROL_TYPE, CONTROL_MEMBER))
        return 2

    with tempfile.TemporaryDirectory() as tmp:
        # --- ARM 1: FAIL. A phantom member on a REAL type must be named and exit 1. ------
        root = _fixture(tmp, "phantom", (
            "using Excalibur.Compliance;\n"
            "services.Configure<%s>(o =>\n{\n    o.%s = 5;\n});"
            % (CONTROL_TYPE, PHANTOM_MEMBER)))
        rc, out = _run(repo, root)
        named = PHANTOM_MEMBER in out
        print("ARM 1 (FAIL/safety)      exit=%d named-phantom=%s" % (rc, named))
        if rc != 1 or not named:
            ok = False
            print("  ! expected exit 1 naming %s" % PHANTOM_MEMBER)

        # --- ARM 2: PASS. The same shape with a REAL member must exit 0. ----------------
        root = _fixture(tmp, "real", (
            "using Excalibur.Compliance;\n"
            "services.Configure<%s>(o =>\n{\n    o.%s = 25;\n});"
            % (CONTROL_TYPE, CONTROL_MEMBER)))
        rc, out = _run(repo, root)
        clean = "VERDICT: PASS" in out
        print("ARM 2 (PASS/liveness)    exit=%d verdict-pass=%s" % (rc, clean))
        if rc != 0 or not clean:
            ok = False
            print("  ! expected exit 0; a gate that flags everything fails here")

        # --- ARM 3: FAIL via an object initializer, the other binding path. -------------
        root = _fixture(tmp, "init", (
            "using Excalibur.Compliance;\n"
            "var o = new %s { %s = 1, %s = 2 };"
            % (CONTROL_TYPE, CONTROL_MEMBER, PHANTOM_MEMBER)))
        rc, out = _run(repo, root)
        print("ARM 3 (FAIL/initializer) exit=%d named-phantom=%s" % (rc, PHANTOM_MEMBER in out))
        if rc != 1 or PHANTOM_MEMBER not in out:
            ok = False
            print("  ! expected exit 1 naming %s via the initializer path" % PHANTOM_MEMBER)

        # --- ARM 4: AMBIGUOUS is a bucket, not a hit and not a miss. --------------------
        amb = types.get(AMBIGUOUS_TYPE)
        if amb is not None and len(amb.paths) > 1 and not amb.partial:
            root = _fixture(tmp, "amb", (
                "using Excalibur.Compliance;\n"
                "var e = new %s { %s = 1 };" % (AMBIGUOUS_TYPE, PHANTOM_MEMBER)))
            rc, out = _run(repo, root)
            bucketed = "AMBIGUOUS" in out and "1 ambiguous" in out and "0 unresolved" in out
            print("ARM 4 (AMBIGUOUS bucket) exit=%d bucketed=%s (%s has %d declarations)"
                  % (rc, bucketed, AMBIGUOUS_TYPE, len(amb.paths)))
            if rc != 0 or not bucketed:
                ok = False
                print("  ! an ambiguous receiver must be bucketed, never reported as a phantom")
        else:
            print("ARM 4 (AMBIGUOUS bucket) SKIPPED -- %s is not ambiguous in this repo"
                  % AMBIGUOUS_TYPE)

        # --- ARM 5: REFUSE. No symbol table => exit 2, and REFUSE is not PASS. ----------
        empty = os.path.join(tmp, "emptyrepo")
        os.makedirs(empty, exist_ok=True)
        rc, out = _run(empty, empty)
        refused = rc == 2 and "REFUSE" in out
        print("ARM 5 (REFUSE)           exit=%d refused=%s" % (rc, refused))
        if not refused:
            ok = False
            print("  ! an unmeasurable run must exit 2, never 0")

        # --- ARM 6: the display cap must not move the count. ---------------------------
        root = _fixture(tmp, "cap", (
            "using Excalibur.Compliance;\n"
            "var o = new %s { %sA = 1, %sB = 2, %sC = 3 };"
            % (CONTROL_TYPE, PHANTOM_MEMBER, PHANTOM_MEMBER, PHANTOM_MEMBER)))
        rc_full, out_full = _run(repo, root, max_report=60)
        rc_cap, out_cap = _run(repo, root, max_report=1)
        same = ("3 unresolved" in out_full and "3 unresolved" in out_cap
                and "not displayed" in out_cap)
        print("ARM 6 (count != display)  exit=%d/%d count-stable=%s" % (rc_full, rc_cap, same))
        if not same:
            ok = False
            print("  ! a truncated display must not truncate the count")

    print("")
    print("SELF-TEST %s" % ("PASS -- FAIL, PASS, REFUSE and the AMBIGUOUS bucket are all reachable"
                            if ok else "FAILED -- a verdict is unreachable; the gate is not trustworthy"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
