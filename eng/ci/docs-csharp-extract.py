#!/usr/bin/env python3
"""docs-csharp-extract.py — extract C# code blocks from docs and phantom-scan tier-1 blocks.

Standalone, stdlib-only. Two jobs:

  1. Walk the doc surface (docs-site, docs, **/README*), extract every fenced C#
     code block, classify each as tier-1 (resolve) or tier-2 (compile), and (with
     --json) emit a JSON array of per-block records. That record IS the contract the
     downstream tier-2 full-compile gate consumes.

  2. Default (gate) mode: for every tier-1 block that establishes framework context
     (a `using Excalibur.*` / `using Dispatch.*`), flag any referenced framework type
     that does NOT exist in the repo's real public surface (PublicAPI*.txt + a
     lightweight grep of public type declarations). Exit non-zero on any phantom.

Conservative by design: it only flags identifiers used in a clear type position inside
a snippet that has already declared framework context, and never flags a name that
appears anywhere in the real-symbol set or in the BCL denylist. A phantom detector that
cries wolf gets ignored, so when unsure it does NOT flag.

Boundary: this tool does tier-1 *resolution* only. Tier-2 full compilation of `runnable`
blocks is the tier-2 gate's follow-on, not this tool.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys

# ---------------------------------------------------------------------------
# Doc walking / fence extraction
# ---------------------------------------------------------------------------

SKIP_DIRS = {"node_modules", "bin", "obj", ".git", ".dts", ".claude"}
DOC_ROOTS = ("docs-site", "docs")  # plus repo-wide README*.md
CSHARP_LANGS = {"csharp", "cs"}
FENCE_RE = re.compile(r"^(\s*)(`{3,}|~{3,})(.*)$")


def _iter_markdown_files(root: str):
    """Yield markdown/mdx files under the doc roots + any README*.md, skipping junk dirs."""
    seen = set()
    scan_roots = [os.path.join(root, d) for d in DOC_ROOTS]
    scan_roots.append(root)  # repo-wide walk for README*.md
    for base in scan_roots:
        if not os.path.isdir(base):
            continue
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
            for fn in filenames:
                low = fn.lower()
                is_readme = low.startswith("readme") and low.endswith(".md")
                is_doc = base != root and (low.endswith(".md") or low.endswith(".mdx"))
                if not (is_readme or is_doc):
                    continue
                full = os.path.join(dirpath, fn)
                if full in seen:
                    continue
                seen.add(full)
                yield full


def _info_tokens(info: str):
    return info.strip().split()


def extract_blocks(root: str, repo_root: str):
    """Return a list of block records extracted from the doc surface under `root`."""
    blocks = []
    for path in _iter_markdown_files(root):
        try:
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                lines = fh.read().split("\n")
        except OSError:
            continue
        try:
            rel = os.path.relpath(path, repo_root).replace(os.sep, "/")
        except ValueError:
            # Different drive (e.g. a temp fixture) — fall back to the absolute path.
            rel = path.replace(os.sep, "/")
        i = 0
        n = len(lines)
        while i < n:
            m = FENCE_RE.match(lines[i])
            if not m:
                i += 1
                continue
            indent, fence, info = m.group(1), m.group(2), m.group(3)
            tokens = _info_tokens(info)
            lang = tokens[0].lower() if tokens else ""
            fence_line = i + 1  # 1-based line of the opening fence
            # collect until a matching closing fence (same char, >= length, no info)
            body = []
            j = i + 1
            closed = False
            while j < n:
                cm = FENCE_RE.match(lines[j])
                if cm and cm.group(2)[0] == fence[0] and len(cm.group(2)) >= len(fence) \
                        and not _info_tokens(cm.group(3)):
                    closed = True
                    break
                body.append(lines[j])
                j += 1
            if lang in CSHARP_LANGS:
                code = "\n".join(body)
                first = body[0].strip() if body else ""
                # Opt-out: an info-string `ignore`/`no-compile` token (or a first
                # in-fence line `// no-compile`) marks a DELIBERATE teaching placeholder
                # — a snippet whose pseudo/example types are intentional. It is excluded
                # from phantom gating by declaration, and takes precedence over `runnable`.
                is_ignored = ("ignore" in tokens) or ("no-compile" in tokens) \
                    or (first == "// no-compile")
                # tier-2 (compile) iff info-string carries the `runnable` token,
                # or the first in-fence line is exactly `// compile-check`.
                is_compile = ("runnable" in tokens) or (first == "// compile-check")
                tier = "ignore" if is_ignored else ("compile" if is_compile else "resolve")
                blocks.append({
                    "file": rel,
                    "startLine": fence_line,
                    "endLine": (j + 1) if closed else n,  # 1-based closing fence line
                    "lang": "csharp",
                    "tier": tier,
                    "code": code,
                })
            i = j + 1 if closed else j
    return blocks


# ---------------------------------------------------------------------------
# Real public-surface symbol set
# ---------------------------------------------------------------------------

IDENT_RE = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")
PUBLIC_TYPE_RE = re.compile(
    r"\bpublic\b[^\n;{]*?\b(?:class|interface|record|struct|enum)\s+([A-Z][A-Za-z0-9_]*)"
)


def build_real_symbols(repo_root: str):
    """Collect every simple identifier from PublicAPI*.txt + public type decls in src/**."""
    symbols = set()
    src = os.path.join(repo_root, "src")
    # 1. Authoritative: PublicAPI*.txt lines (type + member fully-qualified names).
    if os.path.isdir(src):
        for dirpath, dirnames, filenames in os.walk(src):
            dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
            for fn in filenames:
                if fn.startswith("PublicAPI") and fn.endswith(".txt"):
                    try:
                        with open(os.path.join(dirpath, fn), "r", encoding="utf-8",
                                  errors="replace") as fh:
                            for line in fh:
                                if line.startswith("#"):
                                    continue
                                for tok in IDENT_RE.findall(line):
                                    symbols.add(tok)
                    except OSError:
                        continue
    # 2. Fallback: grep public type declarations across src/**/*.cs.
    if os.path.isdir(src):
        for dirpath, dirnames, filenames in os.walk(src):
            dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
            for fn in filenames:
                if not fn.endswith(".cs"):
                    continue
                try:
                    with open(os.path.join(dirpath, fn), "r", encoding="utf-8",
                              errors="replace") as fh:
                        for name in PUBLIC_TYPE_RE.findall(fh.read()):
                            symbols.add(name)
                except OSError:
                    continue
    return symbols


# ---------------------------------------------------------------------------
# Tier-1 phantom detection
# ---------------------------------------------------------------------------

# Framework context: a snippet that imports an Excalibur/Dispatch namespace.
FRAMEWORK_USING_RE = re.compile(r"^\s*using\s+((?:Excalibur|Dispatch)(?:\.[A-Za-z0-9_]+)*)\s*;",
                                re.MULTILINE)

# Type-position candidate patterns (PascalCase identifiers used AS types).
# Deliberately conservative: only TYPE positions + the framework-registration idiom.
# Bare `Name(` and instance `.Name(` are NOT used — they catch method definitions
# (`void Run()`) and instance calls (`svc.DoThing()`), which are noise, not types.
CANDIDATE_PATTERNS = [
    re.compile(r"\bnew\s+([A-Z][A-Za-z0-9_]*)"),            # new X(
    re.compile(r":\s*([A-Z][A-Za-z0-9_]*)"),                # : X  (base type)
    re.compile(r"<\s*([A-Z][A-Za-z0-9_]*)"),                # <X  (generic arg)
    re.compile(r",\s*([A-Z][A-Za-z0-9_]*)\s*>"),            # , X>  (generic arg)
    re.compile(r"\b([A-Z][A-Za-z0-9_]*)\s+[a-z_][A-Za-z0-9_]*\s*[=;,)]"),  # Type name =
    # X.StaticMember — negative lookbehind excludes CHAINED members (DateTime.UtcNow.X):
    re.compile(r"(?<![\w.])([A-Z][A-Za-z0-9_]*)\."),
    # NOTE: intentionally NO bare `Name(` / `.Add*(` invocation pattern. Those catch
    # BCL methods (`.AddSeconds(`, `.ConfigureAwait(`) and method definitions — pure
    # noise. Real framework extension methods (AddDispatch) live in the symbol set.
]

# Common BCL / language identifiers we never flag even if absent from the public surface.
BCL_DENYLIST = {
    # namespaces / roots
    "System", "Microsoft", "Threading", "Tasks", "Collections", "Generic", "Linq",
    "Text", "Json", "Serialization", "IO", "Net", "Http", "Extensions",
    "DependencyInjection", "Logging", "Configuration", "Hosting", "Options",
    # `Tests` is a namespace ROOT here (Tests.Shared.Fixtures) and a segment of every test
    # project's namespace -- never a type. Without it, any snippet quoting a test file's
    # `using`/`namespace` reads as a phantom API.
    "Tests",
    # very common BCL types
    "Task", "ValueTask", "CancellationToken", "CancellationTokenSource", "Guid",
    "DateTime", "DateTimeOffset", "TimeSpan", "TimeProvider", "String", "Int32",
    "Int64", "Boolean", "Object", "Exception", "Console", "Math", "Convert",
    "List", "Dictionary", "IEnumerable", "IReadOnlyList", "IReadOnlyDictionary",
    "IList", "ICollection", "Array", "Span", "Memory", "ReadOnlySpan", "ReadOnlyMemory",
    "Func", "Action", "Nullable", "Type", "Attribute", "Stream", "MemoryStream",
    "Encoding", "StringBuilder", "Regex", "HttpClient", "HttpResponseMessage",
    "HttpRequestMessage", "IServiceCollection", "IServiceProvider", "ILogger",
    "ILoggerFactory", "IConfiguration", "IHost", "IHostBuilder", "IOptions",
    "ServiceLifetime", "Enumerable", "KeyValuePair", "Tuple", "Uri", "Environment",
    "Assembly", "TimeZoneInfo", "Random", "RandomNumberGenerator", "JsonSerializer",
    "JsonSerializerOptions", "Interlocked", "Volatile", "Activity", "ActivitySource",
    "Meter", "TaskCompletionSource", "SemaphoreSlim", "Channel", "Program", "Startup",
    "Debug", "Trace", "GC", "Buffer", "BitConverter", "Path", "File", "Directory",
    "IDisposable", "IAsyncDisposable", "EventArgs", "EventHandler", "Lazy", "Comparer",
    "IEquatable", "IComparable", "Result", "Args", "Builder", "Options",
    # ASP.NET Core hosting / minimal API surface commonly shown in examples
    "WebApplication", "WebApplicationBuilder", "ServiceCollection", "ControllerBase",
    "IApplicationBuilder", "IEndpointRouteBuilder", "HttpContext", "IResult", "Results",
    "MapPost", "MapGet", "MapPut", "MapDelete", "Ok", "BadRequest", "NotFound",
    # common exceptions
    "InvalidOperationException", "ArgumentException", "ArgumentNullException",
    "NotSupportedException", "NotImplementedException", "OperationCanceledException",
    "TimeoutException", "AggregateException", "ApplicationException",
    # test frameworks / misc BCL shown in examples
    "Assert", "Should", "Xunit", "Fact", "Theory", "Mock", "It", "UTF8", "ASCII",
    "Unicode", "CultureInfo", "Stopwatch", "Process", "Thread", "Encoding",
    # System.Text.Json family commonly configured in examples
    "JsonNamingPolicy", "JsonStringEnumConverter", "JsonIgnoreCondition",
    "JsonConverter", "JsonPropertyName", "JsonSerializerContext", "JsonNode",
    "JsonDocument", "JsonElement", "JsonWriterOptions", "Utf8JsonWriter",
    "Utf8JsonReader", "JsonNumberHandling",
    # more common hosting / auth / data BCL + framework-external types in examples
    "BackgroundService", "IHostedService", "TokenValidationParameters", "DataRow",
    "IAsyncLifetime", "ClaimsPrincipal", "ClaimsIdentity", "Claim", "DbConnection",
    "DbContext", "SqlConnection", "IDbConnection", "DateOnly", "TimeOnly", "Uri",
}

# Types DEFINED inside a snippet are local example declarations, never phantoms.
SNIPPET_TYPE_DECL_RE = re.compile(
    r"\b(?:class|interface|record|struct|enum)\s+([A-Z][A-Za-z0-9_]*)")
# Generic type-parameter convention (TMessage, TResponse, TKey) — not a real type.
GENERIC_PARAM_RE = re.compile(r"^T[A-Z]")


# A string literal is DATA, never an API reference. Without this, any dotted text inside quotes is
# read as a qualified type: a declared message name like [MessageName("Contoso.Sales.OrderPlaced")]
# reported "Contoso" as a phantom API, as would a connection string, a URL, a topic name or a JSON
# path. The candidate patterns below are deliberately loose because real code is loose; the fix is to
# stop feeding them text that cannot contain a type reference in the first place.
# Raw string literals first: """...""" spans lines and is exactly how SQL gets embedded in C#,
# so without it every SELECT/WHERE/AND reads as a phantom type.
RAW_STRING_RE = re.compile(r'"{3,}.*?"{3,}', re.DOTALL)
STRING_LITERAL_RE = re.compile(
    r'@?"(?:[^"\
]|\.)*"'   # "..." and @"..."
    r"|'(?:[^'\
]|\.)*'"  # 'c'
)


# Comments, like string literals, cannot contain a live type reference. Without this, a filename in
# a comment ("defined in Messages/OrderActions.cs below") is read as a qualified type and reported
# as a missing framework API. Stripped after raw strings and before literals so a // inside a string
# is not mistaken for a comment.
LINE_COMMENT_RE = re.compile(r"//[^\n]*")
BLOCK_COMMENT_RE = re.compile(r"/\*.*?\*/", re.DOTALL)


def _strip_string_literals(code: str) -> str:
    """Blank out literals and comments, preserving newlines so line numbers stay accurate."""
    def blank(m):
        return "".join(ch if ch == chr(10) else " " for ch in m.group(0))

    code = RAW_STRING_RE.sub(blank, code)
    code = STRING_LITERAL_RE.sub(blank, code)
    code = BLOCK_COMMENT_RE.sub(blank, code)
    return LINE_COMMENT_RE.sub(blank, code)


def _referenced_candidates(code: str):
    cands = set()
    code = _strip_string_literals(code)
    for pat in CANDIDATE_PATTERNS:
        for name in pat.findall(code):
            cands.add(name)
    return cands


def scan_block_for_phantoms(block: dict, real_symbols: set, file_declared: set | None = None):
    """Return list of (name,) phantom type names in a single tier-1 block, or []."""
    code = block["code"]
    if not FRAMEWORK_USING_RE.search(code):
        return []  # no framework context established -> do not scan (low-FP policy)
    # Types declared ANYWHERE in the same document count as declared here. A tutorial is one
    # continuous narrative: a record introduced in "Step 2: Define the events" is plainly in scope
    # for "Step 3: Build the aggregate", and a reader following along has it. Scoping declarations
    # to a single fence reported every such type as a missing framework API -- six of them in one
    # tutorial -- which is a property of how documentation is written, not a defect in the docs.
    defined = set(SNIPPET_TYPE_DECL_RE.findall(code))
    if file_declared:
        defined |= file_declared
    phantoms = []
    for name in sorted(_referenced_candidates(code)):
        if name in real_symbols:
            continue
        if name in defined:
            continue
        if name in BCL_DENYLIST:
            continue
        if GENERIC_PARAM_RE.match(name):
            continue
        # keyword-ish / too-generic guards
        if len(name) < 3:
            continue
        phantoms.append(name)
    return phantoms


# ---------------------------------------------------------------------------
# Diff-scope loaders
# ---------------------------------------------------------------------------

def _load_gate_files(path):
    """Load a newline-delimited set of repo-relative doc paths (whole-file scope)."""
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as fh:
            return {ln.strip().replace(os.sep, "/") for ln in fh if ln.strip()}
    except OSError:
        return set()


def _load_gate_lines(path):
    """Load `relpath:linenumber` entries into {path: set(changed_line_numbers)} (hunk scope)."""
    out = {}
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as fh:
            for ln in fh:
                ln = ln.strip()
                if not ln or ":" not in ln:
                    continue
                rel, _, num = ln.rpartition(":")
                rel = rel.strip().replace(os.sep, "/")
                try:
                    out.setdefault(rel, set()).add(int(num))
                except ValueError:
                    continue
    except OSError:
        return {}
    return out


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main(argv=None):
    ap = argparse.ArgumentParser(description="Extract + phantom-scan C# doc snippets.")
    ap.add_argument("--root", default=None,
                    help="Doc-walk root (default: --repo). Scopes ONLY the doc scan.")
    ap.add_argument("--repo", default=".",
                    help="Repo root used to resolve the real public surface (default: cwd).")
    ap.add_argument("--json", action="store_true",
                    help="Emit the block records array (no phantom gating).")
    ap.add_argument("--gate-files", default=None,
                    help="Path to a newline-delimited file of repo-relative doc paths. "
                         "When set, phantom gating is restricted to blocks in those files "
                         "(whole-file scope); symbol resolution stays repo-wide. "
                         "Absent = gate the whole doc surface.")
    ap.add_argument("--gate-lines", default=None,
                    help="Path to a file of `relpath:linenumber` entries (one per changed "
                         "line). When set, gating is restricted to blocks whose fenced span "
                         "intersects a changed line (HUNK scope) — a snippet is gated only "
                         "if the diff actually touched it. Takes precedence over --gate-files.")
    args = ap.parse_args(argv)

    repo_root = os.path.abspath(args.repo)
    doc_root = os.path.abspath(args.root) if args.root else repo_root

    blocks = extract_blocks(doc_root, repo_root)

    # Diff-scope is applied to BOTH outputs. It used to be built below, after the early return
    # for --json, so `--json --gate-files X` accepted the flag and emitted every block in the
    # tree -- a whole-tree answer wearing a diff-scoped invocation, and silent about it.
    gate_files = _load_gate_files(args.gate_files) if args.gate_files else None
    gate_lines = _load_gate_lines(args.gate_lines) if args.gate_lines else None

    def in_scope(b):
        if gate_lines is not None:
            changed = gate_lines.get(b["file"])
            if not changed:
                return False
            lo, hi = b["startLine"], b.get("endLine", b["startLine"])
            return any(lo <= L <= hi for L in changed)
        if gate_files is not None:
            return b["file"] in gate_files
        return True

    if args.json:
        json.dump([b for b in blocks if in_scope(b)], sys.stdout, indent=2)
        sys.stdout.write("\n")
        return 0

    # Gate mode: phantom-scan tier-1 blocks.
    real_symbols = build_real_symbols(repo_root)
    files = {b["file"] for b in blocks}
    t1 = [b for b in blocks if b["tier"] == "resolve"]
    t2 = [b for b in blocks if b["tier"] == "compile"]
    ignored = [b for b in blocks if b["tier"] == "ignore"]

    # Declarations are file-scoped: gather every type any snippet in a document declares, then let
    # each block in that document see them. Cross-block references are how tutorials read.
    declared_by_file: dict = {}
    for b in blocks:
        declared_by_file.setdefault(b["file"], set()).update(
            SNIPPET_TYPE_DECL_RE.findall(b["code"]))

    phantom_count = 0
    for b in t1:
        if not in_scope(b):
            continue
        for name in scan_block_for_phantoms(b, real_symbols, declared_by_file.get(b["file"])):
            phantom_count += 1
            print(f"{b['file']}:{b['startLine']}: phantom API '{name}' "
                  f"— not found in public surface")

    if gate_lines is not None:
        scope = f"; hunk-scoped to {len(gate_lines)} changed file(s)"
    elif gate_files is not None:
        scope = f"; scoped to {len(gate_files)} changed file(s)"
    else:
        scope = ""
    print(f"{len(blocks)} blocks ({len(t1)} T1 resolve / {len(t2)} T2 compile / "
          f"{len(ignored)} ignored) across {len(files)} files{scope}; "
          f"{phantom_count} phantom(s)")
    return 1 if phantom_count else 0


if __name__ == "__main__":
    sys.exit(main())
