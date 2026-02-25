# Documentation Infrastructure Configuration

This document provides the configuration required for deploying the Excalibur documentation site.

## DNS Configuration

### Required CNAME Record

| Type | Name | Value | TTL |
|------|------|-------|-----|
| CNAME | docs | trigintafaces.github.io | 3600 |

**Domain:** `excalibur-dispatch.dev`
**Result:** `docs.excalibur-dispatch.dev` → GitHub Pages

### Verification

After DNS propagation (24-48 hours), verify with:

```bash
dig docs.excalibur-dispatch.dev CNAME
# Expected: docs.excalibur-dispatch.dev. 3600 IN CNAME trigintafaces.github.io.
```

## GitHub Repository Settings

### 1. Enable GitHub Pages

1. Go to repository **Settings** → **Pages**
2. Set **Source**: `GitHub Actions`
3. Set **Custom domain**: `docs.excalibur-dispatch.dev`
4. Check **Enforce HTTPS**

### 2. Configure NuGet API Key Secret

1. Go to repository **Settings** → **Secrets and variables** → **Actions**
2. Create new **Repository secret**:
   - **Name:** `NUGET_API_KEY`
   - **Value:** Your NuGet.org API key (from nuget.org account)

### 3. Create GitHub Pages Environment

1. Go to repository **Settings** → **Environments**
2. Create environment: `github-pages`
3. Add protection rules as needed (optional)

### 4. Create NuGet Production Environment

1. Go to repository **Settings** → **Environments**
2. Create environment: `nuget-production`
3. Add protection rules (recommended):
   - Required reviewers for production releases
   - Deployment branches: `main` only

## Workflow Triggers

### docs-deploy.yml
- **Trigger:** Push to `main` with changes in `docs-site/**`
- **Manual:** Workflow dispatch available

### nuget-publish.yml
- **Trigger:** Push tags matching `v*.*.*`
- **Manual:** Workflow dispatch with version input

## SSL/HTTPS

GitHub Pages automatically provisions and renews SSL certificates via Let's Encrypt. No manual configuration required.

## Maintenance

### Updating Documentation

1. Edit files in `docs-site/docs/`
2. Commit and push to `main`
3. Workflow automatically deploys

### Publishing NuGet Packages

1. Create and push tag: `git tag v1.0.0 && git push origin v1.0.0`
2. Workflow builds, tests, packs, and publishes

### Creating Version Snapshots

At release time:

```bash
cd docs-site
npm run docusaurus docs:version 1.0.0
git add versioned_docs versioned_sidebars versions.json
git commit -m "docs: version 1.0.0"
git push
```

---

*Last Updated: 2026-01-02*
*Sprint 275 - Public Launch Preparation*
