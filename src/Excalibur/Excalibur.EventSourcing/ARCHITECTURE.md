# Architecture — Event Sourcing Tenant Isolation

> **Guarantee contract for tenant isolation across the event store and snapshot store.** This document is
> the source of truth for *how the event-sourcing stores keep one tenant's data from ever being read,
> written, or erased under another tenant* — and how "no tenant" (an untenanted deployment) is represented
> so it can never collide with a real tenant. It is a contributor + integrator reference. Keep it current:
> any change to a tenant write path, read predicate, or erase predicate updates this file, verified at
> architectural review.

## Guarantee

**Tenant-scoped isolation.** Every event-store and snapshot-store read, write, and erase is confined to a
single **tenant partition**. A tenant-scoped operation never observes, overwrites, or erases another
tenant's rows, and an untenanted operation never touches any tenant's rows (and vice versa).

There are exactly **three kinds of partition**, and they are mutually exclusive:

| Partition | Meaning |
|---|---|
| A **real tenant** (`Scoped("<id>")`) | a genuine tenant identifier; guaranteed non-null and non-whitespace |
| The **default tenant** (`Scoped(TenantDefaults.DefaultTenantId)`) | the single tenant of a single-tenant deployment that has opted into ambient tenancy; a reserved identifier that a real tenant can never equal |
| **Untenanted** (`TenantScope.None`) | tenancy is not applicable to the row (no ambient tenant); a distinct partition, not a tenant |

The defect this guarantee closes: **multiple different encodings of "no tenant" landing in one column**, so
that a write under one encoding becomes invisible (or destructively visible) to a read under another. There
is now **one** canonical default-tenant identifier at the context layer, and each store encodes "untenanted"
with a **single, collision-proof value** appropriate to its constraint model (below).

> **Consumer obligation:** none for isolation itself — it is enforced store-side. If you enable ambient
> multi-tenancy, resolve a tenant per operation; if you do not, operations run untenanted and stay in the
> untenanted partition.

## How it is achieved (the seam)

### 1. One canonical default-tenant identifier (context layer)

The single-tenant default context and the configured fallback resolve to **one** reserved identifier —
`TenantDefaults.DefaultTenantId` — used everywhere a default tenant is needed. It is a **reserved token**
(wrapped in double underscores), deliberately shaped so a real deployment naming a tenant `"Default"` can
never collide with it. `Scoped()` rejects null/whitespace, so a real tenant identifier can never equal the
reserved token either.

### 2. Untenanted encoding per store — dictated by the constraint model

"Untenanted" is a **distinct partition**; how it is physically represented is a store-private detail governed
by that store's uniqueness/upsert mechanism and by whether a tenant column exists at all. Two shapes are in
use, and **both satisfy the same invariant** (write and read agree within the store; a real tenant can never
equal the untenanted partition):

- **Append stores (the event store): the impl is tenant-column-AGNOSTIC; the shipped reference schema carries
  a present-but-nullable `TenantId`; untenanted rows are `NULL`; isolation on the unscoped path is enforced at
  the composition layer, not by a bare-store predicate.** The unscoped path emits **no tenant reference at
  all** — an unscoped write (`TenantScope.None`) omits the tenant column, and an unscoped read/erase emits no
  tenant predicate — so the store works **with or without** a tenant column: a minimal non-multi-tenant
  consumer need not carry one, while the shipped reference schema includes a nullable `TenantId` (an unscoped
  write leaves it `NULL` — the untenanted partition). A `Scoped(id)` write stamps the tenant. Because the
  unscoped read/erase targets the whole aggregate (no tenant predicate), isolation is provided by the
  surrounding composition, homogeneously per deployment (the same injected `ITenantContext` governs write and
  read/erase):
    - **Non-multi-tenant deployment:** every row is `NULL`-tenant (all writes are unscoped), so an unscoped
      read/erase spans only the single untenanted partition — there are no other tenants' rows to reach.
    - **Multi-tenant deployment:** every operation resolves a `Scoped` tenant, and the row-discriminator
      composition interposes a **fail-closed guard** (`TenantScopedEventStore`, §3) that **throws** on any
      unscoped erase/read *before* a predicate-less statement can run. So an unscoped operation can never reach
      another tenant's rows.
  Consequently, cross-tenant over-erasure is prevented by the **composition-layer guard (multi-tenant) plus the
  all-`NULL` untenanted partition (non-multi-tenant)** — the enforcing conformance test is the erasure
  contributor's multi-tenant fail-closed lock (§ Evidence). The bare store's unscoped no-predicate statement is
  safe *in composition*, not structurally isolated at the bare layer. The event row's uniqueness key does not
  include the tenant.

- **Relational upsert stores (the SQL snapshot stores): untenanted = the reserved sentinel
  `'__untenanted__'`, by design.** This applies to the SQL Server, PostgreSQL, Oracle and SQLite snapshot
  stores; document and key-value stores encode the tenant in the document key instead (see *Known gaps*).
  The snapshot is an **upsert** keyed on `(aggregate, aggregate-type, tenant)`, so the tenant participates in a
  `UNIQUE` constraint. `NULL` cannot serve there: SQLite (and pre-15 PostgreSQL) treat `NULL` as **distinct**
  in a `UNIQUE` constraint, so a nullable tenant would make every untenanted save a new row instead of an
  upsert — a duplicate-snapshot leak — and SQLite offers no `NULLS NOT DISTINCT`. The snapshot stores
  therefore encode untenanted as the reserved non-empty sentinel `'__untenanted__'`, bound on both the write
  and the read path so the two always agree. This is **collision-proof**: a scoped tenant term can never be
  the sentinel, so no real tenant can claim the untenanted partition. The sentinel is a **store-internal key
  encoding** — it never crosses the tenant boundary and never appears as a tenant identity to a consumer.

  > **The empty string cannot serve as this sentinel and is no longer used.** Oracle folds `''` to `NULL`, so
  > the identical intent became a *different value* on that provider and required a separate function-based
  > unique index to stay correct. A concrete non-empty sentinel expresses identically on every provider, which
  > is what allows all of them to share one representation and one set of statements.

  > **Upgrading from a deployment written under the earlier `''` encoding:** rows stored with `''` are not
  > matched by a read that binds the sentinel — the read uses a direct equality on the tenant column, with no
  > `COALESCE` to bridge the two encodings, so an unmigrated untenanted snapshot is silently invisible and the
  > aggregate rebuilds from its events instead. Run the snapshot sentinel migration script shipped with your
  > provider package before upgrading; do not rely on the old value being read back.

### 3. Erase (right-to-erasure) stays within the partition

The erase path uses the **same scope-branch as the reads**: `TenantScope.Scoped` emits `tenant = @t`, and
`TenantScope.None` (non-multi-tenant) emits **no tenant predicate**, targeting the single-partition table.
Under multi-tenancy the erase is **fail-closed structurally**, symmetric with reads and appends: the
row-discriminator composition wraps the erasure surface with a tenant-scoping guard that requires a resolved
ambient tenant and throws `TenantRequiredException` **before any erase** — so a multi-tenant deployment can
never emit a predicate-less erase across every tenant's rows, even on the default per-subject erase path. A
genuine non-multi-tenant deployment (no guard, no tenant column) erases its single partition unchanged. The
erase therefore can neither miss a subject's rows (a silent no-op erase) nor sweep another tenant's rows
(over-erasure). The snapshot upsert stores, whose tenant column is always present, scope by the reserved
untenanted key (the reserved `'__untenanted__'` sentinel) instead of omitting the predicate.

The erasure capability is **forwarded through the whole event-store decoration chain** so it is reachable via
the supported dependency-injection composition. The shared decorator base forwards the erase to its inner store
by default (recursing to the terminal provider store, which performs the tombstoning), so a telemetry, metrics,
or tenant-scoping decorator cannot silently strip erasure merely by not re-implementing it. The tenant-scoping
decorator overrides the forward to apply the fail-closed guard above; a decorator wrapping a store that does not
support erasure surfaces a clear error at erase-time rather than removing the capability.

## Consumer obligations

- To operate multi-tenant, establish the ambient tenant per operation; otherwise operations run untenanted.
- Do not attempt to use the reserved empty-string or reserved default-tenant identifier as a real tenant id
  — `Scoped()` rejects empty/whitespace, and the reserved default token is not a real tenant.

## Evidence (conformance)

The tenant-isolation properties are proven by real-infrastructure conformance/regression tests (non-skipped
where the backing store is available):

| Property | Proven by |
|---|---|
| Untenanted write is readable by an untenanted read | snapshot + event-store unscoped round-trip arms |
| A tenant-scoped read never sees another tenant's or the untenanted rows (and vice versa) | scoped-isolation arms |
| An untenanted double-write upserts to **exactly one row** (the reserved sentinel upsert key) | snapshot untenanted-double-write arm |
| A multi-tenant unscoped erase fails closed (throws, mutates zero rows); a non-MT erase still tombstones; a scoped erase touches only its tenant | event-store erase fail-closed + isolation arms |
| Erasure resolved through the supported DI composition reaches the store and tombstones (the decoration chain does not strip the capability), for both multi-tenant and non-multi-tenant hosts | event-store erasure real-DI-resolve arms (MT + non-MT) |
| The default-tenant identifier is a single canonical reserved value | tenant-defaults unit arms |

## Known gaps

- **Upgrading across the untenanted-sentinel change requires running the provider's migration script.**
  The **relational** snapshot stores (SQL Server, PostgreSQL, Oracle, SQLite) encode untenanted as one
  reserved non-empty sentinel. **Document and key-value snapshot stores do not use this encoding at all** —
  they compose the tenant into the snapshot's document identifier, so an untenanted snapshot simply has no
  tenant term in its key and there is no `UNIQUE`-constraint problem for a sentinel to solve. The paragraphs
  above describe the **relational** family only. An earlier
  encoding used the empty string, which Oracle folds to `NULL` and which therefore could not be a shared
  representation. Reads bind the sentinel with a direct equality and **no `COALESCE` bridging the two
  encodings**, so a snapshot row still carrying the old value is **not found** — the read returns nothing and
  the aggregate rebuilds from its event stream rather than raising an error. The data is not lost, but the
  snapshot's performance benefit is silently gone until the rows are migrated. Run the snapshot sentinel
  migration script shipped in your provider package as part of the upgrade.

- **Tiered (hot/cold) erasure covers the hot tier only.** When events are archived to a cold tier, the erase
  tombstones the hot tier; the cold archive has no erase surface yet, so a right-to-erasure request against an
  aggregate with archived events is not silently partial — it fails fast rather than leaving cold copies behind.
  Full cold-tier erasure is not yet implemented. Consumers requiring GDPR erasure of archived events should not
  enable cold-tier archival until that capability lands.
