---
sidebar_position: 23
title: Migration Guides
description: Step-by-step guides for migrating to Dispatch from MediatR, MassTransit, NServiceBus, and the ASP.NET eventing proposal, plus version upgrade instructions.
---

# Migration Guides

Dispatch provides migration paths from popular .NET messaging libraries. Each guide covers the key differences, mapping tables, and step-by-step instructions to help you transition smoothly.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Dispatch.Abstractions
  ```
- Familiarity with [Getting Started](../getting-started/index.md) and [Core Concepts](../core-concepts/index.md)

## Available Migration Guides

- **[From MediatR](from-mediatr.md)** -- Migrate request/response and notification patterns to Dispatch actions and events.
- **[From MassTransit](from-masstransit.md)** -- Migrate consumers, sagas, and transport configuration to Dispatch equivalents.
- **[From NServiceBus](from-nservicebus.md)** -- Migrate handlers, sagas, and pipeline behaviors to the Dispatch model.
- **[From ASP.NET Eventing Proposal](from-aspnet-eventing-proposal.md)** -- Migrate from the ASP.NET eventing proposal pattern.

## Reference

- **[Version Upgrades](version-upgrades.md)** -- Versioning policy and current registration API reference.
- **[MessageContext Guide](messagecontext-v1.md)** -- Using IMessageContext direct properties for type-safe, high-performance message context access.

## See Also

- [Getting Started](../getting-started/index.md) — New project setup from scratch
- [Core Concepts](../core-concepts/index.md) — Excalibur framework fundamentals
