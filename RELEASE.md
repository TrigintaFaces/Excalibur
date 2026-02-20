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

We follow [Semantic Versioning](https://semver.org/) (SemVer):

| Version | Meaning | Example |
|---------|---------|---------|
| `MAJOR.MINOR.PATCH` | Stable release | `1.0.0` |
| `X.Y.Z-alpha.N` | Alpha pre-release | `1.1.0-alpha.1` |
| `X.Y.Z-beta.N` | Beta pre-release | `1.1.0-beta.2` |
| `X.Y.Z-rc.N` | Release candidate | `1.1.0-rc.1` |

**When to bump:**

- **MAJOR**: Breaking API changes
- **MINOR**: New features (backwards compatible)
- **PATCH**: Bug fixes (backwards compatible)

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

### Version Bump

- [ ] Version updated in `Directory.Build.props`
- [ ] All package versions consistent

```xml
<!-- Directory.Build.props -->
<Version>X.Y.Z</Version>
```

---

## Automated Pipeline

The release pipeline is defined in `.github/workflows/release.yml`.

### Trigger Methods

| Method | Trigger | Use Case |
|--------|---------|----------|
| **Git tag** | Push tag `v*` | Standard releases |
| **Manual dispatch** | GitHub Actions UI | Ad-hoc releases |

### Pipeline Jobs

The automated pipeline runs 8 jobs in sequence:

```
pre-release-validation
        │
        ├── build-packages (Ubuntu)
        └── build-packages (Windows)
                │
        release-quality-gates
                │
        create-release
                │
        publish-nuget
                │
        post-release
```

| Job | Purpose | Duration |
|-----|---------|----------|
| `pre-release-validation` | Version check, build, critical tests | ~5 min |
| `build-packages` (x2) | Multi-OS package building | ~3 min each |
| `release-quality-gates` | Security scan, package analysis | ~2 min |
| `create-release` | GitHub release with auto notes | ~1 min |
| `publish-nuget` | NuGet.org publish with verification | ~10 min |
| `post-release` | Summary, notifications | ~1 min |

### Package Count

Each release produces the current validated shipping set (see `eng/ci/validate-shipping-filter.ps1` report):

- **Dispatch packages**: Core, Abstractions, Transports, Hosting, Serialization, Observability
- **Excalibur packages**: Domain, EventSourcing, Saga, Hosting, Compliance, LeaderElection

All packages multi-target:
- `net8.0` (.NET 8 LTS)
- `net9.0` (.NET 9 STS)
- `net10.0` (.NET 10)

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

# Verify multi-targeting
# Should show lib/net8.0/ and lib/net9.0/ folders
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
| Each package contains | `lib/net8.0/`, `lib/net9.0/` |

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
| Missing TFM | Check `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>` |

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
| NuGet secret missing | Add `NUGET_API_KEY` to repository secrets |

---

## Security Considerations

### NuGet API Key

- Stored as GitHub secret `NUGET_API_KEY`
- Scoped to Excalibur packages only
- Rotated annually

### Release Verification

Each release includes:
- Security scan of packages
- Package integrity validation
- Source-link for debugging

### Signed Packages

Future consideration: Package signing with certificate.

---

## Environment Configuration

### GitHub Repository Secrets

| Secret | Purpose |
|--------|---------|
| `NUGET_API_KEY` | NuGet.org publish API key |
| `GITHUB_TOKEN` | Auto-provided, used for releases |

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

---

*Last Updated: Sprint 341 (W5.T5.1 Define Release Pipeline)*

