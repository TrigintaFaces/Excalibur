---
sidebar_position: 2.6
title: Resolved Issues
description: Defects previously disclosed for the Excalibur 10.0.0 pre-release that have since been fixed, and the release each fix landed in.
---

# Resolved issues

These were disclosed on [Known issues](known-issues.md) and have since been fixed. They are kept here rather than deleted, because a deleted entry is indistinguishable from one nobody ever wrote down.

**Read the version, not the heading.** An entry here is fixed in the release it names. If you are on an earlier version the defect is still present for you, and the entry describes what you are running.

**Confirmation means the code is present in the package you can install.** Where we confirmed a fix against a published package, we read the discriminating type or member out of that package's own assembly rather than out of our repository. That is not a statement that it was executed there. Verify against the package you actually restored before treating any entry as closing a risk for you.

---

## Fixed in a published release

### SQL Server change data capture can silently drop captured changes, permanently, when a batch ends abnormally

**You are affected only if ALL THREE of these hold.** Stating it as "SQL Server CDC is affected" would
be wrong and would send unaffected consumers on an audit they do not need:

1. you use **SQL Server** change data capture, **and**
2. the reader is **still working through that table** when the batch ends. This is the real
   discriminator: if it reaches the end of the available changes first it discards its own in-memory
   position, and the problem cannot arise however the batch then fails. **and**
3. the batch then ends **without recording delivery of everything the reader had already queued** —
   the common case is a handler that throws — in a process that **keeps polling** afterwards. A crash
   takes the stale in-memory position with it and the next start reloads the durable one, so a hard
   failure is *safer* here than a survivable one.

**Condition 2 is the one to check yourself.** A handler that returns immediately lets the reader finish
the table and discard its position, which is why a prompt handler never reaches this and why a test
suite sees none of it. Almost any real handler occupies time, so in production the question is usually
only whether condition 3 also happens. **A slow handler alone is not enough** — if your batches complete, the checkpoint is
written and the invariant is restored on every cycle. It is the abnormal ending that does the damage,
because the step that records what was actually delivered sits on the normal path rather than in a
cleanup block, so an aborted batch skips it.

**When it does occur, captured changes are never delivered to anyone and the loss becomes permanent.**
There is no exception, no error return and nothing in a log. The next poll advances past the lost
range, so the window to notice closes on its own.

**How it happens.** Reading and delivering advance two different positions. The reader advances an
in-memory position as it *queues* changes; the durable checkpoint advances only as they are
*delivered*. **If the reader finishes a table it discards its in-memory position for that table**, the
two can no longer disagree, and nothing is at risk. If it is still mid-table when the batch ends, that
position survives — and because the position only ever moves to a strictly greater value, the next
poll discards the durable one in favour of it and skips everything queued but not delivered.

**The queue does not have to be full.** That is worth stating because it is the natural assumption and
it is wrong: a modest backlog on a large queue reaches this the same way. Our own regression test for
this runs at the default queue size with the queue never close to full. **The loss is bounded by `QueueSize + ConsumerBatchSize - 1`, which at the shipped
defaults of 1000 and 50 is up to 1049 changes per occurrence** — and once the next poll checkpoints, it
survives a process restart.

:::danger This was measured, and the number was predicted before it was measured
Against a real SQL Server instance with `QueueSize` deliberately set to 4 and a handler that occupies
the consumer and then throws, **46 of 50 captured changes were ever delivered — a shortfall of exactly
4, the queue size.** Two consecutive runs were identical, and the shortfall had been predicted as
"approximately `QueueSize`" before the experiment was built, specifically so that a failure for some
other reason could not be mistaken for confirmation.
:::

:::warning The obvious way to test this will tell you that you are fine
**The defect needs the reader still mid-table, a faulting batch AND a second poll.** A check whose
handler succeeds, or that polls once, or whose data is small enough that the reader finishes first,
will pass every time — and the natural way to check, running a small CDC job and confirming the changes
arrive, is exactly that shape.
**Seeing your changes arrive in a small test is not evidence that you are unaffected; it is a test that
cannot fail.**

This is also why our own suite did not catch it: a prompt handler takes one change, that batch of one
completes, and its checkpoint is written before the reader moves on, so the invariant is restored
continuously. To exercise the real path you need a handler that occupies the consumer **and then
throws**, with at least two polling cycles. Setting `QueueSize` to a small value makes it reachable in
a test without production volume.
:::

**"Have I already lost data?" — we cannot tell you, and you should assume the honest answer.** The loss
is silent and the checkpoint has already advanced past it, so nothing in the framework retains a record
that a change went undelivered. **There is no diagnostic we can offer you after the fact.** The only
way to answer it is outside the framework: compare what exists in your source tables — row counts, or
the LSN range for the period in question — against what your handler actually recorded downstream. If
you have no independent record of what your handler processed, the question is not answerable at all,
which is itself worth knowing before you need the answer.

**And your own telemetry will not answer it — it has an alibi we gave you.** If you followed the CDC
guidance and installed an idempotency filter, you already expect to see fewer events reach your
handlers than the source produced, and you have a documented reason for it. **A filter suppressing
duplicates and a poll silently dropping the tail look identical from outside**: the same shortfall,
with an explanation you were told to anticipate. So a careful reader checks their own metrics, finds
exactly what the documentation led them to expect, and closes this page. **Compare against the source
table, not against your own pipeline's counts.**

**This breaks a promise we made elsewhere, in the direction you were not warned about.** Until now our
change data capture guidance stated flatly that CDC is **at-least-once** and told you to add an
idempotency filter if your handlers are not naturally idempotent. **We have since corrected that page**
— delivery semantics are provider-specific and replay is not the only failure mode — but the version
you read said otherwise, and the filter it recommended is the right defence **against duplicates.** This defect fails the other way: it delivers **too few** events,
not too many. So a consumer who read that page, believed it, and installed the deduplication it
recommends took the exact opposite precaution to the one that would have helped, and had positive
reason not to go looking for missing changes. **If you followed our CDC guidance, you are the reader
this entry is for.**

**Is it fixed?** **Fixed in `10.0.0-alpha.12`.** **This entry previously said the fix was "not yet in any released version" — that was wrong, and it stayed wrong after the release shipped.** If you are on 10.0.0-alpha.12 or later you already have this fix; if you are on an earlier version, upgrading resolves it. Not yet confirmed by reading the published assembly — verify against the package you actually restore. We can prove that bound rather than assert it. The published version list for `Excalibur.Cdc.SqlServer` has 82
entries and the most recent is `10.0.0-alpha.11`, whose package predates the correction by three days —
so **no published version can contain it**, and that is the complete published set rather than the
subset we happen to hold. We are deliberately **not**
publishing a list of affected versions: whether a specific published build executes this path is a
behavioural question about that build, and we have not run it. Use the reproduction above against the
version you actually have.

**Mitigation.** **Stop your handlers throwing**: catch and handle their own failures so every batch
runs through to the point where delivery is recorded. **This follows from reading the code and we have
not run it**, and it is a mitigation rather than a fix — the defect is still present.

:::danger Do not raise `QueueSize` to reduce this — we measured it, and it multiplies the loss
Raising the queue is the intuitive response and it is the wrong one. Against a real SQL Server
instance on the affected code, changing **only** that value:

| `QueueSize` | delivered | **lost** |
|---|---|---|
| 4 | 46 of 50 | **4** |
| 40 | 10 of 50 | **40** |

(Consumer batch size was held at 1 throughout, so these are the `QueueSize` term of the bound above
in isolation.) **The loss scaled one-for-one with `QueueSize`.** A larger queue lets the reader get
further ahead before anything stops it, so it does not reduce your exposure — it scales it. If you
have already raised `QueueSize` for throughput, you have raised your worst case by the same factor.
:::

### An activity-group grant works on the first request and is silently denied on every request after it

**You are affected if you authorize through activity groups.** That is the whole condition, and it
is shorter than it looks like it should be for a reason worth stating: **there is no cache-free
configuration to fall back on.** The authorization policy provider takes the application-scoped
distributed cache as a required constructor dependency, so "activity groups without a policy cache"
is not a state you can be in — a host without that cache fails at start-up, with an error naming the
registration call to add. If you build a bare service provider and never start a host, the failure moves
to the first authorized request instead.

**If you use only direct grants, you are unaffected** regardless of your caching.

Where activity groups are in use, a grant that should apply is refused once the cache starts serving hits. **There is
no error.** The refusal is indistinguishable from a policy that correctly says no — no exception, no
warning, nothing in a log that reads as wrong.

**The tell, and you can check it in two requests.** The policy is read from your store on a cache
miss and from the cache on a hit, and the grant survives only on the miss path:

| | what happens |
|---|---|
| first request after a cold cache | read from the store — **the grant applies** |
| every request after that | served from cache — **the grant is silently denied** |

So the signature is an authorization decision that **changes without anything changing**. If you have
ever seen a permission work once after a restart and then stop, this is a candidate.

:::danger The obvious way to test this will tell you that you are fine, and it is wrong
**The cache-miss path authorizes correctly.** So if you restart the application, clear the cache, or
try it on a freshly-started instance, **you will see the grant work and conclude you are unaffected.**
That conclusion is exactly backwards: you tested the one path that is not broken.

**Exercise the same activity-group grant twice in succession, within the cache lifetime, without
restarting anything. The second attempt is the one that shows the defect.**

**Do not change anything between the two attempts.** Adding, revoking or editing a grant or an
activity group **clears the cache**, which sends the next check down the working path and resets the
test. A check written the natural way — *grant the permission, then verify it* — invalidates the
cache with the grant and therefore **passes every time, forever.** That is the most likely way to
conclude wrongly that you are unaffected, and it is probably how any test you already have is
written.

**On SQL Server and PostgreSQL a second defect also refuses the grant on the cache-miss path, so you
may see it fail on both requests rather than only the second. Failing twice does not rule this out —
it is a stronger signal, not a weaker one.**

This is also why it survived our own test suite — every existing arm takes the miss path.
:::

**Why — and the cache is not the broken part.** The policy's activity-group collection is declared
as a weakly-typed `object` on the seam that crosses the cache. **That widening is the defect.** A
declared `object` is the one shape a JSON round trip cannot carry intact: it comes back as a
`JsonElement`, which satisfies none of the collection tests the authorization check performs, so the
group contributes no activities and the grant is not found.

The round trip is what makes the widening *observable*, not what causes it — which is why every
provider is affected, why no serializer setting avoids it, and why the fix is to narrow the declared
type at the seam rather than to change how the document is cached. We state this precisely because
the natural reading of the two-request test above is that caching is at fault; it is not, and acting
on that reading leads to the wrong workaround.

**What is NOT affected — measured, not assumed.** **Ordinary grants are fine.** Every read of the
grants dictionary is key-only — two `ContainsKey` lookups and one enumeration that discards the value
— so the value shape this round trip damages is never read for them, and the same mechanism cannot
reach them. Every other consumer of that cache only invalidates it.

**The blast radius is activity groups, and nothing else.** If you do not use activity-group grants,
no audit is needed.

**What to do.** There is no configuration switch for this: the authorization policy provider takes
the distributed cache as a required dependency, so it cannot be turned off. Until a fixed version is
available:

- **Treat an activity-group grant as unreliable** and prefer a direct grant for anything you cannot
  afford to have silently refused.
- If you must keep activity groups, **substituting a no-op cache for the application-scoped
  registration** forces every read to miss and take the store path, which is the path that works.

  :::warning Substitute the right one — the wrong one silently disables your whole application cache
  Authorization uses **two** cache registrations, and they are easy to confuse:

  | registration | what it is |
  |---|---|
  | `AddDistributedMemoryCache()` / `AddStackExchangeRedisCache(...)` | the **base** cache — your entire application uses this |
  | `AddApplicationScopedDistributedCache(...)` | a **keyed wrapper** around the base, which partitions the keyspace per application |

  **Replace the wrapper, not the base.** Substituting a no-op for the *base* cache also stops the
  authorization symptom — which is exactly the problem, because the two outcomes are
  indistinguishable from the authorization side while the second one has **turned off caching
  everywhere else in your application**, with nothing to tell you.
  :::

  This trades the authorization cache's performance benefit for correctness, and it **follows from
  reading the code rather than from a test we have run** — verify it in your own deployment before
  relying on it.

**Is it fixed?** **Fixed in `10.0.0-alpha.12`.** **This entry previously said the fix was "not yet in any released version" — that was wrong, and it stayed wrong after the release shipped.** If you are on 10.0.0-alpha.12 or later you already have this fix; if you are on an earlier version, upgrading resolves it. Not yet confirmed by reading the published assembly — verify against the package you actually restore. The correction is complete in our source — the seam no longer carries the weakly-typed value at all, so the defect is not expressible there rather than merely absent. Until a release ships, the mitigation above is the only remedy available to you.

:::info What we have not established
**We do not know which published versions execute the broken path, and we are not going to guess.**
The components are present in every published package we inspected, and the defect is confirmed in
our current source for every provider — but establishing that a *specific* published build takes that
branch needs a behavioural test against that build, which we have not run. Five separate attempts to
date the defect from source history produced four different answers, and we withdrew them rather than
publish one.

**Use the two-request check above against your own deployment.** It answers the question for the
version you actually have, which is the only version that matters to you.
:::

### `[EncryptedField]` on a `string` property is silently ignored, and every example we published used a `string`

**Who this affects.** Anyone who annotated a `string` property with `[EncryptedField]` — which is anyone
who followed the documentation, because every example we published used one.

**What you see.** Nothing. The attribute applies, the code compiles, and no warning or error is raised at
any point. The property is stored in plaintext.

**The mechanism, in every released version.** All three paths that act on the attribute selected only
`byte[]` properties (`EncryptionDecryptionService`, `ReEncryptionService`, and the encrypting
projection-store decorator). A `string` never matched, so the annotation was read, found not to apply, and
skipped in silence. The capability was present in the same assembly — the crypto-shredding path selects
`string` *and* `byte[]`, so it has always crypted strings — the encryption paths simply did not use it.

**This is distinct from the `[Sensitive]` entry above, and more pointed.** `[Sensitive]` never claimed to
be the encryption annotation once you read past its summary. `[EncryptedField]` *is* the encryption
annotation, and this hits the consumer who reached for the right one.

**What to do.** Audit every `[EncryptedField]` annotation for a `string` property. Those values are in
plaintext now and nothing will migrate them for you: when this is fixed it will encrypt new writes only,
because nothing decrypts — nothing was ever encrypted. Move the property to `byte[]`, or use
`[PersonalData]` on a record that also carries `[DataSubjectId]` with crypto-shredding registered.

**A note on our own examples.** Some published examples were changed to `byte[]`; the attribute's own
IntelliSense example still shows a `string` property. Neither tells you anything about the values you
already stored. **Do not treat an example — of either shape — as evidence that your existing annotations
are fine.** Anyone who annotated a `string` under a released version is still in plaintext and still has
no signal.

**Is it fixed?** **Fixed in `10.0.0-alpha.12`.** **This entry previously said the fix was "not yet in any released version" — that was wrong, and it stayed wrong after the release shipped.** If you are on 10.0.0-alpha.12 or later you already have this fix; if you are on an earlier version, upgrading resolves it. Not yet confirmed by reading the published assembly — verify against the package you actually restore. **That fix will not reach the values you
already stored:** nothing was ever encrypted, so there is nothing to decrypt, and any fix can only encrypt
new writes. **Before you migrate a `string` property to `byte[]` to work around this, check which version
you are on** — that schema change is not required on a release that honours the annotation.
Crypto-shredding is unaffected throughout — it has always handled `string` and `byte[]` alike.

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

**Is it fixed?** **Fixed in `10.0.0-alpha.11`.** **This entry previously said the fix was "not yet in any released version" — that was wrong, and it stayed wrong after the release shipped.** If you are on 10.0.0-alpha.11 or later you already have this fix; if you are on an earlier version, upgrading resolves it. Not yet confirmed by reading the published assembly — verify against the package you actually restore. The *behaviour* was never wrong and
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

**Is it fixed?** **Fixed in `10.0.0-alpha.11`.** **This entry previously said the fix was "not yet in any released version" — that was wrong, and it stayed wrong after the release shipped.** If you are on 10.0.0-alpha.11 or later you already have this fix; if you are on an earlier version, upgrading resolves it. Not yet confirmed by reading the published assembly — verify against the package you actually restore. The fix removes `DisposeAsync()`
from this type entirely and stops it implementing `IAsyncDisposable`: the method released nothing — the
only state the middleware held was a process-lifetime `ActivitySource` — so the interface was a claim on
resources the type did not have. If you have been calling `DisposeAsync()` on it directly, that call has
never done anything.

`10.0.0-alpha.11` carries the fix, so upgrading resolves this. On earlier versions the `await using`
form above is the remedy.

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

**Is it fixed?** **Fixed in `10.0.0-alpha.11`.** **This entry previously said the fix was "not yet in any released version" — that was wrong, and it stayed wrong after the release shipped.** If you are on 10.0.0-alpha.11 or later you already have this fix; if you are on an earlier version, upgrading resolves it. Not yet confirmed by reading the published assembly — verify against the package you actually restore. The correct behaviour for a capability that cannot produce a value
is to refuse rather than to return a confident empty one, so both members now throw rather than hand
back a fabricated package or an empty export. **Plan for that:** code that calls either one today should not
be written to depend on a successful return.

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

**Is it fixed?** **Fixed in `10.0.0-alpha.12`.** **This entry previously said the fix was "not yet in any released version" — that was wrong, and it stayed wrong after the release shipped.** If you are on 10.0.0-alpha.12 or later you already have this fix; if you are on an earlier version, upgrading resolves it. Not yet confirmed by reading the published assembly — verify against the package you actually restore. The remedy was a contract change rather than a patch — the result
had to be able to say "not assessed" before anything downstream could stop treating it as a failure, and it
now can. `10.0.0-alpha.11` and `10.0.0-alpha.10` behave as described above; `10.0.0-alpha.12` and later
report the control as not assessed rather than as a failure against you.

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
`Excalibur.Compliance` is `3.0.0-alpha.157`, published 2026-04-20. Every release up to and including `10.0.0-alpha.11` contains
them, and `10.0.0-alpha.12` is the first that does not, so there is no lower bound worth stating but
there is now an upgrade that removes the behaviour. If you have this package at all, this entry applies to you.

**Is it fixed?** **Fixed in `10.0.0-alpha.12`.** **This entry previously said the fix was "not yet in any released version" — that was wrong, and it stayed wrong after the release shipped.** If you are on 10.0.0-alpha.12 or later you already have this fix; if you are on an earlier version, upgrading resolves it. Not yet confirmed by reading the published assembly — verify against the package you actually restore. It will also say which controls the fix covers. Until this entry
names a release, there is no upgrade that changes what your report says.

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
only one you can act on.

**Is it fixed?** **Fixed in `10.0.0-alpha.11`.** **This entry previously said the fix was "not yet in any released version" — that was wrong, and it stayed wrong after the release shipped.** If you are on 10.0.0-alpha.11 or later you already have this fix; if you are on an earlier version, upgrading resolves it. Not yet confirmed by reading the published assembly — verify against the package you actually restore.

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
pre-release of it up to and including `10.0.0-alpha.10` contains it. **The repair is not in `10.0.0-alpha.10` or any earlier
release** — but it IS in `10.0.0-alpha.11` and later, so upgrading resolves this. **This paragraph
previously said the repair was on the main branch and unreleased; that stopped being true when
`10.0.0-alpha.11` shipped.**

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

**Is it fixed?** **Fixed in `10.0.0-alpha.11`.** **This entry previously said the fix was "not yet in any released version" — that was wrong, and it stayed wrong after the release shipped.** If you are on 10.0.0-alpha.11 or later you already have this fix; if you are on an earlier version, upgrading resolves it. Not yet confirmed by reading the published assembly — verify against the package you actually restore. The sender now forwards the properties you set. Until this entry
names a release, the workaround above is what you have.

---

### Middleware is resolved once, from the root container, so Development hosts cannot dispatch and Production hosts share one instance across every request

**What you see.** Two different things, and which one you get depends on a setting you probably did not
choose. ASP.NET Core turns on the container's scope validation in the Development environment and leaves
it off everywhere else.

In **Development**, a host that registered middleware the documented way fails on its first dispatch:

```csharp
builder.Services.AddDispatch(dispatch => dispatch.UseMiddleware<MyMiddleware>());
```

```
InvalidOperationException: Pipeline 'Default' cannot be built because 1 required middleware
could not be resolved:
  - MyMiddleware: Cannot resolve scoped service 'MyMiddleware' from root provider.
```

A host that selected a pipeline profile rather than registering middleware itself sees no exception at
all. The profile's stages are dropped one by one, each with a warning, and the host dispatches through an
empty pipeline. Nothing in the response says a stage is missing.

In **Production** neither happens, because scope validation is off there. The pipeline composes, every
stage runs, and **one instance of each middleware serves every request for the life of the process**.

**Why.** The composed pipeline is registered as a singleton, and the container hands a singleton factory
the root provider by definition. Every middleware this framework seats is registered with a scoped
lifetime — the four stages of the default profile, and everything `UseMiddleware<T>()` registers for you.
Resolving them while composing the pipeline therefore resolves scoped services from the root. Scope
validation refuses that outright; without it the resolution succeeds and the instances are held forever,
together with whatever they were constructed with — an outbox store, a unit of work, a tenant context.

**What it costs you.** The Development failure is loud and will not reach your users. The Production
behaviour is the serious one: a service you registered per-request is shared across every request that
passes through that middleware, with no error and nothing in your telemetry to distinguish it. If any of
your middleware constructor-injects a scoped service, treat it as shared process-wide on any released
version.

**The workaround, if you cannot upgrade.** Register your middleware yourself, as a singleton, through the
documented enumerable registration, and do not call `UseMiddleware<T>()` for it:

```csharp
services.TryAddEnumerable(ServiceDescriptor.Singleton<IDispatchMiddleware, MyMiddleware>());
```

The composed pipeline reads that registration, and a singleton resolved from the root is not a captive
dependency. It only holds if the middleware genuinely has no per-request state of its own: take your
scoped services from `context.RequestServices` inside `InvokeAsync` rather than through the constructor,
and keep per-request values on `IMessageContext` rather than in fields. That is the guidance for writing
middleware in any case.

The same applies to the default profile's own stages. Registering those four types as singletons **before**
you call `AddDispatch()` takes effect, because the framework's own registration yields to one you already
made.

Turning scope validation off in Development stops the exception and leaves the shared instance in place.
It makes the symptom go away and the defect remain, so we do not suggest it.

**Which versions are affected.** The singleton pipeline registration is present in the shipped assemblies
of `10.0.0-alpha.10` and `10.0.0-alpha.11`, which are the two we opened, and the code carrying it predates
every `10.0.0-alpha`. Your own `UseMiddleware<T>()` registrations reach it on any of them. The default
profile's four stages became scoped registrations between `alpha.10` and `alpha.11`, so an empty pipeline
where a profile was selected is something we can place only at `alpha.11` and later. **We have not
established the first affected version** for the `UseMiddleware<T>()` half and would rather say so than
name one we have not opened; if you are on a version not listed above, assume you are affected.

**Is it fixed?** **Fixed in `10.0.0-alpha.12`.** **This entry previously said the fix was "not yet in any released version" — that was wrong, and it stayed wrong after the release shipped.** If you are on 10.0.0-alpha.12 or later you already have this fix; if you are on an earlier version, upgrading resolves it. Not yet confirmed by reading the published assembly — verify against the package you actually restore. That is measured by opening the shipped
assemblies: the types the fix introduces are absent from both `alpha.10` and `alpha.11`. The fix keeps the
composed pipeline a singleton and stops it holding the middleware: a stage the container would serve fresh
per scope is resolved per dispatch instead, from your request scope where you have one and from a scope
created for that dispatch and disposed after it where you do not.

## Earlier resolutions

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

- [Known issues](known-issues.md) — what is still wrong.
