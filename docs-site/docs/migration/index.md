---
sidebar_position: 23
title: Migration Guides
description: Upgrading an existing Excalibur install to 10.0.0, and step-by-step guides for migrating to Dispatch from MediatR, MassTransit, NServiceBus, and the ASP.NET eventing proposal.
---

# Migration Guides

Two different journeys land on this page. If you are **upgrading an existing Excalibur install**, start
with [Upgrading to 10.0.0](#upgrading-to-1000). If you are **arriving from another .NET messaging
library**, the guides under [Migrating from another library](#migrating-from-another-library) cover the
key differences, mapping tables, and step-by-step instructions.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [Getting Started](../getting-started/index.md) and [Core Concepts](../core-concepts/index.md)

## Upgrading to 10.0.0

**Already using Excalibur and moving to 10.0.0? Start here.** The guides below this section are for
arriving from a *different* library; these two are for upgrading ours.

Read **[Before you upgrade](../whats-new.md#before-you-upgrade)** first — it collects every change that
requires you to act, including the schema columns to add and the APIs that were removed. Three of those
changes rewrite data you have already stored, and all three must be done **before** you deploy the new
package:

- **[Cosmos DB, DynamoDB, Firestore and MongoDB keys carry the tenant](nosql-tenant-key-rekey.md)** --
  The **event stores and saga stores** on those four providers compose the owning tenant into the stored
  key, so documents written by an earlier version are not addressable by this one. **The store refuses to
  serve rather than reading them back as empty**, so an unmigrated deployment fails at its first read
  instead of splitting an aggregate's history in two or re-running a saga from the beginning. This one
  applies **even if you never enabled multi-tenancy** — the key carries a reserved single-tenant or
  untenanted segment either way. Re-key, or start on a fresh collection.
- **[Authorization grants require a tenant](authorization-tenant-required.md)** -- Grants written with no
  tenant on Cosmos DB, DynamoDB, Firestore, or MongoDB were filed under a reserved literal that is part of
  the partition key or document id. Correcting one is a delete-and-reinsert. Each provider stores grants in
  two containers, and the guide names both.
- **[Firestore and Elasticsearch inbox keys change shape](inbox-document-id-rekey.md)** -- The document id
  identifying an inbox entry is composed differently, so entries written by an earlier version are not found
  by this one. Drain the inbox before upgrading, or re-key the existing entries.

Also see **[Migrating to .NET 10](net10-only.md)** if your projects are not yet on `net10.0`, and
**[Version Upgrades](version-upgrades.md)** for the versioning policy and what each release stage promises.

## Migrating from another library

- **[From MediatR](from-mediatr.md)** -- Drop-in compatibility shim (`Excalibur.Dispatch.Compat.MediatR`) for a mechanical namespace-swap migration with Roslyn code-fixes (`EXMIG####`), plus the canonical-API rewrite path for request/response and notification patterns.
- **[From MassTransit](from-masstransit.md)** -- Migrate consumers, sagas, and transport configuration to Dispatch equivalents.
- **[From NServiceBus](from-nservicebus.md)** -- Migrate handlers, sagas, and pipeline behaviors to the Dispatch model.
- **[From ASP.NET Eventing Proposal](from-aspnet-eventing-proposal.md)** -- Migrate from the ASP.NET eventing proposal pattern.

## Framework Migrations

- **[Migrating to .NET 10](net10-only.md)** -- Every shipping package collapsed to `net10.0`. Consumer project TFM, SDK, Docker images, and serverless runtime identifiers must be updated.

## Reference

- **[Version Upgrades](version-upgrades.md)** -- Versioning policy and current registration API reference.
- **[MessageContext Guide](messagecontext-v1.md)** -- Using IMessageContext direct properties for type-safe, high-performance message context access.

## See Also

- [Getting Started](../getting-started/index.md) — New project setup from scratch
- [Core Concepts](../core-concepts/index.md) — Excalibur framework fundamentals
