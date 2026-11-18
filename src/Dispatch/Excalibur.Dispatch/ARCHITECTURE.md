# Dispatch pipeline â€” architecture and guarantees

## Guarantee

**Stated so it can be falsified.**

> **Stage decides across stages. You decide within a stage.**

A middleware declaring a lower `DispatchMiddlewareStage` executes before one declaring a higher stage,
**regardless of the order you added them**. Within one stage, middleware execute in the order you added
them. A middleware declaring no stage is treated as `End`.

**Both halves matter and the second is the one that surprises people.** Across stages, registration
order is explicitly **not** preserved â€” that is the feature, not an accident. Stating only "registration
order is preserved" would describe a contract stronger than the one you get.

**Why the split.** `Use()`-order-is-the-order ties two decisions together: *what runs* and *when it runs
relative to everything else*. It then hands both to the caller, who has the information for only one of
them. The middleware author knows which phase their work belongs to; the application author knows which
middleware they want. Stage ordering gives each decision to whoever holds the facts.

---

## Guarantees, and what actually enforces each

| # | Guarantee | Seam | RED-detecting arm |
|---|---|---|---|
| 1 | Two middleware with equal effective stage execute in the order they were added | `MiddlewareChainBuilder.cs:152` â€” `while (j >= 0 && (middlewares[j].Stage ?? End) > currentStage)`. The **strict `>`** means equal keys never swap, so the insertion sort is stable by construction | `MiddlewareChainBuilderShould.ChainExecutor_WithMiddleware_ExecutesChainInOrder`, `DispatchMiddlewareInvokerShould.InvokeAsync_WithMultipleMiddleware_ExecutesInOrder`. **Mutant: `>` â†’ `>=`** reverses equal runs and reddens both |
| 2 | A lower stage executes before a higher one, regardless of registration order | `Delivery/Pipeline/MiddlewareChainBuilder.cs:144-160` â€” `SortByStageInPlace`, a stable insertion sort keyed on `Stage ?? End` | `MiddlewareChainBuilderShould.ChainExecutor_RunsALowerStageFirst_WhateverOrderTheyWereAddedIn` (five stages added in exactly reverse order) and `â€¦RunsAnUnstagedMiddlewareLast_EvenWhenItWasAddedFirst`. **Mutant: make `SortByStageInPlace` a no-op** â€” both go RED |
| 3 | A pipeline in which an `IRequiresTenantContext` middleware would run before the last `IEstablishesTenantContext` middleware is **refused when the resolved middleware list is published**, with `InvalidOperationException` naming both concrete types, rather than starting and silently observing no tenant | `Delivery/Pipeline/TenantOrderingRule.cs`, called at `PipelineBuilder.cs:397` as the expression assigned to the resolved-middleware field, so the list cannot be published unverified. Keyed on capability markers read at TYPE level through `MiddlewareIdentity.TypeOf`, so a consumer gets the guarantee by implementing the interface and keeps it when their middleware is registered `Scoped` or wrapped | `TenantContextOrderingThroughRealContainerShould` (4 arms, through a real container) and `TenantContextOrderingShould` (5 unit arms). **Mutant: read the capability off the entry (`m is TCapability`) instead of off the type it stands for** â€” the two scoped-establisher arms go RED, and all 5 unit arms stay GREEN |
| 4 | `AddDispatch()` seats **no** middleware. A host that configures nothing dispatches and does nothing else | `DefaultPipelineProfiles.CreateDefaultProfile()` declares an empty list | `DefaultPipelineProfilesShould.CreateDefaultProfileThatSeatsNoMiddleware` asserts `MiddlewareEntries.ShouldBeEmpty`, paired with `â€¦CreateStrictProfileWithSecurityMiddleware` as its liveness twin so "empty" cannot be satisfied by a profile builder that seats nothing at all |

### Guarantee 2 is now bound on the executing path â€” and the measurement that got it there

**Until these arms existed, deleting the entire stage sort left every test in the file green.** That is
measured, not inferred: with `SortByStageInPlace` stubbed to return immediately, the suite reported
**22 passed, 2 failed** â€” and the 2 were the arms added for this guarantee. All 21 pre-existing tests
passed with the sort disabled.

The cause was fixture shape rather than missing tests. Every fixture driving the live sort declared
`Stage => null`, so every element keyed to the same value and the comparison was never handed two
different keys. Tests elsewhere *do* assert cross-stage ordering, but they construct `DispatchPipeline`
directly â€” and no dispatched message reaches that object (see below), so those assertions were aimed at
a type the dispatcher does not use.

**The two guarantees are bound independently**, which is what stops one arm standing in for the other:

| mutant | guarantee 1 (order within a stage) | guarantee 2 (order across stages) |
|---|---|---|
| `SortByStageInPlace` returns immediately | green | **RED** (2 arms) |
| the strict `>` at `MiddlewareChainBuilder.cs:152` becomes `>=` | **RED** (3 arms) | green |

Each mutant reddens exactly one guarantee and leaves the other alone. A single mutant reddening
everything would only have shown that the chain runs at all.

---

## Which ordering a dispatched message actually experiences

**Not the one most of the tests assert against.**

```
PipelineBuilder.cs:397   _resolvedMiddleware = resolvedMiddleware      UNSORTED, UNVERIFIED
PipelineBuilder.cs:400   new DispatchPipeline(resolvedMiddleware, â€¦)   its ctor computes a SORTED,
                                                                       tenant-VERIFIED orderâ€¦
DispatchPipeline.cs:22   _ordered = TenantOrderingRule.OrderAndVerify(â€¦)  â€¦which never escapes the type
DispatchBuilder.cs:368   new DispatchMiddlewareInvoker(â€¦ResolvedMiddlewareâ€¦)   the UNSORTED list,
                                                                       re-sorted independently by
                                                                       MiddlewareChainBuilder
Dispatcher.cs:838, :929  middlewareInvoker.InvokeAsync(â€¦)              the only sites that run middleware
```

**So there are two sorts.** `DispatchPipeline`'s is computed and discarded; `MiddlewareChainBuilder`'s is
the one that runs. Both are stable and keyed on `Stage ?? End`, so they agree today â€” but only one of
them is reachable, and the tests are mostly aimed at the other.

**`DispatchPipeline` is no longer load-bearing for the tenant-ordering refusal, and that is the point.** The refusal used to run only because this object was still being constructed â€” a side effect of an allocation rather than a named act. It now runs at `PipelineBuilder.cs:397`, where the resolved list is published to both consumers, so a host that resolves only the invoker is covered and the check no longer depends on anyone wanting a pipeline object. `DispatchPipeline` still verifies, through the same rule, for the direct-construction path at `DispatchServiceCollectionExtensions.cs:103`.

---

## Evidence (conformance)

Every guarantee above is bound by an arm on the path a dispatched message actually takes, and each arm is
mutant-proven â€” a green suite is not evidence that an arm exists, so the mutant is named beside it.

| guarantee | arm | mutant that reddens it |
|---|---|---|
| 1 â€” order within a stage | `MiddlewareChainBuilderShould.ChainExecutor_WithMiddleware_ExecutesChainInOrder`, `â€¦ChainExecutor_KeepsRegistrationOrder_WithinASingleStage`, `DispatchMiddlewareInvokerShould.InvokeAsync_WithMultipleMiddleware_ExecutesInOrder` | the strict `>` in `SortByStageInPlace` becomes `>=`, so the insertion sort swaps equal keys â€” **3 arms RED** |
| 2 â€” order across stages | `MiddlewareChainBuilderShould.ChainExecutor_RunsALowerStageFirst_WhateverOrderTheyWereAddedIn`, `â€¦ChainExecutor_RunsAnUnstagedMiddlewareLast_EvenWhenItWasAddedFirst` | `SortByStageInPlace` returns immediately, leaving registration order intact â€” **2 arms RED** |
| 3 â€” a tenant-ordering violation is refused | `TenantContextOrderingThroughRealContainerShould` â€” 4 arms through a REAL container, including one that resolves only the invoker and never constructs a pipeline; plus `TenantContextOrderingShould` â€” 5 unit arms | read the capability off the pipeline ENTRY (`m is TCapability`) rather than off the type it stands for â€” **2 arms RED**. The 5 unit arms stay GREEN under that mutant, which is why they were not sufficient on their own |
| 4 â€” `AddDispatch()` seats no middleware | `DefaultPipelineProfilesShould.CreateDefaultProfileThatSeatsNoMiddleware`, with `â€¦CreateStrictProfileWithSecurityMiddleware` as the liveness twin | add any entry to `CreateDefaultProfile` â€” the first arm goes RED while the twin stays green |

**The two mutants are disjoint, and that is the point.** Each reddens exactly one guarantee and leaves the
other green, so neither arm is standing in for the other. A single mutant that reddened everything would
only establish that the chain runs at all.

**What this does NOT cover**, stated so the table is not read as more than it is: these are
sampling-class arms (R2). They execute the orderings we thought to write down. Rungs R3 and R4 â€” a
property suite over generated registration orders and stage assignments, and a model of the ordering and
verification seam â€” remain UNVERIFIED for this subsystem.

## Message upcasting â€” version-chain coherence

The upcasting pipeline had no written specification until the design was reviewed adversarially. The
absence was prior to the defect below: nothing stated what the seam owed, so nothing could say it was
broken. The properties are recorded here first, then what enforces each.

**State.** A set of edges `(messageType m, from f, to t, transform)` with `f < t`. Both `latest(m)` --
the highest `t` registered for `m` -- and the chain between two versions are DERIVED from that set, not
stored. `Register` adds one edge or refuses; the four read operations derive from it.

| # | Guarantee (falsifiable) | Seam | RED-detecting arm |
|---|---|---|---|
| 5 | **Chain coherence.** A returned message is the ordered composition of a chain that was shortest in **one** edge-set and executed against **that same** edge-set. Never a target version chosen against one set with a chain resolved against another | `Versioning/UpcastingPipeline.cs` â€” every public operation takes its registry snapshot with a single `Volatile.Read` and passes the resolved per-type graph down. `Traverse` is `private static`, takes the graph as a parameter and has no `this`, so re-reading the field is **not available to it**. The violation is inexpressible rather than merely avoided | Bound structurally, not by an arm. See *the interleaving this closed* below â€” the previous design admitted it and no test detected it |
| 6 | **Registry reads do not acquire the registration lock.** Cache population may synchronize internally. The pipeline does not hold its registration or cache-population locks across consumer transformation callbacks. A message already at the latest version resolves no chain and acquires nothing | `Versioning/UpcastingPipeline.cs` â€” registered edges live in an immutable registry published by `Volatile.Write` under a registration-only lock; readers only ever read it. There is no invalidation sweep: a new registry replaces the previous one wholesale, so a stale chain cache cannot be swept incorrectly | `UpcastingTraversalPerformanceShould.Admit_a_registration_while_an_upcaster_callback_is_still_running` â€” blocks inside an upcaster, then registers from another thread under a bounded wait, and asserts the in-flight traversal still completes correctly. **Mutant: wrap the traversal in the registration lock** â€” that one arm goes RED on a timeout and **all 45 others stay green**, which is what establishes nothing else in the suite covers it |
| 7 | **Forward only, and a gap fails loud.** No edge with `f >= t` is accepted. A requested version with no chain throws naming the type and both versions; it never returns a half-upgraded message | `Versioning/UpcastingPipeline.cs` â€” the `f >= t` check precedes any mutation; the resolver returns `null` for "no chain" and an empty chain for "already there", so the two are distinguishable and the absent case throws | The registration and no-path arms in `UpcastingPipelineShould` |

### The interleaving guarantee 5 closed

The previous design observed the state **twice** per `Upcast` â€” once to choose the target version, once to
resolve the chain. Three published states are enough to break it, with single-edge registration and no
batch API:

```
E0 = {1->2, 2->3, 3->4}          latest = 4
A: reads latest(E0) = 4, target := 4                      [A preempted]
B: Register(4->5)  ->  E1, latest = 5
C: Register(1->4)  ->  E2, the shortest 1->4 is now the direct edge
A: resumes, resolves the chain against E2  ->  one direct edge
```

Ask which edge-set explains the outcome. `latest = 4` holds only at `E0`, and `E0` contains no `1->4`
edge; the set `{1->2, 2->3, 3->4, 1->4}` with `latest = 4` never existed, because `4->5` was published
first. **No sequential execution produces this result.** The consumer receives a message at version 4
while the latest is 5, with one transform applied instead of three â€” and nothing throws, nothing logs,
and nothing downstream can detect it. That silence, not the severity, is what places this seam in the
highest blast-radius class.

Taking the snapshot once removes it. The single read is the whole fix, and it is why `Traverse` takes the
graph as a parameter instead of reading the field itself.

### Consumer obligations specific to upcasting

- **An upcaster must be a pure function of its input.** The pipeline invokes it without holding its registration or cache-population locks; it may run
  on any thread, and a chain may be resolved once and applied many times.
- **An upcaster may register from inside itself.** The pipeline does not refuse registration merely
  because it occurs inside a callback. Invalid metadata, duplicate edges and disposal still reject it. The registration applies to subsequent calls; the traversal already running finishes
  against the edge-set it started with.
- **Register during startup where you can.** Registration is correct at any time, but a registration
  concurrent with dispatch means some in-flight messages resolve against the previous edge-set, which is
  permitted by guarantee 5 and is not an error.

### Known gaps â€” upcasting

- **Property-based and model-checked verification remain UNVERIFIED for this seam.** Guarantee 5 rests on
  a structural argument and an adversarial review, not on a checked model of the publication protocol,
  and guarantee 6 rests on one mutation-proven arm. The arms that exist sample the input space; they say
  nothing about the whole state space.
- **No arm detects a regression of guarantee 5.** It is enforced by the shape of the code â€” a static
  helper that cannot reach the field â€” so re-introducing a second read would require changing that
  signature. A reviewer should treat any change that gives `Traverse` access to the registry, or that
  reads the registry more than once in one operation, as reopening the defect above.
- **Each resolver memo admits only registered endpoint pairs.** Checking only the highest version
  did not bound caller-controlled negative or missing intermediate endpoints. The resolver now keys
  memos by immutable registration-list identity and rejects unregistered endpoints before caching.
  A memo contains at most `V²` pairs for its `V` registered vertices; this is not a fixed byte limit
  or a global bound on concurrently live memos. Invalidation can leave old memos alive until their
  readers finish. `UpgradePathSnapshotShould` tests cache population after arbitrary missing endpoints
  and caller labels, with gated generation and collection tests. The original implementation failed
  these tests; raw failures and fixes are retained under
  `artifacts/benchmarks/current-refresh-20261101/upcasting-red-dispatch-r1` and
  `upcasting-green-dispatch-r3`.
- **Type-associated state is weakly keyed, deliberately, and this is a host-compatibility property
  rather than a memory one.** The derived message-type names hang off a weak-keyed table. A static
  dictionary holding strong references to types would root their assemblies, so a collectible load
  context that registered an upcaster could never unload â€” which breaks plugin and some serverless
  hosts. Replacing the weak table with a dictionary to speed up lookup would trade a supported
  deployment scenario for a saving on the registration path, which is not a hot path.
- **Performance observations are scoped to their recorded workload and configuration.**
  Allocation reproducibility must be verified rather than assumed. The November 1
  upcasting observations followed a job change and have no matched pre-change run;
  they do not prove a speedup. See the current performance campaign evidence in
  `artifacts/benchmarks/current-refresh-20261101`.

## Consumer obligations

- **`AddDispatch()` seats no middleware.** Name what you want. All the built-in middleware remain
  registered, so naming any of them resolves without extra work.
- **Order within a stage is yours.** If two of your middleware must run in a particular order and share a
  stage, add them in that order.
- **Order across stages is not yours.** Declaring a stage is how you influence it.
- **A tenant-reading middleware should implement `IRequiresTenantContext`**, and one that establishes
  tenancy `IEstablishesTenantContext`. That is what makes guarantee 3 cover your middleware rather than
  only ours.

---

## Known gaps

- **The refusal fires at first resolve, not at host startup.** It runs inside a DI singleton factory, so a misconfiguration surfaces the first time the pipeline is resolved. If that first resolve happens while handling a message rather than during startup, the consumer meets it as a runtime fault on a live message. Forcing the resolve from a hosted service at startup would make it a startup failure; that is not done today.
- **The rule is evaluated over the FULL middleware set, while execution filters by message kind.** Filtering preserves relative order, but it can remove the establisher and keep the reader â€” leaving a reader that runs with nothing establishing tenancy for it, which the rule then treats as having no ordering relation to violate. Whether a reader with no establisher in the applicable subsequence should itself be refused is a contract question and is deliberately not decided.
- **`IDispatchPipeline` is public and states no contract.** A consumer may implement and register their own, and it will carry none of these guarantees. Nothing detects that, because the published abstraction never promised them.
- **Two implementations of one stable sort** (`DispatchPipeline.cs:52` LINQ, `MiddlewareChainBuilder.cs:144`
  insertion). They agree today. Nothing enforces that they continue to.
- **`DispatchOptions.EnablePipelineSynthesis` controls nothing.** The profile registry registers its
  profiles unconditionally, so the condition it guards is never reached.

---

## Rigor

> Blast radius: **catastrophic**. The discriminator is silence, not severity â€” a middleware that is
> silently skipped or reordered produces no error, and an authorization middleware that does not run
> looks exactly like one that ran and permitted.

- **R1** â€” compiler, nullable reference types, analyzers. Met.
- **R2** â€” this document plus a RED-detecting arm per guarantee. **Met.** Each of the four guarantees has an arm on the path a dispatched message takes, and each is mutant-proven to go RED independently.
- **R3** â€” property-based tests over registration orders and stage assignments. **UNVERIFIED**; none exist.
- **R4** â€” a model of the ordering and verification seam. **UNVERIFIED**.

The arms that exist are sampling-class. Do not describe this seam as covered.

## Profile materialization diagnostics

`PipelineBuilder.Build()` reports each Optional entry it cannot materialize with existing Warning
event10024, naming the actual selected profile, middleware type and reason. It emits once per unresolved
entry per Build call, not once per dispatch; registered/resolvable entries remain quiet. Required
entries still refuse construction. This does not auto-register dependencies or change ordering,
activation, middleware membership or scope ownership.

The private diagnostic profile follows `UseProfile` and resets on `Clear`; direct composition uses the
builder Name as fallback. Selecting another profile replaces prior entries; named pipeline configuration
is not automatic endpoint/message routing. `ProfileCriticalityReachabilityShould` distinguishes the
profile name from pipeline Name, missing/registered cases, two actual dispatches, another Build and
Required refusal. These are bounded composition observations, not universal activation-race coverage.

A successful build probe does not certify later factory results. Existing `ScopeResolvedMiddleware`
can still receive a null Optional factory during dispatch and continue, or a null Required one and
refuse. Build diagnostics do not certify that later dynamic boundary. See the
[profile diagnostic decision](../../../management/architecture/adr-188-pipeline-extensions-es-builder-parity.md#2026-11-16-decision-addendum-profile-skips-are-explicit-composition-diagnostics)
and [consumer guide](../../../docs-site/docs/pipeline/profiles.md).

## Owned JSON metadata composition

`SystemTextJsonSerializer.CreateWithMetadata(options)` uses the existing codec under one owned frozen
wire policy. It copies caller options and resolver/converter membership, composes consumer-first
metadata with existing Core/CloudEvent generated fallbacks, and validates finite promised root metadata
before returning. BCL checks refuse missing/incompatible metadata; no reflection resolver is installed.
Resolver/converter objects remain borrowed, not deep-cloned or disposed. Later changes to the original JsonSerializerOptions scalar values or
membership lists do not replace captured selections; borrowed resolver/converter objects still own
their internal state.

Advanced `Create(context)` keeps that context metadata's effective options; it is not rebound to a fresh
policy. Converter behavior and effective writer formatting, including indent/newline settings, remain
observable. Generic/runtime/buffer paths share the declared policy. Available CloudEvent root metadata
does not prove the SDK object's entire nested graph serializable; canonical CloudEvents formatting and
persisted `AotJsonEventSerializer` each retain their separate policy contracts.

The former two AOT codecs and four helper registrations are withdrawn. Register the actual codec through
the existing serialization builder or `AddPluggableSerializer`; duplicate IDs refuse. Registry/payload
selection does not implicitly promise direct ISerializer injection. Framing remains serializer-ID byte
plus actual codec bytes, and failures retain the existing SerializationException/inner-error contract.

`AotSerializationRegistrationContractShould` covers registration order/isolation, nested graph/direct
bytes, missing child, caller snapshot, formatting, ID conflict and malformed metadata. The scoped Native
JSON probe retains35 behaviors and adds4 owned-policy/framing checks; this is not whole-framework,
provider or transport Native certification or a new throughput/allocation guarantee. See the
[metadata composition decision](../../../management/architecture/ADR-062-Pluggable-Serialization-Architecture.md#2026-11-16-decision-addendum-one-owned-json-policy-not-a-second-codec-family)
and [consumer migration](../../../docs-site/docs/advanced/native-aot.md).

## Scheduler execution and clocks

Scheduled payloads use `DispatchJsonSerializer`, a message serializer distinct from `IEventSerializer`.
Registering an event-store context with `AddAotEventSerializer` does not supply scheduler payload
metadata. With JSON reflection disabled, compose the message serializer with an application context
that includes each scheduled payload type. A DI-owned factory may use its existing public constructor
and pass the registered `IPooledBufferService`; register it before the default `TryAdd` registration.
Do not supply a disposable singleton instance whose ownership remains external, merge canonical
serializer families, or enable reflection to hide missing metadata. The hosted recovery probe records
this setup correction in the [scheduler decision](../../../management/architecture/recurring-scheduler-model-consolidation.md#message-metadata-composition-addendum-2026-11-16).

The recurring contract is `IDispatchScheduler` and the actual selected schedule store. The unconsumed
`RecurringCronJob` model is withdrawn: its retry/statistics/window setters never configured this worker.
Existing cron/interval recurrence, explicit timezone/profile behavior and durable occurrence
admission/failure/quarantine/recovery remain unchanged. Host-requested
`SchedulerOptions.MaxAdmissionAttemptsPerOccurrence` defaults to5 and is captured per occurrence; it
counts admissions (including abandoned or ambiguously settled grants), not three handler retries or
a new per-job policy. Application catalog labels and cumulative display statistics remain application
data. See the [scheduler consolidation decision](../../../management/architecture/recurring-scheduler-model-consolidation.md)
and [migration/recovery guide](../../../docs-site/docs/core-concepts/configuration-advanced.md#recurring-job-model-migration).

`ScheduledMessageService` dispatches scheduled actions, events and documents through the
dispatcher; `ScheduledMessageOutcomeShould` exercises the document pipeline path.
Each admitted occurrence owns an asynchronous DI scope, supplied as the message context's
`RequestServices`. The scope remains alive through progress and the matching release
attempt, then is asynchronously disposed. Catch-up occurrences use distinct scopes.
`ScheduledOccurrenceScopeShould` checks lifetime and cleanup ordering, including faults
and cancellation. Asynchronous dependency disposal remains cooperative.
Its injected `TimeProvider` controls polling, signal-wait deadlines and finite
dispatch deadlines. Caller cancellation remains effective for infinite deadlines. A signal
wait cancels its actual reader when the polling deadline expires, so an abandoned reader
does not consume the next notification. Deadline scopes dispose both their linked token
source and their provider-backed timer source.

`MissedScheduleTimeoutShould`, `ScheduledMessageServiceTimeoutShould` and
`ScheduledMessageServiceTimeAwareShould` exercise these paths with controlled time.
These controls establish the tested clock and cancellation behavior; they do not prove
durable recovery or physical timer release independently of the implementation.

Parsed cron schedules delegate timezone evaluation to Cronos. Transition-zone recurrences
explicitly declare `CronRecurrenceProfile.CronosCompatible` through the public scheduler;
there is no implicit transition profile. The immutable preset declares gap coalescing
(including a matching ordinary boundary), fixed-time once-earlier folds and both ordered
UTC series for interval cron. Persisted identifier/version primitives travel with detached
snapshots, retained failure envelopes and recovered work. Unknown or missing declarations
cannot authorize new transition-zone occurrences. UTC/installed transition-free forms
remain available without a profile; one-time and elapsed-interval timezone metadata is
not reinterpreted as a cron declaration. The two added scheduler overloads and schedule-spec
properties intentionally break custom interface implementations in this greenfield API.

Supplied timezone identifiers must roundtrip through the installed BCL database with the
same rules; custom same-ID/different-rules zones are refused. Future instants depend on
installed timezone data; stored/admitted UTC authority is never reinterpreted. An already
captured occurrence may retry, recover and settle under its original authority. If a genuinely
fulfilled occurrence cannot compute a valid future configuration, conditional fulfillment
may pause it with an explicit `FutureConfigurationBlocked` diagnostic while retaining raw
configuration, payload and last success. Failed/unsettled work still follows retain/retry/
quarantine, never a fabricated successful configuration pause. Repair validates the profile
and zone, replaces the settled schedule with a fresh generation and calculates a real next
occurrence. Legitimate finite cron exhaustion is completion, not invalid configuration.

The separate
`CronScheduler.HandleDaylightSavingTransition` helper interprets the supplied wall-clock
fields in the supplied zone; its adjustment option does not change parsed cron evaluation.
Valid inputs and disabled adjustment preserve the original value, including its offset.
Ambiguous inputs select the earlier UTC occurrence.

The schedule-store startup gate requires a durability attestation for the exact selected
`IScheduleStore` instance unless volatile operation is explicitly allowed. Each service
provider owns its own non-owning witness. A later replacement, including a different
instance of the same implementation or an unregistered proxy, cannot inherit an earlier
store's attestation. Register a durable proxy through the same attesting seam when the
proxy itself is the selected store. The container retains sole disposal ownership.
The durable seam activates its singleton with `ActivatorUtilities`: an explicit
`ActivatorUtilitiesConstructor` wins over a longer resolvable constructor; otherwise
normal longest-resolvable selection applies, and ambiguous/missing dependencies fail.
This is an intentional activation contract, unlike ordinary implementation-type DI
registration's handling of that attribute.
Registering a custom store as durable declares a capability; the gate does not test that
provider's disk persistence, transactions or recovery. A new container sharing an in-memory
dictionary is not evidence of recovery after process termination.

Stores implementing `IScheduleExecutionStore` provide occurrence admission on the same
authority as their administrative `IScheduleStore` operations. The in-memory store
implements this capability with detached copies, fresh administrative generations and
one progress commit per admission. Cancellation or replacement before admission prevents
that invocation; an already admitted invocation may finish, but its old result cannot
revive cancellation or overwrite a replacement. Matching release also cannot release a
newer owner. The worker acquires before decoding and releases independently of a stopped
host token. Host shutdown retains the due occurrence for another attempt.

The preserving scheduled-delivery runtime requires `IScheduleExecutionFailureStore` on the
same selected store. Its first budgeted admission atomically captures the positive automatic
budget with the grant and count, before dispatch. A later host proposing another limit receives
`UsedExisting` and the captured limit; it cannot widen this occurrence's budget. Release and
expiry never reset it. New ordinary occurrences may capture fresh policy after successful
advancement. Grant counts are not proven handler-invocation counts: a process can stop before
entering its handler. Dispatch is permitted only within the captured budget. On an over-bound
grant the worker attempts explicit recoverable quarantine without invoking the handler.

Failure recording and quarantine share the owning store's transaction or lock. Failed, null,
unknown or malformed work remains at the same occurrence with its payload and consumed count;
it is not silently coalesced away. Already-admitted unsettled work overrides catch-up omission
on retry, including `SkipMissed`, `DisableSchedule` and a zero replay maximum. At the bound,
confirmed quarantine retains the original envelope, cause, occurrence and budget, disables only
that failed schedule, and is queryable in a bounded exact-tenant inventory. Its future automatic
firings are paused until explicit recovery; unrelated healthy schedules continue. Quarantine is
not delivery success or exactly-once external effects.

`TryResumeQuarantinedOccurrenceAsync` compares the exact quarantine token and a nonempty recovery
command. Its atomic durable receipt grants a fresh generation, count zero and a copy of the
original captured budget while retaining immutable failure history. The next normal acquisition
mints execution ownership. Identical commands return the same receipt after restart and later
progress; a conflicting command cannot grant another reset. Manual recovery is a separate audited
action, not a global bound on arbitrary operator activity. Ordinary replacement refuses captured
unsettled work even if a failure write was interrupted before creating history. Explicit completion
retains interrupted or recovering work as administrative quarantine, clearing the old owner without
erasing its occurrence, payload or count. Successful settlement permits ordinary future rescheduling.

False, failed or uncertain disposition writes never authorize progress or count reset. Unknown
in-flight acknowledgements are reconciled, not called rollback. Capped per-row TimeProvider backoff
prevents tight disposition loops and never dispatches handlers beyond the captured budget. The
standard `schedule-disposition` health check reports a local aggregate unresolved count; bounded keyed
logs diagnose affected work without exposing a cross-tenant inventory. This local observation is not
a durable quarantine receipt. Confirmed provider state is available through the separate exact-tenant
failure queries. Permanent storage failure is degraded/unresolved, not nominal healthy recovery.

Conditional completion distinguishes actual fulfilled dispatch from first-grant policy omission or
disabling. Policy completion preserves the prior last-execution timestamp and cannot retire known
failed or recovered work. Mutable reported attempt counts and policy observations are not bearer
authority. The provider rechecks its own state. `ScheduleFailureStoreConformanceTestKit` supplies
required budget, quarantine/recovery, administrative/query and cancellation authority controls;
physical durability and hosted execution remain separate actual infrastructure gates.

For recurring work beyond the polling/tolerance window, `SkipMissed` advances without
dispatch, `ExecuteLatestMissed` coalesces to one invocation, and `DisableSchedule` disables
the row. `ExecuteAllMissed` admits each persisted occurrence separately, with a positive
`MaxMissedExecutions` bounding each poll and retaining excess backlog for later polls.
Zero skips missed catch-up and advances into the future; it does not suppress an on-time
invocation. Overdue one-time deliveries still run. Ordinary interval advancement uses
completion time; catch-up advances from the recorded occurrence.
An undecodable, unsupported or failed invocation ends that catch-up batch and retains the
same occurrence for bounded retry and recoverable quarantine. It does not advance/reset on
failure. A cancelled host retains work without claiming handler-failure retirement. Expired per-attempt tokens cannot record success even when a
handler catches cancellation and returns normally. Replay logs report bounded actual
attempt counts for the current poll.

The separate `Excalibur.Jobs` Quartz integration declares a different cron convenience
default: ONE (`CronTriggerMisfireInstruction.FireAndProceed`), coalescing recognized
misfires into one immediate firing before resuming the schedule. Its public
`Action<IQuartzBuilder>` callback also permits NONE (`DoNothing`) and ALL
(`IgnoreMisfires`) through Quartz's own trigger APIs. ALL is not this worker's bounded
replay and can produce an outage catch-up burst. Quartz's misfire threshold and job
store govern recognition and acquisition; these mappings imply neither durable
storage nor exactly-once execution. They do not change this worker's `SkipMissed`
default or scheduled-row replay options.


Legacy stores exposing only `IScheduleStore` remain usable for administrative composition,
but are unsupported for the preserving hosted runtime. Actual startup validates failure-store
capability separately from durability attestation, and direct worker construction requires the
same capable store. Volatile persistence opt-in does not waive execution safety. This intentionally
strengthens the supported runtime contract; no lossy compatibility profile is implemented.
Declaring a custom provider durable does not establish these additional guarantees.
Providers implementing the execution capability must coordinate administration, admission,
progress and release at one storage boundary and specify crashed-owner recovery. The
`ScheduleExecutionStoreConformanceTestKit` supplies consumer contract controls; it cannot
certify disk persistence, distributed recovery or NativeAOT publishing. The SQL Server
provider's separate process controls exercise crashed and gracefully released store owners
against a real database; they do not certify hosted-worker or NativeAOT process recovery.
Handlers must be idempotent; this scheduler does not promise exactly-once external effects.
The existing public context `Items` exposes `ScheduleStoreId`, `ScheduleId`, `ScheduleGeneration`
and `ScheduleOccurrenceUtc` from the admitted lease. This key is stable through same-occurrence
retry/restart, differs for other schedules or future occurrences, and excludes owner/attempt
observations. Producer correlation is preserved. Explicit recovery creates a new generation;
its retained receipt links that new execution epoch to the original quarantined work.

Expiring providers can implement `IScheduleExecutionLeaseRenewal` on the configured store.
Acquisition and renewal grant a positive `LeaseDuration` measured at the provider's atomic
authority boundary. Renewal checks the complete current, unexpired tuple and remains valid
through progress settlement until release. The worker uses a conservative local elapsed
deadline, renews throughout execution and latches ownership loss on rejection, failure or
deadline expiry. Loss cancels the occurrence and bars new progress requests or catch-up;
an in-flight request may still be accepted if authoritative ownership remains current and
unexpired. Cooperative cancellation cannot roll back handler
effects or forcibly terminate user code. Renewal cleanup is bounded and observes late
provider failures without disposing cancellation resources still in use. This capability
does not by itself provide durable recovery or distributed external-effect fencing.


### Poison first-attempt evidence and producer metadata

Poison processing uses `context.GetOrCreateProcessingFeature().FirstAttemptTime` for
internal first-attempt evidence. Original string metadata named `FirstAttemptTime`
remains application data and is never parsed as that evidence. Reapplying it on the
same context cannot restart the poison age. Existing programmatic `DateTimeOffset`
values in `Items` initialize the feature only when its timestamp is absent; afterwards
the feature wins. Restore trusted timestamps through the typed feature when carrying
processing state to another context. This local rule does not provide durable
first-attempt retention across independently reconstructed broker deliveries.

### Dispatcher construction and remote connection lifetime

Resolving `IDispatcher` and dispatching only local work do not force the single remote
message-bus factory. `FinalDispatchHandler` captures the sole remote name and retry-policy
choice during construction, then resolves that bus on a matching remote dispatch. Only a
successful bus is cached; an absent bus or a custom-provider resolution fault is not retained
by this optimization. Both ordinary and retry-owned dispatch use the same successful cache.
Named/keyed selection and resource disposal remain the provider's responsibility.

This boundary does not make every DI resolution I/O-free: explicitly resolving a remote bus,
channel or connection can connect, and hosted receivers can connect during startup. Remote
resolution failures surface on actual remote use under the existing dispatch error policy.
The controlled `RabbitMqDispatcherLazinessShould` and
`FinalDispatchHandlerSingleTransportLazinessShould` cases lock the resolution/local/remote
boundary and successful versus absent/faulted resolution behavior; they are not broker or
NativeAOT execution evidence.
