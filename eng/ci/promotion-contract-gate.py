#!/usr/bin/env python3
"""Structural gate over the release promotion contract.

WHAT THIS EXISTS TO CATCH
-------------------------
"Promotion" is only meaningful if the thing promoted was built somewhere else. A release
workflow that BUILDS the packages it later publishes has not promoted anything -- it has
produced and published in one motion, and the hashes, the SBOM and the provenance it emits
all describe bytes that only ever existed inside the run that shipped them. Nothing outside
that run can contradict it, which is another way of saying nothing can check it.

The contract these assertions bind:

  A1  The release workflow cannot rebuild what it publishes.
  A2  A separate producing workflow exists and is complete: it packs, hashes, generates an
      SBOM, and attests BOTH provenance and SBOM over the packages it just made.
  A3  Attestation happens in the producing workflow and never in a workflow that downloaded
      the artifact. Attesting a downloaded copy is provenance about a journey, not an origin.
  A4  The admission job verifies hashes AND attestation AND version, refuses when no official
      build exists, and every consumer of the package set is gated behind it.
  A5  The draft-until-published ordering still holds.
  A6  There is exactly one canonical package artifact and no rival name to choose instead.
  A7  This gate's own workflow is triggered by edits to the workflows it asserts about.

WHAT IT CANNOT PROVE
--------------------
This is a graph and step-presence check. It proves the verification steps EXIST in the
admission job and that no consumer can reach the packages without passing through it. It does
not execute them, so it cannot prove the shell inside a verification step is itself correct.
That limit is stated rather than implied: a reader who mistakes this for a behavioural proof
would over-trust it, and an over-trusted gate is worse than an absent one.

EXIT CODES -- distinct on purpose.
  0  PASS    every assertion held
  1  FAIL    at least one assertion was violated (each is named)
  3  REFUSE  the workflows could not be read or parsed, so NOTHING was measured.
             REFUSE IS NOT A PASS. A gate that cannot see its subject must say so.
"""

import argparse
import copy
import os
import re
import sys
import tempfile

try:
    import yaml
except ImportError:  # pragma: no cover - exercised only on a host without pyyaml
    print("::error::REFUSE: pyyaml is not available, so no workflow could be parsed.")
    sys.exit(3)

EXIT_PASS = 0
EXIT_FAIL = 1
EXIT_REFUSE = 3

# The one name any consumer may ask for. An OS suffix or a second name is what makes two
# candidate package sets nameable and therefore pickable.
CANONICAL_ARTIFACT = "packages"

RELEASE_WF = "release.yml"
OFFICIAL_WF = "official-build.yml"
REHEARSAL_WF = "release-rehearsal.yml"
ADMISSION_JOB = "build-packages"

SHIPPING_FILTER = "ShippingOnly.slnf"

RE_DOTNET_PACK = re.compile(r"\bdotnet\s+pack\b")
RE_BUILD_SHIPPING = re.compile(r"\bdotnet\s+build\b[^\n]*" + re.escape(SHIPPING_FILTER))
RE_SHA_VERIFY = re.compile(r"\bsha256sum\s+(-c|--check)\b")
RE_SHA_PRODUCE = re.compile(r"\bsha256sum\b")
RE_ATTEST_VERIFY = re.compile(r"\bgh\s+attestation\s+verify\b")
RE_SBOM_TOOL = re.compile(r"cyclonedx|\bsbom\b", re.I)
RE_VERSION_WORD = re.compile(r"version", re.I)
RE_MISMATCH = re.compile(r"mismatch|does not match|!=|-ne\b|differs", re.I)
RE_REFUSE = re.compile(r"REFUSE")
RE_RIVAL_NAME = re.compile(r"^packages[-_.]")


class Problem:
    """One violated assertion. The id is load-bearing: the self-test asserts on it, so a
    mutation cannot be reported as 'some failure' and pass for a proof of non-vacuity."""

    def __init__(self, assertion, message):
        self.assertion = assertion
        self.message = message

    def __str__(self):
        return f"FAIL [{self.assertion}] {self.message}"


# ----------------------------------------------------------------------- parsing helpers

def load_workflows(directory):
    """Parse every workflow in the directory. Returns (workflows, refusal_message)."""
    if not os.path.isdir(directory):
        return None, f"workflow directory '{directory}' does not exist"
    found = {}
    for entry in sorted(os.listdir(directory)):
        if not entry.endswith((".yml", ".yaml")):
            continue
        path = os.path.join(directory, entry)
        try:
            with open(path, encoding="utf-8") as handle:
                doc = yaml.safe_load(handle.read())
        except Exception as exc:  # a workflow we cannot parse is unmeasured, not clean
            return None, f"could not parse '{entry}': {exc}"
        if isinstance(doc, dict):
            found[entry] = doc
    if not found:
        return None, f"no workflows parsed under '{directory}'; an empty set passes vacuously"
    return found, None


def jobs_of(workflow):
    jobs = (workflow or {}).get("jobs")
    return jobs if isinstance(jobs, dict) else {}


def steps_of(job):
    steps = (job or {}).get("steps")
    return [s for s in steps if isinstance(s, dict)] if isinstance(steps, list) else []


def step_run(step):
    return str(step.get("run") or "")


def step_uses(step):
    return str(step.get("uses") or "")


def step_with(step):
    with_block = step.get("with")
    return with_block if isinstance(with_block, dict) else {}


def job_text(job):
    """Every `run:` body in a job, concatenated. Used for presence checks only."""
    return "\n".join(step_run(s) for s in steps_of(job))


def needs_of(job):
    needs = (job or {}).get("needs") or []
    if isinstance(needs, str):
        return [needs]
    return [str(n) for n in needs] if isinstance(needs, list) else []


def transitive_needs(jobs, job_name, seen=None):
    seen = seen if seen is not None else set()
    for parent in needs_of(jobs.get(job_name)):
        if parent in seen:
            continue
        seen.add(parent)
        transitive_needs(jobs, parent, seen)
    return seen


def uploads_artifact(job, name):
    return [s for s in steps_of(job)
            if "upload-artifact" in step_uses(s) and str(step_with(s).get("name") or "") == name]


def downloads_artifact(job, name):
    return [s for s in steps_of(job)
            if "download-artifact" in step_uses(s) and str(step_with(s).get("name") or "") == name]


def any_download(job):
    return [s for s in steps_of(job) if "download-artifact" in step_uses(s)] or \
           [s for s in steps_of(job) if "gh run download" in step_run(s)]


def attest_steps(job):
    return [s for s in steps_of(job) if "attest-" in step_uses(s)]


def upload_paths(step):
    return str(step_with(step).get("path") or "")


def triggers_of(workflow):
    """`on:` is YAML 1.1 truth, so safe_load hands it back under the boolean key True."""
    for key in ("on", True, "True"):
        if key in (workflow or {}):
            block = workflow[key]
            if isinstance(block, dict):
                return block
    return {}


# --------------------------------------------------------------------------- assertions

def check(workflows):
    """Return the list of violated assertions. Empty list == the contract holds."""
    problems = []
    release = workflows.get(RELEASE_WF)
    official = workflows.get(OFFICIAL_WF)

    # -- A1: the release workflow cannot rebuild what it publishes -----------------------
    if not release:
        problems.append(Problem("A1", f"{RELEASE_WF} is missing; the release graph cannot be checked"))
        admission = None
    else:
        admission = jobs_of(release).get(ADMISSION_JOB)
        if not admission:
            problems.append(Problem("A1", f"{RELEASE_WF} has no '{ADMISSION_JOB}' job"))
        else:
            for step in steps_of(admission):
                run = step_run(step)
                if RE_DOTNET_PACK.search(run):
                    problems.append(Problem(
                        "A1",
                        f"'{ADMISSION_JOB}' runs `dotnet pack`; the release workflow would be "
                        "publishing an artifact it made itself, which is production, not promotion"))
                    break
            for step in steps_of(admission):
                if RE_BUILD_SHIPPING.search(step_run(step)):
                    problems.append(Problem(
                        "A1",
                        f"'{ADMISSION_JOB}' builds {SHIPPING_FILTER}; the shipping set must arrive "
                        "from the producing workflow, never be rebuilt here"))
                    break

    # The two checks above are keyed to a job NAME, and a name is not the property. A rival producer
    # in any OTHER job of the release workflow would satisfy both while re-creating the exact defect,
    # so the general form is asserted separately: ORIGINATING the bytes is what disqualifies a job.
    # Packing without uploading is deliberately permitted -- a validation pack nothing can download
    # is a set nothing can promote.
    if release:
        for job_name, job in jobs_of(release).items():
            if uploads_artifact(job, CANONICAL_ARTIFACT) and not any_download(job):
                problems.append(Problem(
                    "A1",
                    f"{RELEASE_WF} job '{job_name}' uploads '{CANONICAL_ARTIFACT}' without "
                    "downloading it first, so the release workflow ORIGINATES the package set it "
                    "publishes; re-uploading a verified download is promotion, minting one is not"))

    # -- A2: the producer exists and is complete -----------------------------------------
    producer = None
    if not official:
        problems.append(Problem(
            "A2",
            f"{OFFICIAL_WF} does not exist; there is no build to promote FROM, so every "
            "downstream verification would be checking an artifact against itself"))
    else:
        for name, job in jobs_of(official).items():
            if uploads_artifact(job, CANONICAL_ARTIFACT) and not any_download(job):
                producer = (name, job)
                break
        if not producer:
            problems.append(Problem(
                "A2",
                f"{OFFICIAL_WF} has no job that packs and uploads the '{CANONICAL_ARTIFACT}' "
                "artifact without first downloading it"))
        else:
            pname, pjob = producer
            text = job_text(pjob)
            if not RE_DOTNET_PACK.search(text):
                problems.append(Problem("A2", f"producer job '{pname}' never runs `dotnet pack`"))
            if not RE_SHA_PRODUCE.search(text):
                problems.append(Problem(
                    "A2",
                    f"producer job '{pname}' records no hash manifest; without one, "
                    "'the same packages' is an assertion nobody can check after the fact"))
            if not RE_SBOM_TOOL.search(text):
                problems.append(Problem("A2", f"producer job '{pname}' generates no SBOM"))
            uses = " ".join(step_uses(s) for s in steps_of(pjob))
            if "attest-build-provenance" not in uses:
                problems.append(Problem("A2", f"producer job '{pname}' does not attest build provenance"))
            if "attest-sbom" not in uses:
                problems.append(Problem(
                    "A2",
                    f"producer job '{pname}' does not attest the SBOM; a consumer could then read a "
                    "dependency list but not establish that it is THAT package's list"))
            paths = " ".join(upload_paths(s) for s in uploads_artifact(pjob, CANONICAL_ARTIFACT))
            for required in ("SHA256SUMS.txt", "build-receipt.json"):
                if required not in paths:
                    problems.append(Problem(
                        "A2",
                        f"the '{CANONICAL_ARTIFACT}' artifact does not carry {required}; computed on "
                        "the runner and left there, it answers the divergence question only during "
                        "the one run that cannot ask it"))

    # "Sole producer" is a COUNT, not an existence check. A complete, correct official build proves
    # nothing about promotion if a second workflow can also mint a set called 'packages' -- a
    # consumer would then have two origins and no rule for choosing between them.
    originators = [(wf_name, job_name)
                   for wf_name, workflow in workflows.items()
                   for job_name, job in jobs_of(workflow).items()
                   if uploads_artifact(job, CANONICAL_ARTIFACT) and not any_download(job)]
    if len(originators) > 1:
        where = ", ".join(f"{w} job '{j}'" for w, j in originators)
        problems.append(Problem(
            "A2",
            f"'{CANONICAL_ARTIFACT}' is originated in {len(originators)} places ({where}); exactly "
            "one workflow may mint the package set or promotion has no single origin to promote from"))
    elif originators and originators[0][0] != OFFICIAL_WF:
        problems.append(Problem(
            "A2",
            f"'{CANONICAL_ARTIFACT}' is originated by {originators[0][0]} job "
            f"'{originators[0][1]}' rather than by {OFFICIAL_WF}"))

    # -- A3: attestation only where the bytes were made ----------------------------------
    for wf_name, workflow in workflows.items():
        for job_name, job in jobs_of(workflow).items():
            if attest_steps(job) and any_download(job):
                problems.append(Problem(
                    "A3",
                    f"{wf_name} job '{job_name}' attests an artifact it DOWNLOADED; that is "
                    "provenance describing a journey rather than an origin"))
    if release:
        for job_name, job in jobs_of(release).items():
            if attest_steps(job):
                problems.append(Problem(
                    "A3",
                    f"{RELEASE_WF} job '{job_name}' mints an attestation; the release workflow "
                    "consumes a build it did not make and must not be able to vouch for it"))

    # -- A4: admission verifies, refuses, and gates every consumer -----------------------
    if admission:
        text = job_text(admission)
        cross_run = [s for s in downloads_artifact(admission, CANONICAL_ARTIFACT)
                     if {"run-id", "run_id", "github-token"} & set(step_with(s))]
        if not cross_run and "gh run download" not in text:
            problems.append(Problem(
                "A4",
                f"'{ADMISSION_JOB}' does not download '{CANONICAL_ARTIFACT}' from another run; "
                "an intra-run download would re-admit this workflow's own output"))
        if not RE_SHA_VERIFY.search(text):
            problems.append(Problem("A4", f"'{ADMISSION_JOB}' never runs `sha256sum -c`; the hash manifest is carried but not checked"))
        if not RE_ATTEST_VERIFY.search(text):
            problems.append(Problem("A4", f"'{ADMISSION_JOB}' never runs `gh attestation verify`; the attestation is present but unexamined"))
        if not any(RE_VERSION_WORD.search(step_run(s)) and RE_MISMATCH.search(step_run(s))
                   for s in steps_of(admission)):
            problems.append(Problem(
                "A4",
                f"'{ADMISSION_JOB}' has no step comparing the embedded version with a failure "
                "branch; a correctly-hashed, correctly-attested build of the WRONG version would pass"))
        if not RE_REFUSE.search(text):
            problems.append(Problem(
                "A4",
                f"'{ADMISSION_JOB}' has no REFUSE path; when no successful official build exists "
                "for the commit it must say so, not fall through"))

    if release:
        jobs = jobs_of(release)
        for job_name, job in jobs.items():
            if job_name == ADMISSION_JOB:
                continue
            if downloads_artifact(job, CANONICAL_ARTIFACT):
                if ADMISSION_JOB not in transitive_needs(jobs, job_name):
                    problems.append(Problem(
                        "A4",
                        f"{RELEASE_WF} job '{job_name}' consumes '{CANONICAL_ARTIFACT}' without "
                        f"'{ADMISSION_JOB}' in its transitive needs; it would run against an "
                        "unadmitted artifact"))

    # -- A5: draft until published --------------------------------------------------------
    if release:
        jobs = jobs_of(release)
        create = jobs.get("create-release")
        if not create:
            problems.append(Problem("A5", "create-release job is missing"))
        else:
            rel = [s for s in steps_of(create) if "action-gh-release" in step_uses(s)]
            if not rel:
                problems.append(Problem("A5", "create-release has no action-gh-release step"))
            elif step_with(rel[0]).get("draft") is not True:
                problems.append(Problem(
                    "A5",
                    f"create-release publishes with draft={step_with(rel[0]).get('draft')!r}; it must "
                    "be True so the release is not announced before the packages are on the feed"))
        fin = jobs.get("finalize-release")
        if not fin:
            problems.append(Problem("A5", "finalize-release job is missing; nothing would publish the draft"))
        else:
            if "publish-nuget" not in needs_of(fin):
                problems.append(Problem("A5", f"finalize-release does not depend on publish-nuget (needs={needs_of(fin)})"))
            cond = str(fin.get("if") or "")
            if "publish-nuget.result" not in cond or "success" not in cond:
                problems.append(Problem(
                    "A5",
                    f"finalize-release condition {cond!r} does not require publish-nuget to have "
                    "SUCCEEDED; it could publish a release whose packages failed to upload"))
            if "always()" in cond:
                problems.append(Problem("A5", "finalize-release uses always(); a failed publish must leave the release a draft"))

    # -- A6: one canonical artifact, no rival --------------------------------------------
    for wf_name, workflow in workflows.items():
        for job_name, job in jobs_of(workflow).items():
            for step in steps_of(job):
                if "upload-artifact" not in step_uses(step):
                    continue
                name = str(step_with(step).get("name") or "")
                if ".nupkg" in upload_paths(step) and name != CANONICAL_ARTIFACT:
                    problems.append(Problem(
                        "A6",
                        f"{wf_name} job '{job_name}' uploads .nupkg under the name '{name}'; a second "
                        "nameable package set is a rival any consumer could pick instead"))
                elif RE_RIVAL_NAME.match(name):
                    problems.append(Problem(
                        "A6",
                        f"{wf_name} job '{job_name}' uploads '{name}'; a suffixed package artifact "
                        "makes two candidate sets nameable and therefore pickable"))
            for step in steps_of(job):
                if "download-artifact" not in step_uses(step):
                    continue
                name = str(step_with(step).get("name") or "")
                if RE_RIVAL_NAME.match(name):
                    problems.append(Problem(
                        "A6",
                        f"{wf_name} job '{job_name}' downloads '{name}' rather than the canonical "
                        f"'{CANONICAL_ARTIFACT}'"))

    # -- A7: this gate's workflow fires on the workflows it asserts about ------------------
    rehearsal = workflows.get(REHEARSAL_WF)
    if rehearsal:
        trig = triggers_of(rehearsal)
        for event in ("push", "pull_request"):
            block = trig.get(event)
            paths = (block or {}).get("paths") if isinstance(block, dict) else None
            paths = [str(p) for p in paths] if isinstance(paths, list) else []
            for required in (RELEASE_WF, OFFICIAL_WF):
                if not any(required in p for p in paths):
                    problems.append(Problem(
                        "A7",
                        f"{REHEARSAL_WF} '{event}' paths do not include {required}; the one edit "
                        "these assertions exist to catch is the edit that would not trigger them"))
    return problems


# ---------------------------------------------------------------------------- fixtures

def compliant_fixtures():
    """A minimal tree that satisfies every assertion. The self-test mutates copies of this."""
    official = {
        "name": "Official Build",
        "jobs": {
            "official-build": {
                "permissions": {"id-token": "write", "attestations": "write"},
                "steps": [
                    {"name": "Pack", "run": "dotnet pack eng/ci/shards/ShippingOnly.slnf -o packages"},
                    {"name": "Hash", "run": "sha256sum *.nupkg | tee SHA256SUMS.txt"},
                    {"name": "Receipt", "run": "echo '{}' > packages/build-receipt.json"},
                    {"name": "SBOM", "run": "dotnet CycloneDX ./src -o ./sbom -F Json"},
                    {"name": "Provenance", "uses": "actions/attest-build-provenance@v4",
                     "with": {"subject-path": "packages/*.nupkg"}},
                    {"name": "SBOM attestation", "uses": "actions/attest-sbom@v4",
                     "with": {"subject-path": "packages/*.nupkg", "sbom-path": "sbom/bom.json"}},
                    {"name": "Upload", "uses": "actions/upload-artifact@v7",
                     "with": {"name": "packages",
                              "path": "packages/*.nupkg\npackages/SHA256SUMS.txt\npackages/build-receipt.json"}},
                ],
            }
        },
    }
    release = {
        "name": "Release",
        "jobs": {
            "build-packages": {
                "steps": [
                    {"name": "Admit", "uses": "actions/download-artifact@v8",
                     "with": {"name": "packages", "run-id": "${{ steps.official.outputs.run-id }}",
                              "github-token": "${{ github.token }}"}},
                    {"name": "Require an official build",
                     "run": "if [ -z \"$RUN_ID\" ]; then echo '::error::REFUSE: no successful official build for this commit'; exit 3; fi"},
                    {"name": "Verify hashes", "run": "sha256sum -c SHA256SUMS.txt"},
                    {"name": "Verify attestation", "run": "gh attestation verify packages/*.nupkg --repo $GITHUB_REPOSITORY"},
                    {"name": "Verify version",
                     "run": "if [ \"$EMBEDDED\" != \"$VERSION\" ]; then echo 'version mismatch'; exit 1; fi"},
                    {"name": "Re-upload the admitted set", "uses": "actions/upload-artifact@v7",
                     "with": {"name": "packages", "path": "packages/*.nupkg"}},
                ]
            },
            "staging-validation": {
                "needs": ["build-packages"],
                "steps": [{"uses": "actions/download-artifact@v8", "with": {"name": "packages"}}],
            },
            "create-release": {
                "needs": ["staging-validation"],
                "steps": [
                    {"uses": "actions/download-artifact@v8", "with": {"name": "packages"}},
                    {"uses": "softprops/action-gh-release@v2", "with": {"draft": True}},
                ],
            },
            "publish-nuget": {
                "needs": ["staging-validation", "create-release"],
                "steps": [{"uses": "actions/download-artifact@v8", "with": {"name": "packages"}}],
            },
            "finalize-release": {
                "needs": ["publish-nuget"],
                "if": "needs.publish-nuget.result == 'success'",
                "steps": [{"run": "gh release edit --draft=false"}],
            },
        },
    }
    ci = {
        "name": "CI",
        "jobs": {
            "package": {
                "steps": [
                    # Packing for validation is fine: nothing uploads it, so nothing can promote it.
                    {"name": "Package validation", "run": "dotnet pack eng/ci/shards/ShippingOnly.slnf -o ./packages"},
                ]
            }
        },
    }
    rehearsal = {
        "name": "Release Rehearsal",
        "jobs": {"release-rehearsal": {"steps": [{"run": "./eng/ci/release-rehearsal.ps1"}]}},
    }
    # `on:` is written explicitly under the boolean key PyYAML will hand back on load, so the
    # A7 trigger arm exercises the same code path the real workflow takes.
    for wf in (rehearsal,):
        wf[True] = {
            "push": {"paths": [".github/workflows/release.yml", ".github/workflows/official-build.yml"]},
            "pull_request": {"paths": [".github/workflows/release.yml", ".github/workflows/official-build.yml"]},
        }
    return {RELEASE_WF: release, OFFICIAL_WF: official, "ci.yml": ci, REHEARSAL_WF: rehearsal}


def _admission(wfs):
    return wfs[RELEASE_WF]["jobs"]["build-packages"]


def _producer(wfs):
    return wfs[OFFICIAL_WF]["jobs"]["official-build"]


def _drop_step(job, token):
    job["steps"] = [s for s in job["steps"]
                    if token not in str(s.get("run") or "") and token not in str(s.get("uses") or "")]


MUTATIONS = [
    ("A1", "release rebuilds via dotnet pack",
     lambda w: _admission(w)["steps"].append({"run": "dotnet pack eng/ci/shards/ShippingOnly.slnf"})),
    ("A1", "release rebuilds the shipping filter",
     lambda w: _admission(w)["steps"].append({"run": "dotnet build eng/ci/shards/ShippingOnly.slnf -c Release"})),
    # The rival-producer arms. A job NAME is not the property, so these move the defect to a job the
    # named checks above do not look at -- the shape that would otherwise pass while the release
    # workflow still mints its own package set.
    ("A1", "a differently-named release job mints the package set",
     lambda w: w[RELEASE_WF]["jobs"].__setitem__("repack", {
         "steps": [{"run": "dotnet pack eng/ci/shards/ShippingOnly.slnf -o packages"},
                   {"uses": "actions/upload-artifact@v7",
                    "with": {"name": "packages", "path": "packages/*.nupkg"}}]})),
    ("A2", "a second workflow also originates the package set",
     lambda w: w["ci.yml"]["jobs"].__setitem__("rival", {
         "steps": [{"run": "dotnet pack eng/ci/shards/ShippingOnly.slnf -o packages"},
                   {"uses": "actions/upload-artifact@v7",
                    "with": {"name": "packages", "path": "packages/*.nupkg"}}]})),
    ("A2", "the producing workflow is deleted",
     lambda w: w.pop(OFFICIAL_WF)),
    ("A2", "producer stops attesting the SBOM",
     lambda w: _drop_step(_producer(w), "attest-sbom")),
    ("A2", "producer stops attesting provenance",
     lambda w: _drop_step(_producer(w), "attest-build-provenance")),
    ("A2", "producer stops generating an SBOM",
     lambda w: _drop_step(_producer(w), "CycloneDX")),
    ("A2", "producer stops hashing",
     lambda w: _drop_step(_producer(w), "sha256sum")),
    ("A2", "the hash manifest is left off the artifact",
     lambda w: _producer(w)["steps"][-1]["with"].__setitem__("path", "packages/*.nupkg\npackages/build-receipt.json")),
    ("A2", "the build receipt is left off the artifact",
     lambda w: _producer(w)["steps"][-1]["with"].__setitem__("path", "packages/*.nupkg\npackages/SHA256SUMS.txt")),
    ("A3", "the release workflow attests a downloaded copy",
     lambda w: _admission(w)["steps"].append(
         {"uses": "actions/attest-build-provenance@v4", "with": {"subject-path": "packages/*.nupkg"}})),
    ("A4", "admission stops verifying hashes",
     lambda w: _drop_step(_admission(w), "sha256sum -c")),
    ("A4", "admission stops verifying attestation",
     lambda w: _drop_step(_admission(w), "gh attestation verify")),
    ("A4", "admission stops checking the version",
     lambda w: _drop_step(_admission(w), "$EMBEDDED")),
    ("A4", "admission loses its REFUSE path",
     lambda w: _drop_step(_admission(w), "REFUSE")),
    ("A4", "admission downloads from its own run instead of the official build",
     lambda w: _admission(w)["steps"][0].__setitem__("with", {"name": "packages"})),
    ("A4", "a consumer is no longer gated behind admission",
     lambda w: w[RELEASE_WF]["jobs"]["staging-validation"].__setitem__("needs", [])),
    ("A5", "the release is created already published",
     lambda w: w[RELEASE_WF]["jobs"]["create-release"]["steps"][1]["with"].__setitem__("draft", False)),
    ("A5", "finalize runs with always()",
     lambda w: w[RELEASE_WF]["jobs"]["finalize-release"].__setitem__(
         "if", "always() && needs.publish-nuget.result == 'success'")),
    ("A5", "finalize no longer requires publish to have succeeded",
     lambda w: w[RELEASE_WF]["jobs"]["finalize-release"].__setitem__("if", "")),
    ("A6", "a rival suffixed package artifact appears",
     lambda w: _producer(w)["steps"].append(
         {"uses": "actions/upload-artifact@v7", "with": {"name": "packages-ubuntu", "path": "packages/*.nupkg"}})),
    ("A6", "the orphaned build artifact uploads packages under a second name",
     lambda w: w["ci.yml"]["jobs"]["package"]["steps"].append(
         {"uses": "actions/upload-artifact@v7", "with": {"name": "build-artifacts", "path": "./packages/*.nupkg"}})),
    ("A7", "the rehearsal stops firing on producer edits",
     lambda w: w[REHEARSAL_WF][True]["push"].__setitem__("paths", [".github/workflows/release.yml"])),
]


def write_fixtures(directory, workflows):
    for name, doc in workflows.items():
        with open(os.path.join(directory, name), "w", encoding="utf-8") as handle:
            yaml.safe_dump(doc, handle, sort_keys=False)


def self_test():
    """Prove every assertion can FAIL, and that a compliant tree PASSES.

    Both arms are required. A gate proven only to fail is satisfied by one that rejects
    everything, and a gate proven only to pass is satisfied by one that asserts nothing."""
    failures = []
    tmp = tempfile.mkdtemp(prefix="promotion-contract-selftest-")

    # LIVENESS: a compliant tree must come back clean. Without this arm, every mutation
    # below would still go red under a gate that simply always fails.
    base = os.path.join(tmp, "compliant")
    os.makedirs(base)
    write_fixtures(base, compliant_fixtures())
    parsed, refusal = load_workflows(base)
    if refusal:
        failures.append(f"compliant fixture REFUSED to load: {refusal}")
    else:
        problems = check(parsed)
        if problems:
            failures.append("compliant fixture reported " + str(len(problems)) +
                            " problem(s): " + "; ".join(str(p) for p in problems))
        else:
            print("SELF-TEST: PASS -- a compliant workflow tree is accepted (liveness)")

    # SAFETY: each mutation must be caught, and caught BY THE ASSERTION IT VIOLATES. A gate
    # that goes red for the wrong reason is not binding the property it advertises.
    for index, (assertion, label, mutate) in enumerate(MUTATIONS):
        case_dir = os.path.join(tmp, f"case{index:02d}")
        os.makedirs(case_dir)
        mutated = copy.deepcopy(compliant_fixtures())
        mutate(mutated)
        write_fixtures(case_dir, mutated)
        parsed, refusal = load_workflows(case_dir)
        if refusal:
            failures.append(f"[{assertion}] {label}: fixture REFUSED to load: {refusal}")
            continue
        problems = check(parsed)
        hit = [p for p in problems if p.assertion == assertion]
        if not problems:
            failures.append(f"[{assertion}] {label}: mutation went UNDETECTED -- the gate is vacuous here")
        elif not hit:
            failures.append(f"[{assertion}] {label}: caught, but by "
                            f"{sorted({p.assertion for p in problems})} rather than {assertion}")
        else:
            print(f"SELF-TEST: PASS -- {assertion} goes RED when {label} (safety)")

    # SAFETY: an unreadable subject must REFUSE, never report a clean contract.
    parsed, refusal = load_workflows(os.path.join(tmp, "does-not-exist"))
    if parsed is not None or not refusal:
        failures.append("a missing workflow directory did not REFUSE")
    else:
        print("SELF-TEST: PASS -- an unreadable workflow tree REFUSES rather than passing (safety)")

    empty = os.path.join(tmp, "empty")
    os.makedirs(empty)
    parsed, refusal = load_workflows(empty)
    if parsed is not None or not refusal:
        failures.append("an empty workflow directory did not REFUSE")
    else:
        print("SELF-TEST: PASS -- an empty workflow tree REFUSES rather than passing vacuously (safety)")

    if failures:
        for item in failures:
            print(f"SELF-TEST: FAIL -- {item}")
        return EXIT_FAIL
    print(f"SELF-TEST: the promotion-contract gate is non-vacuous "
          f"({len(MUTATIONS)} mutations, each caught by its own assertion).")
    return EXIT_PASS


# --------------------------------------------------------------------------------- main

def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--workflows", default=".github/workflows",
                        help="directory holding the workflow files to assert over")
    parser.add_argument("--self-test", action="store_true",
                        help="prove every assertion can fail, then exit")
    args = parser.parse_args(argv)

    if args.self_test:
        return self_test()

    workflows, refusal = load_workflows(args.workflows)
    if refusal:
        print(f"::error::REFUSE: {refusal}. Nothing was measured -- this is not a pass.")
        return EXIT_REFUSE

    problems = check(workflows)
    if problems:
        for problem in problems:
            print(f"::error::{problem}")
        by_assertion = sorted({p.assertion for p in problems})
        print(f"\nPROMOTION CONTRACT: FAILED -- {len(problems)} violation(s) "
              f"across {', '.join(by_assertion)}")
        return EXIT_FAIL

    # Enumerate what was actually checked. A success message that under-reports its own
    # coverage cannot tell a reader whether a newly-added arm ran at all.
    print("PROMOTION CONTRACT: PASSED -- checked that the release workflow neither packs nor "
          "builds the shipping set (A1); that the producing workflow packs, hashes, generates an "
          "SBOM and attests both provenance and SBOM over the artifact it uploads, carrying the "
          "hash manifest and build receipt with it (A2); that no workflow attests an artifact it "
          "downloaded and the release workflow mints no attestation at all (A3); that admission "
          "pulls the set cross-run and verifies hashes, attestation and version, refuses without "
          "an official build, and gates every consumer behind itself (A4); that the release is "
          "created as a draft and finalized only on a successful publish, without always() (A5); "
          "that exactly one package artifact name exists and no rival is uploaded or downloaded "
          "(A6); and that the rehearsal fires on edits to both workflows it asserts about (A7).")
    return EXIT_PASS


if __name__ == "__main__":
    sys.exit(main())
