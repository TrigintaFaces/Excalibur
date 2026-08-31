---
sidebar_position: 2
title: Third-Party Licenses
description: License terms of the third-party packages Excalibur depends on, and the three whose terms are not standard open source.
---

# Third-Party Licenses

Excalibur depends on third-party NuGet packages. The complete per-package list — package id, pinned
version, and license — is generated from the repository's project files and published as
[`THIRD-PARTY-NOTICES.md`](https://github.com/TrigintaFaces/Excalibur/blob/main/THIRD-PARTY-NOTICES.md)
in the repository. That file is the authoritative list and is regenerated against the pinned versions;
this page does not restate it, so that the two can never disagree.

Most entries are ordinary permissive open-source licenses — MIT, Apache-2.0, BSD, and the PostgreSQL
license — and need no action from you. **Three do not**, and each is reached only through a specific
opt-in package. If you do not install that package, its terms do not reach you.

## Dependencies with non-standard terms

| Dependency | Reached only through | Position |
|---|---|---|
| `Oracle.ManagedDataAccess.Core` | `Excalibur.EventSourcing.Oracle`, `Excalibur.Inbox.Oracle`, `Excalibur.Outbox.Oracle`, `Excalibur.Saga.Oracle` | Proprietary. Oracle Free Distribution, Hosting, and Use Terms and Conditions — not an OSI-approved license. See [Oracle](../data-providers/oracle.md). |
| `IBMMQDotnetClient` | `Excalibur.Dispatch.Transport.IbmMq` | Proprietary. No SPDX expression; the package requires license acceptance and ships IBM's terms. See [IBM MQ](../transports/ibm-mq.md). |
| `QuestPDF` | `Excalibur.Compliance.Pdf` | MIT below a revenue threshold; a paid license is required above it. See below. |

### QuestPDF revenue threshold

`QuestPDF` is licensed under the MIT license for organisations with **less than USD 1,000,000 in annual
gross revenue**. Above that threshold, its terms require a paid Professional or Enterprise license.
The current terms are published at [questpdf.com/license](https://www.questpdf.com/license/).

This affects you only if you install `Excalibur.Compliance.Pdf`, the opt-in package that renders
compliance evidence to PDF. Every other compliance feature — audit logging, erasure, crypto-shredding,
the evidence stores — works without it. If your organisation is above the threshold and you do not want
a QuestPDF license, do not install that package and produce your evidence documents through your own
renderer.

## What Excalibur does and does not assert

Excalibur redistributes no third-party vendor software. Referencing one of these packages makes NuGet
install the dependency into **your** application, so the license obligations are yours, and Excalibur
asserts nothing about your entitlement on your behalf.

Read the terms shipped inside the dependency package before you ship, and confirm your deployment is
covered. Where these terms do not suit you, the remedy is to choose a different provider: every other
database provider and pipeline-integrated transport carries an OSI-approved driver license.

This page is a pointer to the vendors' own terms, not a summary you can rely on and not legal advice.
Consult qualified legal counsel in your jurisdiction.

## See Also

- [Legal Notices](./index.md) — framework licensing and compliance disclaimers
- [Compliance Disclaimer](./compliance-disclaimer.md) — what the compliance features do and do not guarantee
