# Architecture — Excalibur.Compliance (erasure and legal hold)

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

## Evidence

- **SQL Server, both contracts:** tenant-isolation suites run against a real SQL Server container, with
  safety arms (a tenant that owns nothing must read nothing) and liveness arms (the owning tenant must
  read its own row — a store that returns nothing to everybody passes safety trivially).
- **Non-vacuity:** the safety arms were verified RED against a one-token revert of the deployment-mode
  flag, and the read/mutation asymmetry against a revert of the mutation predicate to the read form. Both
  cycles rebuilt the implementation and test projects explicitly; a run against a stale binary proves
  nothing.

## Known gaps

These are stated because a guarantee with no enforcing test is documented, never asserted.

- **PostgreSQL is fixed but unverified.** The structural change is in place, but no test runs against a
  real PostgreSQL server, so nothing would RED-detect a regression there.
- **Two safety arms are not proven non-vacuous.** The arms covering a caller who names another tenant
  pass under the mutant for an incidental reason — the seed row carried no tenant before the fix, so the
  named-tenant filter matched nothing either way. They bind the contract going forward; they are not
  evidence that it was broken before.
- **The data-inventory store is a different contract and is not covered by this document.**
- **The audit store and the general compliance store hold tenant-owned rows and are neither verified nor
  covered by the startup check.** They are excluded deliberately rather than gated blind: failing a host
  closed on a store whose isolation has not been demonstrated would trade a real outage for an unproven
  guarantee.
