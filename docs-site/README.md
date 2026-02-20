# Docs Site Contributor Guide

This folder contains the Docusaurus site for Excalibur + Dispatch.

## Goals

- Keep docs routes stable and predictable across releases.
- Keep links version-safe (no hardcoded `/docs/next/...` links in content).
- Keep plugin usage minimal and purposeful.

## Route and Versioning Model

- `/docs/*` = latest stable release docs.
- `/docs/next/*` = unreleased docs (current branch).
- `/docs/<version>/*` = historical versions.

Pre-release behavior:
- Before the first stable release, current docs are served at `/docs/*`.
- There is no `/docs/next/*` route until at least one stable version exists.

Post-release behavior:
- After `docs:version` creates `versions.json`, the latest stable version is used as `lastVersion`.
- Stable docs are served at `/docs/*`, and unreleased docs move to `/docs/next/*`.

## Link Policy (Required)

Use one of these patterns:

- Relative links in markdown (`../event-sourcing/index.md`).
- Canonical absolute docs links (`/docs/getting-started`).

Do not use:

- Hardcoded `/docs/next/...` links in docs content or pages.

CI enforces this with:

```bash
npm run validate:version-links
```

If a very rare exception is needed, add `allow-docs-next-link` on that line.

## Announcement Bar

Announcement bar is env-driven in `docusaurus.config.ts`:

- `DOCS_ANNOUNCEMENT_TEXT` (empty by default = hidden)
- `DOCS_ANNOUNCEMENT_ID` (defaults to `docs-site-announcement`)

Example:

```bash
DOCS_ANNOUNCEMENT_TEXT="Release 1.0.0 is live." npm run start
```

## Version Workflow

Run from `docs-site/`:

1. Create a stable docs version at release time:

```bash
npm run docusaurus docs:version 1.0.0
```

2. Commit generated versioned docs + `versions.json`.
3. Keep writing unreleased docs in `docs/` (these are shown as `next` after first stable release).
4. Verify local build and links:

```bash
npm run typecheck
npm run validate:version-links
CI=true DOCUSAURUS_STRICT_LINKS=true npm run build
```

## Plugin Policy

Keep plugin footprint small:

- Keep: local search and LLM export.
- Add only when there is a clear operational need and ownership plan.
- Avoid speculative plugins that increase build complexity.

## Local Development

```bash
npm ci
npm run start
```

## CI Expectations

The `documentation-validation` CI job runs:

1. `npm run typecheck`
2. `npm run validate:version-links`
3. `CI=true DOCUSAURUS_STRICT_LINKS=true npm run build`

Any broken link or hardcoded `/docs/next` content link fails CI.
