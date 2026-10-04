# GitHub Actions workflows

This directory holds the CI/CD configuration for Excalibur.

## Where the authoritative list lives

**Do not maintain a workflow list here.** Every enumerable fact about the workflows — how many there
are, every job, its triggers, its wall-clock budget, whether it builds or tests, whether it inherits
token permissions, whether it is soft — is generated directly from the workflow files into:

> **`eng/reports/ci-workflow-inventory.md`**

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
| Official build | `official-build.yml` | Builds a commit once and produces the canonical package set: signed, then hashed, with its SBOM, build receipt, and provenance attestation. Runs on every merge to `main`, and on a release tag. |
| Scheduled validation | `nightly.yml` | Work that is expensive, probabilistic, or infrastructure-sensitive, and does not belong on the critical path of a pull request. |
| Release promotion | `release.yml` | Promotes an artifact the official build produced. It does not build the packages it publishes. |

### Why the release workflow does not build

A release workflow that builds its own packages is not promoting anything — it is producing and
publishing in one step, and "the artifact was verified" then means it verified its own output.
`release.yml` instead waits for the official build of the same commit, downloads that run's
artifact, and verifies the hash manifest, the provenance attestation, and the embedded version
before any consumer sees it. If no successful official build exists for the commit, it refuses; it
never falls back to building.

Provenance is attested in the official producing workflow. Its isolated finalizer verifies an
internal handoff from that same run and attempt before it signs and attests the final bytes.
The release workflow only consumes and verifies those attestations; it never re-attests a download.

### Why signing happens at the origin, before the hash manifest

Signing a package rewrites it. The signature becomes an entry in the archive, so the file's hash
changes — and everything this pipeline says about a package is a statement about bytes. Sign after
the manifest, the attestation, and the staged install test, and all three describe a file that no
longer exists, while the pipeline goes on reporting that these bytes were validated. That claim
would then be false of every byte a consumer receives, on every release that signs.

`official-build.yml` builds and generates its SBOM without signing credentials or OIDC authority.
A fresh finalizer downloads the unsigned handoff by artifact ID, verifies its run, attempt, source,
manifest digest, complete file inventory and contents, and then optionally signs. It checks that
signing preserved package payloads before generating the final manifest, receipt and attestations.
Only this final canonical artifact is admitted, staged, install-tested and published.

The finalizer executes no source checkout, build cache, downloaded helper or project/tool restore.
Its exact job and inherited execution environment are bound by
`eng/ci/official-finalizer-contract.json`. A deliberate change requires independent review and an
explicit contract update; CI never regenerates that input to accept a changed privileged job.
The handoff tests execute the actual inline workflow verifiers against substituted and incomplete
artifacts. This contract and these tests complement code review; they do not prove hosted execution.

`release.yml` therefore signs nowhere. It checks the **artifact** — that the packages carry a
signature and that the signature is valid — rather than checking whether a certificate happens to be
configured on the runner, which is a fact about the environment rather than about what ships. A
package that is unsigned by accident and one that is unsigned by decision look identical at that
moment, so absence refuses and only an explicit `ALLOW_UNSIGNED_RELEASE=true` proceeds.

**Releases are currently produced UNSIGNED**, because author signing requires a certificate from a
public CA — nuget.org rejects self-issued ones — and none is available at present. Rather than leave
that state to an environment setting someone has to know to look for, `ALLOW_UNSIGNED_RELEASE`
defaults to `true` in both workflows, in the open, where it can be read and reversed. Every
promotable build still emits a warning saying the set carries no author signature: unsigned by
default must not become unsigned and silent, which is the entire reason the control exists.

Signing remains optional. Before enabling it, the release owner must configure the `package-signing`
environment with required reviewers, self-review disabled, administrator bypass disabled, and exactly
the custom branch policy `main` and tag policy `v*`. Store both signing secrets in that environment,
never at repository scope. Configure `ALLOW_UNSIGNED_RELEASE=false` when signed releases are required.
The finalizer checks the actual protection rules before signing and refuses partial credentials or
signing/verification failures. An environment name in YAML alone does not establish these protections.
Certificate issuance, expiry monitoring, rotation and revocation remain the release owner's duties;
rotate both secrets together and validate a new candidate before publication. No settings or secrets
are provisioned by this repository change. Rehearsal receives no author-signing credentials.

Consumers are unaffected in one respect worth stating plainly: nuget.org applies its own repository
signature to everything it serves, so packages remain verifiable. What is absent is the second,
publisher-level guarantee an author signature adds on top.

Immediately before the push, the provenance attestation is verified again. Between admission and the
push the set crosses an artifact store where it is identified by *name*, not by content, and the hash
manifest cannot close that gap on its own: `SHA256SUMS.txt` travels inside the artifact, so
re-running it downstream proves only self-consistency. The attestation is the one statement that did
not travel with the bytes, and it is checked at the last moment it can still stop a push.

That contract is enforced structurally rather than by convention:
`python3 eng/ci/promotion-contract-gate.py` asserts it against the workflow files — including the
signing order above — and runs as part of the release rehearsal. It carries a `--self-test` proving
each assertion can fail. `python3 eng/ci/assert-packages-signed.py --self-test` does the same for the
signature check the publishing job runs against the artifact.

## Documentation permissions

Documentation compilation runs without Pages write or OIDC permissions. The website
build has `contents: read` and `pages: read` so it can read the existing Pages
configuration. Only its deployment job receives `pages: write` and `id-token: write`,
and that job depends on a successful build and is restricted to non-PR runs on `main`.
The API-reference workflow generates an artifact and receives only `contents: read`.
The `github-pages` environment declaration identifies the deployment environment;
its actual protection rules must be checked in repository settings.

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
