#!/usr/bin/env python3
"""Census for the receipt-without-work gate. Emits tab-separated records; renders no verdict.

The shell caller decides. See receipt-without-work-gate.sh for the defect class, the oracle,
and the exit-code contract.

Records:
  ESCAPES   <TAB> file <TAB> line <TAB> method <TAB> mark <TAB> kind
                                the mark is reached on a path that skips every unit of work
  BARE      <TAB> file <TAB> line <TAB> method <TAB> mark <TAB> kind
                                the method performs no work at all and still writes the mark
  DOMINATED <TAB> file <TAB> line <TAB> method <TAB> mark
                                the mark is reached only after the work -- correct
  UNREAD    <TAB> file <TAB> line <TAB> method <TAB> mark
                                the mark escapes its guard but nobody reads it -- harmless
  MUTATOR   <TAB> file <TAB> line <TAB> method <TAB> mark
                                the mark is written by a type that holds no collaborator and so
                                cannot perform out-of-process work -- writing the mark IS its
                                contract, and the work belongs to its caller
  COUNTS    <TAB> files <TAB> methods <TAB> marks
"""
import os
import re
import sys

# ---------------------------------------------------------------------------------------------
# The MARK vocabulary. Named as a CONCEPT with its spellings enumerated, not as a list of the
# four field names someone last looked at. A mark is a durable assertion that work happened.

# spelling 1 -- a boolean done-flag. Stem-matched rather than name-matched so that _initialized,
# isStarted, SchemaCreated and _connected are one rule and not four. The leading class excludes
# a word character so an underscore-prefixed field is reached, which a word boundary is not.
FLAG_STEMS = (
    r"nitiali[sz]ed|tarted|eady|onnected|oaded|onfigured|egistered|reated|pened|"
    r"unning|repared|ootstrapped|rovisioned|ubscribed|armed|nsured"
)
RE_FLAG = re.compile(r"(?:^|[^A-Za-z0-9_])(_?[A-Za-z]*(?:" + FLAG_STEMS + r"))\s*=\s*true\s*;")

# spelling 2 -- a status enum moved to a terminal/completion member.
DONE_WORDS = (
    r"Completed|Notified|Sent|Processed|Succeeded|Done|Applied|Delivered|Published|"
    r"Erased|Resolved|Closed|Acknowledged|Committed|Provisioned|Archived|Purged|Replayed"
)
RE_STATUS = re.compile(r"\b([A-Za-z_]*Status)\s*=\s*[A-Za-z_]*\.?(?:" + DONE_WORDS + r")\b")

# spelling 3 -- a completion timestamp stamped from a clock.
RE_STAMP = re.compile(
    r"\b([A-Za-z_]*(?:" + DONE_WORDS + r")(?:At|On|Time|Timestamp))\s*=\s*"
    r"[^;]*(?:UtcNow|GetUtcNow|Now|imeProvider)"
)

# spelling 4 -- a log line asserting the work happened, past tense. "Sending" is not a receipt.
RE_LOGDONE = re.compile(r"\bLog[A-Za-z_]*(?:" + DONE_WORDS + r")\s*\(")

MARKS = (("flag", RE_FLAG), ("status", RE_STATUS), ("stamp", RE_STAMP), ("log", RE_LOGDONE))

# ---------------------------------------------------------------------------------------------
# WORK. Any invocation that is not itself a mark and not bookkeeping. A GENERIC call is
# matched too: a pattern requiring the paren to follow the identifier is blind to
# GetService<T>(...) entirely, and a work detector blind to generic calls reports the
# methods that use them as doing nothing. That blindness was found by reading a finding
# rather than by the pattern failing, which is why the arms below pin it.
# that CANNOT constitute the work a receipt attests -- guard clauses, name lookups, logging, and
# the mark writes themselves. Everything else counts as work, so the failure direction is
# under-reporting a defect, never inventing one.
RE_CALL = re.compile(r"\b([A-Za-z_][A-Za-z0-9_]*)\s*(?:<[^<>()]*>)?\s*\(")
NOT_WORK = re.compile(
    r"^(?:if|while|for|foreach|switch|catch|lock|using|return|throw|new|nameof|typeof|sizeof|"
    r"default|checked|unchecked|await|Log[A-Za-z0-9_]*|ThrowIf[A-Za-z0-9_]*|"
    r"[A-Za-z0-9_]*Exception|Debug|Trace|Assert|ConfigureAwait|Equals|GetHashCode|ToString|"
    r"Invariant|Format|get|set)$"
)

# A conditional region may execute zero times, so a mark outside one is reached regardless. A
# try/using/lock/finally block is NOT conditional -- it always runs -- and treating it as one
# would report every disposal pattern in the tree as a finding.
RE_COND_OPEN = re.compile(r"^\s*(?:\}\s*)?(?:else\s+)?(?:if|switch|catch)\b")

RE_METHOD = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|async|virtual|"
    r"override|sealed|partial|extern|unsafe|new)\s+)+"
    r"[A-Za-z_][A-Za-z0-9_<>,\[\]\?\. ]*\s+"
    r"([A-Za-z_][A-Za-z0-9_]*)\s*(?:<[^>(]*>)?\s*\([^;]*$"
)

# A type that holds no collaborator cannot perform work. Its mark-writing method is a state
# mutator whose whole contract is to record a transition the CALLER performed, so "this method
# did no work" is not a finding about it -- it is the shape of an entity. Measured on this tree:
# the DTO/entity types score 0 and every service, adapter and store scores 1 or more, so the
# separation is structural and needs no list of names.
#
# Resolved per FILE rather than per type. A file mixing an entity with a service therefore scores
# the service's collaborators and the entity's mutators stay under judgement: the approximation
# OVER-reports a subject, and can never drop one.
RE_COLLABORATOR = re.compile(
    r"^\s*(?:private|protected|internal)\s+(?:static\s+)?readonly\s"
    r"|(?:class|record|struct)\s+\w+(?:<[^>]*>)?\s*\((?!\s*\))"
)

# A TYPE declaration is not a method, and the primary-constructor form looks exactly like one:
#   internal sealed class Pump(IClient c) : IHostedService
# Left unguarded it parses as a method whose body is the ENTIRE type, so a mark in one member is
# judged against work in another. A lookahead inside the method pattern does not hold -- the
# modifier run backtracks and lets "class" be read as the return type -- so it is a separate test.
RE_TYPE_DECL = re.compile(r"\b(?:class|record|struct|interface|enum|delegate)\s")

RE_LINE_COMMENT = re.compile(r"//.*$")
RE_STRINGS = re.compile(r'@?"(?:[^"\\]|\\.)*"')


def strip(src):
    """Blank out comments and string bodies so a mark quoted in a doc comment is not a finding.

    Line count and line length are preserved so reported line numbers stay true to the file.
    """
    out = []
    in_block = False
    for raw in src.split("\n"):
        line = raw
        if in_block:
            end = line.find("*/")
            if end < 0:
                out.append("")
                continue
            line = " " * (end + 2) + line[end + 2:]
            in_block = False
        while True:
            start = line.find("/*")
            if start < 0:
                break
            end = line.find("*/", start + 2)
            if end < 0:
                line = line[:start]
                in_block = True
                break
            line = line[:start] + " " * (end + 2 - start) + line[end + 2:]
        line = RE_LINE_COMMENT.sub("", line)
        line = RE_STRINGS.sub(lambda m: '"' + " " * (len(m.group(0)) - 2) + '"', line)
        out.append(line)
    return out


def lines_of(text):
    return text.split(chr(10))


def find_mark(line):
    for kind, rx in MARKS:
        m = rx.search(line)
        if m:
            return kind, (m.group(1) if m.lastindex else m.group(0).strip())
    return None, None


def methods_of(lines):
    """Yield (name, body) where body is a list of (index, text, conditional_depth).

    Conditional depth is tracked separately from brace depth: a mark at conditional depth 0 is
    reached on every path through the method, whichever try/using/lock blocks enclose it.
    """
    n = len(lines)
    i = 0
    while i < n:
        m = RE_METHOD.match(lines[i])
        if not m or RE_TYPE_DECL.search(lines[i]):
            i += 1
            continue
        name = m.group(1)
        j, opened = i, False
        while j < n and j < i + 12:
            if "{" in lines[j]:
                opened = True
                break
            if ";" in lines[j] or "=>" in lines[j]:
                break
            j += 1
        if not opened:
            i += 1
            continue
        # Brace-by-brace, so that a conditional whose brace opens on the NEXT line -- which is
        # this tree's brace style -- is still recognised as opening a conditional region. A
        # depth counter that closed the region at the header line reads the guarded work as
        # unconditional and reports the defect as correct.
        body, stack, pending = [], [], False
        bdepth = 1
        k = j + 1
        while k < n and bdepth > 0:
            line = lines[k]
            is_header = RE_COND_OPEN.match(line) is not None
            body.append((k, line, sum(stack)))
            had_brace = False
            for ch in line:
                if ch == "{":
                    stack.append(pending)
                    pending = False
                    had_brace = True
                    bdepth += 1
                elif ch == "}":
                    if stack:
                        stack.pop()
                    had_brace = True
                    bdepth -= 1
            if not had_brace:
                if is_header:
                    pending = True
                elif line.strip():
                    pending = False
            k += 1
        yield name, body
        i = max(k, i + 1)


def classify(body, whole, has_collaborator):
    """Per mark in one method body, yield (tag, line_index, mark, kind)."""
    work_depths = [
        d for (_, ln, d) in body
        if find_mark(ln)[0] is None
        and any(not NOT_WORK.match(c) for c in RE_CALL.findall(ln))
    ]
    for (idx, ln, d) in body:
        kind, mark = find_mark(ln)
        if not kind:
            continue
        # Q2 -- is the mark READ? A boolean flag must be read somewhere in the file, or nobody
        # treats it as proof. A status, a stamp and a log ARE the observable: their reader is the
        # consumer, the dashboard or the audit trail, and no in-file evidence can rule that out.
        if kind == "flag":
            # A read is an occurrence that is neither a write of the mark nor its DECLARATION.
            # Counting the declaration as a read makes every write-only flag look consulted, and
            # the harmless case (Q1 without Q2) then reports as a defect -- a finding with no
            # victim, which is how a gate earns its own suppression.
            token = re.escape(mark)
            occurs = re.compile(r"(?<![A-Za-z0-9_])" + token + r"(?![A-Za-z0-9_])")
            writes = re.compile(token + r"\s*=[^=]")
            declares = re.compile(r"(?:bool|Boolean)[?]?\s+" + token + r"\s*[;=]")
            read = any(
                occurs.search(ln) and not writes.search(ln) and not declares.search(ln)
                for ln in lines_of(whole)
            )
        else:
            read = True
        if not work_depths and not has_collaborator:
            yield "MUTATOR", idx, mark, kind
        elif not work_depths:
            yield ("BARE" if read else "UNREAD"), idx, mark, kind
        elif d == 0 and all(wd > 0 for wd in work_depths):
            yield ("ESCAPES" if read else "UNREAD"), idx, mark, kind
        else:
            yield "DOMINATED", idx, mark, kind


def main(root):
    files = methods = marks = 0
    src_root = os.path.join(root, "src")
    if not os.path.isdir(src_root):
        return 2
    for dirpath, dirnames, filenames in os.walk(src_root):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj")]
        for fn in sorted(filenames):
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            try:
                with open(path, "r", encoding="utf-8", errors="replace") as fh:
                    src = fh.read()
            except OSError:
                continue
            if not any(rx.search(src) for _, rx in MARKS):
                continue
            files += 1
            rel = os.path.relpath(path, root).replace("\\", "/")
            lines = strip(src)
            whole = "\n".join(lines)
            has_collab = any(RE_COLLABORATOR.search(ln) for ln in lines)
            for name, body in methods_of(lines):
                methods += 1
                for tag, idx, mark, kind in classify(body, whole, has_collab):
                    marks += 1
                    row = [tag, rel, str(idx + 1), name, mark]
                    if tag in ("ESCAPES", "BARE"):
                        row.append(kind)
                    print("\t".join(row))
    print("\t".join(("COUNTS", str(files), str(methods), str(marks))))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "."))
