---
title: Capability Migration Map
description: Consolidated map of capability placement changes between Dispatch and Excalibur.
---

# Capability Migration Map

This page captures architecture migration decisions so contributors can see where capabilities now live.

## Migration Summary

| Capability Area | Canonical Placement | Outcome |
|---|---|---|
| Message contracts + dispatch pipeline | `Excalibur.Dispatch`, `Excalibur.Dispatch.Abstractions` | Dispatch remains standalone messaging core |
| Minimal ASP.NET Core bridge | `Excalibur.Dispatch.Hosting.AspNetCore` | Thin bridge only; rich hosting is external |
| CQRS orchestration | `Excalibur.Domain`, `Excalibur.Application` | Excalibur wrapper responsibility |
| Event sourcing / outbox / saga orchestration | `Excalibur.EventSourcing.*`, `Excalibur.Outbox.*`, `Excalibur.Saga.*` | Excalibur-only ownership |
| Compliance providers | `Excalibur.Compliance.*` (+ Dispatch abstractions/hooks) | Provider ownership clarified |
| Postgres package naming | Canonical `Postgres` | Legacy naming removed from guidance |

## Decision Rule

- If a feature is transport/pipeline generic, it belongs in Dispatch.
- If a feature is CQRS/domain-hosting opinionated, it belongs in Excalibur.

## Source of Truth

Governance source:

- [`management/governance/framework-governance.json`](https://github.com/TrigintaFaces/Excalibur/blob/main/management/governance/framework-governance.json)

Related docs:

- [Dispatch vs Excalibur](./dispatch-excalibur-boundary.md)
- [Capability Ownership Matrix](./capability-ownership-matrix.md)
