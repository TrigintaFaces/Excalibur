#!/usr/bin/env bash
# duplicate-xml-doc-tags.sh — fail on duplicate XML doc tags within one /// doc block.
#
# HOME: eng/ci/ (moved from .claude/harness — .claude is not published, so a gate living there
#       can never run where CI runs). This gate is the enforcement arm for the defect below.
#
# THE DEFECT: the C# compiler emits BOTH tags and warns about NEITHER when a single
# documentation block carries two <summary>, two <remarks>, or two <value> elements. The XML doc
# generator keeps only one, so half a member's documented contract silently never reaches the
# shipped NuGet .xml / IntelliSense. `dotnet-best-practices.md` forbids it; nothing enforced it.
#
# WHAT THIS GATE CHECKS: for every C# file under the scan roots, each MAXIMAL run of consecutive
# `///` lines (one doc block) must contain at most one opening <summary>, at most one <remarks>,
# and at most one <value>. A block with two of any is a VIOLATION.
#
# Generated files (*.Designer.cs, *.g.cs, *.generated.cs, anything under obj/) are excluded —
# they are not hand-authored and are not shipped as first-party doc surface.
#
# Usage:   bash eng/ci/duplicate-xml-doc-tags.sh [ROOT|FILE ...]   (default: src)
#          A ROOT is walked for *.cs; a FILE ending in .cs is scanned directly (lets the
#          pre-commit hook pass only the staged .cs files — fast, no full-tree tax per commit).
# Exit:    0 clean · 1 duplicate tag(s) found · 2 cannot evaluate (no C# files under roots)
set -uo pipefail

E_OK=0; E_VIOLATION=1; E_ENV=2
ROOTS=("${@:-src}")

python3 - "${ROOTS[@]}" <<'PY'
import os, re, sys

roots = sys.argv[1:] or ["src"]
TAGS = ("summary", "remarks", "value")
open_re = {t: re.compile(rf"<{t}\b") for t in TAGS}

def is_generated(path):
    b = os.path.basename(path).lower()
    if b.endswith((".designer.cs", ".g.cs", ".generated.cs")):
        return True
    p = path.replace("\\", "/").lower()
    return "/obj/" in p or "/bin/" in p or "generated" in b

cs_files = 0
violations = []  # (path, start_line, tag, count)

def iter_cs(roots):
    for root in roots:
        if os.path.isfile(root):
            if root.endswith(".cs"):
                yield root
            continue
        for dirpath, _, names in os.walk(root):
            for name in names:
                if name.endswith(".cs"):
                    yield os.path.join(dirpath, name)

for path in iter_cs(roots):
    if True:
        if True:
            if is_generated(path):
                continue
            cs_files += 1
            try:
                lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
            except OSError:
                continue
            block_start = None
            counts = {t: 0 for t in TAGS}
            def flush(bs, cnt):
                if bs is None:
                    return
                for t in TAGS:
                    if cnt[t] > 1:
                        violations.append((path, bs, t, cnt[t]))
            for i, ln in enumerate(lines, 1):
                s = ln.lstrip()
                if s.startswith("///"):
                    if block_start is None:
                        block_start = i
                        counts = {t: 0 for t in TAGS}
                    for t in TAGS:
                        counts[t] += len(open_re[t].findall(s))
                else:
                    flush(block_start, counts)
                    block_start = None
            flush(block_start, counts)

if cs_files == 0:
    sys.stderr.write("duplicate-xml-doc-tags: no C# files found under %s — cannot evaluate\n"
                     % ", ".join(roots))
    sys.exit(2)

if violations:
    sys.stderr.write("duplicate-xml-doc-tags: %d duplicate XML doc tag(s) — the generator drops all but one:\n"
                     % len(violations))
    for path, line, tag, count in sorted(violations):
        sys.stderr.write("    %s:%d  <%s> x%d in one doc block\n" % (path, line, tag, count))
    sys.stderr.write("  Fix: merge into a single <%s> (use <para> for multiple paragraphs).\n" % "remarks")
    sys.exit(1)

# The denominator, in the standard machine-readable form: what was EXAMINED, not only what was
# FOUND. The zero case already exits 2 above; this states the earned denominator out loud so a
# reader can tell a clean tree from a matcher that stopped matching.
print("EXAMINED: %d C# file(s)" % cs_files)
print("duplicate-xml-doc-tags: clean — %d C# files, no duplicate summary/remarks/value tags." % cs_files)
sys.exit(0)
PY
rc=$?
exit "$rc"
