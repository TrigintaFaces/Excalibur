#!/usr/bin/env python3
"""Read `bd list -p 0 --json` on stdin; partition the P0 set into src/product vs
tooling/process by a FIXED, documented rule, and print a machine-readable denominator.

WHY THIS EXISTS (qc7mhv)
  A "src P0" count used to be a HAND-classification: someone eyeballed each open P0 and
  decided whether it was a product bug or a tooling/process bug. That is neither
  reproducible nor auditable -- two people get two denominators, and the number carries no
  signal. This applies ONE fixed predicate to every bead, so the count is a function of the
  tracker state alone. No human is in the loop per count.

  Kept in its own file (not an inline `python3 - <<'PY'` heredoc) for the same reason as
  _dupcheck.py: a heredoc becomes the SCRIPT on stdin and silently replaces the piped
  tracker data, so the classifier would read nothing and report a confident, empty answer.

THE RULE (documented, deterministic -- change it here, in the open, never per-count)
  1. An explicit label wins: `area:src` -> src ; `area:tooling`/`area:infra`/`area:process`
     -> tooling. Labels are the authoritative signal; the heuristic is only the fallback for
     the (historically 38/40) unlabelled beads.
  2. Otherwise score title+description tokens against two fixed keyword sets and take the
     higher score. A tie or a zero score is UNCLASSIFIED -- reported explicitly, never
     silently bucketed. An honest "I can't tell" beats a guess folded into the denominator.

Output (stdout), machine-readable first line then a human breakdown:
  total=<N> src=<S> tooling=<G> unclassified=<U>

Exit 0 = classified (0+ unclassified printed).  Exit 3 = tracker unreadable (caller must not
treat an unreadable tracker as an empty one).
"""
import json
import re
import sys

# Product/source signals: the code the framework SHIPS.
SRC = {
    "src/", ".cs", ".csproj", "aggregate", "aggregateroot", "idomainevent", "eventstore",
    "event store", "eventsourced", "eventsourcing", "snapshot", "outbox", "saga", "dispatcher",
    "middleware", "pipeline", "serializer", "serialization", "repository", "projection",
    "leaderelection", "leader election", "dapper", "sqlserver", "postgres", "cosmos", "mongo",
    "namespace", "public api", "publicapi", "nuget", "aot", "trimmer", "handler", "transport",
    "idatarequest", "concurrencyexception", "domain", "excalibur.a3", "excalibur.domain",
}
# Tooling/process/infra signals: the machinery AROUND the code, not shipped.
TOOL = {
    "eng/", ".claude", ".github", "ci.yml", "workflow", "gate", "hook", "pre-commit",
    "bd-file", "bd create", "bd list", "bd update", "beads", "daemon", "tracker", "jsonl",
    "opcom", "premise", "premise-triage", "shard", "sprint", "retro", "debrief", "mission",
    "harness", "self-test", "selftest", "denominator", "accounting", "flush", "export",
    "reservation", "capsule", "discovery log", "log-discovery", "changelog", "governance",
}

_TOK = re.compile(r"[a-z0-9_.:/-]{2,}")


def _score(text, vocab):
    t = text.lower()
    # Substring match, not token equality: signals like "src/" or "event store" span a token
    # boundary. A fixed vocabulary of substrings keeps it deterministic.
    return sum(1 for kw in vocab if kw in t)


def classify(issue):
    labels = [str(x).lower() for x in (issue.get("labels") or [])]
    if "area:src" in labels:
        return "src"
    if any(l in labels for l in ("area:tooling", "area:infra", "area:process")):
        return "tooling"
    text = "%s\n%s" % (issue.get("title") or "", issue.get("description") or "")
    s, g = _score(text, SRC), _score(text, TOOL)
    if s > g:
        return "src"
    if g > s:
        return "tooling"
    return "unclassified"


def main():
    raw = sys.stdin.read()
    start = raw.find("[")
    if start < 0:
        return 3
    try:
        issues = json.loads(raw[start:])
    except json.JSONDecodeError:
        return 3

    buckets = {"src": [], "tooling": [], "unclassified": []}
    for i in issues:
        buckets[classify(i)].append(i.get("id") or "?")

    n_src, n_tool, n_unc = len(buckets["src"]), len(buckets["tooling"]), len(buckets["unclassified"])
    print("total=%d src=%d tooling=%d unclassified=%d" % (len(issues), n_src, n_tool, n_unc))
    for name in ("src", "tooling", "unclassified"):
        if buckets[name]:
            print("  %-13s %s" % (name + ":", " ".join(sorted(buckets[name]))))
    return 0


if __name__ == "__main__":
    sys.exit(main())
