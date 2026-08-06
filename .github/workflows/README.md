# GitHub Actions workflows

This directory holds the CI/CD configuration for Excalibur.

## Where the authoritative list lives

**Do not maintain a workflow list here.** Every enumerable fact about the workflows — how many there
are, every job, its triggers, its wall-clock budget, whether it builds or tests, whether it inherits
token permissions, whether it is soft — is generated directly from the workflow files into:

> **`management/reports/ci-workflow-inventory.md`**

Regenerate it with `python3 eng/ci/generate-workflow-inventory.py`. A CI gate runs that script with
`--check` and fails when the committed document no longer matches the workflows it describes.

This split exists because the previous version of this file was hand-maintained, and it drifted in
the way hand-maintained inventories always do: it described four workflows when there were
twenty-one, and asserted a test count and a coverage threshold that were both wrong by a wide
margin. A drifted inventory is worse than no inventory, because it is trusted. Numbers, counts,
durations, and per-workflow tables therefore do not belong in this file — if a fact can be derived
from the workflows, derive it.

## The tiers

The workflow set is organised in four tiers. This is the part that is stable enough to write down.

| Tier | Workflow | Purpose |
|---|---|---|
| PR validation | `ci.yml` | Validates a change before merge. Exposes exactly one aggregated required status, so branch protection never has to be edited when a job is added or removed. |
| Official build | `official-build.yml` | Builds a commit once and produces the canonical package set with its hash manifest, SBOM, build receipt, and provenance attestation. Runs on every merge to `main`, and on a release tag. |
| Scheduled validation | `nightly.yml` | Work that is expensive, probabilistic, or infrastructure-sensitive, and does not belong on the critical path of a pull request. |
| Release promotion | `release.yml` | Promotes an artifact the official build produced. It does not build the packages it publishes. |

### Why the release workflow does not build

A release workflow that builds its own packages is not promoting anything — it is producing and
publishing in one step, and "the artifact was verified" then means it verified its own output.
`release.yml` instead waits for the official build of the same commit, downloads that run's
artifact, and verifies the hash manifest, the provenance attestation, and the embedded version
before any consumer sees it. If no successful official build exists for the commit, it refuses; it
never falls back to building.

Provenance is attested in the workflow that *produces* the artifact, never in one that downloaded
it. An attestation minted over a downloaded copy describes a journey, not an origin.

That contract is enforced structurally rather than by convention:
`python3 eng/ci/promotion-contract-gate.py` asserts it against the workflow files and runs as part
of the release rehearsal. It carries a `--self-test` proving each assertion can fail.

## Releasing

Push a tag of the form `v<version>`. The tag defines the version, and it must point at `HEAD` of a
clean tree. Two runs start: the official build produces the artifact for that commit, and the
release workflow waits for it, verifies it, publishes to a staging feed, validates installation from
that feed, publishes to NuGet from a protected environment, and only then flips the GitHub release
out of draft. A failed publish leaves the release as a draft rather than announcing packages that
are not on the feed.

Re-running a release that partially completed is safe: publication state is probed per package
before anything is pushed.

## Running the same checks locally

Build and test through the same entry point CI uses, rather than reconstructing its arguments:

```bash
./build.sh --restore --build            # or: .\build.cmd -Restore -Build
./build.sh --test --project eng/ci/shards/UnitTests.slnf
./build.sh --pack --configuration Release
```

Individual gates under `eng/ci/` are runnable directly. Any gate that supports `--self-test` will
demonstrate that it is capable of failing, which is the only way to know a green result from it
means anything.
