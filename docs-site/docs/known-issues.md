---
sidebar_position: 2.5
title: Known Issues
description: Identified defects and unverified areas in the Excalibur 10.0.0 pre-release.
---

# Known issues

These are the defects we have identified and classified as affecting this pre-release. **This list is not exhaustive, and we know it is not** — our classification does not yet cover our whole backlog, and unclassified items include ones we rate as high severity. Treat it as the set we have identified, not the set that exists, and weight your own validation accordingly for anything you depend on.

We would rather say that plainly than let the list's completeness be assumed.

The list is in two parts, because two different things get called a "known issue" and conflating them is unhelpful:

- **[Defects](#defects)** — behaviour that is wrong. Something will go badly for you if you rely on it.
- **[Unverified areas](#unverified-areas)** — behaviour we did not prove. This is **absence of evidence, not evidence of failure**; we are telling you where our testing does not reach so you can decide what to validate yourself.

**What earns an entry here is whether a defect can reach you, not how severe it is.** A defect is listed
when a version you could have installed contains it. A serious defect that exists only on our main branch
is not listed — you cannot install our branch, so telling you about it would crowd out the things you can
act on; those belong in release notes. **An absence here therefore means one of two things: we have not
identified it, or it cannot reach you. It does not mean we judged it minor.**

Every entry is re-checked against the code before each update. **This page lists only what is still wrong.** Entries we have confirmed fixed move to [Resolved issues](resolved-issues.md) rather than being quietly deleted, so you can still tell a fixed issue from a forgotten one — and so that what remains here is the list you have to act on.

---

## Defects

**At a glance.** The defects below are grouped by the area they affect. Each group links to its entries; nothing is summarised away, and the full write-up for every issue is still on this page.

| Area | Issues | What tends to go wrong |
|---|---|---|
| [Authorization and multi-tenancy](#authorization-and-multi-tenancy) | 1 | A startup guard that cannot fire, so a mis-ordered pipeline is accepted. |
| [Secrets and data protection](#secrets-and-data-protection) | 2 | Credentials that persist where you did not expect, and an attribute that protects nothing. |
| [Compliance: GDPR and SOC 2](#compliance-gdpr-and-soc-2) | 4 | Reports and certificates that claim more than the framework checked — and one erasure that irreversibly destroys another tenant's data. |
| [Event sourcing, outbox and projections](#event-sourcing-outbox-and-projections) | 5 | Writes reported as succeeding when they did not, or vice versa. |
| [Transports](#transports) | 4 | Messages reported as sent, or acknowledged, when they were neither. |
| [Dependencies and packaging](#dependencies-and-packaging) | 3 | What a published package pulls in that you did not ask for. |
| [Test fixtures](#test-fixtures) | 1 | Shipped testing helpers that do not work as documented. |


**Several of the defects below are the same behaviour in unrelated subsystems: the framework reports
success without having done the thing.** A compliance control reports satisfied without having assessed
anything; an evidence package is returned, complete and hashed, without anything having been collected; a
failed migration reports success; a send succeeds with the message discarded; a send succeeds with your
message properties stripped. They were found separately and share no code — **which is what makes the shape
worth naming once here rather than repeating it in each entry.**

**The generalisation is more useful to you than any single instance: where this framework returns success,
do not treat the return alone as proof the effect happened.** For anything you depend on, assert on the
effect rather than the call — count what arrived, read back what you wrote, check the control you asked to
be applied. That advice would be paranoid about a mature product; it is the correct posture toward this
one today.

**One entry runs the other way**, and it is worth knowing before you read: a SOC 2 control the framework
never assessed is reported as a *failure against you* rather than as a gap in our coverage. The remedy
there is the mirror image — you are checking whether a red is real, not whether a green is.

### Authorization and multi-tenancy

#### The startup check that is supposed to refuse a mis-ordered tenant pipeline never runs

:::warning This is a guarantee that does not fire, not a behaviour that is wrong
If your pipeline is correctly ordered, nothing here affects you. If it is mis-ordered, you were
promised a startup failure and you will not get one — you get silent, tenant-less execution instead.
:::

**What happens.** The framework documents that a pipeline in which a tenant-*reading* middleware would
run before the last tenant-*establishing* middleware is refused at composition, with an error naming
both types. The check identifies each by asking whether the pipeline entry implements the corresponding
marker interface.

By the time middleware reach that check they are frequently not the middleware themselves. A middleware
registered as `Scoped` is represented by a per-dispatch stand-in that holds no instance at all; using
`UseAt<T>(stage)` wraps it; scoping it to message kinds wraps it again. None of those wrappers carries
the marker interfaces, so the check concludes that nothing establishes tenant context and returns
without examining the ordering.

The framework's own tenant-establishing middleware is registered `Scoped`, so in any application built
on the generic host this check has never been able to fire.

**Are you affected?** You are affected if you rely on that refusal to catch a mis-ordered pipeline. You
are **not** harmed by this on its own: a correctly ordered pipeline behaves correctly. What you have
lost is the guard, not the behaviour.

**What that exposes.** If your pipeline *is* mis-ordered — your own tenant-reading middleware placed at
an earlier stage than the middleware that establishes tenancy — that middleware runs outside the ambient
tenant scope and observes **no tenant**. It raises no error and writes no log. A tenant-scoped read
inside it returns the untenanted result rather than failing, so the symptom is wrong data rather than an
outage, and nothing marks which requests were affected.

**What to do now.** Check the ordering yourself rather than relying on the refusal. Any middleware of
yours that reads tenant context must declare a later `DispatchMiddlewareStage` than the middleware that
establishes it, or be registered after it within the same stage. If you have a middleware that reads
tenant identity and you have never seen a startup error, that is not evidence the ordering is right.

**Which versions are affected.** Every published 10.x version. We have not assessed the older 3.x line —
do not read that as clean.

**Is there a fixed version?** **`10.0.0-alpha.13`.** The corrected check reads the capability from the
type each pipeline entry represents rather than from the entry itself, which sees through all three
wrappers. The rule now lives in its own seam (`Excalibur.Dispatch/Delivery/Pipeline/TenantOrderingRule.cs`)
and is exercised through a real container rather than a constructed pipeline. That distinction is the
whole defect: the five older arms pass undecorated test doubles straight to the pipeline constructor, so
they stay green against the broken code and **cannot fail for this defect at all**. Only the
real-container arms detect it.

One residual, stated rather than left for you to find: the rule still returns silently when nothing in
the pipeline establishes tenant context. It refuses a mis-ordering; it does not require that a tenant be
established in the first place.

Not yet confirmed against the published package.

---

### Secrets and data protection

#### The caller's `Authorization` and `Cookie` headers are stored in your SQL Server outbox, and can reach your broker and your traces

:::danger Treat credentials that reached any of these places as exposed
If you dispatch HTTP requests through the ASP.NET Core integration, the caller's request headers —
including bearer tokens, cookies and API keys — are copied onto every message dispatched for that request.
Where they end up depends on which parts of the framework you use; for SQL Server outbox users, **no
opt-in is needed**.
:::

**What happens.** The ASP.NET Core integration copies **every** request header into the message context,
with no filtering. From there they reach up to three places:

| where the headers end up | when | you opted in? |
|---|---|---|
| **Your SQL Server outbox table** — stored in the message's headers | you use the SQL Server outbox store | **No — this is the default behaviour of that store** |
| **Your message broker** — each header becomes a CloudEvents extension attribute, for example one ending in `headerauthorization` | you enabled CloudEvents on the transport with `UseCloudEvents(...)` or an `AddCloudEventsFor<Transport>(...)` method | Yes |
| **Your tracing backend** — each header becomes a span attribute named `context.item.<header>` | you set `IncludeCustomItemsInTraces = true` in the observability options; up to the first ten context items are recorded | Yes |

**Are you affected?** You are affected if you dispatch messages from HTTP requests through the framework's
ASP.NET Core integration (the controller helpers or the endpoint route handlers) **and** any row of the
table applies to you. Our other outbox stores do not copy the headers, and a message published without
CloudEvents does not carry them to the broker. The tracing path's built-in filters remove items whose
names contain *password*, *secret*, *token* or *credential*, but not `Authorization` or `Cookie`.

We have not established whether this is limited to the message dispatched for the request itself or also
reaches messages your handlers publish while handling it, so **assume it covers every message dispatched
or published while handling the request.**

**What that exposes.** Anyone who can read the outbox table or its backups, consume or inspect the
affected topics and queues (including dead-letter queues and broker logs), or read your traces can read
the credentials — for as long as each of those keeps them.

**Which versions are affected.** Every published 10.x version. The unfiltered header copy has existed since
February 2026, and the SQL Server outbox already stored the context on each message before then, so SQL
Server outbox users have been affected since the header copy arrived. The broker path first appeared on
RabbitMQ and later extended to further transports; we have not dated the tracing path. We have not
assessed the older 3.x line — do not read that as clean. As with the other entries on this page, we are
not publishing a list of versions: we cannot produce a complete one, and a partial list would tell some
affected readers they are safe.

**What to do now.** None of this needs a new release:

1. **Stop the exposure at its source:** remove sensitive entries (`Authorization`, `Cookie`, API-key
   headers and similar) from the message context before you dispatch. That closes all three paths at once.
   Short of that, turn CloudEvents off on transports that carry HTTP-originated messages, and leave
   `IncludeCustomItemsInTraces` off — but neither of those protects the SQL Server outbox.
2. **Treat anything that may already be stored as exposed.** Review and purge the headers of rows in your
   SQL Server outbox table **and its backups**, the retention of affected topics, queues and dead-letter
   queues, and any traces recorded with custom items enabled. Rotate long-lived credentials that could be
   among them — API keys and persistent session cookies in particular. Short-lived bearer tokens expire on
   their own, but may still be readable wherever they were stored.

**Is it fixed?** **Not in any released version.** The repair copies a request header into the message
context only when you have named it, and never copies `Authorization`, `Proxy-Authorization`, `Cookie` or
`Set-Cookie` even if named — the same allow-list model as ASP.NET Core header propagation. Because it filters
at the source, it closes all three paths. **That is a behaviour change for you:** if your handlers read
request headers from the message context, they will only see the headers you allow-list. When a release
carries the fix, this entry will name it.

#### The `[Sensitive]` attribute does not encrypt anything — its own documentation says it does

`[Sensitive]`'s summary, which ships inside every package's XML documentation file and so appears in your
IDE as you type it, reads: *"Marks a property as containing sensitive business data requiring
encryption."* Its remarks name trade secrets, API keys and credentials as the data it is for.

**The attribute drives masking. Nothing encrypts on the strength of it.** It is read in exactly two
places — the masking contract and the regex masker — and by no encryption or crypto-shredding path.
So a property you annotate is redacted when it passes through a masker, and **stored in cleartext**.

**What this costs you.** You annotate an API key or a credential, your IDE tells you it requires
encryption and implies the framework provides it, and the value is written to your database in the clear.
The failure is silent: masked output in your logs looks exactly like the protection working, because
masking *is* working — it is the other half that never existed.

**What to do.** Treat `[Sensitive]` as a masking and classification marker only. For encryption at rest,
use `[PersonalData]` on a record that also carries `[DataSubjectId]`, with crypto-shredding registered —
that combination is the one the framework actually encrypts — or `[EncryptedField]`, subject to the entry
below. Audit any property you annotated `[Sensitive]` expecting encryption, and check the stored value
directly rather than the log output.

**If you have already told an assessor that `[Sensitive]` fields are encrypted at rest, check what those
properties also carry before you act.** `[Sensitive]` has never caused encryption in any version we have
published. But encryption selects on `[PersonalData]` and never consults `[Sensitive]`, so a property
carrying *both* — on a record with `[DataSubjectId]`, with crypto-shredding registered — is encrypted by
that path. Your statement is wrong if `[Sensitive]` was the only annotation on those properties, and right
if that combination was also present. Establish which applies to your deployment, and take it to your
assessor with that determination rather than correcting a statement that may be accurate. Our SOC 2
checklist supplied the wording for the assertion, so if you assembled your evidence package from it you
may be carrying the claim without having written it yourself.

**Is it fixed? The wording is. What the attribute does is unchanged, and that is why this entry stays
here.** The attribute's summary, the checklist claim and the attestation wording are corrected in the
release this page accompanies, so your IDE no longer tells you `[Sensitive]` requires encryption the
framework will provide. **`[Sensitive]` still drives masking and still encrypts nothing**, on this release
as on every earlier one.

**Two inert properties are removed in this release, and that is a breaking change to act on.** A
key-purpose selector and a flag claiming annotated values are withheld from exception details shipped in
**every earlier version** and did nothing there: they defaulted to protective-sounding values, and no code
read either of them. If your code sets `EncryptionKeyPurpose` or `ExcludeFromErrors`, it will **not
compile** against this release. Neither property ever did anything, so deleting the assignment is the
whole of the change — there is no replacement to adopt and no behaviour to preserve. `MaskInLogs`, the
neighbouring property on both attributes, is **not** affected and **is** honoured.

**None of that changes your
data or your paperwork.** Values you already stored under `[Sensitive]` are still in cleartext and nothing
migrates them; an attestation you have already given an assessor stays given until you withdraw it. Both
are yours to close — which, together with the attribute's unchanged behaviour, is why this entry stays
here rather than moving to [Resolved issues](resolved-issues.md).

### Compliance: GDPR and SOC 2

#### One tenant's erasure destroys another tenant's data when they share a data-subject id, and the victim's read reports it as lawfully erased

:::danger This destroys data irreversibly, and nothing reports it
Two tenants holding the same data-subject id share one encryption key. Either tenant's erasure destroys
it for both. The other tenant's personal data was never subject to an erasure request, is now permanently
unrecoverable, and reads back as `null` — which this framework defines as the assertion that the data
*was* lawfully erased. Nothing throws, no certificate records a problem, and no log marks which tenant
was affected.
:::

**What happens.** A per-subject encryption key is named by a value derived from the `[DataSubjectId]`
value alone. **The tenant is not part of that name.** A data-subject identifier comes from your own
entity — a customer number, an employee identifier, an e-mail address — and those values repeat across
tenants in ordinary multi-tenant use. Where two tenants hold the same value, both tenants' personal data
is encrypted under one key.

Erasing that subject for either tenant destroys the key. The other tenant's fields then cannot be
decrypted, and the read path returns `null` for each of them. In this framework a `null` from a
crypto-shredded field asserts that the data was lawfully erased, so the second tenant's data is reported
as erased when no erasure was ever requested for it, over data that is now unrecoverable.

**It takes no race and no fault to reach.** Two ordinary writes under two tenants, then one ordinary
erasure. Nothing is concurrent, nothing times out, and nothing throws.

**There is no second control that catches it.** You might reasonably expect the authenticated data bound
into each encrypted field to separate the two tenants, so that a cross-tenant read would at least fail
loudly. On this path it does not: that binding carries no tenant either, so the read produces the erasure
tombstone rather than an authentication failure. Neither the key's name nor the authenticated data
distinguishes the two tenants.

**Are you affected? Three checks, in this order.**

| Check | What it means |
|---|---|
| **1. Did you call `AddCryptoShredding()`, `AddCryptoShreddingWithoutErasure()` or `AddEventSourcingCryptoShredding()`?** | If none of them, this does not reach you. |
| **2. Is your `[DataSubjectId]` value unique across your whole deployment, or only within a tenant?** | Unique deployment-wide: not affected by this mechanism. Unique only within a tenant, and two tenants have ever held the same value: **you are exposed.** |
| **3. Has any erasure completed?** | Each completed erasure destroyed the key for **every** tenant sharing that subject id. That data is already unrecoverable, and you cannot tell from the framework which tenants those were. |

A single-tenant deployment is not exposed by this mechanism — there is no second tenant to collide with.

**How to tell whether it has already happened to you.** This is answerable from your own data, and the
framework cannot answer it for you — no record it keeps distinguishes the two tenants:

1. In each store holding `[PersonalData]`, find every `[DataSubjectId]` value that appears under more than
   one tenant. That is your collision set. **If it is empty, no erasure of yours can have crossed a tenant
   boundary.**
2. For each value in that set, look for a **completed** erasure filed by any one of those tenants.
3. Where you find one, **every other tenant holding that value has lost that data.** Its `[PersonalData]`
   fields now read `null` and no erasure was ever requested for it.

Step 3 identifies the affected tenants exactly. It does not recover anything, and the reads will not start
failing to tell you — they will keep reporting a lawful erasure.

**What that exposes.** For every tenant that shared an erased subject id: personal data that no one asked
to erase is permanently unreadable, and your application, your erasure certificates and your audit trail
all report it as lawfully erased. Nothing can be reconciled afterwards — the key is gone, and no record
distinguishes the tenant who requested the erasure from the tenant who merely shared the identifier.

**What to do now.** Make the `[DataSubjectId]` value globally unique by embedding your tenant identifier
in it — for example `"{tenantId}:{customerId}"` in place of `"{customerId}"`. The key's name is derived
from that value exactly as you supply it, so a value that already carries the tenant gives each tenant
its own key and the collision cannot arise. Per-tenant erasure keeps working unchanged, because the
erasure path derives its target from the same string.

**Apply it at both paths, consistently** — the write path that encrypts the fields, and the
erasure-request path that sets `DataSubjectId` on an erasure request. A value that differs between the two
yields two different keys, so the erasure would destroy nothing while reporting that it completed.

**Three things this mitigation does not do.** All three matter before you rely on it:

- **It protects only data written after you change the value.** Anything already written stays under the
  old shared key.
- **It repairs nothing already written.** Nothing re-keys existing data onto the new identifier for you.
  To move it, read each record, decrypt it under the old identifier, then re-encrypt and store it under
  the new one — in every store holding those fields, and before any erasure runs.
- **Where a collision has already been erased, there is no recovery.** The key is destroyed and that data
  is gone. This mitigation prevents the next occurrence; it cannot undo one.

**And a cost that is not a footnote.** Changing your subject-id values makes your own existing legal
holds, registered data locations and retention rows unmatchable: they were recorded against the old value.
**Re-place every legal hold and re-register every data location under the new identifier before you rely
on either.** A legal hold that no longer matches the identifier does not block the erasure it was placed
to block.

**Which versions are affected.** `Excalibur.Compliance` **`10.0.0-alpha.4` through `10.0.0-alpha.13`** —
ten versions. The whole of the older **3.x line is not affected by this mechanism**: it ends at
`3.0.0-alpha.216`, published before per-subject crypto-shredding existed. We measured the two endpoint
versions by reading the shipped assembly inside each package; the eight between them are inferred from the
derivation being present at both ends with no fix in between. **What that establishes is that the affected
key derivation ships in those packages — not a behavioural assessment of any individual build.**

**Is there a fixed version?** **No — not in any published version.** The fix exists in our source and has
not been released, so there is nothing you can upgrade to today, and the mitigation above is the whole of
the remedy available to you now. This entry will name the release that carries the fix when one ships.

**What the fix will require of you, so it is not a surprise.** The corrected key name includes the tenant,
which means **every key name changes**. Personal data encrypted before that upgrade stays readable — each
stored field records the key that protects it, and that record is unchanged — but an erasure performed
*after* the upgrade targets the new name and will **not** destroy the key holding data written before it.
So treat the upgrade as a cutover: either re-encrypt existing personal data under the new identity, or
complete any outstanding erasures before upgrading. The release notes will carry the migration step.

---

#### An erasure certificate covers only the locations you registered, and reports `Completed` without mentioning the ones you did not

:::note Two related defects on the same document are fixed; this one is not
A `Completed` the framework could not substantiate, and a signature that did not cover what the
certificate said, are both fixed in the release this page accompanies — see [Resolved
issues](resolved-issues.md). This entry is about **what the certificate was ever looking at**, which is
unchanged: all three are independent, and fixing the other two leaves this one in place.
:::

**What you see.** A signed erasure certificate reporting `Completed` for a data subject whose personal
data is still readable — in a table, collection, index or blob your application writes to, or in an
encrypted field. The certificate does not name that location, does not report a gap, and does not
distinguish itself in any way from one that covered everything. The operator cannot tell, the data
subject cannot tell, and neither can an auditor reading it afterwards.

**Why it happens.** Erasure acts on the set of data locations the framework *discovered*, and discovery
is **registration-based**: a location exists because your application called
`IDataInventoryService.RegisterDataLocationAsync`. The framework never derives a location from an
annotation, and this is stated in the documentation shipped inside the package you installed —
*"Inventory does NOT synthesize erase-able locations from `[PersonalData]` attributes (the attribute
carries no storage location)."* **Neither `[PersonalData]` nor `[EncryptedField]` registers a location.**

The coverage gate then asks three questions, and asks all three **only of the locations it has**: was the
key protecting this one deleted, does a registered contributor cover its store kind, does a declared
exemption apply. A location that was never registered is not covered, not exempt, and **not counted as a
gap** — it is simply absent from the question.

**`[PersonalData]` contributes a category name, and a category is not a location.** The gate compares the
annotated category names against the categories of the locations it does have, matching by name. **One
registered location in a category therefore satisfies the gate for every annotated property in that
category, wherever that data actually lives.** Register `Customers.Email` under a `Contact` category, and
a second `Contact` store you never registered raises nothing.

**Encrypted fields are the case most likely to surprise you.** `[EncryptedField]` selects its key by
*purpose* — `EncryptionOptions.DefaultPurpose`, which is `"default"` unless you change it — together with
the tenant. **There is no data-subject dimension in that selection.** Erasing a subject destroys the keys
belonging to that subject; it cannot destroy a key shared by every subject, because doing so would render
every other subject's data unreadable. So an encrypted field survives the erasure **readable**, under a
key that is still live. And because `[EncryptedField]` carries no category and registers no location, it
contributes nothing to either arm of the gate: the field is neither erased nor reported.

**Are you affected? The discriminator is whether every location holding personal data was registered.**

| your application | affected? |
|---|---|
| holds personal data anywhere it did not register via `RegisterDataLocationAsync` | **yes** |
| relies on `[PersonalData]` or `[EncryptedField]` to make data erasable | **yes — an annotation is not a registration** |
| uses `[EncryptedField]` for personal data under a shared purpose key | **yes, and the data stays readable** |
| registered every location, for every category, in every store | not by this mechanism |

**What a certificate covers, stated plainly.** Read every certificate this framework issues as carrying
this scope, whether or not it prints it:

> This certificate covers the data locations registered with the framework's data inventory when the
> erasure ran. Personal data held anywhere else, including stores your application writes directly, is not
> covered by it, and its absence from this certificate is not evidence that it was erased.

**What you must do. Register every location that holds personal data, explicitly, before you rely on an
erasure certificate.** That is the only mechanism the framework has, and it is available to you today:

```csharp
using Excalibur.Compliance;
using Microsoft.Extensions.DependencyInjection;

// Registration: AddDataInventoryService() registers IDataInventoryService (scoped).
services.AddDataInventoryService();

// One call per (table, field) pair that holds personal data — including every
// [EncryptedField] property, which the framework does not register for you.
await inventory.RegisterDataLocationAsync(
    new DataLocationRegistration
    {
        TableName = "Customers",
        FieldName = "EmailAddress",
        DataCategory = "Contact",
        DataSubjectIdColumn = "CustomerId",
        IdType = DataSubjectIdType.UserId,
        KeyIdColumn = "EncryptionKeyId",
        TenantId = tenantId,
    },
    cancellationToken);
```

`TableName`, `FieldName`, `DataCategory`, `DataSubjectIdColumn`, `IdType` and `KeyIdColumn` are all
required; `TenantId`, `TenantIdColumn` and `Description` are optional. `IdType` must be the identifier
type your own column actually holds — `UserId`, `Email`, `ExternalId`, `NationalId`, `Hash` or `Custom`.

**Two things registration does not do for you**, and both matter before you treat a certificate as
evidence:

- **It does not reconcile categories.** Registering one location in a category silences the annotated-gap
  arm for that whole category, so keep your own list of which locations exist per category rather than
  reading the certificate as that list.
- **It does not make a shared-purpose encrypted field erasable.** A location counts as covered only when
  its key was deleted, a registered contributor covers its store kind, or a declared exemption applies —
  and a key shared across subjects is never deleted by erasing one of them. Registering such a field makes
  the shortfall *visible* rather than closing it; to complete an erasure you still need a contributor that
  removes the row, or a key scoped to the subject.

**Which versions are affected.** Registration-based discovery is the design of this subsystem rather than
a regression, so we have no evidence of a version without it. **Confirmed in the newest published version,
`10.0.0-alpha.11`**, by reading that package's shipped documentation surface: the inventory's own
documentation states it does not synthesise locations from annotations, and `RegisterDataLocationAsync` is
the only member in the package that registers one (control: a different registration member is present in
the same surface, so the absence is a real reading and not a failed lookup). The same design is present in
our source at two points spanning the publishing window, **9 August 2026 and 14 September 2026**, with no
change between them. **The remaining seven published `10.0.0-alpha.*` versions we have not examined, and
that means unmeasured, not clean.**

**Is it fixed?** **Not in any released version.** The remedy is to make selecting a field for encryption
and registering it as a data location the same act, so that the framework declares what it has made
opaque rather than leaving that to you. When a release carries it, this entry will name that release.
**Until then, registering every location yourself is the whole of the remedy**, and no configuration
setting substitutes for it.

#### A GDPR erasure reported as completed leaves the personal data in every read model

:::danger An erasure certificate does not mean the data is gone
Erasure removes the payload from the event store. It does **not** reach projections, so any read model
built from those events keeps the personal data indefinitely — and the certificate you produce for a
regulator does not say so.
:::

**What happens.** Erasure operates on the event store: it forgets the event payload there. Projections
are separate documents or rows, written by the apply path as events arrive, and nothing in the erasure
path revisits them. So after a successful erasure the personal data remains in every projection derived
from the erased events, in whatever store holds them, for as long as that store keeps it.

**Are you affected?** You are affected if you both (a) build projections or read models from events, and
(b) rely on the framework's erasure to satisfy a subject's deletion request. You are not affected if you
do not use projections, or if you already erase your read models yourself by some other means.

**What that exposes.** A deletion request you have reported as fulfilled is not fulfilled. The data
remains queryable through exactly the surfaces your application reads from — usually the ones serving
user-facing features — and it will be rebuilt into any projection replayed from a source that still
holds it. Treat any erasure performed through the framework as covering the event store only.

**What to do now.** Erase the corresponding projection documents or rows yourself as part of your
erasure workflow, and do not treat an erasure certificate as evidence that read models were cleared. If
you have already issued certificates on this basis, they overstate what was done.

:::warning A retraction published here was wrong about your version. It is withdrawn.

**Read this if you saw the earlier text.** A revision of this page briefly told you that
`IProjectionRecovery.ReapplyAsync` does not clear the subject's row and reports success anyway, and
that there was no version-level workaround. **That statement was about our unreleased source, not
about any version you can install, and it should not have been published unscoped.**

**For every published 10.x version, `ReapplyAsync` clears the row as originally described.** The
conditional write that produced the defect does not exist in the published line: the type that
introduces it has never been present on the branch our packages are built from, and the recovery path
there writes unconditionally. If you held back an erasure remediation on the strength of the retracted
text, you can proceed.

The defect was real in work that has not shipped, and it is fixed there: recovery now either writes the
re-folded state at the position the row already holds, or fails loudly. It never reports a successful
recovery having written nothing.

**This does not change the gap described above.** Erasure still does not reach projections
automatically. `ReapplyAsync` could not produce a row for an aggregate whose every event is a
tombstone in any published version; **`10.0.0-alpha.13` corrects that half** — it now skips tombstones
structurally and writes the empty state for a fully erased aggregate. The projection gap itself remains
open and is described in the next paragraph.
:::

**What still works, and what does not.** A full projection rebuild re-folds rows from the tombstoned
stream and does clear the subject's contributions from rows shared with other subjects. It does **not**
produce a row for an aggregate whose every event is now a tombstone, so it cannot clear that subject's
own row. `ReapplyAsync` is the call intended for that row: given the aggregate id it re-folds the
tombstoned stream and writes the result, which for a fully erased aggregate is the empty state.

Both are calls **you** must make. Neither is triggered by the erasure itself, so an erasure you have
reported as completed has not touched any read model unless you invoked one of them. Treat clearing
projections as your own step in the erasure workflow. A later version narrows that step to the projections
the erasure names — see below.

**What changes in `10.0.0-alpha.13`.** An erasure on a host with registered projections reports
**partial** rather than `Completed`, and **the certificate you receive says so in a form built to be
read by an auditor**. Nothing about the erasure itself changes — the same rows are tombstoned and the
same read models keep the data. What changes is that the certificate stops implying otherwise.

Concretely, the certificate gains a list of what the erasure did **not** reach, separate from the list
of data lawfully retained and never mixed with it. Each entry names the kind of store still holding the
data, describes in one sentence why the erasure did not reach it, states explicitly that **no lawful
basis is claimed** for that retention, and tells you that discharging it remains your obligation and
that doing so **takes the affected read model offline for the duration**. That last point matters
operationally: the remedy must not be run against a live projection processor, because a rebuild
interrupted part-way leaves the subject cleared from some rows and not others with no record of which.

A certificate with nothing outstanding is unchanged, byte for byte, so certificates you have already
been issued continue to verify.

If you have built your own projection erasure, register an `IErasureContributor` declaring
`DataStoreKind.Projection` and the framework stands down.

**What changes after `10.0.0-alpha.13` — and this is in no released version yet.** Erasure now clears,
at the moment of erasure and with no call from you, every projection row whose id is the erased aggregate's
id. That is the row this entry says a rebuild cannot produce and `ReapplyAsync` had to be invoked for. You no
longer invoke anything for it.

**What it still does not clear, and this is now named rather than silent:** a projection keyed on anything
other than the aggregate id — a per-tenant total, a leaderboard, any row several subjects contributed to. The
framework cannot identify which of those rows held the erased subject, because that is a fact about the
events that produced the row and not about the row itself. So it **names those projections in the erasure
result and carries them into the signed certificate**, the outcome is `PartiallyCompleted` rather than
`Completed`, and your manual step narrows to the projections it named instead of covering all of them.

One boundary worth knowing: that naming comes from the framework's own gap reporting, which stands down if
you register your own contributor covering projections. If you do, naming the residue becomes yours.

**Which versions are affected.** Every published 10.x version. This is a capability that was never
built rather than one that broke, so there is no earlier unaffected version: the projection apply path
and the erasure path have never been connected. We have not assessed the older 3.x line — do not read
that as clean.

**Is there a fixed version?** **Partly, in `10.0.0-alpha.13`.** That release stops the erasure
reporting `Completed` when registered projections still hold the subject: it reports **partial** and the
certificate carries the unreached-data list described above. **The gap itself is not fixed and is not
scheduled** — erasure still does not reach read models, and clearing them remains your step. What
changed is that the certificate no longer implies otherwise.

Not yet confirmed against the published package: we name the release that carries the fix, and we
confirm it by reading the shipped assembly once it is published. Verify against the package you
actually restore.

---

#### GDPR data-portability and subject-access requests report success having done nothing

:::danger These produce evidence a regulator may read
The built-in Article 20 export returns `Completed` — with a data size — having read, serialised and
written nothing. The built-in Article 15 request can be marked `Fulfilled` at the instant it is
created. Neither is detectable from outside: a completed export with a plausible size looks exactly
like a real one.
:::

**What happens.** Two GDPR services ship as the default behind public registration calls.

`AddDataPortability()` registers an Article 20 exporter whose `ExportAsync` returns
`Status = ExportStatus.Completed` unconditionally. Nothing is read from any store, nothing is
serialised, and nothing is written — the result type carries no payload, path or URI, so there is
nowhere an export could be placed even in principle. The `DataSize` field, documented as the byte count
of the exported data, is filled with the number of discovered data locations multiplied by 1024.

`AddSubjectAccessRequests()` registers an Article 15 service with an `AutoFulfill` option. With it set,
a request is created with `Status = Fulfilled` and `FulfilledAt = ` the current time, in the same
operation that creates it — before anything could have been gathered or sent.

**Are you affected?** You are affected if you call either registration and treat its result as evidence
that a data-subject request was satisfied. You are not affected if you registered your own
implementation of either interface: both use `TryAdd`, so yours takes precedence and the built-in one
is never constructed.

**What that exposes.** A subject-access or portability response recorded as fulfilled when no data was
produced. The consequence is not a runtime fault — it is a compliance record that overstates what was
done, and the audience for it is a regulator or an auditor reviewing your controls.

**What to do now.** Register your own `IDataPortabilityService` and `ISubjectAccessService` before
calling the `Add*` methods, and treat any completion these services previously recorded as unverified.
Only you can decide what to export, in what shape, and where it goes.

**Which versions are affected.** Every published 10.x version. We have not assessed the older 3.x line
— do not read that as clean.

**Is there a fixed version?** **`10.0.0-alpha.13`**, and the fix is a deliberate breaking change rather
than a silent repair: the built-in exporter now **refuses** with an
error naming the remedy instead of reporting a completed export, and the `AutoFulfill` option is
**removed** so a request is always created `Pending` and can only become `Fulfilled` through the
explicit `FulfillRequestAsync` call that records an act you actually performed. If you relied on
`AutoFulfill`, that reliance was on a value that never reflected any work.

---

### Event sourcing, outbox and projections

#### The outbox failure report is required to check an owner it is never given

**Who this affects.** Anyone whose messages take the outbox failure path — which is every outbox user,
since a failed delivery is not an unusual event — and, more sharply, anyone who wrote their own
`IOutboxStore`. **This entry does not name a store that is safe from it.** The gap is in the shape of the
published interface, so no choice of store is a documented way around it, and we are not going to imply
one exists.

**What you see.** Nothing at the time. Later, a message that a current dispatcher is still delivering
under its own claim is reported failed by a dispatcher that lost that claim — typically a slow attempt
from before a leader handover or a lease expiry, arriving after the claim moved on. Depending on how your
store handles the report, the message can be withheld for a backoff it did not earn, have its attempt
count advanced by an attempt the current owner did not make, or be released from a claim that is still in
use — the same message then being delivered twice. There is no error on either side.

**What it costs you, stated precisely.** A delivery failure reported by an expired claim releases the
reservation held by a live successor **of the same process**, so the duplicate-delivery window ceases to be
bounded by your reservation timeout and collapses to your failure floor. On the PostgreSQL store's
defaults those are 300 seconds and 30 seconds respectively — the message becomes re-claimable roughly 270
seconds early, **inside the successor's live lease**.

**This does not create the first duplicate.** The outbox is at-least-once by contract, and a successor
claiming an expired lease can already produce one. **What this removes is the bound on how many follow.**

**Consumer obligation you have not been told before now.** The release also clears the reservation
timeout, so **your failure-backoff floor becomes the entire remaining protection** against a message being
picked up again while an earlier attempt is still in flight. Size it accordingly; the reservation timeout
is not doing the work you may assume it is.

**What we told you to do, and what we gave you to do it with.** The documentation shipped in the package
states the guard as a requirement of the failure report:

> *"**Only the claim's owner may report against it.** A report from a dispatcher that no longer holds the
> claim is a no-op rather than an error: it is stale, not invalid. Honouring it would release a claim its
> successor is still delivering under, and both would then send the same message."*

That is the correct rule. **The member it is written on cannot evaluate it.** In the packages we can
open, `MarkFailedAsync` takes:

```csharp
MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
```

**`messageId` says which message. Nothing says which claim, or who is reporting.** The claim's owner is
the one fact the guard turns on, and it is not among the inputs. The member also sits on `IOutboxStore`
rather than on `IFencedOutboxStore` — which, in the same published documentation, carries exactly two
members, the claim and the success mark:

```
IFencedOutboxStore.GetUnsentMessagesAsync    the claim
IFencedOutboxStore.MarkSentAsync             the success mark
IOutboxStore.MarkFailedAsync                 the failure report -- no fencing token, no claim identity
```

So the fence covers taking work and reporting success, and stops at the boundary of reporting failure.

**Before you change anything you built: the ownership check has two halves, and they are not equally
affected.** The reporter's *process* identity does not have to arrive as a parameter to be available — a
store that belongs to one dispatcher process knows its own identity and can compare it against the one
recorded on the row. **If you have built a check on that basis, it can be doing real work, and you should
not remove it on the strength of the signature alone.** Whether any particular store performs that
comparison is an implementation choice we are still establishing across our own providers, and we are not
characterising yours.

**What no implementation can do through this member is tell two claim cycles of the same process apart.**
A process identity is the same value on your second claim as on your first, so a delayed report from an
earlier cycle of a still-running dispatcher is indistinguishable from a current one. That is the half the
wording promises and the call cannot carry.

**What you must do.** If you implemented `IOutboxStore` yourself, do not read the guard as satisfied
because you wrote a store that follows the documentation — **the documentation asked you for a check its
own signature does not support.** Open your `MarkFailedAsync` and answer one question: **when a report
arrives, what do you compare against the row's current claim?** If the answer is "nothing" — because
nothing identifying the claim reaches the method — then a stale report is being honoured, and the
paragraph above tells you what that costs. Enforcing it needs the claim to arrive with the report; that
means carrying it in state you control alongside the call, because the published signature will not carry
it for you.

**What a store does about this varies, and we are still establishing what each of ours does.** Nothing stops an implementation comparing the row's recorded owner against an identity it holds outside the call, and the stores in this framework do not all handle it the same way. **We are not going to describe the per-store behaviour until we have measured every one**, and we are deliberately not naming a safe store in the meantime. What is settled is the surface: the call gives you no way to say which claim you hold and no way to learn what the store decided.

**Which versions are affected — check the property against your own package**, rather than a list of
versions we happen to hold. Open the documentation for `MarkFailedAsync` in the package you installed. **If
it contains the paragraph beginning "Only the claim's owner may report against it", your version states
the guard**, and the signature above shows it cannot be evaluated from the call. We found the paragraph in
every 10.x package we could open from `10.0.0-alpha.9` onward, and not in the three earlier ones — but we
cannot open the releases we do not hold, and we are deliberately not stating that as a first-affected
version. The signature has carried no claim identity in any 10.x package we opened, including the ones
without the paragraph.

**What the remedy is.** A separate, claim-scoped contract: a store opts into it, and its failure report
carries the claim identity as an argument, so the guard the documentation states becomes evaluable from the
call rather than from state the store happens to hold. We looked for it in every package we hold up to
`10.0.0-alpha.10` — `10.0.0-alpha.5` through `alpha.10`, the `10.0.0-alpha.10.14` build, and the `3.0.0`
line — and it is in **none** of them (control: the existing outbox contract is present in all of them, so
the absence is a real reading and not a failed lookup).

**`10.0.0-alpha.11` does carry it**, which is why the answer below is not a simple "no": the claim-scoped
contract is declared in that package's shipped documentation surface, measured the same way as the
absences above.

**The 3.x line is not affected**, and this is a measurement rather than a bound: the same method exists
there with the same four parameters, and its shipped documentation states no owner guard at all. It never
asked anyone for a check it could not support.

**Is it fixed?** **Substantially, in the pre-release this page accompanies — and the remaining gap is
narrower and differently shaped, so read which case you are in.**

The claim-carrying members now exist and are implemented: a failure report can be made against the claim
it was handed under, and under a leader election it can also be made against the tenure that holds the
lease. **PostgreSQL, SQL Server and Oracle all carry both**, each verified by a test that executes the
statement against a real engine of that kind rather than against a stand-in. The drain prefers those
members whenever the store offers them.

**What is no longer true:** the sentence above saying this entry *"does not name a store that is safe from
it."* It does now. On those three stores a report from a lost claim is refused by the statement performing
the write, and a report from a superseded tenure is refused by the fence.

**What is still true, and is the residual:** the base `IOutboxStore.MarkFailedAsync` is unchanged and still
takes no claim, because changing it would break every store anyone has written. **A custom store that
implements only that interface still has the gap in full** — nothing about this release repairs a store
that does not offer the claim-scoped member, and the documentation shipped inside earlier packages still
cannot be recalled. If you wrote your own store, implementing `IClaimScopedOutboxStore` is what closes it
for you.

**And one new refusal you should know about:** a host that configures a leader election on a store that
cannot fence the completion path now **fails at startup** rather than draining unfenced. The stores that
cannot express an atomic high-water mark — the cloud-native and search-backed ones — therefore require an
explicit `AsSingleWriter()` opt-out under a leader election, which is the same opt-out they have always
required for the mark-sent transition.

#### The batch failure report performs no ownership check, and its documentation does not say so

**Who this affects.** Anyone who reports outbox failures in batches — through
`IOutboxStoreBatch.MarkBatchFailedAsync`, or through the `MarkBatchFailedAsync` extension method on
`IOutboxStore`. **This entry does not name a safe alternative, and the single-message member is not one.**
It has its own entry above, for a different reason.

**What you see.** Nothing at the time. A batch failure reported after your claim on those messages has
already been superseded is accepted, and the reservation a live successor holds is released — so the
messages become claimable again while an earlier attempt may still be in flight.

**What we told you about it.** This is the entire documentation shipped for that member:

```
Marks a batch of messages as failed.
```

That is the whole of it. **It says nothing about ownership, and there is nothing in it to warn you** —
which is why this is worth an entry of its own rather than a footnote to the single-message case.

**Why it cannot check.** Look at what you pass it: a list of message ids, an error, a retry count, a
cancellation token. **Nothing identifies which claim you hold**, so the store has nothing to compare
against the claim recorded on the row. This is not a check the implementation skips — **it is a check the
call cannot express**, and no store, ours or yours, can perform it through this member.

**How to confirm it applies to you.** You do not need to run anything. **Open your code and look for a
call to `MarkBatchFailedAsync`.** If you have one, that call performs no ownership check, on any store.
If you want to see it for yourself, look at the method signature in the package you installed: if it takes
no argument identifying your claim, there is nothing for the store to verify.

**What you can do about it now.** Treat a batch failure report as unconditional. If your dispatcher may
lose its claim mid-batch — a lease expiry, a leader handover, a paused process resuming — then either
report those failures individually so you can reason about each one, or accept that a message may return
to the claimable set while an earlier delivery is still running and make your handlers idempotent, which
is the correct posture toward this outbox in any case.

**Which versions are affected — check the signature in your own package**, rather than a list of versions
we happen to hold. **If `MarkBatchFailedAsync` takes no argument identifying your claim, your version has
this.** We found it in every 10.x package we could open, including the earliest we hold, so we have no
evidence of a 10.x release without it.

**Is it fixed?** **Partly, and in a released version — but not in the member you are calling.**
`10.0.0-alpha.11` ships an additional `MarkBatchFailedAsync` overload that takes a claim identity and
refuses a report made under a claim the store no longer recognises. **The unscoped member is unchanged and
still performs no check**, so switching packages does not fix your code; you have to call the overload that
takes the claim. If you keep calling the four-argument member, the behaviour above is exactly what you have.

#### A subscriber reading the global event stream can permanently skip a committed event

:::danger A skipped event is never redelivered
The subscriber advances past the missing position and no later read from that checkpoint revisits it.
There is no error, no retry, and nothing downstream learns that the event was missed.
:::

**What happens.** Global stream positions are allocated by the database's own identity or sequence
mechanism, which hands out the number **before** the transaction commits. Two appends can therefore
commit out of position order: position 6 can become visible while position 5 is still in flight.

A subscriber reads the stream with an ordered range query and advances a high-water mark. If its read
observes 6 while 5 is uncommitted, it returns 6, the subscriber advances past 5, and 5 — which commits a
moment later — is never read from that checkpoint again.

**Are you affected?** You are affected if you consume the global event stream (projections driven by the
global stream, subscriptions, or any reader that tracks a position and advances it) **and** more than one
writer can append concurrently. A strictly single-writer deployment is not exposed. The window is small
but it is not rare under concurrent load, and it widens with transaction duration.

**What that exposes.** Silent, permanent event loss from the point of view of every downstream consumer.
A projection built by a subscriber that skipped an event is missing that event's effect forever, and a
rebuild from the same stream produces a different — correct — answer, so the two disagree with no
indication of which is right.

**What to do now.** If you can, serialise appends so only one transaction writes at a time; that removes
the interleaving entirely, at a cost in throughput. Otherwise treat a projection built from the global
stream as reconstructible and rebuild it periodically rather than trusting its incremental state. A
rebuild reads committed data and is not exposed to the in-flight window.

**Which versions are affected.** Every published 10.x version. We have not assessed the older 3.x line —
do not read that as clean.

**Is there a fixed version?** **`10.0.0-alpha.13`, and it needs a migration from you — a package
upgrade alone is not enough.** Two things changed together. Positions are now allocated from a
singleton counter row **inside the appending transaction** rather than from `IDENTITY`/`SERIAL`, which
hands out the number before commit; that makes the committed positions a contiguous prefix. And a read
of the global stream now returns only the contiguous run from your position, stopping at the first gap,
because the allocation change alone does not close this — a scan spans states, so a subscriber could
still pass a slot while it was uncommitted.

**This introduces a failure mode you need to recognise before you upgrade.** A permanently absent
position now **stalls** a subscriber instead of being skipped past. That is the correct direction — a
stall is loud, a skipped event is silent and unrecoverable — but it is a behaviour change, and legacy
archival deleted rows, which is exactly what leaves a permanent hole. Archival now tombstones a row
instead of deleting it, so a database provisioned by the shipped create script cannot acquire one; a
database whose archival predates that shape has no in-place upgrade and should be re-provisioned. See
[Global-stream reads stop at gaps](./migration/global-stream-reads-stop-at-gaps.md).

**Two limits you must read before treating this as closed.** The schema change was made by rewriting
`001_CreateEventStoreSchema.sql` in place, and **there is no `ALTER` migration** — nothing converts a
deployed `IDENTITY` column into the counter row, so **a package-only upgrade leaves you with the
defect**. The supported path is to recreate the store, or to apply `001` and then `012` by hand. And the
contiguous-read decorator is wired for **SQL Server only**, deliberately: the skip it prevents needs a
non-atomic scan, which is a property of SQL Server's locking `READ COMMITTED`. The other four providers
rest on the gapless-allocation invariant alone.

Not yet confirmed against the published package.

---

#### Rebuilding a projection appears to succeed and changes nothing your application reads

**What happens.** The rebuild writes its result to a single row keyed by the projection's **type name**.
Every other path that maintains a projection — the live apply path used as events arrive, and recovery —
writes **one row per aggregate**, keyed by the aggregate's identifier. Those are different keys, so the
rebuild populates a row no reader looks at and leaves every row a reader *does* look at untouched.

**Are you affected?** You are affected if you call the rebuild service to repair or re-derive a
projection. The live apply path is keyed correctly and is not affected.

**What that exposes.** A rebuild is the remedy you reach for when a projection is known to be wrong: a
bad deployment, a corrected handler, a missed event. Running it reports success, takes as long as a real
rebuild, and leaves the incorrect data in place. Anyone reading the projection afterwards sees the same
wrong answer and has been told it was repaired.

**What to do now.** **On versions before `10.0.0-alpha.13`:** do not rely on the rebuild service to
correct a projection — clear the projection's rows yourself and replay the events through the normal apply
path. **On `10.0.0-alpha.13`:** use the rebuild service, which writes under the key a reader queries.
**Do not hand-replay through the apply path on that version** — see [A projection could report a
position it did not hold](resolved-issues.md#a-projection-could-report-a-position-it-did-not-hold-so-a-read-model-silently-double-counted-or-omitted-events)
on the resolved-issues page, where the apply path is one of the ways a false position was produced.

**Which versions are affected.** Every published version before `10.0.0-alpha.13`. The rebuild has
written its result this way since February 2026, before the first 10.x release. We have not assessed the older 3.x line — do not
read that as clean.

**Is there a fixed version?** **`10.0.0-alpha.13`.** A rebuild or recovery now folds the handlers a
projection declares through `WhenHandledBy`, and a rebuild writes each document under the key a reader
actually queries rather than one document under the projection type name.

**A rebuild now refuses rather than reporting `Completed`** when the global-stream read stops at a gap,
naming the position it stopped at. Two different causes, and the remedy differs: if that position belongs
to an append still in flight, **re-run the rebuild** and it clears on its own; if it is permanently absent
— a store archived by a version that deleted event rows — **apply the archival gap backfill for your
provider first** (see [Global-stream reads stop at gaps](./migration/global-stream-reads-stop-at-gaps.md)).
A refusal has changed nothing, so nothing is lost by finding out this way.

The gap refusal is confirmed against the released source for `10.0.0-alpha.13`, which carries the wording
quoted above. An earlier revision of this entry pointed at a migration numbered `012`; no such migration
exists and the reference has been corrected to the backfill guide. We have not inspected the compiled
assemblies of any published package.

---


#### Every projection write fails on MongoDB

**What happens.** The MongoDB projection store stamps each written document with an "updated at" value
taken as a `DateTimeOffset`. The MongoDB driver has no mapping from `DateTimeOffset` to a BSON value and
raises an error rather than converting it, so the write throws before the document is stored. The field
is written on every upsert, so **every** projection write through this store fails.

**Are you affected?** You are affected if you use the MongoDB projection store. Our other projection
stores are not affected. The failure is loud rather than silent — you will see the error — so if you are
using this store successfully today, you are not on the affected path.

**What that exposes.** No projection state is written on MongoDB. Nothing is silently corrupted; the
capability does not work.

**What to do now.** On `10.0.0-alpha.13` and later this is fixed. On earlier versions, use another
projection store or write the projection documents through your own MongoDB code.

**Which versions are affected.** Every published 10.x version. The value has been written as a
`DateTimeOffset` since February 2026, before the first 10.x release. We have not assessed the older 3.x
line — do not read that as clean.

**Is there a fixed version?** **`10.0.0-alpha.13`.** The store now converts the timestamp before
handing it to the driver — it writes `DateTimeOffset.UtcNow.UtcDateTime`, because BSON has no
offset-carrying date type and the driver throws rather than guessing
(`Excalibur.Data.MongoDB/Projections/MongoDbProjectionStore.cs`).

The arm that covers it is `MongoDbPositionedProjectionConformanceTests`, which is **integration-only and
container-gated**: it does not run without a MongoDB container, so a green unit build is not evidence
about this path. The original failure was loud, so you will know either way. Not yet confirmed against
the published package.

---

### Transports

#### Rejecting a message with `requeue: true` does not arrange redelivery — it waits for your consumer to stop

**What you see.** Nothing. The call returns normally, a line appears in your log, and your code carries
on believing the message has been put back. There is no exception, no return value, and no status to
inspect — `RejectAsync` returns a plain `Task`, so **there is no channel through which it could tell you
anything**, even in principle.

**What actually happens.** Rejecting with `requeue: true` declines to commit the offset and discards the
transport's own bookkeeping for that message. **It does not seek, does not notify the broker, and does
not schedule anything.** The message becomes available again only when this consumer stops polling long
enough for its group session to expire and the partition to be reassigned. Declining to commit is a
legitimate Kafka pattern — the defect is that the contract presents a **conditional, deferred** outcome
as though it were an arranged one.

**The case that bites.** A long-running consumer that requeues a message and keeps polling **may never
see that message again for the life of the session**, with nothing in the return value, the message, or
your own state to indicate it. If you requeue on a transient failure and expect a retry shortly, that
retry is not coming while the consumer stays healthy.

**Can you force redelivery yourself? No — and we checked rather than assumed.** The shipped transport
surface exposes no seek operation, no access to the underlying consumer, and no redelivery affordance of
any kind; `Requeue` exists only as an enum value on the reject call. **The only lever you have is to
stop the consumer** — end the process, or let the session lapse — so the partition is reassigned and the
uncommitted message is delivered to whoever picks it up.

**What you must do.** Treat `requeue: true` as *"I decline to commit this"* rather than *"redeliver
this."* Concretely, and available today: if you need a bounded, observable retry, do it in your own
handler — retry in-process, or publish the message to a retry topic you control — rather than relying on
requeue to bring it back. If you use requeue as a backstop for a message you genuinely cannot process,
that is sound, provided nothing downstream is waiting for the redelivery to happen promptly.

**How to confirm it on the version you hold.** Reject a message with `requeue: true` in a consumer you
then leave running, and keep polling the same partition. Watch for the message to reappear. It will not,
until you stop that consumer. If you then restart and it arrives, you have this behaviour.

**Which versions are affected — every published version.** The reject path has this shape in all of
them. **No released version behaves differently, and we are not naming a target release.** When a release
carries a repair we stand behind, this entry will name that version and say what changes for a caller
of the reject path.

#### The Azure Event Hubs transport can report a send as successful while dropping the message

**What you see.** Nothing. That is the defect. When you publish through the Azure Event Hubs transport,
messages are accumulated into a batch before being sent. If a message does not fit in the batch, it is
discarded and the send continues and reports success. **No exception is raised, no warning is logged, and
the returned result does not indicate that anything was lost.** The messages that did fit arrive normally,
so the topic looks healthy and your application has no signal to act on.

You are most likely to meet it under the conditions that make a batch fill: large payloads, many messages
published in one operation, or a message whose size grows after enrichment.

**On `10.0.0-alpha.11` and later, both send paths refuse.** **This paragraph previously said the
refusing path was CloudEvents-only and had been added after the most recent release, so no released
version could escape the defect. That was wrong.** Both the standard and the CloudEvents send path carry
the size check and throw at `10.0.0-alpha.11`, `10.0.0-alpha.12` and on main; batching happens in one
place, and there is no third path that discards. On versions **before** `10.0.0-alpha.11` the original
statement holds: no configuration avoids it, and CloudEvents does not help.

**What you must do.** Until you are on a version carrying the fix, do not treat a successful return from
this transport as proof of delivery. If you need that assurance now, either publish messages individually
rather than in a batch, or reconcile what you sent against what your consumers received. The outbox
pattern gives you the same assurance structurally, because an unacknowledged message stays in the outbox.

**How to confirm it on the version you hold.** Do not check this by reading our source — that tells you
about the current main branch, not the package you restored. Publish a batch whose combined payload is
comfortably larger than your namespace's maximum event size, then count the events that arrive on the
hub. If fewer arrive than you sent and the call reported success, your version carries this defect.

**Is it fixed? Yes — in `10.0.0-alpha.11` and later.** Both send paths refuse an over-large message with
an exception instead of discarding it. **This entry previously said the repair was "not yet in any
released version" — that was wrong, and it stayed wrong through two releases.** If you are on
`10.0.0-alpha.11` or later you already have the fix; on anything earlier, upgrading resolves it and the
workarounds above are no longer your only option. Not yet confirmed by reading the published assembly —
verify against the package you actually restore.

**Which versions are affected — every version before `10.0.0-alpha.11`.** This has been present since
before the transport reached its current package name. **This section previously said every published
version contains it; that is no longer true and was already untrue when `10.0.0-alpha.11` shipped.** **We are deliberately not naming a
first-affected version**: the code has passed through more than one mass-rename commit, and a
path-scoped history search reports the rename rather than the original change — twice while investigating
this, that error made the defect look months younger than it is. Rather than publish a date we would have
to correct upward, we are naming no first-affected version. The *first fixed* version we can name, and
have measured against the repository at both tags, is `10.0.0-alpha.11`.

#### A Kafka rebalance commits past messages still running in your handlers, so they are never redelivered

:::note A separate Kafka message-loss defect is listed directly below
This entry covers messages lost across a **rebalance**. A handler that **throws** loses its message by a
different route, with a different trigger — see the next entry. They are independent: if you are exposed
to one you should assume you are exposed to the other, and fixing either leaves the other in place.
:::

**What you see.** Nothing at the time, and nothing afterwards. A routine rebalance happens — you deploy,
you scale out, a consumer restarts, or a slow batch exceeds the poll interval — and afterwards some
messages have simply never been handled. There is no exception, no warning, and no gap in the consumer
group's committed offsets to look at, because the offsets are exactly what tell you everything was
consumed. The messages are not in a dead-letter topic either. They were delivered once, to a handler that
had not finished, and the group moved past them.

**Why it happens.** When partitions are revoked, the transport commits the consumer's *stored* offsets.
The underlying client stores an offset the moment it hands a message to the application — not when your
handler completes — because `enable.auto.offset.store` is left at its default of `true`. So the stored
position is one past *every message ever delivered*, including the ones still running. Committing it on
revoke moves the group's committed offset beyond unfinished work, and the next owner of the partition
resumes after it. No crash is required; only that a handler had not returned yet.

The transport does track a settled position and commits that explicitly on its normal path. The revoke
path does not go through that tracking, so the safeguard is bypassed at the one edge it does not own.

**What you must do — and there is one question to answer before you do it, because this workaround is
not safe for every deployment.**

**Precondition: do you read your dead-letter topic through the bundled dead-letter consumer?** If your
code calls `Consume` on that consumer anywhere, the answer is yes.

**If you do NOT use it**, apply the workaround. Turn off the client's automatic offset store, which the
transport passes straight through to the underlying client:

```csharp
services.Configure<KafkaOptions>(options =>
{
    options.AdditionalConfig["enable.auto.offset.store"] = "false";
});
```

**If you DO use it, this setting will break it, and you must deal with that first.** The dead-letter
consumer commits by asking the client for its *stored* offsets — the mechanism you have just switched
off — and unlike the rebalance path it does not tolerate there being none. Its `Consume` call throws as
soon as it has messages to return, **and the messages it already read are lost with the exception**,
from a topic that is your last copy. Do one of these **before** applying the setting:

- read the dead-letter topic with your own consumer for as long as the workaround is in place; or
- wrap the `Consume` call and treat that specific failure as *"nothing was committed, these messages
  will be delivered again"* — which is true and safe, because the offsets did not move.

**If you can do neither, do not apply the workaround.** There is no safe form of it for your
deployment, and you are choosing between two loss paths rather than removing one. In that case your
remaining options are the ones that do not touch offset storage at all: keep handler durations well
inside the poll interval so rebalances are rarer, and reconcile what you consumed against what you
produced rather than trusting the committed offset.

With automatic storing off, nothing is recorded ahead of your handlers, so the commit on revoke cannot
carry the group past unfinished work. The explicit commits the transport makes at settled positions are
unaffected and continue to advance the group normally.

**The trade is duplicates instead of loss.** Messages that were in flight during a rebalance are
delivered again to the partition's new owner, so **your handlers must be idempotent.** That is already
the obligation this transport documents, and it is the correct side of the trade to be on: a duplicate is
recoverable and a lost message is not.

**Also leave `EnableAutoCommit` off**, which is its default. Turning it on commits those same stored
offsets on a timer, which reintroduces the loss on its own without any rebalance being involved.

**How to confirm it on the version you hold.** Do not check this by reading our source; that describes
the main branch rather than the package you restored. Give a handler an artificial delay of a minute,
publish a batch of messages, and once they are dispatched start a second consumer in the same group to
force a rebalance. Then read the group's committed offset with `kafka-consumer-groups --describe`. If the
committed offset is already past the messages your handler has not finished, your version carries this,
and those messages will not be fetched again by any member of the group.

**Is it fixed?** **No released version behaves differently.** A correct fix must at least do two things: the
revoke path must commit the tracked settled position explicitly rather than the stored one,
and automatic offset storing must be turned off in the transport's own configuration, because while it is
on any argument-less commit anywhere in the process defeats the guarantee. **Those two are
necessary and we are not claiming they are sufficient** — review of a candidate fix with both
parts in place found further ways the group can still move past unfinished work, so treat this
entry as open until a release names itself as carrying the complete repair. **Until a release carries
that, the workaround above is what you have, and it is effective today.** When a release carries the fix,
this entry will name that version.

**Which versions are affected.** Every published `10.0.0-alpha.*` version — `alpha.4` through `alpha.11`
— carries it. The commit-on-revoke path has behaved this way since it was introduced, which predates the
oldest of those releases, so there is no 10.x version where it is absent. We have **not** separately
verified the superseded `3.0.0-alpha.*` line and are not telling you it is safe; nothing we are aware of
changed this behaviour there.

#### A Kafka handler that throws loses its message: it is neither committed nor redelivered

**This is a second, independent Kafka defect.** The entry above is about a *rebalance*. This one needs no
rebalance at all — only a handler that throws.

**What you see.** A handler throws an unhandled exception. The framework logs it, and the consumer moves
on to the next message. **The message that failed is never delivered to your handler again.** It is not
retried, it does not reach a dead-letter topic, and the consumer group's committed offsets give no sign
that anything is missing.

**Why it happens.** When a handler throws, the Kafka subscriber catches the exception and records it,
and does nothing else: it neither commits the offset nor seeks back to it. With the offset left
uncommitted and no rewind, the consumer's position has already advanced past the message, so it is not
fetched again — and the next offset that *is* committed covers it. The message is gone without ever
having been handled successfully.

**Which versions are affected.** **Every published version that contains the Kafka transport
subscriber is affected**, and we are deliberately not publishing a list of versions. We confirmed the
behaviour in every `10.0.0` pre-release this repository has tagged; we have not verified the superseded
`3.0.0-alpha.*` line and are not telling you it is safe.

**What to do now.**

- **Catch exceptions inside your handlers** and decide their outcome yourself — return a failure result,
  or route the message to your own retry or dead-letter mechanism — so that nothing reaches the
  framework as an unhandled exception.
- **Make handlers idempotent**, and reconcile against the source of truth if you need to know what was
  lost: nothing in the consumer group's offsets will tell you.
- **Treat an unhandled-exception log entry from the Kafka subscriber as a lost message**, not as a
  warning. Alert on it.

**Is it fixed?** **Not in any released version.** When a release carries a fix, this entry will name
that version.

### Dependencies and packaging

#### `Excalibur.Outbox.Marten` before `10.0.0-alpha.12` brings in a Marten with a critical SQL-injection advisory

**FIXED IN `10.0.0-alpha.12`, published 2026-09-25. Upgrade to it — you do not need to override
anything.** Versions `10.0.0-alpha.4` through `10.0.0-alpha.11` declare a dependency on
**Marten 9.12.0**, which carries
**CVE-2026-75513 / GHSA-rfx3-98h7-v3xp, CVSS 9.1**
(`CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:C/C:H/I:L/A:L`). Marten interpolates a runtime, potentially
attacker-influenced value into generated SQL as a single-quoted string literal, without escaping or
parameterization, so a value containing a single quote can break out and inject arbitrary SQL.

**The advisory names six files, and they are not all one kind of path:**

| Group | File | Reached by |
|---|---|---|
| LINQ provider | `DictionaryItemMember.cs` | a dictionary indexer key — **the primary confirmed vector** |
| LINQ provider | `DictionaryContainsKeyFilter.cs` | `Dictionary.ContainsKey(key)` |
| LINQ provider | `SelectParser.cs` | a constant string projected through `Select` |
| Tenant management | `DeleteAllForTenant.cs` | a tenant id reaching per-tenant projection teardown |
| Tenant management | `DatabaseScopedTenantPartitions.cs` | a tenant id inlined into `FOR VALUES IN` partition DDL |
| Event store daemon | `Events/Daemon/Internals/EventLoader.cs` | a per-tenant partition-pruning literal — a defence-in-depth sink rather than a primary vector |

**Writing no LINQ does not put you outside this.** The tenant-management and daemon paths are reached
by ordinary multi-tenant operation rather than by query style. **That is a statement about coverage,
not about likelihood** — the confirmed vector is the LINQ dictionary-key path, and the others are
additional sinks the patch closes.

The advisory's stated impact is **multi-tenant authorization bypass — returning other tenants' rows —
and blind data exfiltration**. Its vector carries a low integrity impact as well (`I:L`), and data
modification may also be reachable: Npgsql accepts semicolon-separated statements by default, with no
setting to disable. **Whether that is reachable through Marten's generated commands is something we
have not established.**

#### ⚠ The advisory is wider than the version we declare

**Affected: Marten `>= 7.0.0, <= 9.12.0`. Patched: `9.13.0`.**

**Every version of our package that you can install declares `9.12.0`**, so the table below is about the
version you get *by default*. **But if you have overridden Marten to any version from `7.0.0` onward, you
are still affected** — pinning *backwards*, or to any 8.x, does not help. Only `9.13.0` or later does.

**The remedy is now a version, not a workaround: upgrade to `10.0.0-alpha.12` or later.** We verified
this against the published manifest rather than our own source — `alpha.12` declares
`<dependency id="Marten" version="9.13.0" />` and `alpha.11` declares `9.12.0`.

> **This paragraph previously said the opposite, and said it for a day.** It read: *"Our source has
> since moved to `9.13.0`, and that does not help you yet … overriding the Marten version yourself is
> the remedy that exists today."* That was true when written and became false when `alpha.12` was
> published on 2026-09-25. It is quoted here rather than deleted because it erred in the direction
> that keeps you on a vulnerable library: it told you no fixed version existed while one did. If you
> pinned Marten manually on the strength of it, that override is no longer needed, though it is
> harmless provided you pinned forward to `9.13.0` or later.

**This was in every version we had published up to and including `alpha.11`.** We checked each one's manifest on nuget.org rather than
inferring it from our source:

| Package | Versions | Declares |
|---|---|---|
| `Excalibur.Outbox.Marten` | `10.0.0-alpha.4` through `10.0.0-alpha.11` — all 8 | `Marten` `9.12.0` |

**Why it reaches you even though you never asked for 9.12.0.** Our manifest names `9.12.0` without
brackets, which NuGet reads as a *minimum*, not a pin. NuGet then applies its lowest-applicable-version
rule and restores exactly `9.12.0` — the vulnerable one — unless something else in your graph asks for
more. So the default outcome of installing this package is the vulnerable version.

**Your own build probably told you this already — and if it did not, that silence is not evidence.**
Our packages target `net10.0` only, and on .NET 10 and later NuGet's dependency audit defaults to
auditing **transitive** packages, not just direct ones. So an ordinary `dotnet restore` reports the
vulnerable Marten pin as `NU1903`/`NU1904`, on every restore, without needing anything from us. **If
you have seen that warning, it is this — the same finding, not a second one**, and the ids above should
reconcile with whatever your own scanning reported.

**Two cases where you would not have been told**, which is why this entry exists rather than leaving it
to your toolchain:

- you set `NuGetAuditMode` to `direct`, or suppressed `NU1903`/`NU1904`. That is a default control
  switched off rather than something we concealed — but it does mean the warning never reached you.
- the audit **warns**; it only fails the build if you treat warnings as errors. A warning in a noisy
  restore log is easy to have scrolled past.

**What to do — and you do not need a release from us.** Add a direct reference to a fixed Marten
alongside our package:

```xml
<PackageReference Include="Marten" Version="9.13.0" />
```

NuGet's *direct dependency wins* rule means your reference overrides the transitive one, and because
this is an upgrade it raises no downgrade warning. **The vulnerability is entirely in the dependency, so
pinning it removes your exposure completely** — there is nothing left for us to fix on your behalf.

**Our own declared dependency is fixed — `10.0.0-alpha.12` and later declare Marten `9.13.0`. What is
not fixable by us is an override you made yourself**, and that is what this section is about: if you have
pinned Marten to any version from `7.0.0` through `9.12.0`, upgrading our package does not move you off it,
because a direct reference in your project wins over our transitive one. Raise your own pin to `9.13.0` or
later. That remedy is under your control today and there is nothing left for us to ship on your behalf.

> **This paragraph previously read: "Not yet fixed in any released version. There is no version of
> `Excalibur.Outbox.Marten` you can upgrade to that resolves this."** It contradicted this entry's own
> opening, which names `10.0.0-alpha.12` as carrying the fixed pin, and it erred in the direction that
> keeps you on a vulnerable library. It is quoted rather than deleted so a reader who acted on it can
> recognise it.

**What we have not established, stated so you can judge the urgency yourself.** We have confirmed the
vulnerable dependency is in your graph. We have **not** determined whether our own outbox queries reach
an unescaped code path — our store does use Marten's LINQ provider, and most of its predicates compare
enums, integers and timestamps rather than strings. **Nor have we assessed the tenant-management paths
at all**, which the advisory names alongside LINQ. **Treat that as unfinished work on our side, not as
reassurance.**

**And it would not change your exposure even if we finished it and the answer were "we never reach it."**
The vulnerable library is loaded into your process. Your own code queries Marten through the same
session our store uses, so the unescaped path is reachable from your application whether or not our
outbox happens to touch it. **What our store does bounds *our* culpability, not *your* risk** — so if we
later publish "the outbox does not reach the vulnerable path," read that as narrowing where the fault
lies, never as a reason to unpin Marten.

**Applies only if you use the Marten outbox store.** The other outbox providers do not reference Marten.

#### Published pre-release packages carry a benchmarking harness as a direct dependency

**What you see.** If you restored `10.0.0-alpha.8`, your dependency graph contains `BenchmarkDotNet` and three compiler-platform packages that nothing in your application uses. 103 of the 195 packages published at that version declare the harness directly, so most of the framework brings it in. You will see it in your lock file, in a restore-graph listing, and in any dependency or supply-chain scan you run against your build.

This affects what you restore and audit, not what you run: nothing in the framework calls into the harness at runtime, so there is no behavioural change and no code of yours to alter. The cost is a larger restore, additional packages in your lock file, and additional entries a scanner will attribute to your application.

**How it happened.** One package referenced the harness without marking it as a build-time-only dependency, and this repository pins transitive package versions centrally. That combination promotes a transitive reference to a direct one at every package that depends on it, and packing writes direct references into the published manifest. It reached every package with a path to the one that carried it — 102 of them, plus the package itself.

**What you must do.** Move to a pre-release later than `10.0.0-alpha.8`. There is no useful workaround while you remain on it: the dependency is declared in the published manifest, so it is resolved before any setting in your own project applies. Excluding its assets stops it being referenced by your compilation but does not remove it from your restore graph or from a scanner’s view.

Later versions do not carry it, and the packaging pipeline now fails the build if any shipped package declares a dependency from a category that cannot be correct at your runtime — benchmarking harnesses, test frameworks, mocking and assertion libraries, analyzers, and the compiler platform among them.

---

---

### Test fixtures

#### The bundled Cosmos DB emulator fixture cannot connect using its documented approach

**What you see.** Calls made through a `CosmosClient` built against `CosmosDbContainerFixture` may never reach the emulator. Rather than failing quickly, requests repeat and hang.

**What you must do.** Set **both** `LimitToEndpoint` and `SerializerOptions` on the client options:

```csharp
var options = new CosmosClientOptions
{
    LimitToEndpoint = true,
    ConnectionMode = ConnectionMode.Gateway,
    SerializerOptions = new CosmosSerializationOptions
    {
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
    },
};
```

:::danger `SerializerOptions` is not optional, and omitting it fails silently
An earlier version of this page showed only `LimitToEndpoint` and `ConnectionMode`. **Following that incomplete recipe produces a client whose point-reads silently miss.** The Cosmos SDK's default serializer emits PascalCase property names, so a client built without the naming policy writes `Id` where a later point-read looks for `id` — and the read returns nothing for a document that is present, with no error. If you built a client from our previous instructions, add `SerializerOptions`.
:::

The endpoint option was established by execution against the emulator, using client options alone with nothing taken from the fixture. It addresses the advertised-endpoint obstacle; your environment may impose others beyond it. The fixture owns only the container lifecycle and the connection string, and the emulator can be slow to become ready — keep test timeouts generous.

## Unverified areas

Nothing in this section is a report of broken behaviour. Each entry marks a place where our testing does not reach, so that you do not read our test totals as covering it.

### The Record-of-Processing-Activities data map has no executing test coverage

**What you see.** Nothing that distinguishes it from covered code — which is the point of disclosing it.

**What it means.** The data-map query path in the SQL Server and PostgreSQL compliance providers is not exercised by any executing test. Tests for it exist, but they either substitute the query store or target the in-memory implementation, so no query is ever executed against a database. The shared conformance suite covers this path, but only its in-memory derivation was ever implemented.

**How we found out, because it is the honest answer.** A defect on this path made every call to it fail unconditionally, and it still passed our full unit suite, our harness checks, and review. A method that could not succeed under any input survived all of that. That tells you nothing about the defect and a great deal about the coverage: the only way it survives is if nothing exercises the path.

**That defect is now fixed. This entry is not about the defect.** Repairing the query makes it work; it does not create the test that would have caught it, so the coverage gap this describes is unchanged.

**What you must do.** Validate the data-map path against your own infrastructure before relying on it for a record-of-processing-activities report.

### The shipped key-escrow conformance kit has no implementations

**What it means.** The key-escrow conformance kit we ship is an abstract base class with **no derived suites anywhere**, so no test runner discovers anything in it. It cannot execute by construction.

**What you must do.** If you implement a custom key-escrow provider, do not expect the shipped conformance kit to validate it — it will run nothing.

**The snapshot kit was in the same state and no longer is.** It now has a derived suite exercising it against the in-memory store, so it is no longer inert. Separately, and unchanged, snapshot-store *behaviour* across providers is covered by nine provider suites deriving an internal base class rather than the shipped kit — so a custom snapshot provider validated against the shipped kit is being held to a narrower set of arms than our own providers are. The shipped snapshot kit currently carries no tenant-isolation and no concurrency arms; if you need either verified for your provider, write those yourself for now.

### Several integration suites are now measured executing

**RESOLVED by measurement.** This entry previously read *"Several integration suites are not measured
executing"* and named Elasticsearch monitoring, OpenSearch, tiered storage (S3 and Azure Blob) and tenant
sharding as areas we held no measurement for. It set its own bar explicitly: *"Our standard for removing
one of these is the suite being **measured green**, not the fix being written or the reasoning being
persuasive."* That measurement now exists.

Measured across the full shard set, counting per-test outcomes from the result files rather than summary
lines:

| Area | Passed | Failed | Not executed |
| --- | --- | --- | --- |
| Elasticsearch | 2,253 | 0 | 0 |
| OpenSearch | 127 | 0 | 0 |
| Tiered storage (S3, Azure Blob) | 79 | 0 | 0 |
| Tenant sharding | 198 | 0 | 0 |

**Why the zero in the last column is the load-bearing number.** The original concern was not that these
suites failed — it was that they *gate on container availability and skip silently*, so a green build said
nothing about them. A count of zero not-executed is what distinguishes "ran and passed" from "was skipped
and reported clean". The same measurement run shows a sibling suite with 11 not-executed entries (Azure
Key Vault, which has no local emulator), which is what establishes that the count can be non-zero and is
therefore worth reading.

**What you must do.** Nothing for these four areas. Validating against your own infrastructure remains
sound practice, but this framework no longer asks you to compensate for a measurement it does not hold.
### Cosmos DB coverage is thinner than the rest

Three separate things are true about Cosmos DB in this release, and they are easier to act on together than apart.

- **The integration tests now pass, measured.** This bullet previously read *"Some integration tests do
  not pass… we have not published a build in which they executed and passed."* A full run of the
  Cosmos-traited integration tests now reports **1,393 passed, 0 failed, 0 not executed**, with emulator
  containers observed starting during the run — so the result is not a skipped suite reporting clean. The
  remaining two bullets below still stand: scheduling and the telemetry suite are separate matters from
  whether these tests pass.
- **The snapshot-store conformance suite runs nightly, not on every change.** Cosmos DB tests are deliberately excluded from the per-change build so it does not depend on a slow emulator start; they run on a nightly schedule instead. A Cosmos regression is therefore caught within a day rather than on the change that introduces it. The readiness check **refuses** rather than skipping when the emulator is not genuinely usable, so a nightly pass is real evidence — but a green per-change build is not evidence about Cosmos.
- **The event-store telemetry suite executed on no CI runner at all, and we have removed the cause but not yet published a passing run.** All fourteen of its tests self-skipped on Linux runners, and both of our CI paths are Linux — so the suite reported a clean green with none of its fourteen tests executed, and nothing in the result summary was red. The self-skip is now gone: in CI an unavailable emulator fails the suite with a named error instead of skipping it, and a separate check refuses the job when a run reports success without producing evidence that it reached the emulator. **What we cannot yet tell you is whether these fourteen tests pass**, because the suppression we removed claimed they were unstable, and we have not published a run in which they executed. Treat this suite as unverified until we can point at that run — the difference from before is that a green here can no longer be earned by skipping.

**About the other providers.** A recent full run also showed failures outside Cosmos DB, but on inspection almost all were test containers failing to start on the machine running the suite — a local resource limit, not the providers misbehaving. We are not reporting those as provider defects, and we are not claiming that run proves the other providers correct either. What we can say is narrower and it is what we mean: **the Cosmos DB gaps above are real coverage gaps; the rest of that run's failures were environmental.**

**What you must do.** Treat the Cosmos DB provider as materially less proven than the others in this release. If you depend on it, validate the operations you rely on against your own infrastructure before trusting them in production.

### The in-memory inbox is not tenant-aware

**What it means.** The in-memory inbox store keys entries without a tenant term, so it does not separate tenants. Every persistent inbox provider does — the tenant is part of the stored key, so reads and claims are constrained by construction.

**The guard is now order-independent.** `AddMultiTenancy()` requires a tenant-scoping capability from the inbox and throws when the in-memory store is registered, because that store advertises none. That check reads registrations, so on its own it saw only what was registered before the call — registering the inbox afterwards used to slip past it. The same requirement is now re-asserted against the finished container at host start — both the per-contract requirement, which names the specific capability each contract must present, and the broader sweep over every contract the framework declares tenant-owned — so neither call order reaches a started host with an unattested inbox. The previously disclosed ordering gap is closed.

**What you must do.** Do not use the in-memory inbox for multi-tenant workloads — it is intended for development and tests. If you do use it multi-tenant, do not rely on the startup guard alone.

---

## Resolved

Fixed issues have moved to **[Resolved issues](resolved-issues.md)**, with the release each fix landed in and how we confirmed it.

They are listed rather than deleted so that a fixed issue stays distinguishable from a forgotten one. They are not on this page so that this page answers one question only: what is still wrong.

## See also

- [What's New](./whats-new.md) — what changed in this release, and what to do to upgrade
- [Multi-tenancy](./multi-tenancy.md) — how tenant isolation is intended to work
- [Versioning strategy](./migration/version-upgrades.md) — release stages and what each one guarantees
