# Architecture Diagrams

This directory is the contributor index for architecture visuals used across `docs/` and `docs-site/`.

## Purpose

Use this folder to keep diagram assets discoverable, versioned, and linked to the governing architecture decisions.

## Canonical Sources

Primary architecture definitions live in:

- `management/architecture/`
- `management/specs/`
- ADRs under `docs/adrs/`

Diagram exports in this folder must point back to one of those sources.

## What To Include

- Diagram export file (`.svg`, `.png`, or generated markdown blocks)
- Short caption explaining what system boundary or flow is shown
- Owner/team responsible for keeping it current
- Link to the ADR/spec that defines the behavior

## Change Rules

When changing a diagram:

1. Update the governing ADR/spec in the same PR.
2. Update docs that embed the old diagram.
3. Add a changelog note if the boundary/flow changed materially.
4. Verify broken links are not introduced.

## Recommended Naming

- `dispatch-core-pipeline.svg`
- `dispatch-routing-decision-flow.svg`
- `excalibur-patterns-integration.svg`

Prefer behavior-focused names over sprint-specific names.

## Validation Checklist

- Diagram matches current package/namespace boundaries.
- Links resolve in both GitHub markdown and docs-site builds.
- Terminology matches current package names.

## Related Docs

- `docs/architecture/README.md`
- `docs/architecture/boundary-rules.md`
- `docs-site/docs/architecture/index.md`
