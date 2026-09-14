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

Every entry is re-checked against the code before each update. Entries we have confirmed fixed are listed under [Resolved](#resolved-since-the-last-update) rather than quietly deleted, so you can tell a fixed issue from a forgotten one.

---

## Defects

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

### The `[Sensitive]` attribute does not encrypt anything — its own documentation says it does

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
that combination is the one the framework actually encrypts. Audit any property you annotated
`[Sensitive]` expecting encryption, and check the stored value directly rather than the log output.

**If you have already told an assessor that `[Sensitive]` fields are encrypted at rest, that statement is
not true of any version we have published**, and correcting it is more urgent than the code change.

### `[EncryptedField]` on a `string` property is silently ignored, and every example we published used a `string`

**Who this affects.** Anyone who annotated a `string` property with `[EncryptedField]` — which is anyone
who followed the documentation, because every example we published used one.

**What you see.** Nothing. The attribute applies, the code compiles, and no warning or error is raised at
any point. The property is stored in plaintext.

**The mechanism.** All three paths that act on the attribute select only `byte[]` properties
(`EncryptionDecryptionService`, `ReEncryptionService`, and the encrypting projection-store decorator). A
`string` never matches, so the annotation is read, found not to apply, and skipped in silence. The
capability is present in the same assembly — the crypto-shredding path selects `string` *and* `byte[]`, so
it has always crypted strings — the encryption paths simply do not use it.

**This is distinct from the `[Sensitive]` entry above, and more pointed.** `[Sensitive]` never claimed to
be the encryption annotation once you read past its summary. `[EncryptedField]` *is* the encryption
annotation, and this hits the consumer who reached for the right one.

**What to do.** Audit every `[EncryptedField]` annotation for a `string` property. Those values are in
plaintext now and nothing will migrate them for you: when this is fixed it will encrypt new writes only,
because nothing decrypts — nothing was ever encrypted. Move the property to `byte[]`, or use
`[PersonalData]` on a record that also carries `[DataSubjectId]` with crypto-shredding registered.

**A note on our own correction.** The published examples have since been changed to `byte[]`, which fixes
the documentation and not the defect — and it makes the defect *less* discoverable, because the documented
path no longer trips it. Anyone who annotated a `string` from the earlier guidance is still in plaintext
and still has no signal. **Do not treat the corrected examples as evidence that your existing annotations
are fine.**

**Is it fixed?** **Not in any released version.** Crypto-shredding is unaffected throughout — it has always
handled `string` and `byte[]` alike.

### The bundled Cosmos DB emulator fixture cannot connect using its documented approach

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

### IntelliSense tells you audit chain verification is confined to your tenant; on both SQL stores it is not

**This is a documentation defect, not a data-isolation one.** No audit record crosses a tenant boundary, and
nothing you have stored is exposed to anyone. What is wrong is what the framework *told you the result
means* — and for an audit artefact, that is the part you would hand to an assessor.

**What you see.** Hovering `IAuditQuery.VerifyChainIntegrityAsync(startDate, endDate, ct)` in your IDE
shows:

> *Confined the same way as `QueryAsync`: verifies only the caller's own tenant's hash chain over the given
> range, never another tenant's.*

**What actually happens.** The method takes no tenant argument, and its scope is a property of the store:

| store | scope of a verification |
|---|---|
| SQL Server | **estate-wide**, enumerated per partition |
| PostgreSQL | **estate-wide**, enumerated per partition |
| in-memory | confined to the ambient tenant |

So on either production store the result attests the integrity of the whole chain, not of one tenant's
slice. The cross-reference is what makes this hard to catch: `QueryAsync` **is** confined exactly as the
sentence says, so the claim borrows its credibility from a true statement about a neighbouring member.

**What you must do.** **Do not present an estate-wide verification result to a single tenant as evidence
about their own data.** It is a sound integrity check — it is simply a check over more than you were told.
Treat it as an operator-level operation. If you want to restrict who may call it, register
`AddRbacAuditStore()`, which requires a compliance-officer role or above and writes a meta-audit record of
every verification.

**Which versions are affected.** The remark is attached to that method in the shipped XML documentation of
`Excalibur.Compliance.Abstractions` in **`10.0.0-alpha.9` and `10.0.0-alpha.10`**. We checked the shipped
documentation file in every earlier package we hold — `alpha.8`, `alpha.7`, `alpha.5` and the `3.0.0`
line — and the sentence is **not** present in any of them. It entered between `alpha.8` and `alpha.9`.

**Is it fixed?** The wording is corrected at our development head and **is not in any released version
yet.** When a release carries it, this entry will name that version. The *behaviour* was never wrong and
does not change; only the description of it does.

### A container holding the timeout middleware cannot be disposed synchronously

**What you see.** You register the timeout middleware the documented way and then dispose your container
the ordinary way:

```csharp
builder.Services.AddDispatch(dispatch => dispatch.UseTimeout());

using var provider = services.BuildServiceProvider();   // throws on dispose
```

At scope disposal you get:

```
InvalidOperationException: ... type only implements IAsyncDisposable.
Use DisposeAsync to dispose the container.
```

**Why.** `TimeoutMiddleware` implements **only** `IAsyncDisposable` — it has an async `DisposeAsync()` and
no synchronous `Dispose()`. `Microsoft.Extensions.DependencyInjection` refuses to dispose such a service
from a synchronous disposal path rather than blocking on it, so the throw comes from the container, not
from this framework, and it names the container rather than the middleware that caused it.

**The workaround, if you cannot upgrade.** Dispose asynchronously:

```csharp
await using var provider = services.BuildServiceProvider();
```

Any host that disposes its container asynchronously is unaffected. The crash reaches you on a
**synchronous** disposal path — most often where you build and dispose a provider yourself, in a test, a
console app, or a short-lived worker. If you are unsure which your host does, `await using` is safe either
way.

**Which versions are affected.** Every published `10.0.0-alpha` we can read carries both the middleware
and the `UseTimeout` registration that reaches it — measured in the shipped assemblies of `alpha.5`,
`alpha.7`, `alpha.8`, `alpha.9` and `alpha.10`, and in the late `3.0.0-alpha` packages as well. The
async-only disposal contract is recorded in the framework's published public-API surface, not merely at
our development head.

**We have not established the first affected version**, and we would rather say so than name one we have
not opened. If you are on a version not listed above, assume you are affected and use `await using`.

**Is it fixed?** A fix exists but **is not in any released version yet.** The fix removes `DisposeAsync()`
from this type entirely and stops it implementing `IAsyncDisposable`: the method released nothing — the
only state the middleware held was a process-lifetime `ActivitySource` — so the interface was a claim on
resources the type did not have. If you have been calling `DisposeAsync()` on it directly, that call has
never done anything.

When a release carries the fix, this entry will name that version. Until it does, there is no upgrade that
resolves this for you, and the `await using` form above is the remedy.

### The SOC 2 evidence package is fabricated, and its chain-of-custody hash is the same value every time

**What you see.** `ISoc2ComplianceService.GetEvidenceAsync(criterion, periodStart, periodEnd, ct)` returns a
complete, well-formed `AuditEvidence` for any criterion and any period. It is empty and it is not derived
from anything:

```
Items                  = []
Summary.TotalItems     = 0
Summary.AuditLogEntries        = 0
Summary.ConfigurationSnapshots = 0
Summary.TestResults            = 0
ChainOfCustodyHash     = <the same string, always>
```

Nothing is queried. The implementation does not read an evidence store, and the criterion and period you
pass do not influence the result. **The `ChainOfCustodyHash` is computed over the empty item list, so it is
a constant** — the SHA-256 of an empty string, identical for every criterion, every period, and every
tenant. It is a well-formed value in the field an assessor would use to establish that an evidence package
has not been altered, and it establishes nothing.

**Why you are unlikely to catch this.** Nothing fails. There is no exception, no warning, and no empty-result
signal that reads as "not implemented" rather than "nothing happened in this period" — an empty evidence
package for a quiet period is a plausible answer. The hash is present and looks like the artefact that makes
the package trustworthy, which is precisely the field that would stop you looking further.

**Which versions are affected.** All currently published ones, including `10.0.0-alpha.10` — confirmed by
reading `GetEvidenceAsync` and `ChainOfCustodyHash` out of that package's own shipped assembly. The method
is on the published public surface, so any consumer who called it received this.

**What you must do.** **Do not use `GetEvidenceAsync` output as evidence for anything, and do not hand its
`ChainOfCustodyHash` to an assessor.** If you have already included a generated evidence package in an audit
submission, it does not support the controls it appears to support, and the hash does not attest to its
integrity — treat those as gaps to re-evidence by other means rather than as covered. Collect SOC 2 evidence
from your own systems of record: your audit log store, your configuration management, and your test results.

**The sibling export is the same shape, and easier to spot.**
`ISoc2AuditExporter.ExportForAuditorAsync(format, periodStart, periodEnd, ct)` returns a **zero-byte array**
for every format and every period — also present in `10.0.0-alpha.10`. That one at least announces itself: an
empty file is visibly empty. The evidence package above is the dangerous one, because it is well-formed.

**What the fix will be, so you can plan.** The correct behaviour for a capability that cannot produce a value
is to refuse rather than to return a confident empty one. Expect a future version to throw rather than hand
back a fabricated package or an empty export; code that calls either today should not be written to depend on
a successful return.

### A SOC 2 control nobody assessed is reported as a failure against you, not as a gap in our coverage

**This is the inverse of the entry below it, and it is the worse direction.** That one gives you a green
you did not earn. This one puts a **red against your name** for a control the framework never looked at —
in the document you hand an assessor.

**What you see.** A generated SOC 2 report marks a criterion **not met**, lists an exception under it, and
the overall opinion comes back **Qualified** or **Adverse**. Your controls may be fine. The framework simply
had **no validator registered for that criterion at all**.

**Why.** A criterion with no registered validator yields an empty control list, so the report has nothing to
aggregate. Rather than recording that it assessed nothing, it **fabricates the criterion's whole status**:
not met, effectiveness score zero, and a validation timestamp of the moment you asked. The exception list is
then built from not-met criteria, and the opinion is derived from the resulting compliance level.

**A criterion that IS registered is not affected**, and this is worth stating because the narrower claim is
the true one: every control id the report iterates comes from the same registration map it validates
against, so a *partially* registered criterion cannot produce this. The failure is all-or-nothing per
criterion — which makes it likeliest exactly where you are claiming a criterion you have not wired a
validator for.

**Why this matters more than an ordinary wrong number.** Assurance work turns on a distinction this
collapses:

| | means | who it reflects on |
|---|---|---|
| **scope limitation** | "we did not examine this" | the examiner's coverage |
| **exception** | "we examined it and it failed" | **your control environment** |

The framework reports the first as the second. It understates our coverage and overstates your defects, in
the one artefact whose purpose is to be read by someone deciding whether to rely on your controls.

**Which versions are affected.** Every published package we hold: `10.0.0-alpha.5`, `.7`, `.8`, `.9`,
`.10`, and the `3.0.0` line — the validation service and the report generator are present in all of them
(control: the SOC 2 compliance service is present in each on the same read). We have not established a
first-affected version and are not going to name one we have not opened.

**How to tell, on your own installation.** The fabricated timestamp is the discriminator — a criterion the
framework never examined is stamped as validated at the instant you asked:

```csharp
var soc2   = provider.GetRequiredService<ISoc2ComplianceService>();
var asked  = DateTimeOffset.UtcNow;
var status = await soc2.GetComplianceStatusAsync(tenantId: null, cancellationToken);

foreach (var (criterion, s) in status.CriterionStatuses.Where(x => !x.Value.IsMet))
{
    // A not-met criterion whose LastValidated is ~the moment you called, with a zero score,
    // was never assessed. A genuinely failed one carries the timestamp of its real validation.
    Console.WriteLine($"{criterion}: score={s.EffectivenessScore} validated={s.LastValidated:O} asked={asked:O}");
}
```

**What you must do.**

1. **Before generating a report, confirm a validator is registered for every criterion you are claiming.**
   A criterion with no validator reads as failed rather than as unassessed.
2. **Do not hand a Qualified or Adverse opinion to an assessor without checking why.** Open the exception
   list and the per-control evidence: an unassessed control shows evidence saying the provider is not
   configured, sitting under a verdict that reads as a control failure.
3. **Where a control is genuinely performed outside this framework**, state it to your assessor as a scope
   limitation with its own evidence. It is not something we can evidence for you, and the report as
   generated will not distinguish it.

**There is a second symptom in the same report, and it is easy to miss.** A criterion for which **no**
control was assessed at all is not only marked not met — it is stamped with a validation timestamp of
*right now*. An assessor reads that as "examined a moment ago, and failed." Nothing was examined. The
timestamp is invented because the field has nowhere to record that no validation happened.

**Both symptoms have one cause**, and it is worth stating because it tells you where else to be careful:
the result type has no way to represent "not assessed". A two-state met/not-met flag must render an
unexamined control as *not met*, and a non-optional timestamp must be filled with *something*. The code is
not careless; **the type left no honest option**.

**Is it fixed?** No. **No released version behaves differently**, and the remedy is a contract change
rather than a patch — the result has to be able to say "not assessed" before anything downstream can stop
treating it as a failure. When a release carries it, this entry will name that version and say what
changes in a generated report.

### SOC 2 controls can report as satisfied without the framework having assessed them

**What you see.** `AddSoc2ComplianceWithBuiltInValidators()` (and `AddSoc2ComplianceWithMonitoring()`,
which calls it) registers `AvailabilityControlValidator`. Both of its dependencies are optional and
default to `null`:

```csharp
AvailabilityControlValidator(
    IComplianceMetrics? complianceMetrics = null,
    IBackupConfigurationProvider? backupConfigProvider = null)
```

With neither registered — which is what you get unless you wire them yourself — a generated SOC 2 report
marks **AVL-002 (Performance Metrics)** and **AVL-003 (Backup Verification)** as **satisfied**. Neither was
assessed. The framework has no means of observing your monitoring or your backups when those providers are
absent, so the passing verdict rests on an assumption that an external system performs them, not on
anything it checked.

**Why you are unlikely to catch this.** The verdict is a pass, and the evidence attached to it says the
provider is not configured — so the report contains both the green mark and the reason it should not be
green. An assessor reading the verdict column sees two satisfied controls.

**What you must do.** Until this is fixed, treat AVL-002 and AVL-003 in a generated report as **not
assessed** unless you have registered `IComplianceMetrics` and an `IBackupConfigurationProvider` reporting
`IsBackupConfigured == true`. Do not hand a generated report to an assessor without confirming those two
registrations. If monitoring or backup verification is performed by a system outside this framework, that
arrangement needs independent attestation — it is not something we can evidence for you.

**This is not confined to the two controls above.** In `10.0.0-alpha.10`, eight of the framework's fourteen
SOC 2 control handlers contain no failing path at all — for these, the validator cannot return anything
other than satisfied, whatever you configure:

| | |
|---|---|
| SEC-002 | Encryption in Transit |
| AVL-001 | Health Monitoring |
| AVL-002 | Performance Metrics |
| AVL-003 | Backup Verification |
| INT-001 | Input Validation |
| INT-002 | Idempotency |
| INT-003 | Delivery Confirmation |
| CNF-001 | Data Classification |

**The table is anchored to one published version. Check it against the version YOU installed**, which may
differ — controls are being given failing paths as they are fixed, so a later release will show fewer than
eight. **Check it against your package and not against our repository.**
Register SOC 2 compliance without the provider a control names, run validation, and read the verdict for
that control:

```csharp
// Register the built-in validators and NOT the provider AVL-002 names.
services.AddSoc2ComplianceWithBuiltInValidators();

var validation = provider.GetRequiredService<IControlValidationService>();
var result = await validation.ValidateControlAsync("AVL-002", cancellationToken);

// On an affected version this is true even though no IComplianceMetrics is registered.
Console.WriteLine(result.IsEffective);
```

A control that reports satisfied while its declared mechanism is absent is an instance of this defect. Every
type used above is part of the package's public surface, so this runs against the assembly you installed —
you are observing the behaviour of your own build, not reading ours. **Do not verify this by reading our source** — the handlers are being changed as these are fixed, so
our repository already disagrees with the table above for some controls and will disagree for more. What
your installed package does is settled; what our source does is not.

**Whether each of these is a false attestation is a separate question we have not finished answering.** A
control may have no failing path because the framework genuinely always provides the mechanism — plausible
for INT-002, where idempotency comes from the outbox — or because nothing checks, which is the case for the
two availability controls above. **We are not going to tell you which is which until we have determined
it.** In the meantime the safe reading is the general one: **treat a satisfied verdict from this framework
as evidence that a code path ran, not that a control was assessed**, and confirm for each control that the
mechanism it names is actually present in your deployment.

**Which versions are affected — all of them.** Both behaviours were written before this package was ever
published: AVL-002 on 2025-11-26 and AVL-003 on 2026-01-19, while the earliest published
`Excalibur.Compliance` is `3.0.0-alpha.157`, published 2026-04-20. **There is no released version without
them**, so there is no upgrade within the current line that removes the behaviour and no lower bound worth
stating. If you have this package at all, this entry applies to you.

**The fix is not in any published version.** Nothing you can install carries it — **when a release does, this entry will name that version and say which controls it covers.**
Until it names one, there is no upgrade that changes what your report says.

**Scope.** Plain `AddSoc2Compliance()` does not register the built-in validators, so on its own it is
unaffected — but `AddControlValidator<TValidator>()` registers one explicitly, and a built-in validator
registered that way behaves exactly as described above. Where a
control does have a failing path, it is used properly — `CNF-002` and `CNF-003` both treat an absent
declared mechanism as an unverified control rather than a pass, and `CNF-003` is the pattern the
availability fix follows. That is why the table above lists particular controls rather than whole
validators: `CNF-001` has no failing path while its two siblings in the same class do.

### Oracle schema scripts do not stop on error, so a failed migration reports success

**What you see.** The `.sql` files shipped under `scripts/` in the `Excalibur.*.Oracle` packages are applied
by you, typically with SQL\*Plus. Almost none of them begin with

```sql
WHENEVER SQLERROR EXIT FAILURE
```

Without it SQL\*Plus prints the error, **continues to the next statement, and exits `0`**. A migration
runner, deployment pipeline or shell script that checks the exit code is told the schema was applied
correctly when it was not. Later statements run against a table that was never created or never altered, so
what you end up with is a schema that is partly migrated and reports as fully migrated.

**Why you are unlikely to catch this.** The failure is silent in exactly the place you would look. The
process exits `0`, your pipeline goes green, and the divergence only surfaces later as a missing column or a
constraint that was never added — usually at runtime, in the subsystem that depends on it.

**The guard is present on a minority of scripts, which is worse than its being absent everywhere.** If you
open one of the scripts that has it and conclude we apply it as a matter of course, you will be wrong about
the rest. Check every script you run rather than sampling one.

**What you must do.** Do not rely on the exit code of a SQL\*Plus run over these scripts. Either prepend
`WHENEVER SQLERROR EXIT FAILURE` to each script yourself before applying it, or verify the resulting schema
directly — the tables, columns and constraints the script was supposed to create — rather than trusting that
the command succeeded.

**Which versions are affected.** All currently published ones. In the newest published package,
`10.0.0-alpha.10`, **at least eleven of the seventeen Oracle scripts carry no guard** — so upgrading to the
latest release does not close the gap. We state that as a floor rather than an exact split on purpose: eleven
of the seventeen gained the guard only after that release was cut, so they cannot have been in it. Whether any
of the remaining six were guarded in the package as published we have not confirmed, and we would rather
understate our own protection than overstate it. The unguarded eleven are every script that creates a schema
from scratch, plus several migrations. **The first script you run against an empty database is among them**,
and the guarded ones are upgrade scripts you reach later, if at all.

The counts above describe `10.0.0-alpha.10` specifically, because that is the artefact you installed and the
only one you can act on. **The fix is not in any published version.** Nothing you can install carries it yet — **when a release does, this entry will name that
version**, and until it names one there is no upgrade that resolves this for you.

### The IBM MQ transport discards every message property you set

**What you see.** If you send through the IBM MQ transport and set properties on the message — a
correlation key, a routing hint, a tenant marker, CloudEvents attributes, anything — none of them reach the
queue. The send succeeds, no error is raised and no warning is logged. A receiver on the other side reads
the message back with an empty property set, and code that branches on one of those properties takes the
absent path.

Only the message body and one framework-internal property survive. Every property your own code attached is
dropped silently at the point of send.

**How it happened.** The sender wrote a single internal property and never enumerated the caller's. The
receiving half was correct throughout — it reads the full property set off the queue — so a round trip
through this transport looked like a receiver defect, or like properties that were never set, rather than
like a send-path loss. Nothing in the framework raised an error, because from the sender's point of view
writing one property and writing twenty are the same successful operation.

**Which versions are affected.** The transport has carried this since it was first added, and every
pre-release of it up to and including `10.0.0-alpha.10` contains it. **The repair is not in
`10.0.0-alpha.10` or any earlier release**; it is on the main branch and unreleased. **When a release carries the fix, this entry
will name that version**, and until it names one there is no upgrade that resolves this for you.

**How to confirm it on the version you hold.** Do not check this by reading our source — that tells you
about the current main branch, not about the package you have restored. Check it against your own queue
manager instead:

1. Register the transport as you normally would with `AddIbmMqTransport`, pointing at a queue you can read.
2. Send one message with at least one property set on it, alongside whatever body you normally use.
3. Receive that message back through the same transport and inspect the received message's `Properties`.

**If the property you set is absent, your version carries this defect.** The body arrives intact either
way, so a body-only check will not show it. If you cannot run against a real queue manager, the shortcut
is the version test above rather than any local experiment: nothing in your own configuration changes the
outcome.

**What you must do.** While you are on any released version, do not rely on message properties surviving a
send through IBM MQ. Carry the values you need inside the message body instead, where they are unaffected.
If you have code that reads a property back after a round trip and treats its absence as meaningful — a
missing tenant, an unset correlation id, an absent CloudEvents attribute — that branch has been taken on
every message, and any decision it made is worth re-examining rather than assumed correct.

Nothing you can configure changes this on a released version; the properties are discarded before the
message reaches the queue manager.

---

### The Azure Event Hubs transport can report a send as successful while dropping the message

**What you see.** Nothing. That is the defect. When you publish through the Azure Event Hubs transport,
messages are accumulated into a batch before being sent. If a message does not fit in the batch, it is
discarded and the send continues and reports success. **No exception is raised, no warning is logged, and
the returned result does not indicate that anything was lost.** The messages that did fit arrive normally,
so the topic looks healthy and your application has no signal to act on.

You are most likely to meet it under the conditions that make a batch fill: large payloads, many messages
published in one operation, or a message whose size grows after enrichment.

**No configuration avoids this on a released version.** On the version you have installed there is one
send path and it discards. A second, refusing path exists on the main branch, but it was added after the
most recent release — so if you have read elsewhere that enabling CloudEvents avoids this, that is true of
our development branch and **not of anything you can install today.** Do not spend effort reconfiguring to
escape it; there is no configuration that does.

**What you must do.** Until you are on a version carrying the fix, do not treat a successful return from
this transport as proof of delivery. If you need that assurance now, either publish messages individually
rather than in a batch, or reconcile what you sent against what your consumers received. The outbox
pattern gives you the same assurance structurally, because an unacknowledged message stays in the outbox.

**How to confirm it on the version you hold.** Do not check this by reading our source — that tells you
about the current main branch, not the package you restored. Publish a batch whose combined payload is
comfortably larger than your namespace's maximum event size, then count the events that arrive on the
hub. If fewer arrive than you sent and the call reported success, your version carries this defect.

**Is it fixed?** The repair exists on the main branch: both send paths now refuse an over-large message
with an exception instead of discarding it. **It is not yet in any released version.** When a release
carries it, this entry will name that version — until it names one, there is no upgrade that resolves
this for you, and the workarounds above are what you have.

**Which versions are affected — all of them.** This has been present since before the transport reached
its current package name, and every published version contains it. **We are deliberately not naming a
first-affected version**: the code has passed through more than one mass-rename commit, and a
path-scoped history search reports the rename rather than the original change — twice while investigating
this, that error made the defect look months younger than it is. Rather than publish a date we would have
to correct upward, we are telling you the safe thing, which is that no released version is free of it.

### The outbox fencing contract we published tells you to build the problem it exists to prevent

**Who this affects.** Only you, if you wrote your own outbox store against `IFencedOutboxStore`. **If you
use the outbox stores that ship with the framework, you did not implement this contract — we did**, and
the shipped stores we have examined hold the fence once per claim scope rather than once per row, which
is what the instruction should have said. This entry is about **the instruction we published to
implementers**, not a general statement that those stores are free of every fencing concern. **In
particular it does not clear the failure path**, which is a separate defect affecting every outbox user
regardless of whose store you run: see [the outbox failure report is required to check an owner it is
never given](#the-outbox-failure-report-is-required-to-check-an-owner-it-is-never-given).

**What you see.** Nothing, until a leader handover. Then two instances drain the same outbox at the same
time and your consumers receive the same messages twice. There is no error, because both instances
believe they hold the claim and, under the contract as written, both are right.

**What we told you to do.** The documentation shipped in the package says, of the claim operation:

> *"Implementations MUST claim only rows whose stored fencing high-water mark is less than or equal to
> `fencingToken` … A presented token below the stored high-water mark indicates a superseded (stale)
> leader; the store MUST exclude those rows from the claim."*

Read literally — and it is a `MUST`, so it is meant to be read literally — that puts the high-water mark
**on each row**. It is the wrong place for it.

**Why that is harmful rather than merely imprecise.** A mark stored per row is only advanced on rows that
have actually been claimed. So after a handover, a superseded leader presenting its old token is refused
on the rows the new leader has already taken, and **admitted on every row the new leader has not yet
reached** — which, at the moment of handover, is most of them. The superseded leader then claims and
drains them alongside the current one. This is not a race that needs an unusual interleaving; it is what
an ordinary failover does.

**What you must do, and it is two things rather than one.** Store the fence **once per claim scope** —
one row keyed by the outbox it guards — and compare the presented token against that, so a superseded
token is refused everywhere at once rather than row by row. **That alone is not sufficient.** A fencing
token identifies a *tenure*, not a *claim*, so it cannot tell two claim cycles of the same tenure apart:
a delayed report from an earlier cycle of the still-current leader presents a valid token and is
indistinguishable from a current one. Closing that needs an identifier for the individual claim, carried
alongside the token and checked with it. **A store that fixes only the first is correct about leaders and
still silent about claims**, which is why we are describing both here rather than telling you twice.

**How to confirm it in what you built.** Do not test this against a running system — a handover that
happens to be clean proves nothing. Open your implementation and answer one question: **when you compare
the presented fencing token, what row are you reading the stored mark from?** If the answer is "the
message row I am about to claim", you built what we specified, and you have this problem. If it is "a
single row for the whole outbox", you did not follow our instruction and your store is in better shape
than our documentation.

**Is it fixed?** The contract text is being corrected, and the framework's own stores never implemented
the version we published — they already fence per scope. **The correction is not in any released version,
and the documentation inside the package you installed cannot be recalled.** Until a release carries the
corrected text, this entry is the correction.

**Which versions are affected — assume every 10.x pre-release you hold.** We read the documentation file
inside the published packages available to us, and **every 10.x package we could open carries the wrong
instruction**, with none free of it. The interface itself has existed since the tenth of July 2026, and
the earliest 10.x package we hold was published a month after that — so there is no version in our reach
that predates the problem. **We are deliberately not naming a first-affected version.** We cannot open
the releases we do not hold, and a version's absence from our cache is not evidence that it is clean; a
reader on an early alpha should not read a list that starts at alpha.5 as permission to skip this.

**The 3.x line is genuinely not affected**, and that one is a measurement rather than a bound: the
interface does not exist in it at all. We checked its shipped documentation directly.

### The outbox failure report is required to check an owner it is never given

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

**Is it fixed?** A remedy exists in our development branch and **is not in any released version.** The shape
it takes is a separate, claim-scoped contract: a store opts into it, and its failure report carries the
claim identity as an argument, so the guard the documentation states becomes evaluable from the call rather
than from state the store happens to hold. We looked for it in every package we hold — `10.0.0-alpha.5`
through `alpha.10`, the `10.0.0-alpha.10.14` build, and the `3.0.0` line — and it is in **none** of them
(control: the existing outbox contract is present in all of them, so the absence is a real reading and not
a failed lookup).

**So nothing you can install today carries this, and the guidance above is what applies to you.** When a
release does carry it, this entry will name that version and say what changes for a store you wrote.

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

### The batch failure report performs no ownership check, and its documentation does not say so

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

**Is it fixed?** **Not in any released version.** An additional overload that accepts a claim identity
would not by itself resolve this for you: the member you call today would remain, unchanged, and calling
it would still perform no check. Until a release says otherwise, the behaviour above is what you have.

### Published pre-release packages carry a benchmarking harness as a direct dependency

**What you see.** If you restored `10.0.0-alpha.8`, your dependency graph contains `BenchmarkDotNet` and three compiler-platform packages that nothing in your application uses. 103 of the 195 packages published at that version declare the harness directly, so most of the framework brings it in. You will see it in your lock file, in a restore-graph listing, and in any dependency or supply-chain scan you run against your build.

This affects what you restore and audit, not what you run: nothing in the framework calls into the harness at runtime, so there is no behavioural change and no code of yours to alter. The cost is a larger restore, additional packages in your lock file, and additional entries a scanner will attribute to your application.

**How it happened.** One package referenced the harness without marking it as a build-time-only dependency, and this repository pins transitive package versions centrally. That combination promotes a transitive reference to a direct one at every package that depends on it, and packing writes direct references into the published manifest. It reached every package with a path to the one that carried it — 102 of them, plus the package itself.

**What you must do.** Move to a pre-release later than `10.0.0-alpha.8`. There is no useful workaround while you remain on it: the dependency is declared in the published manifest, so it is resolved before any setting in your own project applies. Excluding its assets stops it being referenced by your compilation but does not remove it from your restore graph or from a scanner’s view.

Later versions do not carry it, and the packaging pipeline now fails the build if any shipped package declares a dependency from a category that cannot be correct at your runtime — benchmarking harnesses, test frameworks, mocking and assertion libraries, analyzers, and the compiler platform among them.

---

---

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

### Several integration suites are not measured executing

**What it means.** These areas are exercised by unit tests, but we hold no measurement showing their integration suites executing: Elasticsearch monitoring, OpenSearch, tiered storage (S3 and Azure Blob), and tenant sharding. Their suites are included in our test selection and are not quarantined; they gate on container availability at runtime, so an absent container skips them rather than failing.

**We are stating this narrowly on purpose.** There is real counter-evidence — several of these areas carry sibling tests that *cannot* skip and would turn the build red if the infrastructure were missing, which suggests the containers are present and the suites do run. We are not treating that as grounds to clear the entry, because it is an inference and not a measurement. Our standard for removing one of these is the suite being **measured green**, not the fix being written or the reasoning being persuasive.

**What you must do.** If you depend on these areas, validate them in your own environment. Expect this entry to narrow once we can publish the measurement.

### Cosmos DB coverage is thinner than the rest

Three separate things are true about Cosmos DB in this release, and they are easier to act on together than apart.

- **Some integration tests do not pass.** When the Cosmos DB integration tests are run, some fail. We have not resolved those failures for this release, and we have not published a build in which they executed and passed.
- **The snapshot-store conformance suite runs nightly, not on every change.** Cosmos DB tests are deliberately excluded from the per-change build so it does not depend on a slow emulator start; they run on a nightly schedule instead. A Cosmos regression is therefore caught within a day rather than on the change that introduces it. The readiness check **refuses** rather than skipping when the emulator is not genuinely usable, so a nightly pass is real evidence — but a green per-change build is not evidence about Cosmos.
- **The event-store telemetry suite executed on no CI runner at all, and we have removed the cause but not yet published a passing run.** All fourteen of its tests self-skipped on Linux runners, and both of our CI paths are Linux — so the suite reported a clean green with none of its fourteen tests executed, and nothing in the result summary was red. The self-skip is now gone: in CI an unavailable emulator fails the suite with a named error instead of skipping it, and a separate check refuses the job when a run reports success without producing evidence that it reached the emulator. **What we cannot yet tell you is whether these fourteen tests pass**, because the suppression we removed claimed they were unstable, and we have not published a run in which they executed. Treat this suite as unverified until we can point at that run — the difference from before is that a green here can no longer be earned by skipping.

**About the other providers.** A recent full run also showed failures outside Cosmos DB, but on inspection almost all were test containers failing to start on the machine running the suite — a local resource limit, not the providers misbehaving. We are not reporting those as provider defects, and we are not claiming that run proves the other providers correct either. What we can say is narrower and it is what we mean: **the Cosmos DB gaps above are real coverage gaps; the rest of that run's failures were environmental.**

**What you must do.** Treat the Cosmos DB provider as materially less proven than the others in this release. If you depend on it, validate the operations you rely on against your own infrastructure before trusting them in production.

### The in-memory inbox is not tenant-aware

**What it means.** The in-memory inbox store keys entries without a tenant term, so it does not separate tenants. Every persistent inbox provider does — the tenant is part of the stored key, so reads and claims are constrained by construction.

**The guard is now order-independent.** `AddMultiTenancy()` requires a tenant-scoping capability from the inbox and throws when the in-memory store is registered, because that store advertises none. That check reads registrations, so on its own it saw only what was registered before the call — registering the inbox afterwards used to slip past it. The same requirement is now re-asserted against the finished container at host start — both the per-contract requirement, which names the specific capability each contract must present, and the broader sweep over every contract the framework declares tenant-owned — so neither call order reaches a started host with an unattested inbox. The previously disclosed ordering gap is closed.

**What you must do.** Do not use the in-memory inbox for multi-tenant workloads — it is intended for development and tests. If you do use it multi-tenant, do not rely on the startup guard alone.

---

## Resolved since the last update

Listed rather than deleted, so a fixed issue is distinguishable from a forgotten one.

**Each entry below now states where the fix is, and how we know.** Where an entry says *confirmed in*
a version, we read the discriminating type or member out of that published package's own assembly — not out
of our repository. Where we could not confirm it against a published package, the entry says so plainly
rather than implying a release.

Confirmation at this level means the code is **present** in the package you can install. It is not a
statement that it was executed there. **Verify against the package you actually restored** before treating
any entry as closing a risk for you.

- **Erasure and legal-hold reads are now tenant-scoped.**
  **Confirmed present in `Excalibur.Compliance` `10.0.0-alpha.10`** for the in-memory stores — the tenant
  derivation and the untenanted sentinel are both in that package's shipped assembly. **The SQL Server and
  PostgreSQL compliance packages were not checked**: we hold no copy of them to read, so for those two
  providers this is unestablished, not confirmed.
  Previously disclosed as unscoped, including a
  case where a tenant-wide legal hold was not consulted at all when no tenant was supplied, so an
  irreversible erasure could proceed past it. All six stores (SQL Server, PostgreSQL and in-memory, for
  both erasure requests and legal holds) now derive their tenant term from the ambient tenant context
  through a single derivation point, and a caller-supplied tenant is **ANDed onto** that term rather than
  replacing it — so the argument can only narrow a result, never widen it. Both contracts are now in the
  set `AddMultiTenancy()` checks, so a multi-tenant host that registers an unscoped implementation fails
  at startup instead of leaking at runtime.
  Reading and mutating a hold are deliberately asymmetric: a tenant **sees** an estate-wide hold, because
  it blocks that tenant's erasures, but cannot **modify** one — otherwise a tenant could re-home an
  estate-wide preservation order into its own partition and silently lift it for everyone else.
  **Not yet proven on PostgreSQL:** the structural fix is in place there, but no test runs against a real
  PostgreSQL server to detect a regression, so treat that provider as fixed-but-unverified.
  Background sweeps that expire holds and drain scheduled erasure requests remain deliberately
  estate-wide; scoping them to one tenant would stall erasure for every other tenant and make expired
  holds permanent.
- **The Cosmos DB, DynamoDB, Firestore and MongoDB event stores now confine tenants.**
  **Not confirmed against any published package.** The change landed on 2026-08-24, which is before the
  `10.0.0-alpha.10` packages were published, so it may well be in them — but we hold no copy of those four
  packages to read, and "before the release date" is not the same as "in the release". Treat this as
  unestablished for your installation and check the behaviour below against the package you restored.
  Previously
  disclosed as not separating tenants at all: their document keys were composed from the aggregate type and
  aggregate id, so two tenants writing the same aggregate id shared one document set and one version
  sequence, and a read under either returned the other's events. All four now compose the owning tenant
  into the document key as its leading segment, which makes a cross-tenant read unaddressable rather than
  merely filtered and gives each tenant its own version sequence. The shared conformance kit's three tenant
  arms — including the one that catches a filter-only fix, where two tenants sharing an aggregate id must
  version it independently — pass against real MongoDB, DynamoDB Local, and the Cosmos DB and Firestore
  emulators. A multi-tenant host may now register any of the four under either isolation strategy.
  **This changed the stored key shape.** Documents written by an earlier version are not addressable by the
  new key until re-keyed. Nothing is destroyed and no *startup* check fires, but the store no longer serves
  them as an empty stream: the first read that would have reported a false absence throws
  `InvalidOperationException` naming the collection and the offending key, and modifies nothing. **The same
  change applies to the saga stores on those four providers**, where an unguarded false absence would have
  made a coordinator restart a saga and re-fire every compensating action it had already performed.
  See [Cosmos DB, DynamoDB, Firestore and MongoDB keys carry the tenant](./migration/nosql-tenant-key-rekey.md).
- **Inbox reads are now tenant-scoped.**
  **Confirmed present in `Excalibur.Inbox.SqlServer` `10.0.0-alpha.10`** — the tenant partition helper is in
  that package's shipped assembly. **The other inbox providers were not checked**, for the same reason: we
  hold no copy of those packages.
  Previously disclosed as unscoped across the board. The relational stores (SQL Server, PostgreSQL, Oracle) now apply a tenant predicate to their read, claim and merge paths and fail closed when a tenant is active but unresolved; the document and cache stores (MongoDB, Cosmos DB, DynamoDB, Firestore, Redis, Elasticsearch) carry the tenant inside the stored key, so a keyed read cannot cross tenants. The in-memory store is the exception and is listed above.
- **The unexplained not-found responses from the Cosmos DB snapshot store are explained, and were never a provider fault.**
  **No version applies to this one.** The fault was in our own test harness and was never in any shipped
  package, so there is nothing for you to upgrade to. We disclosed seeing not-found responses for a database that should have existed, and said we did not know the cause and could not rule out the provider. We since determined it: our own test teardown deleted a database shared by the whole test class, so the first test destroyed it for every test that followed. It was our test harness. **Nothing about it affected consumers, and the previous entry implying the provider might be at fault was wrong.**

---

## See also

- [What's New](./whats-new.md) — what changed in this release, and what to do to upgrade
- [Multi-tenancy](./multi-tenancy.md) — how tenant isolation is intended to work
- [Versioning strategy](./migration/version-upgrades.md) — release stages and what each one guarantees
