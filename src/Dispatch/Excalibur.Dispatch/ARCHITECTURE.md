# Dispatch pipeline — architecture and guarantees

## Guarantee

**Stated so it can be falsified.**

> **Stage decides across stages. You decide within a stage.**

A middleware declaring a lower `DispatchMiddlewareStage` executes before one declaring a higher stage,
**regardless of the order you added them**. Within one stage, middleware execute in the order you added
them. A middleware declaring no stage is treated as `End`.

**Both halves matter and the second is the one that surprises people.** Across stages, registration
order is explicitly **not** preserved — that is the feature, not an accident. Stating only "registration
order is preserved" would describe a contract stronger than the one you get.

**Why the split.** `Use()`-order-is-the-order ties two decisions together: *what runs* and *when it runs
relative to everything else*. It then hands both to the caller, who has the information for only one of
them. The middleware author knows which phase their work belongs to; the application author knows which
middleware they want. Stage ordering gives each decision to whoever holds the facts.

---

## Guarantees, and what actually enforces each

| # | Guarantee | Seam | RED-detecting arm |
|---|---|---|---|
| 1 | Two middleware with equal effective stage execute in the order they were added | `MiddlewareChainBuilder.cs:152` — `while (j >= 0 && (middlewares[j].Stage ?? End) > currentStage)`. The **strict `>`** means equal keys never swap, so the insertion sort is stable by construction | `MiddlewareChainBuilderShould.ChainExecutor_WithMiddleware_ExecutesChainInOrder`, `DispatchMiddlewareInvokerShould.InvokeAsync_WithMultipleMiddleware_ExecutesInOrder`. **Mutant: `>` → `>=`** reverses equal runs and reddens both |
| 2 | A lower stage executes before a higher one, regardless of registration order | `Delivery/Pipeline/MiddlewareChainBuilder.cs:144-160` — `SortByStageInPlace`, a stable insertion sort keyed on `Stage ?? End` | `MiddlewareChainBuilderShould.ChainExecutor_RunsALowerStageFirst_WhateverOrderTheyWereAddedIn` (five stages added in exactly reverse order) and `…RunsAnUnstagedMiddlewareLast_EvenWhenItWasAddedFirst`. **Mutant: make `SortByStageInPlace` a no-op** — both go RED |
| 3 | A pipeline in which an `IRequiresTenantContext` middleware would run before the last `IEstablishesTenantContext` middleware is **refused when the resolved middleware list is published**, with `InvalidOperationException` naming both concrete types, rather than starting and silently observing no tenant | `Delivery/Pipeline/TenantOrderingRule.cs`, called at `PipelineBuilder.cs:397` as the expression assigned to the resolved-middleware field, so the list cannot be published unverified. Keyed on capability markers read at TYPE level through `MiddlewareIdentity.TypeOf`, so a consumer gets the guarantee by implementing the interface and keeps it when their middleware is registered `Scoped` or wrapped | `TenantContextOrderingThroughRealContainerShould` (4 arms, through a real container) and `TenantContextOrderingShould` (5 unit arms). **Mutant: read the capability off the entry (`m is TCapability`) instead of off the type it stands for** — the two scoped-establisher arms go RED, and all 5 unit arms stay GREEN |
| 4 | `AddDispatch()` seats **no** middleware. A host that configures nothing dispatches and does nothing else | `DefaultPipelineProfiles.CreateDefaultProfile()` declares an empty list | `DefaultPipelineProfilesShould.CreateDefaultProfileThatSeatsNoMiddleware` asserts `MiddlewareEntries.ShouldBeEmpty`, paired with `…CreateStrictProfileWithSecurityMiddleware` as its liveness twin so "empty" cannot be satisfied by a profile builder that seats nothing at all |

### Guarantee 2 is now bound on the executing path — and the measurement that got it there

**Until these arms existed, deleting the entire stage sort left every test in the file green.** That is
measured, not inferred: with `SortByStageInPlace` stubbed to return immediately, the suite reported
**22 passed, 2 failed** — and the 2 were the arms added for this guarantee. All 21 pre-existing tests
passed with the sort disabled.

The cause was fixture shape rather than missing tests. Every fixture driving the live sort declared
`Stage => null`, so every element keyed to the same value and the comparison was never handed two
different keys. Tests elsewhere *do* assert cross-stage ordering, but they construct `DispatchPipeline`
directly — and no dispatched message reaches that object (see below), so those assertions were aimed at
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
PipelineBuilder.cs:400   new DispatchPipeline(resolvedMiddleware, …)   its ctor computes a SORTED,
                                                                       tenant-VERIFIED order…
DispatchPipeline.cs:22   _ordered = TenantOrderingRule.OrderAndVerify(…)  …which never escapes the type
DispatchBuilder.cs:368   new DispatchMiddlewareInvoker(…ResolvedMiddleware…)   the UNSORTED list,
                                                                       re-sorted independently by
                                                                       MiddlewareChainBuilder
Dispatcher.cs:838, :929  middlewareInvoker.InvokeAsync(…)              the only sites that run middleware
```

**So there are two sorts.** `DispatchPipeline`'s is computed and discarded; `MiddlewareChainBuilder`'s is
the one that runs. Both are stable and keyed on `Stage ?? End`, so they agree today — but only one of
them is reachable, and the tests are mostly aimed at the other.

**`DispatchPipeline` is no longer load-bearing for the tenant-ordering refusal, and that is the point.** The refusal used to run only because this object was still being constructed — a side effect of an allocation rather than a named act. It now runs at `PipelineBuilder.cs:397`, where the resolved list is published to both consumers, so a host that resolves only the invoker is covered and the check no longer depends on anyone wanting a pipeline object. `DispatchPipeline` still verifies, through the same rule, for the direct-construction path at `DispatchServiceCollectionExtensions.cs:103`.

---

## Evidence (conformance)

Every guarantee above is bound by an arm on the path a dispatched message actually takes, and each arm is
mutant-proven — a green suite is not evidence that an arm exists, so the mutant is named beside it.

| guarantee | arm | mutant that reddens it |
|---|---|---|
| 1 — order within a stage | `MiddlewareChainBuilderShould.ChainExecutor_WithMiddleware_ExecutesChainInOrder`, `…ChainExecutor_KeepsRegistrationOrder_WithinASingleStage`, `DispatchMiddlewareInvokerShould.InvokeAsync_WithMultipleMiddleware_ExecutesInOrder` | the strict `>` in `SortByStageInPlace` becomes `>=`, so the insertion sort swaps equal keys — **3 arms RED** |
| 2 — order across stages | `MiddlewareChainBuilderShould.ChainExecutor_RunsALowerStageFirst_WhateverOrderTheyWereAddedIn`, `…ChainExecutor_RunsAnUnstagedMiddlewareLast_EvenWhenItWasAddedFirst` | `SortByStageInPlace` returns immediately, leaving registration order intact — **2 arms RED** |
| 3 — a tenant-ordering violation is refused | `TenantContextOrderingThroughRealContainerShould` — 4 arms through a REAL container, including one that resolves only the invoker and never constructs a pipeline; plus `TenantContextOrderingShould` — 5 unit arms | read the capability off the pipeline ENTRY (`m is TCapability`) rather than off the type it stands for — **2 arms RED**. The 5 unit arms stay GREEN under that mutant, which is why they were not sufficient on their own |
| 4 — `AddDispatch()` seats no middleware | `DefaultPipelineProfilesShould.CreateDefaultProfileThatSeatsNoMiddleware`, with `…CreateStrictProfileWithSecurityMiddleware` as the liveness twin | add any entry to `CreateDefaultProfile` — the first arm goes RED while the twin stays green |

**The two mutants are disjoint, and that is the point.** Each reddens exactly one guarantee and leaves the
other green, so neither arm is standing in for the other. A single mutant that reddened everything would
only establish that the chain runs at all.

**What this does NOT cover**, stated so the table is not read as more than it is: these are
sampling-class arms (R2). They execute the orderings we thought to write down. Rungs R3 and R4 — a
property suite over generated registration orders and stage assignments, and a model of the ordering and
verification seam — remain UNVERIFIED for this subsystem.

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
- **The rule is evaluated over the FULL middleware set, while execution filters by message kind.** Filtering preserves relative order, but it can remove the establisher and keep the reader — leaving a reader that runs with nothing establishing tenancy for it, which the rule then treats as having no ordering relation to violate. Whether a reader with no establisher in the applicable subsequence should itself be refused is a contract question and is deliberately not decided.
- **`IDispatchPipeline` is public and states no contract.** A consumer may implement and register their own, and it will carry none of these guarantees. Nothing detects that, because the published abstraction never promised them.
- **Two implementations of one stable sort** (`DispatchPipeline.cs:52` LINQ, `MiddlewareChainBuilder.cs:144`
  insertion). They agree today. Nothing enforces that they continue to.
- **`DispatchOptions.EnablePipelineSynthesis` controls nothing.** The profile registry registers its
  profiles unconditionally, so the condition it guards is never reached.

---

## Rigor

> Blast radius: **catastrophic**. The discriminator is silence, not severity — a middleware that is
> silently skipped or reordered produces no error, and an authorization middleware that does not run
> looks exactly like one that ran and permitted.

- **R1** — compiler, nullable reference types, analyzers. Met.
- **R2** — this document plus a RED-detecting arm per guarantee. **Met.** Each of the four guarantees has an arm on the path a dispatched message takes, and each is mutant-proven to go RED independently.
- **R3** — property-based tests over registration orders and stage assignments. **UNVERIFIED**; none exist.
- **R4** — a model of the ordering and verification seam. **UNVERIFIED**.

The arms that exist are sampling-class. Do not describe this seam as covered.
