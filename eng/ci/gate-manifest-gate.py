#!/usr/bin/env python3
"""Gates eng/governance/gate-manifest.yaml against the gate scripts actually on disk.

WHY: the manifest exists because gate membership was not data anywhere -- it was a property you
reconstructed by parsing two dispatchers, and every reconstruction was instrument-dependent. A
manifest fixes that only while it stays true to the tree. Left ungated it rots exactly like the
lists it replaces, and it rots INVISIBLY, because a membership list that has drifted still reads
as an authoritative answer.

THE TWO ROT DIRECTIONS ARE DIFFERENT DEFECTS AND BOTH ARE CHECKED:

  ORPHAN      a gate script on disk with no manifest entry.
              A new gate is invisible to every consumer of this file.

  DEAD ROW    a manifest entry naming a file that no longer exists.
              This is the worse one. It reads as coverage, and nothing ever looks at it again --
              a row whose subject is gone is not merely stale, it silently answers questions
              about a thing that is not there. A membership list decays into confident fiction
              one deleted script at a time.

WHAT THIS GATE DELIBERATELY DOES NOT CHECK: whether `kind` is CORRECT.
  Gate-versus-reporter is not mechanically derivable. A refusal exit and a finding exit share the
  non-zero space, and which is which lives in the script's header prose -- measured, three
  successive exit-code proxies gave three different answers, and the best of them still read a
  reporter whose `exit 3` means "tracker unreadable" as a failure. So the gate checks that `kind`
  is PRESENT and DRAWN FROM THE VOCABULARY, never that it is right. Asserting a derived kind is
  the counting error this whole exercise exists to remove.

  `kind: UNREVIEWED` is therefore legal, and RATCHETED: the unreviewed population may shrink and
  may never grow. Blocking on it instead would pay whoever is in a hurry to stamp a verdict they
  did not derive, which is the same defect wearing a deadline.

EXIT  0 manifest agrees with the tree, no unreviewed growth
      1 an orphan, a dead row, a bad field value, or unreviewed growth
      2 REFUSE: nothing was checked (manifest missing/unparseable, or the scan found no scripts --
        an empty census would pass vacuously, and a silent vacuous pass is the defect this gate
        is about)
"""
import io
import os
import re
import subprocess
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(os.path.dirname(__file__))))
MANIFEST = os.path.join(REPO_ROOT, "eng", "governance", "gate-manifest.yaml")
UNREVIEWED_FLOOR = os.path.join(REPO_ROOT, "eng", "ci", "gate-manifest-unreviewed-floor.txt")

KINDS = {"gate", "reporter", "UNREVIEWED"}
RUNS_IN = {"pre-commit", "ci", "pre-commit+ci", "nowhere", "local-only"}

# Scripts that are the gates' own tests/locks/fixtures are not themselves gates.
EXCLUDE_SUFFIX = re.compile(r"\.(test|harness-lock|fixture)\.(sh|py)$")
SCAN_GLOBS = ["eng/ci/*.sh", "eng/ci/*.py", ".claude/harness/*.sh", ".claude/harness/*.py"]


def scan_scripts(repo=None):
    repo = repo or REPO_ROOT
    out = subprocess.run(["git", "-C", repo, "ls-files"] + SCAN_GLOBS,
                         capture_output=True)
    paths = [p.strip() for p in out.stdout.decode("utf-8", "replace").split("\n") if p.strip()]
    return sorted(p for p in paths if not EXCLUDE_SUFFIX.search(p))


def parse_manifest(path):
    """Minimal reader for this file's fixed shape. Returns (entries, error).

    Deliberately not a YAML dependency: this gate must run in any CI image, and a gate that
    cannot start is a gate that reports nothing.
    """
    if not os.path.isfile(path):
        return None, "missing"
    entries, cur = [], None
    try:
        with open(path, encoding="utf-8") as fh:
            for raw in fh:
                line = raw.rstrip("\n")
                s = line.strip()
                if not s or s.startswith("#"):
                    continue
                m = re.match(r'-\s+path:\s*"?([^"]+)"?\s*$', s)
                if m:
                    cur = {"path": m.group(1)}
                    entries.append(cur)
                    continue
                m = re.match(r'([a-z_]+):\s*"?([^"]*)"?\s*$', s)
                if m and cur is not None:
                    cur[m.group(1)] = m.group(2).strip()
    except OSError as exc:
        return None, str(exc)
    return entries, None


def read_floor(path):
    if not os.path.isfile(path):
        return None, "missing"
    try:
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                s = line.strip()
                if not s or s.startswith("#"):
                    continue
                if not s.isdigit():
                    return None, "non-integer floor: {!r}".format(s[:40])
                return int(s), None
    except OSError as exc:
        return None, str(exc)
    return None, "empty"


def check(manifest_path=MANIFEST, floor_path=UNREVIEWED_FLOOR, repo=None, quiet=False):
    entries, err = parse_manifest(manifest_path)
    if err:
        print("REFUSE: gate manifest ({}). Nothing was checked; this is NOT a pass.".format(err),
              file=sys.stderr)
        return 2
    if not entries:
        print("REFUSE: gate manifest has no entries. An empty manifest passes vacuously.",
              file=sys.stderr)
        return 2

    on_disk = scan_scripts(repo)
    if not on_disk:
        print("REFUSE: the script scan matched nothing, so NOTHING was compared. This is not a "
              "clean tree; it is an instrument that found no subject.", file=sys.stderr)
        return 2

    listed = {e["path"] for e in entries}
    disk = set(on_disk)
    orphans = sorted(disk - listed)
    missing = sorted(listed - disk)

    # A row whose subject is absent from the TRACKED scan has two very different causes, and
    # conflating them hands the reader the wrong remedy.
    #
    #   present on disk, NOT tracked   the manifest is ahead of the index. The script is real,
    #                                  and a clean checkout will not have it. Remedy: stage it.
    #   absent from disk entirely      the script was deleted and the row outlived it.
    #                                  Remedy: drop the row.
    #
    # `git ls-files` remains the right scan -- CI checks out committed content, so an untracked
    # script does not exist for CI. But that makes this gate inherit, by construction, exactly
    # the blindness that `git add -u` has to a NEW file: the script is invisible to the scan
    # while its row is loudly present. Reported as "does not exist" that is simply false, and
    # it sends the reader to delete a row they should be staging a file for.
    repo_root = repo or REPO_ROOT
    untracked_rows = sorted(p for p in missing
                            if os.path.isfile(os.path.join(repo_root, p)))
    untracked_set = set(untracked_rows)
    dead_rows = [p for p in missing if p not in untracked_set]

    bad_kind = [e for e in entries if e.get("kind") not in KINDS]
    bad_runs = [e for e in entries if e.get("runs_in") not in RUNS_IN]
    unreviewed = [e for e in entries if e.get("kind") == "UNREVIEWED"]

    floor, ferr = read_floor(floor_path)
    grew = (ferr is None and len(unreviewed) > floor)

    if not quiet:
        print("EXAMINED: {} manifest entr(ies) against {} script(s) on disk".format(
            len(entries), len(on_disk)))
        if orphans:
            print("ORPHAN scripts (on disk, absent from the manifest -- invisible to every "
                  "consumer of this file):", file=sys.stderr)
            for p in orphans[:20]:
                print("  {}".format(p), file=sys.stderr)
            if len(orphans) > 20:
                print("  ... and {} more".format(len(orphans) - 20), file=sys.stderr)
        if untracked_rows:
            print("UNTRACKED SUBJECTS (the manifest names a script that is on disk but not "
                  "tracked -- a clean checkout, and therefore CI, will not have it. Stage the "
                  "script; do NOT drop the row):", file=sys.stderr)
            for p in untracked_rows[:20]:
                print("  {}".format(p), file=sys.stderr)
        if dead_rows:
            print("DEAD ROWS (manifest names a file that is not on disk at all -- reads as "
                  "coverage, answers questions about a thing that is not there. Drop the "
                  "row):", file=sys.stderr)
            for p in dead_rows[:20]:
                print("  {}".format(p), file=sys.stderr)
        if bad_kind:
            print("BAD kind (must be one of {}):".format(sorted(KINDS)), file=sys.stderr)
            for e in bad_kind[:20]:
                print("  {} :: kind={!r}".format(e.get("path"), e.get("kind")), file=sys.stderr)
        if bad_runs:
            print("BAD runs_in (must be one of {}):".format(sorted(RUNS_IN)), file=sys.stderr)
            for e in bad_runs[:20]:
                print("  {} :: runs_in={!r}".format(e.get("path"), e.get("runs_in")),
                      file=sys.stderr)
        if grew:
            print("UNREVIEWED RATCHET: {} entr(ies) carry kind: UNREVIEWED, floor is {}. The "
                  "unreviewed population may shrink and may never grow. Review an entry and lower "
                  "the floor -- do NOT stamp a kind you did not derive.".format(
                      len(unreviewed), floor), file=sys.stderr)
        # ASCII separators on purpose: this line lands in CI logs and in a mirrored repository,
        # and a non-ASCII bullet renders as mojibake in several consoles.
        print("gate-manifest: {} entr(ies) | {} orphan | {} untracked | {} dead row(s) | "
              "{} bad kind | {} bad runs_in | unreviewed {}/{}".format(
                  len(entries), len(orphans), len(untracked_rows), len(dead_rows),
                  len(bad_kind), len(bad_runs),
                  len(unreviewed), ("NO FLOOR" if ferr else floor)))

    if ferr is not None:
        print("REFUSE: unreviewed floor ({}). The ratchet was NOT evaluated; this is NOT a pass."
              .format(ferr), file=sys.stderr)
        return 2
    return 1 if (orphans or untracked_rows or dead_rows or bad_kind or bad_runs or grew) else 0


def self_test():
    import tempfile
    fails = 0
    arms = 0
    NL = chr(10)

    def write_manifest(path, rows):
        with open(path, "w", encoding="utf-8") as fh:
            fh.write("gates:" + NL)
            for r in rows:
                fh.write('  - path: "{}"'.format(r[0]) + NL)
                fh.write('    kind: {}'.format(r[1]) + NL)
                fh.write('    runs_in: {}'.format(r[2]) + NL)

    with tempfile.TemporaryDirectory() as tmp:
        repo = os.path.join(tmp, "repo")
        os.makedirs(os.path.join(repo, "eng", "ci"))
        subprocess.run(["git", "-C", repo, "init", "-q"], capture_output=True)
        real = "eng/ci/real-gate.sh"
        with open(os.path.join(repo, real), "w", encoding="utf-8") as fh:
            fh.write("#!/usr/bin/env bash" + NL + "exit 0" + NL)
        subprocess.run(["git", "-C", repo, "add", "-A"], capture_output=True)

        mpath = os.path.join(tmp, "m.yaml")
        fpath = os.path.join(tmp, "floor.txt")
        with open(fpath, "w", encoding="utf-8") as fh:
            fh.write("0" + NL)

        # Arm 1 (safety): manifest that matches the tree passes.
        write_manifest(mpath, [(real, "gate", "ci")])
        rc = check(mpath, fpath, repo=repo, quiet=True)
        print("  PASS  a manifest matching the tree passes" if rc == 0
              else "  FAIL  returned {}, expected 0".format(rc))
        arms += 1
        fails += rc != 0

        # Arm 2 (liveness): a script on disk with no entry is an ORPHAN and fails.
        write_manifest(mpath, [])
        rc = check(mpath, fpath, repo=repo, quiet=True)
        print("  PASS  an empty manifest over a non-empty tree REFUSEs" if rc == 2
              else "  FAIL  returned {}, expected 2".format(rc))
        arms += 1
        fails += rc != 2

        other = "eng/ci/second-gate.sh"
        with open(os.path.join(repo, other), "w", encoding="utf-8") as fh:
            fh.write("#!/usr/bin/env bash" + NL + "exit 1" + NL)
        subprocess.run(["git", "-C", repo, "add", "-A"], capture_output=True)
        write_manifest(mpath, [(real, "gate", "ci")])
        rc = check(mpath, fpath, repo=repo, quiet=True)
        print("  PASS  a script on disk with no manifest entry FAILS as an orphan" if rc == 1
              else "  FAIL  returned {}, expected 1".format(rc))
        arms += 1
        fails += rc != 1

        # Arm 3 (liveness): an entry whose subject no longer exists FAILS.
        write_manifest(mpath, [(real, "gate", "ci"), (other, "gate", "ci"),
                               ("eng/ci/deleted-subject.sh", "gate", "ci")])
        rc = check(mpath, fpath, repo=repo, quiet=True)
        print("  PASS  an entry whose subject is gone FAILS as a dead row" if rc == 1
              else "  FAIL  returned {}, expected 1".format(rc))
        arms += 1
        fails += rc != 1

        # Arm 4 (liveness): a kind outside the vocabulary fails.
        write_manifest(mpath, [(real, "probably-a-gate", "ci"), (other, "gate", "ci")])
        rc = check(mpath, fpath, repo=repo, quiet=True)
        print("  PASS  a kind outside the vocabulary FAILS" if rc == 1
              else "  FAIL  returned {}, expected 1".format(rc))
        arms += 1
        fails += rc != 1

        # Arm 5 (liveness): an unrecognised runs_in fails -- 'local-only' must be DECLARED, so a
        # typo of it must not silently pass as some other state.
        write_manifest(mpath, [(real, "gate", "local"), (other, "gate", "ci")])
        rc = check(mpath, fpath, repo=repo, quiet=True)
        print("  PASS  an unrecognised runs_in FAILS ('local' is not 'local-only')" if rc == 1
              else "  FAIL  returned {}, expected 1".format(rc))
        arms += 1
        fails += rc != 1

        # Arm 6 (safety): 'local-only' IS accepted -- a deliberately-local gate must be
        # expressible, or mesh tooling reads as unwired forever and inflates the gate count.
        write_manifest(mpath, [(real, "gate", "local-only"), (other, "gate", "ci")])
        rc = check(mpath, fpath, repo=repo, quiet=True)
        print("  PASS  runs_in: local-only is a legal declared state" if rc == 0
              else "  FAIL  returned {}, expected 0".format(rc))
        arms += 1
        fails += rc != 0

        # Arm 7 (liveness): UNREVIEWED growth past the floor fails.
        write_manifest(mpath, [(real, "UNREVIEWED", "ci"), (other, "gate", "ci")])
        rc = check(mpath, fpath, repo=repo, quiet=True)
        print("  PASS  UNREVIEWED growth past the floor FAILS" if rc == 1
              else "  FAIL  returned {}, expected 1".format(rc))
        arms += 1
        fails += rc != 1

        # Arm 8 (safety): the same entry under a floor that allows it passes -- so arm 7 is
        # detecting GROWTH, not merely the presence of the word UNREVIEWED.
        with open(fpath, "w", encoding="utf-8") as fh:
            fh.write("1" + NL)
        rc = check(mpath, fpath, repo=repo, quiet=True)
        print("  PASS  UNREVIEWED at or under the floor passes" if rc == 0
              else "  FAIL  returned {}, expected 0".format(rc))
        arms += 1
        fails += rc != 0

        # Arm 9 (refusal): a missing floor REFUSEs rather than passing unratcheted.
        os.remove(fpath)
        rc = check(mpath, fpath, repo=repo, quiet=True)
        print("  PASS  a missing unreviewed floor REFUSEs (exit 2)" if rc == 2
              else "  FAIL  returned {}, expected 2".format(rc))
        arms += 1
        fails += rc != 2

        # Arm 10 (refusal): a missing manifest REFUSEs.
        rc = check(os.path.join(tmp, "nope.yaml"), fpath, repo=repo, quiet=True)
        print("  PASS  a missing manifest REFUSEs (exit 2)" if rc == 2
              else "  FAIL  returned {}, expected 2".format(rc))
        arms += 1
        fails += rc != 2

        # Arm 11 (refusal): the THIRD refuse path -- a scan that matches no scripts.
        #
        # This branch is the one that most looks like success: every manifest entry becomes a
        # "dead row" against an empty disk, so a gate without this guard would either fail loudly
        # for the wrong reason or, if the comparison were written the other way round, report a
        # clean tree because it found nothing to disagree with. An instrument that located no
        # subject has not cleared the subject. Measured tonight on a sibling gate: a ratchet
        # printed "0 grew, 0 new" over a floor that was not there, and it was found only by
        # deleting the input. Three REFUSE paths were declared here and two were arm-covered; a
        # membership gate landing with the exact vacuity it exists to prevent is the one irony
        # worth paying an arm to avoid.
        empty_repo = os.path.join(tmp, "empty-repo")
        os.makedirs(empty_repo)
        subprocess.run(["git", "-C", empty_repo, "init", "-q"], capture_output=True)
        with open(fpath, "w", encoding="utf-8") as fh:
            fh.write("1" + NL)
        rc = check(mpath, fpath, repo=empty_repo, quiet=True)
        print("  PASS  a scan that matches no scripts REFUSEs (exit 2)" if rc == 2
              else "  FAIL  returned {}, expected 2".format(rc))
        arms += 1
        fails += rc != 2


        # Arm 12 (discrimination): an entry whose subject is ON DISK BUT UNTRACKED must be
        # reported as an untracked subject, NOT as a dead row -- and the deleted-subject case
        # must still be reported as a dead row.
        #
        # Both fail, so an exit-code arm cannot tell them apart; the whole value of the split
        # is which sentence the reader gets, because the two remedies are opposites -- stage
        # the file, versus delete the row. So this arm reads the OUTPUT. Without it the split
        # could be entirely cosmetic and every other arm would still pass.
        import contextlib

        loose = "eng/ci/loose-untracked-gate.sh"
        with open(os.path.join(repo, loose), "w", encoding="utf-8") as fh:
            fh.write("#!/usr/bin/env bash" + NL + "exit 0" + NL)
        # deliberately NOT `git add`ed -- that is the condition under test
        with open(fpath, "w", encoding="utf-8") as fh:
            fh.write("9" + NL)
        write_manifest(mpath, [(real, "gate", "ci"), (other, "gate", "ci"),
                               (loose, "gate", "ci"),
                               ("eng/ci/deleted-subject.sh", "gate", "ci")])
        buf = io.StringIO()
        with contextlib.redirect_stderr(buf), contextlib.redirect_stdout(io.StringIO()):
            rc = check(mpath, fpath, repo=repo, quiet=False)
        err = buf.getvalue()
        untracked_named = ("UNTRACKED SUBJECTS" in err and loose in err.split(
            "DEAD ROWS")[0])
        dead_named = ("DEAD ROWS" in err and "deleted-subject.sh" in err.split(
            "DEAD ROWS")[-1])
        crossed = "deleted-subject.sh" in err.split("DEAD ROWS")[0]
        ok = rc == 1 and untracked_named and dead_named and not crossed
        print("  PASS  untracked-on-disk and deleted-entirely are reported apart" if ok
              else "  FAIL  rc={} untracked_named={} dead_named={} crossed={}".format(
                  rc, untracked_named, dead_named, crossed))
        arms += 1
        fails += not ok
    if fails:
        print("self-test: {} of {} arm(s) FAILED".format(fails, arms))
        return 1
    print("self-test: {}/{} arms pass".format(arms, arms))
    return 0


if __name__ == "__main__":
    arg = sys.argv[1] if len(sys.argv) > 1 else ""
    if arg == "--self-test":
        sys.exit(self_test())
    if arg in ("", "--check"):
        sys.exit(check())
    print("usage: {} [--self-test|--check]".format(os.path.basename(sys.argv[0])), file=sys.stderr)
    sys.exit(2)
