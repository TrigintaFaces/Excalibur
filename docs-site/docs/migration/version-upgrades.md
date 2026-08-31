---
sidebar_position: 4
title: Versioning Strategy
description: Versioning policy, release stages, deprecation rules, and how to stay informed about Excalibur releases.
---

# Versioning Strategy

## Current Status

Excalibur is in **pre-release**, targeting a first stable release of **10.0.0**. APIs may change between pre-release builds. Use pre-release versions for evaluation, early adoption, and feedback.

## Version Scheme

**The package major version matches the targeted .NET major version.** Because Excalibur single-targets `net10.0`, the first stable release is **10.0.0**. When the framework's development line moves to a newer runtime — for example `net11.0` — that line ships as `11.x`.

| Version Component | Meaning | Example |
|-------------------|---------|---------|
| **Major** (`10.x`, `11.x`, …) | The targeted .NET major version | `10.x` targets `net10.0`; `11.x` targets `net11.0` |
| **Minor** (`10.X.0`) | New features, backward compatible within the major | New middleware, new transport provider |
| **Patch** (`10.0.X`) | Bug fixes, backward compatible within the major | Fix null reference, correct calculation |

Minor and patch releases follow [Semantic Versioning 2.0.0](https://semver.org/) **within a major line**: they are always backward compatible. The major number is not an independent API-break counter — it tells you, at a glance, which .NET runtime the package targets, with zero ambiguity about framework compatibility. This mirrors how the .NET platform itself ships (`Microsoft.Extensions.*` and `Microsoft.AspNetCore.*` lock their major to the .NET major), so an Excalibur version reads like a first-party .NET package.

Each Excalibur package single-targets one .NET major. There is no multi-targeting of older runtimes, which lets the framework adopt current-runtime APIs and current third-party dependency majors without being pinned to what also resolves on an older .NET.

## Release Stages

| Stage | NuGet Tag | API Stability | Recommended For |
|-------|-----------|---------------|-----------------|
| **Alpha** | `10.0.0-alpha.N` | APIs may change between releases | Evaluation, early adoption, feedback |
| **Beta** | `10.0.0-beta.N` | APIs are feature-complete but may have minor adjustments | Integration testing, pre-production validation |
| **Release Candidate** | `10.0.0-rc.N` | APIs are frozen; only critical bug fixes | Final validation before production |
| **Stable** | `10.0.0` | Backward-compatible within the major line | Production use |

### What Pre-Release Means for You

- **You can build real applications** -- the framework is functionally complete with an extensive automated test suite
- **APIs may change** -- method signatures, interface shapes, and configuration patterns may evolve
- **No guaranteed upgrade path** between pre-release builds -- consult release notes before upgrading
- **Feedback is welcome** -- your input directly shapes the stable API surface

## Breaking Change Policy

Breaking changes are communicated through multiple channels:

1. **CHANGELOG.md** -- Categorized changes (Added, Changed, Deprecated, Removed, Fixed). Until `10.0.0`
   ships, the pre-releases are documented cumulatively under a single heading rather than one entry per
   alpha; the file says so at the top and explains why.
2. **PublicAPI tracking** -- `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` files in each package track API surface changes
3. **GitHub Releases** -- Tagged releases with detailed notes
4. **Migration guides** -- For significant changes, dedicated migration documentation is provided

### During Pre-Release (Alpha/Beta)

Breaking changes may occur between any pre-release version. Review the CHANGELOG before upgrading for the
cumulative picture, and the tagged release notes on GitHub for what changed in a specific pre-release.

### After Stable Release

- Within a major line (a single .NET major), **minor and patch releases are always backward compatible**
- Breaking API changes are reserved for a new **major line**, which coincides with adopting a new .NET major
- Behavioral changes (same API, different behavior) are treated as breaking

## Deprecation Policy

Once stable, Excalibur follows a minimum deprecation window:

1. **Deprecation notice** -- The API is marked with `[Obsolete("Use X instead.")]` and documented in the CHANGELOG
2. **Minimum one minor version** -- The deprecated API continues to work for at least one minor release cycle within the current major line
3. **Removal** -- The API is removed at the next major line, with a migration guide

During pre-release, deprecated APIs may be removed in any subsequent build.

## What Changed Between Versions

This page states the *policy*. For what actually changed in the release you are moving to:

1. **[Before you upgrade](../whats-new.md#before-you-upgrade)** -- the collected list of changes that
   require you to act, including schema columns to add and APIs that were removed. Start here.
2. **[Migration guides](index.md#upgrading-to-1000)** -- step-by-step guides for the changes that
   rewrite data you have already stored. Both current ones must be completed *before* you deploy the new
   package: [authorization grants require a tenant](authorization-tenant-required.md) and
   [Firestore and Elasticsearch inbox keys change shape](inbox-document-id-rekey.md).
3. **`CHANGELOG.md`** -- the full per-change history, with each breaking entry labelled and carrying its
   migration.

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
- [Before you upgrade](../whats-new.md#before-you-upgrade) -- What changed in this release, and what to do about it
- [From MediatR](from-mediatr.md) -- MediatR migration guide
- [Getting Started](../getting-started/index.md) -- New project setup from scratch
