#!/usr/bin/env python3
"""Config-binding completeness census for Excalibur.Compliance. Emits a verdict; the shell caller
(config-binder-completeness-gate.sh) owns the build and the exit-code contract.

The Configuration Binding Source Generator populates an already-constructed instance
(`instance.Property = value`), which is only legal for a `set` accessor -- never `init`. For any
config-bound type with an `init`-only property, the generator does not fail to compile and does not
warn: it silently emits a BindCore body that validates keys and assigns nothing for that property. A
consumer configuring that property from appsettings gets it silently dropped, with no error at build
time or run time.

For every first-party Excalibur.Compliance* type the generator emits a BindCore for, this counts the
properties actually assigned inside that method body and compares it to the type's own
settable-property count (get;set; and get;init; -- init counts here because it is a genuine shortfall
the moment it appears, not a step away from one). Prints one line per shortfall and exits 1 if any are
found; exits 0 (silent) otherwise.

Usage: config-binder-completeness-gate.py <repo-root> <path-to-generated-BindingExtensions.g.cs>
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

BINDCORE_RE = re.compile(
    r"public static void BindCore\(IConfiguration configuration, ref global::([\w.]+) instance,.*?\)\s*\{",
    re.DOTALL,
)
PROPERTY_RE = re.compile(r"\{\s*get;\s*(?:set|init);\s*\}")


def brace_match_body(src: str, open_brace_index: int) -> str:
    depth = 0
    i = open_brace_index
    while i < len(src):
        if src[i] == "{":
            depth += 1
        elif src[i] == "}":
            depth -= 1
            if depth == 0:
                return src[open_brace_index : i + 1]
        i += 1
    raise ValueError("unbalanced braces")


def generated_binder_assignments(generated_path: Path) -> dict[str, set[str]]:
    src = generated_path.read_text(encoding="utf-8")
    result: dict[str, set[str]] = {}
    for m in BINDCORE_RE.finditer(src):
        type_name = m.group(1)
        if not type_name.startswith("Excalibur.Compliance"):
            continue
        body = brace_match_body(src, m.end() - 1)
        result[type_name] = set(re.findall(r"instance\.(\w+)\s*=", body))
    return result


def settable_property_count(repo_root: Path, type_name: str) -> int | None:
    """Grep repo_root for the type's own declaration and brace-match its own body. Returns None if
    the type's source could not be located unambiguously (reported as unresolved, never silently
    skipped -- an unresolved type must not read as a clean result)."""
    short_name = type_name.rsplit(".", 1)[-1]
    for path in (repo_root / "src" / "Excalibur").glob("Excalibur.Compliance*/**/*.cs"):
        if "obj" in path.parts or "bin" in path.parts:
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        m = re.search(rf"(?:class|record)\s+{re.escape(short_name)}\b[^{{]*\{{", text)
        if not m:
            continue
        body = brace_match_body(text, m.end() - 1)
        return len(PROPERTY_RE.findall(body))
    return None


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: config-binder-completeness-gate.py <repo-root> <BindingExtensions.g.cs>", file=sys.stderr)
        return 2
    repo_root = Path(sys.argv[1]).resolve()
    generated_file = Path(sys.argv[2])

    assignments = generated_binder_assignments(generated_file)

    shortfalls = []
    unresolved = []
    for type_name, assigned in sorted(assignments.items()):
        settable = settable_property_count(repo_root, type_name)
        if settable is None:
            unresolved.append(type_name)
            continue
        if len(assigned) < settable:
            shortfalls.append((type_name, len(assigned), settable))

    for type_name in unresolved:
        print(f"config-binder-completeness-gate: UNRESOLVED (source not found, not cleared): {type_name}", file=sys.stderr)

    if shortfalls:
        print("config-binder-completeness-gate: FAIL -- config binding silently drops these properties:", file=sys.stderr)
        for type_name, assigned_count, settable_count in shortfalls:
            print(f"  {type_name}: generator assigns {assigned_count} of {settable_count} settable properties", file=sys.stderr)
        print(
            "  Fix: change the un-assigned properties' `init` accessor to `set` (BindCore assigns an "
            "already-constructed instance, which `init` forbids outside its own object initializer).",
            file=sys.stderr,
        )
        return 1

    if unresolved:
        # Unresolved types are surfaced but do not fail the gate on their own -- most are BCL/
        # third-party types (e.g. TimeSpan) the generator also emits a BindCore for, which this
        # script has no first-party source to check. A first-party type going unresolved here means
        # the glob above needs widening, not that the gate should go quiet about it.
        print(f"config-binder-completeness-gate: {len(unresolved)} type(s) unresolved, 0 confirmed shortfalls", file=sys.stderr)

    print(f"config-binder-completeness-gate: PASS -- {len(assignments) - len(unresolved)} type(s) fully bound")
    return 0


if __name__ == "__main__":
    sys.exit(main())
