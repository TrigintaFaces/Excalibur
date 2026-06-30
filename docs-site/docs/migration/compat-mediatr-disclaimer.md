---
title: Trademark and Non-Affiliation Notice (MediatR Compatibility)
description: Trademark attribution, non-affiliation, purpose-limitation, and warranty notice for the Excalibur.Dispatch.Compat.MediatR compatibility package and its migration guidance.
sidebar_position: 90
---

# Trademark and Non-Affiliation Notice

> **DRAFT — PENDING ATTORNEY REVIEW.** This notice is a draft prepared to assist legal
> review. It has **not** been reviewed or cleared by an attorney and must **not** be
> treated as legal advice or as a final, approved legal notice. Do not publish or rely on
> it until qualified legal counsel has reviewed and approved it. See the
> [Reviewer checklist](#reviewer-checklist-for-counsel) below.

This notice applies to the `Excalibur.Dispatch.Compat.MediatR` package (the
"compatibility package") and to the MediatR migration documentation it accompanies.

## TRADEMARK ATTRIBUTION

"MediatR" and any associated names, logos, and marks are trademarks or registered
trademarks of the MediatR project and its respective owner(s). [VERIFY] The exact
trademark owner, registration status, and the correct trademark/registered-trademark
symbols should be confirmed by counsel. [LEGAL REVIEW NEEDED]

Excalibur.Dispatch does not own and claims no rights in the "MediatR" name or any related
mark. Any reference to "MediatR" in the compatibility package, its source, its
identifiers, or its documentation is made solely on a **nominative and descriptive basis**
— to accurately identify the third-party library with which the compatibility surface is
intended to be source-compatible, and to help users migrate away from it. Such references
are not intended to suggest any commercial relationship, origin, or association.
[JURISDICTION-SPECIFIC] The availability and scope of nominative or descriptive fair use of
a trademark vary by jurisdiction and should be confirmed by counsel.

## NO AFFILIATION OR ENDORSEMENT

Excalibur.Dispatch and the `Excalibur.Dispatch.Compat.MediatR` compatibility package are
independent works. They are **not affiliated with, sponsored by, endorsed by, or otherwise
associated with** the MediatR project or its owner(s). The MediatR project and its
owner(s) have not reviewed, approved, certified, or endorsed Excalibur.Dispatch, the
compatibility package, or this documentation. No statement in this documentation should be
read as a representation by, or on behalf of, the MediatR project or its owner(s).

## PURPOSE LIMITATION

The compatibility package exists **solely to assist interoperability and migration**. It
provides an independently developed, API-compatible surface (for example, request,
notification, handler, sender, and publisher abstractions) so that existing application
code written against the third-party library can compile and run against Excalibur.Dispatch
with minimal changes.

The compatibility package is **not** a copy, fork, or redistribution of the MediatR
project's source code. It is an independent implementation that mirrors a public API shape
for interoperability purposes only. [VERIFY] Counsel should confirm that the compatibility
surface as shipped contains only independently authored code and does not incorporate or
redistribute third-party source, and should confirm the licensing posture of any API
elements reproduced for compatibility. [LEGAL REVIEW NEEDED]

## NO LEGAL OR LICENSING ADVICE

This documentation does **not** constitute legal advice and **no legal advice** is provided
through your use of the compatibility package or this documentation. Migrating between
software libraries can carry licensing and other legal obligations that depend on your
specific circumstances.

**You remain solely responsible** for your own license compliance with respect to any
third-party software you migrate from or to, including the third-party library identified
in this documentation. Before relying on the compatibility package, **you must conduct your
own independent review, testing, and validation** — including a license-compliance
assessment — and should consult qualified legal counsel regarding your specific obligations.
Excalibur.Dispatch does not guarantee that any particular migration path satisfies your
licensing or other legal requirements, and does not guarantee any particular outcome.

## NO WARRANTY

THE COMPATIBILITY PACKAGE AND THIS DOCUMENTATION ARE PROVIDED **"AS IS"**, WITHOUT WARRANTY
OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, TITLE, AND NON-INFRINGEMENT, to the
fullest extent permitted by applicable law. The full warranty terms are governed by the
software's open-source license. [VERIFY] Confirm the wording matches the package's actual
license (for example, an MIT-style license) and that the "AS IS" language is consistent
with that license text.

## LIMITATION OF LIABILITY

To the fullest extent permitted by applicable law, the authors and copyright holders of
Excalibur.Dispatch assume no liability and shall not be liable for any claim, damages, or
other liability — whether in an action of contract, tort, or otherwise — arising from, out
of, or in connection with the compatibility package, this documentation, or their use. The
limitation and exclusion of liability are governed by the software's open-source license.
[JURISDICTION-SPECIFIC] Some jurisdictions (including under EU and Member State consumer
protection law) restrict the exclusion or limitation of certain liabilities; counsel should
confirm the appropriate limiting language for each target market and add an EU-equivalent
formulation (for example, "to the extent permitted under applicable law") where required.

---

## Reviewer checklist (for counsel)

This draft requires attorney review before publication. At minimum, counsel should confirm:

- [ ] Exact trademark owner name and legal entity for the "MediatR" mark, and the correct
      mark symbols (™ / ®) and placement.
- [ ] Registration status of the mark in each relevant jurisdiction.
- [ ] That the nominative/descriptive fair-use characterization is appropriate and adequate
      in each target jurisdiction (US and EU at minimum).
- [ ] That the package as shipped contains only independently authored code and does not
      redistribute or incorporate third-party source.
- [ ] The accuracy of the "AS IS" warranty disclaimer against the package's actual
      open-source license text.
- [ ] Jurisdiction-specific limitation-of-liability wording (US "to the fullest extent
      permitted by law" and an EU/Member State consumer-protection-aware equivalent).
- [ ] Whether a separate, more prominent trademark line is warranted in the package README,
      NuGet package description, and any UI/CLI surface.
- [ ] That all `[VERIFY]`, `[LEGAL REVIEW NEEDED]`, and `[JURISDICTION-SPECIFIC]` markers
      are resolved and removed prior to publication.

> AI-assistance disclosure: This draft was generated with AI assistance and requires
> attorney review before use.
