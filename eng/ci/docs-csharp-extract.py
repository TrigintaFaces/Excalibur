#!/usr/bin/env python3
"""Extract Markdown C# fences and diff scope. C# binding belongs to RepositoryChecks."""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
import subprocess

# ---------------------------------------------------------------------------
# Doc walking / fence extraction
# ---------------------------------------------------------------------------

# Build output and package caches by name; every DOT-directory by rule. The rule replaces an
# enumerated list of internal directories, which went stale the moment another one appeared.
SKIP_DIRS = {"node_modules", "bin", "obj", "packages"}
DOC_ROOTS = ("docs-site", "docs")  # plus repo-wide README*.md
CSHARP_LANGS = {"csharp", "cs"}
FENCE_RE = re.compile(r"^(\s*)(`{3,}|~{3,})(.*)$")


def _iter_markdown_files(root: str, repo_root: str):
    """Yield markdown/mdx files under the doc roots + any README*.md, skipping junk dirs."""
    seen = set()
    scan_roots = [os.path.join(root, d) for d in DOC_ROOTS]
    scan_roots.append(root)  # repo-wide walk for README*.md
    repository = os.path.normcase(os.path.abspath(repo_root))
    generated = [os.path.join(repository, name) for name in ("artifacts", "graphify-out")]
    for base in scan_roots:
        normalized = os.path.normcase(os.path.abspath(base))
        if any(normalized == path or normalized.startswith(path + os.sep) for path in generated):
            continue
        if not os.path.isdir(base):
            continue
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS and not d.startswith(".")]
            if os.path.normcase(os.path.abspath(dirpath)) == repository:
                # Generated release evidence and knowledge-graph copies are not source documentation.
                # Limit this exclusion to repository roots; source docs with these names still count.
                dirnames[:] = [d for d in dirnames if d not in {"artifacts", "graphify-out"}]
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
    for path in _iter_markdown_files(root, repo_root):
        try:
            with open(path, "r", encoding="utf-8", errors="strict") as fh:
                lines = fh.read().split("\n")
        except OSError:
            raise
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



def _load_gate_files(path):
    with open(path, encoding="utf-8-sig") as handle:
        return {line.strip().replace("\\", "/") for line in handle if line.strip()}


def _load_gate_lines(path):
    result = {}
    with open(path, encoding="utf-8-sig") as handle:
        for line in handle:
            if not line.strip():
                continue
            file, separator, number = line.strip().rpartition(":")
            if not separator or int(number) <= 0:
                raise ValueError("Malformed changed-line entry: " + line)
            result.setdefault(file.replace("\\", "/"), set()).add(int(number))
    return result


def _git(repo, *args):
    result = subprocess.run(["git", "-C", repo, "-c", "core.quotepath=false", *args],
                            capture_output=True, text=True, encoding="utf-8", check=True)
    return result.stdout


def changed_lines(repo, base):
    _git(repo, "rev-parse", "--verify", base + "^{commit}")
    files = _git(repo, "diff", "--name-only", "--diff-filter=AM", "-z", base, "--").split("\0")
    result = {}
    for file in filter(None, files):
        if not file.lower().endswith((".md", ".mdx")):
            continue
        diff = _git(repo, "diff", "--no-ext-diff", "--no-color", "-U0", base, "--", file)
        for match in re.finditer(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@", diff, re.MULTILINE):
            start, count = int(match.group(1)), int(match.group(2) or 1)
            result.setdefault(file, set()).update(range(start, start + count))
    untracked = set(filter(None, _git(repo, "ls-files", "--others", "--exclude-standard", "-z", "--", "*.md", "*.mdx").split("\0")))
    return result, untracked


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", default=".")
    parser.add_argument("--root")
    parser.add_argument("--json", action="store_true", help="Emit selected fence records; no C# analysis.")
    parser.add_argument("--request", action="store_true", help="Emit all contextual records with explicit selected flags.")
    parser.add_argument("--base", help="Git base for changed-line scope, including local working-tree changes.")
    parser.add_argument("--gate-files")
    parser.add_argument("--gate-lines")
    args = parser.parse_args(argv)
    repo = os.path.abspath(args.repo)
    blocks = extract_blocks(os.path.abspath(args.root) if args.root else repo, repo)
    files = _load_gate_files(args.gate_files) if args.gate_files else None
    lines = _load_gate_lines(args.gate_lines) if args.gate_lines else None
    untracked = set()
    if args.base and files is None and lines is None:
        lines, untracked = changed_lines(repo, args.base)

    def selected(block):
        if block["file"] in untracked:
            return True
        if lines is not None:
            return any(block["startLine"] <= line <= block["endLine"] for line in lines.get(block["file"], ()))
        return files is None or block["file"] in files

    if not (args.json or args.request):
        print("REFUSE: this tool only extracts Markdown. Run RepositoryChecks --documentation for compiler validation.", file=sys.stderr)
        return 2
    result = [{**block, "selected": selected(block)} for block in blocks] if args.request else [block for block in blocks if selected(block)]
    json.dump(result, sys.stdout, indent=2)
    print()
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        print("REFUSE: Markdown/diff extraction: " + str(error), file=sys.stderr)
        sys.exit(2)
