#!/usr/bin/env python3
"""Assert that the packages about to be published carry an author signature.

WHAT THIS EXISTS TO CATCH
-------------------------
A publishing job that checks "is a signing certificate configured?" is asking about its own
environment. The question that matters is "do the bytes I am about to push carry a signature?"
-- a property of the artifact. The two answers diverge the moment signing moves, fails, or is
skipped, and only the second one is what a consumer receives.

The distinction this gate is built around: "these are unsigned because something went wrong"
and "we have decided to ship unsigned" look identical at the moment of publishing and are
completely different statements. Treating absence as consent is a silent fail-open -- the
release goes green the first time a certificate expires, rotates or is renamed, and nobody
learns the packages stopped being signed. So absence REFUSES, and only an explicit opt-out
proceeds.

HOW SIGNEDNESS IS READ
----------------------
A signed .nupkg carries a `.signature.p7s` entry at the archive root. Reading the archive
answers "is it signed at all" without depending on which certificate roots the host happens
to trust -- which is the point: the fail-open case above must not be maskable by an
environmental failure. Whether that signature is VALID is a separate, stronger question,
answered by `dotnet nuget verify` in the step after this one. Presence and validity are
different properties and are checked separately on purpose.

EXIT CODES -- distinct on purpose.
  0  PASS    every package is signed, or unsigned by explicit opt-out
  1  FAIL    at least one package is unsigned and no opt-out was given
  3  REFUSE  there was nothing to measure (missing directory, no packages, unreadable
             archive). REFUSE IS NOT A PASS. A gate that cannot see its subject must say so,
             because an empty set otherwise satisfies every assertion by giving it nothing to
             object to.
"""

import argparse
import os
import sys
import tempfile
import zipfile

EXIT_PASS = 0
EXIT_FAIL = 1
EXIT_REFUSE = 3

# The entry NuGet writes into the archive root when a package is author- or repository-signed.
SIGNATURE_ENTRY = ".signature.p7s"


def is_signed(path):
    """True/False, or raises. A package that cannot be opened is not 'unsigned' -- it is
    unmeasured, and the caller turns that into REFUSE rather than into a verdict."""
    with zipfile.ZipFile(path) as archive:
        return SIGNATURE_ENTRY in archive.namelist()


def partition(packages_dir):
    """Return (signed, unsigned, refusal_message)."""
    if not os.path.isdir(packages_dir):
        return [], [], "package directory '%s' does not exist" % packages_dir

    names = sorted(n for n in os.listdir(packages_dir) if n.endswith(".nupkg"))
    if not names:
        return [], [], (
            "no .nupkg under '%s'. Zero subjects is not zero findings -- an empty set would "
            "satisfy this gate by giving it nothing to object to, and the push after it would "
            "publish nothing while reporting success." % packages_dir)

    signed, unsigned = [], []
    for name in names:
        path = os.path.join(packages_dir, name)
        try:
            (signed if is_signed(path) else unsigned).append(name)
        except Exception as exc:  # unreadable is unmeasured, never a pass
            return [], [], "could not read '%s' as a package archive: %s" % (name, exc)
    return signed, unsigned, None


def emit_output(all_signed):
    """Publish the verdict so the validity check downstream can condition on it. Written to
    GITHUB_OUTPUT when present; absent locally, which is not an error."""
    target = os.environ.get("GITHUB_OUTPUT")
    if not target:
        return
    with open(target, "a", encoding="utf-8") as handle:
        handle.write("all_signed=%s\n" % ("true" if all_signed else "false"))


def run(packages_dir, allow_unsigned):
    signed, unsigned, refusal = partition(packages_dir)
    if refusal:
        print("::error::REFUSE: %s Nothing was measured -- this is not a pass." % refusal)
        return EXIT_REFUSE

    total = len(signed) + len(unsigned)

    if not unsigned:
        emit_output(True)
        print("all %d package(s) carry an author signature" % total)
        return EXIT_PASS

    if not allow_unsigned:
        emit_output(False)
        print("::error::REFUSE: %d of %d promoted package(s) carry no author signature, and no "
              "explicit opt-out is set:" % (len(unsigned), total))
        for name in unsigned:
            print("::error::  %s" % name)
        print("::error::Signing happens in the producing build, before the hash manifest and the "
              "attestation, so that the bytes tested are the bytes signed are the bytes shipped. "
              "Configure the signing certificate there and promote a new build, or set the "
              "repository variable ALLOW_UNSIGNED_RELEASE=true to publish unsigned deliberately. "
              "A missing signature is NOT taken as consent to ship unsigned.")
        return EXIT_FAIL

    emit_output(False)
    # WHAT UNSIGNED COSTS, so the opt-out stays a judgement rather than a shrug: nuget.org accepts
    # unsigned packages and applies its own repository signature to everything it serves, so
    # consumers keep an authenticity guarantee from the registry. Author signing adds a second,
    # publisher-level guarantee on top, and it is not free -- the certificate must come from a
    # public CA, since nuget.org rejects self-issued ones. Unsigned is a real reduction in
    # guarantee, not a formality.
    print("::warning::Publishing %d of %d package(s) UNSIGNED by explicit opt-out "
          "(ALLOW_UNSIGNED_RELEASE=true)." % (len(unsigned), total))
    for name in unsigned:
        print("::warning::  %s" % name)
    print("::warning::These carry NO author signature. Consumers still get nuget.org's repository "
          "signature, which the registry applies to everything it serves, but no publisher-level "
          "guarantee from us. Remove this variable once a certificate is available.")
    return EXIT_PASS


# --------------------------------------------------------------------------------- self-test

def _make_package(path, signed):
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr("Some.Package.nuspec", "<package/>")
        archive.writestr("lib/net10.0/Some.Package.dll", "MZ")
        if signed:
            archive.writestr(SIGNATURE_ENTRY, "not a real signature, but the entry that marks one")


def self_test():
    """Prove the gate can FAIL, can PASS, and can REFUSE -- each for its own reason.

    All three arms are required. A gate proven only to fail is satisfied by one that rejects
    everything; a gate proven only to pass is satisfied by one that asserts nothing; and a gate
    with no REFUSE arm reports an unmeasured subject as a clean one."""
    failures = []
    root = tempfile.mkdtemp(prefix="assert-packages-signed-selftest-")

    def case(label, expected, packages, allow_unsigned=False):
        directory = os.path.join(root, label)
        os.makedirs(directory)
        for name, signed in packages:
            _make_package(os.path.join(directory, name), signed)
        actual = run(directory, allow_unsigned)
        if actual != expected:
            failures.append("%s: expected exit %d, got %d" % (label, expected, actual))
        else:
            print("SELF-TEST: PASS -- %s -> exit %d" % (label, actual))

    # LIVENESS: a fully signed set must be ACCEPTED. Without this arm every check below is
    # satisfied by a gate that refuses everything, which protects nothing and ships nothing.
    case("all-signed", EXIT_PASS, [("A.nupkg", True), ("B.nupkg", True)])

    # SAFETY: the defect this gate exists for -- an unsigned package reaching the push.
    case("one-unsigned", EXIT_FAIL, [("A.nupkg", True), ("B.nupkg", False)])
    case("all-unsigned", EXIT_FAIL, [("A.nupkg", False)])

    # The opt-out is a DECISION and must work; absence of a certificate is not that decision.
    case("unsigned-with-optout", EXIT_PASS, [("A.nupkg", False)], allow_unsigned=True)

    # SAFETY: nothing to measure must REFUSE, never pass vacuously.
    case("empty-directory", EXIT_REFUSE, [])

    missing = os.path.join(root, "does-not-exist")
    if run(missing, False) != EXIT_REFUSE:
        failures.append("a missing package directory did not REFUSE")
    else:
        print("SELF-TEST: PASS -- a missing package directory REFUSES rather than passing")

    corrupt_dir = os.path.join(root, "corrupt")
    os.makedirs(corrupt_dir)
    with open(os.path.join(corrupt_dir, "A.nupkg"), "w", encoding="utf-8") as handle:
        handle.write("this is not a zip archive")
    if run(corrupt_dir, False) != EXIT_REFUSE:
        failures.append("an unreadable package was not REFUSED -- it must not read as 'unsigned'")
    else:
        print("SELF-TEST: PASS -- an unreadable package REFUSES rather than being called unsigned")

    if failures:
        for item in failures:
            print("SELF-TEST: FAIL -- %s" % item)
        return EXIT_FAIL
    print("SELF-TEST: the signature gate is non-vacuous (accepts signed, rejects unsigned, "
          "honours an explicit opt-out, and refuses when there is nothing to measure).")
    return EXIT_PASS


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--packages-dir", default="packages",
                        help="directory holding the .nupkg files about to be published")
    parser.add_argument("--allow-unsigned", action="store_true",
                        help="publish unsigned deliberately; absence of a signature is otherwise a refusal")
    parser.add_argument("--self-test", action="store_true",
                        help="prove the gate can fail, pass and refuse, then exit")
    args = parser.parse_args(argv)

    if args.self_test:
        return self_test()
    return run(args.packages_dir, args.allow_unsigned)


if __name__ == "__main__":
    sys.exit(main())
