---
sidebar_position: 41
title: Architecture
description: Architectural boundaries, ownership rules, and decision records for Dispatch and Excalibur.
---

# Architecture

This section explains how Dispatch and Excalibur are structured, how boundaries are enforced, and how contributors should reason about ownership.

## What You Will Find Here

- boundary definitions and dependency direction rules
- capability ownership matrix (Dispatch vs Excalibur)
- MessageContext and pipeline design rationale
- architecture decision records and supporting references

## Start Here

1. [Dispatch / Excalibur Boundary](dispatch-excalibur-boundary.md)
2. [Capability Ownership Matrix](capability-ownership-matrix.md)
3. [Capability Migration Map](capability-migration-map.md)
4. [MessageContext Design](messagecontext-design.md)

## Contributor Expectations

When you change architecture-sensitive behavior:

- update the relevant architecture page in this section
- update contributor docs in `docs/architecture/`
- include tests that enforce the new boundary/rule

## Related Sections

- [Core Concepts](../core-concepts/index.md)
- [Performance](../performance/index.md)
- [Dispatch vs Excalibur](../dispatch-vs-excalibur.md)

## How To Use This Section

- New contributors should start with boundary and ownership pages before changing package references.
- Maintainers should update architecture docs in the same PR as boundary-sensitive code changes.
- Reviewers should treat architecture doc drift as a quality issue, not optional polish.

## Change Control

Architecture updates should include:

1. updated architecture page(s)
2. linked tests that enforce changed boundaries
3. migration notes if consumer-facing behavior changes
