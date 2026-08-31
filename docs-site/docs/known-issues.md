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

Every entry is re-checked against the code before each update. Entries we have confirmed fixed are listed under [Resolved](#resolved-since-the-last-update) rather than quietly deleted, so you can tell a fixed issue from a forgotten one.

---

## Defects

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

- **Erasure and legal-hold reads are now tenant-scoped.** Previously disclosed as unscoped, including a
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
- **The Cosmos DB, DynamoDB, Firestore and MongoDB event stores now confine tenants.** Previously
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
- **Inbox reads are now tenant-scoped.** Previously disclosed as unscoped across the board. The relational stores (SQL Server, PostgreSQL, Oracle) now apply a tenant predicate to their read, claim and merge paths and fail closed when a tenant is active but unresolved; the document and cache stores (MongoDB, Cosmos DB, DynamoDB, Firestore, Redis, Elasticsearch) carry the tenant inside the stored key, so a keyed read cannot cross tenants. The in-memory store is the exception and is listed above.
- **The unexplained not-found responses from the Cosmos DB snapshot store are explained, and were never a provider fault.** We disclosed seeing not-found responses for a database that should have existed, and said we did not know the cause and could not rule out the provider. We since determined it: our own test teardown deleted a database shared by the whole test class, so the first test destroyed it for every test that followed. It was our test harness. **Nothing about it affected consumers, and the previous entry implying the provider might be at fault was wrong.**

---

## See also

- [What's New](./whats-new.md) — what changed in this release, and what to do to upgrade
- [Multi-tenancy](./multi-tenancy.md) — how tenant isolation is intended to work
- [Versioning strategy](./migration/version-upgrades.md) — release stages and what each one guarantees
