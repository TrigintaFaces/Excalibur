#!/usr/bin/env python3
"""Gates eng/ci/aot-suppression-classification.json against the suppression baseline.

WHY: eng/ci/aot-suppression-baseline.json grandfathers a suppression's EXISTENCE; it says
nothing about whether the reflection it silences is actually unreachable (PROVABLY-SAFE) or
just hidden from the consumer (SHOULD-PROPAGATE). The companion classification file records
that judgment. Without this gate the classification file rots exactly like the baseline would
have without ITS gate: it falls behind, still looks complete, and nobody notices until someone
reads it.

The classification key is (file, warningId, justification-text) -- deliberately line-number-free,
because line drift (files growing/shrinking from unrelated edits) is not a judgment change. One
classification entry covers every baseline row sharing that exact triple.

The undecided population is RATCHETED, not blocked -- see UNPROVEN_BASELINE below for why
blocking on it would pay for exactly the fabricated verdicts this gate exists to keep out.

EXIT  0 no integrity fault, and the not-proven population is at or below its recorded floor
      1 an integrity fault (a stale entry, a verdict outside the vocabulary, or a DEAD-REDUNDANT
        entry missing either of its two required controls), OR the not-proven population GREW or
        gained a new (file, warningId) pair
      2 REFUSE: nothing was checked -- the baseline is missing or empty, the classification file is
        missing or unreadable, or the ratchet floor is missing or malformed. A REFUSE is never a
        pass; it means the gate could not measure, which is a different fact from "clean".
"""
import json
import os
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(os.path.dirname(__file__))))
BASELINE = os.path.join(REPO_ROOT, "eng", "ci", "aot-suppression-baseline.json")
CLASSIFICATION = os.path.join(REPO_ROOT, "eng", "ci", "aot-suppression-classification.json")

# THE RATCHET, and why this gate does not simply block on everything undecided.
#
# An honest instrument that refuses to guess necessarily grows the undecided pile -- that is the
# instrument working, not the code getting worse. If the gate BLOCKS on that pile, the cheapest
# way back to green is to write confident verdicts into it, which is precisely the unearned
# population the refusal just removed, and the counter would score it as progress. A gate that
# pays people to undo the honesty that caused it is worse than no gate.
#
# So, matching unconditional-skip-ratchet.sh in this directory: baseline the current counts and
# fail on an INCREASE or a NEW offender. The pile may shrink and may never grow.
#
# WHAT IS RATCHETED IS THE LOAD-BEARING CHOICE. It is NOT "rows classified" -- ratchet that and it
# is gamed by classifying carelessly, which is the same defect wearing the opposite sign. The
# ratcheted quantity is ROWS WHOSE VERDICT IS NOT PROVEN, and every not-proven state is counted
# INSIDE it: a row with no entry at all, and a row explicitly marked undecided. If a refusal state
# sat OUTSIDE the blocking number it would become the new parking space -- move a row there, the
# number falls, and nothing was ever examined. Any new honest state added to this gate must get
# that parking-space analysis before it ships.
UNPROVEN_BASELINE = os.path.join(REPO_ROOT, "eng", "ci", "aot-suppression-unproven-baseline.txt")

VERDICTS = {"PROVABLY-SAFE", "SHOULD-PROPAGATE", "UNCLASSIFIED-NEEDS-OWNER", "DEAD-REDUNDANT"}

# DEAD-REDUNDANT means "stripped the suppression, rebuilt clean" -- which is only evidence if
# the trim/AOT analyzer was actually evaluating that project. Without a second, independent half
# naming how analysis was live (IsAotCompatible/EnableTrimAnalyzer on that csproj, or a known-live
# diagnostic in the same build), a clean build proves nothing and the verdict is itself an
# unearned assertion -- the exact defect this file exists to remove.
DEAD_REDUNDANT_REQUIRES = "analyzerControl"

# ...and that control is NOT SUFFICIENT, which was learned the expensive way. `analyzerControl`
# answers "was the analyzer evaluating this project?" It is silent on "was this suppression
# actually neutralised?" -- and those are different questions with the same symptom: silence.
#
# Measured: rows certified DEAD-REDUNDANT on the evidence "ZERO of this code in this file", each
# carrying a perfectly good analyzerControl, over sites the stripping instrument had never
# rewritten because it did not recognise the suppression's SPELLING. There are at least three
# legal spellings of one suppression -- an attribute with the code colon-suffixed
# ("IL2026:RequiresUnreferencedCode"), an attribute with the bare code ("IL3050") and the
# category as a separate argument, and a pragma disabling several codes on one line
# (disable IL2026, IL3050). An enumeration keyed to one spelling returns a confident zero over
# the other two. Neutralise the suppressions and rebuild: the diagnostics fire.
#
# So the strip is THREE-state, and the third state is the point:
#     neutralised and silent  -> INERT   (the verdict DEAD-REDUNDANT is earned)
#     neutralised and fired   -> LIVE    (load-bearing; not dead)
#     COULD NOT NEUTRALISE    -> REFUSE  (no verdict at all -- and never INERT)
# `stripControl` is what makes the third state expressible: a DEAD-REDUNDANT row must state how
# many suppression tokens of this code in this file the instrument actually rewrote. A row that
# cannot say that has not earned a verdict, because its silence is indistinguishable from an
# instrument that never reached the site.
DEAD_REDUNDANT_ALSO_REQUIRES = "stripControl"


def _key(entry):
    return (entry["file"], entry["warningId"], entry["justification"])


def _load(path):
    """Returns (data, error). error is None on success, else a printable reason."""
    if not os.path.isfile(path):
        return None, "missing"
    try:
        with open(path, encoding="utf-8") as fh:
            return json.load(fh), None
    except (OSError, ValueError) as exc:
        return None, str(exc)


def _load_unproven_baseline(path):
    """Returns ({(file, warningId): rows}, error). Missing file is an error, never an empty dict:
    an absent baseline read as {} would make every existing pair look NEW and fail the whole
    population, and read as "no limit" would make the ratchet vacuous. Say which it is."""
    if not os.path.isfile(path):
        return {}, "missing"
    out = {}
    try:
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                line = line.rstrip("\n")
                if not line.strip() or line.lstrip().startswith("#"):
                    continue
                parts = line.split("\t")
                if len(parts) != 3:
                    return {}, "malformed line: {!r}".format(line[:80])
                try:
                    out[(parts[1], parts[2])] = int(parts[0])
                except ValueError:
                    return {}, "non-integer count: {!r}".format(line[:80])
    except OSError as exc:
        return {}, str(exc)
    return out, None


def _display_path(path):
    # relpath raises on Windows when path and REPO_ROOT are on different drives (e.g. a temp
    # dir under a self-test) -- fall back to the raw path rather than crashing the gate.
    try:
        return os.path.relpath(path, REPO_ROOT)
    except ValueError:
        return path


def check(baseline_path=BASELINE, classification_path=CLASSIFICATION, quiet=False,
          unproven_baseline_path=UNPROVEN_BASELINE):
    baseline, err = _load(baseline_path)
    if err:
        print("REFUSE: baseline {} ({}). Nothing was checked; this is NOT a pass."
              .format(_display_path(baseline_path), err), file=sys.stderr)
        return 2
    classification, err = _load(classification_path)
    if err:
        print("REFUSE: classification file {} ({}). Nothing was checked; this is NOT a pass."
              .format(_display_path(classification_path), err), file=sys.stderr)
        return 2

    rows = baseline.get("suppressions", [])
    if not rows:
        print("REFUSE: baseline has 0 suppressions. Nothing was checked; this is NOT a pass.",
              file=sys.stderr)
        return 2

    # The denominator: the population this gate walked, not what it found. A gate that reports only
    # its findings prints the same green whether the tree is clean or its input stopped resolving.
    # Emitted unconditionally and before any verdict -- gating it behind the quiet flag would make
    # the declaration optional, which is the silent-green this line exists to prevent.
    print("EXAMINED: {} baseline suppression row(s)".format(len(rows)))

    baseline_rows_by_key = {}
    for row in rows:
        baseline_rows_by_key.setdefault(_key(row), []).append(row)
    baseline_keys = set(baseline_rows_by_key)

    entries = classification.get("classifications", [])
    bad_verdict = [e for e in entries if e.get("verdict") not in VERDICTS]
    missing_control = [
        e for e in entries
        if e.get("verdict") == "DEAD-REDUNDANT" and not str(e.get(DEAD_REDUNDANT_REQUIRES, "")).strip()
    ]
    missing_strip = [
        e for e in entries
        if e.get("verdict") == "DEAD-REDUNDANT"
        and not str(e.get(DEAD_REDUNDANT_ALSO_REQUIRES, "")).strip()
    ]
    # UNCLASSIFIED-NEEDS-OWNER is a routing verdict, not a decision: it records that a human
    # looked and could not decide. It must NOT buy a pass. Before this was blocking, an entry
    # carrying it removed its baseline key from `unclassified` -- so a row nobody had decided
    # counted as classified and the gate could reach exit 0 over a population explicitly marked
    # undecided. That is the same unearned green this file exists to make impossible.
    needs_owner = [e for e in entries if e.get("verdict") == "UNCLASSIFIED-NEEDS-OWNER"]
    needs_owner_keys = {_key(e) for e in needs_owner}
    classification_keys = {_key(e) for e in entries}

    unclassified = sorted(baseline_keys - classification_keys)
    stale = sorted(classification_keys - baseline_keys)

    total_rows = len(rows)
    unclassified_rows = sum(len(baseline_rows_by_key[k]) for k in unclassified)
    # A needs-owner row is not classified either; counting it as such overstates the
    # burn-down by exactly the number of rows nobody decided.
    needs_owner_rows = sum(len(baseline_rows_by_key[k])
                           for k in needs_owner_keys if k in baseline_rows_by_key)
    classified_rows = total_rows - unclassified_rows - needs_owner_rows

    # The ratcheted population: every baseline row whose verdict is NOT PROVEN, whether that is
    # because nothing classified it or because something looked and explicitly declined to decide.
    # Both are counted here on purpose -- see UNPROVEN_BASELINE above for why a refusal state kept
    # outside this number would become a parking space.
    unproven_keys = set(unclassified) | (needs_owner_keys & baseline_keys)
    current_unproven = {}
    for k in unproven_keys:
        pair = (k[0], k[1])
        current_unproven[pair] = current_unproven.get(pair, 0) + len(baseline_rows_by_key[k])
    baseline_unproven, ratchet_err = _load_unproven_baseline(unproven_baseline_path)
    grew, appeared, dead_floor = [], [], []
    if ratchet_err is None:
        for pair, n in sorted(current_unproven.items()):
            was = baseline_unproven.get(pair)
            if was is None:
                appeared.append((pair, n))
            elif n > was:
                grew.append((pair, was, n))
        # A ratchet's soundness is decided by what it does with a row whose SUBJECT no longer
        # exists -- not by whether the count can grow. A growth-only ratchet accumulates dead
        # rows that present as coverage, and its green gets STRONGER as it empties of meaning.
        #
        # The rot here is a floor entry for a (file, warningId) that the baseline no longer
        # contains at all. Nothing ever reads it: the comparison above walks the CURRENT
        # population and looks each pair up, so a floor entry no current pair matches is never
        # examined. It sits there holding open an allowance for a subject that is gone -- and if
        # that pair is ever reintroduced it is measured against the old allowance instead of
        # being reported NEW. Amnesty, granted silently, by a row nobody can see.
        #
        # Measured when this was added: 0 such entries, and NO CODE THAT COULD EVER FIND ONE.
        # A count of zero today is not a guard tomorrow, which is the whole reason this exists.
        baseline_pairs = {(row["file"], row["warningId"]) for row in rows}
        dead_floor = sorted(pair for pair in baseline_unproven
                            if pair not in baseline_pairs)

    if not quiet:
        # Every count here carries its UNIT, because this line mixes two populations and the
        # unlabelled version was read as one. It opened with a ROW ratio and then reported
        # "N unclassified", which is a PATTERN count -- so a reader reasonably took the burn-down
        # to be N rows when it was N reviewed verdicts over a larger number of rows. The row
        # figure was computed here and never printed. A number whose unit is inferred from its
        # neighbours is a number the reader has to guess at, and they will guess consistently
        # wrong when the neighbours disagree.
        print("aot-suppression-classification: {}/{} baseline ROW(s) classified "
              "({} distinct pattern(s) covered; unclassified: {} pattern(s) over {} row(s); "
              "needs-owner: {} pattern(s) over {} row(s); "
              "{} stale, {} bad-verdict, {} DEAD-REDUNDANT missing analyzer control, "
              "{} missing strip control; RATCHET not-proven {} row(s) over {} "
              "(file,code) pair(s), {})"
              .format(classified_rows, total_rows, len(classification_keys) - len(stale),
                      len(unclassified), unclassified_rows,
                      len(needs_owner_keys), needs_owner_rows,
                      len(stale), len(bad_verdict), len(missing_control), len(missing_strip),
                      sum(current_unproven.values()), len(current_unproven),
                      # "0 grew, 0 new" over an unevaluated ratchet is a green-looking number for
                      # a measurement that never ran -- the defect this gate exists to refuse.
                      # Say NOT EVALUATED, and let the exit code below say REFUSE.
                      "NOT EVALUATED ({})".format(ratchet_err) if ratchet_err is not None
                      else "{} grew, {} new, {} dead floor entr(ies)".format(
                          len(grew), len(appeared), len(dead_floor))))
        if unclassified:
            print("UNCLASSIFIED patterns (baseline row with no classification entry), "
                  "worst offenders first:", file=sys.stderr)
            worst = sorted(unclassified, key=lambda k: -len(baseline_rows_by_key[k]))[:20]
            for k in worst:
                print("  {} rows :: {} {} :: {}".format(
                    len(baseline_rows_by_key[k]), k[0], k[1], k[2][:90]), file=sys.stderr)
            if len(unclassified) > 20:
                print("  ... and {} more distinct pattern(s)".format(len(unclassified) - 20),
                      file=sys.stderr)
        if stale:
            print("STALE classification entries (no matching baseline row):", file=sys.stderr)
            for k in stale:
                print("  {} {} :: {}".format(k[0], k[1], k[2][:90]), file=sys.stderr)
        if bad_verdict:
            print("BAD VERDICT (not one of {}):".format(sorted(VERDICTS)), file=sys.stderr)
            for e in bad_verdict:
                print("  {} {} :: verdict={!r}".format(e.get("file"), e.get("warningId"),
                                                         e.get("verdict")), file=sys.stderr)
        if missing_control:
            print("DEAD-REDUNDANT missing '{}' (a clean rebuild alone is not evidence unless "
                  "the analyzer was live in that project -- name the EVALUATED "
                  "EnableTrimAnalyzer/EnableAotAnalyzer, or a known-live diagnostic in the same build -- NOT IsAotCompatible, which does not gate the analyzers):".format(DEAD_REDUNDANT_REQUIRES),
                  file=sys.stderr)
            for e in missing_control:
                print("  {} {} :: {}".format(e.get("file"), e.get("warningId"),
                                              e.get("justification", "")[:90]), file=sys.stderr)
        if missing_strip:
            print("DEAD-REDUNDANT missing '{}' (an analyzer control proves the analyzer RAN; it "
                  "cannot prove this suppression was REACHED. State how many suppression tokens "
                  "of this code in this file were rewritten -- counting every spelling: the "
                  "colon-suffixed attribute, the bare-code attribute, and the multi-code pragma. "
                  "A count the instrument cannot produce is a REFUSE, and a REFUSE is never "
                  "INERT):".format(DEAD_REDUNDANT_ALSO_REQUIRES), file=sys.stderr)
            for e in missing_strip:
                print("  {} {} :: {}".format(e.get("file"), e.get("warningId"),
                                              e.get("justification", "")[:90]), file=sys.stderr)

        if grew or appeared:
            print("UNPROVEN RATCHET: the not-proven population may shrink and may never grow.",
                  file=sys.stderr)
            for (pair, was, now) in grew:
                print("  GREW {} -> {} rows :: {} {}".format(was, now, pair[0], pair[1]),
                      file=sys.stderr)
            for (pair, n) in appeared:
                print("  NEW  {} row(s) :: {} {}".format(n, pair[0], pair[1]), file=sys.stderr)
            print("  If you made a verdict PROVABLE, lower the baseline in {} and the gate "
                  "holds you to the new floor. Do NOT clear this by writing a confident verdict "
                  "you did not measure -- that is the population this gate exists to keep out."
                  .format(_display_path(unproven_baseline_path)), file=sys.stderr)
        if dead_floor:
            print("STALE RATCHET FLOOR: {} entr(ies) name a (file, warningId) the baseline no "
                  "longer contains. Nothing reads them, and each one silently pre-authorises a "
                  "future reintroduction of that pair instead of reporting it NEW. Regenerate "
                  "with --write-unproven-baseline.".format(len(dead_floor)), file=sys.stderr)
            for pair in dead_floor[:20]:
                print("  DEAD {} {}".format(pair[0], pair[1]), file=sys.stderr)
            if len(dead_floor) > 20:
                print("  ... and {} more".format(len(dead_floor) - 20), file=sys.stderr)

    # An absent or malformed ratchet oracle REFUSEs. Reading it as an empty dict would mark every
    # existing pair NEW and fail everything; reading it as "no limit" would pass vacuously. Both
    # are worse than saying nothing was measured.
    if ratchet_err is not None:
        print("REFUSE: unproven baseline {} ({}). The ratchet was NOT evaluated; this is NOT a "
              "pass.".format(_display_path(unproven_baseline_path), ratchet_err), file=sys.stderr)
        return 2

    # NOTE what is deliberately NOT in this condition: `unclassified` and `needs_owner`. They are
    # governed by the ratchet above, not by a hard block -- blocking on them pays for fabricated
    # verdicts. What remains here is integrity, not burn-down: a verdict naming a row that no
    # longer exists, a verdict outside the vocabulary, and a DEAD-REDUNDANT claim missing either
    # of its two controls. None of those shrink over time; they are simply wrong.
    return 1 if (stale or bad_verdict or missing_control or missing_strip
                 or grew or appeared or dead_floor) else 0


def _write(path, obj):
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(obj, fh, indent=2, ensure_ascii=False)
        fh.write("\n")


def self_test():
    import tempfile
    fails = 0
    arms = 0
    with tempfile.TemporaryDirectory() as tmp:
        baseline_path = os.path.join(tmp, "baseline.json")
        classification_path = os.path.join(tmp, "classification.json")
        unproven_path = os.path.join(tmp, "unproven.txt")

        def floor(pairs):
            """Writes a ratchet floor. pairs: {(file, warningId): rows}."""
            with open(unproven_path, "w", encoding="utf-8") as fh:
                for (f, w), n in sorted(pairs.items()):
                    fh.write(("{}" + '\t' + "{}" + '\t' + "{}" + '\n').format(n, f, w))

        # A floor generous enough that the pre-ratchet arms below exercise the property they
        # were written for, not the ratchet. Arms 11-15 drive the floor deliberately.
        floor({("src/A.cs", "IL2026"): 99})

        row = {"file": "src/A.cs", "line": 10, "warningId": "IL2026", "justification": "reflects"}
        entry = {"file": "src/A.cs", "warningId": "IL2026", "justification": "reflects",
                  "verdict": "PROVABLY-SAFE", "demonstratedBy": "test"}

        # Arm 1 (safety): a fully-classified baseline passes.
        _write(baseline_path, {"suppressions": [row]})
        _write(classification_path, {"classifications": [entry]})
        rc = check(baseline_path, classification_path, quiet=True,
                   unproven_baseline_path=unproven_path)
        print("  PASS  fully classified baseline passes" if rc == 0
              else "  FAIL  returned {}, expected 0".format(rc))
        arms += 1
        fails += rc != 0

        # Arm 2 (liveness): an EMPTY classification over a non-empty baseline is the whole
        # population unproven. Under the ratchet it does not block merely for being undecided --
        # blocking there pays for fabricated verdicts -- but it must not be reported as decided,
        # and it must fail the instant it exceeds the floor. Both halves asserted.
        floor({("src/A.cs", "IL2026"): 0})
        _write(classification_path, {"classifications": []})
        rc = check(baseline_path, classification_path, quiet=True,
                   unproven_baseline_path=unproven_path)
        print("  PASS  an unproven row over its floor fails" if rc == 1
              else "  FAIL  returned {}, expected 1".format(rc))
        arms += 1
        fails += rc != 1
        floor({("src/A.cs", "IL2026"): 99})

        # Arm 2b (liveness): UNCLASSIFIED-NEEDS-OWNER records that someone looked and could
        # not decide. It is routing, not a decision, so it must never count as CLASSIFIED --
        # and, the load-bearing half, it must sit INSIDE the ratcheted not-proven number. If it
        # sat outside, moving a row into it would lower the blocking count while nothing was
        # examined: a parking space. Proven here by driving the floor to the UNCLASSIFIED count
        # and then marking the row needs-owner: if needs-owner were exempt the not-proven count
        # would drop to 0 and pass; because it is counted, the row still occupies its slot.
        _write(baseline_path, {"suppressions": [row]})
        floor({("src/A.cs", "IL2026"): 1})
        _write(classification_path, {"classifications": []})
        rc_unclassified = check(baseline_path, classification_path, quiet=True,
                                unproven_baseline_path=unproven_path)
        _write(classification_path, {"classifications": [
            {"file": row["file"], "warningId": row["warningId"],
             "justification": row["justification"],
             "verdict": "UNCLASSIFIED-NEEDS-OWNER", "demonstratedBy": "test"}]})
        rc_parked = check(baseline_path, classification_path, quiet=True,
                          unproven_baseline_path=unproven_path)
        # Now shrink the floor by one. An exempt needs-owner would report 0 not-proven rows and
        # still pass; a counted one exceeds the floor and fails.
        floor({("src/A.cs", "IL2026"): 0})
        rc_squeezed = check(baseline_path, classification_path, quiet=True,
                            unproven_baseline_path=unproven_path)
        ok = (rc_unclassified == 0 and rc_parked == 0 and rc_squeezed == 1)
        print("  PASS  needs-owner is counted INSIDE the ratchet, not a parking space" if ok
              else "  FAIL  unclassified={} parked={} squeezed={}, expected 0/0/1"
                   .format(rc_unclassified, rc_parked, rc_squeezed))
        arms += 1
        fails += not ok
        floor({("src/A.cs", "IL2026"): 99})

        # Arm 3 (liveness): a stale classification entry (baseline row gone) must FAIL.
        _write(baseline_path, {"suppressions": []})
        _write(classification_path, {"classifications": [entry]})
        # An empty baseline on its own REFUSEs (arm 5) before staleness is even checked, so pair
        # the stale entry with one still-live row to isolate the staleness arm.
        other_row = {"file": "src/B.cs", "line": 1, "warningId": "IL3050", "justification": "x"}
        other_entry = {"file": "src/B.cs", "warningId": "IL3050", "justification": "x",
                        "verdict": "PROVABLY-SAFE", "demonstratedBy": "test"}
        _write(baseline_path, {"suppressions": [other_row]})
        _write(classification_path, {"classifications": [entry, other_entry]})
        rc = check(baseline_path, classification_path, quiet=True,
                   unproven_baseline_path=unproven_path)
        print("  PASS  a stale classification entry fails" if rc == 1
              else "  FAIL  returned {}, expected 1".format(rc))
        arms += 1
        fails += rc != 1

        # Arm 4 (liveness): an unrecognized verdict string fails even if the key matches.
        _write(baseline_path, {"suppressions": [row]})
        bad_entry = dict(entry, verdict="LOOKS-FINE-TO-ME")
        _write(classification_path, {"classifications": [bad_entry]})
        rc = check(baseline_path, classification_path, quiet=True,
                   unproven_baseline_path=unproven_path)
        print("  PASS  an unrecognized verdict fails" if rc == 1
              else "  FAIL  returned {}, expected 1".format(rc))
        arms += 1
        fails += rc != 1

        # Arm 5 (refusal, DERIVED over every reference the gate reads):
        #
        # Every comparison a gate performs creates a way for it to pass without measuring, and the
        # shape is always the same -- THE THING IT COMPARES AGAINST IS ABSENT. Three of those
        # appeared in this one file: a verdict with no strip evidence, a refusal state outside the
        # counted population, and a ratchet whose floor was missing while the summary printed
        # "0 grew, 0 new". None was visible from reading the gate, running it, or its own arms.
        # Each was visible only by DELETING AN INPUT AND CONFIRMING THE GATE SCREAMS.
        #
        # So this arm is derived rather than written out: it reads check()'s own signature, and
        # for EVERY path it accepts, removes that file and asserts REFUSE. Add a fourth reference
        # and this arm covers it the moment the parameter exists -- no one has to remember. That
        # is the difference between a standing check and the lucky discovery that produced it.
        import inspect
        path_params = [name for name in inspect.signature(check).parameters
                       if name.endswith("_path")]
        # The derivation itself must be non-vacuous: an empty list would make this arm assert
        # nothing while printing a PASS -- the very defect it exists to catch.
        ok = len(path_params) >= 3
        if not ok:
            print("  FAIL  derived-reference arm found {} path parameter(s); expected >= 3"
                  .format(len(path_params)))
        for name in path_params:
            _write(baseline_path, {"suppressions": [row]})
            _write(classification_path, {"classifications": [entry]})
            floor({("src/A.cs", "IL2026"): 99})
            good = {"baseline_path": baseline_path,
                    "classification_path": classification_path,
                    "unproven_baseline_path": unproven_path}
            if check(quiet=True, **good) != 0:
                print("  FAIL  derived-reference arm could not reach a passing state first")
                ok = False
                break
            kwargs = dict(good)
            kwargs[name] = os.path.join(tmp, "absent-reference.json")
            rc = check(quiet=True, **kwargs)
            if rc != 2:
                print("  FAIL  removing {} returned {}, expected 2 (REFUSE)".format(name, rc))
                ok = False
        print("  PASS  removing ANY of the {} reference(s) check() reads REFUSEs"
              .format(len(path_params)) if ok else "  FAIL  derived-reference arm")
        arms += 1
        fails += not ok

        # Arm 7 (refusal): an empty baseline REFUSEs rather than reporting a vacuous pass.
        _write(baseline_path, {"suppressions": []})
        rc = check(baseline_path, classification_path, quiet=True,
                   unproven_baseline_path=unproven_path)
        print("  PASS  empty baseline REFUSEs (exit 2)" if rc == 2
              else "  FAIL  returned {}, expected 2".format(rc))
        arms += 1
        fails += rc != 2

        # Arm 8 (liveness): DEAD-REDUNDANT with an analyzer control fails -- WITHOUT one. This
        # is the RED the operator required proven before the verdict is trusted: a clean rebuild
        # alone must not be enough to pass the gate.
        _write(baseline_path, {"suppressions": [row]})
        no_control = dict(entry, verdict="DEAD-REDUNDANT",
                           demonstratedBy="stripped, rebuilt clean, 0 errors")
        _write(classification_path, {"classifications": [no_control]})
        rc = check(baseline_path, classification_path, quiet=True,
                   unproven_baseline_path=unproven_path)
        print("  PASS  DEAD-REDUNDANT with no analyzer control fails" if rc == 1
              else "  FAIL  returned {}, expected 1".format(rc))
        arms += 1
        fails += rc != 1

        # Arm 9 (liveness): an analyzer control alone is NOT enough. This is the arm that would
        # have caught the rows certified dead over sites the instrument never rewrote: every one
        # of them had an analyzerControl, and the analyzer really had run. What none of them had
        # was any evidence the suppression itself was reached.
        analyzer_only = dict(no_control,
                             analyzerControl="EMPIRICAL: 6 diagnostics of this code fired "
                                             "elsewhere in this project in the same build")
        _write(classification_path, {"classifications": [analyzer_only]})
        rc = check(baseline_path, classification_path, quiet=True,
                   unproven_baseline_path=unproven_path)
        print("  PASS  DEAD-REDUNDANT with an analyzer control but no strip control fails"
              if rc == 1 else "  FAIL  returned {}, expected 1".format(rc))
        arms += 1
        fails += rc != 1

        # Arm 10 (safety): the same entry WITH both controls passes.
        with_control = dict(analyzer_only,
                            stripControl="2 of 2 suppression tokens of this code in this file "
                                         "rewritten (1 colon-form attribute, 1 multi-code pragma)")
        _write(classification_path, {"classifications": [with_control]})
        rc = check(baseline_path, classification_path, quiet=True,
                   unproven_baseline_path=unproven_path)
        print("  PASS  DEAD-REDUNDANT with an analyzer control AND a strip control passes"
              if rc == 0 else "  FAIL  returned {}, expected 0".format(rc))
        arms += 1
        fails += rc != 0

        # Arm 11 (liveness): a pair absent from the floor is a NEW offender and fails, even
        # though its count is small. Growth by appearance is still growth.
        floor({("src/OTHER.cs", "IL3050"): 5})
        _write(baseline_path, {"suppressions": [row]})
        _write(classification_path, {"classifications": []})
        rc = check(baseline_path, classification_path, quiet=True,
                   unproven_baseline_path=unproven_path)
        print("  PASS  an unproven pair absent from the floor fails as NEW" if rc == 1
              else "  FAIL  returned {}, expected 1".format(rc))
        arms += 1
        fails += rc != 1

        # (The floor's absence was its own arm here; it is now covered by the derived
        # reference arm above, which also covers any reference added later. What is NOT
        # derivable is a reference that is PRESENT but malformed -- a different failure from
        # absence, and one the derivation cannot generate -- so that keeps an explicit arm.)
        with open(unproven_path, "w", encoding="utf-8") as fh:
            fh.write("this is not a floor line" + chr(10))
        _write(baseline_path, {"suppressions": [row]})
        _write(classification_path, {"classifications": []})
        rc = check(baseline_path, classification_path, quiet=True,
                   unproven_baseline_path=unproven_path)
        print("  PASS  a malformed ratchet floor REFUSEs (exit 2)" if rc == 2
              else "  FAIL  returned {}, expected 2".format(rc))
        arms += 1
        fails += rc != 2

        # Arm 13 (liveness): a FLOOR entry whose subject no longer exists must FAIL.
        #
        # A ratchet's soundness is decided by what it does with a row whose subject is gone, not
        # by whether the count can grow. A growth-only ratchet accumulates dead rows that read as
        # coverage while pre-authorising the return of a pair nobody is watching -- its green gets
        # stronger as it empties of meaning. The comparison walks the CURRENT population, so a
        # floor entry no current pair matches is never examined by construction; only an explicit
        # check finds it. Planting one and requiring a FAIL is the arm that keeps that true.
        _write(baseline_path, {"suppressions": [row]})
        _write(classification_path, {"classifications": [entry]})
        floor({("src/A.cs", "IL2026"): 99,
               ("src/DELETED-SUBJECT.cs", "IL2026"): 4})
        rc = check(baseline_path, classification_path, quiet=True,
                   unproven_baseline_path=unproven_path)
        print("  PASS  a floor entry whose subject is gone FAILS" if rc == 1
              else "  FAIL  returned {}, expected 1".format(rc))
        arms += 1
        fails += rc != 1

        # ...and the safety half: a floor holding ONLY live subjects still passes, so the arm
        # above is detecting the dead entry rather than any floor with two rows in it.
        floor({("src/A.cs", "IL2026"): 99})
        rc = check(baseline_path, classification_path, quiet=True,
                   unproven_baseline_path=unproven_path)
        print("  PASS  a floor of only-live subjects still passes" if rc == 0
              else "  FAIL  returned {}, expected 0".format(rc))
        arms += 1
        fails += rc != 0

    if fails:
        print("self-test: {} of {} arm(s) FAILED".format(fails, arms))
        return 1
    # DERIVED, never hardcoded. This line previously read "9/9" as a literal, so adding a tenth
    # arm left it reporting nine -- a self-test whose own summary cannot count the arms it ran is
    # the unearned green this gate exists to make impossible, printed by the gate about itself.
    print("self-test: {}/{} arms pass".format(arms, arms))
    return 0


def write_unproven_baseline(baseline_path=BASELINE, classification_path=CLASSIFICATION,
                            out_path=UNPROVEN_BASELINE):
    """Rewrites the ratchet floor from the CURRENT state.

    Run this when you have PROVEN a verdict and the floor should drop. It is deliberately a
    separate, explicit command: if the gate refreshed its own floor it would ratchet nothing, and
    a floor that rises silently is how a population grows while its gate reports green.
    """
    baseline, err = _load(baseline_path)
    if err:
        print("REFUSE: baseline {} ({}).".format(_display_path(baseline_path), err), file=sys.stderr)
        return 2
    classification, err = _load(classification_path)
    if err:
        print("REFUSE: classification {} ({}).".format(_display_path(classification_path), err),
              file=sys.stderr)
        return 2
    rows = baseline.get("suppressions", [])
    if not rows:
        print("REFUSE: baseline has no rows; refusing to write a vacuous floor.", file=sys.stderr)
        return 2
    by_key = {}
    for row in rows:
        by_key.setdefault(_key(row), []).append(row)
    entries = classification.get("classifications", [])
    decided = {_key(e) for e in entries if e.get("verdict") != "UNCLASSIFIED-NEEDS-OWNER"}
    counts = {}
    for k, group in by_key.items():
        if k in decided:
            continue
        pair = (k[0], k[1])
        counts[pair] = counts.get(pair, 0) + len(group)
    with open(out_path, "w", encoding="utf-8") as fh:
        fh.write('# Ratchet floor for the NOT-PROVEN population of the AOT suppression\n# classification: baseline rows with no verdict, plus rows whose verdict is an\n# explicit refusal to decide. Both are counted here on purpose -- a refusal kept\n# outside the blocking number becomes a parking space.\n# Format: <rows>\\t<file>\\t<warningId>. The gate fails on an INCREASE\n# or a NEW pair. Lower a number ONLY by proving a verdict, never by asserting one.\n')
        for (f, w), n in sorted(counts.items()):
            fh.write(("{}" + '\t' + "{}" + '\t' + "{}" + '\n').format(n, f, w))
    print("wrote {}: {} row(s) over {} (file,code) pair(s)"
          .format(_display_path(out_path), sum(counts.values()), len(counts)))
    return 0


if __name__ == "__main__":
    arg = sys.argv[1] if len(sys.argv) > 1 else ""
    if arg == "--self-test":
        sys.exit(self_test())
    if arg == "--write-unproven-baseline":
        sys.exit(write_unproven_baseline())
    if arg in ("", "--check"):
        sys.exit(check())
    print("usage: {} [--self-test|--check|--write-unproven-baseline]"
          .format(os.path.basename(sys.argv[0])), file=sys.stderr)
    sys.exit(2)
