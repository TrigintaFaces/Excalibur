---
sidebar_position: 2
title: Third-Party Licenses
description: License terms of the third-party packages Excalibur depends on, and the three whose terms are not standard open source.
---

# Third-Party Licenses

Excalibur depends on third-party NuGet packages. The complete per-package list — package id, pinned
version, and license — is published as
[`THIRD-PARTY-NOTICES.md`](https://github.com/TrigintaFaces/Excalibur/blob/main/THIRD-PARTY-NOTICES.md)
in the repository. It is generated from the shipping projects' own files and lock files, so it covers
both the packages those projects reference directly and the transitive dependencies that central
package pinning promotes into each published package's dependency list — the same set your restore
resolves. That file is the authoritative list; this page does not restate it, so that the two can
never disagree.

That file also records the native components reached through the Kafka client. `Confluent.Kafka` ships
managed assemblies only; the native library it calls, `librdkafka.redist`, is resolved transitively
into your application, states no SPDX license expression, and publishes its terms — BSD-2-Clause for
librdkafka itself, plus the components built into its binaries — as a single `LICENSES.txt`.

Most entries are ordinary permissive open-source licenses — MIT, Apache-2.0, BSD, and the PostgreSQL
license — and need no action from you. **Three do not**, and each is reached only through a specific
opt-in package. If you do not install that package, its terms do not reach you.

## Dependencies with non-standard terms

| Dependency | Reached only through | Position |
|---|---|---|
| `Oracle.ManagedDataAccess.Core` | `Excalibur.EventSourcing.Oracle`, `Excalibur.Inbox.Oracle`, `Excalibur.Outbox.Oracle`, `Excalibur.Saga.Oracle` | Proprietary. Oracle Free Distribution, Hosting, and Use Terms and Conditions — not an OSI-approved license. See [Oracle](../data-providers/oracle.md). |
| `IBMMQDotnetClient` | `Excalibur.Dispatch.Transport.IbmMq` | Proprietary. No SPDX expression; the package requires license acceptance and ships IBM's terms. See [IBM MQ](../transports/ibm-mq.md). |
| `QuestPDF` | `Excalibur.Compliance.Pdf` | Source-available dual license. **Not MIT**, not OSI-approved; free use requires qualifying for its Community License. See below. |

### QuestPDF licensing — your organisation must qualify, and we cannot qualify for you

`QuestPDF` is **not MIT-licensed.** The license file shipped inside the package is a source-available
dual license — a Community License, or a paid Professional or Enterprise license — and it states that it
is not an OSI-approved open-source license and that the MIT License does not govern use under the
Community terms. **An earlier version of this page described it as MIT below a revenue threshold. That
was wrong, and it understated what the license asks of you.**

**Eligibility for the Community License is assessed per organisation across several categories, and
revenue is only one of them.** Two points decide it for most readers:

- **Public-sector entities, government agencies and publicly traded companies are not eligible,
  regardless of revenue.** Academic institutions are treated under their own category.
- The small-business category requires **annual gross revenue under USD 1,000,000** in the most recently
  completed fiscal year, measured **on a consolidated basis across entities under common control** — not
  per subsidiary, and not per project.

Individuals, qualifying charitable organisations, qualifying academic institutions and eligible
open-source projects each have their own category. The terms are published at
[questpdf.com/license](https://www.questpdf.com/license/), and the license file shipped in the package is
what governs.

**The obligation is yours rather than ours.** Eligibility attaches to the organisation using the
software, so installing `Excalibur.Compliance.Pdf` means your organisation must qualify on its own
account. Nothing we do can satisfy it for you, and this page is not a determination that you qualify.

This affects you only if you install `Excalibur.Compliance.Pdf`, the opt-in package that renders
compliance evidence to PDF. Every other compliance feature — audit logging, erasure, crypto-shredding,
the evidence stores — works without it. If your organisation does not qualify and you do not want a paid
QuestPDF license, do not install that package and produce your evidence documents through your own
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
