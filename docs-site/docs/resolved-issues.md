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

### A saga discarded every event of a type after the first, unless you set a step id

:::danger Upgrading to this release is not transparent — read the migration note below
The fix changes the format of an identifier that is already persisted inside saved saga state. A saga
mid-flight across the upgrade may run one step a second time, and a saga that already lost events is
not repaired by upgrading.
:::

**What the defect was.** A saga remembers which events it has already processed so a redelivery does not
run a step twice. The identifier it remembered was built from the event's type name, the saga's id, and
`ISagaEvent.StepId`. `StepId` is optional, and when it was not set the identifier was the same for
**every event of that type reaching that saga** — so the first was processed and every later one was
treated as a redelivery of the first and dropped. Each drop was reported as `skipped duplicate event`,
which is the opposite of what happened: it was a distinct event that was never handled, and nothing else
recorded it.

**Were you affected?** You were affected if a saga of yours handles the same event type more than once
during its lifetime and those events carry no `StepId` — an order saga receiving `ShipmentDispatched` for
three parcels, an approval saga receiving `ApprovalGranted` from several approvers, a batch saga
receiving one `ItemCompleted` per item. You were not affected if every event type reaches a given saga at
most once, or if you already set a distinct `StepId` per delivery.

**What it exposed.** The saga advanced on the first event and then stopped responding to the rest while
appearing healthy: no exception, no failed status, and a log line affirmatively reporting correct
deduplication. Downstream systems waiting on the saga's later steps waited indefinitely. Because the
discarded events never reached a handler, **handler idempotency did not help** — idempotency protects
against a step running twice, and this was a step running zero times.

**Which versions were affected.** Every 10.x release before this one, and the older 3.x line broadly. The
identifier had been derived this way since before the 10.x line began.

**Is it fixed?** **Yes, in the release this page accompanies.** Replay identity is the envelope message id
that the delivery carries — a property of the *delivery*, not of the payload — rather than a value
composed from the event type, the saga id and `StepId`. `StepId` no longer affects deduplication at all,
and two distinct deliveries cannot share an identifier.

**A delivery that carries no message identity is processed and not deduplicated**, rather than being
mistaken for a duplicate. Every send the framework makes stamps an identity, so this applies only to
inbound messages from a producer that sends none. The saga is then at-least-once for that type and
**your handlers must be idempotent.** The first such delivery of each event type logs a warning naming
the type, and every one increments the `excalibur.saga.undeduplicable_deliveries` counter, so a producer
that never sends an identity appears in your metrics rather than only in the first log line.

#### What you need to know when you upgrade

- **A saga that is mid-flight across the upgrade may run one step a second time.** Identifiers written by
  the old scheme cannot be produced again, so they are left in place and ignored rather than translated;
  a delivery the saga had already recorded is therefore no longer recognised. This is the at-least-once
  direction that handler idempotency covers. The alternative — honouring the old identifiers — would have
  kept dropping events for every upgraded saga indefinitely, which nothing covers. To avoid the
  re-execution entirely, drain your sagas before upgrading.
- **There is no migration to run, and we do not supply one.** The old identifiers are not merely stale,
  they are **ambiguous**: a key that two distinct events collapsed onto records that *an* event of that
  type was processed and cannot say which one, or how many were dropped behind it. Nothing can decompose
  it, so nothing rewrites it. Upgrading is a package upgrade; there is no step to perform.
- **A saga that already lost an event is not repaired by upgrading, and no migration can repair it.**
  Recovery is a replay from your stream, and it is evidence-based because we hold no record of what was
  discarded. Three things to know before you start:
  - **Handler idempotency does not help here, and this is the half people get wrong.** Idempotency
    protects a step from running *twice*. These steps ran *zero* times — the event never reached a
    handler — so there is nothing to de-duplicate and nothing to retry. Only re-delivering the event
    fixes it.
  - **Do not use the saga's stored `ProcessedEventIds` as your evidence.** A collided identifier appears
    exactly once no matter how many events collapsed onto it, so the set's size is not the number of
    deliveries the saga processed. That is the ambiguity above; the set will look consistent and tell you
    nothing.
  - **Determine it from your own stream against your own saga state.** Look for a saga that received more
    than one event of a single type while that type carried no `StepId`, and compare the events your
    stream holds for it against the progress the saga's state actually records. The difference is what
    was dropped. Re-deliver those events on this release — each delivery carries its own identity, so two
    events of one type are two events; on an earlier release a re-delivery would have been discarded
    exactly as the original was. A saga too far diverged to replay into has to be advanced by hand.

---

### A GDPR erasure could leave the subject's full state readable through a snapshot, and a retry reported it already erased

:::danger If you ran an erasure on an earlier release, re-run it
An erasure that was interrupted part-way could not be finished by retrying it, and the retry reported
success. Re-running the same erasure on this release completes it. There is nothing else to do and no
migration to apply.
:::

**Were you affected?** You were affected if **all three** hold. Stating it as "erasure was broken" would
be wrong and would send unaffected consumers on an audit they do not need:

1. you registered a **snapshot store** — with no snapshot store there was no second copy and the defect
   could not arise; **and**
2. an erasure **ran** for a subject whose aggregate had a snapshot; **and**
3. that erasure **did not complete cleanly** — the result reported partial, or the process ended
   mid-erasure. A clean run deleted the snapshot in the same pass and left nothing behind.

**Condition 3 is the one to check.** The erasure result reports partial when any step failed, so your own
erasure records are the evidence. If every erasure you ran reported success, this did not affect you.

**What it exposed.** The events were tombstoned but the snapshot survived, and a surviving snapshot is
readable: a snapshot at count `N` over events `0..N-1` makes the aggregate load fetch zero event rows, so
the tombstone is never seen and the snapshot is returned as the aggregate. An ordinary load of that
aggregate handed back the **full pre-erasure state**. Nothing reported it — the erasure's own
already-erased check saw the tombstoned events and agreed the subject was erased.

**And a retry could not fix it.** The erasure skipped any aggregate whose events were already tombstoned,
so every subsequent attempt declared that aggregate done without re-attempting the snapshot. The
incomplete state was permanent for as long as the snapshot lived.

**Is it fixed?** **Yes, in the release this page accompanies.** The snapshot is destroyed **before** the
events, so a fault between the two stores now leaves a not-yet-erased aggregate with no snapshot — a
slower rehydrate from its own events and nothing else — instead of erased events behind a readable copy.
Nothing spans those two stores transactionally, so one of the two orders had to survive a fault between
them, and this is the one that does. The already-erased check now reports rather than skips, and every
step is idempotent, so **re-running an erasure resumes it**.

**What you need to do.** Re-run any erasure that did not report a clean completion. It is safe to re-run
one that did: re-erasing already-tombstoned events is a no-op that reports zero, so it will not
double-count in the certificate. If you want to confirm an individual subject, load the aggregate — on
this release an erased aggregate no longer returns its pre-erasure state.

### An access review reported every unreviewed grant revoked while revoking none of them

**What the defect was.** Access-review campaigns that revoke unreviewed grants, and entitlement reports,
both asked the grant store for "every grant" — but expressed *every* as an empty string, and every shipped
store treated an empty string as a value to match rather than as "no filter". No grant has an empty tenant,
so nothing matched. The revoke step then finished a loop over zero grants and reported that all of them
were revoked, because it started from "all revoked" and only a failure changed that. A campaign scoped to a
user or a tenant was affected in the same way: that scope's filter value was discarded before the query.
Entitlement reports came back empty.

**A second, narrower problem on the SQL Server and PostgreSQL stores.** If you called the public
grant-query API yourself — `IGrantQueryStore.GetMatchingGrantsAsync` or `IGrantRepository.MatchingAsync` —
with real ids, an id containing `_` or `%` was matched as a wildcard pattern, so it could return grants
belonging to **other users or other tenants**. The framework's own authorization decisions did not go
through this query — they match ids exactly — so no access decision the framework made was affected. The
exposure was to code of yours that used the query API and trusted its result.

**Were you affected?** You were affected if you used the governance package (`Excalibur.A3.Governance`) for
access-review campaigns that revoke unreviewed grants, or for entitlement reports, on **any** grant store.

**What it exposed.** A control that reported success without acting. If you used access reviews to remove
access nobody re-approved, the step that does it reported that everything was revoked when nothing was: the
access stayed in place, and your records said it was gone.

**Which versions were affected.** Every release built from source on or after 26 March 2026. We are not
publishing a list of versions, for the reason given on [Known issues](known-issues.md): a partial list
would tell some affected readers they are safe.

**Is it fixed?** **Yes, in the release this page accompanies.** `IGrantQueryStore.GetMatchingGrantsAsync`
matches under **ordinal equality** — case-sensitive, with no wildcards and no collation folding. **`null`,
and only `null`, means "do not filter on this field"**; an empty or whitespace filter is **refused** rather
than read as "all", because a value that means "match everything" is exactly what a caller passes by
accident. Reading across tenants is a separate member, `GetMatchingGrantsAcrossTenantsAsync`, which says so
in its name.

And the revoke step no longer writes a campaign's completion receipt unless the revocation actually
happened. A missing grant store, a store that cannot be queried, or a grant whose deletion exhausted its
retries each leave the campaign open and logged rather than marked expired — because the campaign record is
the audit evidence a later reader treats as proof the review completed.

#### What you need to know when you upgrade

- **Past campaigns are still unverified, and upgrading does not re-run them.** Access you believed an
  access review removed may still be in place. Re-check grants for any campaign whose result you relied on,
  and correct any record that states the access was removed. This is the half no code change reaches.
- **An empty or whitespace filter is now refused rather than silently matching nothing.** If your own code
  passed `""` to `GetMatchingGrantsAsync` meaning "all", that call now throws `ArgumentException`. Pass
  `null` for that field instead. The change is deliberate: the old spelling was indistinguishable from an
  accident, and it is the accident that produced this defect.
- **You no longer need to filter grant-query results by exact id yourself** when your ids can contain `_`
  or `%`. Matching is ordinal and wildcard-free.

### One tenant's activity-group grant could authorize another tenant's activities

:::danger Upgrading changes an authorization outcome — read this before you upgrade
Every other entry on these pages describes something you *lose*. This one **granted** access that should
have been denied, across a tenant boundary. Upgrading removes that access, so a tenant whose grant was
resolving another tenant's activity group stops being authorized for it. If anything in your estate has
come to depend on that resolution, it begins to be denied on this release.
:::

**What the defect was.** The in-memory activity-group store built **one catalogue of activity groups for
the whole process**, keyed on the group name alone with no tenant anywhere in the key, and the
authorization check looked groups up in it by bare name. **Every tenant's groups were visible to every
tenant.** Two consequences, and the first needed nothing unusual to happen:

- **A grant in one tenant could name a group belonging only to another tenant**, and it resolved — the
  group was in the same catalogue. No coincidence of naming was required.
- **Where two tenants used the same group name**, their activities were merged into a single list, so a
  grant in either one authorized the union.

A type check in the authorization path looked like it would stop this and did not: the value involved is a
list of strings, which satisfies that check, so it passed for this store while genuinely excluding the
database-backed ones.

The tenant was missing from the *write* key as well as the read: entries were stored under
group-name-and-activity alone, and the write did not overwrite an existing one. **So if creating an
activity group ever returned `0` for a group-and-activity pair another tenant already used, that tenant's
entry was silently not stored** — the same missing tenant, visible as a return value rather than as an
authorization decision.

**Were you affected? You were affected by default, and configuring a database is what protected you.**
That inverts the usual shape of these lists, so do not scan it for an opt-in you made:

| what you did | affected? |
|---|---|
| called `AddExcaliburA3()` and registered **no** activity-group store | **yes — this was the default** |
| registered the SQL Server or PostgreSQL store | no |

`AddExcaliburA3()` registers the in-memory activity-group store with `TryAddSingleton`, so it was what you
got unless you replaced it. There was no setting that turned this on or off — the exposure followed from
which store was resolved.

**Do not read "in-memory" as "development only."** The store's own documentation intends it for
*"development, testing, and standalone scenarios where no persistent store is configured."* **Standalone is
not development.** A single-process deployment serving real tenants was squarely the affected case, and so
was a test environment holding real tenant data.

**What it exposed.** Authorization across a tenant boundary — access granted that should have been denied.
Treat it as an authorization failure, not a reliability one.

**Which versions were affected.** **Confirmed present in `10.0.0-alpha.11`**: that package's shipped
assembly contains the pre-fix types and members and none of the symbols introduced by the correction, and
we identified which variant that is by matching our source at the commit before the fix. The default
registration of the in-memory store is present at that same commit, so "affected by default" describes the
shipped shape and not only our source. **We are not publishing a list of every affected version** — we have
measured one, and naming versions we have not opened would be guesswork.

**Is it fixed?** **Yes, in the release this page accompanies.** Every key on the in-memory store now
carries the tenant, composed through `SegmentedKey` so that no two (tenant, name) pairs can produce the
same key — a group name is unique only *within* a tenant. Every read filters on the entry's tenant as well
as composing it into the key, and the two do different jobs: without the filter every caller receives every
tenant's catalogue, and without the composition two tenants' same-named groups fuse into one entry.
`FindActivityGroupsAsync` requires a tenant and returns only that tenant's groups; `ActivityGroupExistsAsync`
matches both terms rather than the name alone; and `CreateActivityGroupAsync` refuses a blank tenant rather
than writing an entry no tenant-scoped read can address.

#### What you need to know when you upgrade

- **Confirm it in one test, and use two tenants.** Under tenant A, create an activity group containing an
  activity that tenant B has no legitimate access to. Then, **as tenant B**, grant an activity-group grant
  naming tenant A's group. On this release tenant B is **not** authorized for that activity. The obvious
  single-tenant version of this test passes on every version and proves nothing; so does a test in which
  each tenant only ever names its *own* groups, because nothing asks the catalogue for a foreign name.
- **"Has this already been exploited?" is a question upgrading does not answer**, and neither can the
  framework: an authorization that should have been denied and was granted is indistinguishable, in any log
  we write, from one that was legitimately granted. **There is no signal to search for.** If you need
  assurance for the period before you upgraded, compare the activities your grants actually resolved
  against the activities your tenants' groups were supposed to contain — from your own records, not ours.
- **A grant that named a foreign group stops resolving.** That is the fix working. If a tenant loses an
  authorization on upgrade, the grant was reaching another tenant's group and the denial is correct.
- **Registering a tenant-scoping store remains supported and is no longer a workaround.** Either the SQL
  Server or PostgreSQL store, or your own `IActivityGroupStore` through
  `IA3Builder.UseActivityGroupStore<TStore>()`, which replaces the default outright:

  ```csharp
  builder.UseActivityGroupStore<MyTenantScopedActivityGroupStore>();
  ```

### Refreshing activity groups or their grants deleted every tenant's, not just yours

**What the defect was.** The store operations that cleared activity groups and activity-group grants **took
no tenant parameter at all** — `DeleteAllActivityGroupsAsync` received only a cancellation token. The
built-in sync used that deliberately: it fetched the whole catalogue from your configured endpoint, cleared
the table, and re-created every row with the tenant it came back with. That is coherent only while the
source really does return every tenant, and three things could break it:

- **An empty response that arrived successfully.** All three refresh operations deleted unconditionally and
  then repopulated only behind a `if (… .Length > 0)` guard. A `200 OK` whose body deserialized to `null`
  or an empty list **ran the delete and skipped the repopulate** — the table was emptied and nothing was
  written back.
- **One malformed row in an otherwise healthy payload.** The repopulate validated each row's tenant *as it
  wrote it*, inside the loop, after the delete had already committed. A single row carrying no tenant threw
  part-way through and left the store partly wiped.
- **Calling the operation yourself**, or a sync source returning one tenant's catalogue rather than the
  estate's.

**Three operations carried this shape, not one**, and the mechanism was identical in each:
`SyncActivityGroupsAsync` (every group, every tenant), `SyncActivityGroupGrantsAsync` (one user, every
tenant), and `SyncAllActivityGroupGrantsAsync` (**every grant, every user, every tenant**).

**Were you affected?** You were affected if you refreshed activity groups in a multi-tenant process, on
**any** store — unlike the entry above, this was not specific to the in-memory store, because the missing
tenant was in the operation's signature rather than in one store's keying. **The framework never invoked
any of the three**; they are public API you opt into by wiring a sync path. If you never wired one, this
did not describe you.

**What it exposed. On a database-backed store this was a committed `DELETE` against a shared table with no
`WHERE` clause — not lost process state.** Restarting the process did not bring it back. Tenants other than
the one you refreshed lost their activity groups and any authorization that depended on them.

**Which way it failed.** In steady state it **denied**: the decision path is fail-closed, so an emptied
store removed authorizations and could never manufacture one. But cache invalidation was the **last** step
of all three operations, so a refresh that threw before reaching it left cached decisions serving — and the
cache uses a sliding expiration renewed by each read, so on a busy deployment **a revocation the refresh
was performing could fail to take effect indefinitely.**

**Is it fixed?** **Yes, in the release this page accompanies**, and in four separate places:

- **Validation precedes destruction.** Every row's tenant is resolved, and the whole catalogue constructed
  and validated, *before* anything is deleted. A payload one store would refuse is refused for every store
  while the existing catalogue is still intact — so an unappliable payload can no longer empty the estate
  and then throw with nothing restored.
- **An empty payload is refused on the two estate-wide syncs.** `SyncActivityGroupsAsync` and
  `SyncAllActivityGroupGrantsAsync` raise rather than applying a response that carried no rows, because an
  empty response cannot be distinguished from an upstream filter, scope or schema change. Nothing is
  deleted and the existing set stays intact.
- **A body that did not deserialize is a failure, never an empty set.** The per-user sync deliberately
  *does* accept an empty snapshot — a user who now holds no activity-group grants is an ordinary state and
  every grant of theirs must be revoked — so "the authority returned nothing" and "the fetch did not
  produce a list" are told apart rather than collapsed.
- **The unscoped delete is gone.** `DeleteAllActivityGroupsAsync` is replaced by an atomic
  `ReplaceAllActivityGroupsAsync`, which exchanges the catalogue under its own lock and reports the tenants
  it replaced, and by a tenant-scoped `DeleteActivityGroupsForTenantAsync`. Cache invalidation now covers
  both sides of the exchange — the tenants that *had* groups and the tenants that *have* them — so a tenant
  the payload dropped no longer keeps a cached catalogue conferring activities that no longer exist.

#### What you need to know when you upgrade

- **`DeleteAllActivityGroupsAsync` no longer exists.** If you called it, your code will **not compile**
  against this release. Replace it with `DeleteActivityGroupsForTenantAsync(tenantId, …)` if you meant one
  tenant, or with `ReplaceAllActivityGroupsAsync(catalogue, …)` if you meant a wholesale exchange — the
  second is atomic, so it does not leave a window in which the catalogue is empty.
- **An estate-wide sync still replaces the estate, and this is the residual to size yourself.** A refusal
  covers an *empty* payload, not a *narrower* one: if your sync source returns one tenant's catalogue
  rather than the whole estate's, the other tenants' groups are still replaced away. That is what a full
  refresh means. Point these operations at a source that genuinely returns every tenant, or use the
  tenant-scoped delete and write that tenant's rows yourself.
- **Keep an independent copy of your activity groups and grants.** The authority that returns a bad
  response is the same authority you would restore from, so re-running the sync is not a recovery plan.
- **Data already lost is not restored by upgrading.** Re-seed every tenant, not only the one you intended
  to refresh.

### An authorization grant whose tenant, type or qualifier contained `:` or `%` was silently never applied

**What the defect was.** Grant keys are composed by escaping each term and then joining them: `%` and `:`
are replaced with `%25` and `%3A`, so that a term containing either cannot be mistaken for the separator.
**One of the parsers did not reverse that escaping**, so a key written from escaped terms was read back as
different terms than it was written with.

**What you saw.** Nothing. The grant was written without error, the call returned, and nothing was logged.
The grant was simply never matched when it was read back, so the holder was refused exactly as though the
grant had never been issued. The effect was that the grant appeared **absent rather than wrong** — it
produced a refusal, never a wrong allow, and never access for anyone else.

**Were you affected?** You were affected if any of your grant terms — the tenant id, the grant type, or the
qualifier — contained a colon or a percent sign. Identifiers issued by an external identity provider
commonly do: an OpenID Connect `sub` claim is an opaque string the provider chooses, and URN- and
URI-shaped values are ordinary rather than unusual.

**Is it fixed?** **Yes, in the release this page accompanies.** Decomposition is now the format owner's job
rather than each parser's: `SegmentedKey.Split` unescapes every segment it returns, and `GrantKey` and
`GrantScope` defer to it instead of splitting by hand. The same change closes two neighbouring defects that
hand-written parsers carried — a split that dropped empty segments, which shifted every later term left so
that a key whose user term was empty addressed a different record entirely; and a segment-limited split,
which collapsed a surplus separator into the final element instead of rejecting it, so a raw-separator
spelling could decode to a grant an escaped key already named. A key carrying the wrong number of segments
is now rejected rather than padded or truncated.

#### What you need to know when you upgrade

- **Nothing reaches grants you have already stored, and nothing needs to.** They were written correctly; it
  was the read that failed to match them. They resolve again as soon as you are on this release, with no
  migration and no re-issue.
- **A grant holder who was being refused starts being allowed.** That is the fix working — the grant was
  always valid. If you issued a replacement grant to work around the refusal, you now have two, and the
  duplicate is yours to remove.

### A category-scoped erasure erased everything, and reported success

**What the defect was.** You submitted an erasure naming specific data categories — `Scope =
ErasureScope.Selective`, with `DataCategories` listing what you meant to erase. The framework **required**
you to supply those categories, refusing the request without them. It then erased the data subject
entirely: their encryption key was destroyed, and every event of every aggregate your data-subject mapping
returned had its payload nulled and its type replaced with an erasure marker. The result reported success
and the certificate read `Completed`.

**Nothing was selective** — not by category, not by field, not by event type. Your categories were
validated, stored on the request, and carried onto the certificate. No erasure step ever read them.

**Were you affected?**

| what you did | affected? |
|---|---|
| submitted an erasure with `Scope = ErasureScope.Selective` | **yes — everything reachable through your mapping was erased** |
| submitted `User` or `Tenant` scope | not by this defect; those scopes mean what they say |
| never used the erasure feature | no |

**Our own documentation carried a worked example of the mode**, so a consumer following the compliance
guide would have arrived at exactly this configuration.

**What it exposed.** The categories you named were the ones you wanted gone. Everything you did *not* name
was erased too, and the destruction is irreversible: there is no recovery for a tombstoned event payload
and none for a destroyed key. If a mapped aggregate also held records you are obliged to retain — a
transaction, a warranty, an audit trail — those are gone, and the certificate says the erasure completed
correctly.

**Which versions were affected.** Every published 10.x version up to and including `10.0.0-alpha.13`.

**Is it fixed?** **Yes, in the release this page accompanies, and read what the refusal covers before you
rely on it.** The event-store contributor has exactly one action — tombstone every event of an aggregate —
so it cannot express a category restriction. Rather than tombstoning anyway or reporting a success it did
not earn, it now **refuses** a `Selective` request and returns a failure naming why. Because that is a
failure rather than a success, the coverage gate leaves the obligation outstanding and **the request cannot
report `Completed`** — an operator reading the result learns the named categories were not separately
honoured, instead of discovering it from a certificate that says otherwise.

**What the refusal does not withhold, stated plainly: the data subject's own encryption key is still
destroyed.** Key destruction is queued unconditionally and happens *before* any contributor runs, so by the
time the event-store pass declines, personal fields annotated for per-subject encryption are already
unrecoverable. Declining the event-store pass withholds *additional* destruction — the whole-aggregate
tombstoning — it does not withhold the erasure. **So `Selective` is still not selective in the
key-destruction dimension**, and nothing on this release makes it so.

#### What you need to know when you upgrade

- **A `Selective` erasure that used to report `Completed` now reports an outstanding obligation.** That is
  the fix. It is a behaviour change for any workflow that branched on the completion status, and the new
  status is the truthful one.
- **To erase at category granularity**, do one of the two things the refusal names: annotate those fields
  for per-subject encryption so key destruction reaches them, or register a contributor declaring
  event-store coverage that can honour the category restriction. Expressing the request as `User` scope
  remains correct when whole-subject erasure is what you intend.
- **Audit which aggregate types your `IAggregateDataSubjectMapping` returned**, for every `Selective`
  erasure you ran on an earlier version. That set is what was destroyed. There is no remedy for the data
  itself; what you can establish is the scope of the loss, and whether any of it was under a retention
  obligation. Upgrading does not repair it and no migration can.
- **Keep data you must retain in aggregates your mapping does not return**, which remains good practice
  independently of this defect.

### An erasure destroyed records the law required you to keep, and there was no way to stop it

:::note This was not previously listed on Known issues
Every other entry on this page was disclosed there first. This one was not — we identified it while
building the capability that fixes it. It is recorded here rather than left out, because the versions it
affects are versions you could have installed.
:::

**What the defect was.** Erasure tombstoned whole aggregates unconditionally. Every event of every
aggregate your `IAggregateDataSubjectMapping` returned had its payload nulled and its type replaced with
a reserved marker, and there was no mechanism — no option, no registration, no contributor hook — by
which a deployment could say *this aggregate type is one the law requires me to keep.*

**Where that lands is not an edge case.** Article 17(3) withholds the right to erasure to the extent
processing is necessary to comply with a legal obligation, and for many businesses that obligation covers
the customer's **identity**, not merely the transaction. A vehicle title transfer, a lien entry or a
safety recall notice is worthless without the buyer; a warranty cannot be honoured without knowing who
holds it. Erasing such a record is not compliance — it is a different breach, against a different statute,
and it is irreversible.

**Were you affected?**

| what you did | affected? |
|---|---|
| ran an erasure whose mapping returned an aggregate type you are obliged to retain | **yes — that record is destroyed, and there is no recovery** |
| ran an erasure whose mapping returned only personal-data aggregates, with transaction records modelled separately | not by this defect; that modelling was, and remains, the right shape |
| never used the erasure feature | no |

**The only workaround was to map fewer aggregates** and carry the obligation in your own retention and
redaction process — which our own documentation recommended, because it was the only honest advice
available. It works only while the personal data is separable from the record. Where the buyer's name is
*on* the invoice, it was a choice between destroying the record and not erasing at all.

**Which versions were affected.** Every published 10.x version up to and including `10.0.0-alpha.13`.

**Is it fixed?** **Yes, in the release this page accompanies.** A deployment can now declare that an
aggregate type is retained, and an erasure skips it:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Excalibur.Compliance;
using Excalibur.Dispatch;

services.AddErasureRetention(new ErasureRetention
{
    AggregateType = "SalesRecord",
    TenantId = TenantScope.UntenantedSentinel, // or the tenant whose obligation this is
    Basis = LegalHoldBasis.LegalObligation,
    Justification = "Vehicle sales records are kept for six years under the tax code's "
                  + "record-keeping requirement and for product-recall traceability.",
    RetentionPeriod = TimeSpan.FromDays(365 * 6),
});
```

The record survives whole and readable, and the erasure record names the retention, its basis, the
justification and the period — for every data subject appearing in that record, not only for the one the
obligation was written about. Full guidance is on
[GDPR Erasure](compliance/gdpr-erasure.md#when-the-law-requires-you-to-keep-the-record-declare-a-retention).

#### What you need to know when you upgrade

- **Nothing changes unless you declare a retention.** An aggregate type you do not name is erased exactly
  as before, so a deployment that declares none is unaffected by this fix.
- **Declare the retention before the data is written.** Naming a type moves which key its personal fields
  are encrypted under, and that is what keeps the record readable afterwards. Events written **before**
  the declaration were encrypted under the subject's own key — the one the erasure destroys — so they
  survive the tombstone with their personal fields no longer decryptable. **The framework ships no
  backfill for this, and you should not wait for one — but it is unbuilt rather than impossible.** The key
  that protects those older records is destroyed by an *erasure*, so a deployment declaring a retention
  today still holds it, and re-encrypting that data under the retention's handle before any erasure runs
  is a migration you can write. **Audit which of your newly retained types already hold data**, and decide
  per type whether to re-encrypt it or to accept that pre-declaration records will lose their personal
  fields at the first erasure.
- **The retained aggregate must carry its own copy of what it needs.** A sales record that reaches its
  buyer by following a reference into a customer aggregate breaks when that customer is erased.
- **This does not repair anything already destroyed.** A tombstoned payload has no recovery. What you can
  establish is the scope of the loss — audit which aggregate types your mapping returned for every erasure
  you have run — and whether any of it was under a retention obligation.
- **The unit is the aggregate type, whole. There is no field-level erasure inside a retained record.**
  If `SalesRecord` is retained, every data subject named on one keeps their personal data there for the
  declared period; you cannot erase the buyer's name from an otherwise-retained invoice. That limit is
  deliberate — a partly-erased record is a mutated record with no evidentiary value, which was the reason
  for keeping it — and it is explained in full under
  [the case that remains unsupported](compliance/gdpr-erasure.md#the-case-that-remains-unsupported).
- **Releasing the retention when it expires is yours.** The declared period is recorded and reported; it
  is not a timer, because when a particular record's obligation lapses depends on facts the framework does
  not hold.

### A legal hold that retained only some categories was treated as no hold at all

**What the defect was.** The framework decided whether to proceed by reading a single derived condition
meaning *"there are active holds **and** none of them exempts a category."* A hold that **did** name exempt
categories therefore made that condition false, which is indistinguishable from having no holds at all. The
exemption list was never consulted again. Your hold service reported that specific categories must be
retained; the erasure ran anyway, as though no hold existed — the data subject's key was destroyed and the
mapped aggregates were tombstoned — and reported success.

**Were you affected? Only if you implement `ILegalHoldService` yourself.** Our own hold services never
produce a partial hold, so a deployment using the shipped implementation was never exposed. It is recorded
here because we **advertised** the mode: `ILegalHoldService` is public, and the factory for building a
partial result is public and documented as meaning holds that block specific categories while erasure
proceeds for the rest.

**What it exposed.** The categories you had asserted a legal basis to retain were destroyed, and the result
reported success.

**Is it fixed?** **Yes, in the release this page accompanies.** A partially-blocking hold now stops the
erasure **before** the subject's key is destroyed, at both points it can be caught: a request that cannot be
honoured is declined when it is *made*, rather than accepted and failed later, and the execute path refuses
again. The refusal names the retained categories, records the request as blocked by legal hold, and states
why neither step can exclude a retained category — key destruction is scoped to the subject and not to a
category, and the event-store pass is whole-aggregate. **Nothing is erased.**

Refusing is the conservative direction: withholding an erasure is recoverable by releasing or narrowing the
hold, and destroying held records is not. The hold carries basis, case reference and description, which is
what makes the refusal auditable.

#### What you need to know when you upgrade

- **An erasure that used to run against a partial hold is now blocked.** If your workflow expected it to
  proceed, it stops. Release or narrow the hold, or erase the subject where the retained categories are
  stored separately.
- **You no longer need the workaround of returning a fully blocking result** whenever any category must be
  retained. A partial result is now honoured as a partial result.
- **Erasures that already ran against a partial hold destroyed the retained categories, and upgrading does
  not repair them.** Your hold records — basis, case reference, description — establish what should have
  been kept.

### A GDPR erasure certificate could report Completed when the framework could not check its own coverage

**What the defect was.** Before completing an erasure the framework lists the annotated data categories it
could not cover. On a host where the scan cannot see every assembly carrying `[PersonalData]`, that list
came back **empty for exactly the same reason full coverage produces an empty list** — there was no state
meaning *"the scan was incomplete."* An empty list was read as *"nothing uncovered,"* so absence of evidence
was rendered as evidence of coverage, on the document that attests a legal obligation was discharged.

**What you saw.** A signed erasure certificate saying `Completed`, for a data subject whose personal data
was never enumerated. **There was nothing on the certificate that distinguished it from a genuine one** —
not a warning, not a partial status, not an empty field you could notice. The operator could not tell, the
data subject could not tell, and neither could an auditor reading the certificate later.

**Were you affected? The discriminator was the scan's reach, not your publish mode.**

| your deployment | affected? |
|---|---|
| the erasure host published **trimmed** or with **Native AOT** | **yes — confirmed** |
| an ordinary host where every assembly carrying `[PersonalData]` is loaded and visible | not by this mechanism |
| a host that loads assemblies lazily, by plugin, or on demand | **we did not measure this** |

We deliberately did not tell you a JIT host was categorically safe. The property that matters is whether
the scan reaches every assembly carrying the annotation. Trimming and Native AOT are the confirmed way to
break that; they may not be the only way.

**Which versions were affected.** **Six of the eight published `10.0.0-alpha.*` versions are confirmed** —
`alpha.5`, `alpha.7`, `alpha.8`, `alpha.9`, `alpha.10` and `alpha.11` — by reading the shipped documentation
surface of each package. **The remaining two we did not examine, and that means unmeasured, not clean.**

**Is it fixed?** **Yes, in the release this page accompanies.** The annotation scan now reports whether its
coverage was *established*, and completion requires it. An unestablished scan is its own state, distinct
from a scan that ran and found nothing, so the two observations are no longer the same. When the scan could
not be completed, the erasure is **not** reported `Completed`, and the error says why in terms an operator
can act on: the annotated categories are a lower bound, a category the scan never observed is
indistinguishable from one that is covered, and this is expected on a trimmed or ahead-of-time host where
whole-domain reflection cannot see what the trimmer removed.

**The analyzer-suppression justification compiled into the shipped assemblies now states the same thing.**
On earlier versions two of those justifications asserted that a category the scan misses *fails the coverage
gate rather than completing silently* — the opposite of what the code did — and that text is exactly what an
assessor reads when asking why a warning was suppressed in a personal-data path. On this release the code
does what the justification says.

#### What you need to know when you upgrade

- **An erasure on a trimmed or AOT host now fails instead of issuing a certificate.** That is the fix, and
  it is a behaviour change you will notice immediately if that is how you publish. The two remedies the
  error names are: run the erasure on a host where reflection over the domain model is available — this is
  an administrative compliance path, not a hot path — or restrict the host to crypto-shred-only erasure,
  where coverage is established by key destruction rather than by locating annotated data.
- **`KeyShredOnlyErasure` bypasses this gate, deliberately, and it is not a way to dodge the refusal.** It
  changes what erasure *means* for your deployment rather than restoring the guarantee. Adopting it to get
  past a refusal would leave you attesting to a different thing than you think.
- **Certificates you issued from a trimmed or AOT host do not establish what they claim, and upgrading does
  not revisit them.** Re-run those erasures on this release, and treat the earlier certificates as
  unverified rather than as evidence.
- **We have not proved the repair on a trimmed or AOT publish.** The arm that would demonstrate it requires
  such a publish and does not exist, so the behaviour on that configuration is reasoned from the code rather
  than executed. Verify it in your own environment if you publish that way.

### An erasure certificate's signature authenticated who it was about, not what it said

**What the defect was.** The signed input was three identity fields — the request identifier, the hashed
data subject identifier, and the completion timestamp. **The certificate's claims were not part of it.**
Alter any claim — the exemptions, the record counts, the store kinds, the description of what was erased —
and the signed input was unchanged, so the signature still matched. The document's identity was
authenticated; its content was not.

**The certificate's version marker was outside the signature too.** The document carries a version field
identifying the signing scheme that produced it, and that field was not part of the signed input either, so
it could be altered like any other claim — which means **you cannot use it to establish which scheme signed
a certificate issued on an earlier version.**

**And the claims themselves could be unsubstantiated independently of any tampering.** On the normal path,
where the certificate is written as the erasure completes, the `Verified` flag was set true unconditionally
— nothing was checked — and the verification methods it listed were copied from your configuration rather
than from anything that ran. On the fallback path, where a certificate is rebuilt from stored status, the
flag read true whenever the deleted-key count was zero, which is the same value produced by a successful
erasure of a subject with no keys and by an erasure that deleted nothing.

**Which versions were affected.** **Every published version containing the erasure-certificate feature.**
The feature was introduced in November 2025, and since then the signature covered only the request
identity, subject hash and completion time. There was never a correct version to roll back to, and we
deliberately did not publish a list: most published versions have no tag we can resolve, so any list would
assert safety for versions nobody checked.

**In earlier versions the "signature" was not keyed at all, so anyone could produce one.**

| certificates issued by a version from | what the signature is |
|---|---|
| November 2025 to February 2026 | an **unkeyed SHA-256 hash**, always. It carries no key, so anyone can compute a matching value for any certificate, including one they wrote themselves |
| February 2026 to July 2026 | HMAC-SHA256 **when a signing key was configured**. With no key it silently fell back to the same unkeyed hash |
| July 2026 onward | HMAC-SHA256; issuing a certificate fails when no signing key is configured |

**To tell whether your own older certificates carry an unkeyed hash**, search the logs of the host that
issued them for the warning `No HMAC signing key configured — falling back to unsigned hash for
certificate`. Each occurrence names the request whose certificate was issued without a key. Certificates
issued before February 2026 carry an unkeyed hash whether or not you configured a key, and no warning was
logged for them. A certificate with an unkeyed hash establishes nothing about who issued it.

**Why you would not have found this through your usual channels.** This is our own code. There is no CVE
and no dependency advisory, so a scanner would not surface it and a supply-chain review would not either.

**Is it fixed?** **Yes, in the release this page accompanies, and in three parts.**

- **The signature covers the whole payload.** It is HMAC-SHA256 over a canonical serialization of the
  entire certificate payload rather than an enumerated set of fields, so a claim added later is covered
  automatically instead of being forgotten. Serialization goes through the source-generated context, so the
  property order is fixed at compile time and the payload carries only scalars and ordered lists — a
  non-deterministic signed input would fail verification intermittently, which would be a worse defect than
  the one it replaces.
- **The signature carries a scheme prefix**, so a verifier can select the canonical form without first
  parsing a payload it has not authenticated. A bare tag forces a verifier to guess which scheme produced
  it, and with older certificates sitting in consumer databases the only way to guess is to try each one —
  which is a downgrade path by another name.
- **The framework now ships a verifier.** `ErasureCertificateVerifier` checks the signature and reports
  `SignatureMismatch` as a distinct finding. On earlier versions nothing in the framework checked the
  signature at all: `ErasureVerificationService` verifies that *erasure occurred* and never touched the
  signature, and its name invited the opposite reading.

**And the claims are substantiated rather than asserted.** On the normal path `Verified` states the one
claim the execution can positively establish, and `Verified` and the listed methods are derived from a
single value so they cannot disagree — a certificate reading "Verified = true, methods = none" is now
inexpressible rather than merely unlikely. On the reconstructed path `Verified` is **false**
unconditionally, because that path has nothing to substantiate the erasure with.

#### What you need to know when you upgrade

- **Certificates issued before this release will not verify against the new scheme.** This is the migration
  consequence and it is not avoidable: the signed input changed, so an old signature cannot match a new
  canonical form. Nothing about the signed-input format was ever published, so no documented contract is
  broken — but if you reverse-engineered the format, it changes under you.
- **Requesting a certificate again does not reissue it.** Once a certificate has been issued for a request,
  asking for it again returns the stored document unchanged. Upgrading does not re-sign anything you already
  hold, and a certificate you hold is exactly as good, or as limited, as it was when it was issued.
- **If you have already supplied certificates as evidence**, the exposure is that nothing on them proves
  the claims are as issued. Whether that warrants telling anyone is your call to make with your own counsel;
  it is recorded here so the decision is yours to make. It reaches beyond GDPR work — the certificate is
  documented in our GDPR, HIPAA and SOC 2 material.
- **Keep protecting the certificates at rest and in transit.** Restrict write access to the store and keep
  your own record of what was issued. For documents issued on this release the signature detects alteration;
  for everything you already hold, comparison against your own record is still the only thing that does.

### The outbox fencing contract we published told implementers to build the problem it exists to prevent

**Who this affected.** Only you, if you wrote your own outbox store against `IFencedOutboxStore`. **If you
use the outbox stores that ship with the framework, you did not implement this contract — we did**, and the
shipped stores hold the fence once per claim scope rather than once per row, which is what the instruction
should have said. This entry is about **the instruction we published to implementers**, not a general
statement that those stores are free of every fencing concern. **In particular it does not clear the
failure path**, which is a separate matter affecting every outbox user regardless of whose store you run:
see [the outbox failure report is required to check an owner it is never
given](known-issues.md#the-outbox-failure-report-is-required-to-check-an-owner-it-is-never-given).

**What the defect was.** The documentation shipped in the package said, of the claim operation:

> *"Implementations MUST claim only rows whose stored fencing high-water mark is less than or equal to
> `fencingToken` … A presented token below the stored high-water mark indicates a superseded (stale)
> leader; the store MUST exclude those rows from the claim."*

Read literally — and it is a `MUST`, so it was meant to be read literally — that put the high-water mark
**on each row**. It is the wrong place for it.

**Why that was harmful rather than merely imprecise.** A mark stored per row is only advanced on rows that
have actually been claimed. So after a handover, a superseded leader presenting its old token is refused on
the rows the new leader has already taken, and **admitted on every row the new leader has not yet reached**
— which, at the moment of handover, is most of them. The superseded leader then claims and drains them
alongside the current one. That is not a race needing an unusual interleaving; it is what an ordinary
failover does.

**What you saw.** Nothing, until a leader handover. Then two instances drained the same outbox at the same
time and your consumers received the same messages twice. There was no error, because both instances
believed they held the claim and, under the contract as written, both were right.

**Which versions were affected — assume every 10.x pre-release you hold.** We read the documentation file
inside the published packages available to us, and **every 10.x package we could open carries the wrong
instruction**, with none free of it. The interface has existed since 10 July 2026, and the earliest 10.x
package we hold was published a month later, so there is no version in our reach that predates the problem.
**We deliberately did not name a first-affected version:** we cannot open the releases we do not hold, and a
version's absence from our cache is not evidence that it is clean.

**The 3.x line is genuinely not affected**, and that one is a measurement rather than a bound: the interface
does not exist in it at all. We checked its shipped documentation directly.

**Is it fixed?** **Yes, in the release this page accompanies.** The shipped contract text now states the
placement explicitly rather than leaving it to be inferred: **the high-water mark is stored once per outbox
*scope*, not per message row.** An implementation must claim rows only when the presented token is greater
than or equal to the scope's stored mark, and must advance that mark to the maximum of its current value and
the presented token **within the same atomic action as the claim**. A presented token below the mark
identifies a superseded tenure: the claim yields zero rows and must not throw, because this is a set-based
operation rather than an error.

**And it names the failure mode rather than only the rule.** The contract now says in as many words that the
mark answers which *tenure* may act on the scope, that it records nothing about an individual row, and that
**a per-row column does not implement this contract** — because a per-row mark cannot see a tenure that
touched a different row in the same scope, so it admits a superseded leader on every row the current leader
has not yet reached. It also records that such a store **would pass a single-row test**, which is why the
distinction is stated in the contract rather than left to the reader.

**The second half is stated too.** A fencing token identifies a *tenure*, not a *claim*, so it cannot
discriminate two claim cycles of the same tenure: a delayed report from an earlier cycle of the
still-current leader presents a valid token and is indistinguishable from a current one. The contract says
so, so a store that fences correctly per scope is not thereby told it has closed everything.

#### What you need to know when you upgrade

- **Open your implementation and answer one question**, rather than testing against a running system — a
  handover that happens to be clean proves nothing. **When you compare the presented fencing token, what row
  are you reading the stored mark from?** If the answer is "the message row I am about to claim", you built
  what the old text specified and you have the duplicate-drain problem; move the mark to a single row keyed
  by the outbox it guards. If it is "a single row for the whole outbox", you did not follow the old
  instruction and your store already matches the corrected contract.
- **Fencing per scope alone is necessary and not sufficient.** Closing the claim-cycle gap needs an
  identifier for the individual claim, carried alongside the token and checked with it. A store that fixes
  only the scope placement is correct about leaders and still silent about claims.
- **The documentation inside packages you have already installed cannot be recalled.** If you hold an
  earlier package, the wrong `MUST` is in its XML documentation file and will keep appearing in your IDE
  until you upgrade. The corrected text ships with this release.

### A projection could report a position it did not hold, so a read model silently double-counted or omitted events

**What the defect was.** A projection row carries a position saying which prefix of the event stream its
state contains. A positioned write names the prefix it is claiming — and when the row being advanced carried
no usable position, the writer could not know what the stored state already held, so instead of refusing it
**adopted** the row and stamped its own batch's position onto it. That claim was a guess, and when it was
wrong it was wrong in the direction that loses data.

**What you saw.** Nothing. That is the defect. The row was **indistinguishable from a correctly advanced
one**: no error, no warning, no failed write, nothing in a log. A read model built this way could
double-count some events and omit others while reporting a position that said otherwise.

**How a row came to carry no usable position — four ways, and on most providers you needed do nothing.**

1. **You use a document store** — Cosmos DB, DynamoDB, Firestore, MongoDB, Elasticsearch or OpenSearch —
   **and had projections before you upgraded.** A document written before the position field existed carries
   no such field, which reads as *a complete fold whose position is unknown*, and that is exactly what got
   adopted. **No action on your part was required to be in this case.**
2. **You ran an erasure recovery.** On any provider, recovering a fully erased aggregate replayed its
   events, skipped every tombstone, derived no position, and wrote the rebuilt row unnumbered. The documented
   remedy for clearing an erased subject from a read model was itself how the row was produced.
3. **You use a relational store and added the position column with a default in the negative range.** SQL
   Server and PostgreSQL projection tables are yours to create, so you choose the column's default on
   upgrading; a negative default made every pre-existing row read the same way as case 1.
4. **You called `UpsertUnnumberedAsync` yourself.** On any provider. That method is documented for a caller
   holding a complete fold whose position it does not know, and a row written through it is precisely the
   shape that got adopted.

**Were you affected?**

| what applied to you | affected? |
| --- | --- |
| `10.0.0-alpha.13` with a **document store** and projections predating the upgrade | **yes — no action on your part required** |
| `10.0.0-alpha.13` and you ran an erasure recovery, any provider | **yes** |
| `10.0.0-alpha.13` with a **relational store**, position column added with a default in the negative range | **yes** |
| `10.0.0-alpha.13` and you called `UpsertUnnumberedAsync` yourself, any provider | **yes** |
| `10.0.0-alpha.13`, relational store, no negative default, no erasure recovery, never called `UpsertUnnumberedAsync` | not by this mechanism |
| `10.0.0-alpha.12` or earlier | **no** — those versions track no projection position at all |

**Which versions were affected.** `10.0.0-alpha.13` introduced positioned projections and is the **only
published version that contains them**. We establish this from the release tag and the published version
list, not from inspecting the compiled assemblies.

**Is it fixed?** **Yes, in the release this page accompanies.** A row carrying no usable position is now
**refused** rather than adopted. The write reports `ProjectionAdvanceOutcome.Unplaceable`, which states what
the write did — nothing was written, and the row carries no number to advance from. It is not a retry:
re-reading yields the same refusal. To learn what the row itself holds, read its position; the read reports
that atomically and the write result deliberately does not.

#### What you need to know when you upgrade

**Your events are intact** on every affected version — nothing in the event store was lost or altered. What
was wrong is only the read model's claim about what it contains. Upgrading stops new false claims being
written; **it does not correct a row that already carries one.** Rows written on `10.0.0-alpha.13` need to be
deleted and rebuilt, and that is your step.

**Step 0 — check this first.** A rebuild **refuses rather than reporting completion** when the global-stream
read stops at a gap, naming the position it stopped at. Two different causes: if that position belongs to an
append still in flight, **re-run the rebuild** and it clears on its own; if it is permanently absent — a
store archived by a version that deleted event rows — **apply the archival gap backfill for your provider
first** (see [Global-stream reads stop at gaps](./migration/global-stream-reads-stop-at-gaps.md)). A rebuild
that refuses has changed nothing, so nothing is lost by finding out this way.

1. Register the rebuild service if you have not already: `services.AddProjectionRebuild()`.
2. Delete the affected rows with `IProjectionStore.DeleteAsync`.
3. Rebuild with `IProjectionRebuildService.RebuildAsync<TProjection>()`, which replays the event stream and
   writes each row's state together with the position it folded.

**Delete before rebuilding.** A rebuild writes only when the position it computed is ahead of the one
stored, so a row already carrying a position at or above the stream's head can be left untouched by a
rebuild that appears to succeed. Deleting first removes the stored claim, so the rebuild writes
unconditionally.

If you cannot identify which projections are affected, rebuild the ones whose aggregates you have erased,
and any that existed before you added the position column.

**Two calls to avoid while repairing.** Do not use `IProjectionRecovery.ReapplyAsync` for this — on
`10.0.0-alpha.13` recovering a fully erased aggregate is one of the two ways the false position was produced
in the first place. And do not repair by calling `UpsertAtPositionAsync` yourself with a null expected
position — on that version it is the call that adopts. On this release the same call refuses instead, which
is the fix, but a refusal repairs nothing either: the repair is a rebuild.

### A projection refold could retry forever against a row that would never change

**What the defect was.** The store refused a write because the row carried no usable position, which is
**terminal**: re-reading yields the same refusal. Translating that refusal into a refold result, three
providers fell through to a catch-all that reported it as **superseded** — which means *re-read and retry* —
and the caller did, against a row that could not change.

**What you saw.** A refold that never completed. No error, no exception, no failed write. If your retry was
uncapped, that was a hot loop in production. If it was capped, you got a repair that reported failure every
time and never said why.

**Were you affected?**

| what applied to you | affected? |
| --- | --- |
| you call `RefoldAtPositionAsync` on **Cosmos DB, Elasticsearch or OpenSearch** against a row carrying no usable position | **yes** |
| you call it on another provider | not by this mechanism |
| you do not call `RefoldAtPositionAsync` | no |

A row came to carry no usable position in the four ways listed under *[A projection could report a position
it did not hold](#a-projection-could-report-a-position-it-did-not-hold-so-a-read-model-silently-double-counted-or-omitted-events)*
— and on the six document stores no action on your part was required to be in that state.

**Which versions were affected.** `10.0.0-alpha.13` only, which is the one published version containing
positioned projections.

**Is it fixed?** **Yes, in the release this page accompanies.** All three providers — Cosmos DB,
Elasticsearch and OpenSearch — now translate an unplaceable write into `ProjectionRefoldOutcome.RequiresRebuild`
rather than letting it fall through to superseded. A terminal refusal reports that it requires a rebuild, so
a caller that retries on superseded stops retrying, and a caller that caps its retries is told what the
refusal actually was.

#### What you need to know when you upgrade

- **The remedy is unchanged: rebuild.** The result now names it rather than leaving you to infer it. Follow
  the rebuild procedure under *[A projection could report a position it did not
  hold](#a-projection-could-report-a-position-it-did-not-hold-so-a-read-model-silently-double-counted-or-omitted-events)*,
  including its precondition about gaps in the global stream.
- **If you added a retry cap as a workaround, you can keep it.** It is sound practice independently of this
  defect, and on this release the terminal outcome is distinguishable so the cap is no longer the only thing
  stopping the loop.
- **Nothing is lost by a refused refold**: it wrote nothing, on this release and on `10.0.0-alpha.13` alike.

### An append that was committed could be reported as a conflict, and retrying it wrote the event twice

**What the defect was.** A store classified an append as having lost a concurrency race by reading the
stream's *version*, and a moved version does not say **who** moved it. "Another writer took my version" and
"I took it myself and lost the acknowledgement" move the stream identically, so a classifier reading the
version alone had to get one of those two cases wrong — and it got the second one wrong.

On SQL Server the route was the exception path: the store caught database exceptions raised by the commit
itself and treated them as evidence the append had written nothing, on the reasoning that a failed
transaction was rolled back. That reasoning does not hold when **the server committed successfully and the
acknowledgement was lost** — a dropped connection, a command timeout, or a pause longer than the client
timeout, all routine against a managed cloud database. The rollback did nothing, because the transaction
was already committed; the store re-read the version, found it had moved because *this* writer moved it,
and returned a conflict for an append that was durably on disk.

On Firestore the route was the pre-check, and it needed no concurrency at all — one process, one lost
packet. An append at expected version 0 read the next slot as absent, wrote, and committed; the
acknowledgement was lost, the driver re-ran the callback, and the second attempt found the slot occupied by
**its own event** and reported a conflict. The check asked *is the slot occupied* when the question is *is
it occupied by someone else* — the document already carried its event identifier and nothing read it.

**This was the more common shape, and it is why the fix moved.** A retry of an append whose acknowledgement
was lost does not reach the write at all: its own committed write is what moved the version, so it is
turned away at the version pre-check, before any insert is attempted and before any exception can be
raised. A probe sitting on the exception path — which is where the first repair put it on several stores —
never fired.

**What it exposed.** The documented response to a concurrency conflict is reload-and-retry. Retrying
appended the same business event a second time **at the next version**, where no uniqueness key can catch
it because the versions differ. Your aggregate's history then contained the same business event twice, no
read distinguished it from two genuine events, and every replay applied it twice — so any state derived by
folding events (balances, counters, totals, state machines) was silently wrong and stayed wrong for the
life of the stream. **Reporting a conflict in that state was not the cautious answer; it was a duplicate
generator.**

**Were you affected?** You were affected if a commit acknowledgement could be lost between your application
and your event store — any deployment where the two are separated by a network, and especially a managed
database that pauses or fails over. A purely local database made it unlikely but not impossible. **This
was not confined to one provider**, which is what an earlier revision of this entry got wrong: the
misclassification was found and closed on **all nine event stores**, each by its own route.

**Which versions were affected.** Every published 10.x version before `10.0.0-alpha.13`. The SQL Server
handling had been in place since late July 2026, before the first 10.x release. We have not assessed the
older 3.x line — do not read that as clean.

**Is there a fixed version?** **`10.0.0-alpha.13`, on every provider.** Before classifying anything, the
store asks which records are present **by identity** — and it asks at both moments that are reachable:
before the write, where the version is found already moved because a previous invocation committed; and
after a failed write, where the pre-check matched and could not have seen anything, so this invocation
committed and lost its acknowledgement.

The guarantee it now keeps, in falsifiable terms: **an append is reported successful if and only if its
events are durably present.** Append one event to a new stream, then present the same event, with the same
identifier, at the same expected version a second time. The second call reports **success**, and the stream
then holds **exactly one** event.

**Every store reaches it, and two do so differently.** SQL Server, PostgreSQL, Oracle, MongoDB, SQLite,
Cosmos DB and Firestore read back by event identifier; Firestore's costs no extra round trip, because the
slot it compares was already read for the conflict check. DynamoDB uses the provider's own idempotency
primitive — a deterministic `ClientRequestToken` on `TransactWriteItems`, so a retried transaction is
recognised by the service rather than reconstructed afterwards. **Redis reaches it differently, and its
window changed after `10.0.0-alpha.13`:** a stream offers no keyed read by event identifier, so instead of
probing, its Lua script records event identifiers beside the version counter atomically with the append.
In `10.0.0-alpha.13` it recorded **one**, which covered a retry of the most recent append to a stream but
not one arriving after another writer had appended — see *Redis reported a retry as a conflict once another
writer had appended* below. From the release this page accompanies it records a bounded set, so a retry is
recognised however many writers intervened, up to a configurable limit.

**This supersedes two things this entry previously said.** It said a correct fix required an idempotency
constraint on the events table, and therefore a schema change that could not reach a deployed database
through a package upgrade — that was wrong; the event ids were already stored, so **no schema change is
needed** and the fix reaches you through the package. And it said the repair was SQL Server only, with
PostgreSQL and Oracle carrying no equivalent — also wrong, and wrong in the direction that leaves a reader
on another provider believing they are still exposed.

**It also corrects the evidence claim.** This entry previously said no test exercised the reconciliation
and that removing it left our suite green. `10.0.0-alpha.13` carries
`EventStoreConformanceTestKit.ReAppendingTheSameEventsReportsSuccessAndDoesNotDuplicate`, wired by every
provider suite and run against real infrastructure rather than a stand-in. It needs no fault injection:
re-presenting the same events at the same expected version is exactly what a driver retry after a lost
acknowledgement looks like from outside the store. It asserts both halves — success reported, and exactly
one event in the stream.

**How we confirmed this.** We read the release tag for `10.0.0-alpha.13` and checked that every commit
carrying the nine-store repair, the opt-out removal and the conformance arm is an ancestor of it. That is a
reading of our repository at the release tag, **not** of the published assembly. Verify against the package
you actually restore.

#### What you need to know when you upgrade

- **A caller retrying the same business command must present the same event identifier.** This is an
  obligation the fix creates and cannot enforce for you. An event identifier names a **business event, not
  an attempt** — it is what lets the store recognise its own earlier write. **A caller that mints a fresh
  identifier per attempt does not get this guarantee:** the store cannot recognise the retry, reports a
  conflict, and you are back to needing idempotency downstream. That degrades to at-least-once rather than
  becoming unsafe, but it is a change you make in your own code, not one you get by upgrading.
- **Do not reuse an identifier for a different business event.** This is the one direction the store cannot
  defend against: it recognises the identifier, reports success, and **the second event is never written.**
- **Two options are removed, and code that sets them will not compile.** `UseTransactionalBatch` and
  `UseTransactionalWrite` — the opt-outs that permitted a non-atomic append — are gone rather than taught
  to report honestly, because the probe's soundness rests on the batch committing atomically and their only
  effect was to permit a torn prefix by configuration. The path is now chosen by **count**: one event is a
  single write, two to a hundred commit as one transactional batch, and **an append of more than a hundred
  events is refused with `EventBatchTooLargeException` before anything is written**, rather than committed
  as a sequence of batches that could leave a torn prefix behind. Split a larger append into batches of at
  most a hundred yourself.
- **You no longer need the workaround this entry used to give you** — re-reading the stream before
  re-appending to check whether your event is already present. The store does that check, by identity,
  as long as you present the same identifier.
- **Duplicates already written are not repaired by upgrading.** Nothing detects or removes a business event
  that was appended twice at two versions on an earlier release. If you retried appends on a conflict, look
  for repeated event identifiers within a stream; that is the signature, and it is the only one, because no
  read distinguishes the duplicate from a genuine event.
- **One property your own code must not break.** The probe reads committed state outside the write
  transaction, which is sound only because the store is append-only: once a row carrying an event
  identifier exists in a stream, it exists in every later state. Deleting a row is safe, and rewriting one
  while preserving its identifier is safe. **Reassigning a different identifier to an existing row is
  not** — the probe would then fail to recognise a write that did happen, and the duplicate comes back. If
  you implement your own erasure, retention trimming or stream compaction, preserve or remove event
  identifiers; never reassign them.

---

### Redis reported a retry as a conflict once another writer had appended, and the documented response then duplicated the event

**Were you affected?** Only on **Redis**, and only if **more than one writer appends to the same stream**.
A single-writer stream never reaches this. Every other event store recognised the retry correctly.

**What the defect was.** Recovering from a lost acknowledgement means re-presenting the same append. Every
store answers *did my events land?* — but a Redis stream offers no keyed read by event identifier, so
instead of probing, its script recorded the batch's first identifier beside the version counter. It recorded
**one**. A retry that arrived after another writer had appended no longer matched it, so it was reported as a
concurrency conflict — and the response we ourselves document for a conflict is reload-and-retry, which
appends the same business event again at the next version. The stream uniqueness key does not catch it,
because the version differs. The duplicate is permanent and every replay applies it twice.

**A second, narrower case.** An append whose events carried **blank** identifiers recorded a blank marker,
and a second writer also using blank identifiers matched it — so two genuinely conflicting appends
recognised each other and one conflict was silently swallowed. Blank identifiers are now neither recorded
nor matched, so that pair conflicts as it should.

**Is it fixed?** **Yes, in the release this page accompanies.** The single marker becomes a bounded set
keyed by identity, maintained inside the same script and therefore still atomic with the append. A retry is
recognised however many writers appended in between, up to `RetryRecognitionWindow` on the Redis event-store
options (default 64). Past that bound the retry is reported as a conflict — the same answer a caller who
mints a fresh identifier per attempt receives — so the guarantee is **bounded by count rather than
unbounded**, and it is stated that way rather than implied.

**Which versions were affected.** `10.0.0-alpha.13` and earlier.

**What you need to do.** Upgrade. Two things are worth knowing beyond that:

- **Duplicates already written are not repaired by upgrading, and nothing detects them for you.** The only
  signature is a repeated event identifier within a stream. If you ran Redis with concurrent writers to one
  stream and saw retries, that is where to look.
- **The obligation the fix rests on is yours:** a caller retrying the same business command must present the
  **same** event identifier. One that mints a fresh identifier per attempt is indistinguishable from a new
  event and gets at-least-once delivery — on every store, not only Redis.

**A note for existing Redis deployments.** The recognition key changed shape. A store upgraded in place over
streams written by an earlier release drops the older marker on first contact with that stream rather than
failing, so no migration step is required of you.

---

### A legal hold placed after an erasure request was recorded was never seen, and the data was destroyed

**What you saw.** A legal hold created for a data subject whose erasure request already existed did not
stop that erasure. The keys were destroyed, irreversibly, and the request reported completion. A hold
created *before* the request was always honoured, which is why this was easy to miss: the behaviour was
correct in the case most people test.

**Why.** `CheckHoldsAsync` hashes the data-subject identifier it is given, and holds are stored under that
hash. Once an erasure request is recorded, the only identifier the erasure path retains is the hash — the
raw value is deliberately not kept, because keeping it would defeat the erasure. Every check made while
the erasure ran therefore passed an already-hashed value to a method that hashed it again, and looked for
a double hash among holds stored under a single one. The hash is not idempotent, so the lookup could never
match and reported *no holds* rather than failing.

**Fixed.** `ILegalHoldService` now also exposes `CheckHoldsByHashAsync`, which takes the stored hash and
does not re-hash it; the hash depth is in the method name so a caller cannot pick the wrong one silently.
Both members share a single query path, so a hold found by one is found by the other. See [What's
New](./whats-new.md#before-you-upgrade) for the migration if you implement or call that interface.

**Separately, the hold is now re-checked before every key destruction** rather than once per run. The
earlier check ran immediately after the erasure claimed the request, minutes before the first irreversible
destruction, with the whole key-discovery pass in between. A hold arriving in that window now stops the
destruction at the next key, the request cannot be reported `Completed`, and the number of keys already
destroyed is recorded. **One key-wide window remains and cannot be closed**: the hold lives in this
framework's store while the destruction happens at an external key manager that cannot roll back. If a
hold may be placed against a subject whose erasure is already running, treat the recorded
already-destroyed count as authoritative — that data is gone.

### A request holding two erasure certificates could not retrieve either of them, permanently

**What you saw.** `GetCertificateAsync` for a request threw `InvalidOperationException` on every call, for
that request, forever. The certificates were intact in storage; the lookup was the thing that failed.

**Why.** The certificates table keys uniqueness on the certificate id and indexes the request id
*non-uniquely*, while both SQL providers read the per-request certificate with a single-row query. A
request can legitimately hold two certificates — the partial-completion path issues one and the
completion path issues another — and the second one made the single-row read ambiguous.

**Fixed.** The per-request read is now total and ordered: it resolves to the current certificate, newest
first with a tiebreak that makes the order deterministic across providers. Both documents remain
retrievable by their own certificate id. A unique constraint was considered and rejected — it would have
forbidden the second *document* rather than the ambiguity, and both are evidence an auditor may be
entitled to.

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
