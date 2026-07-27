#!/usr/bin/env python3
"""ddl-pack-completeness — every package that SHIPS SQL DDL must PACK it into its NuGet package.

WHAT THIS EXISTS TO PREVENT
---------------------------
A package carries the .sql schema a consumer needs in order to run it, the .sql sits in the source
tree, and the csproj never packs it. The framework does not auto-create tables, so a consumer who
installs the package cannot obtain the schema at all: the file exists for us and not for them.
Nothing fails. The build is green, the package is valid, and the omission produces no line
anywhere -- an unshipped file is silence, not an error.

Measured across the source tree: 14 packages ship .sql, 5 pack it, 9 do not. The 5 that pack
correctly are the control that the pattern is established and cheap to follow.

THE THREE STATES -- REFUSE IS NOT A PASS
  0 PASS     every package that ships .sql also packs it
  1 FAIL     at least one package ships .sql and does not pack it
  2 REFUSE   this checker could not build its population, so it measured NOTHING

REFUSE exists because the failure mode of a completeness checker is to go green when its own
population is empty: "0 unpacked packages" is trivially true over 0 packages. A safety assertion
("no package ships unpacked DDL") is fully satisfied by a checker that looks at nothing at all, so
an empty population is a hard REFUSE rather than a pass.

THE PREDICATE IS DERIVED FROM THE ARTIFACT, NEVER FROM A DIRECTORY CONVENTION
  ships DDL : any *.sql exists anywhere under the package directory
  packs DDL : the csproj has a <None>/<Content> item whose Include path ends in .sql
              AND that same item carries Pack="true"

Both halves are deliberate, and both were arrived at by getting it wrong first:

  A directory-keyed query (Include="Scripts...) is STRUCTURALLY BLIND, not merely narrow. Two
  packages ship DDL outside a Scripts/ dir -- one under Sql/, one under a feature folder -- so no
  amount of widening a Scripts/-keyed pattern can surface them. They cannot appear in the result.
  Keying on the artifact (*.sql anywhere) has no such blind spot.

  A loose search for Pack="true" anywhere in the csproj reports EVERY package as packed, because
  README and icon items carry Pack="true" too. The attribute must be read off the same item whose
  Include is the .sql -- hence the per-item parse below rather than a file-level search.

Two earlier counts of this population were wrong in OPPOSITE directions and both looked plausible.
That is why the self-test carries controls in both directions: a known-packed fixture must read
PACKED and a known-unpacked one must read UNPACKED. A detector that can only report one verdict is
not measuring anything.

WHY PYTHON AND NOT SHELL
------------------------
The first implementation was a shell script and it could not finish. Not slow -- unable: it spawned
roughly seven processes per csproj, and on a Windows checkout with real-time AV scanning that is
seconds per project across ~200 projects. A 300s bound expired 8 packages in and exited 124, which
is the BOUND speaking and not a verdict about the tree. A gate whose honest answer arrives after
the timeout everyone gives it will be read as green by someone. Single process, one filesystem
walk, no per-file subprocesses.

THE SECOND HALF: A PACKAGE THAT SHIPS NOTHING IS INVISIBLE TO THE CHECK ABOVE
-----------------------------------------------------------------------------
The scan above skips any package with zero .sql before it can be judged (`if count == 0`).
That is correct for its own question -- you cannot fail to pack a file you do not have -- but it
makes the checker STRUCTURALLY BLIND to the worse case, which is a package that ships no schema
AT ALL while its provider siblings ship one. `Excalibur.EventSourcing.SqlServer` is exactly that:
Oracle and Postgres both ship and pack an event-store schema, SqlServer ships none, and the only
copies of the SqlServer DDL live in samples/ and docs-site. No amount of widening the packing
predicate surfaces it -- the package cannot appear in that result.

So parity is a SEPARATE question asked over a DIFFERENT population, not a wider version of the
first one:

  ships-nothing gap : a package whose family has >=2 RELATIONAL provider siblings, where at least
                      one sibling ships .sql and this one ships none

RELATIONAL is load-bearing and is the whole difference between a gate people keep and a gate
people suppress. The naive form -- "any provider sibling" -- reports 6 findings on this tree and
3 are noise: Redis, MongoDB, DynamoDb and Firestore stores have no DDL to ship, so flagging them
is crying wolf on every document store. Restricted to relational providers it reports 3 families
and every one is a real consumer-facing gap.

USAGE
  ddl-pack-completeness.py              scan the source tree, report, exit 0/1/2
  ddl-pack-completeness.py --self-test  prove this gate is non-vacuous (safety AND liveness)
"""

from __future__ import annotations

import fnmatch
import os
import re
import sys
import tempfile

REPO = os.path.dirname(os.path.dirname(os.path.abspath(os.path.dirname(__file__))))

# <None ...> / <Content ...> opening tags, captured whole so attributes are read per item.
_ITEM = re.compile(r"<(?:None|Content)\b[^>]*>", re.IGNORECASE | re.DOTALL)
_INCLUDE = re.compile(r'Include\s*=\s*"([^"]*)"', re.IGNORECASE)
_PACK_TRUE = re.compile(r'Pack\s*=\s*"true"', re.IGNORECASE)

# Build output, not shipped source. A .sql copied into obj/ or bin/ is not evidence that the
# package ships one, and counting it would inflate the population with artifacts of a prior build.
_PRUNE = {"obj", "bin", ".git", "node_modules"}


def src_root() -> str:
    """Resolved per call, never captured once at import.

    An earlier revision baked this into a module-level constant, so the self-test's fixture root
    was ignored and every arm silently scanned the real tree. The arms still went red and green in
    the expected order, so nothing looked wrong -- a positive control passes just as happily
    against the wrong subject as the right one. The resolved root is printed with the result below
    rather than assumed.
    """
    return os.environ.get("DDL_PACK_SRC_ROOT") or os.path.join(REPO, "src")


def packs_ddl(csproj_text: str, pkgdir: str, shipped: list[str]) -> bool:
    """True when a packed .sql item exists AND its Include glob actually matches a shipped file.

    The glob check is not belt-and-braces, it closes a hole this gate had: a csproj can declare
    `Include="Scripts\\*.sql" Pack="true"` while the package's DDL lives under `Sql/`. The
    declaration is present, so a declaration-only predicate reports PACKED — and the package ships
    nothing. That is the advertised-but-unwired shape, in the gate written to prevent it. Verified
    against the observable once by packing a real project and reading the .sql out of the .nupkg;
    this check is what makes it hold without a pack step.
    """
    for tag in _ITEM.findall(csproj_text):
        include = _INCLUDE.search(tag)
        if not (include and include.group(1).lower().endswith(".sql") and _PACK_TRUE.search(tag)):
            continue
        pattern = include.group(1).replace("\\", os.sep).replace("/", os.sep)
        # MSBuild's ** is a recursive-any; fnmatch's * already crosses separators, so a plain
        # translation is adequate for "does this glob select at least one shipped .sql".
        pattern = pattern.replace("**" + os.sep, "*").replace("**", "*")
        full = os.path.join(pkgdir.rstrip(os.sep), pattern)
        if any(fnmatch.fnmatch(s, full) for s in shipped):
            return True
    return False


# Providers whose stores are backed by a relational engine and therefore need shipped DDL. A
# document/KV store ships none by design, so including it here would make the parity check fire on
# every MongoDB and Redis package -- noise that gets a gate suppressed rather than fixed.
_RELATIONAL = {"sqlserver", "postgres", "oracle", "sqlite", "mysql"}

def baseline_path() -> str:
    """Resolved per call, never captured at import — the same trap `src_root` documents.

    A module-level constant here would point the self-test's arms at the REAL baseline while they
    scan a fixture tree, so every arm would report the five real gaps as 'stale' and the arms would
    still go red and green in roughly the expected order. Wrong subject, plausible output.
    """
    return os.environ.get("DDL_PARITY_BASELINE") or os.path.join(
        os.path.dirname(os.path.abspath(__file__)), "ddl-parity.baseline.txt"
    )


def _read_baseline(path: str) -> set[str] | None:
    """Known gaps. None means unreadable -> REFUSE, never an empty set.

    An unreadable baseline read as "no entries" would turn every tracked gap into a fresh FAIL and
    the gate would be reverted within the hour. Read as "everything forgiven" it would suppress the
    real regressions. Neither guess is acceptable, so the checker declines to measure.
    """
    try:
        with open(path, encoding="utf-8") as fh:
            return {
                line.strip()
                for line in fh
                if line.strip() and not line.lstrip().startswith("#")
            }
    except OSError:
        return None


def parity_gaps(csprojs: list[str], sqls: list[str]) -> dict[str, dict[str, int]]:
    """family -> {provider: sql-count} for RELATIONAL providers only.

    Keyed off the package name, so a family is derived mechanically rather than hand-listed; a
    hand-listed family set is the shape that has failed on this tree four separate times.
    """
    families: dict[str, dict[str, int]] = {}
    for csproj in csprojs:
        parts = os.path.basename(csproj)[: -len(".csproj")].split(".")
        if len(parts) < 3 or parts[-1].lower() not in _RELATIONAL:
            continue
        pkgdir = os.path.dirname(csproj) + os.sep
        families.setdefault(".".join(parts[:-1]), {})[parts[-1]] = sum(
            1 for s in sqls if s.startswith(pkgdir)
        )
    return families


def check_parity(csprojs: list[str], sqls: list[str], out, err) -> int:
    path = baseline_path()
    baseline = _read_baseline(path)
    if baseline is None:
        print(f"::error:: REFUSE - cannot read parity baseline: {path}", file=err)
        print("Nothing was measured for parity. This is not a pass.", file=err)
        return 2

    gaps: list[str] = []
    for family, provs in sorted(parity_gaps(csprojs, sqls).items()):
        if len(provs) < 2:
            continue  # no sibling to compare against; parity is undefined, not satisfied
        shipping = {p: n for p, n in provs.items() if n > 0}
        if not shipping:
            continue  # nobody in the family ships DDL -- that is a design choice, not a gap
        gaps.extend(f"{family}:{p}" for p, n in sorted(provs.items()) if n == 0)

    fresh = sorted(set(gaps) - baseline)
    stale = sorted(baseline - set(gaps))

    print(
        f"parity: relational sibling gaps={len(gaps)}  baselined={len(baseline)}  "
        f"new={len(fresh)}  stale-baseline={len(stale)}",
        file=out,
    )

    if fresh:
        print(f"::error:: {len(fresh)} package(s) ship NO DDL while a relational sibling does:", file=err)
        for g in fresh:
            print(f"::error::   {g}", file=err)
        print(
            "\nA consumer installing these cannot obtain the schema at all, and the framework does\n"
            "not auto-create tables. Ship the DDL (and pack it), or add the gap to\n"
            f"{os.path.basename(path)} with the bead that will close it.",
            file=err,
        )
        return 1

    if stale:
        # The baseline is checked in BOTH directions on purpose. A forgiveness list that only ever
        # grows stops being a record of known gaps and becomes a place regressions hide.
        print(f"::error:: {len(stale)} baseline entr(ies) are no longer gaps - prune them:", file=err)
        for g in stale:
            print(f"::error::   {g}", file=err)
        return 1

    return 0


def walk(root: str) -> tuple[list[str], list[str]]:
    """One filesystem pass -> (csproj paths, .sql paths). No per-package re-walk."""
    csprojs: list[str] = []
    sqls: list[str] = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in _PRUNE]
        for name in filenames:
            lowered = name.lower()
            if lowered.endswith(".csproj"):
                csprojs.append(os.path.join(dirpath, name))
            elif lowered.endswith(".sql"):
                sqls.append(os.path.join(dirpath, name))
    return sorted(csprojs), sorted(sqls)


def scan(out=sys.stdout, err=sys.stderr) -> int:
    root = src_root()
    # The resolved root is part of the finding, not context. A count with no stated subject cannot
    # be reproduced or challenged.
    print(f"scanning: {root}", file=out)

    if not os.path.isdir(root):
        print(f"::error:: REFUSE - source root does not exist: {root}", file=err)
        print("Nothing was measured. This is not a pass.", file=err)
        return 2

    csprojs, sqls = walk(root)

    shipping: list[tuple[str, int, bool]] = []
    for csproj in csprojs:
        pkgdir = os.path.dirname(csproj) + os.sep
        count = sum(1 for s in sqls if s.startswith(pkgdir))
        if count == 0:
            continue
        try:
            with open(csproj, encoding="utf-8-sig", errors="replace") as fh:
                text = fh.read()
        except OSError as exc:  # unreadable csproj => cannot classify => REFUSE, never assume
            print(f"::error:: REFUSE - cannot read {csproj}: {exc}", file=err)
            return 2
        own = [s for s in sqls if s.startswith(pkgdir)]
        shipping.append(
            (os.path.basename(os.path.dirname(csproj)), count, packs_ddl(text, pkgdir, own))
        )

    for name, count, packed in sorted(shipping, key=lambda r: (not r[2], r[0])):
        print(f"  {'PACKED  ' if packed else 'UNPACKED'} {count:2d}  {name}", file=out)

    offenders = [(n, c) for n, c, p in shipping if not p]
    print("---", file=out)
    print(
        f"packages shipping .sql: {len(shipping)}   "
        f"packed: {len(shipping) - len(offenders)}   UNPACKED: {len(offenders)}",
        file=out,
    )

    # The empty-population REFUSE. A checker over zero packages reports zero offenders, which is
    # indistinguishable from a clean tree -- so it must never be able to say PASS.
    if not shipping:
        print(f"::error:: REFUSE - no package under {root} ships any .sql.", file=err)
        print(
            "The population is empty, so this gate measured NOTHING. "
            "A zero here is not a clean tree.",
            file=err,
        )
        return 2

    # Parity is asked over a DIFFERENT population (packages shipping NOTHING), so it runs
    # regardless of the packing verdict and the worst of the two governs. Running it only on a
    # packing PASS would make the blind half invisible again the moment the visible half went red.
    parity = check_parity(csprojs, sqls, out, err)

    if offenders:
        print(f"::error:: {len(offenders)} package(s) ship SQL DDL they never pack:", file=err)
        for name, count in offenders:
            print(f"::error::   {name} ({count} .sql)", file=err)
        print(
            "\nA consumer installing these packages cannot obtain the schema needed to run them,\n"
            "and the framework does not auto-create tables. Add the .sql to the csproj as a\n"
            "packed item, following a package that already does it correctly:\n\n"
            '  <ItemGroup>\n'
            '    <None Include="Scripts\\**\\*.sql" Pack="true" PackagePath="scripts\\" />\n'
            "  </ItemGroup>",
            file=err,
        )
        return 1

    if parity != 0:
        return parity

    print("PASS - every package shipping .sql packs it, and no new sibling-parity gap.", file=out)
    return 0


# ---------------------------------------------------------------------------------------------
# --self-test: prove the gate is NON-VACUOUS in both directions.
#
#   SAFETY   an unpacked package is DETECTED           (fixture: .sql, no Pack)      -> 1
#   LIVENESS a correctly-packed package is ALLOWED     (fixture: .sql, Pack="true")  -> 0
#   REFUSE   an empty population does NOT report PASS  (fixture: no .sql at all)     -> 2
#
# The liveness arm is the one that matters most and the one that would be forgotten: without it, a
# gate hardcoded to `return 1` passes its own safety test forever.
#
# The UNPACKED fixture deliberately CONTAINS Pack="true" -- on its README item, while the .sql item
# has none. A file-level search reads that as PACKED. That is the exact loose-match error behind one
# of the two earlier miscounts, planted here so the gate cannot regress into it.
# ---------------------------------------------------------------------------------------------
_PACKED_CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\\" />
    <None Include="Scripts\\**\\*.sql" Pack="true" PackagePath="scripts\\" />
  </ItemGroup>
</Project>
"""

_UNPACKED_CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\\" />
    <None Include="Scripts\\**\\*.sql" />
  </ItemGroup>
</Project>
"""

# Declaration present, correctly formed, and selecting nothing: the fixture's DDL lives under Sql/.
_WRONG_GLOB_CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <None Include="Scripts\\*.sql" Pack="true" PackagePath="scripts\\" />
  </ItemGroup>
</Project>
"""


def _fixture(root: str, name: str, kind: str) -> None:
    pkg = os.path.join(root, name)
    if kind == "nosql":
        os.makedirs(pkg, exist_ok=True)
        with open(os.path.join(pkg, f"{name}.csproj"), "w", encoding="utf-8") as fh:
            fh.write('<Project Sdk="Microsoft.NET.Sdk"></Project>\n')
        return
    # The wrong-glob fixture ships its DDL under Sql/ while its csproj packs Scripts\*.sql, so the
    # declaration is PRESENT and selects NOTHING. A declaration-only predicate calls this PACKED and
    # the package ships no schema -- the hole the glob check closes.
    ddl_dir = "Sql" if kind == "wrongglob" else "Scripts"
    os.makedirs(os.path.join(pkg, ddl_dir), exist_ok=True)
    with open(os.path.join(pkg, ddl_dir, "001_Schema.sql"), "w", encoding="utf-8") as fh:
        fh.write("CREATE TABLE Example (Id int);\n")
    body = {
        "packed": _PACKED_CSPROJ,
        "unpacked": _UNPACKED_CSPROJ,
        "wrongglob": _WRONG_GLOB_CSPROJ,
    }[kind]
    with open(os.path.join(pkg, f"{name}.csproj"), "w", encoding="utf-8") as fh:
        fh.write(body)


def self_test() -> int:
    import io
    import shutil

    arms = [
        ("SAFETY   unpacked .sql is detected", [("Unpacked.Package", "unpacked")], 1),
        ("LIVENESS correctly-packed .sql is allowed", [("Packed.Package", "packed")], 0),
        (
            "SAFETY   one offender among packed siblings is detected",
            [("Packed.Package", "packed"), ("Unpacked.Package", "unpacked")],
            1,
        ),
        (
            "SAFETY   packed glob matching NOTHING is not PACKED",
            [("WrongGlob.Package", "wrongglob")],
            1,
        ),
        ("REFUSE   empty population is NOT a pass", [("NoDdl.Package", "nosql")], 2),
        # --- parity arms -------------------------------------------------------------------
        # SAFETY: the case the packing check is structurally blind to. Fam.Postgres ships and packs
        # DDL, Fam.SqlServer ships none. The packing half sees NO offender here (you cannot fail to
        # pack a file you do not have), so if this arm passes only because of the packing check it
        # is not testing parity at all -- it must be the parity half that produces the 1.
        (
            "SAFETY   relational sibling shipping NO ddl is detected",
            [("Excalibur.Fam.Postgres", "packed"), ("Excalibur.Fam.SqlServer", "nosql")],
            1,
        ),
        # LIVENESS: the arm that decides whether anyone keeps this gate. A document-store sibling
        # shipping no DDL is CORRECT and must be ALLOWED. Without this arm a parity check hardcoded
        # to flag every non-shipping sibling passes its own safety test forever, fires on every
        # MongoDB/Redis package, and gets suppressed -- at which point the safety arm protects
        # nothing.
        (
            "LIVENESS non-relational sibling shipping NO ddl is allowed",
            [("Excalibur.Fam.Postgres", "packed"), ("Excalibur.Fam.MongoDB", "nosql")],
            0,
        ),
        # LIVENESS: a family where NOBODY ships DDL is a design choice, not a gap.
        (
            "LIVENESS family where no sibling ships ddl is allowed",
            [("Excalibur.Bare.Postgres", "nosql"), ("Excalibur.Bare.SqlServer", "nosql")],
            2,  # REFUSE: no .sql anywhere -> empty packing population, which is never a PASS
        ),
    ]

    print("self-test: proving this gate can report FAIL, PASS and REFUSE")
    failures = 0
    tmp = tempfile.mkdtemp()
    previous = os.environ.get("DDL_PACK_SRC_ROOT")
    previous_baseline = os.environ.get("DDL_PARITY_BASELINE")
    # An EMPTY fixture baseline, so parity arms are judged against the fixtures alone. Pointing
    # them at the real baseline would report its five real entries as 'stale' in every arm --
    # right-looking verdicts computed over the wrong subject.
    fixture_baseline = os.path.join(tmp, "empty.baseline.txt")
    with open(fixture_baseline, "w", encoding="utf-8") as fh:
        fh.write("# intentionally empty\n")
    os.environ["DDL_PARITY_BASELINE"] = fixture_baseline
    try:
        for label, fixtures, want in arms:
            root = os.path.join(tmp, "src")
            shutil.rmtree(root, ignore_errors=True)
            os.makedirs(root, exist_ok=True)
            for name, kind in fixtures:
                _fixture(root, name, kind)
            os.environ["DDL_PACK_SRC_ROOT"] = root
            sink = io.StringIO()
            got = scan(out=sink, err=sink)
            if got == want:
                print(f"  ok    {label:<58} exit {got}")
            else:
                print(f"  FAIL  {label:<58} exit {got} (expected {want})")
                failures += 1
    finally:
        if previous is None:
            os.environ.pop("DDL_PACK_SRC_ROOT", None)
        else:
            os.environ["DDL_PACK_SRC_ROOT"] = previous
        if previous_baseline is None:
            os.environ.pop("DDL_PARITY_BASELINE", None)
        else:
            os.environ["DDL_PARITY_BASELINE"] = previous_baseline
        shutil.rmtree(tmp, ignore_errors=True)

    print("---")
    if failures == 0:
        print("self-test PASS - gate is non-vacuous in both directions.")
        return 0
    print(
        f"::error:: self-test FAILED ({failures} arm(s)). This gate cannot be trusted.",
        file=sys.stderr,
    )
    return 1


if __name__ == "__main__":
    arg = sys.argv[1] if len(sys.argv) > 1 else ""
    if arg == "--self-test":
        sys.exit(self_test())
    if arg == "":
        sys.exit(scan())
    print(f"usage: {os.path.basename(__file__)} [--self-test]", file=sys.stderr)
    sys.exit(2)
