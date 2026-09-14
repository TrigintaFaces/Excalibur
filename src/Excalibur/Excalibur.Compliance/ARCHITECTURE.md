# Architecture — Excalibur.Compliance (erasure, legal hold, and SOC 2 attestation)

This document states what the erasure and legal-hold subsystem guarantees, how it achieves it, and —
just as importantly — what it does **not** yet prove. It is a contract, not a description: every claim
below is meant to be falsifiable, and where we cannot falsify one we say so rather than assert it.

## Guarantees

### Tenant isolation

**Under multi-tenancy, a read of an erasure request or a legal hold returns rows owned by the ambient
tenant, plus rows explicitly recorded as estate-wide. It returns no other tenant's rows.**

This holds for all six shipped stores — SQL Server, PostgreSQL and in-memory, for both contracts.

**A caller-supplied `tenantId` can only narrow that result, never widen it.** The argument is ANDed onto
the ambient term rather than replacing it, so two equality terms can only intersect: a caller naming
another tenant gets the empty set, and a caller naming none still gets their own ambient scope. Widening
would require changing an `AND` to an `OR`, which is a one-token change a reviewer can see.

### Legal holds block erasure, and a missing tenant does not lift them

**An active legal hold covering a data subject blocks that subject's erasure, and a hold check made
without a tenant consults estate-wide holds rather than none.**

The second half is the load-bearing part. An estate-wide hold carries no data-subject identifier, and the
subject query matches on that identifier — in SQL a null never equals a value — so the subject path can
never return one. If the estate-wide lookup is also skipped, the check sees **zero** holds and reports
nothing blocking. Erasure is irreversible, so that failure is unrecoverable in the direction that matters.

### Reading and mutating a hold are asymmetric, deliberately

**A tenant sees an estate-wide hold. A tenant cannot modify one.**

A tenant must see it, because it blocks that tenant's erasures. A tenant must not modify it: a mutation
matching an estate-wide row would let one tenant re-home an estate-wide preservation order into its own
partition, silently lifting it for every other tenant — whose next erasure then proceeds and reports
success. Reads use *owned-or-estate-wide*; mutations use strict ownership.

### SOC 2 attestation — what a generated report may be read to assert

**Guarantee: a generated report attests only to criteria that a registered validator actually assessed.
Every other criterion is reported as not assessed, is excluded from the compliance percentage — from the
numerator and the denominator both — and does not move the auditor opinion.**

This section previously read `UNVERIFIED` in its stronger sense: the behaviour had been *measured to
contradict* the target. That measurement is no longer current. The value set can now hold the fact, the
aggregation honours it, and two arms bind the one-token mutant that would undo it.

**How it is achieved.** `CriterionOutcome` carries three states — `NotAssessed`, `Met`, `NotMet` — and
`ControlSection.Outcome` is typed by it (`Soc2Report.cs`), replacing a `bool` in which "nobody assessed
this" and "we assessed it and it failed" were the same value. `Soc2ReportGenerator` derives the state from
whether any validation result exists at all, rather than from whether the results were favourable; the
compliance percentage is computed over assessed sections only; the exception loop takes `NotMet` sections
and skips both `NoExceptions` and `NotTested`, so a criterion nobody examined raises no finding against
the consumer; and an unassessed criterion instead receives an entry whose text attributes the gap to this
framework's coverage rather than to the consumer's controls. Where nothing at all was assessed the report
is `Unknown`, not `NonCompliant` — the latter reached an adverse opinion for a host that had simply
registered no validators.

**The enforcing arms, and the one-token mutant they bind.** The violating mutant named below was applied
and measured, not predicted: collapsing the three-state derivation back to `validationResults.Count > 0
&& …` — which maps the not-assessed state onto the failing one — turns
`Soc2ReportGeneratorShould.Report_an_unassessed_criterion_as_NotAssessed_rather_than_NotMet` and
`Soc2ReportGeneratorDepthShould.Section_is_not_assessed_rather_than_not_met_when_no_validator_is_registered`
RED, and only those. Both directions of the biconditional are covered: a criterion with no registered
validator is reported not-assessed, and a criterion whose validator ran and failed is still reported
not-met.

**Consumer obligations.** Validators are opt-in. A report generated with none registered for a criterion
is not a deficient report — it is a report that says, in the document handed to an assessor, that this
framework did not assess that criterion. Substantiating it is the consumer's to arrange.

**Known gap, stated because it bounds the guarantee above.** The guarantee covers the criterion layer.
One layer below it, a control validator reports effectiveness as a `bool`, so a validator whose own check
could not run — it threw, or had nothing in scope — has no way to say so and must spell the outcome as
either effective or deficient. Both spellings assert something that was not established. The criterion
layer is therefore honest about *coverage* and not yet fully honest about *the confidence of an individual
control's verdict*; treat a single control's effectiveness as a weaker claim than the criterion-level
outcome until that type carries the third state too.

## How it is achieved

- **One derivation point per store.** Each store exposes a single private ambient-scope property, and
  every tenant-facing statement reads it. Nothing binds a tenant by hand, so there is no per-call-site
  opportunity to bind a nullable value and produce a predicate that silently matches nothing.
- **Deployment mode decides the shape**, and it is read from the multi-tenancy configuration rather than
  inferred from whether a tenant context is registered — the framework always registers a single-tenant
  default, so presence would make every deployment look multi-tenant and strand existing rows.
- **The capability marker cannot be separated from the dependency.** Stores register through the
  tenant-scoped registration seam, which resolves the tenant context itself and hands it to the factory.
  A store built without it is inexpressible through that seam, so a store cannot carry a truthful-looking
  isolation marker while never having received the dependency.
- **The startup check fails closed.** Both contracts are in the set the multi-tenancy registration
  verifies, so a multi-tenant host registering an unscoped implementation fails at startup rather than
  leaking at runtime.

### Failure reporting is discriminable

**A caller can tell "this erasure request is already stored" from every other reason a save can fail, by
exception type alone, and without referencing a database provider.**

`SaveRequestAsync` raises `DuplicateErasureRequestException` when — and only when — a request with that
identifier is already stored; `SaveCertificateAsync` raises `DuplicateErasureCertificateException` on the
same terms. Every other terminating condition carries its own type, and the one most easily mistaken for a
duplicate sits outside the `InvalidOperationException` hierarchy altogether:
`ErasureStoreNotProvisionedException` for a schema that is absent or missing columns the store binds,
`TenantRequiredException` for an unresolved ambient tenant, `ObjectDisposedException` for a disposed store.

The distinction is load-bearing, not cosmetic. The readings demand opposite responses: "already stored"
means the request is safe and the caller should stop, while every other condition means nothing was stored
and the caller must re-file. A caller that cannot tell them apart, and takes the first reading, silently
discards erasure requests — and an erasure request is a data subject's exercise of a statutory right, so
nothing downstream reports the loss.

`GetStatusAsync` and `UpdateStatusAsync` stay total over lookup: a request that is not there is reported
as `null` and `false` respectively, never as an exception. The only condition under which they do not
return is a store whose schema cannot answer at all.

### Schema provisioning is settled at startup, not on the write path

**A host whose erasure schema is absent or stale fails to start, rather than failing one erasure request
at a time.** Each provider store contributes an `IErasureSchemaValidator`, and the hosted service
registered alongside it verifies every one during startup. Provisioning is a property of the deployment,
so it is checked once, where the fault is attributable — not on the path of a data subject's request,
where it is not.

A first-use check remains inside each store as the fail-closed floor for consumers that never run that
hosted service: a store constructed directly, or a serverless host with no startup pipeline. It raises the
same provisioning type, so the floor and the startup check report the same condition the same way.

## Consumer obligations

- **Set the ambient tenant; do not rely on the argument.** Scope comes from the ambient tenant context.
  The `tenantId` parameter can narrow a result but cannot select a tenant.
- **Always supply a tenant when checking legal holds in a multi-tenant deployment.** Omitting it widens
  the check to estate-wide holds, which is safe but broader than you probably intend, and is reported at
  warning level when it changes the outcome.
- **Do not scope the background sweeps.** Expiry of holds and draining of scheduled erasure requests are
  estate-wide by design. Scoping them to one tenant would stall erasure for every other tenant and make
  expired holds permanent.
- **Single-tenant deployments need no action.** The tenant term applies only when multi-tenancy is
  configured; existing rows are untouched and no migration is required.
- **Catch `DuplicateErasureRequestException`, never `InvalidOperationException`, to detect a re-filed
  request.** The base type is also raised by conditions meaning the request was *not* stored, so a caller
  branching on it treats an unprovisioned database as a request already on file and drops it.
- **Run the host's startup pipeline in production.** Startup is where a provisioning fault is reported as
  a provisioning fault. A consumer that skips it still fails closed on first use, but learns about a
  mis-provisioned database from a data subject's request rather than from a failed deployment.

## Evidence

- **SQL Server and PostgreSQL, both contracts** — `SqlServerErasureStoreTenantIsolationShould`,
  `SqlServerLegalHoldStoreTenantIsolationShould`, `PostgresErasureStoreTenantIsolationShould`,
  `PostgresLegalHoldStoreTenantIsolationShould`: tenant-isolation suites run against real containers, with
  safety arms (a tenant that owns nothing must read nothing) and liveness arms (the owning tenant must
  read its own row — a store that returns nothing to everybody passes safety trivially).
- **Non-vacuity:** the safety arms were verified RED against a one-token revert of the deployment-mode
  flag, and the read/mutation asymmetry against a revert of the mutation predicate to the read form. Both
  cycles rebuilt the implementation and test projects explicitly; a run against a stale binary proves
  nothing.
- **Duplicate discrimination, both SQL providers, real containers** — `PostgresErasureDuplicateTranslationShould`,
  `SqlServerErasureDuplicateTranslationShould`, `InMemoryLegalHoldStoreDuplicateSignalShould`: a genuine duplicate must raise the
  specific type and preserve the provider's own exception as its inner exception (liveness), while a
  provider failure that is not a uniqueness violation must not be translated at all (safety). The paired
  arms fail both a blanket catch and a filter narrowed until it never fires.
- **Provisioning faults, both SQL providers, real containers** — `PostgresErasureProvisioningFaultShould`,
  `SqlServerErasureProvisioningFaultShould`: an unprovisioned store must raise the
  provisioning type, and that type must not be assignable to `InvalidOperationException` (safety), while a
  provisioned store must still start and still store (liveness). The startup arms resolve the hosted
  service through the real registration path rather than constructing it, so a registration that
  contributes no validator fails rather than passing quietly.

- **SOC 2 attestation: NO EVIDENCE EXISTS, and the distinction matters.** No conformance arm, unit test
  or governance check RED-detects a report that asserts a verdict on a criterion nothing assessed. This
  is **not** the ordinary "unverified" case — a guarantee we believe and have not yet proven. It has been
  **measured false**: the behaviour described under the attestation guarantee above was reproduced by
  reading the enumeration and aggregation paths, and the affected set is the default configuration rather
  than an exotic one. Treat every statement a generated report makes about an unassessed criterion as
  unsupported until an arm exists that fails when the report asserts one. **Do not cite this subsystem's
  reports as evidence of coverage.**

## Known gaps

These are stated because a guarantee with no enforcing test is documented, never asserted.

- **PostgreSQL is fixed but unverified.** The structural change is in place, but no test runs against a
  real PostgreSQL server, so nothing would RED-detect a regression there.
- **Two safety arms are not proven non-vacuous.** The arms covering a caller who names another tenant
  pass under the mutant for an incidental reason — the seed row carried no tenant before the fix, so the
  named-tenant filter matched nothing either way. They bind the contract going forward; they are not
  evidence that it was broken before.
- **The in-memory store has no schema, so the provisioning guarantee is vacuous for it.** It cannot be
  mis-provisioned and contributes no validator; the guarantee binds the SQL providers only.
- **The data-inventory store is a different contract and is not covered by this document.**
- **The audit store and the general compliance store hold tenant-owned rows and are neither verified nor
  covered by the startup check.** They are excluded deliberately rather than gated blind: failing a host
  closed on a store whose isolation has not been demonstrated would trade a real outage for an unproven
  guarantee.
  The general compliance store — consent records, erasure logs and subject-access requests — ships in exactly
  two implementations, enumerated here because the guarantee above does not cover them: a Postgres store in
  `Excalibur.Compliance.Postgres` and a MongoDB store in `Excalibur.Compliance.MongoDb`. There is no
  in-memory implementation of this contract, and after the provider split **this package ships none of them**,
  which is what keeps the MongoDB and Postgres drivers out of a consumer that uses neither. The split moved
  assemblies and changed no behaviour: the tenant term each store binds is unchanged by it. Both stores
  partition by tenant — the tenant participates in the MongoDB document key and in the Postgres uniqueness
  constraint, so it is the upsert conflict target rather than a filter applied afterwards — but neither is
  exercised against a real server here, so that is described, not asserted.
