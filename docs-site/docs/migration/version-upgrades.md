---
sidebar_position: 4
title: Versioning Strategy
description: Versioning policy, release stages, deprecation rules, and how to stay informed about Excalibur releases.
---

# Versioning Strategy

## Current Status

Excalibur is currently at **3.0.0-alpha** (pre-release). APIs may change between alpha releases. Use alpha versions for evaluation, early adoption, and feedback.

## Semantic Versioning

Once stable, Excalibur follows [Semantic Versioning 2.0.0](https://semver.org/):

| Version Component | Meaning | Example |
|-------------------|---------|---------|
| **Major** (X.0.0) | Breaking API changes | Removing a public method, changing a return type |
| **Minor** (0.X.0) | New features, backward compatible | New middleware, new transport provider |
| **Patch** (0.0.X) | Bug fixes, backward compatible | Fix null reference, correct calculation |

## Release Stages

| Stage | NuGet Tag | API Stability | Recommended For |
|-------|-----------|---------------|-----------------|
| **Alpha** | `3.0.0-alpha.N` | APIs may change between releases | Evaluation, early adoption, feedback |
| **Beta** | `3.0.0-beta.N` | APIs are feature-complete but may have minor adjustments | Integration testing, pre-production validation |
| **Release Candidate** | `3.0.0-rc.N` | APIs are frozen; only critical bug fixes | Final validation before production |
| **Stable** | `3.0.0` | Backward-compatible within major version | Production use |

### What Alpha Means for You

- **You can build real applications** -- the framework is functionally complete with 44,000+ automated tests
- **APIs may change** -- method signatures, interface shapes, and configuration patterns may evolve
- **No guaranteed upgrade path** between alpha releases -- consult release notes before upgrading
- **Feedback is welcome** -- your input directly shapes the stable API surface

## Breaking Change Policy

Breaking changes are communicated through multiple channels:

1. **CHANGELOG.md** -- Updated for every release with categorized changes (Added, Changed, Deprecated, Removed, Fixed)
2. **PublicAPI tracking** -- `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` files in each package track API surface changes
3. **GitHub Releases** -- Tagged releases with detailed notes
4. **Migration guides** -- For significant changes, dedicated migration documentation is provided

### During Pre-Release (Alpha/Beta)

Breaking changes may occur between any pre-release version. Always review the CHANGELOG before upgrading.

### After Stable Release

- Breaking changes only occur in **major version** bumps
- Minor and patch versions are always backward compatible
- Behavioral changes (same API, different behavior) are treated as breaking

## Deprecation Policy

Once stable, Excalibur follows a minimum deprecation window:

1. **Deprecation notice** -- The API is marked with `[Obsolete("Use X instead. Will be removed in vN+1.")]` and documented in the CHANGELOG
2. **Minimum one minor version** -- The deprecated API continues to work for at least one minor release cycle
3. **Removal** -- The API is removed in the next major version, with a migration guide

During pre-release, deprecated APIs may be removed in any subsequent release.

## Upgrade Best Practices

1. **Read the CHANGELOG** -- Check for breaking changes and migration notes before upgrading
2. **Test before upgrading** -- Run your full test suite on the current version
3. **Upgrade in staging first** -- Validate in a non-production environment
4. **Back up persistence stores** -- Event stores, outbox tables, and saga stores before major upgrades
5. **Plan rollback** -- Always have a rollback strategy for production deployments

## Subscribing to Updates

Stay informed about releases and changes:

- **GitHub Releases** -- Watch the [Excalibur repository](https://github.com/TrigintaFaces/Excalibur/releases) for release notifications
- **CHANGELOG** -- Review `CHANGELOG.md` in the repository root for detailed change history
- **NuGet** -- Configure NuGet notifications for `Excalibur.Dispatch` and other packages you depend on

## See Also

- [Migration Overview](index.md) -- All migration guides
- [From MediatR](from-mediatr.md) -- MediatR migration guide
- [Getting Started](../getting-started/index.md) -- New project setup from scratch
