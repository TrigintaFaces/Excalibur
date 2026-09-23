# Release Process

This document describes the release process for Excalibur NuGet packages.

---

## Overview

Excalibur follows a **stable, tested, documented** release philosophy:

- All releases are **automated** via GitHub Actions
- Packages are published to **NuGet.org** upon release
- Both **stable** and **pre-release** versions are supported
- Shipping count is validated by CI (`eng/ci/validate-shipping-filter.ps1`) against the current source graph.

### Versioning

**The package major version matches the targeted .NET major version.** The runtime packages
single-target `net10.0`, so the current line is `10.x`; when the development line moves to a newer
runtime it ships as `11.x`. The major number is **not** an independent API-break counter.

**This is a statement about the VERSION, not about every package's TFM.** Two shipping packages
target `netstandard2.0` and are versioned `10.x` like the rest: `Excalibur.Dispatch.Analyzers` and
`Excalibur.Dispatch.Migration`, because Roslyn loads analyzers into the compiler process and requires
that target. Do not "fix" them to `net10.0`.

| Version | Meaning | Example |
|---------|---------|---------|
| `MAJOR` | The targeted .NET major | `10.x` targets `net10.0` |
| `MINOR` | New features, backward compatible within the major line | `10.1.0` |
| `PATCH` | Bug fixes, backward compatible within the major line | `10.0.1` |
| `X.Y.Z-alpha.N` | Alpha pre-release | `10.0.0-alpha.12` |
| `X.Y.Z-beta.N` | Beta pre-release | `10.0.0-beta.1` |
| `X.Y.Z-rc.N` | Release candidate - API frozen | `10.0.0-rc.1` |

Minor and patch releases follow [Semantic Versioning](https://semver.org/) **within a major line**.
Breaking API changes are reserved for a new major line, which coincides with adopting a new .NET major.

The consumer-facing statement of this policy is
[Versioning and release stages](https://docs.excalibur-dispatch.dev/docs/migration/version-upgrades),
and it also fixes how long each line is supported, what is backported to an older line, and the
release cadence. **This section must not diverge from it.**

### Release Ownership

| Question | Answer |
|----------|--------|
| Who owns a release? | The repository maintainers, acting through the `nuget-production` GitHub environment |
| What publishes to NuGet.org? | The publish job in `release.yml`, running in that environment. It holds **no stored key** -- the credential is minted per run by a Trusted Publishing OIDC exchange whose nuget.org policy names this repository, that workflow file and that environment |
| How is one started? | A maintainer pushing a version tag, or a manual workflow dispatch |

The **Manual Release (Local)** flow below builds, validates and inspects packages. It does **not**
publish them: the only documented path to NuGet.org is that workflow.

---

## Pre-Release Checklist

Before creating a release, verify the following:

### Build & Test

- [ ] `dotnet build` succeeds with 0 errors
- [ ] All CI test shards pass (unit/integration/functional/conformance/architecture/boundary/contract)
- [ ] `eng/ci/shards/ShippingOnly.slnf` builds with **0 warnings**
- [ ] Sample validation passes (`pwsh eng/validate-samples.ps1`)

```bash
# Verify build and tests
dotnet build Excalibur.sln -c Release
dotnet test Excalibur.sln -c Release

# Verify samples
pwsh eng/validate-samples.ps1
```

### Documentation

- [ ] `docs-site` builds cleanly (`npm run build`)
- [ ] `CHANGELOG.md` updated with release notes
- [ ] Breaking changes documented (if MAJOR bump)
- [ ] **First stable release only — cut the docs-site version.** The site is unversioned while only
      prereleases exist, which is correct: every page is `current`. The moment a stable version ships
      without this step, a consumer holding it reads a site that keeps moving, with no banner telling
      them the content is unreleased. The machinery is already built and dormant in
      `docs-site/docusaurus.config.ts`:
  - [ ] `cd docs-site && npm run docusaurus docs:version <version>` (creates `versions.json` and
        `versioned_docs/`, which the config reads if present and tolerates if absent)
  - [ ] Uncomment the version block in `docusaurus.config.ts` with `path: ''` so the stable line
        serves from the site root
  - [ ] Set `DOCS_HAS_STABLE_RELEASE=true` in the docs build environment, and
        `DOCS_LAST_STABLE_VERSION=<version>` if you need to pin which version is treated as latest
  - [ ] Confirm the result: the version dropdown lists the released version and `Next (unreleased)`,
        the released line serves from the root, and `current` has moved to `/next` behind an
        `unreleased` banner

### Ecosystem Review

Recorded once per release, in the release notes, so that the evidence exists at the time the decision
was made rather than being reconstructed later:

- [ ] **Platform support** - the targeted .NET major is still officially supported; record the version
      actually built against, and the source for its support status
- [ ] **Dependency updates** - what moved, and what was deliberately held back and why
- [ ] **Compatibility floors retained** - no dependency or compiler-host floor was raised without
      compatibility proof; *latest* does not authorize previews or floating versions
- [ ] **Deprecations** - anything newly deprecated, and where its migration is documented
- [ ] **New applicable platform features** - adopted, or noted with the reason for not adopting
- [ ] **Validation** - the full suite passed on the exact candidate SHA, with the package hashes it
      produced. A skipped, quarantined or infrastructure-unavailable case is not a pass

### Version Bump

- [ ] Version updated in `Directory.Build.props`
- [ ] All package versions consistent

```xml
<!-- Directory.Build.props -->
<Version>X.Y.Z</Version>
```

### Template Version Pin (stable cut only)

The scaffolding templates under `templates/**` default `$(ExcaliburDispatchVersion)` to a floating
prerelease (`10.0.0-*`) so `dotnet new` always resolves the newest prerelease during the alpha/beta/rc
line. **This step applies only when THIS release is the first stable `10.0.0` tag** — do not perform it
on an alpha, beta, or rc release, and do not perform it early:

- [ ] Every `templates/**/*.csproj` default for `$(ExcaliburDispatchVersion)` is switched from the
      prerelease-floating form (`10.0.0-*`) to the stable-floating form (`10.*`), so a fresh scaffold
      keeps resolving the newest version but stops silently picking up a prerelease once one exists.
- [ ] After switching, `dotnet new` + `dotnet restore` is run for at least one template per family and
      the resolved package versions are confirmed to carry no prerelease suffix.

Getting this ordering wrong in either direction breaks `dotnet new` for every consumer: switching to
`10.*` before a stable `10.0.0` exists on the feed means NuGet resolves `>= 10.0.0` against a feed that
has none, and every fresh scaffold fails to restore before a consumer writes a line of code; leaving the
prerelease-floating default in place after `10.0.0` ships stable means a fresh scaffold keeps silently
resolving prerelease packages instead of the stable line just released.

---

## Automated Pipeline

The release pipeline is defined in `.github/workflows/release.yml`.

### Trigger Methods

| Method | Trigger | Use Case |
|--------|---------|----------|
| **Git tag** | Push tag `v*` | Standard releases |
| **Manual dispatch** | GitHub Actions UI | Ad-hoc releases |

### Pipeline Jobs

The automated pipeline runs 11 jobs. `pre-release-validation` is the only entry point; every other
job depends on it, and the order below is its `needs:` graph:

```
pre-release-validation
        │
pre-tag-soak-build  ->  pre-tag-unit-soak
        │
        ├── build-packages (Ubuntu)
        └── build-packages (Windows)
                │
        ├── release-quality-gates
        └── staging-validation
                │
        create-release        (needs release-quality-gates)
                │
        publish-nuget         (needs staging-validation)
                │
        ├── finalize-release
        ├── announce-draft-retained
        └── post-release
```

| Job | Purpose |
|-----|---------|
| `pre-release-validation` | Version check, build, critical tests |
| `pre-tag-soak-build` | Builds the soak candidate before the tag exists |
| `pre-tag-unit-soak` | Runs the unit suite against that candidate |
| `build-packages` (x2) | Multi-OS package building |
| `release-quality-gates` | Security scan, package analysis |
| `staging-validation` | Validates the built packages before publish |
| `create-release` | GitHub release with auto notes |
| `publish-nuget` | NuGet.org publish with verification |
| `finalize-release` | Promotes the release once publish succeeds |
| `announce-draft-retained` | Reports a draft left in place when publish does not complete |
| `post-release` | Summary, notifications |

Durations are not listed: they are not measured anywhere, and a number nobody re-measures becomes a
claim the next reader inherits.

### Package Count

Each release produces the current validated shipping set (see `eng/ci/validate-shipping-filter.ps1` report):

- **Dispatch packages**: Core, Abstractions, Transports, Hosting, Serialization, Observability
- **Excalibur packages**: Domain, EventSourcing, Saga, Hosting, Compliance, LeaderElection

Runtime packages target `net10.0` (single-target; .NET 8 / .NET 9 multi-target was dropped). **The
analyzer and migration packages target `netstandard2.0`**, because Roslyn loads analyzers into the
compiler process and requires that target — `Excalibur.Dispatch.Analyzers` and
`Excalibur.Dispatch.Migration` are the two shipping packages this applies to.

---

## Manual Release (Local)

For testing or emergency releases, you can build packages locally.

### 1. Build and Pack

```powershell
# Clean build
dotnet clean
dotnet restore

# Build release configuration
dotnet build -c Release

# Create packages
dotnet pack -c Release --output ./release-packages/
```

### 2. Validate Package Count

```powershell
$packages = Get-ChildItem ./release-packages/*.nupkg
$packages | Measure-Object  # Expected: current shipping count from eng/ci/validate-shipping-filter.ps1
```

### 3. Inspect Package Contents

```powershell
# Check package metadata
dotnet nuget inspect ./release-packages/Excalibur.Dispatch.1.0.0.nupkg

# Verify TFM
# Should show lib/net10.0/ folder
```

### 4. Local NuGet Testing

```powershell
# Add local feed
dotnet nuget add source ./release-packages/ -n LocalTest

# Test installation in a new project
dotnet new console -n TestProject
cd TestProject
dotnet add package Excalibur.Dispatch --version 1.0.0 --source LocalTest
```

---

## Dry Run

Before a real release, perform a dry run to validate packaging:

```powershell
# 1. Clean build
dotnet clean
dotnet restore

# 2. Pack all shipping packages
dotnet pack --configuration Release --output ./release-test/

# 3. Verify count
$packages = Get-ChildItem ./release-test/*.nupkg
$packages | Measure-Object  # Should match current validated shipping count

# 4. Validate a few key packages
dotnet nuget inspect ./release-test/Excalibur.Dispatch.1.0.0.nupkg
dotnet nuget inspect ./release-test/Excalibur.Dispatch.Abstractions.1.0.0.nupkg
dotnet nuget inspect ./release-test/Excalibur.Domain.1.0.0.nupkg

# 5. Cleanup
Remove-Item -Recurse ./release-test/
```

### Expected Results

| Check | Expected |
|-------|----------|
| Package count | Matches current validated shipping count |
| Build errors | 0 |
| Build warnings | 0 |
| Each package contains | `lib/net10.0/` — except the analyzer and migration packages, which ship at `analyzers/dotnet/cs` |

---

## Creating a Release

### Method 1: Git Tag (Recommended)

```bash
# 1. Ensure main is up to date
git checkout main
git pull origin main

# 2. Create and push tag
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0
```

The pipeline automatically:
- Validates the version format
- Builds and tests
- Creates GitHub release
- Publishes to NuGet.org

### Method 2: Manual Dispatch

1. Go to **Actions** > **Release** workflow
2. Click **Run workflow**
3. Enter version (e.g., `1.0.0`)
4. Check **Pre-release** if applicable
5. Click **Run workflow**

---

## Post-Release Tasks

After a successful release:

1. **Verify NuGet.org**
   - Check packages are available: https://www.nuget.org/packages?q=Dispatch
   - May take 15-30 minutes to propagate

2. **Update Documentation**
   - Update version references in docs
   - Add migration notes if needed

3. **Announce**
   - GitHub Discussions
   - Social media (optional)

4. **Monitor**
   - Watch for issue reports
   - Check NuGet download stats

---

## Troubleshooting

### Build Failures

| Issue | Solution |
|-------|----------|
| Missing dependencies | Run `dotnet restore` |
| Version mismatch | Check `Directory.Build.props` |
| Test failures | Fix tests before release |

### Packaging Issues

| Issue | Solution |
|-------|----------|
| Wrong package count | Check `eng/ci/shards/ShippingOnly.slnf` includes all projects |
| Missing DLLs | Verify `<IsPackable>true</IsPackable>` |
| Missing TFM | Check `<TargetFramework>net10.0</TargetFramework>` |

### NuGet Publish Failures

| Issue | Solution |
|-------|----------|
| API key expired | Regenerate in NuGet.org, update secret |
| Package already exists | Increment version |
| Rate limited | Wait and retry |

### Pipeline Issues

| Issue | Solution |
|-------|----------|
| Tag not triggering | Verify tag format starts with `v` |
| Permissions denied | Check `GITHUB_TOKEN` permissions |
| OIDC exchange returned no key | The nuget.org Trusted Publishing policy is missing or names a different repository, workflow file or environment. Nothing was published; fix the policy and re-run |

---

## Security Considerations

### Publishing credential: Trusted Publishing, not a stored key

**There is no stored `NUGET_API_KEY`.** `release.yml:1372-1400` mints the publishing credential per run
through a NuGet.org **Trusted Publishing** OIDC exchange:

- The key is issued at the start of the publish job and **expires in about an hour**
- nuget.org validates the token against a policy naming **this repository, the `release.yml` workflow
  file, and the `nuget-production` environment** -- all three, or no key is issued
- **One token buys exactly one key**
- If the exchange fails, the job fails and **nothing is published**. `create-release` produced a DRAFT
  first, so a failed publish leaves an unannounced draft to delete rather than a public release
  pointing at packages that do not exist

**Why this is not a rotation policy.** A stored key is a standing capability: it works from anywhere,
for anyone holding it, until somebody notices. There is nothing here to rotate because there is nothing
here that outlives the run.

**This section previously described a stored key "rotated annually." That was false at HEAD** and is
recorded rather than quietly deleted, because it is a security claim and someone may have relied on it.

### Release Verification

Each release includes:
- Security scan of packages
- Package integrity validation
- Source-link for debugging

### Package Signing

**Policy: repository-signed, not author-signed.** Those are two different signatures and only one of
them is ours to decide.

| Signature | Applied by | On our packages | Why |
|-----------|-----------|-----------------|-----|
| **Repository** | NuGet.org, automatically, on every package it accepts | **Yes** | Nothing for us to configure. This is what `dotnet nuget verify` checks by default |
| **Author** | The publisher, using a code-signing certificate from a public CA | **No** | No certificate has been obtained - see below |

Measured against the published artifacts, not assumed: `excalibur.a3.abstractions.10.0.0-alpha.11.nupkg`
in the local package cache contains a `.signature.p7s` naming *"NuGet.org Repository by Microsoft"*
with the service index `https://api.nuget.org/v3/index.json` - the same signature a first- or
third-party package carries. A package built here and never published carries no `.signature.p7s` at
all, which is what makes the distinction observable rather than inferred.

**Why there is no author signature.** Author signing requires a code-signing certificate issued by a
public CA; NuGet.org rejects self-issued ones, and no certificate has been purchased. This is a cost
decision, deliberately taken, not an oversight.

**How the build behaves.** `ALLOW_UNSIGNED_RELEASE` defaults to `true` in `official-build.yml`, the
signing step is skipped when no certificate is present rather than failing, and the release refuses
only when a build is promotable *and* the explicit opt-out is absent. **That refusal is a control and
must stay** - it is the only thing keeping "the certificate is absent" distinct from "we decided to
ship unsigned", which are identical at the moment of failure and completely different in meaning.

**Transition criterion.** If a public-CA code-signing certificate is obtained, author signing is
adopted and the `|| 'true'` default on `ALLOW_UNSIGNED_RELEASE` is **removed in the same change**, so
that a certificate which later expires, rotates or is renamed refuses the release rather than silently
downgrading it to unsigned.

**Review date.** Reviewed at each release-candidate cut and at each new major line.

---

## Environment Configuration

### GitHub Repository Secrets

| Secret | Purpose | Is it a credential? |
|--------|---------|---------------------|
| `NUGET_USER` | The nuget.org **profile name** for the Trusted Publishing OIDC exchange | **No.** Held as a secret only to keep the account name out of a mirrored file |
| `GITHUB_TOKEN` | Auto-provided, used for releases | Yes, scoped by GitHub to the run |

**No NuGet publishing key is stored.** See [Publishing credential](#publishing-credential-trusted-publishing-not-a-stored-key).

### GitHub Environment

The `nuget-production` environment:
- Required for NuGet publishing
- Has protection rules (optional)
- URL: https://www.nuget.org/profiles/TrigintaFaces

---

## Related Documentation

- [SUPPORT.md](SUPPORT.md) - Support policy, provider tiers, security reporting
- [CONTRIBUTING.md](CONTRIBUTING.md) - Contribution guidelines
- [Directory.Build.props](Directory.Build.props) - Version and package metadata
- [eng/ci/shards/ShippingOnly.slnf](eng/ci/shards/ShippingOnly.slnf) - Shipping package filter
- [Versioning and release stages](https://docs.excalibur-dispatch.dev/docs/migration/version-upgrades) - Supported frameworks, release stages, and what each guarantees. Every shipping package single-targets `net10.0`.


