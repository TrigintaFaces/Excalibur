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

### Erasure and legal-hold reads are not tenant-scoped

**What you see.** Nothing fails. If you run more than one tenant against shared erasure-request or legal-hold tables, a read can return records belonging to another tenant — and, in one direction, an erasure can proceed that should have been blocked.

**What it means.** The erasure-request and legal-hold stores do not implement tenant scoping. There is no ambient tenant context in any of them, no tenant-scoping decorator, and — unlike the event, saga, inbox, outbox and projection stores — these two contracts are **not** covered by the fail-closed check in `AddMultiTenancy()`, so a multi-tenant host does not fail at startup either. The tenant value is recorded against each row; the read path does not reliably constrain queries to it.

**The consequence is worse in one direction than the other, and our previous description of this had it backwards.** We said the failure mode was over-application — that omitting the tenant returned *more* holds rather than fewer, so erasure would be wrongly blocked rather than wrongly permitted, and that this direction was recoverable. **That is true only for holds placed against a specific data subject.** It is false, and unsafe, for tenant-wide holds:

- A tenant-wide hold is stored with no data-subject identifier.
- The subject-hold query matches on that identifier, and a null value never matches — so a tenant-wide hold can never be returned by it.
- The tenant-wide lookup that *would* return it is skipped entirely when no tenant is supplied.

So a hold check made without a tenant sees **none** of your tenant-wide holds, reports nothing blocking, and the erasure proceeds. **Erasure is irreversible.** A legal hold exists precisely to prevent that, and this defeats it silently.

**Which versions.** Every published version up to and including the current pre-release.

**We are not giving you a site count, deliberately.** Our own count of affected call sites moved five times while we investigated, and every revision was upward. A number here would be frozen at publication while the truth was not, and it would have understated the exposure. The boundary above is what we can state and stand behind: **these subsystems do not implement tenant scoping.** That is true regardless of how many call sites it turns out to be.

**What you must do.**

- **Always supply a tenant when checking or evaluating legal holds**, even where the API accepts none. An erasure request submitted without a tenant will not consult tenant-wide holds.
- If you operate erasure or legal hold multi-tenant, do not rely on the store layer for isolation in this release. Apply tenant filtering in your own query path, or give each tenant its own database or schema.
- If you run a single tenant, no action is needed.

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

---

## Unverified areas

Nothing in this section is a report of broken behaviour. Each entry marks a place where our testing does not reach, so that you do not read our test totals as covering it.

### The Record-of-Processing-Activities data map has no executing test coverage

**What you see.** Nothing that distinguishes it from covered code — which is the point of disclosing it.

**What it means.** The data-map query path in the SQL Server and PostgreSQL compliance providers is not exercised by any executing test. Tests for it exist, but they either substitute the query store or target the in-memory implementation, so no query is ever executed against a database. The shared conformance suite covers this path, but only its in-memory derivation was ever implemented.

**How we found out, because it is the honest answer.** A defect on this path made every call to it fail unconditionally, and it still passed our full unit suite, our harness checks, and review. A method that could not succeed under any input survived all of that. That tells you nothing about the defect and a great deal about the coverage: the only way it survives is if nothing exercises the path.

**That defect is now fixed. This entry is not about the defect.** Repairing the query makes it work; it does not create the test that would have caught it, so the coverage gap this describes is unchanged.

**What you must do.** Validate the data-map path against your own infrastructure before relying on it for a record-of-processing-activities report.

### Two shipped conformance kits have no implementations

**What it means.** The key-escrow and snapshot-store conformance kits we ship are abstract base classes with **no derived suites anywhere**, so no test runner discovers anything in them. They cannot execute by construction.

**Narrower than it sounds, for snapshots.** Snapshot-store behaviour *is* covered — by nine provider suites deriving from a different base class. The orphan is the shipped kit, not the capability. Key escrow has no such alternative coverage.

**What you must do.** If you implement a custom key-escrow provider, do not expect the shipped conformance kit to validate it — it will run nothing.

### Several integration suites are not measured executing

**What it means.** These areas are exercised by unit tests, but we hold no measurement showing their integration suites executing: Elasticsearch monitoring, OpenSearch, tiered storage (S3 and Azure Blob), and tenant sharding. Their suites are included in our test selection and are not quarantined; they gate on container availability at runtime, so an absent container skips them rather than failing.

**We are stating this narrowly on purpose.** There is real counter-evidence — several of these areas carry sibling tests that *cannot* skip and would turn the build red if the infrastructure were missing, which suggests the containers are present and the suites do run. We are not treating that as grounds to clear the entry, because it is an inference and not a measurement. Our standard for removing one of these is the suite being **measured green**, not the fix being written or the reasoning being persuasive.

**What you must do.** If you depend on these areas, validate them in your own environment. Expect this entry to narrow once we can publish the measurement.

### Cosmos DB coverage is thinner than the rest

Three separate things are true about Cosmos DB in this release, and they are easier to act on together than apart.

- **Some integration tests do not pass.** When the Cosmos DB integration tests are run, some fail. We have not resolved those failures for this release, and we have not published a build in which they executed and passed.
- **The snapshot-store conformance suite runs nightly, not on every change.** Cosmos DB tests are deliberately excluded from the per-change build so it does not depend on a slow emulator start; they run on a nightly schedule instead. A Cosmos regression is therefore caught within a day rather than on the change that introduces it. The readiness check **refuses** rather than skipping when the emulator is not genuinely usable, so a nightly pass is real evidence — but a green per-change build is not evidence about Cosmos.
- **The event-store telemetry suite executes on no CI runner at all.** All fourteen of its tests self-skip on Linux runners, and both of our CI paths are Linux. It is unverified in this build, not merely thinly covered.

**About the other providers.** A recent full run also showed failures outside Cosmos DB, but on inspection almost all were test containers failing to start on the machine running the suite — a local resource limit, not the providers misbehaving. We are not reporting those as provider defects, and we are not claiming that run proves the other providers correct either. What we can say is narrower and it is what we mean: **the Cosmos DB gaps above are real coverage gaps; the rest of that run's failures were environmental.**

**What you must do.** Treat the Cosmos DB provider as materially less proven than the others in this release. If you depend on it, validate the operations you rely on against your own infrastructure before trusting them in production.

### The in-memory inbox is not tenant-aware

**What it means.** The in-memory inbox store keys entries without a tenant term, so it does not separate tenants. Every persistent inbox provider does — the tenant is part of the stored key, so reads and claims are constrained by construction.

**A guard exists, and we have not established that it is order-independent.** `AddMultiTenancy()` requires a tenant-scoping capability from the inbox and throws when the in-memory store is registered, because that store advertises none. That check inspects registrations, so we cannot yet rule out its depending on the order in which you call `AddMultiTenancy()` and register the inbox. We have not found a backstop independent of that ordering, and we would rather say so than assert a guarantee we have not tested.

**What you must do.** Do not use the in-memory inbox for multi-tenant workloads — it is intended for development and tests. If you do use it multi-tenant, do not rely on the startup guard alone.

---

## Resolved since the last update

Listed rather than deleted, so a fixed issue is distinguishable from a forgotten one.

- **Inbox reads are now tenant-scoped.** Previously disclosed as unscoped across the board. The relational stores (SQL Server, PostgreSQL, Oracle) now apply a tenant predicate to their read, claim and merge paths and fail closed when a tenant is active but unresolved; the document and cache stores (MongoDB, Cosmos DB, DynamoDB, Firestore, Redis, Elasticsearch) carry the tenant inside the stored key, so a keyed read cannot cross tenants. The in-memory store is the exception and is listed above.
- **The unexplained not-found responses from the Cosmos DB snapshot store are explained, and were never a provider fault.** We disclosed seeing not-found responses for a database that should have existed, and said we did not know the cause and could not rule out the provider. We since determined it: our own test teardown deleted a database shared by the whole test class, so the first test destroyed it for every test that followed. It was our test harness. **Nothing about it affected consumers, and the previous entry implying the provider might be at fault was wrong.**

---

## See also

- [What's New](./whats-new.md) — what changed in this release, and what to do to upgrade
- [Multi-tenancy](./multi-tenancy.md) — how tenant isolation is intended to work
- [Versioning strategy](./migration/version-upgrades.md) — release stages and what each one guarantees
