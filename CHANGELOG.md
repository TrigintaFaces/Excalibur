# Changelog

All notable changes to Excalibur and Excalibur.Dispatch are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Known Issues

Defects identified and classified as affecting this pre-release. The full descriptions, with the action
each one requires of you, are on the **Known issues in this pre-release** section of the What's New page;
they are kept there rather than duplicated here so the two cannot drift apart.

- **The bundled Cosmos DB emulator fixture cannot connect using its documented approach.** Its
  documentation blames the emulator's self-signed certificate; the actual obstacle is that the client is
  sent to the emulator's advertised port rather than the mapped one. Set
  `CosmosClientOptions.LimitToEndpoint = true`.
- ~~**The same fixture pins an emulator image that becomes ready but cannot create a database.**~~
  **Resolved in this pre-release** — see *Fixed*. The fixture now defaults to a version-anchored image
  that can create a database, and the image is overridable without deriving a type. The earlier advice to
  *"pin it by digest"* is withdrawn: a digest is architecture-specific and would have broken arm64
  consumers, which a tag does not.
- **Integration coverage has known failures.** Cosmos DB integration tests are **no longer excluded from
  CI** (see *Changed*), but that change is newer than any run we have published — **we have not yet
  published a build in which they executed and passed.** Until we do, treat the Cosmos DB provider as
  materially less proven than the others and validate the operations you depend on against your own
  infrastructure. When those tests are run manually, some do not pass, and we have not resolved those
  failures. A recent full run also showed failures outside Cosmos DB, but almost all were test containers
  failing to start on the machine running the suite — a local resource limit rather than provider defects,
  and we are not reporting them as such.
- **Unexplained not-found responses from the Cosmos DB snapshot store.** Cause not determined, and we
  have not established whether it originates in the provider or in our own test setup. If you see one
  where data should be present, do not treat it as authoritative, and please report it.

This list reflects what we have classified, not everything that exists — see the What's New page for the
limits of that claim.

### Changed

- **`CircuitBreakerPattern` and `CircuitBreakerFactory` have been removed.** Nothing registered them: the
  only dependency-injection registration of `ICircuitBreakerFactory` was Polly's, so no application could
  resolve this breaker even deliberately. Its one remaining effect was harmful — being named as a concrete,
  non-virtual return type is what prevented the Polly breaker from running at all (see *Fixed*). The
  circuit-breaker contract (`ICircuitBreakerFactory`, `IResiliencePattern`) still ships in the core package;
  the implementation ships in `Excalibur.Dispatch.Resilience.Polly`, matching how the platform separates an
  abstractions package from its implementation. If you referenced either type directly, depend on
  `IResiliencePattern` and add the Polly package.

- **`CircuitBreakerMiddleware` now takes a `TimeProvider`.** It timed its open-duration deadline — the one
  that decides when a half-open probe is admitted — from `DateTimeOffset.UtcNow`, so that recovery path
  could only be exercised by sleeping in real time. It now reads an injected `TimeProvider`, and
  `AddDispatchPipeline` registers the system provider with `TryAdd` so your own clock still wins. You only
  need to change something if you construct the middleware yourself; resolving it from the container
  continues to work unchanged.

- **Cosmos DB integration tests are no longer excluded from CI.** The build previously filtered them out
  entirely, so a green build said nothing whatever about that provider's integration behaviour. The filter
  is removed, and an emulator readiness check now probes the data plane — creating a database rather than
  trusting a readiness endpoint — and **refuses** when the emulator is not genuinely usable, instead of
  quietly skipping. Any set of tests still skipped is enumerated in a reviewable allowlist rather than
  disappearing into a filter expression. **This does not yet mean the provider is verified**: it means a
  future green build will be evidence about it, where the previous one could not be. See *Known Issues*.

### Fixed

- **The Record-of-Processing-Activities data map failed on every call, on both SQL providers.** The query
  referenced a tenant parameter that was never supplied to the command, so the data-map read threw
  unconditionally on SQL Server and PostgreSQL alike — it could not succeed under any input. Every existing
  test for this path substituted the query store, so no query was ever executed against a database and the
  defect passed the full suite. The parameter is now bound, and the coverage gap is disclosed on the What's
  New page: repairing the query does not create the test that would have caught it.

- **Erasure and legal-hold reads were not scoped to a tenant.** A caller that omitted the tenant argument,
  or supplied another tenant's identifier, could read records belonging to a different tenant, including
  case references. Scoping is now applied across all three data-inventory implementations. The conformance
  kit could not previously observe this at all — with no tenant scope change between writes, its isolation
  check was comparing names rather than isolation — so it gains an explicit scope switch, declared as a
  required member so that a provider cannot silently omit it.

- **The Cosmos DB snapshot store could abandon a write without reporting it.** After exhausting its
  optimistic-concurrency attempts the save returned normally, so a caller was told the snapshot had been
  stored when it had not. It now raises a concurrency error carrying the version it expected and the
  version actually stored. The attempt bound is a guard against an unbounded spin, not a budget on how many
  writers may contend.

- **The Firestore snapshot store surfaced a transport exception on write contention.** Concurrent saves to
  one aggregate contend on a single document, and the loser received a raw gRPC status where every other
  provider reports a concurrency error. Contention is now retried within a bound — safe because the store's
  version guard makes a save idempotent, so a writer that is overtaken while waiting finds the newer
  version already stored and returns without writing — and reported as a concurrency error if the bound is
  exhausted.

- **Enabling encryption or telemetry on a document-store inbox silently weakened its delivery guarantee.**
  The inbox pipeline reserves its strongest path — the handler and the processed-mark committing together
  inside one provider-native transaction — for stores that advertise it, and it looks for that capability on
  the outermost store it is given. The encrypting and telemetry decorators did not carry the capability
  forward, so a decorated Cosmos DB or MongoDB inbox was no longer recognised and quietly fell back to
  at-least-once redelivery. Nothing failed and nothing was logged; the only symptom was a handler running
  more than once for a message you expected to be processed exactly once. Both decorators now forward the
  capability and report it based on what the store underneath them actually supports, so wrapping a store no
  longer changes its guarantee. A store that deliberately declines the atomic contract — such as a Cosmos DB
  container configured without the shared partition key its transactional batch requires — is still reported
  honestly rather than re-advertised as atomic. If you enabled inbox encryption or telemetry on a document
  store, your handlers were running under at-least-once delivery and needed to be idempotent; after this
  release they run under the guarantee the store advertises.

- **Disposing one Cosmos-backed store could break Cosmos access for the whole application.** The Cosmos
  snapshot store and saga store disposed their `CosmosClient` unconditionally, including when the client
  had been injected — and an injected client is normally a singleton shared by every consumer. Disposing
  a single store therefore left later operations elsewhere throwing `ObjectDisposedException: Accessing
  CosmosClient after it is disposed`, an error that pointed at the disposal rather than at whatever code
  happened to run next. Each store now disposes the client only when it constructed it, matching the
  ownership rule the MongoDB, DynamoDB and AWS stores already follow. If you inject a shared
  `CosmosClient`, you no longer need to keep every store alive for the process lifetime to avoid this.

- **Concurrent appends to DIFFERENT Firestore aggregates could fail each other with a transaction lock
  timeout.** The event store's optimistic-concurrency check ran as a transactional *query*, filtered on
  stream and ordered by version. A transactional query in Firestore locks the index range it scans, not
  merely the documents it returns, so appends to unrelated streams took overlapping range locks and
  aborted one another — aggregates that share no stream, and cannot genuinely contend, were contending.
  The check now uses point reads against the deterministic document ids, which lock exactly the documents
  named: the slot after the expected version must be empty, and the expected version itself must exist.
  If you saw sporadic concurrency failures under parallel writes to distinct aggregates, that is this.

- **The Polly circuit breaker never executed. If you registered it, you still got the built-in breaker.**
  `ICircuitBreakerFactory.GetOrCreate` declared a concrete return type, and that class declares no virtual
  members — so the Polly implementation could not override anything and instead re-declared every member
  with `new`. `new` hides rather than overrides and binds by the *static* type, so every caller, holding the
  declared concrete type, reached the built-in breaker's code. The Polly package was referenced, registered,
  constructed, and inert. The same wrapper also replaced your `CircuitBreakerOptions` with defaults on the
  way through, so any thresholds you configured were discarded. `GetOrCreate` now returns the
  `IResiliencePattern` abstraction that both breakers already implemented, and the Polly factory returns its
  adapter directly. **This is a breaking signature change**: if you assign the result to a variable typed as
  the concrete breaker, change it to `IResiliencePattern` (or `var`). If you selected Polly for its
  behaviour — its half-open probing and its metrics — you were not getting it before and are now.

- **The Cosmos DB client recipe we publish omitted the serializer, and following it produced a client
  whose point-reads silently miss.** Both the emulator fixture's documentation and the
  `Excalibur.Data.CosmosDb` README showed a `CosmosClientOptions` carrying only `LimitToEndpoint` and
  `ConnectionMode`. The Cosmos SDK's default serializer emits PascalCase property names, so a client
  built that way writes `Id` where Cosmos requires `id`, and a subsequent point-read by id finds
  nothing — with no error to indicate why. Both recipes now configure camelCase property naming, using
  the same shape our own stores use. **If you built a client from either recipe, add
  `SerializerOptions` with `PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase`;** documents
  already written by an unconfigured client carry PascalCase names and will not be found by an
  id-based read.
- **The bundled Cosmos DB emulator fixture defaulted to an image that could not create a database.** It
  pinned a floating `:latest` tag, which became ready, answered its readiness probe and then failed on
  first use — presenting as a timeout rather than as a broken image. It now defaults to a version-anchored
  image, and the version is stated in the fixture rather than implied.
- **The Cosmos DB emulator image could not be changed without deriving a type.** Choosing a different
  image required subclassing the fixture, which is not something a consumer of a testing package should
  have to do. It is now overridable through public API and through an environment variable, with the
  resolved image exposed so a test can assert which one it got.
- **Purpose-based key selection never worked in the Vault key provider.** `RotateKeyAsync` accepted a
  `purpose` and did not persist it, key metadata hardcoded the purpose to null, and `ListKeysAsync` then
  filtered on that field — so `GetActiveKeyAsync` with any non-null purpose could not return a key, for
  any key, under any configuration. The feature was inert rather than unreliable, on a shipped compliance
  provider. The purpose is now persisted alongside the key and read back into its metadata. Rotating with
  a null purpose preserves an existing one rather than erasing it, so a caller that rotates without
  restating the purpose does not silently drop it.

- **The legal-hold stores leaked the raw database exception when a hold was placed twice.** `ILegalHoldStore`
  requires `InvalidOperationException` for a duplicate, and both the SQL Server and PostgreSQL stores let
  the provider's own exception escape instead — SQL Server errors 2627 and 2601, PostgreSQL SQLSTATE 23505.
  A consumer writing `catch (InvalidOperationException)` therefore handled the duplicate correctly against
  the in-memory store and **missed it entirely against a real database**, so a legal hold could fail to
  register without the caller noticing. Both providers now translate it and preserve the original as
  `InnerException`.

- **The dead-letter queue's operator paths could not reach another tenant's entries.** The queue is an
  operator surface whose inspection, replay and purge are documented as estate-wide, but the scope every
  entry point resolved was never "no scope": an absent tenant context mapped to the untenanted
  partition, which matches only entries carrying the sentinel. An operator inspecting or replaying a
  tenant's dead letter got "not found". Estate-wide access now works as documented. This does not widen
  a multi-tenant deployment -- a host with tenancy registered still resolves a real tenant and stays
  scoped, and one whose context resolves nothing still fails closed; only a host with no tenant context
  changes, and there every entry carries the untenanted sentinel anyway, so the same rows are selected.

- **The SQL Server snapshot store could not read a table created by its own setup script.** The store
  materialised `CreatedAt` as a `DateTime` while the shipped `002_CreateSnapshotSchema.sql` declares it
  `DATETIMEOFFSET`, so no constructor matched the returned columns and every snapshot read failed
  outright. Anyone who ran the documented setup hit this on their first read. The store now reads the
  column as a `DateTimeOffset` and passes it through unchanged; previously it forced the value to UTC,
  discarding the offset for any non-UTC writer.

- **The Postgres snapshot store let an older snapshot overwrite a newer one.** Its upsert applied
  unconditionally, so with several instances snapshotting the same aggregate the last write won
  regardless of version and `GetLatestSnapshot` could go backwards. It now only ever moves a snapshot
  forward, matching the SQL Server store.

- **Concurrent snapshot saves failed on Oracle.** Two sessions could both take the MERGE's not-matched
  branch and the second raised `ORA-00001`. The save now retries once, which is sufficient: the row
  exists by then and the existing version guard decides the outcome.

- **DynamoDB never created its tables.** Two exception types share the name `ResourceNotFoundException`
  -- the AWS one and Excalibur's -- and the enclosing namespace wins over a `using`, so every `catch`
  in the DynamoDB package bound to the wrong type and the AWS one passed straight through. It compiled,
  so nothing reported it. `CreateTableIfNotExists` and `AutoCreateTable`, both defaulting to `true`,
  silently did nothing, and every operation failed with "Cannot do operations on a non-existent table"
  -- raised from inside the method whose job is to create that table. Affects the snapshot store,
  projection store, and persistence provider.

- **A saga completed at a non-UTC offset could not be saved on Postgres.** Npgsql accepts a
  `DateTimeOffset` for `timestamptz` only at offset zero and rejects anything else, so the save threw.
  The retention threshold had the same hole, so a sweep expressed in a local offset threw instead of
  purging. Both now normalise to UTC, preserving the instant.

- **The in-memory saga store disclosed other tenants' sagas.** `QuerySagas` applied only the *optional*
  tenant filter and `GetSummary` applied none, so a multi-tenant host received every tenant's saga ids
  and types whenever no filter was passed -- the default. Both are now scoped to the ambient tenant,
  matching the SQL providers. Statistics remain estate-wide for an unscoped caller, which is the
  intended operator diagnostic.

- **Elasticsearch inbox cleanup deleted entries it should have kept.** It filtered on age alone, so it
  removed `Failed` entries -- dropping the record of work still needing attention -- and `Pending`
  ones, dropping the deduplication record so the message would be processed again on redelivery, which
  is the one thing an inbox exists to prevent. It also keyed on `ReceivedAt` rather than `ProcessedAt`,
  so a long-running or recently-retried entry was deleted purely for having arrived early. Cleanup now
  removes only processed entries, past their processed time, as the SQL providers already did.

### Added

- **The Marten outbox now claims messages before dispatching them.** `GetUnsentMessagesAsync` was a
  plain query with no claim, so two dispatchers polling together both sent every message: duplicate
  delivery for as long as more than one instance ran, with nothing reporting it. Claims are taken
  atomically and held for `MartenOutboxStoreOptions.ClaimTimeout` (default 5 minutes), after which a
  crashed dispatcher's messages become available again; they are released as soon as a message is sent
  or returned for retry. Claims live in a table this store creates (`ClaimsSchemaName` /
  `ClaimsTableName`, default `public.excalibur_outbox_claims`) rather than in your Marten documents, so
  your serializer configuration cannot affect them. **Set `ClaimTimeout` above your longest expected
  send:** a shorter value lets a second dispatcher take a message the first is still working on.

- **The documented SQL Server CDC idempotency schema was missing a column the filter writes, and its
  primary key was wrong.** The published `CREATE TABLE` for the processed-events table declared
  `TableName`, `Lsn`, `SeqVal` and `ProcessedAt`, keyed on the first three. The filter writes and
  dedupes on `ConsumerId`, which the schema did not declare at all, so a table created from the
  documentation failed on the first processed event with `Invalid column name 'ConsumerId'`. The key
  mattered as much as the column: `ConsumerId` scopes the dedupe namespace to one consumer, and without
  it in the key the first consumer to process a change marks it done for every other consumer of that
  table — the others then skip a change they never saw, silently. That is the multi-instance
  configuration the same page recommends. The documented schema now declares the column and includes it
  in the key.

  **If you created this table from an earlier version of the documentation, alter it before upgrading:**
  add the `ConsumerId` column and rebuild the primary key to include it. A table created by hand without
  the column has never successfully recorded a processed event; one created with the column but keyed
  only on `(TableName, Lsn, SeqVal)` may have suppressed changes for every consumer after the first, and
  those changes will not be re-delivered by fixing the key alone.

- **Snapshots no longer leak or overwrite across tenants on SQL Server.** The SQL Server snapshot
  registrations built the store without a tenant context. That parameter is optional and defaults to
  none, so every tenant's snapshot was written under the framework's reserved untenanted marker: all
  tenants shared a single row per aggregate id, and whichever tenant saved last silently overwrote the
  others. Any multi-tenant host using the SQL Server event-sourcing registration was affected, and the
  loss was silent — reads returned another tenant's state rather than failing. The registrations now
  resolve the ambient tenant context. Single-tenant hosts are unaffected: with no tenant registered the
  scope remains untenanted, which is the correct behaviour for them.

- **The SQL Server outbox no longer reports completed messages as still sending.** Marking a message
  sent left its lease columns populated, while the statistics query counts in-flight work as any leased
  row without regard to status. Every message ever sent therefore stayed in that count permanently, so
  the sending total grew without bound for the life of the table and never returned to zero. Sending now
  releases the lease, and the count reflects work actually in flight.

- **DynamoDB snapshot stores no longer fail on first use against a new table.** Table creation is
  eventually consistent: for a short window after the table is created, describing it can still report
  it as missing. That error escaped the very routine responsible for creating the table, surfacing as
  "Cannot do operations on a non-existent table" on a cold start. The wait now treats a not-yet-visible
  table as pending rather than as failure, and is bounded — previously a table that never became active
  would wait forever holding the initialisation lock, hanging the caller instead of reporting an error.

- **Saving an existing saga no longer fails on Oracle.** The version-gated upsert referenced `TenantId`
  in the `MERGE` `ON` clause and also assigned it in `WHEN MATCHED THEN UPDATE SET`. Oracle rejects that
  combination outright (`ORA-38104: Columns referenced in the ON Clause cannot be updated`), so every
  save against a saga that already existed failed — the entire update path on this provider. Creating a
  saga was unaffected, which is why the failure only appeared once a saga advanced. The assignment has
  been removed: a matched row already carries the tenant the statement matched on, so writing it again
  was always a no-op. Tenant isolation is unchanged — the tenant term remains an unconditional part of
  the match, and new rows still stamp it on insert. Other providers were never affected; the restriction
  is specific to Oracle.

- **The workflow code-fix package now builds.** `Excalibur.Workflows.CodeFixes` pinned Roslyn 4.14.0
  while the analyzer package it depends on resolved 5.3.0, so it linked an assembly built against a
  version it did not load and the build failed on an unresolvable assembly conflict. Both now compile
  against Roslyn 5.3.0, with `System.Collections.Immutable` unified alongside it. A code-fix provider
  and the analyzer whose diagnostics it fixes have to agree on one Roslyn version; they now do.
  If you consume the workflow analyzers, they require a toolchain new enough for Roslyn 5.3.

### Removed

- **`ResilientElasticsearchClient` and `MonitoredResilientElasticsearchClient` are no longer public.** Both
  are implementation behind `IResilientElasticsearchClient`, which is unchanged and remains the supported
  way to depend on this behaviour. If you referenced either concrete type, depend on the interface. This is
  a binary-breaking removal, made deliberately while the API is still unfrozen; after the stable release it
  would have been reserved for a new major line.

- **The outbox cleanup builder and its retention settings.** No store read the values. A validator
  range-checked them, the documentation taught them, and a sample configured them, so the surface
  looked complete from every angle except the one that mattered. Configuring retention through this
  builder never changed what was retained, so it has been removed rather than documented: 13 public
  API entries are gone from the baseline. Provider-level retention that is genuinely wired is
  unaffected — five outbox providers expire delivered entries natively, and those paths still work.
  Consumers who set these values were not getting the behaviour they configured; see the per-provider
  retention table for what each provider actually does.

### Fixed

- **Saga loads, purges and summary queries no longer cross tenants on the unscoped path.** Every saga
  request built its tenant predicate conditionally — present when a tenant scope was set, and **absent
  entirely when one was not**. An unscoped load therefore matched on saga id and saga type alone and
  returned whichever tenant's saga held that id. This was the ordinary load path, not an exotic one, and
  it behaved this way in SQL Server, PostgreSQL and Oracle alike. The predicate is now unconditional:
  an unscoped caller resolves to the reserved untenanted partition rather than to "no predicate", which
  is the same resolution the write side already performed — so a saga saved under a given scope is
  loadable under that scope and no other. The saga tables are re-keyed on `(TenantId, SagaId)` to match.

  **If you have an existing saga database, run the upgrade script for your provider**
  (`SagaTimeouts.Upgrade.sql`, shipped in the SQL Server and Oracle packages). The create scripts are
  unguarded, so re-running them against a provisioned database will error; the upgrade scripts guard
  each statement on the condition it repairs and are safe to run repeatedly. Execute them with a tool
  that honours batch separators. A database provisioned from the current create script already has the
  correct shape and needs nothing.

  `SagaState.TenantId`'s documentation now states plainly that **the strength of this binding differs by
  store**, and that a populated value does not by itself mean the store refuses a cross-tenant read —
  under the weaker shape the row leaves the database and enters your process before it is rejected,
  which is materially different for compliance, for memory, and for a crash dump.

- **Tenant key columns in the shipped schemas now pin a binary collation.** SQL Server's default
  server collation is case-insensitive, so `Acme` and `acme` resolved to one row set in the database
  while the framework compared tenant identity case-sensitively in .NET. The two layers disagreed and
  the storage layer was the permissive one, which means a tenant-scoped lookup could return another
  tenant's rows. The schema scripts and the SQL examples in the documentation and samples now declare
  these columns `COLLATE Latin1_General_BIN2`, matching the comparison the framework performs.
  Oracle expresses this differently — a column-level `COLLATE` is not portable there and would fail
  the install on a standard configuration — so Oracle is handled at the session level instead.
  The upgrade scripts remediate existing databases as well as fresh installations. They key on the
  column's actual collation rather than on whether the column exists or is nullable, because a column
  can be present, `NOT NULL`, fully migrated, and still carry the wrong collation — an earlier form of
  these guards skipped exactly the already-multi-tenant databases where a cross-tenant match is
  possible. Re-running a script against an already-correct database changes nothing. Four tables are
  covered: `OutboxMessages`, `OutboxMessageTransports`, `DeadLetterQueue`, and `inbox_messages`.

- **Audit queries are scoped to the ambient tenant instead of filtering on a caller-supplied one.**
  A query that omitted the tenant returned every tenant's audit events, and a query naming another
  tenant returned theirs. Four audit stores carried the defect. The tenant term is now bound from
  the ambient context on every read and the caller-supplied argument is not consulted — matching
  the behaviour our documentation already described. Hosts that relied on passing a tenant argument
  to read another tenant's audit trail were relying on a disclosure bug; use an ambient scope.

- **The outbox sample's schema now creates the `OutboxFence` table.** The drain names it
  unconditionally, so a consumer who followed the sample verbatim built a schema that failed on the
  first drain — including on a single instance, where nothing suggests a fence is involved.

- **Outbox retention is documented per provider instead of as one claim about all of them.** The
  pages described retention as unbounded by default, including in an erasure context; five of eleven
  providers expire delivered entries natively. A reader could have built a deletion mechanism they
  did not need, or described their own retention posture inaccurately.

- **A dead-letter warning that told consumers their reads were not tenant-scoped has been removed.**
  The reads are scoped. The warning instructed readers not to expose either interface to a tenant,
  which was false and more damaging than the gap it originally described.

- **Conformance kits are attributed to the correct package and path**, including in two compliance
  checklists, and an unsupported test count has been dropped rather than replaced with another.

- **The inbox and its processor no longer advertise exactly-once delivery.** The XML documentation
  that ships in the package promised exactly-once while the subsystem's guarantee contract states
  at-least-once. A consumer reading IntelliSense at the moment they decide whether their handler
  needs to be idempotent was being told it did not. It does.

- **Crypto-shred documentation now matches what the decorators implement.** The markers promised
  per-subject shredding; the decorators use a single default context. The claim is scoped to what
  the type can actually express.

- **`AddMongoDbComplianceStore` now produces a resolvable store.** Both overloads register the
  single-tenant default context that `MongoDbComplianceStore` requires, so calling either one and
  nothing else yields a working `IComplianceStore` instead of throwing on first resolve. Hosts that
  compose multi-tenancy are unaffected — the ambient context still wins.

- **The legal-hold conformance kit can now detect a tenant leak.** Its fixture held a single tenant,
  so a store that ignored the tenant argument entirely returned that tenant's holds and passed
  certification. The kit now seeds two tenants and asserts both arms: a scoped read never returns
  another tenant's hold, and still returns its own. Implementers who passed the old kit should re-run
  it — a green from the previous version did not establish tenant scoping.

- **The audit annotation store binds its untenanted sentinel as a parameter instead of interpolating
  it into the statement text.** The interpolated form was safe only by accident — the value is a
  framework constant, never consumer input — but it placed a framework value inside a SQL string, a
  shape that stays safe only while nobody changes where the value comes from. The predicate is now
  byte-identical to the one the audit store emits.

- **`SqlServerAuditAnnotationStoreOptions.EventsTableName` is validated as non-empty at startup.**
  An annotation derives its tenant by joining the audit events table. With the name blank the store
  did not fail loudly — it resolved a join against the wrong table and every tenancy predicate
  silently matched nothing.

### Added

- **`Excalibur.EventSourcing.SqlServer` now ships its schema.** The package persisted to an event
  store and a snapshot table it gave you no way to create — its Oracle and PostgreSQL siblings ship
  schema scripts and this one did not, so adopting it meant reconstructing the schema by reading the
  store's queries. Both scripts are now in the package under `scripts/`.

- **The audit conformance kit now has an arm for unscoped queries.** A query that omits a tenant
  returned every tenant's audit events, and nothing in the kit tested for it. The new arm fails on
  that behaviour and runs against real Postgres and SQL Server containers, not only the in-memory
  store — implementers who passed the previous kit should re-run it.

- **Two build-honesty gates that cannot report a pass they did not earn.**
  `assert-compiled-not-skipped` reads a build log and answers whether the code was actually
  compiled, refusing (a distinct exit, never a pass) on a log it cannot interpret or that is
  absent; `aot-publish-validation-exit` refuses to turn a failed AOT publish into a green
  verdict. Both ship with self-tests covering the safety arm (a bad input is refused) and the
  liveness arm (a good input still passes), so neither can go quiet and be mistaken for clean.

### Changed

- **Outbox transport rows now record the tenant of the message they belong to.** The
  `OutboxMessageTransports` table gains a `TenantId` column, and `InsertTransportDeliveryRequest`'s
  constructor takes the tenant term so the insert can write it. The value comes from the parent outbox
  message inside the same transaction, so a transport row's tenant always equals its parent's — it is
  not read from ambient context, because the store deliberately reads none. **If you upgrade the schema
  without upgrading the package, transport rows written in between take the column's `'__untenanted__'`
  default rather than their real tenant**; the two belong together and are released together. This is a
  breaking change for anyone constructing `InsertTransportDeliveryRequest` directly.

  **The scoping of this seam is partial in this release, and the halves are not symmetric.** Reads are
  constrained to the caller's tenant — a caller holding another tenant's message id is no longer handed
  that tenant's transport rows. The three status transitions that mark a delivery sent, failed, or
  skipped are **not** yet constrained: they key on the message id — the same identifier the read path
  takes — so a caller able to reach the read is equally able to transition another tenant's row, marking
  it sent, failed, or skipped. Recording a tenant on a row is not the same as enforcing it on every path that
  touches the row, and a partially-scoped seam reads as a finished one unless it says otherwise — which
  is why this is stated here rather than left to be inferred. If you are running multi-tenant on this
  pre-release, do not treat transport-delivery status transitions as tenant-isolated.

- **`TenantScope.UntenantedSentinel` is now `static readonly` rather than a `const`.** A `const` is
  inlined into consuming assemblies at their compile time, so the literal is copied into every consumer
  binary instead of being resolved from ours. Had it shipped that way the value could never have been
  corrected: an application built against a stable release would keep its inlined copy until it was
  recompiled, and a package upgrade cannot force that. As a `static readonly` field the value resolves
  through the assembly at run time, so any future correction reaches consumers when they upgrade. This
  is binary-breaking for assemblies already compiled against a pre-release build — recompile against
  the current package. The value, the comparisons, and the reserved-sentinel rejection are unchanged.

- **`IColdEventStore.WriteAsync` now returns the durable watermark instead of `Task`.** It returns the
  highest version whose entire prefix is durably committed in cold storage, and the archive service deletes
  hot events only up to that value — so a partial or deferred cold write now bounds hot deletion rather than
  destroying the only remaining copy of not-yet-archived events. An empty batch returns `-1`. This is a
  breaking change for anyone implementing `IColdEventStore`: return the submitted maximum **only after** the
  storage receipt confirms durability, never before.

- **Keyed stores can no longer emit a statement without a tenant term.** Stores whose unique key includes
  the tenant column — inbox, saga, snapshot, dead-letter queue, and tenant-columned event stores — now bind
  their tenant term through a partition type with exactly two inhabitants (a validated real tenant, or the
  reserved `__untenanted__` sentinel) and no empty-term inhabitant, no public constructor, and no default.
  A read, erase, or replay matching every tenant's rows is therefore unconstructable rather than merely
  discouraged. Column-agnostic append-log requests keep the existing scope type, whose `None` case still
  deliberately emits no term.

  **Schema impact:** keyed tables declare their tenant column `NOT NULL` and include it in the unique key;
  a single-tenant host stores the sentinel rather than `NULL`. Backfill legacy `NULL` tenants to
  `__untenanted__` **before** applying the constraints, or those rows stop matching any tenant predicate.
  The dead-letter queue additionally carries the originating tenant as provenance so a replay re-enters the
  same tenant.

  **Known gap:** cold/archive storage carries no tenant term on any method, so the guarantee does **not**
  extend to the cold tier. Isolate archived events per tenant yourself (separate container, bucket, or
  prefix) and treat cold reads as unscoped.

### Changed

- **`IDeadLetterQueue.ReplayBatchAsync` returns `ReplayBatchResult` instead of a bare count.** The previous
  signature capped the batch and returned only how many messages it replayed, so a caller could not tell a
  fully-drained queue from one that still held messages — an operator reading that number would reasonably
  conclude the work was finished. The result now reports what was enumerated, what was replayed, and whether
  the batch was truncated. **Breaking for anyone calling `ReplayBatchAsync`:** read `Replayed` for the old
  value, and check `Truncated` before treating a replay as complete.

### Fixed

- **The data-inventory conformance kit's tenant-isolation arm could not fail for cross-tenant disclosure.**
  The assertion was an `OR` — it passed if either the caller's own row or the untenanted row was returned,
  and it passed equally when a *second tenant's* rows were returned alongside them. A second tenant's
  registration was created, saved, and then never referenced, so the check that would have caught
  disclosure did not exist. The arm now requires each expected row independently and fails on any row
  belonging to another tenant. **If you certified a store against the previous kit, that certification did
  not test tenant isolation** — re-run it.

- **The Postgres GDPR compliance store had no tenant term, so one tenant could revoke another's consent.**
  Consent, erasure-log and subject-access rows carried no tenant discriminator, and the consent upsert
  conflicted on `(subject_id, purpose)` alone. Two tenants recording consent for the same data subject
  collapsed onto a single row: one tenant's withdrawal silently revoked the other's grant, and a grant
  silently reinstated a consent the other tenant's subject had withdrawn. Reads keyed the same way, so one
  tenant's `legal_basis` was returned to another. The tenant term is now bound at every SQL site and leads
  every key. The three tables are also provisioned rather than assumed to exist, with `tenant_id TEXT NOT
  NULL` in each primary key — `NOT NULL` is load-bearing rather than stylistic, because `NULL != NULL` makes
  every untenanted row distinct and silently disables the constraint it appears to add. The MongoDB
  compliance store carried the same defect — a unique index on `(subject_id, purpose)` with no tenant term,
  so the collapse was enforced by the database itself — and is fixed alongside it. **Migration:** existing
  consent, erasure-log and subject-access rows have no tenant value; assign them one before applying the
  new keys, or rows that belong to no tenant stop matching every tenant-scoped read.

- **Dead-letter stores kept message bodies with no tenant concept at all.** The poison-message store family
  paralleled `IDeadLetterQueue` but had no tenant term, so failed-message payloads were readable across
  tenants. All three implementations and their registration paths now scope by tenant, and the tenant
  context is supplied by the DI factories rather than at the call site, so a consumer cannot present an
  unwired store as a tenant-scoped one. The conformance kit asserts both directions — a scoped read does not
  observe another tenant's entry, and does still return its own — so a store that returns nothing to anybody
  cannot pass.

- **The outbox was documented as exactly-once; it is at-least-once.** Six consumer-facing pages described a
  delivery guarantee the implementation does not provide, contradicting the subsystem's own architecture
  contract. A message may be delivered more than once, the duplicate window is bounded by the retry floor,
  and handlers must be idempotent. Documentation describing exactly-once *processing* via inbox or
  idempotent-consumer dedupe is unchanged and remains accurate.

- **A cloud-snapshot archive sample taught permanent data loss.** The manual archive runner wrote events to
  cold storage, discarded the returned watermark, and deleted hot events up to the version it had *requested*
  rather than the version cold storage had durably stored. On any partial or deferred cold write it deleted
  the only surviving copy. The sample and its README now mirror the framework's own archive seam.

- **Recording a data-breach notification no longer claims subjects were notified when nothing was sent.**
  The built-in `IBreachNotificationService` records breach state and has **no notification transport** — it
  cannot deliver anything to anyone. It nevertheless set the breach status to "subjects notified", stamped
  the notification timestamp, and logged a successful send. That value then flows into the compliance
  evidence a customer hands to an auditor, so the framework was manufacturing an attestation that a GDPR
  Article 34 obligation had been discharged when no subject had been contacted. `NotifyAffectedSubjectsAsync`
  now **throws** rather than record a delivery it did not perform, and when `AutoNotify` is enabled the same
  refusal is raised from `ReportBreachAsync` — but only *after* the breach and its Article 33 deadline have
  been persisted, so the report survives and only the false claim is lost. Register an
  `IBreachNotificationService` implementation that performs real delivery, or disable `AutoNotify`.

- **Registering separation-of-duties enforcement with an empty policy store now fails at startup.** With
  zero policies loaded, every separation-of-duties check reports "no conflicts" regardless of the grants
  held — enforcement is registered, advertised, and completely inert, and the result is indistinguishable
  at evaluation time from a genuinely conflict-free request. A startup check now throws when the configured
  policy store loads no policies, naming the store type. Load the policy set the deployment expects, or do
  not register separation-of-duties enforcement on hosts that genuinely have none.

- **Outbox retention cleanup now declares that it deletes across every tenant.** `CleanupSentMessagesAsync`
  was a range delete over the tenant-bearing outbox table, running under a store exemption that justifies
  global reach for statements addressing a globally-unique id — but this statement addresses no id at all,
  so it had inherited an exemption that never covered its shape. Retention sweeping *is* definitionally an
  operator-wide operation, so the behavior was correct; its undeclared globality was not. **Renamed to
  `CleanupAllTenantsSentMessagesAsync`** (and the bulk-cleanup sibling likewise) across the interface, all
  eight provider stores, the bulk-cleanup adapter, and the conformance kit, so a caller cannot reach
  cross-tenant deletion without naming it.

- **A host registering leader election could resolve nothing and drain its outbox unfenced.** The
  standalone Postgres and SQL Server registrations added `ILeaderElection` only under a service key, but
  consumers of a single leader election — including the outbox leader gate — resolve `ILeaderElection`
  directly, and a keyed registration does not satisfy an unkeyed request. The result was no leader gate
  at all, so an outbox drain proceeded without one.

  Both providers now also register the interface unkeyed, via `TryAdd` so a consumer's own registration
  still wins.

- **Saga stores now scope tenant access on every keyed path, and refuse rather than guess when they
  cannot.** Previously the four document-store providers (MongoDB, Cosmos DB, DynamoDB, Firestore)
  persisted a tenant on each saga and never filtered on it, so a read keyed by saga id could return
  another tenant's saga. On Cosmos, a save at another tenant's saga id read that tenant's document for
  its version, found the versions in agreement, and **overwrote it** — optimistic concurrency was
  satisfied because the version checked belonged to the victim.

  All four now carry the tenant as a first-class, filterable field and check ownership on every keyed
  operation, including the read-modify-write path that no query filter can cover. Purges scope to the
  caller's tenant or refuse. **A tenancy violation now raises a distinct exception type**, so it cannot
  be mistaken for a concurrency conflict and retried indefinitely.

  **Retry classifiers now refuse to retry a tenancy violation.** Both retry surfaces previously defaulted
  to retrying an unrecognised exception, so the new distinct tenancy type would have been retried
  indefinitely — a caller looping forever on a write that can never succeed. The floor composes with
  `AND`, so a consumer's own predicate can narrow what is retried but never widen it back.

  **Integration coverage for these paths is still being written**; the behaviour is implemented and
  verified by build and unit tests only.

- **Oracle snapshot pruning ignored tenancy entirely and could delete another tenant's snapshots.** The
  age-based prune declared a tenant predicate and then never placed it in the statement, so the `DELETE`
  matched on aggregate and version alone. A prune run by one tenant removed **every** tenant's snapshots
  for that aggregate below the cutoff. Snapshots are rebuildable from the event stream, so this is a
  loss of the optimization rather than of history — **replay after such a prune is correct but slow.**

- **SQLite and SQL Server snapshot stores now apply tenant predicates unconditionally**, matching the key
  their writes already use. Previously a read or delete taken without a tenant context emitted no tenant
  filter and could match any tenant's row.

  **Oracle briefly regressed during this change and is now fixed.** An interim revision compared against
  `NVL(@TenantId, '')`, but Oracle converts the empty string to `NULL`, so that predicate was never true and
  a single-tenant host could not read back the snapshots it wrote. Oracle now uses `TENANTID IS NULL` for
  the untenanted partition, which is the only predicate that can address it on that platform. Confirmed
  against a live Oracle instance.

- **SQLite snapshot store: an unscoped read could return another tenant's snapshot.** The write path
  stored every row under a `COALESCE(@TenantId, '')` sentinel and keyed the upsert on
  `(AggregateId, AggregateType, TenantId)` — the tenant was always part of the key. The read and delete
  paths, however, emitted a tenant predicate only when a tenant context was present, so a read taken
  **without** one carried no tenant filter at all and matched any tenant's row for that aggregate.

  Neither half was wrong on its own; they disagreed about whether the tenant is part of the key. Reads
  and deletes are now unconditional and agree with the write. **A single-tenant host is unaffected** —
  its rows live under the sentinel and continue to match. **A multi-tenant host that ever resolved a
  snapshot outside a tenant scope should treat those reads as suspect.**

### Changed

- **`AddDefaultDispatchPipelines()` no longer forces strict security.** It now registers a non-strict
  working default (default and event pipelines, no `Required` security middleware) that builds out of the
  box. Call the new **`AddStrictDispatchPipelines()`** for the fail-closed posture that refuses to start
  unless authentication, authorization, and validation are registered.

- **Compliance erasure stores default `AutoCreateSchema = false`.** The SQL Server and PostgreSQL erasure
  stores now verify their schema exists at startup and fail fast if it is missing, rather than provisioning
  it. Set `AutoCreateSchema = true` to restore automatic creation. If you relied on auto-provisioning,
  provision the tables from the store's own definition or opt back in explicitly.

- **The `Kind` property was removed from the built-in message types** (`CommandBase`, `JobBase`,
  `NotificationBase`, `QueryBase<TResponse>`, `MemoryMessage`, `CloudEventMessage`, `GenericDispatchMessage`,
  `TimerInfo`). A message's kind is derived from the dispatch interface it implements
  (`IDispatchAction<TResponse>`, `IDispatchEvent`, `IDispatchDocument`). Replace `message.Kind ==
  MessageKinds.Event` with a type check such as `message is IDispatchEvent`.

- **Audit, grant, and key stores expose an opt-in durability gate.** When enabled — and, for key
  durability, whenever compliance encryption is configured — the framework validates at startup that the
  registered store is durable and refuses to boot on a volatile one. Each store's `AllowVolatile…` option
  remains the deliberate way to accept a volatile store for development.

- **`PipelineProfile.CreateStrictProfile()` and `CreateInternalEventProfile()` have been removed.** Both
  factories returned a profile whose name announced a security posture the returned object did not
  configure — a "strict" profile that enabled nothing strict. A profile that misdescribes its own
  middleware is worse than no profile, because it is selected precisely by hosts trying to be careful.
  Build the profile explicitly, or select one whose declared middleware is enforced (see the pipeline
  profile documentation).

- **`InsertEventRequest` has been removed** from the SQL Server and PostgreSQL event stores. Event
  insertion goes through the batch path, which is the only shape the stores actually execute.

- **PostgreSQL and Oracle now ship a snapshot schema script.** Both providers carry a
  `001_CreateSnapshotSchema.sql` alongside the existing SQL Server script, so a consumer provisions the
  snapshot table from an artifact that matches the columns the store reads and writes rather than
  reconstructing it from documentation.

- **`ISnapshot.TenantId` is now a required interface member.** It previously carried a default
  implementation returning `null`, which meant a snapshot type that never implemented tenancy compiled
  cleanly and behaved as single-tenant — indistinguishable from one that had deliberately opted out.

  **If you implement `ISnapshot` directly, you must now declare `TenantId`.** Return `null` for a
  single-tenant host; return the owning tenant otherwise. **If you wrap or decorate a snapshot, forward
  the underlying value rather than returning `null`** — a wrapper that drops it silently reassigns the
  snapshot to no tenant, which the framework's own compression and encryption decorators did until this
  release.

  Types deriving from the supplied `Snapshot` record are unaffected; it already declares the member.

### Fixed

- **Snapshots are now keyed by tenant on every persistent store, so two tenants holding the same aggregate
  no longer overwrite each other.** The snapshot key was
  `(AggregateId, AggregateType)`. In a multi-tenant host the second tenant's save matched the first
  tenant's row and replaced it, and the save API reported success. No column distinguished the survivors,
  so the loss was not detectable from the data afterwards.

  The tenant now participates in the upsert's match key, not only in the inserted columns. Writing a
  discriminator without keying on it would leave the original key in place and lose data while appearing
  fixed.

  **Every persistent snapshot store now implements tenant scoping:** SQL Server, PostgreSQL (both
  providers), Redis, SQLite, Oracle, MongoDB, Cosmos DB, DynamoDB, Firestore and in-memory. Snapshot
  decorators — compression and encryption — forward the tenant rather than dropping it, so a snapshot
  passing through them keeps its owner.

  **Conformance coverage lags the implementation.** The provider conformance suites do not yet exercise
  tenant isolation on most stores, and two providers have no snapshot conformance suite at all. The
  behaviour is implemented on every store; it is not yet independently verified on every store.

  **Single-tenant hosts are unaffected on every provider.** Tenant scoping is conditional: an unscoped
  host emits no tenant column and its schema is unchanged.

  **Multi-tenant hosts must update their snapshot table.** The published schema declares the tenant
  column `NOT NULL` and includes it in the primary key. It carries **no default**, deliberately: the
  tenant is a component of the row's identity, not an optional filter, and a key column is not
  defaulted. With a default, an `INSERT` that omitted the tenant would land the row silently in the
  untenanted partition, making "I forgot to supply the tenant" indistinguishable from "this row is
  deliberately untenanted." Without one, that statement fails outright. Every published schema, sample
  script and initializer declares the column this way.

- **Consumer-facing messages no longer cite internal document numbers.** Package descriptions, a
  `NotSupportedException` raised on an unsupported change feed mode, and the strings the SOC2 control
  validators emit into compliance reports all referenced internal architecture-decision identifiers that
  are not published with the framework. The compliance-report case was the least appropriate: those
  strings reach an auditor, not a developer.

  Every technical statement is unchanged; only the unresolvable reference was removed.

- **A message type that declares no kind now receives every middleware, not the fewest.** A type
  implementing only the bare `IDispatchMessage` marker had no kind, and three separate classification
  sites defaulted it to `Document`. Document-kind messages are not covered by the authentication,
  authorization, or validation middleware, so the type the framework understood least received the
  least protection — silently, with nothing in the pipeline to indicate it.

  All three sites now route through a single fall-through: an unclassified type is treated as
  `MessageKinds.All`, so every middleware applies to it. Because failing closed is silent, and silence
  is how this survived, the fall-through also emits an `Activity` event naming the type and the
  interfaces it may declare — so the cause surfaces as itself rather than downstream as an
  authorization failure that names the wrong problem.

  Declare `IDispatchAction`, `IDispatchEvent`, or `IDispatchDocument` to choose a kind deliberately.
  `GenericDispatchMessage` now declares `IDispatchAction`, which is what it always was.

- **Disabling automatic schema creation now verifies the schema exists instead of assuming it.** With
  `AutoCreateSchema` set to `false`, the compliance stores created nothing, checked nothing, and marked
  themselves initialised. A deployment that manages its own schema — the reason to disable the option —
  got a store that reported itself ready against a database it had never looked at, and the first
  indication of a missing table arrived later, at an unrelated call site.

  The disabled path now verifies the schema is present and fails at initialisation with a message naming
  what is missing. Applies to the erasure, legal-hold and data-inventory stores on both the PostgreSQL
  and SQL Server providers.

  **The default is unchanged.** If you rely on automatic creation nothing about your setup changes; if
  you disable it, a missing schema is now reported when you start rather than when you first use the
  store.


- **Events dispatched through a profile that accepts them are now authenticated, authorized and
  validated.** Authentication, authorization and validation each declared that they apply to actions
  only — in both the applicability attribute and the interface property — so an event dispatched through
  a profile accepting actions and events reached none of the three. Input sanitization and tenant
  identity did apply to events, so the security surface was split with nothing stating that it was: a
  build that selected a security-enforcing profile and succeeded still processed events without
  authentication or authorization.

  All four affected middleware now apply to actions **and** events. Both the attribute and the interface
  property are widened together, because the applicability evaluator prefers the attribute and falls
  back to the property — changing only one leaves the narrowing in place while appearing to remove it.

  **If you dispatch events and relied on the previous behaviour**, events now pass through
  authentication and authorization, and an event that cannot satisfy them will be rejected where it
  previously proceeded. This is the intended behaviour of a security-enforcing profile.

- **`CloudEventMessage` and `TimerInfo` are now classified as events.** Both implemented only the base
  message interface, so the pipeline could not determine what kind of message they were and fell back to
  a default. Message classification is by type, so declaring the event interface is what makes the
  determination correct.


- **The AOT sample smoke test in CI can now fail.** The step ran the sample with `|| true`, under a
  `timeout`, and with `--no-build`. A crash was swallowed by `|| true`; a hang produced exit `124` and
  was swallowed identically; `--no-build` meant it executed whatever binary happened to be present. All
  three outcomes printed `AOT sample smoke test completed`, so the step reported success without ever
  being able to report anything else. It now captures the run's exit status and fails on a non-zero
  result, and distinguishes a timeout from a crash rather than treating both as success.


- **Selecting the `strict` pipeline profile no longer builds a pipeline with its security middleware silently missing.** A profile declared its middleware as bare types, with no way to say that an entry must be present, so every profile-sourced entry was optional by construction: if it could not be created it was skipped with a debug-level log and the pipeline reported success. A host that selected `strict` — the profile intended for external and partner input — without registering the services those middleware depend on built a pipeline **without authentication or authorization** and processed requests unauthenticated, with nothing logged above debug.

  Profile entries now carry a criticality. An entry marked `Required` that cannot be materialized **fails the build**, naming the service it needed and listing every unresolved entry together so they can be fixed in one pass. An entry marked `Optional` is still skipped, so an unwired outbox or audit sink does not prevent a correctly configured application from starting.

  All entries in the shipped profiles now declare their criticality explicitly. In `strict`, the five middleware that **enforce** a boundary — rate limiting, authentication, tenant identity, input sanitization and authorization — are `Required`. The remaining entries, and every entry in the `default`, `internal-event` and `batch` profiles, are `Optional`; `default` in particular runs on a zero-configuration setup where none of its entries materialize, and must continue to build.

  **A profile you implement yourself can now mark an entry `Required` too.** The previous guidance to avoid profiles for security middleware and use explicit `Use…()` calls instead no longer applies.

  **One limitation remains and is not closed by this change.** `Required` guarantees a middleware can be **created**; it does not widen the message kinds that middleware **applies to**. Authentication and authorization declare that they apply to actions only, while `strict` accepts both actions and events — so **an event dispatched through `strict` is not authenticated or authorized**, even though the build-time check passed, because the check and the applicability filter answer different questions. Rate limiting, tenant identity and input sanitization do apply to events. If you dispatch events that carry an authorization boundary, enforce it in the handler or in middleware that applies to events, and do not read a successful `strict` build as evidence that events are authorized.

- **Middleware you add explicitly to a pipeline now fails the build when it cannot be created, instead of being silently dropped.** A middleware added through an explicit `Use…()` call is treated as an instruction rather than a suggestion: when it cannot be materialized from the service provider — because the middleware type itself is not registered, or because one of its own constructor dependencies is missing — building the pipeline now **fails**, and the error names every unresolved entry together with the reason for each. Previously the entry was skipped with a debug-level log and the pipeline reported success, so a host could start and process traffic without middleware it had explicitly asked for. The failure occurs at startup rather than on first dispatch.

  **The profile-selection gap this originally left open has since been closed — see the next entry.**

- **Shipped database schemas in the documentation and samples did not match the columns the code writes — including one that instructed storing personal identifiers in plaintext.** Several `CREATE TABLE` scripts published in the documentation and sample projects had drifted from the code that reads and writes those tables, and a consumer who provisioned from them got a database the framework cannot use correctly.

  **If you provisioned any of the following from our documentation or samples, check your schema:**

  - **GDPR erasure request table.** The published schema declared the data-subject identifier as a plaintext column and omitted most of the columns the store writes. **The store hashes that identifier before persisting it** (keyed HMAC-SHA-256 with a required pepper) and never stores it in the clear. A consumer following the published script created a table whose column set the store could not populate, and whose naming invited storing subject identifiers in plaintext — on the page describing how to honour erasure requests. **The erasure store provisions its own schema and always did; the published script should never have been there and has been removed.** Both the SQL Server and PostgreSQL erasure stores create their tables on first use.
  - **Event store tables.** Four published schemas were not merely missing columns but used different column names than the insert path, so a consumer following them failed on the first append.
  - **Event store payload column nullability.** Sample schemas declared the event payload column `NOT NULL`, while GDPR erasure works by nulling that column. Erasure against a schema provisioned from those samples could not succeed — and the failure surfaced only when a data subject exercised a right.

  All published schemas now match the code that writes them, and the schemas the framework provisions itself are no longer duplicated in documentation where they could drift again.

- **Released packages are now signed, and a release that cannot sign refuses to publish.** Package signing had never actually run. The rehearsal workflow guarded its signing step with a condition that could never be true — it tested an environment variable declared in that same step's own `env:` block, which is not in scope for the step's own `if:`, so the condition always read an empty value and only the "signing skipped" branch was reachable. The production release workflow had no signing step at all, so packages were pushed to the public feed unsigned. Signing is now declared at job scope in both, and the release workflow validates the certificate and signs before pushing. A missing certificate now **fails the release** rather than falling through to an unsigned publish: an unsigned package on a public feed cannot be recalled, so this is treated as a supply-chain integrity control rather than optional infrastructure. The rehearsal workflow still skips with a notice, since a rehearsal that cannot sign is degraded rather than dangerous.

- **Oracle outbox: reporting a message failed on a reserved row is no longer a silent no-op.** When the Oracle store reserves a row it stamps a per-claim dispatcher id of the form `{dispatcherId}:{token}`, but its mark-failed and mark-backoff guards matched the bare `dispatcher_id` — so on the reserved drain path every failure matched zero rows: the retry count was never incremented, the error was never recorded, the retry floor was never stamped, and the lease was never released. A poison message was therefore never dead-lettered and failures were invisible to `GetFailedMessagesAsync`. The guards now match on an exact dispatcher-id prefix (no `LIKE` wildcards), so a failure report from the owning processor is applied and one from a superseded processor is a no-op. The same defect in the crash-recovery reservation reset — which could clear a *foreign* live reservation and double-deliver — is fixed the same way. (Oracle only; the other providers were already correct.)
- **PostgreSQL projection store validates filter and order-by property names, closing a SQL-injection vector.** `PostgresProjectionStore` interpolated projection filter keys and `QueryOptions.OrderBy` values directly into the generated SQL, so a crafted property name could break out of the JSON-path expression. Both are now validated against a strict identifier pattern and rejected with `ArgumentException` before any SQL is built, matching the SQL Server projection store, which was already guarded. The tenant predicate was already parameterized and was never exposed.
- **PostgreSQL and Oracle outbox reservation timeout is measured in seconds, not milliseconds.** Both stores wrote the reservation window as seconds but consumed it as milliseconds, so the default 300-second window became 300 ms — a claim expired mid-send under normal latency and another dispatcher re-claimed the in-flight message, double-delivering on the exactly-once path. The unit is now seconds end-to-end with the intended default.
- **Outbox `MarkFailedAsync` now behaves identically across every provider.** The in-memory, PostgreSQL, Oracle, and SQL Server outbox stores previously diverged on what marking a message failed meant — terminal drop, immediate re-claim (a retry hot-loop), or lease-timeout expiry — and the conformance suite did not catch it. All four now share one contract: a failed message is not re-claimable until a failure-anchored retry floor elapses (so a failing message cannot busy-loop), a processor that does not own the reservation cannot release it by reporting failure, and the recorded retry count never decreases under a late, lower-count failure report.
- **`IntervalSnapshotStrategy` rejects a non-positive interval.** Constructing the strategy with an interval of zero or a negative value now throws at construction, instead of silently never taking a snapshot.
- **Telemetry context enrichment fails open when an enricher throws.** `ContextEnrichingExporter` now skips and logs an enricher that throws rather than propagating the exception into the export pipeline, so a faulty enricher can no longer break telemetry export.
- **PostgreSQL outbox: scheduling a message with a non-UTC `DateTimeOffset` no longer fails to stage it.** `ScheduleAsync`/`EnqueueAsync` bound `ScheduledAt` and `NextAttemptAt` as raw `DateTimeOffset`, which Npgsql rejects for any value whose offset is not zero (`Cannot write DateTimeOffset with Offset=-05:00:00 … only offset 0 (UTC) is supported`). On a host in any non-UTC timezone the natural call — `ScheduleAsync(message, DateTimeOffset.Now.AddMinutes(5))` — therefore **threw and the message was never staged**, while the same call on a UTC host succeeded. Both parameters now normalize to UTC before binding, so scheduling behaves identically regardless of host offset.
- **PostgreSQL outbox: marking a message failed no longer releases another processor's reservation.** `MarkFailedAsync` cleared `dispatcher_id`/`dispatcher_timeout` unconditionally, so a late failure report from a superseded processor would release the reservation held by the processor that currently owned the message — making it immediately claimable by a third while still in flight, a double-delivery window on the exactly-once path. The update now matches only an unreserved row or the caller's own reservation; a report against someone else's live lease is a no-op. Staging a message and reporting it failed without ever reserving it remains supported.
- **Single-tenant applications can resolve a saga store again.** The SQL Server, PostgreSQL, and Oracle saga registrations did not register a default `ITenantContext`, so resolving `ISagaStore` in an application that had not configured multi-tenancy threw at resolve time. All three now call `AddDefaultTenantContext()` (as the other 11 store registrations already did); it is `TryAdd`-based, so an application that registers ambient multi-tenancy still wins and the tenant-isolation guarantee is unchanged.
- **Oracle fenced outbox commands bind parameters by name.** The fenced claim/delete commands previously relied on ODP.NET positional binding, feeding the same fencing token and tenant scope through several duplicate positional parameters kept in hand-maintained order; a mis-ordering would have compared the wrong token silently. They now set `BindByName` so each named value binds once, resting the fencing invariant on the language rather than parameter order.
- **PostgreSQL and Oracle outbox now preserve the caller's `CreatedAt` and deliver same-partition messages in order.** When a staged message was reloaded during draining, the PostgreSQL and Oracle stores silently re-stamped its `CreatedAt` to the current time (a query column-alias/property mismatch left the persisted timestamp unhydrated) and dropped its `SequenceNumber` — so a consumer reading outbox timestamps saw the drain time rather than the enqueue time, and same-partition ordering was not enforced on those two providers. Both stores now carry the original `CreatedAt` and `SequenceNumber` through persist and drain-reload, and their claim query orders by `(partition_key, sequence_number, occurred_on)` for per-partition FIFO, matching the SQL Server store. **PostgreSQL outbox users must add a `sequence_number` column** (a non-null 64-bit integer defaulting to zero) to the `outbox` table — see [Outbox schema → PostgreSQL](docs-site/docs/patterns/outbox.md#postgresql) for the exact column definition.
- **MediatR compatibility layer no longer invokes the consumer `configure` callback twice.** `AddMediatRCompat(configure)` previously ran the callback once directly and again when `IOptions<MediatRCompatOptions>` materialized, double-firing any side effects inside it. The callback now runs exactly once.
- **Google Cloud Logging audit exporter now includes audit-event metadata.** The exporter previously dropped the `Metadata` dictionary from an audit event; it now emits each entry as a flat `metadata.<key>` field on the log payload (Google Cloud's payload model is string-keyed and cannot nest), so custom audit metadata is no longer silently lost.
- **PostgreSQL outbox: failed-message retrievability and scheduled-delivery timing corrected.** Marking a message failed now records the retry count and error on the row so it remains retrievable via `GetFailedMessagesAsync` and is counted by statistics; the dead-letter transition at the retry ceiling is owned by the outbox processor, not silently applied by the store on `MarkFailed` (matching every other provider). Separately, `scheduled_at` values now bind and round-trip as `timestamptz`, fixing a delivery-time skew that could shift a scheduled message by the host's UTC offset.
- **Oracle inbox is now tenant-scoped, matching the other multi-tenant providers.** The Oracle inbox store derives a tenant scope from the ambient `ITenantContext` on all keyed operations (dedup, claim, mark-processed), so inbox reads and idempotency are isolated per tenant — bringing it to parity with the SQL Server and PostgreSQL inbox stores. Previously the Oracle inbox was not tenant-scoped, which could cross tenant boundaries in a multi-tenant deployment.
- **The inbox dedup/claim key is now tenant-column-agnostic per deployment, closing a silent cross-tenant deduplication collision.** The relational inbox stores (SQL Server, PostgreSQL, Oracle) now select their physical schema by deployment mode instead of carrying a nullable `TenantId` on the pair key. A **single-tenant** deployment (the default) keys on the pair `(MessageId, HandlerType)` with **no `TenantId` column** — a single-tenant consumer pays nothing for a discriminator it never uses. A **multi-tenant** deployment (`AddMultiTenancy()` registered) keys on the triple `(MessageId, HandlerType, TenantId)` with `TenantId` **`NOT NULL`**, so two tenants sharing a `(MessageId, HandlerType)` can never dedup against each other. Previously the multi-tenant schema left `TenantId` nullable on the pair key, and because SQL treats NULLs as distinct, first-writer-wins never fired for an untenanted row — duplicates slipped through with no error. The store now **verifies the physical key against the registered mode at startup and fails fast on a mismatch** — a multi-tenant store can never silently run against the single-tenant (column-absent) schema, and vice versa. Each provider ships two labelled scripts (`001_CreateInboxSchema.sql` single-tenant, `001_CreateInboxSchema.MultiTenant.sql`) plus an expand-contract `002_MigrateToMultiTenant.sql` that grows an existing single-tenant table into the triple key, anchoring existing rows to the reserved `__untenanted__` sentinel. See [Inbox Pattern](docs-site/docs/patterns/inbox.md).
- **Snapshot encryption now applies on the keyed store-resolution path.** When a snapshot store is registered **keyed** (e.g. `"default"`, the shape the GDPR snapshot-erasure / repository path resolves), `AddSnapshotEncryption()` now re-registers the encrypting decorator **keyed-if-keyed**, so a `[PersonalData]` snapshot field resolved through `GetRequiredKeyedService<ISnapshotStore>("default")` is encrypted at rest. Previously the decorator was re-registered non-keyed, leaving the keyed resolution pointing at the bare store and persisting personal data in plaintext — a crypto-shred bypass.

### Changed

- **Datadog and Splunk audit exporters use the standard HTTP resilience handler.** Both exporters now delegate transient-failure handling to `Microsoft.Extensions.Http.Resilience` (`AddStandardResilienceHandler`) instead of a hand-rolled retry loop, which adds a circuit breaker and a total-request timeout the previous code lacked. Retry behavior is still driven by each exporter's existing retry options.
- **ASP.NET Core dispatch helpers now support value-type responses.** The `where TResponse : class` generic constraint has been relaxed on the minimal-API dispatch endpoints (`DispatchPostAction`/`DispatchGetAction`) and the controller dispatch extensions, so a message handler returning a value type (e.g. `Guid`, `int`) can be wired directly to an HTTP endpoint. Previously only reference-type responses compiled.
- **Outbox delivery is leader-fenced by default when leader election is registered.** When an `ILeaderElection` provider is present, the outbox drain now runs **single-active** (only the elected leader publishes) by default — guarding against split-brain double-delivery across instances — where this previously required explicit opt-in. An outbox store whose backend cannot provide the atomic fencing guarantee **fails fast at startup** rather than silently double-delivering. If exactly one process drains the outbox even though a leader election is registered for *other* resources (leases, scheduled jobs), assert that topology with `outbox.AsSingleWriter()` (or `OutboxDeliveryOptions.SingleActiveWriter = true`) to opt out of fencing — the processor then logs a warning and drains unfenced, which is also the escape hatch for a store that cannot express an atomic fencing high-water mark. Without a leader-election provider the outbox runs single-instance exactly as before.
- **In-memory inbox eviction now fails closed instead of silently dropping a live deduplication record.** When the in-memory inbox store (`AddInMemoryInboxStore()`) reaches `MaxEntries`, it reclaims a non-live entry (received/failed) first, then an already-processed entry past the retention window; if every entry is a live deduplication marker or in-flight claim still inside the retention window, it now throws `InvalidOperationException` rather than evicting a live marker. Silently dropping a live marker would let a redelivery re-admit and re-process the same message — the exact duplicate the inbox exists to prevent. Raise `MaxEntries` or shorten `RetentionPeriod` if the store legitimately needs to hold more concurrent in-window markers.
- **Recurring and scheduled message dispatch now read time through `TimeProvider`.** `RecurringDispatchScheduler` and `ScheduledMessageService` accept an optional `TimeProvider` (defaulting to `TimeProvider.System`), so scheduling decisions can be driven by a test clock (`FakeTimeProvider`) for deterministic tests. Existing registrations and runtime behavior are unchanged.

### Removed

- **`ILeadershipToken`.** The abstraction (and its internal Redis implementation) had no production construction path — nothing ever created one — and has been removed.

### Security

- **Audit annotation access checks can no longer be skipped by registration order.** The SQL Server
  package bound `IAuditAnnotationStore` to its own store while the core package bound the same
  interface to the role-checking decorator. Both used `TryAdd`, which is first-registration-wins, so
  whichever extension method a host called first decided whether access checks ran at all — calling
  `AddSqlServerAudit…` before `AddAuditAnnotations` produced an undecorated production store with no
  warning and no failure. **If you register the SQL Server audit annotation store, assume annotation
  reads were unfiltered and review who could reach them.** The interface now has a single binding
  that resolves the underlying store from a well-known key after all registration has completed, so
  ordering cannot affect the result. A host that registers no underlying store now fails at
  resolution instead of silently falling back to the in-memory one.
  The generic `AddAuditAnnotations<TStore>()` overload was initially missed and bound the interface
  straight to the supplied store; it now registers that store as the inner store like every other
  path. **All three registration overloads are covered**, and the store you supply is wrapped rather
  than replacing the decorator. Stated as an enumeration so you can check it: the only unkeyed binding
  of `IAuditAnnotationStore` is the decorator itself, and the in-memory default, a caller-supplied
  store, and the SQL Server store are all registered under the inner-store key.

- **Key-escrow split threshold must be at least 2.** A `SplitThreshold` of 1 — a "1-of-N" quorum any single custodian could reconstruct alone — is now rejected as a fail-fast startup configuration error, and `TotalShares` must be at least the threshold.
- **Shamir secret-sharing field arithmetic is constant-time.** The GF(256) multiply and inverse underlying key-share splitting and reconstruction are now branchless and constant-time, removing a timing/cache side channel from operations on secret key material.
- **`System.Security.Cryptography.Xml` updated to 10.0.10, closing an XML-encryption bypass.** The pinned 10.0.7 carries four high-severity advisories: three denial-of-service flaws in XML encryption handling, where crafted encrypted XML causes uncontrolled resource consumption (CVE-2026-50648, CVE-2026-50525, CVE-2026-47302), and — more seriously — a security-feature bypass in `EncryptedXml` that allows an attacker to circumvent encryption protections and read encrypted data (CVE-2026-47304, CVSS 8.1). The package reaches this framework transitively through `Microsoft.AspNetCore.DataProtection`, which backs the shipping security packages, so consumers using data protection were exposed even though no framework code calls `EncryptedXml` or `SignedXml` directly. All four are fixed in 10.0.10.

### Added

- **Tenant-scoped store registration (`AddTenantScopedStore`).** `services.AddTenantScopedStore<TContract, TStore>(Func<IServiceProvider, ITenantContext, TStore>)` registers a persistence store together with the tenant-scoping capability it advertises. The factory is handed the resolved `ITenantContext` and emits the capability marker in the same act — the marker is now **structurally inseparable** from the tenant-context dependency: a store built without the tenant context cannot be constructed and cannot advertise the capability, so a multi-tenant store can never advertise tenant isolation it did not actually receive. Applied across all relational Outbox / Inbox / Saga / event-store providers.
- **Single-active CDC with leadership fencing (`Excalibur.Cdc`).** When an `ILeaderElection` provider is registered, the change-data-capture pipeline runs **single-active** across instances — only the elected leader advances the change feed, and every checkpoint write is guarded by a **monotonic fencing token**. A demoted instance whose token has been superseded has its checkpoint write rejected with `CdcLeadershipSupersededException` (a terminal, non-retryable stand-down signal) instead of advancing the feed and double-processing changes. Without a leader-election provider CDC runs single-instance exactly as before — opt-in, no default behavior change. See [Change Data Capture](docs-site/docs/patterns/cdc.md).
- **Durable workflow signal inbox on SQL Server (`Excalibur.Workflows.SqlServer`).** `services.AddSqlServerWorkflowSignalInbox(o => ...)` provides a **restart-durable** backing store for workflow external signals — each `(instanceId, signalId)` is persisted with an idempotent conditional insert so a producer's post-restart redelivery is admitted exactly once, and signals drain in durable append order. `services.RequireDurableSignalInbox()` is an opt-in startup guard that **fails host start** when only the in-memory signal inbox is wired, turning silently-lost-on-restart signals into a fail-fast error rather than a runtime surprise. See [Durable Execution](docs-site/docs/event-sourcing/durable-execution.md).
- **Materialized-view delivery semantics — idempotent views can run on non-atomic stores.** A projection declares the guarantee it needs from its view store via `IMaterializedViewBuilder<TView>.DeliverySemantics` (a new `ViewDeliverySemantics` enum, defaulting to `ExactlyOnce`). `ExactlyOnce` (accumulating / non-idempotent projections, e.g. a running total) requires an `IAtomicMaterializedViewStore` that persists the view and its checkpoint atomically, and wiring one to a non-atomic store is **refused at startup**. `AtLeastOnceIdempotent` (upsert-by-view-id projections whose `Apply` is idempotent) tolerates the at-least-once replay a non-atomic store allows after a crash, so it may run on **any** view store — including Elasticsearch and OpenSearch, which have no cross-index transaction. The guarantee is an intrinsic property of the `Apply` logic, so the projection author declares it, not the deployment. See [Materialized Views → Delivery semantics](docs-site/docs/event-sourcing/materialized-views.md#delivery-semantics).
- **Durable execution / workflows (`Excalibur.Workflows`, `Excalibur.Workflows.Abstractions`).** A replayable workflow foundation whose progress survives process restarts. Register the engine with `services.AddWorkflows()`, an activity with `services.AddActivity<TActivity, TInput, TOutput>(name)`, and a workflow body with `services.AddWorkflow(name, body)`. A workflow body advances by invoking journaled activities via `IWorkflowContext.CallActivityAsync<TResult>(name, input, ct)`; on a crash the workflow resumes from its journal without re-running completed steps. Replay is exactly-once per step (deduplicated by instance id + step ordinal, short-circuiting already-completed activities), single-writer via optimistic concurrency (`WorkflowConcurrencyException`), with idempotent completion and a `WorkflowOptions.MaxReplayEvents` bound (default 10,000, validated at startup). The `IWorkflowContext` determinism surface covers journaled time (`UtcNowAsync`), identifiers (`NewGuidAsync`), durable timers (`CreateTimerAsync`), and external signals (`WaitForSignalAsync`); external signals are delivered exactly-once through `IWorkflowExecutor.SignalAsync` (deduplicated by a stable, producer-supplied `signalId`). The opt-in `Excalibur.Workflows.Analyzers` / `Excalibur.Workflows.CodeFixes` packages flag non-deterministic calls (`DateTimeOffset.UtcNow`, `Guid.NewGuid()`, `Task.Delay`, …) inside a workflow body at build time and rewrite them to the matching context member. See [Durable Execution](docs-site/docs/event-sourcing/durable-execution.md).
- **Per-subject crypto-shredding (`Excalibur.Compliance`).** `services.AddCryptoShredding()` registers per-subject field-level crypto-shredding — personal-data fields marked `[PersonalData]` on a type carrying a `[DataSubjectId]` are encrypted with a per-subject AES-256-GCM key generated from the key provider's CSPRNG, and destroying that subject's key (`ISubjectKeyManager.DestroyKeyAsync`) destroys **all** its key versions, rendering the data unrecoverable. The field cryptor is **fail-closed**: a `[DataSubjectId]` type that resolves zero `[PersonalData]` fields throws `EncryptionException` rather than silently persisting plaintext, an unsupported algorithm with a live key throws, and the reflection path is AOT/trim-safe via `[DynamicallyAccessedMembers]` rooting. Scoped to per-subject field-level crypto-shredding (store-wide erasure application is a separate capability). See [Crypto-Shredding](docs-site/docs/compliance/crypto-shredding.md).
- **First-class multi-tenancy (`Excalibur.Dispatch`).** `services.AddTenantContext(o => ...)` adds an ambient `ITenantContext` resolved by `ITenantResolver` from the message items (`excalibur.dispatch.tenant-id`), falling back to `TenantContextOptions.DefaultTenantId`. Setting `RequireTenant = true` makes a missing tenant **fail fast** with `TenantRequiredException` — a tenant-isolation guarantee rather than a silent default. Options are validated at startup and registered via `TryAdd` (overridable). On the persistence side, a single `services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator | Sharding)` call wires **first-class storage isolation** — `RowDiscriminator` wraps registered tenant-aware stores with tenant-scoped decorators, `Sharding` routes each tenant to its own shard (delegating to `EnableTenantSharding`). It is **fail-closed by construction**: an unset/invalid strategy, `RowDiscriminator` with no tenant-aware store registered, or `Sharding` without tenant routing enabled throws at composition time (and again at startup via `ValidateOnStart`) rather than leaving stores silently unscoped; the call is idempotent. Leader-election leases can be tenant-scoped and fail-closed via `ILeaderElectionFactory.CreateTenantScopedElection(...)` / `CreateTenantScopedHealthBasedElection(...)`, which throw `TenantRequiredException` when no ambient tenant is resolved. See [Multi-Tenancy](docs-site/docs/multi-tenancy.md).
- **MQTT transport primitives (`Excalibur.Dispatch.Transport.Mqtt`).** `services.AddMqttTransport(name, mqtt => ...)` registers a keyed `ITransportSender`/`ITransportReceiver` over an MQTT broker with configurable QoS (`AtMostOnce`/`AtLeastOnce`/`ExactlyOnce`), optional TLS, MQTT-5 shared subscriptions (`UseSharedSubscription` → `$share/{group}/{topic}` competing consumers), and a payload-size guard (`MaxPayloadBytes`, `null` opts out). Options are validated at startup; the receiver uses manual acknowledgement with withhold-ack redelivery. Transport primitives only (not yet pipeline-integrated). See [MQTT](docs-site/docs/transports/mqtt.md).
- **IBM MQ transport primitives (`Excalibur.Dispatch.Transport.IbmMq`).** `services.AddIbmMqTransport(name, ibmmq => ...)` registers a keyed `ITransportSender`/`ITransportReceiver` over an IBM MQ queue manager with a **unit-of-work per message** (ack commits, reject rolls back for redelivery), receive-batch tuning (`Receive.MaxBatchSize` 1–256, `WaitIntervalMilliseconds`), and a payload-size guard (`MaxPayloadBytes`). Options are validated at startup. Transport primitives only. See [IBM MQ](docs-site/docs/transports/ibm-mq.md).
- **Google Cloud Spanner connection foundation (`Excalibur.Data.Spanner`).** `services.AddSpannerDataProvider(o => ...)` registers `ISpannerConnectionProvider` (`CreateConnection`, `ExecuteInRetryableTransactionAsync`) with automatic abort-retry (`MaxAbortRetries`, default 5) against Google Cloud Spanner or its emulator (`EmulatorHost`). This ships the **connection foundation only** — the event store, outbox, inbox, and saga stores are not yet available on Spanner. See [Spanner](docs-site/docs/data-providers/spanner.md).
- **Oracle Database provider (`Excalibur.EventSourcing.Oracle`, `Excalibur.Outbox.Oracle`, `Excalibur.Inbox.Oracle`, `Excalibur.Saga.Oracle`).** Excalibur's reliable-persistence subsystems now run on Oracle Database via opt-in, Dapper-based packages over `Oracle.ManagedDataAccess.Core`, behind the same store abstractions as every other provider. Register the event store and snapshots with `services.AddOracleEventStore(...)` / `AddOracleSnapshotStore(...)`, the outbox through the outbox builder with `outbox.UseOracle(...)`, the inbox with `services.AddOracleInboxStore(...)`, and saga state with `services.AddOracleSagaStore(...)` (plus `AddOracleSagaTimeoutStore(...)` for durable timeouts). Each supports a connection factory or startup-validated options; event-store batch appends are atomic (no torn stream prefix on a mid-batch failure), and schema/table names supplied via configuration are validated and quoted against SQL injection. See [Oracle Provider](docs-site/docs/data-providers/oracle.md).
- **Apache Pulsar transport primitives (`Excalibur.Dispatch.Transport.Pulsar`).** Registers the low-level Pulsar transport primitives — a keyed `ITransportSender` and `ITransportReceiver` over the DotPulsar client that send, receive, and acknowledge messages against a Pulsar broker — with `services.AddPulsarTransport(name, pulsar => pulsar.ServiceUrl(...).Topic(...).SubscriptionName(...).SubscriptionType(...))`. Subscription modes map to Pulsar's native `Shared`/`Exclusive`/`Failover`/`KeyShared`, options are validated at startup, and the receiver enforces a payload-size guard (`Receive.MaxPayloadBytes`). This package intentionally ships the **transport primitives only**; high-level integration into the dispatch pipeline (an adapter that publishes and consumes typed dispatch messages end-to-end) is provided separately, and request/reply is not natively supported. See [Apache Pulsar Transport](docs-site/docs/transports/pulsar.md).
- **Aggregate handlers (Decider) and cascading (`Excalibur.EventSourcing.Handlers`).** `services.AddAggregateHandler<TAggregate, TKey, TMessage>(resolveId, decide)` registers a handler that routes a dispatched command to an event-sourced aggregate — resolve identity, load, apply the domain decision, save with optimistic concurrency — with the identity resolver and decision supplied explicitly (no reflection, AOT-safe). A missing aggregate surfaces as `ResourceNotFoundException` and a stale write as `ConcurrencyException`. A handler opts into **cascading** by returning a result that implements `ICascade`; the follow-up messages are staged to the same outbox the handler writes to, inheriting its delivery semantics (atomic with a transactional writer, otherwise at-least-once). See [Aggregate Handlers & Cascading](docs-site/docs/event-sourcing/aggregate-handlers.md).
- **Operational dashboard read-authorization policy and bounded result sizes.** `DashboardOptions.ReadActionsPolicy` gates every read endpoint behind a named authorization policy (symmetric with `MutatingActionsPolicy`; `null` keeps reads open by default). List endpoints (dead-letter entries, stuck sagas) now clamp their page size to `[1, MaxPageSize]`, falling back to `DefaultPageSize` (default `50`; `MaxPageSize` default `500`), validated at startup. See [Operations → Operational Dashboard](docs-site/docs/operations/dashboard.md#security).
- **Free OSS operational dashboard (`Excalibur.Operations.Dashboard`, `Excalibur.Operations.Dashboard.Spa`, `Excalibur.Operations.Dashboard.EventSourcing`).** A free, open-source, **read-only-by-default** operational dashboard surfaces live **outbox, dead-letter, inbox, saga, projection/CDC-lag, and leader-election** state across every configured storage provider — no paid license, no bespoke admin UI. Register with `services.AddDashboard()` and map with `app.MapDashboard()` (read API under `{RoutePrefix}/api`, embedded single-page app at `{RoutePrefix}`; `RoutePrefix` defaults to `/dashboard`). Point-in-time state comes from the existing per-subsystem admin read-models and throughput is derived from OpenTelemetry meters; a capability-discovery root endpoint (`GET {prefix}/api/`) lets the SPA render only the panels backed by a configured subsystem, and absent subsystems **fail open** (report not-configured) instead of returning 500. The SPA is served as embedded static assets (no external CDN) under a strict Content-Security-Policy, and the serving path plus source-generated JSON serialization keep the consumer path trim- and native-AOT-safe. The read API is **unauthenticated by default** — gate it by mapping the dashboard inside a parent `RequireAuthorization` route group when reads are sensitive (DLQ exception messages/correlation ids, saga tenant ids). **Mutating actions** (dead-letter replay) are **opt-in** via `DashboardOptions.EnableMutatingActions` (default `false`, so the mutating endpoint group is not mapped at all — 404, zero attack surface) and, when enabled, require an authenticated, authorized caller (`MutatingActionsPolicy`). The projection/CDC-lag panel ships in the separate `Excalibur.Operations.Dashboard.EventSourcing` add-on (`AddProjectionLagDashboard()`) so the base package has no event-sourcing dependency. See [Operations → Operational Dashboard](docs-site/docs/operations/dashboard.md).
- **Exactly-once transactional inbox on SQL Server, PostgreSQL, MongoDB, and Azure Cosmos DB.** The inbox stores can now run the duplicate check, the handler, and the processed-mark inside a **single provider-native transaction**, closing the crash window inherent in the default two-step claim protocol (which is exactly-once for concurrent redelivery but at-least-once across a process crash). On **SQL Server and PostgreSQL** the transactional path is **always on** — the handler runs inside a local `IDbTransaction` and the store reports `SupportsTransactional = true` unconditionally; a handler enlists its own commands on the same transaction via `context.GetInboxTransactionScope()?.AsSqlTransaction()`. On **MongoDB and Cosmos DB** it is opt-in per provider — `MongoDbInboxOptions.EnableTransactions` (requires a replica set) or `CosmosDbInboxOptions.SharedPartitionKey` (Cosmos `TransactionalBatch` is single-partition) — with the handler enlisting via `?.AsMongoSession()` / `?.AsCosmosBatch()`. The middleware automatically uses the transactional path when the store advertises `SupportsTransactional`, and falls back transparently to the idempotent claim path otherwise — never a false atomic advertisement. See [Inbox Pattern → Provider-Native Transactional Inbox](docs-site/docs/patterns/inbox.md#provider-native-transactional-inbox-sql-server-postgresql-mongodb--cosmos-db).
- **Sent-tracking capability on outbox stores (`IOutboxStoreCapabilities.SupportsSentTracking`).** An outbox store reports whether it retains a successfully-sent message as a countable, cleanup-eligible `OutboxStatus.Sent` row. Tracking stores keep the row (statistics count it, cleanup removes it); the relational **delete-on-sent** stores (PostgreSQL, Oracle) remove the row on mark-sent and report `false`, so statistics and cleanup behave correctly instead of assuming one uniform storage model. A store that does not implement the interface is treated as a tracking store (the default). This is a data-shaped capability in the BCL idiom (`Stream.CanSeek`), sibling to `IInboxStoreCapabilities`.
- **Completed-saga retention purge on every saga store.** `ISagaStore.PurgeCompletedBeforeAsync(threshold, ct)` now works on the document stores **Azure Cosmos DB, AWS DynamoDB, and Google Firestore**, joining the in-memory, SQL Server, PostgreSQL, and MongoDB providers for full parity — completed sagas older than a retention window can be purged on every backend, and in-flight sagas are never removed. Drive it with the automatic cleanup background service (`SagaOptions.EnableAutomaticCleanup` / `SagaRetentionPeriod` / `CleanupInterval`) or by calling `PurgeCompletedBeforeAsync` directly; it returns the exact count removed. A store that cannot purge by age throws `NotSupportedException` rather than silently returning `0`. See [Sagas → Retention & Cleanup](docs-site/docs/sagas/index.md#retention--cleanup).
- **`BackoffStrategy.DecorrelatedJitter` retry backoff.** AWS-style decorrelated jitter for the in-process retry path — each delay is sampled from `[baseDelay, previousDelay * 3]` (capped at `maxDelay`), threading the previous delay forward for smoother, less-correlated growth than full jitter. It is stateful and in-process only; durable retry paths (outbox/inbox schedulable stores) continue to use attempt-derived strategies. See [Resilience with Polly](docs-site/docs/operations/resilience-polly.md).
- **Saga automatic cleanup background service.** When `SagaOptions.EnableAutomaticCleanup` is enabled, a hosted background service periodically deletes completed/expired saga state on `SagaOptions.CleanupInterval` (default 1 hour). Registration wires the service automatically.
- **CloudEvents is now first-class across transports.** Envelope↔CloudEvent mapping runs through a single canonical emit path configured with `AddCloudEvents(...)` on the dispatch builder, with per-transport binding — Kafka uses the standard structured `ce_` header binding and AWS EventBridge maps CloudEvent extensions losslessly. The default ingress path works out of the box — schema validation is **opt-in** (`ValidateSchema` defaults to `false`, matching Microsoft's `OutputCache`/`HybridCache` never-fail-the-core stance); enabling `ValidateSchema` without a configured schema registry **fails fast at startup** (`OptionsValidationException`) rather than throwing on every message at runtime. When validation is enabled, it **fails closed**: a payload that fails to load or validate against its schema is rejected rather than silently passing. The mapper is AOT-safe (no reflection on the emit path). Consumer-supplied per-message validation remains available via `UseCloudEventValidation(...)`. See [Transports → CloudEvents](docs-site/docs/transports/index.md).
- **AOT-safe startup options validation across the framework (~40 packages).** Every options type now validates at startup via a source-generated, trim/AOT-safe `IValidateOptions<T>` wired with `ValidateOnStart()`, so a misconfiguration fails fast with `OptionsValidationException` naming the offending setting instead of surfacing as an obscure runtime error later.

- **Key reactivation across key-management providers.** `IKeyManagementAdmin.ReactivateKeyAsync(...)` restores a previously suspended encryption key to usable state, complementing suspension — implemented for the in-memory, AWS KMS, Azure Key Vault, and HashiCorp Vault providers.
- **Fail-fast event-type registration validation.** When the default (reflection-free) event serializer is used, an `IHostedService` validator now fails fast at startup if the event-type registry is empty or missing a required registration, with an error message that names the fix — instead of a silent runtime deserialization failure. Consumer-supplied serializers are exempt. Populate the allow-list via the event-sourcing builder's `RegisterEventTypes*` methods.
- **Payload-size DoS guard on every transport ingress (both receive and subscribe surfaces) plus the outbox.** The outbox publish path and **all six transports** (AWS SQS, Azure Service Bus, Google Pub/Sub, gRPC, Kafka, RabbitMQ) now reject oversized messages at the boundary — measured before deserialization on both the polling receiver and the push subscriber — so one large payload cannot exhaust memory, poison-loop, or strand a batch. Each surface has a bounded default sized to its broker profile (SQS/Azure Service Bus 256 KiB, Google Pub/Sub 10 MiB, gRPC/Kafka/RabbitMQ/outbox 4 MiB) and is tuned via `MaxPayloadBytes` (`null` opts out; a non-positive value is rejected at startup). Over-limit deliveries are rejected using each transport's native negative-acknowledgement (nacked / dead-lettered / abandoned) and logged. See [Runtime Contract → Payload Size Contract](docs-site/docs/operations/runtime-contract.md#payload-size-contract).
- **Optional keyed telemetry pepper (HMAC-SHA-256 fingerprints).** Telemetry tag fingerprints can be upgraded from an unkeyed SHA-256 digest to keyed HMAC-SHA-256 by supplying a secret pepper — `TelemetrySanitizerOptions.Pepper` (observability sanitizer) and `MaskingTelemetrySanitizerOptions.Pepper` (security-audit masking sanitizer). This protects low-entropy identifiers (short user IDs, source IPs) against brute-force/rainbow-table correlation. The pepper is optional (`null` = unkeyed default) and fingerprinting never throws on the telemetry path regardless of the setting (fail-open). See [PII-Safe Telemetry → Keyed fingerprints](docs-site/docs/observability/pii-safe-telemetry.md#keyed-fingerprints-pepper).
- **`ILeaderElection.AcquisitionFailed` event.** Leader election now raises `AcquisitionFailed` (with `LeaderElectionAcquisitionFailedEventArgs`: `CandidateId`, `ResourceName`, `Reason`, optional `Exception`, `Timestamp`) whenever an instance fails to acquire leadership — losing the race or erroring during the attempt — and the telemetry decorator records these on the acquisitions counter with a `result=failed` tag. The event fires per failed acquisition attempt (per poll), not per leadership transition, surfacing contention and backend errors a `BecameLeader`/`LostLeadership`-only view would miss. See [Leader Election → Observing acquisition failures](docs-site/docs/leader-election/index.md#observing-acquisition-failures).
- **W3C `tracestate` now propagates across the outbox, symmetric with `traceparent`.** Multi-vendor trace state is captured at staging and restored at publish alongside `traceparent`, so a distributed trace keeps its vendor-specific state across the async outbox hop. `tracestate` is propagated only when present — never fabricated. See [Production Observability → Traces Connect Across the Outbox](docs-site/docs/observability/production-observability.md).
- **`BackoffStrategy.FullJitter` retry backoff.** AWS-style full jitter — the delay is sampled uniformly from `[0, min(maxDelay, baseDelay * multiplier^(attempt-1))]`, maximally decorrelating concurrent clients to avoid a thundering herd on retry. See [Resilience with Polly](docs-site/docs/operations/resilience-polly.md).
- **Secrets-backed message-signing key providers for Azure Key Vault and AWS Secrets Manager.** `AddAzureKeyVaultKeyProvider(...)` (`Excalibur.Security.Azure`) and `AddAwsSecretsManagerKeyProvider(...)` (`Excalibur.Security.Aws`) register an `IKeyProvider` that resolves signing keys from the cloud secret store, **fails closed** (a resolution error never yields a null/empty key), and caches key material for a bounded TTL (`CacheTtlSeconds`, default 300s). Both validate options at startup and register via `TryAdd`. See [Message Signing → Key Provider](docs-site/docs/security/message-signing.md#key-provider).

- **Advertised-or-fail-loud configuration + cross-store append atomicity** -- Excalibur now holds a structural guarantee that **any configuration option the API advertises is either backed by a live implementation or fails fast at startup** with `OptionsValidationException` — a configured strategy or capability never silently degrades to a no-op at runtime. Provider registration paths that previously skipped startup validation now wire `ValidateOnStart()` consistently (for example, every `AddServerlessHosting(...)` overload). Event-store batch appends are now atomic across providers: a multi-row append is all-or-nothing, with no torn event-stream prefix on a mid-batch failure. See [Configuration → Advanced](docs-site/docs/core-concepts/configuration-advanced.md#advertised-capabilities-are-wired-or-fail-loud).

- **Provider conformance parity + transport tuning options** -- Snapshot, event, outbox, and inbox stores are now exercised by a single shared **conformance suite** across all providers (SQL Server, PostgreSQL, SQLite, Redis, MongoDB, Cosmos DB, DynamoDB, Firestore) on real infrastructure using each provider's **default** serializer/client, so a provider can no longer pass its own unit tests while diverging from the contract on a real server. New transport tuning surfaces: **gRPC** resilience options on `GrpcTransportOptions` (automatic retries + hedging, keep-alive ping config, HTTP/2 connection pooling, configurable retryable status codes); **AWS SQS** optional queue provisioning (`IAwsSqsTransportBuilder.ConfigureProvisioning` — create queues / dead-letter redrive / SNS subscriptions, fail-open by default) and a visibility-timeout heartbeat (`ConfigureVisibilityHeartbeat`) for long-running handlers, plus `UseRequestTimeout`/`UseMaxRetryAttempts`; **Google Pub/Sub** auto-applied dead-letter policy (`AutoApplyDeadLetterPolicy`, `DeadLetterMaxDeliveryAttempts`); **RabbitMQ** automatic connection recovery via the fluent builder (`.AutomaticRecovery(enabled, networkRecoveryInterval)` + `RabbitMQConnectionOptions`); **Kafka** consumer `PartitionAssignmentStrategy` with commit-on-revoke; and an **AWS Lambda** SnapStart warm-up hook (`AwsLambdaSnapStartHooks.RegisterWarmup`) to reduce cold starts. OpenTelemetry producer spans were added on the transport publish path. See [What's New](docs-site/docs/whats-new.md).

- **First-party SQL persistence for security events (`Excalibur.Security.AuditLogging`)** -- security events can now be persisted to SQL Server through the tamper-evident, hash-chained `IAuditStore` contract via a bridge adapter, reusing the existing audit-logging stack instead of a parallel store.

- **First-party per-module health checks + Dispatch-core startup validation** -- 19 previously-orphaned health checks are now wired as **public `IHealthChecksBuilder` extensions** across 13 modules, so consumers can register exactly the checks they need (all in the `Microsoft.Extensions.DependencyInjection` namespace, each accepting optional `name`/`failureStatus`/`tags`). New registrations: Dispatch core (`AddDispatchCoreHealthChecks` + `AddPipelineIntegrityHealthCheck`/`AddSerializationHealthCheck`/`AddStreamingHandlerHealthCheck`), caching (`AddCacheHealthCheck`), claim-check (`AddClaimCheckHealthCheck`), data providers (`AddDynamoDbHealthCheck`/`AddFirestoreHealthCheck`/`AddInMemoryHealthCheck`/`AddMongoDbHealthCheck`/`AddMySqlHealthCheck`/`AddRedisHealthCheck`), event sourcing (`AddEventSourcingHealthChecks` + `AddEventStoreHealthCheck`/`AddSnapshotStoreHealthCheck`/`AddTenantShardHealthCheck`/`AddProjectionsHealthCheck`), and compliance/audit/security (`AddComplianceHealthChecks` + `AddEncryptionHealthCheck`/`AddErasureHealthCheck`, `AddAuditStoreHealthCheck`, `AddSecurityHealthCheck`). The convention-based `AddExcaliburHealthChecks()` aggregate is unchanged. Separately, Dispatch-core configuration now participates in `ValidateOnStart` (fail-fast on invalid options). See [Health Checks → First-Party Per-Module Health Checks](docs-site/docs/observability/health-checks.md#first-party-per-module-health-checks).

- **MediatR & MassTransit migration tooling (`Excalibur.Dispatch.Compat.MediatR`, `Excalibur.Dispatch.Compat.MassTransit`, `Excalibur.Dispatch.Migration.Analyzers`, `Excalibur.Dispatch.Migration.CodeFixes`)** -- new, isolated compatibility packages let teams move off the now-commercial MediatR (and simple MassTransit consumers) onto Excalibur.Dispatch via a **mechanical namespace swap plus Roslyn code-fixes**, not a hand rewrite. `Excalibur.Dispatch.Compat.MediatR` ships source-compatible shapes (`IMediator`/`ISender`/`IPublisher`, `IRequest`/`IRequestHandler`, `INotification`/`INotificationHandler`, `IPipelineBehavior`/`RequestHandlerDelegate`, `IStreamRequest`/`IStreamRequestHandler`, `Unit`) that forward to the canonical primitives, so `using MediatR;` → `using Excalibur.Dispatch.Compat.MediatR;` plus `AddMediatR(...)` → `AddMediatRCompat(...)` compiles existing code. `AddMediatRCompat` self-bootstraps the Dispatch core, validates options at startup, and accepts the familiar `RegisterServicesFromAssembly*`/`AddBehavior`/`AddOpenBehavior`/`HandlerLifetime` configuration; handler registration is source-generated (AOT-safe, no consumer-path reflection). Requests resolve to exactly one handler (duplicate → fail-fast `DuplicateRequestHandlerException`; missing → `HandlerNotFoundException`), notifications support many handlers, and pipeline behaviors nest in registration order. The migration analyzer surfaces four diagnostics — **EXMIG0001** (`AddMediatR` → `AddMediatRCompat`, code-fix), **EXMIG0002** (constructs outside the compat contract — pre/post processors, exception handlers/actions, stream pipeline behaviors — manual step), **EXMIG0003** (`using MediatR;` swap, code-fix), **EXMIG0004** (handler method-name delta, code-fix) — so nothing is silently skipped. `Excalibur.Dispatch.Compat.MassTransit` adds a source-compatible `IConsumer<TMessage>`/`ConsumeContext<TMessage>` consumer shim with `AddMassTransitConsumer<TConsumer, TMessage>()` (advanced `ConsumeContext` capabilities — `Respond`/`Publish`/`Send`/`Redeliver` — are intentionally not shimmed and require a manual step). The compat packages depend on `Excalibur.Dispatch`; the canonical packages never depend on them. See [Migrating from MediatR](docs-site/docs/migration/from-mediatr.md#drop-in-compatibility-shim), [Migrating from MassTransit](docs-site/docs/migration/from-masstransit.md#consumer-compatibility-shim), and the [EXMIG diagnostics](docs-site/docs/diagnostics/index.md#migration-diagnostics-exmig).

- **First-class `TenantId` propagation + public Cosmos change-feed durability defaults** -- `TenantId` is now a first-class property on the transport message context, **copied by every transport mapper independently of any header convention**, so multi-tenant routing is no longer dropped at the transport-context boundary (previously a tenant could be silently lost crossing the boundary). Separately, `AddCosmosDbChangeFeedDurabilityDefaults()` is now a **public** cross-package registration (previously internal) — it installs the default in-memory checkpoint store + non-durable startup warning for event-store-only or outbox-only Cosmos consumers, and is overridden by `AddCosmosDbChangeFeedCheckpointStore`.

- **Durable Cosmos change-feed continuation (`Excalibur.Data.CosmosDb`)** -- pull-model change-feed subscriptions can now persist their continuation token so they resume from the last processed position after a restart instead of replaying from the start. A new `IChangeFeedCheckpointStore` (`LoadAsync`/`SaveAsync`) abstraction is flowed into every change-feed subscription created by the persistence provider; the default registration is the process-local `InMemoryChangeFeedCheckpointStore` (non-durable, **emits a one-time LOUD startup warning** — event ID `102803` — so the trade-off is never silent). Register the durable Cosmos-backed store with `services.AddCosmosDbChangeFeedCheckpointStore(sp => container)` (partition key path `/subscriptionId`), or implement `IChangeFeedCheckpointStore` for a different backing store. Durable continuation is **fully functional** for the pull-model subscriptions (data provider, outbox, event store) under the default (Newtonsoft) Cosmos client. The push-model `AllVersionsAndDeletes` processor mode is **not yet covered** — it is gated on the Cosmos SDK `ChangeFeedItem<T>` API reaching GA. See [Cosmos DB → Durable Change Feed Continuation](docs-site/docs/data-providers/cosmosdb.md#durable-change-feed-continuation).

- **Fencing-token providers for every leader-election backend, with fail-closed exhaustion.** Consul, Kubernetes, and MongoDB now ship dedicated fencing-token providers (`AddConsulFencingTokenProvider()`, `AddKubernetesFencingTokenProvider()`, `AddMongoDbFencingTokenProvider()`), and every backend's leader election (Consul, Kubernetes, MongoDB, Postgres, Redis, SQL Server) accepts an optional `IFencingTokenProvider` so a monotonic fencing token can guard against a stale leader. Fencing tokens are **strictly monotonic**; when a provider's token domain is exhausted the provider throws the new `FencingTokenExhaustedException` (namespace `Excalibur.Dispatch.LeaderElection.Fencing`, carrying the optional `ResourceId`) and **fails closed** — it refuses to mint, so leadership cannot be granted or renewed on an unsafe token and a leader that hits exhaustion mid-tenure relinquishes rather than continue. Exhaustion is practically unreachable for the 64-bit self-minting domains (Consul/MongoDB) and reachable only for a narrow native counter such as a Kubernetes `Lease.spec.leaseTransitions` (32-bit). See [Leader Election → Fencing tokens](docs-site/docs/leader-election/index.md#fencing-tokens).
- **`IMessageChannelAdapter` split into focused role interfaces (ISP).** The channel-adapter contract is now composed from `IMessageChannelSender<TMessage>` (`SendAsync`/`SendBatchAsync`), `IMessageChannelReceiver<TMessage>` (`ReceiveAsync`/`ReceiveBatchAsync`), `IMessageChannelAcknowledger<TMessage>` (`AcknowledgeAsync`/`RejectAsync`), and `IMessageChannelConnection` (`ConnectAsync`/`DisconnectAsync`, `ChannelName`, `IsConnected`), so a component can depend on only the capability it uses instead of one wide interface.
- **CronTimer catch-up policy for missed occurrences.** `CronTimerTransportAdapterOptions`/`CronTimerOptions` gain `CatchUpPolicy` (`CronTimerCatchUpPolicy`: `Skip` — the default, drop missed occurrences and resume at the next future one; `FireOnce` — fire a single catch-up; `FireAll` — fire every missed occurrence) plus `MaxCatchUpOccurrences` (default 100) which bounds a `FireAll` pass and is validated at startup (must be ≥ 1 when `FireAll`).
- **Per-queue inbound payload cap for RabbitMQ.** The RabbitMQ queue builder adds `MaxPayloadBytes(int? maxBytes)` (`RabbitMQQueueOptions.MaxPayloadBytes`) so an oversized received message is rejected at that queue's ingress before deserialization (`null` opts out).
- **B3 trace-context propagation.** `UseB3TraceContextInjection()` on the Dispatch builder injects/extracts B3 (Zipkin-style) trace headers for interop with B3-instrumented services.
- **Compliance audit-store role interfaces and master-key backup/restore.** The audit store is split into `IAuditQuery` (`QueryAsync`/`CountAsync`/`GetByIdAsync`/`GetLastEventAsync`/`VerifyChainIntegrityAsync`) and `IAuditWriter` (`StoreAsync`) so read and write concerns are separable. New master-key backup/recovery contracts add `IMasterKeyBackupExporter` (`ExportMasterKeyAsync`, plus `GenerateRecoverySplitAsync` for Shamir threshold shares) and `IMasterKeyRestoreService` (`ImportMasterKeyAsync`/`ReconstructFromSharesAsync`/`VerifyBackupAsync`/`GetBackupStatusAsync`).
- **Cursor pagination for the Cosmos DB projection store.** `CosmosDbProjectionStore<TProjection>.QueryCursorAsync(filters, cursor, pageSize, ct)` returns a `CursorPagedResult<TProjection>` for keyset-style paging over projections.
- **Turnkey exactly-once messaging (`AddExactlyOnceMessaging`).** A single registration — `services.AddExactlyOnceMessaging<TOutboxStore, TInboxStore>(configure)` — composes the hardened outbox, the transactional inbox, and a durable deduplication window behind one call with a documented delivery-guarantee boundary (`ExactlyOnceOptions`). The deduplication time-to-live is tied to the inbox message TTL unless overridden. **Guarantee boundary:** exactly-once when the inbox store implements `ITransactionalInboxStore` (the message-consume and the dedup/inbox commit share one transaction); otherwise at-least-once with durable deduplication, so handlers must be idempotent. Set `ExactlyOnceOptions.RequireTransactionalExactlyOnce = true` to **fail fast at startup** when the configured inbox store cannot honour the transactional guarantee, rather than silently degrading to at-least-once. See [Patterns → Inbox](docs-site/docs/patterns/inbox.md).
- **Canonical event serialization shared by every event store and the DI serializer.** `EventSerializationDefaults.CreateCanonicalOptions()` exposes the single canonical `JsonSerializerOptions` (camelCase, `JsonStringEnumConverter`, omit-null) that all event stores and the default `JsonEventSerializer` now converge on, so an event written by a store and read back through the dispatcher round-trip identically. **Enums now serialize as strings (not numbers) and null properties are omitted**, and a prior Cosmos DB PascalCase / type-erasure divergence is resolved.
- **Outbox message routing destination.** `IOutboxMessage.Destination` (a nullable `string`) records the intended transport/topic for a staged message and is now persisted and reloaded by the Postgres, Cosmos DB, DynamoDB, Firestore, Redis, and MongoDB outbox stores. The Redis and MongoDB stores previously fell back to the message type name; they now carry the real destination-from-context end-to-end.
- **Transport locality classification with fail-closed validation.** A new `TransportLocality` (`Local`/`Remote`) is supplied when registering a transport (`ITransportRegistry.RegisterTransport`/`RegisterTransportFactory`), exposed via `HasRemoteTransport`. Setting `TransportValidationOptions.RequireRemoteTransport` makes startup **fail closed** when a host that requires a remote transport has only in-process (`Local`) transports configured, instead of silently degrading.
- **CloudEvents transport options are validated at startup.** Every CloudEvents-enabled transport (AWS SQS, AWS SNS, AWS EventBridge, Azure Service Bus, Azure Event Hubs, Kafka) now registers an `IValidateOptions<T>` wired via `ValidateOnStart()`, so an invalid CloudEvents configuration fails fast at startup with `OptionsValidationException` instead of surfacing as a runtime serialization error on the first message.
- **Default `IContractVersionService` is now shipped.** The `default` pipeline profile's `ContractVersionCheckMiddleware` now resolves a built-in `IContractVersionService` instead of silently no-op'ing when none is registered; supply your own registration to override it (`TryAdd`).
- **CDC background processing fails fast at startup when misconfigured.** Enabling CDC background processing without the required `ICdcProcessor` dependency now fails at application startup via a hosted-service validator with a message that names the fix, rather than at DI-registration time or silently at runtime.
- **Fail-closed message-ordering validation middleware.** `AddOrderingValidation()` registers an `OrderingValidationMiddleware` that enforces per-sequence ordering and **fails closed** — an out-of-order message is rejected with `OutOfOrderMessageException` (marked `IFailClosedException`, so it is never swallowed by fail-open pipeline handling) rather than silently processed out of sequence.
- **Audit signing-key startup probe + never-drop audit flush.** When audit-log integrity is enabled, an `AuditSigningKeyStartupProbe` (`IHostedService`) resolves the signing-key provider and **fails fast at startup** if no signing key can be produced (provider-agnostic — it does not false-fail async/KMS key providers the way a synchronous options validator would). Separately, the security auditor's flush is now bounded and **never-drop**: on a flush failure the un-flushed events are held in a local retry buffer and health degrades, rather than the events being dropped, while producer backpressure bounds total in-flight.

### Changed

- **Aggregate replay APIs now take `IEnumerable<HistoricEvent>`** instead of `IEnumerable<IDomainEvent>`: `AggregateRoot<TKey>.LoadFromHistory`, `IAggregateSnapshotSupport.LoadFromHistory`, and the `IAggregateRoot<TAggregate, TKey>.FromEvents` / `AggregateRoot<TAggregate, TKey>.FromEvents` factory (and any aggregate's own `FromEvents`, e.g. `Grant.FromEvents`). `HistoricEvent` (`readonly record struct HistoricEvent(IDomainEvent Event, long Version)`) pairs a domain event with the authoritative version recorded by the event store, so replay reads each event's stream position from this envelope rather than trusting the event payload to carry its own version — and a replay that has lost its versions cannot be constructed. **Breaking** (greenfield) — **Migration:** when reloading an aggregate from a raw event sequence, wrap each event with its store-assigned version: `events.Select((e, i) => new HistoricEvent(e, i))` (or the real persisted version).
- **Outbox fencing moved off the core `IOutboxStore` into a discoverable `IFencedOutboxStore` capability, and `IOutboxStore` now implements `System.IServiceProvider`.** The base `IOutboxStore.GetUnsentMessagesAsync`/`MarkSentAsync` no longer take a `long? fencingToken` parameter — leader-election fencing is an optional capability a store advertises via `IFencedOutboxStore` (with a non-nullable `long fencingToken`), discovered by `store.GetService(typeof(IFencedOutboxStore))` rather than a concrete cast. Because discovery goes through `IServiceProvider`, a decorator honours its own invariant: `OutboxStoreDecorator` forwards capabilities transparently, while `IsolatingOutboxStoreDecorator` (the base for transforming decorators such as `EncryptingOutboxStoreDecorator`) **denies capabilities by default and wraps every payload-bearing one**, so a caller can never obtain a raw, encryption-bypassing view of the inner store. **Breaking** (greenfield) — **Migration:** drop the `fencingToken` argument from `IOutboxStore` calls; for fenced drain/mark, resolve `store.GetService(typeof(IFencedOutboxStore))` and call its overloads.
- **`Leadership.FencingToken` is now `long?` (nullable).** The absence of a fencing token — a lease acquired without a fencing-token provider — is modelled as `null` rather than an in-band sentinel such as `0`, so "no fence" can never be confused with a real token value. **Breaking** (greenfield) for code reading `Leadership.FencingToken` — **Migration:** treat `null` as "unfenced" and only compare non-null token values.
- **CloudEvents is no longer a transport decorator.** `CloudEventsTransportSender` and `CloudEventsTransportReceiver` are removed; envelope↔CloudEvent mapping is handled by the CloudEvents bridge/middleware configured through `AddCloudEvents(...)`, giving one canonical emit path and per-transport adapters instead of a hand-composed decorator. **Breaking** (greenfield) — **Migration:** remove any `.Use(inner => new CloudEventsTransportSender(inner, ...))` / `CloudEventsTransportReceiver` decorator wiring from the transport pipeline and call `dispatch.AddCloudEvents(...)` instead.
- **Nested dispatch is now automatic — `DispatchChildAsync` is removed.** The context-free `DispatchAsync(message, ct)` now establishes the causal chain by itself: called from within a handler (an ambient context exists) it dispatches a **child** — a fresh `MessageId` with `CausationId` set to the parent's message id, propagating correlation, identity, and routing — mirroring `Activity`/OpenTelemetry `StartActivity`; called at the top level it creates a fresh root context. **Breaking** (greenfield) — **Migration:** replace `dispatcher.DispatchChildAsync(msg, ct)` with `dispatcher.DispatchAsync(msg, ct)`. To deliberately reuse the parent context instead of childing, pass it explicitly to the `DispatchAsync(message, context, ct)` overload.
- **`AuthorizationMiddleware` removed from the `default` pipeline profile (kept in `strict`).** Because profile entries fail-open when they cannot be materialized — silently skipped if their services are unregistered — keeping authorization in `default` meant a consumer who selected `default` without wiring an authorization service got a silent authorization bypass. (That is still how `Optional` entries behave, and every entry in `default` is `Optional`. Entries marked `Required` now fail the build instead; see the profile-criticality entry above.) Authorization is now opt-in. **Migration:** for permission checks, select the `strict` profile (`pipeline.UseProfile("strict")`) or register `AuthorizationMiddleware` explicitly (`pipeline.Use<AuthorizationMiddleware>()`). Handlers that never required authorization are unaffected.
- **Inbox message claiming is atomic across all providers.** The InMemory, MongoDB, PostgreSQL, Redis, and SQL Server inbox stores now perform a lease-aware atomic claim-before-execute, removing a full-inbox check-then-act (TOCTOU) window. The consumer-facing delivery contract is unchanged — exactly-once for concurrent redelivery, at-least-once across a process crash (handlers must remain idempotent) — but concurrent redeliveries are now blocked correctly under contention on every provider.
- **Kubernetes leader-election timing options use `TimeSpan`, not integer shadows.** `KubernetesLeaderElectionOptions` drops the `LeaseDurationSeconds`/`RenewIntervalMilliseconds`/`RetryIntervalMilliseconds`/`GracePeriodSeconds`/`MaxRetryDelayMilliseconds` numeric shadow properties in favour of the inherited base `TimeSpan` properties (`LeaseDuration`, `RenewInterval`, `RetryInterval`, …) plus a new `MaxRetryDelay` (`TimeSpan`). **Breaking** (greenfield) — **Migration:** `options.LeaseDurationSeconds = 15;` → `options.LeaseDuration = TimeSpan.FromSeconds(15);` and `options.RenewIntervalMilliseconds = 10_000;` → `options.RenewInterval = TimeSpan.FromSeconds(10);`.
- **SQL Server health-based leader election takes its connection settings from options.** `SqlServerHealthBasedLeaderElectionOptions` gains required `ConnectionString` and `LockResource` properties (validated at startup); the `connectionString`/`lockResource` constructor parameters are removed. Consumers using the DI/registration path are unaffected. **Breaking** (greenfield) for direct-construction callers — **Migration:** set `ConnectionString`/`LockResource` on the options instead of passing them to the constructor.
- **`OutboxMessage` value constructors are no longer public.** Public construction is via `OutboxMessage.FromOutboundMessage(...)` or required-member object initialization; the positional value constructors are now internal, making it structurally impossible to construct an outbox message that drops `TenantId`. **Breaking** (greenfield) — **Migration:** construct with `FromOutboundMessage` or an object initializer setting the required members.
- **The advertised-but-unwired `KafkaMessageBusOptions` type is removed.** It exposed configuration that was never connected to the Kafka transport. **Breaking** (greenfield) — **Migration:** configure the Kafka transport through the live `KafkaProducerOptions` / `KafkaOptions` instead.
- **Outbox message payloads are now `byte[]` instead of `string`.** `IOutboxMessage.MessageBody` (and `OutboxMessage.MessageBody`) is a binary `byte[]`, so the outbox carries the serializer's bytes losslessly end-to-end — no lossy `string`↔`byte[]` round-trip for binary or non-UTF-8 serializers (e.g. MessagePack, Protobuf). `OutboxMessage` value-equality is structural over the byte body. The Postgres outbox `message_body` column is now `bytea`. **Breaking** (greenfield) for code that read `MessageBody` as a `string` — decode with your serializer instead. **Upgrade note:** consumers on a pre-existing Postgres outbox schema must migrate the `message_body` column to `bytea`.
- **The middleware custom-validation contract is renamed `IValidationService` → `IMessageValidationService`.** The Dispatch middleware validation service (`Excalibur.Dispatch.Middleware.Validation`) is renamed to resolve a name collision with the unrelated `Excalibur.Dispatch.Abstractions.Validation.IValidationService`, which caused an ambiguous-reference compile error when both namespaces were imported. `NoOpValidationService` and the `ValidationMiddleware` wiring are updated accordingly. **Breaking** (greenfield) — **Migration:** rename your custom `IValidationService` implementation (and any `services.AddScoped<IValidationService, …>()` registration) to `IMessageValidationService`.
- **Several Options types are reorganized into nested sub-option groups (Microsoft-first ≤10-property guideline).** Flat option surfaces that exceeded the guideline are grouped, and inert compat/monitoring shim properties that were never wired have been deleted. The nested groups are set the same way (`Configure<T>` or `appsettings` binding); only a *direct* assignment to a moved property moves onto its group. **Breaking** (greenfield) for code that set the moved properties directly:
  - `ElasticSearchProjectionStoreOptions` — index settings move under `Index` (`o.IndexPrefix` → `o.Index.IndexPrefix`; likewise `IndexName`, `NumberOfShards`, `NumberOfReplicas`, `CreateIndexOnInitialize`, `RefreshInterval`, `IndexMappingConvention`). Connection settings (`NodeUri`/`NodeUris`/`ConnectionPoolType`/`RequestTimeoutSeconds`/`EnableDebugMode`) and `Auth` stay as-is. (The type has no `ConnectionString` — use `NodeUri`.)
  - `SqlServerCdcOptions` — connection settings move under `Connection` (`SqlServerCdcConnectionOptions`).
  - `RabbitMqConsumerOptions` — dead-letter settings move under `DeadLetter` (`RabbitMqConsumerDeadLetterOptions`).
  - `SqlServerOutboxOptions` — table names move under `Tables`, processing tunables under `Processing`.
  - `AzureStorageQueueTransportOptions` — connection settings under `Connection`, dead-letter under `DeadLetter`.
  - `DynamoDbEventStoreOptions` — throughput settings under `Throughput` (inert compat shims removed).
  - `StreamingPullOptions` (Google Pub/Sub) — health/metrics settings under `Monitoring` (inert monitoring shims removed).
  - `ContextValidationOptions` — per-check toggles under `Checks` (`ContextValidationChecksOptions`).
  - `ExcaliburValidationOptions` — grouped under `Databases`, `CloudProviders`, and `MessageBrokers`.
  - `JwtAuthenticationOptions` — reorganized to stay within the property budget.
  - `GooglePubSubOptions` — connection identity moves under `Connection` (`o.ProjectId`/`TopicId`/`SubscriptionId` → `o.Connection.*`) and telemetry/tracing under `Telemetry` (`o.EnableTracePropagation`/`TracingSamplingRatio`/`ExportToCloudMonitoring`/`EnableOpenTelemetry` → `o.Telemetry.*`), alongside the existing `Subscriber` group.
- **Security auditing masks PII by default.** `AddSecurityAuditing()` now installs a masking/hashing telemetry sanitizer as the **default** (via `TryAdd`), so a security-audit sink never emits raw `UserId`/`SourceIp` out of the box — raw passthrough is now **opt-in only**. Tag values become a stable `sha256:`-prefixed fingerprint (correlates across events without exposing the raw value) and secret-shaped payloads are redacted. For cryptographic protection of low-entropy identifiers, register the keyed sanitizer. **Behavior change:** wiring `AddSecurityAuditing()` alone no longer logs raw PII. See [PII-Safe Telemetry → Security Auditing](docs-site/docs/observability/pii-safe-telemetry.md#security-auditing-safe-by-default).
- **RabbitMQ nacks an oversized received message instead of poison-looping.** An over-limit delivery is now nacked in-loop with `requeue: false` (routed to the dead-letter exchange when configured) and the rest of the batch is still returned — previously an oversized message could strand its batch and be redelivered indefinitely.
- **GDPR data-subject identifiers are pseudonymized with a keyed HMAC and a required pepper.** Data-subject hashing now uses `IDataSubjectHasher` (default `HmacDataSubjectHasher`, keyed HMAC-SHA-256) — a one-way, non-reversible pseudonym for match-and-erase, replacing the plain static SHA-256 helper. A secret pepper is **required** and validated at startup (`DataSubjectHashingOptions.Pepper`, ≥ 32 characters; the host fails closed without it), and the hasher is now registered on the standalone `AddLegalHoldService`/`AddDataInventoryService` paths, not only the erasure path. **Migration:** configure `DataSubjectHashingOptions.Pepper` from your secret manager. See [GDPR Erasure → Data-subject hashing](docs-site/docs/compliance/gdpr-erasure.md).
- **DynamoDB event store rejects atomic appends larger than 100 events.** DynamoDB's `TransactWriteItems` hard-caps at 100 items, so an all-or-nothing append beyond that is impossible. With `UseTransactionalWrite = true` (default), an append of **more than 100 events is now rejected at the boundary before any write** — split the batch into appends of at most 100 events. With `UseTransactionalWrite = false`, the non-atomic per-item path still accepts larger batches as documented non-atomic behavior. **Migration:** none for ≤100-event appends; transactional callers appending more must split the batch. See [DynamoDB → Atomic event-store append limit](docs-site/docs/data-providers/dynamodb.md#atomic-event-store-append-limit).

- **`AddComplianceEncryption` collapses to a single fluent builder.** The separate `AddComplianceEncryption(...)` / `AddComplianceEncryption<TKeyManagement>(...)` / `AddComplianceEncryptionWithRotation(...)` overloads are replaced by one builder-based call: `services.AddComplianceEncryption(b => b.WithInMemoryKeyManagement().WithEncryption().WithKeyRotation())` (or `.WithKeyManagement<TProvider>()`). **Breaking** (greenfield) — **Migration:** move the previous overload arguments onto the builder — `WithInMemoryKeyManagement`/`WithKeyManagement<T>` for key management, `WithEncryption` for the AES-GCM options, and `WithKeyRotation` to enable rotation.
- **Elasticsearch/OpenSearch materialized views default to read-your-write consistency.** Per-document writes now default to the `wait_for` refresh policy (a write is visible to a subsequent read without an arbitrary delay), and outbox/inbox statistics use a server-side count instead of materializing large result sets. **Migration:** none required; to restore the prior fire-and-forget behavior set the refresh policy to `"false"` on the materialized-view options. (audit-2026-06)
- **The inert RabbitMQ "streams" surface was removed.** `IRabbitMqStreamConsumer`, `RabbitMqStreamOptions`, `StreamOffset`, and `AddRabbitMqStreamQueues` registered no working consumer and are dropped. **Breaking** (greenfield) — **Migration:** these types were non-functional; use the standard RabbitMQ queue transport instead.

- **Registration collapses to one composition root and the `Add*`/`Use*` verb standard.** `AddDispatch` is now the single registration entry point and the `UseDispatch` alias is **removed**; `Add*` verbs register services (matching `AddLogging`/`AddOptions`) while `Use*` is reserved for pipeline/middleware ordering. **Breaking** (greenfield) — **Migration:** rename `UseDispatch(...)` → `AddDispatch(...)` (behavior is identical). See [Handlers → Registration](docs-site/docs/handlers.md).
- **`Use*`→`Add*` rename completed on the remaining service-registration extensions.** Continuing the `Add*`-registers-services standard, these registration methods are renamed: the eight `UseCloudEventsFor{Sqs,Sns,EventBridge,ServiceBus,EventHubs,PubSub,Kafka,RabbitMq}` transport CloudEvents registrations → `AddCloudEventsFor*`, `UseConfluentFormat` → `AddConfluentFormat`, `UseDispatchResilience` → `AddDispatchResilience`, and `UseStrictContextValidation`/`UseLenientContextValidation` → `AddStrictContextValidation`/`AddLenientContextValidation`. The base `UseCloudEvents` (no `For` suffix) is unchanged. **Breaking** (greenfield) — **Migration:** rename the call sites `Use*` → `Add*` (behavior identical).
- **Saga in-memory store registration renamed `UseInMemoryStore()` → `WithInMemoryStore()`.** On the saga builder (`ISagaBuilder`), the in-memory store selection now uses the `With*` configuration verb. **Breaking** (greenfield) — **Migration:** rename `saga.UseInMemoryStore()` → `saga.WithInMemoryStore()`.
- **`UseValidation` is the canonical full registration; the middleware-only overload is renamed `UseValidationMiddleware`.** The two previously-divergent `UseValidation` overloads (one registered validators, one did not) caused an ambiguous-call compile error when both namespaces were imported. `UseValidation()` now always registers validation infrastructure + middleware; `UseValidationMiddleware()` adds only the middleware. **Breaking** (greenfield) — **Migration:** if you relied on the middleware-only behavior, call `UseValidationMiddleware()`.
- **In-memory deduplication fails closed at capacity instead of silently admitting duplicates.** Capacity is now configurable via `InMemoryDeduplicatorOptions.MaxEntries` (default 100,000; `0` = unbounded). At capacity, a claim that cannot be tracked is denied and the record-producing operations throw a transient `DeduplicationCapacityExceededException` so the message is redelivered rather than admitted without deduplication. **Behavior change** for light-mode (`UseInMemory = true`) deduplication under sustained load — raise `MaxEntries` or use a persistent `IInboxStore`. See [Idempotent Consumer → Idempotency Under Load](docs-site/docs/patterns/idempotent-consumer.md#idempotency-under-load).
- **Inbox stores that cannot honor an atomic claim now fail loud at `ValidateOnStart`** rather than silently degrading to a non-atomic check-then-act path, so a mis-configured store is caught at startup instead of producing duplicate processing at runtime. The inbox/idempotency delivery contract is now documented precisely: exactly-once for concurrent redelivery (atomic claim), at-least-once across a process crash (handlers must be idempotent). See [Inbox](docs-site/docs/patterns/inbox.md) and the [Idempotent Consumer Guide](docs-site/docs/patterns/idempotent-consumer.md).
- **`DefaultAuditContext` fails closed.** A store failure now propagates `AuditPersistenceException` instead of being masked by a `null`/sentinel return, so audit-trail persistence failures are never silently swallowed (audit is compliance-critical). The `MaxAssertionsPerScope` saturation path remains a deliberate log-and-drop.
- **Saga lifecycle/concurrency options are wired to real enforcement.** Previously validated-but-inert `SagaOptions` (concurrency limit, cleanup, optimistic-concurrency toggle) now reach their enforcement sites, and carved toggles fail loud rather than silently no-op.
- **Serverless host providers emit an honest telemetry signal.** AWS Lambda, Azure Functions, and Google Cloud Functions now log (at Information level) that in-process telemetry exporters are in use, replacing a silent no-op behind an advertised-but-inert option — and telemetry never breaks the handler.
- **Backoff strategy enums unified onto canonical `BackoffStrategy` (`Excalibur.Dispatch`, transports)** -- the per-transport backoff enums `Excalibur.Dispatch.Transport.RetryDelayStrategy` and `Excalibur.Dispatch.Transport.Google.BackoffType` are **removed** in favor of the single canonical `Excalibur.Dispatch.Resilience.BackoffStrategy`, and the delay math is centralized in a new stateless `ExponentialBackoff.Calculate(int attempt, in BackoffParameters parameters)` helper (with a `BackoffParameters` value type: `BaseDelay`/`MaxDelay`/`Multiplier`/`JitterFactor`/`UseJitter`) consumed by the AWS SQS, Azure, Google Pub/Sub, and Kafka transports. **Breaking** (greenfield) for code referencing the removed enums — **Migration:** replace `RetryDelayStrategy`/`BackoffType` with `BackoffStrategy` (`Fixed`/`Linear`/`Exponential`/`ExponentialWithJitter`); `RetryDelayStrategy.{Fixed,Linear,Exponential}` map by name, and `BackoffType.Constant` → `BackoffStrategy.Fixed`, `BackoffType.DecorrelatedJitter` → `BackoffStrategy.ExponentialWithJitter`.
- **Distributed circuit breaker uses a windowed failure ratio (`Excalibur.Dispatch.Resilience.Polly`)** -- `DistributedCircuitBreaker` now opens on a **rolling-window** failure ratio gated by minimum throughput (mirroring Polly v8), rather than a lifetime-cumulative count. Three options are now honored: `FailureRatio` (default `0.5`), `MinimumThroughput` (default `10` — minimum attempts within the window before the ratio is evaluated), and `SamplingDuration` (default `30s`). The independent `ConsecutiveFailureThreshold` path is unchanged. **Behavior change** for deployments relying on the prior cumulative semantics. See [Resilience with Polly → Distributed Circuit Breaker](docs-site/docs/operations/resilience-polly.md#distributed-circuit-breaker).

- **Serializer, claim-check, and saga contract corrections (`Excalibur.Dispatch`, `Excalibur.Dispatch.Patterns`, `Excalibur.Saga`)** -- a set of consumer-visible behavior corrections in the correctness burn:
 - **JSON serializers fail loud on empty/null instead of returning silent `null`.** `DispatchJsonSerializer` deserialization now throws `SerializationException` on an empty payload (and on a `null` result for a non-nullable type), and the JSON event serializers (`JsonEventSerializer`/`AotJsonEventSerializer`) now wrap **write-path** failures in `SerializationException` — aligning the whole `ISerializer` family on one null/empty poison-signal contract instead of masking data loss. **Behavior change** — see [Serialization → Troubleshooting](docs/serialization/troubleshooting.md).
 - **`JsonClaimCheckSerializer` defaults to the framework camelCase JSON policy** (camelCase + case-insensitive) when constructed without explicit options, instead of falling through to System.Text.Json's PascalCase/case-sensitive defaults — so claim-check payloads interop with every other `ISerializer`. The two `ISerializer` implementations that previously disagreed on naming/null-contract are now reconciled.
 - **`SagaManager` no longer re-runs a completed saga.** When an event arrives for a saga already in `SagaState.Completed`, `SagaManager` now short-circuits at load time — skipping **both** the handler invocation and the save (no spurious version bump on a finished workflow) — matching `SagaCoordinator`. The event that itself completes the saga still proceeds. See [Sagas → Optimistic Concurrency](docs-site/docs/sagas/index.md#optimistic-concurrency).
 - **Circuit-breaker state machines unified** onto the canonical `CircuitState` + `IDistributedCircuitBreaker`/`ICircuitBreakerPolicy` contracts, replacing 4+ divergent implementations with one conformance-tested seam.

- **`MessageMetadata` composed into 7 focused sub-option groups (`Excalibur.Dispatch`)** -- the flat ~53-property `MessageMetadata` record is refactored into seven each-≤10-property value-type groups (`Identity`, `Routing`, `Timing`, `Observability`, `Delivery`, `EventSourcing`, `Security`), following the Microsoft-first sub-option composition pattern. The **core dispatch identity fields** (`MessageId`, `CorrelationId`, `CausationId`, `MessageType`, `ContentType`, `Source`, `CreatedTimestampUtc`) stay on the root to satisfy `IMessageMetadata`. The **wire (JSON) shape is preserved as a flat object** via `MessageMetadataJsonConverter`, and the `MessageMetadataBuilder` public surface is unchanged, so serialization output and builder-based consumer code are unaffected — only a *direct* read of a moved field moves to its group (e.g. `metadata.TenantId` → `metadata.Security.TenantId`, `metadata.Destination` → `metadata.Routing.Destination`).
- **`VaultOptions` split into focused sub-options (`Excalibur.Compliance.Vault`)** -- to stay within the Microsoft-first ≤10-property budget, detailed HashiCorp Vault settings are grouped into `Auth`, `Keys`, `Retry`, and `Suspension` sub-options (e.g. `options.TransitMountPath` → `options.Keys.TransitMountPath`, `options.AuthMethod` → `options.Auth.AuthMethod`), and a durable key-suspension marker (`Suspension`, persisted to Vault KV) replaces the prior in-memory suspension that did not survive a restart. The `AddVaultKeyManagement(IComplianceVaultBuilder)` fluent builder (`VaultUri`/`TransitMountPath`/`KeyNamePrefix`/`Namespace`/`EnableDetailedTelemetry`/`BindConfiguration`) is unchanged; the grouped sub-options are set via `Configure<VaultOptions>` or `appsettings` binding. **Breaking** for code that set the grouped properties directly (greenfield). See [Cloud KMS Providers — HashiCorp Vault](docs/security/cloud-kms-providers.md).
- **`DefaultRetryPolicy` retries transient failures only (`Excalibur.Dispatch`)** -- when no explicit `RetriableExceptions`/`NonRetriableExceptions` filter matches, the zero-dependency `DefaultRetryPolicy` now defers the retry-vs-abandon decision to the shared `IMessageFailureClassifier` and **retries only failures it classifies as `Transient`**. Permanent-classified exceptions — the `ArgumentException` family (incl. `ArgumentNullException`/`ArgumentOutOfRangeException`), `ValidationException`, `NotSupportedException`/`NotImplementedException`, `UnauthorizedAccessException`/`AuthenticationException`/`ForbiddenException`, and configuration/contract-version errors — are **no longer retried**; they fail fast instead of being retried to the attempt cap (a permanent failure can never succeed on a later attempt, so retrying it only delayed the inevitable throw). Cancellation is still never retried, and an explicitly configured `RetriableExceptions`/`NonRetriableExceptions` allow/deny list continues to take precedence unchanged. **Behavior change.**
- **`AppendResult.FirstEventPosition` is now `long?` (nullable).** The global-stream position returned by an append is `null` for event stores that have no global sequence, rather than a sentinel value that could be mistaken for a real position; `AppendResult.CreateSuccess(long nextExpectedVersion, long? firstEventPosition)` reflects this. **Migration:** callers reading `FirstEventPosition` should treat `null` as "no global position for this store."
- **`AddDispatchCoreHealthChecks` accepts optional `failureStatus` and `tags`.** The Dispatch-core health-check registration now takes `HealthStatus? failureStatus = null` and `IEnumerable<string>? tags = null`, matching the per-module health-check convention so a consumer can classify severity and filter checks by tag.
- **Result caching is enabled by the `configure` call (`Excalibur.Dispatch.Caching`).** Calling the caching `configure` builder now means "enable caching" — the default `IResultCache` wiring activates when you opt in through the builder, rather than requiring a separate enable flag. Two inert `CacheOptions` properties that were never wired (`UseSlidingExpiration`, `EnableCompression`) have been **removed** rather than left as no-ops. **Breaking** (greenfield) — **Migration:** delete any assignment to these properties; sliding expiration/compression were not in effect.
- **`PollyRetryPolicyFactory.Create` builds a Polly v8 `ResiliencePipeline` from `MessageBusOptions`.** The retry factory targets the current Polly v8 resilience-pipeline API. See [Resilience with Polly](docs-site/docs/operations/resilience-polly.md).
- **Google Cloud Pub/Sub options collapse into one canonical `GooglePubSubOptions` with focused sub-options.** The previously-flat transport options are consolidated onto `GooglePubSubOptions`, with subscriber-facing settings grouped under `Subscriber` (`PubSubSubscriberOptions`) and dead-letter settings under `Subscriber.DeadLetter` (`PubSubDeadLetterOptions`). Subscriber tunables move onto the sub-option — e.g. `options.EnableMessageOrdering` → `options.Subscriber.EnableMessageOrdering`, `options.EnableExactlyOnceDelivery` → `options.Subscriber.EnableExactlyOnceDelivery`, and the inbound payload cap is `options.Subscriber.MaxPayloadBytes` (default 10 MiB; `null` opts out). **Breaking** (greenfield) — **Migration:** move subscriber/dead-letter assignments onto `options.Subscriber` and `options.Subscriber.DeadLetter`.

### Removed

- **`AggregateId` and `Version` have been removed from `IDomainEvent` / `DomainEvent`** (greenfield, no compatibility shim). A domain event now models only its own business data; stream identity and position are persistence facts owned by the event store, not properties of the message contract. The aggregate id is supplied to the store as an explicit parameter (`AppendAsync`/`LoadAsync(aggregateId, aggregateType, …)`), and the stream version is assigned by the store at append time and surfaced on the replay envelope (`HistoricEvent.Version` / `StoredEvent.Version`) — never read from the event payload. `IDomainEvent` now exposes `EventId`, `OccurredAt`, `EventType`, `Metadata`, `CorrelationId`, and `CausationId`. **Migration:** delete any `public override string AggregateId => …;` and `Version` members from your event records — they no longer exist to override; put the aggregate's identity in the event's own business property (e.g. `OrderId`). Replace reads of `evt.AggregateId` / `evt.Version` with the event's own id or the store-supplied version (from `HistoricEvent` during replay). A custom event store must assign each event's version itself rather than reading it from the event. See [Domain Events](docs-site/docs/event-sourcing/domain-events.md).
- **The separate circuit-breaker / dead-letter metrics registration classes have been removed** (greenfield, no compatibility shim). The advertised-but-unemitting `CircuitBreakerMetrics`/`DeadLetterQueueMetrics` facade (and its `AddCircuitBreakerMetrics()`/`AddDeadLetterQueueMetrics()` registration) is gone; circuit-breaker and dead-letter telemetry are now emitted directly by the core middleware meters (`Excalibur.Dispatch.CircuitBreakerMiddleware` and `Excalibur.Dispatch.PoisonMessage.Middleware`), requiring no opt-in observability service — subscribe via `AddDispatchInstrumentation()`. Unwired AWS SQS dead-letter scaffolding was likewise removed; SQS uses its native redrive policy. See [Metrics Reference](docs-site/docs/observability/metrics-reference.md).
- **Azure Storage Queues receive operations that were advertised but never wired have been removed** (greenfield, no compatibility shim). Only the non-functional receive trio was cut so the transport surface reflects what actually runs; the send path is unaffected.
- **Hand-rolled primitives that duplicated a .NET / first-party equivalent have been deleted** (greenfield, no compatibility shim). Switch to the named replacement; each removed type had no working consumer wiring left:
  - `ICacheProvider` — custom cache backends now implement the standard `IDistributedCache` (`Microsoft.Extensions.Caching.Distributed`), and cache invalidation from handlers uses `ICacheInvalidationService` or `HybridCache` directly. See [Caching → Cache Providers](docs-site/docs/performance/caching.md).
  - The bespoke two-phase-commit coordinator (`IDistributedTransactionCoordinator`) — use `System.Transactions`, or the outbox / saga patterns for cross-store consistency. The remaining dead 2PC value types (`DistributedTransactionException`, `DistributedTransactionOptions`) are removed with it.
  - `PersistenceOptionsMonitor` / `IPersistenceOptionsMonitor` — the custom options-monitor abstraction had no live consumer; depend on the standard `Microsoft.Extensions.Options.IOptionsMonitor<T>` instead.
  - The unwired data-provider circuit-breaker family (`IDataProviderCircuitBreaker` and the `CircuitBreakerDataProvider` decorator) — use the standard resilience pipeline (`Microsoft.Extensions.Resilience` / Polly v8).
  - `AsyncFactoryHostedService` — perform async initialization directly in `IHostedService.StartAsync`.
- **Runtime-inapplicable serverless deploy-plane options removed** (greenfield, no compatibility shim): `AwsLambdaOptions`, `AzureFunctionsOptions`, and `GoogleCloudFunctionsOptions`. These configured deploy-plane concerns — runtime version, provisioned/reserved concurrency, min/max instances, package type, ingress settings, VPC connector — that the messaging runtime never reads. **Migration:** move those settings to your infrastructure-as-code (SAM/CDK/Terraform/gcloud) or the cloud console, where they belong. Runtime host behavior stays on `ServerlessHostOptions` (`EnableColdStartOptimization`, `ExecutionTimeout`, `MemoryLimitMB`, `Telemetry`, `PreferredPlatform`, `EnvironmentVariables`), configured via the `Add{Aws,Azure,GoogleCloud}...Serverless(options => …)` overload as before. The `Add…Serverless` registration methods are unchanged apart from dropping the removed per-platform `configureOptions` block.

### Fixed

- **Outbox leadership fencing is now an atomic compare-and-swap on PostgreSQL and Oracle.** The fenced drain (`GetUnsentMessagesAsync`) and mark-sent (`MarkSentAsync`) on the PostgreSQL and Oracle outbox stores previously performed the fencing-token check and the mutation as two separate round-trips, leaving a check-then-act window in which a demoted leader — one whose token had already been superseded — could still delete or claim a message and double-deliver on the exactly-once path. Both operations are now a **single-statement compare-and-swap** (a writable CTE on PostgreSQL, a PL/SQL block on Oracle), so the token check and the mutation execute atomically and a stale-token operation affects no rows. Fenced (leader-elected) deployments add a small `outbox_fence` control table holding one monotonic high-water mark per scope; single-instance outboxes are unaffected. See [Outbox schema notes](docs-site/docs/patterns/outbox.md#postgresql).
- **MongoDB leader election now fences durably by default.** When no `IFencingTokenProvider` is supplied, MongoDB leader election defaults to a **durable per-resource fencing counter** in a separate, TTL-free collection instead of a token stored in the lock document. The lock document is destroyed on graceful release and expired by its TTL index, which reset the in-document token to its initial value on restart and could let a stale token from a restarted instance validate as current (split-brain). The durable counter never resets. Supplying your own provider (e.g. `AddMongoDbFencingTokenProvider()`) still overrides the default. See [Leader Election → Fencing tokens](docs-site/docs/leader-election/index.md#fencing-tokens).
- **Event-sourced aggregates whose events implement `IDomainEvent` directly could never be reloaded.** `AggregateRoot.RaiseEvent` stamped an event's version only for events deriving from `DomainEvent`; an event that implemented `IDomainEvent` directly was silently left unversioned, so any aggregate whose stream reached two or more such events threw `EventStreamContiguityException` on reload. The read path no longer consults the payload's version at all — the event store's recorded version is authoritative and is carried alongside each event on replay. Existing event data is unaffected and no migration is required.
- **Exactly-once dedup counter is now collected by OpenTelemetry.** The exactly-once messaging meter is registered in the framework's meter-name set, so the duplicate-suppression counter is exported through the standard OpenTelemetry wiring instead of being silently dropped.
- **Kafka decodes Confluent Schema Registry framing on consume.** A Confluent Schema Registry-configured Kafka transport now strips the 5-byte Confluent wire-format header (magic byte + schema id) from inbound payloads before the canonical deserializer runs, so Confluent-framed messages deserialize correctly. Previously the raw framed bytes were passed downstream undecodable. Non-framed payloads pass through untouched.
- **Caching honors `ICacheable.ShouldCache`.** The caching middleware now evaluates a handler result's `ShouldCache` decision (via `IMessageResult.UntypedReturnValue`), so a result can opt out of caching per-invocation instead of being cached unconditionally.
- **SQL Server inbox builder path enforces the SQL-identifier allowlist.** The builder-based inbox registration now applies the same SQL-identifier allowlist validation as the options-based path, closing a configuration-validation parity gap.
- **Dispatch middleware propagates `OperationCanceledException` unwrapped.** When a handler or downstream middleware observes cooperative cancellation, the base dispatch middleware now rethrows the `OperationCanceledException` (including `TaskCanceledException`) intact — preserving its original stack trace — instead of wrapping it in an `InvalidOperationException`. Callers can once again catch cancellation and the cooperative-cancellation contract holds; unexpected exceptions still carry the diagnostic middleware-name wrap.
- **Ordering-validation middleware advances its per-key watermark on handler success, not on receipt.** The order check still runs before the handler — an out-of-order or duplicate sequence is rejected with `OutOfOrderMessageException` and the rest of the pipeline is not invoked — but the high-water mark now advances only after the handler completes successfully. A handler that throws no longer poisons its ordering key: the transport can redeliver the failed sequence and it is re-attempted in order, while a later sequence still cannot jump ahead of an unprocessed one.
- **Tiered event storage reads through to the cold tier on a hot-tier miss.** `UseTieredStorage(...)` now binds the read-through decorator onto the event store that every reader resolves, so once aged events are archived and trimmed from the hot tier, loading a stream transparently reads the older events back from cold storage instead of returning truncated history. The background archive service and the default `IEventStoreArchive` bind the raw hot tier only, so trimming never reads through cold; tiered storage fails fast at startup if the configured hot store cannot archive rather than silently skipping the archive.
- **Redis and MongoDB outbox stores persist the short message type name as the fallback destination, matching the SQL Server and PostgreSQL stores.** When no explicit routing destination is set on the message context, the Redis and MongoDB outbox now fall back to the message's short type name (rather than a fully-qualified type name), so the persisted destination is consistent across all outbox providers.
- **CloudEvents content-type negotiation is case-insensitive (RFC 2045).** Incoming CloudEvents whose `Content-Type` differs only in letter case (e.g. `Application/CloudEvents+JSON` vs `application/cloudevents+json`) are now recognized and processed, instead of being rejected as unsupported — content-type/media-type comparison is ordinal-case-insensitive per RFC 2045.
- **Authorization startup guard no longer over-fires on the built-in `strict` profile.** The fail-closed authorization startup guard now keys on the **default/selected** pipeline profile (via `PipelineProfileRegistry.GetDefaultProfileName()`), so `AddDefaultDispatchPipelines()` no longer throws at startup merely because the always-seeded `strict` profile declares `AuthorizationMiddleware`. The guard still fails closed when authorization is the consumer's intended-and-selected profile but its services are unresolvable — a real bypass — rather than false-failing a consumer who never selected authorization.
- **Redis and MongoDB outbox stores honor the retry backoff schedule.** The Redis and MongoDB outbox claim-gate now respects each message's `NextAttemptAt` (computed via `TimeProvider`), so a failed message is not re-claimed before its backoff window elapses — matching the SQL Server and PostgreSQL stores.
- **Inbox atomic lease-claim re-admits a failed entry for retry instead of dropping the message.** Under the self-expiring lease claim, a handler failure now leaves the entry in a `Failed` state that a redelivery re-claims (with a monotonically increasing retry count), so a transient failure is retried rather than silently lost. Only a `Processed` entry is terminal for claiming.
- **The outbox persists the real routing destination.** `EnqueueAsync` now derives each message's delivery destination from the message context instead of a hardcoded `"default"`, so a consumer's configured destination is persisted through enqueue → reserve → dispatch and honored on publish.
- **Idempotent handler middleware surfaces both the handler failure and a claim-release failure.** When a handler throws and the subsequent claim release also fails, both are now reported (aggregated) rather than the original handler exception being masked by the release error.
- **Correctness hardening across stores.** The SQL Server event store reports a nullable no-position sentinel on empty append (no longer an ambiguous `0`); Cosmos DB saga concurrency conflicts report the real `actualVersion` (not `-1`); the circuit-breaker trip boundary is corrected and its failure reason is PII-safe; `HybridCache` invalidation materializes keys once before the emptiness check; the outbox reads its payload byte-native (no lossy UTF-8 round-trip); `OutboxMessage.Equals`/`GetHashCode` are null-safe for a null body; and Consul/MongoDB leader-election fencing-token access uses interlocked reads/writes.
- **`UseCaching(WithCachingOptions: ...)` runs the configure callback exactly once.** The caching options configure delegate was previously capable of running more than once during registration; it now applies a single time.
- **SQLite event store returns the canonical no-position result on an empty append.** Appending an empty event batch now returns `null` (no position) rather than `0`, matching the cross-provider no-position contract so callers can no longer misread a `0` as a real stream position.
- **Leader-election fencing token is carried through leadership events, and the health path honours cancellation.** The fencing token issued on acquisition now propagates to `LeaderChangedEventArgs` consumers, and leader-election health checks observe the supplied `CancellationToken` instead of running unbounded.
- **Kubernetes leader-election validator rejects split-brain-prone lease/renew combinations.** The `KubernetesLeaderElectionOptions` validator now enforces the lease-duration/renew-interval sum invariant and a restore-retry lower bound, so a configuration that could permit two simultaneous leaders is rejected at startup.
- **Caching health check self-wires `ICacheHealthMonitor`.** Registering the caching health check no longer requires the consumer to separately register the health monitor.
- **Firestore CDC state store enforces monotonic-forward position updates.** The Firestore CDC state store now performs its watermark update inside a Firestore transaction with a read-in-transaction compare-and-set, rejecting an out-of-order (older-over-newer) position with `FirestoreStalePositionException` instead of silently overwriting a newer checkpoint under concurrency.
- **Google Pub/Sub nacks an oversized/poison message to the dead-letter policy instead of silently acking-and-dropping it.** An over-limit or un-processable delivery on the streaming-pull path is now negatively acknowledged so it flows to the configured dead-letter topic, rather than being acked and lost.
- **Redis leader election fails fast with an actionable error when no connection is configured** instead of a null-reference/opaque failure at first use — the startup error names the missing Redis connection registration.
- **Regex-based tenant-identity matching has a bounded ReDoS timeout.** The tenant-identity consumer pattern now runs with an explicit match timeout (and drops `RegexOptions.Compiled`), so a pathological input cannot hang the matcher.
- **Vault key-suspension check fails closed on a missing mount.** `VaultKeyProvider` previously swallowed every HTTP 404 when checking whether a key was suspended — including a *missing suspension mount* — so if the KV mount became unavailable mid-run a suspended key could read as active (fail-open). The suspension check now propagates a mount-missing 404 at every public seam (`IsKeySuspendedAsync`, `GetKeyAsync`, and the key-listing path), so a key whose suspension state cannot be confirmed is treated as unusable rather than active. **Security fix.**
- **`AwsKmsProvider` alias→key-id cache is bounded.** The internal alias resolution map is now capped (LRU-style eviction) instead of growing unbounded, preventing memory growth under churn of distinct aliases.
- **SQLite table initialization is keyed per table + connection, not a process-global flag.** `SqliteTableInitializer` previously tracked "already initialized" in a process-wide static, so a second SQLite store (a different database file / connection in the same process) saw the flag set and skipped creating its own tables — failing on first use against an unprovisioned schema. Initialization is now keyed by table + connection/file, so every distinct store provisions its own tables.
- **SQLite event store reports a concurrency conflict instead of an infrastructure error.** A concurrent append that loses the version race now returns a normal concurrency-conflict result (the same shape every other event store returns), re-reading the actual version on a fresh connection, rather than surfacing a lower-level connection exception — so optimistic-concurrency retry loops behave consistently across all event-store providers.
- **The transactional outbox preserves `TenantId` on both message-conversion paths.** Both conversion paths in the outbox processor now carry `TenantId` through enqueue → reserve → dispatch for the relational and document providers (SQL Server, PostgreSQL, Redis, MongoDB, Elasticsearch, and in-memory), so tenant isolation is not lost in transit. (Cloud-native change-feed outbox providers — Cosmos DB, DynamoDB, Firestore — are tracked separately.)
- **DynamoDB inbox store auto-creates its backing table** when missing, matching the other DynamoDB stores, instead of failing on first use against an unprovisioned table.
- **Projection replay applies the upcasting pipeline.** Events are now run through the registered upcasters during projection rebuild/replay, so projections built from older event versions see the same upcasted shape as live dispatch.

- **The Postgres outbox persists `TenantId` across every stage and scheduled path**, preserving tenant isolation through enqueue → reserve → dispatch (previously dropped on conversion). **Upgrade note:** consumers on a pre-existing outbox schema must add the `tenant_id` column, or staged messages fail with `column "tenant_id" does not exist`.
- **Outbox `Headers`/metadata round-trip across providers** -- the ElasticSearch, MongoDB, and Redis outbox stores now persist and restore message headers/metadata on reload instead of dropping them.
- **Outbox/inbox staging round-trips through the consumer-configured serializer** -- staging now uses the injected `DispatchJsonSerializer` (honoring the configured converter/resolver) instead of bypassing it.
- **Six inbox providers implement the atomic first-writer-wins claim** (`IClaimableInboxStore`) using each store's native primitive (Cosmos 409, DynamoDB `attribute_not_exists`, MongoDB duplicate-key, Redis `SETNX`, ElasticSearch/Firestore create-conflict), so concurrent duplicates cannot both be admitted.
- **Inbox TTL is honored for Cosmos DB and DynamoDB** (was configured-but-dead).
- **Outbox `EnableParallelProcessing(N)` and `WithProcessorId(...)` now reach the surfaces that read them** — parallel drain engages, and the configured processor id flows to the SQL lease `LeasedBy` column for correct claim ownership.
- **`CronScheduler` honors `EnableExtendedSyntax = false`**, rejecting extended cron expressions when disabled instead of accepting them.
- **OpenSearch query metrics are emitted** via a cached-reflection reflector (previously not recorded).
- **Advertised-unwired consolidation + correctness tail** -- each fix carries a non-vacuous independent regression lock (RED on the pre-fix code), green across the 10-shard full CI run, with both independent reviews (REVIEW_CODE + REVIEW_ARCH) approved at zero blocking findings (HEAD `2af5a4249`):
 - **Serverless handler timeout is fail-closed across all three hosts (`Excalibur.Dispatch.Hosting.AwsLambda`/`.AzureFunctions`/`.GoogleCloudFunctions`)** -- when the remaining invocation time is at or below the internal cleanup reserve, the host now **cancels the handler immediately** instead of scheduling no timeout and letting it run unbounded toward the hard platform kill (the prior `executionTimeout > 0` guard silently skipped cancellation when the remaining time was already exhausted). The timeout is derived uniformly via a shared `ComputeExecutionTimeout` floored at zero, and the cleanup-reserve + `ValidateOnStart` wiring is uniform across AWS/Azure/GCP. See [AWS Lambda → Timeout Errors](docs-site/docs/deployment/aws-lambda.md#timeout-errors).
 - **`TenantId` (and correlation/causation) preserved on direct outbox enqueue (`Excalibur.Outbox.*`, `Excalibur.Dispatch.Abstractions`)** -- direct `IOutboxStore.EnqueueAsync` paths now build the outbound message through a structural `OutboundMessage.FromContext(...)` factory, so the convenience path no longer drops `TenantId`/`CorrelationId`/`CausationId`.
 - **CDC restart-redelivery correctness (`Excalibur.Cdc.Postgres`, `Excalibur.Cdc.MongoDB`)** -- Postgres CDC state-store DDL drops the `COALESCE`-in-primary-key that could collapse distinct checkpoints, and the Mongo processor handles an empty aggregation pipeline correctly, so a restart resumes from the true last-processed position.
 - **Streaming / progress / document dispatch pipeline-bypass is detected loudly (`Excalibur.Dispatch`)** -- these dispatch paths that bypass the middleware pipeline now emit a LOUD diagnostic (event IDs `40208`/`40209`) so an unintended bypass is observable rather than silent.
 - **Saga-store tenant-drift guard (`Excalibur.EventSourcing`)** -- `TenantRoutingSagaStore` records the tenant on load and asserts it equals the ambient tenant on save, failing fast on cross-tenant drift.

- **WIDE correctness/conformance backlog burn (advertised-unwired wiring + fail-open hardening + atomicity)** -- a wide correctness/conformance sprint (~77 beads, 14 lanes); each fix carries a non-vacuous independent regression lock (RED on the pre-fix code), green across the 10-shard full CI run + Docker/TestContainers shards, with both independent reviews (REVIEW_CODE + REVIEW_ARCH/CSO) approved at zero blocking findings (HEAD `f106a4602`):
 - **Caching middleware fails open on tag-store and poison-marker errors (`Excalibur.Dispatch.Caching`)** -- tag registration (`RegisterKeyAsync`) and poison-marker removal are now wrapped so a tag-store backend error is logged and skipped rather than propagating out of dispatch and breaking core message handling (cancellation still propagates). This completes the cross-cutting-cache-must-fail-open mandate; the sibling poison-marker path already failed open.
 - **`CacheResilienceOptions` (circuit breaker / `EnableFallback`) are now wired into the cache pipeline (`Excalibur.Dispatch.Caching`)** -- previously advertised-but-inert configuration now actually engages. Additional caching-cluster correctness: negative-result cache poisoning fixed, `CachingMiddleware` no longer records a MISS for any value, key-builder is applied to `ICacheInvalidator` keys, invalidation no longer runs after the handler, and `DistributedCacheTagTracker` registration is atomic.
 - **CDC checkpoint never advances past an unprocessed change (`Excalibur.Cdc` + provider processors)** -- every processor routes its per-iteration decision through a single pure `CdcFatalGuard.Decide(...)`: success advances, a fatal fault stops loudly, a transient fault reconnects from the un-advanced checkpoint. `DynamoDbCdcProcessor` no longer masks the original exception on a mid-batch state-store save. See [CDC Troubleshooting](docs-site/docs/operations/cdc-troubleshooting.md#during-the-restore-database-unavailable).
 - **Redis outbox poll-claim is atomic (`Excalibur.Outbox.Redis`)** -- claim + reclaim now run as a single atomic Lua lease-claim, so concurrent pollers cannot double-claim a message.
 - **Package-wide Cosmos serializer reconcile (`Excalibur.Data.CosmosDb` + persisted document types)** -- framework-owned clients use System.Text.Json; injectable/consumer-supplied documents are dual-mapped (`[JsonPropertyName]` + `[JsonProperty]`) so they emit correct wire keys under the Cosmos SDK v3 default (Newtonsoft) serializer.
 - **Audit-trail integrity consolidated to one keyed-MAC + round-trip-stable canonicalization seam (`Excalibur.Dispatch.Security` + `Excalibur.Data.ElasticSearch`)** -- three inconsistent integrity mechanisms replaced by a single keyed-MAC over a canonical serialization.
 - **Keyed-DI hardening sweep + structurally keyed-safe `ServiceDescriptor` accessor (`Excalibur.Dispatch`)** -- the inner message-bus build reads keyed accessors so a keyed registration is no longer silently bypassed.
 - **Transport DI is registered iff configured (`Excalibur.Dispatch.Transport.*`)** -- closes a partial registered-iff-configured guard.
- **Transport/integration advertised-unwired wiring + carryovers** -- a focused correctness sprint closing seams where a delivery/ordering guarantee was advertised but not actually wired; each fix carries a non-vacuous independent regression lock (RED on the pre-fix code), green across the 10-shard full CI run + Docker/TestContainers shards, with both independent reviews (REVIEW_CODE + REVIEW_ARCH/CSO) approved at zero blocking findings (HEAD `9403bc805`):
 - **RabbitMQ publisher confirms make publishing at-least-once (`Excalibur.Dispatch.Transport.RabbitMQ`)** -- the sender now waits for broker publisher confirms (`RabbitMqPublisherOptions.EnableConfirms`/`ConfirmTimeout`, on by default), so a publish that the broker never acknowledged is surfaced as a failure instead of being silently dropped (at-most-once → at-least-once).
 - **Google Pub/Sub ordering and flow-control are applied on the real publish path (`Excalibur.Dispatch.Transport.GooglePubSub`)** -- message ordering keys and flow-control settings are now honored on publish rather than configured-but-inert.
 - **Azure Service Bus ordered-sessions consumer (`Excalibur.Dispatch.Transport.AzureServiceBus`)** -- the session-enabled consumer now preserves per-session FIFO ordering on the receive path.
 - **Rich keyed `ITransportSender`/`ITransportReceiver` are wired across all five transports (`Excalibur.Dispatch.Transport.*`)** -- the keyed sender/receiver resolution advertised for RabbitMQ, Kafka, Azure Service Bus, AWS SQS, and Google Pub/Sub is now actually registered and resolvable; a **subscriber-only** Google Pub/Sub registration (no `TopicId`) no longer throws on the sender guard.
 - **Redis outbox staging is atomic (`Excalibur.Outbox.Redis`)** -- the stage operation no longer leaves an orphaned partial hash on a mid-write failure.
 - **CDC fatal-error handoff guard (`Excalibur.Cdc` + provider processors)** -- a fatal change-feed error now flows through a single `CdcFatalGuard.Decide(...)` returning a `CdcFatalDecision` (advance-checkpoint / stop / reconnect), so the five CDC processors share one consistent, non-vacuously-locked fatal-handling policy instead of each diverging.
 - **Serializer-agnostic Cosmos checkpoint document (`Excalibur.Data.CosmosDb`)** -- the change-feed checkpoint document is now dual-annotated (`[JsonPropertyName]` for System.Text.Json **and** `[JsonProperty]` for Newtonsoft) on all four properties, so durable continuation is no longer inert under the Cosmos SDK v3 default (Newtonsoft) client.
 - **`MessageMetadata` attributes/items/claims survive the JSON round-trip (`Excalibur.Dispatch`)** -- the metadata `Attributes`, `Items`, and `Claims` collections are now preserved through serialize→deserialize.
 - **Builder `Add*` accumulates instead of replacing (`Excalibur.Dispatch`)** -- repeated `Add*` builder calls no longer silently discard earlier registrations (a P1 silent-data-loss seam).
 - **A disposed outbox store fails loud instead of swallowing writes as a dup-id no-op (`Excalibur.Outbox`)**.
 - **The `$erased` GDPR sentinel is centralized on `ErasedEventMarker.EventType` (`Excalibur.EventSourcing`)** -- removing a divergent inline literal.
 - **Postgres saga SQL identifier validation (`Excalibur.Saga.Postgres`)** -- a `SagaSqlValidator` now whitelist-validates schema/table identifiers (SQL-injection defense-in-depth), matching the SQL Server saga store.
- **WIDE correctness burn** -- a wide max-throughput correctness sprint; each fix carries a non-vacuous independent regression lock (RED on the pre-fix code), green across the 10-shard full CI run + Docker/TestContainers shards (`-m:1`), with both independent reviews (REVIEW_CODE + REVIEW_ARCH/CSO) approved at zero blocking findings (HEAD `d05e91ba8`):
 - **Leader-election fencing tokens, fail-closed (`Excalibur.LeaderElection.Redis`/`.Postgres`/`.SqlServer`)** -- a monotonic fencing token is now minted **before** leadership is granted (Redis mint-before-grant with post-fence event ordering; SqlServer and Postgres `SEQUENCE`-backed fencing-token providers, plus a corrected T-SQL create that previously failed to parse on real SQL Server), so a stale leader cannot act with a lower token, and token issuance failure fails closed (no grant).
 - **Kafka subscribe path now resolves its `IConsumer` (`Excalibur.Dispatch.Transport.Kafka`)** -- the consumer was advertised but never wired, so the subscribe path could not resolve; DI now provides it.
 - **AWS SQS bus is DI-resolvable and FIFO ordering applies on every publish path (`Excalibur.Dispatch.Transport.AwsSqs`)** -- `AwsSqsMessageBus` now resolves via `IOptions<AwsSqsOptions>`, and the FIFO message-group id + dedup id are applied on **all** publish paths (previously inert on some).
 - **Event-sourcing fail-closed cluster (`Excalibur.EventSourcing` + `Excalibur.Domain` + `Excalibur.EventSourcing.Redis`)** -- snapshot upgrade is fail-closed, eventually-consistent staging recovery re-stages idempotently, erased-event replay honors the `$erased` sentinel, the `AggregateRoot` base `ApplySnapshot` is fail-closed (symmetric with `CreateSnapshot`), and Redis new-aggregate creation requires an empty stream (optimistic concurrency).
 - **Observability middleware fails open on instrumentation failure (`Excalibur.Dispatch.Observability`)** -- an exception while recording telemetry no longer breaks dispatch; the core operation continues and the failure is logged.
 - **Saga `LoadAsync` type-isolation across providers (`Excalibur.Saga.SqlServer`/`.Postgres`/`.MongoDB`)** -- a saga load no longer risks cross-contaminating state across distinct saga types.
 - **Outbox restores W3C `baggage` on the consume hop (`Excalibur.Outbox`)** -- baggage is now restored symmetrically with `traceparent`.
 - **Opt-in `UseReconnect` builder extension makes `ReconnectingTransportSubscriber` reachable (`Excalibur.Dispatch.Transport.Abstractions`)** -- the reconnecting decorator was unreachable; an explicit builder extension now opts it in.
 - **Cron `GetMissedExecutions` excludes the current (`== now`) occurrence (`Excalibur.Dispatch`)**.
 - **SqlServer leader-election `DisposeAsync` no longer hot-spins on error, and renewal filters `OperationCanceledException` (`Excalibur.LeaderElection.SqlServer`)**.
 - **Elasticsearch audit `DeleteByQuery` honors the bounded batch size, and projection exact-match field naming derives from the declared mapping (`Excalibur.Data.ElasticSearch`)**.
- **Reliability & wiring correctness (advertised-but-broken / concurrency sweep)** -- A focused sweep closing wiring/registration/correctness gaps where advertised behavior did not actually fire, plus concurrency/memory hazards; each fix carries a non-vacuous independent regression lock (RED on the pre-fix parent `1cb23744b`), green across the 10-shard full CI run + Docker container shards, with both independent reviews (REVIEW_CODE + REVIEW_ARCH/CSO) approved at zero blocking findings:
 - **`RetryMiddleware` now classifies failed *results* as transient vs permanent instead of retrying every failure (`Excalibur.Dispatch`)** -- a failed `IMessageResult` is retried only when its RFC 7807 status is transient (`408`, `429`, or `5xx`), matching Polly / `HttpClientFactory` `HandleTransientHttpError` semantics. A `4xx` other than 408/429, and a failed result with no `ProblemDetails`/`Status`, are now **permanent → not retried** — previously every non-success result was retried, re-running non-idempotent handlers on permanent client errors. Exception-based retry (`RetryableExceptions`/`NonRetryableExceptions`) is unchanged. The computed backoff is also clamped to `MaxDelay` *before* the `TimeSpan` is constructed, so a high attempt count collapses to `MaxDelay` instead of throwing `OverflowException` (the `ExponentialWithJitter` path previously returned an uncapped delay). **Behavior change** — see [Retry Middleware](docs-site/docs/middleware/built-in.md#retry-middleware).
 - **Inbox retry now honors exponential backoff (`Excalibur.Inbox.SqlServer` + `Excalibur.Outbox`)** -- the inbox processor scheduled failed entries with a hardcoded 5-minute window and never persisted the computed backoff. It now persists `NextAttemptAt = now + CalculateDelay(attempt)` via the new optional `IBackoffSchedulableInboxStore` capability (`MarkFailedWithBackoffAsync`) and fetches only entries with `NextAttemptAt IS NULL OR NextAttemptAt <= now`; the SQL Server inbox store implements it, stores without it fall back to the immediate-retry `MarkFailedAsync` path (fail-open), and the capability is forwarded through the telemetry/encrypting inbox decorators. **SQL Server inbox users must add a `NextAttemptAt DATETIMEOFFSET NULL` column** to the inbox table — see [Inbox → Retry Backoff Schedule](docs-site/docs/patterns/inbox.md#retry-backoff-schedule).
 - **PostgreSQL outbox now applies retry backoff (`Excalibur.Outbox.Postgres`)** -- the Postgres outbox store now implements `IBackoffSchedulableOutboxStore` (`MarkFailedWithBackoffAsync`, signature-identical to SQL Server), so the computed backoff throttles re-claim and the claim query excludes not-yet-due rows. Remaining providers (Redis/MongoDB/Elasticsearch/DynamoDB/Cosmos DB) retain the fail-open immediate-retry path and are tracked as follow-ups. See [Outbox → Ordering and Retry Scheduling](docs-site/docs/patterns/outbox.md#ordering-and-retry-scheduling).
 - **Sagas persist before they dispatch (`Excalibur.Saga`)** -- commands/events emitted during `HandleAsync` (via `SendCommandAsync`/`PublishEventAsync`) are now buffered and dispatched only **after** the saga state is durably persisted (save-then-dispatch, FIFO order). Previously a command dispatched immediately and `SaveAsync` ran afterward, so a persistence failure + replay re-dispatched it (duplicate side effects); now a `SaveAsync` failure dispatches nothing and the emitted messages re-buffer on the next delivery. The helpers remain `protected` but no longer return a dispatch result. See [Sagas → Save-Then-Dispatch Ordering](docs-site/docs/sagas/index.md#save-then-dispatch-ordering).
 - **`ISagaNotFoundHandler<TSaga>` is now invoked (`Excalibur.Saga`)** -- an event arriving for a non-existent saga previously only logged-and-returned even though `ISagaNotFoundHandler<TSaga>` existed and was registered. The coordinator now resolves and invokes it; a default `LoggingNotFoundHandler<TSaga>` is registered out of the box, and `WithNotFoundHandler<TSaga, THandler>()` registers a custom handler to dead-letter/park/compensate (fail-open to the warning log if none is resolvable). See [Sagas → Handling Events for Missing Sagas](docs-site/docs/sagas/index.md#handling-events-for-missing-sagas).
 - **Leader-election renewal timestamps are read lock-free without a torn read (`Excalibur.LeaderElection.Redis`/`.Postgres`/`.SqlServer`)** -- the last-successful-renewal time was a multi-field `DateTimeOffset` read outside the lock in the renewal loop but written inside it, so a torn read could miscompute the grace/split-brain window. All three stores now store ticks in a `long` and read/write via `Interlocked.Exchange`/`Read`.
 - **Event-sourcing concurrency hardening (`Excalibur.EventSourcing` + `Excalibur.Dispatch`)** -- the snapshot-tracking dictionary is now bounded (cap ≈ 1024, re-derive on miss) to prevent unbounded growth for high-cardinality aggregates; `EventVersionManager`'s upgrader map is now thread-safe (`ConcurrentDictionary` + lock, matching `SnapshotVersionManager`); and a handler-warmup-cache TOCTOU NRE on first dispatch racing `FreezeCache()` was closed with a single local-copy read.

### Security

- **Tenant-facing event-store, inbox, and projection reads/writes fail closed on a missing ambient tenant.** The multi-tenant event-store, inbox, and projection-store paths previously fell back to an un-scoped query when no tenant resolved (an internal `?? string.Empty` residue), which could return or overwrite another tenant's rows — a cross-tenant read-leak and message-suppression vector. Every tenant-facing path now throws `ArgumentException` when the resolved tenant is null or whitespace, so the query is **always** tenant-scoped and a misconfiguration fails loudly instead of silently crossing the tenant boundary. The inbox dedup conflict target is always `(message_id, handler_type, tenant_id)`, making cross-tenant message loss structurally inexpressible. Dependency injection always registers a non-null default tenant context, so single-tenant applications are unaffected.
- **Key-escrow quorum secret is zeroed immediately after it is persisted.** The reconstructed quorum secret is scrubbed from memory as soon as the token is persisted, closing the window in which recovered key material lingered in a heap buffer.
- **Real secret scrubbing for in-memory plaintext.** The shared secret-handling helper now holds plaintext in a pinned `char[]` and zeroes it in a `finally` on all paths (including delegate-throw), replacing prior copy-then-zero-a-throwaway behavior that left the real buffer un-scrubbed.
- **HashiCorp Vault credential store rejects a non-`https` URL at construction**, preventing a plaintext token over an insecure transport.
- **Vault secret paths are `Uri.EscapeDataString`-encoded**, preventing reserved characters in a path segment from altering the request.
- **Honest AOT signal for `Excalibur.Security.Aws`** — the package no longer claims AOT compatibility it could not deliver; the trim/AOT status is reported truthfully.
- **MessagePack untrusted-data hardening, audit keyed-MAC, and raw-PII masking** -- the MessagePack serializer's no-options default now deserializes in `MessagePackSecurity.UntrustedData` mode (MessagePack-CSharp's guidance for off-process transport/inbox input, guarding against deep-nesting / hash-collision attacks), and the default System.Text.Json options enforce a bounded `MaxDepth`. Audit-trail integrity moves to a keyed-MAC (tamper-evident under a secret key, not a bare hash) over a round-trip-stable canonical serialization, and `SecurityAuditWriter` no longer writes raw PII (it follows the existing hashing-sanitizer pattern). See [Serialization Providers → MessagePack](docs-site/docs/middleware/serialization-providers.md#messagepack).

- **`JsonEventSerializer` rejects unregistered event types by default (`Excalibur.Dispatch.Abstractions`)** -- deserialization previously resolved an arbitrary type name by scanning every loaded assembly (`AppDomain.CurrentDomain.GetAssemblies()`), a gadget-chain deserialization vector that could resolve an attacker-chosen type. The assembly scan is now **off by default**: an unregistered type name is rejected with `UnknownEventTypeException`. Register your event types — `AddEventTypes<TEvent>()`, `AddEventTypes(params Type[])`, or `AddEventTypesFromAssembly(Assembly)` (secure, recommended) — and a registered type resolves through the allow-list **independently of any scan**, so the secure default is fully usable for event sourcing. For AOT/trimming use `AotJsonEventSerializer` (source-generated type map). To restore the legacy reflection scan in a trusted environment, construct `JsonEventSerializer(allowAssemblyScan: true)`. **Behavior change / security hardening.**

- **ASP.NET Core authorization faults no longer leak (`Excalibur.Dispatch.Hosting.AspNetCore`)** -- when the authorization middleware's evaluation **threw**, it returned HTTP 403 carrying the raw `ex.Message`, both masking a server-class error as a denial and leaking internal detail across the trust boundary. An evaluation exception now returns **HTTP 500** with a generic sanitized message and logs the full exception server-side; an authorization **denial** (not an exception) still returns 403.

- **Messaging reliability hardening (outbox/inbox advertised-but-broken sweep)** -- A focused P1 cluster closing the default-dispatch and outbox seams that advertised a guarantee they did not honor; each fix carries a non-vacuous independent regression lock (RED on the pre-fix parent `83fce02c8`), green across the 10-shard full CI run + Docker SQL Server TestContainers shards, both independent reviews (REVIEW_CODE + REVIEW_ARCH/CSO) approved at zero blocking findings:
 - **The default dispatch pipeline now runs registered middleware on `DispatchAsync` (`Excalibur.Dispatch`)** -- `AddDispatch`'s default pipeline resolved to an empty profile, so `DispatchAsync` bypassed **all** middleware and outbox staging silently never ran on the default path. The default pipeline is now wired to the `default` profile, so the registered default-profile middleware (notably `OutboxStagingMiddleware`) execute out of the box; profile middleware the consumer has not registered skip gracefully (fail-open) with an `InvokerMiddlewareSkipped` (event ID 10024) debug log — only registered middleware run. **Behavior change** — outbox staging and the other default middleware now run by default. See [Pipeline Profiles](docs-site/docs/pipeline/profiles.md).
 - **A consumer-registered `IPipelineProfileRegistry` is preserved instead of clobbered (`Excalibur.Dispatch`)** -- the `DispatchBuilder` constructor unconditionally `Services.Replace(...)`'d the profile registry, discarding a consumer's override. It now guards the replace to a framework-default whitelist (only replacing the framework's own default registration), and `UseProfile` on an unknown profile key throws `ArgumentException` at configuration time (fail-loud) rather than silently resolving an empty pipeline.
 - **Outbox ordering keys are persisted and honored, and exponential backoff is actually applied (`Excalibur.Outbox.SqlServer` + `Excalibur.Dispatch.Abstractions`)** -- messages now store `PartitionKey`/`GroupKey`/`SequenceNumber`, and the SQL Server claim query selects rows in `(PartitionKey, SequenceNumber)` order so same-partition messages are delivered in ascending sequence (per-partition FIFO). On a delivery failure the processor records the next-attempt time on `NextAttemptAt` and the claim predicate excludes the message until it elapses, so the computed exponential backoff genuinely throttles re-delivery — previously the backoff was computed but never applied, so a failed message was re-claimed as soon as its lease expired. A circuit-breaker-open short-circuit is excluded from backoff (no delivery was attempted, so it stays immediately retryable). The new optional `IBackoffSchedulableOutboxStore` capability (`MarkFailedWithBackoffAsync`) carries the schedule; the SQL Server store implements it, stores without it fall back to the existing immediate-retry `MarkFailedAsync` path (fail-open), and the capability is forwarded transparently through the telemetry and encrypting store decorators. **SQL Server outbox users must add the `PartitionKey`/`GroupKey`/`SequenceNumber`/`NextAttemptAt` columns and the `IX_OutboxMessages_Claim` index** to the `OutboxMessages` table — see [Outbox schema](docs-site/docs/patterns/outbox.md#sql-server).
 - **Outbox publishing propagates `TenantId` and `CausationId` to the transport message (`Excalibur.Outbox`)** -- both were dropped when the background publisher rebuilt the message context from the staged outbox row, breaking multi-tenant routing and cause-effect tracing for outbox-delivered messages; they are now carried through symmetrically with the inbox restore side.
- **Large-batch P1 correctness sweep (projection data-loss / options-validation / architecture-enforcement + the transactional event+outbox keystone)** -- An aggressive max-throughput P1 batch; each fix carries a non-vacuous independent regression lock (RED on the pre-fix parent `125d7aa36`), all green across the 10-shard full CI run + Docker/emulator TestContainers shards, with both independent reviews (REVIEW_CODE + REVIEW_ARCH/CSO) approved at zero blocking findings:
 - **DynamoDB & Firestore projection queries now apply filters server-side (`Excalibur.Data.DynamoDb`, `Excalibur.Data.Firestore`)** -- `IProjectionStore<T>.QueryAsync`/`CountAsync` silently **ignored the `filters` argument** and returned unfiltered (over-broad) result sets — a data-correctness defect that could leak rows across a filter boundary. Both stores now translate filters into provider-native predicates: DynamoDB AND-combines them into a server-side `ScanRequest` `FilterExpression` (distinct `#f{n}`/`:v{n}` placeholders so a filter key equal to the type discriminator does not collide; string/number/bool attribute mapping), and Firestore issues real `Where(key, ==, value)` against a write-only flat `_q` index map (top-level scalar properties as Firestore-native camelCase values; the canonical `data` JSON blob stays the deserialization source of truth, preserving exact `decimal`/`DateTimeOffset` round-trip fidelity). A null/empty filter returns all rows; an untranslatable (e.g. nested-key) filter throws `NotSupportedException` rather than silently returning unfiltered.
 - **DynamoDB cursor pagination reports a true total and fills each page (`Excalibur.Data.DynamoDb`)** -- `QueryCursorAsync` issued a full `Select.COUNT` scan **per page** and reported a truncated partial page as the `TotalCount`. It now fills each page to `pageSize` *matched* items by looping `LastEvaluatedKey` (DynamoDB's `Scan` `Limit` applies pre-filter), computes the true total once and carries it in the cursor (`[pk, total]`) for ≤1 COUNT scan per walk, and returns a `null` cursor on exhaustion (no short page with a phantom continuation cursor).
 - **`GlobalStreamProjectionHost` saves the cursor map before advancing the checkpoint (`Excalibur.EventSourcing`)** -- the checkpoint (the source of truth) was saved **before** the cursor map and the pending-cursor buffer was cleared only on the success path, so a crash or `SaveCursorMapAsync` throw could leave the checkpoint advanced ahead of a durable cursor map (restart divergence) and grow `_pendingCursorUpdates` unboundedly under repeated save errors. The order is inverted to **cursor-map first → checkpoint last** (both the periodic flush and the graceful-shutdown flush), so the checkpoint is never ahead of a durable cursor map, and the pending buffer is bounded by clearing/rebuilding it on the error path too.
 - **Kafka dead-letter options are validated unconditionally at startup (`Excalibur.Dispatch.Transport.Kafka`)** -- DLQ options were registered with **no validation** across all three registration paths (action, named-transport, `IConfiguration` overload), so an invalid DLQ config (e.g. `MaxDeliveryAttempts = 0`, empty `TopicSuffix`) surfaced only at first use. A new `KafkaDeadLetterOptionsValidator` (`IValidateOptions<KafkaDeadLetterOptions>`) is wired with `ValidateOnStart()` on every path, so invalid options now throw `OptionsValidationException` at host start.
 - **Polly resilience options validate on the convenience overload too (`Excalibur.Dispatch.Resilience.Polly`)** -- `AddPollyResilience()` called `ValidateOnStart()` only inside the `configuration != null` branch, so the convenience (no-configuration) overload registered the resilience options **without** their validators — invalid values were never caught at startup. `AddOptions<T>().ValidateOnStart()` now runs unconditionally for `TimeoutManagerOptions`, `GracefulDegradationOptions`, and `DistributedCircuitBreakerOptions`; only `.Bind(configuration)` stays gated on a supplied configuration.
- **P1 correctness/security sweep (18 fixes across 11 lanes)** -- An aggressive max-throughput P1 batch; each fix carries a non-vacuous independent regression lock (RED on the pre-fix code), all green across the 10-shard full CI run + Docker TestContainers shards, both independent reviews (REVIEW_CODE + REVIEW_ARCH/CSO) approved with zero blocking findings:
 - **Authorization grants now filter expired entries by default across every store (`Excalibur.A3.*` + all 7 grant stores)** -- `IGrantStore.GetAllGrantsAsync` honored expired grants on the read/decision path, so a lapsed grant could still authorize. The query is now **default-secure (active-only)** with an explicit `includeExpired` opt-in across InMemory/SqlServer/Postgres/MongoDB/CosmosDb/DynamoDb/Firestore (server-side filtering where the provider supports it), backed by pure `Grant.IsActive(asOf)`/`IsExpired(asOf)` predicates; honoring an expired grant on the decision path is now structurally inexpressible. The risk assessor also fails safe to `MaxRiskScore` (100) on an unknown risk.
 - **Audit-log integrity is now a keyed HMAC, fully fail-closed (`Excalibur.Data.ElasticSearch`)** -- audit records were integrity-stamped with an **unkeyed** `SHA-256` (forgeable: anyone could recompute it), and verification masked an unavailable key. Both the write and verify paths now use **HMAC-SHA256 via the new `IAuditSigningKeyProvider`** (`v1:{keyId}:{base64(tag)}` token, constant-time compare); a tampered record fails verification, and an unavailable/unknown key **fails closed** (verification returns false, write throws) rather than emitting an unkeyed tag. Shamir secret sharing additionally rejects sub-threshold reconstruction and tampered shares.
 - **Message-signing key resolution is fail-loud; the dead fabrication path is removed (`Excalibur.Security`)** -- `AddMessageSigning` registered an HMAC signer needing an `IKeyProvider` that nothing supplied, and the only `IKeyProvider` impl (`SecureKeyProvider`) was compile-excluded dead code that would have **minted a key on a miss** (silent fabrication). `SecureKeyProvider` is **deleted** (no fabrication route), and a new `SigningKeyProviderStartupValidator` **fails loud at host start** (`InvalidOperationException` naming the missing provider) instead of failing at first dispatch.
 - **Snapshot store round-trips metadata and guards against version regression (`Excalibur.EventSourcing.SqlServer`)** -- snapshot save/load dropped the `Metadata` dictionary and could overwrite a newer snapshot with an older one. A `Metadata` column now round-trips, and the MERGE updates only `WHEN MATCHED AND source.Version > target.Version`.
 - **CDC processors advance the commit position only at the batch boundary (`Excalibur.Cdc.DynamoDb` + `Excalibur.Cdc.Postgres`)** -- the DynamoDb processor advanced its shard iterator **before** handling records (loss on a mid-batch throw); it now advances only after the whole batch is handed off and re-acquires via `AFTER_SEQUENCE_NUMBER` of the last handled record on a throw. The Postgres processor adds a durable commit-boundary WAL-ack (`ConfirmCommitAsync`), removing the per-message status advance + mid-transaction position save.
 - **InboxProcessor honors the configured retry ceiling (`Excalibur.Outbox`)** -- the reservation query hardcoded `maxRetries = 3` instead of the configured `MaxAttempts`, so a consumer's retry policy was silently ignored. It now uses `_options.MaxAttempts`.
 - **OutboxMiddleware logs the honest staged count (`Excalibur.Dispatch`)** -- on a partial staging failure the middleware logged full success (`outboundMessages.Count`); it now logs only the actually-staged count and emits `LogStagingPartialFailure(staged, total, failed)` (Warning) instead of a phantom success.
 - **BatchChannelReader uses a fresh timeout per flush window (`Excalibur.Dispatch`)** -- a reused `CancellationTokenSource` across windows meant a once-cancelled token stayed cancelled (no further timed flushes); each window now builds a fresh `timeoutCts`+`linkedCts`, and a timeout `OperationCanceledException` is never surfaced as an error.
 - **JobOptionsHostedWatcherService closes an async-void escape (`Excalibur.Jobs`)** -- an unobserved async-void path could let an exception escape the watcher; the handler is fixed (with a justified `CA1031` suppression for the boundary catch).
 - **JsonClaimCheckSerializer honors the serializer error contract (`Excalibur.Dispatch.Patterns`)** -- all four serialize/deserialize methods now throw `SerializationException` on a null/failed result instead of returning a null-forgiving value.
 - **Removed fabricated `HealthMetrics` properties (`Excalibur.Dispatch.Resilience.Polly`)** -- `HealthMetrics.ResponseTimeMs`/`ActiveConnections` were advertised-but-unwired (hardcoded `0`, never read by `DetermineLevel`). Per Microsoft-first "don't ship a fake signal" they are **removed** rather than wired to a synthetic source. **Breaking** (greenfield).
- **Distributed-tracing correctness + concurrency/leader-election sweep** -- A P1 correctness batch; each fix has a non-vacuous independent regression lock (RED on the pre-fix code), all green in the full CI run + Docker integration shard:
 - **End-to-end W3C trace context now survives the outbox hop (`Excalibur.Dispatch` + `Excalibur.Dispatch.Observability`)** -- distributed tracing was broken across every async outbox boundary: staging never captured the ambient `Activity.Current`, so outbound envelopes carried an empty `traceparent`, and `TracingMiddleware` started a **new root span** instead of childing the restored inbound context — orphaning traces. Staging now captures the ambient `traceparent` (a caller-set value takes precedence; no activity and no caller value → no header), publishing restores it onto the outgoing transport context, and the dispatch span re-parents to the restored context as an `ActivityKind.Consumer` child (or, when an ambient activity is already in scope, attaches it as an `ActivityLink` rather than hijacking the local trace). Malformed/absent `traceparent` fails open (new in-process root, never throws); the no-listener fast path is preserved. A producer → outbox → publish → consumer flow is now **one connected trace**.
 - **Leader-election lock paths are reentrancy-safe and probe real ownership (`Excalibur.LeaderElection.SqlServer`, `Excalibur.LeaderElection.Postgres`, `Excalibur.LeaderElection.Redis`)** -- two correctness defects: (1) on the Lost/Stop paths all three providers raised `LostLeadership`/`LeaderChanged` handlers **inside** the coordinator `_lock` (only `BecomeLeader` was correct), risking reentrancy/deadlock if a handler re-entered the coordinator — handlers are now snapshotted under the lock and invoked **outside** it; (2) `VerifyLockAsync` ran `SELECT 1` (connection liveness), which could mask a silently-lost lease, and now probes **actual session lock ownership** (SqlServer `APPLOCK_MODE('public', @Resource, 'Session')`, Postgres `pg_locks` advisory entry).
 - **Transport adapter start/stop is now race-free (`Excalibur.Dispatch`)** -- `TransportAdapterHostedService` mutated its `_startedAdapters` list without `_lock` on the `IHostedService` path while the lifecycle-manager path already synchronized it — a data race / torn-state hazard. All access is now consistently guarded by the existing `_lock`.
- **Handler-DI correctness + serialization framing (2 fixes)** -- A P1 reliability sweep; each fix has a non-vacuous independent regression lock (RED on the pre-fix code):
 - **Handler scope resolution now detects *transitively* scoped constructor dependencies (`Excalibur.Dispatch`)** -- the scope resolver inspected only a handler's **direct** constructor dependencies for a `Scoped` lifetime, so a handler that reached a scoped service through a `Transient` intermediary was still resolved from the root container — a captive dependency (silent under `ValidateScopes=false`). The resolver now walks the constructor-dependency graph recursively (cycle-guarded, registration-metadata only, biasing to a scope on an unprovable edge), so a transitively-reachable scoped dependency correctly forces a per-dispatch scope. The root-resolvable hot path (transient/singleton-only graphs) is unchanged and cached. See.
 - **ClaimCheck serializer frames payloads with a 1-byte tag instead of an in-band magic prefix (`Excalibur.Dispatch.Patterns`)** -- envelope-vs-inline classification used a `"CC01"` magic prefix that a binary base serializer (MessagePack/MemoryPack/Protobuf) payload could coincidentally begin with, causing misclassification (sync round-trip throw, async corruption). Every emitted payload now carries a leading 1-byte frame tag the ClaimCheck layer exclusively owns (`0x00`=inline, `0x01`=envelope); a payload with an unrecognized leading tag throws a typed `SerializationException` rather than falling back to a colliding heuristic. **Format change** (greenfield, no shipped consumers): a payload written by a prior build is not readable by the framed reader.
- **transient circuit-breaker dead-letter correctness (2 fixes)** -- An open circuit breaker is a **transient short-circuit, not a delivery failure**: the message never reached the handler/transport, so dead-lettering it (and, on the high-volume batch path, dead-lettering an entire batch) lost messages on a recoverable outage. Both processors now treat an open breaker as retryable. Each fix has a non-vacuous independent engage-test (RED on the pre-fix code):
 - **`InboxProcessor` retries on an open circuit breaker instead of dead-lettering (`Excalibur.Outbox` + inbox stores)** -- both the pre-dispatch check and a mid-dispatch `CircuitBreakerOpenException` now leave the inbox entry re-admittable for retry with its **attempt count unchanged** (no attempt consumed) and **without writing the deduplication store** (a dedup write would make the retry look like a false duplicate), rather than routing it to the DLQ. Only genuine `MaxAttempts`-exhausted failures dead-letter (`DeadLetterReason.MaxRetriesExceeded`).
 - **`OutboxProcessor` leaves a record re-claimable on an open circuit breaker (`Excalibur.Outbox`)** -- the single-record (`DispatchReservedRecordAsync`) and high-volume batch paths now leave a circuit-breaker-open record re-claimable with its attempt count unchanged, so the next poll retries once the breaker recovers, instead of dead-lettering (which on the batch path caused bulk loss on a transient outage). Only `MaxAttempts`-exhausted failures dead-letter.
- **concurrency-correctness cluster (3 fixes)** -- A targeted P1 sweep of lock-discipline and fail-open defects in the caching and threading primitives. Each fix is a thread-safety correctness change with a non-vacuous independent engage-test (RED on the pre-fix code); all three public types keep their existing signatures (`ICacheKeyBuilder.CreateKey`'s nullable return is noted under **Changed**):
 - **`LruCache<TKey,TValue>` no longer holds its lock across the value factory (`Excalibur.Dispatch.Caching`)** -- `GetOrAdd` invoked the user-supplied `valueFactory` while holding the cache lock, so a slow factory (a database read, HTTP call, or deserialization) serialized every other key's cache operation behind it, and a factory that re-entered the same cache could deadlock or corrupt LRU state. The factory now runs **outside** the lock — matching the `ConcurrentDictionary.GetOrAdd` contract (under concurrent misses for the same key the factory may run more than once, but exactly one entry is committed and every caller receives it) — followed by a lock re-acquire + double-check that returns the winning value if another thread committed first, with the winner inserted **inline** (never via a re-entrant `Set()`, closing the evict-and-discard defect). Public signature unchanged.
 - **`KeyedLock` closes the keyed-semaphore cleanup race (`Excalibur.Dispatch`)** -- the per-key `SemaphoreSlim` could be removed and disposed while a concurrent waiter still referenced it (a waiter observing `ObjectDisposedException`, or two holders acquiring one key). Each key now carries a reference count mutated exclusively under the lock; the semaphore is removed and disposed only when the last reference is released, making "remove a key's semaphore while it is still referenced" structurally inexpressible. A follow-up made `LockHandle._disposed` `volatile` to satisfy the project's disposed-field memory-visibility conformance. Public signatures unchanged.
 - **`DefaultCacheKeyBuilder` fails open instead of fabricating a cache key (`Excalibur.Dispatch.Caching`)** -- when a cache key could not be derived — an `ICacheable<T>` action whose `GetCacheKey()` reflection failed, a runtime type with no resolvable name, or an unserializable action — the builder risked returning a fabricated/guessed key that could cause a false cross-request cache hit (one caller's data served to another). `CreateKey` now returns `null` ("do not cache") for any "cannot derive a key" condition — never throwing, never fabricating a substitute key — and the caching middleware skips the cache and invokes the handler directly. A cross-cutting cache must never break (or corrupt) the core operation. Skipped derivations are logged at `Debug`. See [Caching → Key derivation and fail-open behavior](docs-site/docs/performance/caching.md).
- **silent-failure / false-guarantee P1 sweep** -- Continues the class-extinction onto seams that advertised a durability/correctness guarantee but silently shipped *not-X* — a dropped event reported as success, an "atomic" contract that wasn't, a recovery path that never recovered. Each fix surfaces or structurally prevents the silent failure, with a non-vacuous independent engage-test (RED on the pre-fix code) or a structural contract change:
 - **`DefaultAuditLogger` is now fail-closed (`Excalibur.AuditLogging` / `Excalibur.Compliance.Abstractions`)** -- a store failure in `IAuditLogger.LogAsync` was caught and masked behind a success-shaped `AuditEventId` (the `SequenceNumber == -1` sentinel), so a dropped compliance event looked durably recorded. `LogAsync` now **throws the new `AuditPersistenceException`** on a store failure; a returned `AuditEventId` therefore always denotes a durably persisted event. Genuine cancellation (`OperationCanceledException`) is rethrown unwrapped. Callers that need fail-open availability must catch `AuditPersistenceException` and apply their own retry/queue policy. **Behavior change** — see [Audit Logging → Failure Handling](docs-site/docs/security/audit-logging.md).
 - **Atomic claim-protocol for inbox idempotency (`Excalibur.Dispatch` + inbox/dedup stores)** -- `IdempotentHandlerMiddleware` used a racy check-then-act (`IsProcessedAsync`/`IsDuplicateAsync` then a later mark), so two concurrent duplicates could both pass the check. It now **claims atomically before the handler runs** via the new `IClaimableInboxStore`/`IClaimableDeduplicator` capability (Postgres `INSERT … ON CONFLICT DO NOTHING`, SqlServer `MERGE WITH (HOLDLOCK)`, in-memory `ConcurrentDictionary.TryAdd`) and **releases the claim on handler failure** so a redelivery is re-admitted (a claim-then-leave-terminal would silently drop a failed message). A startup `ValidateOnStart` guard fails fast if a registered store/deduplicator lacks the capability; store decorators forward it and fail loud (`NotSupportedException`) on a non-claimable inner.
 - **`OutboxMiddleware` documents an honest delivery contract (`Excalibur.Dispatch`)** -- the XML doc claimed transactional "Atomicity / guaranteed delivery". The default outbox dispatches **after** the business transaction commits — at-least-once with a crash window between commit and publish, not atomic. The contract now states this plainly and points to the Transactional outbox strategy for zero-loss semantics. No false guarantee is advertised.
 - **`DistributedCircuitBreaker` manual-path cross-instance recovery (`Excalibur.Dispatch`)** -- the manual `RecordSuccessAsync` close-gate read a stale local `_lastKnownState` instead of the authoritative shared store, so a breaker opened on one instance never recovered via another instance's successes. The close-gate now reads `GetStateAsync` (the authoritative store); the `ExecuteAsync` fast path is preserved.
 - **Gap-tolerant cross-provider range paging (`Excalibur.EventSourcing.SqlServer` + `Excalibur.EventSourcing.Postgres`)** -- `ReadRangeAsync` stopped at the first empty batch (`break`), so a gap in the global position sequence silently truncated parallel catch-up reads. Both stores now advance `currentPosition = batchEnd + 1` past gaps, bounded by the caller's `toPosition` (no unbounded tail scan), verified by Docker SQL + Postgres integration engage-tests.
 - **Postgres `GlobalPosition` is now populated (`Excalibur.EventSourcing.Postgres`)** -- `PostgresRangeQueryEventStore` returned `StoredEvent` rows with `GlobalPosition = 0` (the ctor default), breaking global-ordinal ordering and idempotency keys for consumers reading the Postgres global stream. It now selects and maps the real `global_position` column.
- **P1 tail of the "advertised-but-unwired" class** -- Closes the P1 tail of the same reliability/compliance defect class opened: the framework advertised a durability/compliance guarantee but silently shipped *not-X*. Each fix surfaces the failure path explicitly (throw / fail-fast / halt / terminal-status — never a swallowed failure logged as success) with a non-vacuous independent engage-test (RED on the pre-fix code) or structural enforcement that makes the unwired state inexpressible:
 - **HashiCorp Vault & AWS Secrets Manager credential stores now persist for real (`Excalibur.Security`, `Excalibur.Security.Aws`)** -- both stores were config-fallback placeholders that read plain `IConfiguration` and **silently discarded** every `StoreCredentialAsync` call while logging success. The Vault store now reads/writes the real KV v2 HTTP API via an injectable `IVaultSecretClient` seam; the AWS store persists through `IAmazonSecretsManager` (new `AWSSDK.SecretsManager` dependency, AWS package only). A `StoreCredentialAsync` followed by `GetCredentialAsync` now round-trips against the backend, and a backend failure is surfaced as an error (never logged-as-success). **Behavior change:** the HashiCorp Vault store is **no longer default-registered** — `AddSecureCredentialManagement` registers `EnvironmentVariableCredentialStore` as the default `ICredentialStore` and only wires the Vault store when `Vault:Url` is configured.
 - **Outbox terminal `DeadLettered` status (`Excalibur.Dispatch.Abstractions` + stores)** -- a retry-exhausted/dead-lettered outbox message had no terminal state: it stayed `Failed`, was re-claimed by the delivery poller after its lease expired, and was re-delivered and re-dead-lettered forever (duplicate delivery + unbounded DLQ growth). Messages now transition to the terminal `OutboxStatus.DeadLettered`, which every store's claim predicate structurally excludes (an explicit allow-list of claimable statuses), so a dead-lettered message can never be re-claimed.
 - **Inbox `Processing` status is now durably persisted (`Excalibur.Dispatch` inbox middleware + stores)** -- `InboxMiddleware` marked `Processing` in memory only, so the at-most-once concurrency guard and the stuck-processing timeout operated on state no second consumer could observe — dead code. The middleware now persists `InboxStatus.Processing` (via the new `IProcessingTrackingInboxStore` capability) before the handler runs, so a concurrent delivery of the same `(messageId, handlerType)` is durably skipped and the stuck-timeout can reclaim a crashed in-flight message.
 - **Elasticsearch inbox cleanup respects the cutoff (`Excalibur.Data.ElasticSearch`)** -- `ElasticsearchInboxStore.CleanupAsync(olderThan, …)` issued a `MatchAll` query and deleted **every** inbox document regardless of age. It is now date-bound: only documents strictly older than `olderThan` are deleted; documents at or newer than the cutoff are retained.
 - **Transactional outbox staging fails fast when its infrastructure is missing (`Excalibur.EventSourcing`)** -- selecting `OutboxStagingStrategy.Transactional` without a registered `ITransactionalOutboxWriter` + transactional event store silently degraded to non-atomic eventually-consistent staging (integration events lost on a crash between append and stage, no diagnostic). A `ValidateOnStart` guard now throws at startup naming exactly what is missing. Only the **explicit** `Transactional` value trips the guard; `Auto` (documented graceful fallback), `EventuallyConsistent`, and `Deferred` are unaffected.
 - **Projection hosts no longer silently skip poison events (`Excalibur.EventSourcing`)** -- `AsyncProjectionProcessingHost` and `ProjectionRebuildService` skipped an undeserializable or `null`-deserializing event and advanced past it (silent read-model drift), unlike the-fixed `GlobalStreamProjectionHost`. Per Amendment 4: the **continuous `AsyncProjectionProcessingHost` halts on a deserialize-poison event** (stops without advancing the checkpoint, so the position is re-attempted on the next read); the **one-shot `ProjectionRebuildService` fails the rebuild** (rethrows to a `Failed` state, partial state not persisted — no checkpoint, nothing reprocessed). An **apply** failure is **recorded-not-halted in the shared-checkpoint `AsyncProjectionProcessingHost`** (per-projection error + health + observability; read model rebuildable) because halting the shared checkpoint would force the succeeded projections to re-apply the event.
 - **`SqlServerRangeQueryEventStore` reads the correct column (`Excalibur.EventSourcing.SqlServer`)** -- `ReadRangeAsync` queried a non-existent `GlobalPosition` column and threw a missing-column SQL error at runtime on parallel catch-up (masked by in-memory-only tests). It now references the actual global-ordinal `Position` column and returns the events in `[fromPosition, toPosition]` ordered by the global ordinal, verified by a Docker SQL integration engage-test.
- **P0 data-loss & compliance sweep** -- Eliminated a class of "advertised-but-unwired" bugs where the framework silently shipped not-X (a durability/compliance guarantee was advertised but a seam was unwired). Each fix is structurally enforced — the silent-loss path is now inexpressible at the seam, not merely tested — with a non-vacuous independent regression lock (RED on the pre-fix code):
 - **Audit persistence (`Excalibur.Security`)** -- `AddSecurityAuditing` now **fails fast at registration** when `StoreType=SQL` (no SQL-backed audit store ships in the package) instead of wiring a placeholder that silently discarded every audit event.
 - **Audit-log encryption after key rotation (`Excalibur.Security`)** -- decryption honors each record's stored `KeyVersion` instead of a hardcoded `1`, so data encrypted under an older key stays readable after rotation; the envelope gains a 1-byte format-version discriminator (distinct from the 4-byte key version).
 - **Audit archival cutoff (`Excalibur.Data.ElasticSearch`)** -- `ArchiveAuditEventsAsync`/`DeleteArchivedEventsAsync` are date-bound to `cutoffDate` (was `MatchAllQuery`, which archived and deleted *every* audit event regardless of cutoff); deletes only the archived ids, with flush-before-delete ordering so a write failure deletes nothing.
 - **GDPR erasure coverage + verification (`Excalibur.Compliance`)** -- erasure uses a key-aware 3-state coverage gate (Covered/Exempt/Uncovered); a `Completed` certificate is unreachable while any data store is uncovered (was: only the event store was erased, yet the certificate could report success with outbox/inbox/projections/saga state untouched). Erasure verification now carries the deleted-key ids and is no longer vacuous.
 - **ElasticSearch field-encryption key rotation (`Excalibur.Data.ElasticSearch`)** -- rotation retains prior key versions and decrypts by the stamped version, so previously-encrypted data survives a rotation (was: permanently destroyed all prior ciphertext).
 - **Global-stream ordering + projection poison-halt (`Excalibur.EventSourcing`)** -- the global stream orders and pages by `GlobalPosition` (was per-aggregate `Version`, which skipped and duplicated projection events); the projection host halts on a poison (undeserializable/unappliable) event instead of advancing the checkpoint past it.
 - **Aggregate rehydration (`Excalibur.EventSourcing`)** -- `GetByIdAsync` throws on an undeserializable/null event instead of silently skipping it and returning a corrupt source-of-truth aggregate.
 - **CDC checkpoint (`Excalibur.Cdc.SqlServer`)** -- the checkpoint no longer advances past a swallowed failed change when a later change to the same table succeeds.
 - **Saga optimistic concurrency (`Excalibur.Saga.SqlServer`)** -- the SQL saga store now *enforces* optimistic concurrency (store-owns-increment CAS that throws `ConcurrencyException` on a stale write) instead of incrementing a version it never checked.
 - **Outbox job lifetime (`Excalibur.Jobs`)** -- `OutboxJob` no longer disposes its injected singleton `IOutboxDispatcher` on every Quartz fire (which caused use-after-dispose after the first cron run).

### Added

- **`ITransactionalEventStore.AppendWithOutboxStagingAsync` — the transactional event+outbox keystone is now public and real (`Excalibur.EventSourcing.Abstractions` + `Excalibur.EventSourcing.SqlServer`)** -- `ITransactionalEventStore` (an optional extension of `IEventStore`, namespace `Excalibur.EventSourcing`) is promoted from internal scaffolding to a **public** marker interface, and `SqlServerEventStore` now implements it. Its single method — `AppendWithOutboxStagingAsync(aggregateId, aggregateType, events, expectedVersion, stageOutbox, ct)` — is a **store-owned atomic unit of work**: the store opens and owns one connection and one `IDbTransaction`, performs the optimistic-concurrency version check, appends the events, invokes the caller's `stageOutbox` callback on that **same** transaction (only when the version check succeeds), then commits — rolling the entire transaction back on a concurrency conflict or any throw from `stageOutbox`, so neither the events nor the outbox rows persist. Because the transaction never escapes the store, appending events and staging outbox rows on two different transactions is structurally impossible. This completes the `OutboxStagingStrategy.Transactional` path that made fail-fast: with a transactional event store (SQL Server) and an `ITransactionalOutboxWriter` registered, `EventSourcedRepository`'s default `Auto` strategy resolves to `Transactional` and integration events can no longer be lost between the event append and the outbox stage. NoSQL event stores that do not implement the interface continue to fall back to eventually-consistent/deferred staging. See [Outbox Pattern → Event Sourcing Outbox Integration](docs-site/docs/patterns/outbox.md).
- **`IAuditSigningKeyProvider`** -- New capability interface (2 methods, ISP-clean, fail-closed `byte[]?` return, required `CancellationToken`, `ValueTask`) in `Excalibur.Data.ElasticSearch` (namespace `Excalibur.Data.ElasticSearch.Security.Auditing`), with a default `OptionsAuditSigningKeyProvider` reading `AuditOptions`. Supplies the HMAC key for keyed audit-log integrity; the key is held outside the index and rotated via `keyId`.
- **`Grant.IsActive(DateTimeOffset asOf)` / `Grant.IsExpired(DateTimeOffset asOf)` + `IGrantStore.GetAllGrantsAsync(userId, includeExpired, ct)`** -- New pure, clock-free grant predicates (`Excalibur.A3.Abstractions`, namespace `Excalibur.A3.Authorization.Grants`) and a new default-secure store overload (namespace `Excalibur.A3.Authorization.Stores`) that filters expired grants by default with an explicit `includeExpired` opt-in (Microsoft soft-delete-filter idiom).
- **`SigningKeyProviderStartupValidator`** -- New `IHostedService` startup guard (`Excalibur.Security`) registered by `AddMessageSigning` that fails loud (`InvalidOperationException` naming the missing `IKeyProvider`) at host start when message signing is enabled but no key provider is registered, instead of a deferred first-dispatch DI failure.
- **Circuit-breaker state-transition + rejection metrics (`Excalibur.Dispatch`)** -- `CircuitBreakerMiddleware` now emits two counters on a new meter `Excalibur.Dispatch.CircuitBreakerMiddleware`, directly from the always-on middleware (no opt-in observability service required), so breaker trips are alertable out of the box: `dispatch.circuit_breaker.transitions` (emitted only on an actual state change, tagged `circuit.key`/`from_state`/`to_state` with values `closed`/`open`/`half_open`) and `dispatch.circuit_breaker.rejections` (tagged `circuit.key`). Additive to the existing logs/Activity tags, and distinct from the opt-in `CircuitBreakerMetrics` instrumentation (meter `Excalibur.Dispatch.CircuitBreaker`). See [Metrics Reference → Circuit Breaker](docs-site/docs/observability/metrics-reference.md).
- **Leader-election split-brain timing validator (`Excalibur.Dispatch.LeaderElection.Abstractions` + `Excalibur.LeaderElection`)** -- `LeaderElectionOptionsValidator` now additionally enforces the cross-property rule `RenewInterval + GracePeriod + 1s clock-skew margin < LeaseDuration` (beyond the prior individual `RenewInterval < LeaseDuration` / `GracePeriod < LeaseDuration` bounds). A configuration whose effective self-demotion deadline could fall at or after lease expiry — a guaranteed split-brain overlap window — now **fails fast** via `ValidateOnStart` instead of passing validation. The validator is wired into both DI cores (generic `AddExcaliburLeaderElection` and the builder core) via `TryAddEnumerable`, so every distributed provider inherits it. The shipped defaults (Lease 15s / Renew 5s / Grace 5s) satisfy the rule. See [Leader Election → Lease Timing Invariant](docs-site/docs/leader-election/index.md).
- **`IInboxStoreAdmin.MarkFailedAsync(messageId, handlerType, errorMessage, retryCount, ct)`** -- New overload on the inbox admin interface (`Excalibur.Dispatch.Abstractions`, namespace `Excalibur.Dispatch`) that sets an entry's retry count **exactly** (no auto-increment), symmetric with `IOutboxStore.MarkFailedAsync(string, string, int, CancellationToken)`. Used by the retry processor for the transient short-circuit case — an open circuit breaker must leave a message re-admittable for retry *without* consuming an attempt — distinct from the core `IInboxStore.MarkFailedAsync(...)` which increments. Implemented across all 9 inbox stores.
- **`AuditPersistenceException`** -- New sealed exception (`: ApiException`) in `Excalibur.Compliance.Abstractions` (namespace `Excalibur.Compliance`), thrown by `IAuditLogger.LogAsync` when the audit store fails to durably persist an event. Carries an optional `EventId` of the unsaved event. Audit logging is now **fail-closed**: a store failure surfaces as this exception rather than being masked behind a success-shaped `AuditEventId`.
- **`IClaimableInboxStore`** -- New optional capability interface (2 methods: `TryClaimAsync(messageId, handlerType, ct)` returning first-writer-wins `bool`, and `ReleaseAsync(messageId, handlerType, ct)`) in `Excalibur.Dispatch.Abstractions`. A segregated capability (composition, not `IInboxStore` inheritance) implementing the atomic claim-before-execute idempotency protocol: claim atomically, finalize via `MarkProcessedAsync` on success, release on failure so a redelivery re-admits. All shipped inbox stores implement it; a `ValidateOnStart` guard fails fast if a registered store omits it.
- **`IClaimableDeduplicator`** -- New optional capability interface (2 methods: `TryClaimAsync(messageId, expiry, ct)`, `ReleaseAsync(messageId, ct)`) in `Excalibur.Dispatch.Abstractions`. The in-memory analogue of `IClaimableInboxStore`; the successful claim doubles as the dedup marker. Implemented by the built-in `InMemoryDeduplicator`.
- **`OutboxStatus.DeadLettered`** -- New terminal enum member (ordinal 5, append-only) in `Excalibur.Dispatch.Abstractions` marking an outbox message that has permanently failed after exhausting its retry policy and been routed to the dead-letter queue. Outbox store claim predicates exclude it structurally, so it is never re-claimed.
- **`IDeadLetterableOutboxStore`** -- New optional capability interface (1 method: `MarkDeadLetteredAsync(messageId, reason, ct)`) in `Excalibur.Dispatch.Abstractions`. A segregated capability (composition, not `IOutboxStore` inheritance — keeps `IOutboxStore` within the ISP threshold) that durably transitions a retry-exhausted message to the terminal `OutboxStatus.DeadLettered`. All shipped Excalibur outbox stores implement it; a `ValidateOnStart` guard fails fast if a custom polling store omits it.
- **`IProcessingTrackingInboxStore`** -- New optional capability interface (1 method: `MarkProcessingAsync(messageId, handlerType, ct)`) in `Excalibur.Dispatch.Abstractions`. A segregated capability (composition, not `IInboxStore` inheritance) that durably persists the in-flight `InboxStatus.Processing` status before a handler runs, making the inbox's at-most-once concurrency guard and stuck-processing timeout functional.
- **ProjectionContext** -- New read-only record (`IsReplay`, `GlobalPosition`) passed to `When<TEvent>(Action<TProjection, TEvent, ProjectionContext>)` overload on `IProjectionBuilder<T>`. Enables projection handlers to distinguish live events from replay and access global stream position for idempotency. Factory methods: `ProjectionContext.Live` (singleton), `ProjectionContext.Replay(globalPosition)`.
- **WithSearchText** -- New `IProjectionBuilder<T>.WithSearchText(Func<TProjection, string>, Action<TProjection, string>)` method for automatic computed search field generation. AOT-safe dual-delegate approach — no reflection. Computed once per projection upsert. Zero overhead when not configured.
- **IVersionedProjectionStore\<T\>** -- ISP sub-interface of `IProjectionStore<T>` for optimistic concurrency via version tracking. Two methods: `GetVersionedAsync` (returns `VersionedProjection<T>?`) and `UpsertVersionedAsync` (throws `ConcurrencyException` on version mismatch). Numeric `long` version starting at 1, `null` expectedVersion for inserts.
- **VersionedProjection\<T\>** -- Sealed wrapper class containing `Projection` and `Version` properties for concurrency-aware projection reads.
- **CdcTableConfig** -- New bindable POCO in `Excalibur.Cdc` (`[Required] TableName`, optional `CaptureInstance`). `CdcTableTrackingOptions` now derives from it (1-level inheritance), keeping behavioral members (`EventMappings`/`Filter`/mapper delegates) on the derived type so `IConfiguration` only binds the slim POCO. A shared `CdcCaptureInstanceDeriver` derives `CaptureInstances[]` + `CaptureInstanceToTableNameMap` from `Tables` for both the builder and config-driven (`CdcJob`) paths.
- **Bidirectional cursor pagination** -- `CursorPagedResult<T>` gains `PreviousCursor` + `HasPrevious`; `ElasticSearchCursorHelper.ApplyCursorPaging<T>` over-fetches one "peek" row (`Size = pageSize + 1`) so `HasMore`/`HasPrevious` are correct even when a boundary page contains exactly `pageSize` items. `ToCursorResult` now emits both forward and backward cursors so consumers can offer First/Previous/Next/Last. See.
- **`IDispatchAmbientScopeAccessor`** -- New abstraction (1 read-only member, `CurrentServiceProvider`) in `Excalibur.Dispatch.Abstractions` that lets a host surface the ambient DI scope so the singleton dispatcher resolves scoped handlers from it. `Excalibur.Dispatch.Hosting.AspNetCore` provides the implementation over `IHttpContextAccessor`, registered automatically by `WebApplicationBuilder.AddDispatch` and exposed for other composition roots via `services.AddDispatchAmbientScope()`. See.
- **`Excalibur.Dispatch.AspNetCore` metapackage** -- New experience metapackage bundling `Excalibur.Dispatch` + `Excalibur.Dispatch.Hosting.AspNetCore` + `Excalibur.Dispatch.Observability`. A single `services.AddDispatchAspNetCore(...)` wires the dispatcher, OpenTelemetry instrumentation, and request-scope-aware handler resolution (`AddDispatchAmbientScope`) for the common web scenario.

### Changed

- **Duplicate dead `IMessageChannelAdapter<TMessage>` removed (`Excalibur.Dispatch`)** -- two distinct **public** `IMessageChannelAdapter<TMessage>` interfaces shipped under the same simple name; the `Excalibur.Dispatch.Channels` variant was dead (no implementers, no registrations). It is removed (6 `PublicAPI.Shipped.txt` entries), leaving the `Excalibur.Dispatch.Abstractions` `IMessageChannelAdapter` as the single canonical interface, and an architecture boundary test locks the dead variant out of returning. **Breaking change** (greenfield) — though the removed variant had no implementations to break.
- **Architecture boundary tests are now enforced in CI (`tests/architecture/Boundary.Tests`)** -- the Dispatch-vs-Excalibur separation and banned-dependency boundary tests were report-only because `ARCH_ENFORCE` was never set in CI, so a boundary violation never failed the build. CI now runs them under `ARCH_ENFORCE=true` (89/89 green), and several stale serialization/Dapper policy bans that had drifted from the ratified architecture were corrected in the same lane. Contributor-facing (build-gate) change; no runtime behavior change.
- **`IGrantStore.GetAllGrantsAsync` is now default-secure (`Excalibur.A3.Abstractions` + all 7 grant stores)** -- the 2-arg overload now returns **active-only** grants (expired grants excluded from the default read/decision path); pass the new 3-arg `includeExpired: true` overload to retrieve expired grants (e.g. for governance/audit reporting). **Behavior change** — callers that relied on the prior overload returning expired grants must switch to the `includeExpired` overload.
- **Audit-log integrity switched from unkeyed SHA-256 to keyed HMAC-SHA256 (`Excalibur.Data.ElasticSearch`)** -- integrity tags are now `v1:{keyId}:{base64(HMAC)}` and require an `IAuditSigningKeyProvider`; records stamped by a prior build (bare SHA-256) will not verify under the keyed scheme. Verification **fails closed** when the key is unavailable. **Behavior change.**
- **`AddMessageSigning` fails fast at host start without a key provider (`Excalibur.Security`)** -- enabling message signing without a registered `IKeyProvider` now throws `InvalidOperationException` at startup (via `SigningKeyProviderStartupValidator`) instead of a deferred DI resolution failure on first dispatch; the dead `SecureKeyProvider` (which would have silently fabricated a key) is removed. **Behavior change.**
- **AOT trim/`Requires*` attributes removed from `IEventSerializer` and `IEventSourcedRepository<,>` interfaces (`Excalibur.Dispatch.Abstractions` + `Excalibur.EventSourcing.Abstractions`)** -- the consumer-facing serializer/repository interfaces no longer carry `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`; the unavoidable reflection is ctor-gated inside the concrete reflection serializers (AOT-safe impls stay clean), so consumers calling through the interfaces no longer inherit IL2026/IL3050 warnings. AOT-safety improvement, no runtime behavior change.
- **Avro serializer documentation corrected — no schema evolution (`Excalibur.Dispatch.Serialization.Avro`)** -- the package README, the NuGet package description, and the serialization-providers guide advertised "excellent schema evolution support", but the serializer decodes each payload using the reader type's own schema as **both** the reader and writer schema (no Avro writer-schema resolution) — a writer/reader schema skew raises a deserialization error rather than mis-decoding. The docs now state the reader==writer-schema requirement and point to the Kafka transport's Confluent Schema Registry integration (`UseConfluentSchemaRegistry()`) for registry-based workflows. No code behavior change; true writer-schema-resolution (schema evolution) is tracked as a separate enhancement.
- **`ICacheKeyBuilder.CreateKey` now returns `string?` (`Excalibur.Dispatch.Caching`)** -- the return type changed from `string` to nullable `string?`. A `null` result is the documented "do not cache" signal: the caching middleware bypasses the cache and invokes the handler for that request. Implementations **must be infallible** — return `null` rather than throwing when a key cannot be derived. `DefaultCacheKeyBuilder` follows this contract. **Breaking change** for custom `ICacheKeyBuilder` implementations — update the signature to `string? CreateKey(IDispatchAction, IMessageContext)` and return `null` instead of throwing on a "cannot derive a key" condition.
- **Abstractions namespace alignment (Microsoft convention)** -- All 6 non-compliant Abstractions packages now drop `.Abstractions` from CLR namespaces: `Excalibur.Security.Abstractions` → `Excalibur.Security`, `Excalibur.Jobs.Abstractions` → `Excalibur.Jobs`, `Excalibur.A3.Abstractions` → `Excalibur.A3`, `Excalibur.EventSourcing.Abstractions` → `Excalibur.EventSourcing`, `Excalibur.Data.Abstractions` → `Excalibur.Data`, `Excalibur.Dispatch.Abstractions` → `Excalibur.Dispatch`. Assembly/package names unchanged. All 11 Abstractions packages now follow the Microsoft convention. **Breaking change** — consumers must update `using` directives.
- **Duplicate types removed** -- `RouteInfo`, `HealthCheckResult`, `CausationId`, `MessageVersionMetadata` removed from `Excalibur.Dispatch` (kept canonical versions from Abstractions package, now in shared `Excalibur.Dispatch` namespace).
- **IDE0005 dotnet-format mitigation** -- `.editorconfig` suppresses IDE0005 (remove unnecessary usings) to prevent known Roslyn bug that deletes `using var` disposal statements.
- **ES/OpenSearch SDK type leakage removed** -- `IndexConfiguration`, `AliasDefinition`, `AliasOperation` (ElasticSearch + OpenSearch), `IndexTemplateConfiguration`, `ComponentTemplateConfiguration` (OpenSearch) no longer expose SDK types (`IndexSettings`, `TypeMapping`, `Alias`, `QueryContainer`, `AliasAddAction`) on public boundaries. All replaced with `JsonElement?` per. Internal managers deserialize at the implementation boundary. **Breaking change** for consumers directly referencing SDK-typed properties on these models.
- **GCP PubSub SDK fakes replaced with seams** -- `PubSubDeadLetterQueueManager` and `PubSubTransportReceiver` now use `ISubscriberApiClientSeam` internally instead of concrete `SubscriberServiceApiClient`. Public constructors unchanged. Internal seam enables testability without concrete SDK mocking.
- **Microsoft.CodeAnalysis 4.14→5.3** -- Central pin bumped from 4.14.0 to 5.3.0 (Common, CSharp, Workspaces, Analyzers). Source generators remain hard-pinned at 4.14.0 for consumer SDK compatibility (VS 17.14/SDK 9.0.300). Benchmark VersionOverride workaround removed.
- **Roslyn family pin completed** -- Added the missing central pin for the `Microsoft.CodeAnalysis.Scripting` meta package (5.3.0). The earlier 5.3.0 pin set pinned `Scripting.Common` but not the meta `Scripting` package, which `WolverineFx`→`JasperFx.RuntimeCompiler` pulls at 5.0.0 with an exact `Microsoft.CodeAnalysis.CSharp.Scripting [5.0.0]` dependency — colliding with the `>= 5.3.0` pin and breaking `Excalibur.Dispatch.Benchmarks` restore with NU1102. The benchmarks lock file was regenerated.
- **xUnit 2.9→3.x** -- Test infrastructure migrated from xunit 2.9.3 to xunit.v3 3.2.2. IAsyncLifetime Task→ValueTask, Verify.Xunit→Verify.XunitV3, Xunit.SkippableFact replaced with v3 native Assert.SkipUnless/SkipWhen. Zero shipping code changes. Templates updated for v3.
- **Saga: Model B (orchestration) deleted** -- Per, all Model B orchestration infrastructure removed. Only Model A (event-driven choreography) remains. `WithOrchestration()` renamed to `WithCoordination()`. **Breaking change** for consumers using `ISagaOrchestrator`, `ISagaStateStore`, `ISagaDefinition`, `ISagaStep`, `AddExcaliburAdvancedSagas()`, or any Model B types.
- **ISagaTimeout\<TMessage\>** -- New declarative timeout interface (1 method: `HandleTimeoutAsync`). Sagas implement `ISagaTimeout<T>` per timeout type. Coordinator dispatches timeouts before `HandleAsync` with bounded reflection cache.
- **Saga API surface reduction** -- `ISagaReminder`, `ISagaOutboxMediator`, `ISagaStateMigrator<TFrom,TTo>` internalized (7 PublicAPI.Shipped entries removed). These are framework implementation details; consumers use `ISagaBuilder` extensions.
- **Saga static state eliminated** -- Static `ConcurrentBag` pending registrations replaced with instance-scoped `SagaPendingRegistrations` to prevent test contamination.
- **excalibur-saga template rewritten** -- Template now uses Model A types (`SagaBase<T>`, `ISagaTimeout<T>`) instead of deleted Model B types.
- **InMemorySagaStore registered as default** -- `AddExcaliburSaga()` now registers `InMemorySagaStore` via `TryAddSingleton`, providing zero-config prototyping. Persistent stores override via `TryAdd` precedence.
- **DispatchHealthCheckOptions.IncludeSaga removed** -- Dead property referencing deleted `ISagaMonitoringService`. Health check string constant cleaned up.
- **CDC table config unified** -- `DatabaseOptions.CaptureInstances` (`string[]`) replaced with `Tables` (`Collection<CdcTableConfig>`). The config-driven Quartz `CdcJob` path (`Jobs:CdcJob:DatabaseConfigs[].Tables`) and the builder/background path now share a single table model, fixing a silent handler mismatch where the config path could not map a capture instance to its logical table name. `CdcJob` logs a fail-fast warning (`JobsEventId.CdcJobNoTablesConfigured = 147204`) when a database has no tables configured. Dead `CdcDefaultCaptureInstances` removed. **Breaking change** — consumers binding `CaptureInstances` must migrate to `Tables` (each entry needs an explicit `TableName`; `CaptureInstance` optional). See `docs/patterns/cdc.md` Option 2b.
- **`ElasticSearchCursorHelper.ToCursorResult` signature** -- third parameter changed from `bool reverseItems` to `Excalibur.EventSourcing.PageNavigation navigation`; the helper derives reverse internally and assigns forward/backward cursors from the displayed boundary items. **Breaking change** — callers pass the navigation direction and must size queries via `ApplyCursorPaging` (or `Size(pageSize + 1)`) instead of `Size(pageSize)`.
- **Third-party notices: deterministic + redistribution-only** -- `eng/ci/notices-generate.ps1` now excludes `PrivateAssets="all"` build-time-only references (analyzers, source generators, `MinVer`, `System.Collections.Immutable`) — they are not redistributed to consumers, so they no longer appear in `THIRD-PARTY-NOTICES.md`. Multi-version conflicts now resolve to the central `Directory.Packages.props` pin and output rows are sorted ordinally, fixing OS-dependent drift between CI (Linux) and local (Windows) — previously `Microsoft.CodeAnalysis.Common` flipped between its central pin (5.3.0) and the source generators' private 4.14.0 override.
- **Dependency bumps** -- `codecov/codecov-action` v6 → v7 (transport-conformance CI workflow); `Microsoft.NET.ILLink.Tasks` 10.0.8 → 10.0.9 (benchmarks lockfile).

### Fixed

- **Scoped handlers resolved from the root container (captive dependency)** -- Dispatching a handler registered `Scoped` (or a handler with a scoped dependency) through the context-less / ultra-local fast paths failed with `Cannot resolve scoped service '…' from root provider` (surfacing as a 500 / failed `IMessageResult` titled "Direct local dispatch failed"). The singleton `LocalMessageBus` now resolves such handlers from a DI scope — the ambient request scope when an `IDispatchAmbientScopeAccessor` supplies one (shared request-scoped state), otherwise a fresh `IServiceScopeFactory` scope disposed after the handler completes. The scope verdict is deterministic (registered lifetime + constructor inspection) and cached, so the root-resolvable hot path (transient/singleton handlers) is unchanged. Scoped handlers decline the no-context ultra-local fast paths and route exclusively through the context-aware dispatch path, so a context-bound dispatch shares the caller's request scope (`IMessageContext.RequestServices`) rather than resolving from a fresh scope. AOT-safe. See.

### Security

- **Transitive CVE remediation** -- `MessagePack` 3.1.4 → 3.1.7 (CVE-2026-48109: LZ4 decompression denial-of-service). `SQLitePCLRaw` 2.1.11 → bundle 3.0.3 / `lib.e_sqlite3` 3.50.3 (CVE-2025-6965: bundled SQLite < 3.50.2), pulled transitively by `Microsoft.Data.Sqlite`; remediated via Central Package Management transitive pinning. Verified clean via `dotnet restore eng/ci/shards/ShippingOnly.slnf` and `dotnet list package --vulnerable`.

### Added

- **SagaTimeoutOptionsValidator** -- `IValidateOptions<SagaTimeoutOptions>` enforcing PollInterval ≥ 100ms, BatchSize ≥ 1, ShutdownTimeout > 0. Registered with ValidateOnStart.
- **SagaReminderOptionsValidator** -- `IValidateOptions<SagaReminderOptions>` enforcing delay ranges and cross-property constraints (MinimumDelay < MaximumDelay, DefaultDelay in range). Registered with ValidateOnStart.

### Fixed

- **Elasticsearch cursor pagination off-by-one (phantom next page)** -- `ElasticSearchCursorHelper.ToCursorResult` set `NextCursor` whenever `hits.Count >= pageSize`, so a final page of exactly `pageSize` items reported `HasMore = true` and the next `search_after` fetch returned an empty page (a blank "next" page in the UI). Boundary detection is now driven by the over-fetch peek row, so `HasMore` is `false` on an exactly-full last page. The cursor logic was extracted into a pure, store-agnostic `ResolveCursorBoundaries` core with 8 unit tests (previously untestable because `SearchResponse<T>` cannot be constructed in-process).
- **Jobs: `Disabled` flag ignored by CdcJob and DataProcessingJob** -- `CdcJob.ConfigureJob` and `DataProcessingJob.ConfigureJob` registered their job + trigger unconditionally, so `Jobs:*:Disabled: true` was silently ignored — only `OutboxJob` honored it. Both now apply the same schedule-time gate, so `Disabled: true` uniformly means the job's trigger is never registered with the scheduler. Added disabled→not-registered / enabled→registered coverage to `CdcJobShould`, `DataProcessingJobShould`, and `OutboxJobShould` (the reference impl was previously untested for the gate). **Caveat:** under a persistent Quartz job store the schedule-time gate does not remove an already-persisted job — use the runtime watcher (`AddJobWatcher<TJob, TOptions>`, which pauses via the scheduler) or delete the job. See the Jobs guide (`docs-site/docs/patterns/jobs.md`).
- **Outbox dispatcher/processor only available with A3** -- `IOutboxDispatcher` (`MessageOutbox`) and `IOutboxProcessor` (`OutboxProcessor`) were never registered by the outbox subsystem. The only `IOutboxDispatcher` registration in the framework was A3/Audit's fail-fast `DefaultOutboxDispatcher` stub, so `OutboxJob`, `OutboxBackgroundService`, and audited dispatch could not resolve a real dispatcher unless A3 audit was added. (The registrations were dropped when the implementations moved from `Excalibur.Dispatch` to `Excalibur.Outbox` and never restored.) `AddExcaliburOutbox`/`AddOutbox` now registers both: `OutboxProcessor` as **Transient** (per-instance `Init(dispatcherId)` state — each background partition and dispatcher needs its own) and `MessageOutbox` as **Singleton**. The outbox registration removes A3's fail-fast `DefaultOutboxDispatcher` stub (identified by type) before `TryAdd`ing `MessageOutbox`, so the real dispatcher wins regardless of whether audit or the outbox is composed first, while a consumer-supplied `IOutboxDispatcher` still takes precedence. Added registration + composition-order regression tests.
- **Jobs documentation: built-in job registrations** -- `docs-site` jobs guide now shows the service registration each built-in job requires (`AddSqlServerCdcJob` for `CdcJob`, `AddOutbox(...)` for `OutboxJob`, `AddDataProcessing(...)` for `DataProcessingJob`) instead of only `ConfigureJob` (which merely schedules). Also corrected the examples to pass the root `IConfiguration` to `ConfigureJob`, the two-argument `ConfigureHealthChecks(IHealthChecksBuilder, IConfiguration)` signature, the `OutboxJobOptions` type name, and the `Jobs:OutboxJob` section name.
- **JobWorkerSample no longer schedules unwired jobs** -- The deployment job-host sample scheduled `CdcJob`/`OutboxJob`/`DataProcessingJob` without registering their dependencies (they would fail to activate at trigger time). Since the sample is intentionally database-free (in-memory Quartz store + coordination focus), those jobs are no longer scheduled; the exact registration each requires is documented inline, pointing to `CdcJobQuartz` for a complete runnable CdcJob worker.
- **Elasticsearch/OpenSearch index names lowercased across all composition sites** -- Beyond the projection store, several places composed `{consumerPrefix}-…` index names without lowercasing and would hit the same `invalid_index_name_exception` with an uppercase prefix (e.g. an environment segment): `EventualConsistencyTracker`, `ProjectionRebuildManager`, `SchemaEvolutionHandler`, the Elasticsearch/OpenSearch dead-letter handlers, and the Elasticsearch/OpenSearch audit exporters/sinks. Introduced a shared internal `IndexNameNormalizer` in the Data.ElasticSearch/Data.OpenSearch packages and lowercased the audit-sink prefixes. (Outbox/Inbox use the consumer's raw `IndexName` directly, not a composed name, so they are unchanged.)
- **Elasticsearch/OpenSearch projection index names are fully lowercased** -- The index-name convention lowercased only the projection type name, not the consumer-supplied `IndexPrefix`/`IndexName`. An uppercase segment (e.g. an environment-derived `Development`) produced names like `co-transactions-transaction-Development`, which Elasticsearch/OpenSearch reject with a 400 `invalid_index_name_exception` ("must be lowercase") — surfacing as an inline-projection failure during `SaveAsync`. `ElasticSearchProjectionIndexConvention.GetIndexName` and `OpenSearchProjectionStore.GetIndexName` now lowercase the entire composed name (prefix included). No consumer change required.
- **Inline/async projections resolve scoped stores from a scope, not the root provider** -- `InlineProjectionProcessor` (singleton, invoked from `SaveAsync` via `EventNotificationBroker`) and `AsyncProjectionProcessingHost` (singleton `BackgroundService`) passed their captured **root** `IServiceProvider` to each projection's apply delegate, which resolves the **scoped** `IProjectionStore<T>`. Under DI scope validation (the default in the Development host, and the path Quartz jobs exercise) this threw `AggregateException` → *"Cannot resolve scoped service 'IProjectionStore`1[…]' from root provider."* Both now resolve each projection in a freshly created `IServiceScopeFactory` scope (also isolating scoped state across concurrently-applied projections). Notification handlers in `EventNotificationBroker` are likewise resolved from a scope. Latent since inline projections shipped; surfaces whenever projection stores are scoped (SQL Server/Mongo/Elasticsearch/etc.) and scope validation is enabled. Added a scoped-store-under-validation regression test.
- **SQL Server CDC builder registers the CdcJob factory** -- `AddCdcProcessor(cdc => cdc.UseSqlServer(...))` now also registers `IDataChangeEventProcessorFactory`, so configuring SQL Server CDC makes `CdcJob` resolvable without a separate call. The focused `AddSqlServerCdcJob(IConfiguration)` entry point remains for job-only workers that don't set up the full CDC processing builder.
- **CdcJob processor factory never registered** -- `CdcJob` depends on `IDataChangeEventProcessorFactory`, but no DI extension registered it — Quartz activation failed with *"Unable to resolve service for type 'IDataChangeEventProcessorFactory'"*. Added a single feature-registration entry point `services.AddSqlServerCdcJob(IConfiguration)` (in `Excalibur.Jobs.Cdc`, namespace `Microsoft.Extensions.DependencyInjection`) that binds `CdcJobOptions` from `Jobs:CdcJob` and `TryAdd`s the processor factory plus its SQL Server data-access policy factory. Updated the `CdcJobQuartz` sample (its `AddCdcProcessor()` call never registered the factory despite the comment) and added an `ActivatorUtilities` regression test.
- **CdcJob Quartz activation crash (ambiguous constructor)** -- `CdcJob` declares two public 5-parameter constructors (a `Func<string, SqlConnection>` variant and an `IConfiguration` variant). Quartz's `MicrosoftDependencyInjectionJobFactory` activates jobs via `ActivatorUtilities`, which throws *"Multiple constructors accepting all given argument types"* when both are DI-satisfiable. Marked the `IConfiguration` constructor with `[ActivatorUtilitiesConstructor]` so container activation deterministically selects it (`IConfiguration` is always host-registered, so activation needs no `Func<string, SqlConnection>` registration). Added `ActivatorUtilities` regression tests.
- **NU1608 Roslyn version conflict (benchmarks build)** -- WolverineFx → JasperFx.RuntimeCompiler transitively pulled the `Microsoft.CodeAnalysis` meta-package plus the CSharp.Scripting/Scripting.Common/VisualBasic/VisualBasic.Workspaces satellites at 5.0.0, whose exact-match (`= 5.0.0`) dependencies conflicted with the 5.3.0 Common/CSharp/Workspaces pins. Under `-warnaserror` the NU1608 warnings were fatal. Pinned the whole Roslyn family to 5.3.0 in `Directory.Packages.props` so it stays in lockstep.
- **SecurityEventLogger dispose race** -- `Dispose()` no longer races with `StopAsync()`. Added `volatile _disposed` guard, `IAsyncDisposable` implementation, and cancel-before-dispose sequencing to prevent `ObjectDisposedException` during hosted service shutdown.
- **AddExcaliburAdvancedSagas DI trap** -- Method registered middleware requiring unregistered services. Fixed by deleting Model B entirely.
- **SagaOrchestration sample** -- Rewritten from procedural steps to event-driven choreography using framework types (`SagaBase<T>`, `ISagaTimeout<T>`).
- **ProjectionContext.Replay guard** -- `ProjectionContext.Replay(globalPosition)` now throws `ArgumentOutOfRangeException` for negative values, preventing invalid replay state.
- **ExistsAsync extension method** -- `IProjectionStore<T>.ExistsAsync(id, ct)` checks projection existence without full deserialization. Providers implement `IExistsProjectionStore<T>` escape hatch for optimized paths (e.g., SQL `SELECT TOP 1 1`, CosmosDB `HEAD`); fallback uses `GetByIdAsync` + null check.
- **DistinctValuesAsync extension method** -- `IProjectionStore<T>.DistinctValuesAsync(propertyName, filters, ct)` returns distinct property values for filter dropdown faceting. Providers implement `IDistinctValuesProjectionStore<T>` for native queries (e.g., SQL `DISTINCT`, MongoDB `distinct()`); fallback uses reflection.
- **AddProjection&lt;TProjection, TConfig&gt;()** -- Explicit generic registration for `IProjectionConfiguration<T>` implementations. AOT-safe alternative to `AddProjectionsFromAssembly()` assembly scanning.
- **SqlServer MaterializedViewStore** -- `UseMaterializedViewStore()` builder extension on `ISqlServerEventSourcingBuilder` registers `IMaterializedViewStore` backed by SQL Server. Features `EnsureSchemaAsync()` for idempotent DDL creation, `UPDLOCK,ROWLOCK` position tracking, and configurable table names. Default tables: `MaterializedViews` + `MaterializedViewPositions`.
- **Provider-specific QueryPagedAsync/QueryCursorAsync** -- Single-roundtrip pagination overrides via ISP sub-interfaces: `IPageableProjectionStore<T>` (SqlServer `COUNT(*) OVER()`, CosmosDB/MongoDB parallel count) and `ICursorProjectionStore<T>` (DynamoDB `ExclusiveStartKey`/`LastEvaluatedKey` with opaque cursor tokens). Eliminates N+1 roundtrips for paged/cursor queries.
- **AddElasticSearchProjectionStore&lt;T&gt;()** -- Builder chain extension on `IEventSourcingBuilder` for single-projection ES store registration. Two overloads: options-based and URI-based. Bridges to existing `IServiceCollection` extensions.
- **IIndexMappingConvention** -- Pluggable ES index mapping conventions. Single-method ISP interface (`ConfigureMappings`) with `DefaultIndexMappingConvention` singleton pass-through. Configurable via `ElasticSearchProjectionStoreOptions.IndexMappingConvention`.
- **AOT-safe serialization options** -- Consumer-provided `JsonSerializerOptions` property on CosmosDB and DynamoDB projection store options for AOT-safe serialization via source-gen `JsonSerializerContext`. Consolidated 15+ scattered IL2026/IL3050 suppressions to file-level pragmas. Added `[DynamicallyAccessedMembers]` on TProjection for CosmosDB, DynamoDB, and ElasticSearch stores.

- **Flat projection storage across all document-store backends** -- Removed the `data: {... }` envelope wrapper from MongoDB, CosmosDB, and DynamoDB projection stores. Projection fields now live at the document root alongside lightweight `_projection` metadata (id, type, updatedAt). ElasticSearch was flattened previously; all four backends now share the same flat storage pattern. Consumer query repositories using `ElasticRepositoryBase<T>` should remove `data.` field path prefixes. **Breaking change** for consumers with custom queries against the old `data.*` field paths or deserializing the envelope `MongoDbProjectionDocument`/`CosmosDbProjectionDocument` types.
- **MongoDbRepositoryBase\<T\>** -- Base class for custom MongoDB query repositories sharing projection collections, matching the existing `ElasticRepositoryBase<T>` pattern. Includes `IMongoDbRepositoryBase<T>` (CRUD) and `IMongoDbRepositoryBaseQuery<T>` (query) ISP interfaces, plus `MongoDbProjectionCollectionConvention` for consistent collection naming.
- **CosmosDbRepositoryBase\<T\>** -- Base class for custom CosmosDB query repositories sharing projection containers. Includes `ICosmosDbRepositoryBase<T>` (CRUD) and `ICosmosDbRepositoryBaseQuery<T>` (SQL query) ISP interfaces, plus `CosmosDbProjectionContainerConvention` for consistent container naming.
- **DynamoDbRepositoryBase\<T\>** -- Base class for custom DynamoDB query repositories sharing projection tables. Includes `IDynamoDbRepositoryBase<T>` (CRUD) and `IDynamoDbRepositoryBaseQuery<T>` (scan) ISP interfaces, plus `DynamoDbProjectionTableConvention` for consistent table naming.

### Fixed

- **MongoDB regex injection in projection queries** -- `BuildContainsFilter` now uses `Regex.Escape()` before constructing `BsonRegularExpression`, preventing regex metacharacters in filter values from being interpreted as patterns.
- **CosmosDB double-parse in GetByIdAsync** -- Changed from `ReadItemAsync<JsonElement>` + `GetRawText()` + `JsonNode.Parse()` to `ReadItemStreamAsync` + `JsonNode.ParseAsync(stream)` for single-parse deserialization.
- **DynamoDB partition key collision** -- `DynamoDbProjectionStore` now preserves the original partition key value in `_projection.origPk` metadata when a projection property name collides with the configurable partition key name. Restored transparently on read.
- **AOT suppression audit false positives** -- `Invoke-AotSuppressionAudit.ps1` now uses fingerprint-based matching (file + warningId + justification) instead of line numbers. Line shifts from code edits no longer trigger false NEW/STALE pairs.

- **SqlServerCdcIdempotencyFilter** -- Persistent CDC event deduplication using `[Cdc].[CdcProcessedEvents]` table with composite primary key (TableName, Lsn, SeqVal). Supports configurable retention with batched cleanup via `SqlServerCdcIdempotencyFilterOptions` (schema, table name, retention period, cleanup batch size). Registered via `UseSqlServerIdempotencyFilter()` builder extension on `ICdcBuilder`. Includes `IValidateOptions<T>` validator with `ValidateOnStart()`. Complements the `InMemoryCdcIdempotencyFilter` added in for single-instance scenarios.
- **ICdcIdempotencyFilter abstraction** -- Internal interface for CDC event deduplication with `IsProcessedAsync` and `MarkProcessedAsync`. Default `InMemoryCdcIdempotencyFilter` uses bounded `ConcurrentDictionary` (10K cap, skip-when-full). Opt-in via `UseInMemoryIdempotencyFilter()` on `ICdcBuilder`. Integrated into `CdcChangeApplier` — checks before handler dispatch, marks after success.
- **CDC idempotency documentation** -- New docs-site content covering idempotency filter overview (why at-least-once needs dedup), InMemory vs SqlServer filter comparison, DI registration examples, and retention/cleanup guidance. Added to `docs/patterns/cdc.md` and `docs/operations/cdc-troubleshooting.md`.

### Fixed

- **CDC SQL Error 313 stale LSN recovery** -- SQL Error 313 ("insufficient arguments") thrown by CDC table-valued functions when LSN falls outside the valid range (e.g., after CDC cleanup jobs) now triggers graceful stale position recovery. Dual-layer defense: (1) defensive pre-check in `CdcChangeDetector.EnqueueTableChangesAsync` validates lastLsn against `fn_cdc_get_min_lsn` per capture instance and resets checkpoint proactively; (2) error code 313 added to `CdcStalePositionDetector.StalePositionErrorNumbers` as safety-net catch filter. New `StalePositionReasonCodes.TvfInsufficientArguments` reason code for diagnostics.
- **CDC adaptive polling error backoff** -- `CdcProcessingHostedService` now distinguishes no-work cycles (normal delay) from error cycles (exponential backoff). Consecutive errors increment a backoff multiplier capped at 5× `PollingInterval`, reset to 1× on first successful cycle. Prevents tight error-retry loops under sustained failure conditions.
- **CDC SQL timeout from range queries** -- Reverted `fn_cdc_get_all_changes` from range query `(@fromLsn, @maxLsn)` to point query `(@lsn, @lsn)`. The TVF materializes ALL rows in the `[fromLsn, toLsn]` range before `TOP`/`WHERE` filtering, causing execution timeouts on high-volume tables with large checkpoint gaps. Point queries bound the TVF scan to a single LSN. The outer loop in `ProducerLoopCoreAsync` handles LSN-by-LSN advancement.
- **CDC per-row log noise** -- Demoted `DataChangeEventProcessor.LogChangeEventProcessed` from `Information` to `Debug`. Per-row success logging flooded consumer logs with hundreds of identical lines per poll cycle. The batch summary at `CdcChangeApplier.LogCompletedProcessing` already provides operator-level Information totals.

### Changed

- **CDC performance optimization** -- Batch checkpoint writes per-table instead of per-event, adaptive polling skips delay when work found, `CdcDefaultConsumerBatchSize` increased from 10 to 50, pre-computed column filter and shared `DataTypes` dictionary in `CdcRepository.FetchChangesAsync`, cached Polly policy per batch. `ICdcRepository.FetchChangesAsync` now accepts `fromLsn` + `toLsn` range parameters (callers should pass `fromLsn == toLsn` for point queries). `CdcRow.DataTypes` changed from `Dictionary<string, Type>` to `IReadOnlyDictionary<string, Type>`. **Breaking change** for consumers calling `FetchChangesAsync` directly or accessing `CdcRow.DataTypes` as mutable.

### Fixed

- **CDC batch checkpoint data loss** -- Fixed critical bug where `onFatalError`-swallowed exceptions allowed later same-table events to advance the checkpoint past the failed event, permanently skipping it. The table is now removed from checkpoint tracking on failure, ensuring the failed event is reprocessed on the next cycle.

### Changed

- **ServerlessHostOptions ISP split** -- Removed nested `AwsLambda`, `AzureFunctions`, `GoogleCloudFunctions` properties from `ServerlessHostOptions`. Per-platform options now registered independently via `IOptions<AwsLambdaOptions>`, `IOptions<AzureFunctionsOptions>`, `IOptions<GoogleCloudFunctionsOptions>` when calling `AddAwsLambdaHosting()`/`AddAzureFunctionsHosting()`/`AddGoogleCloudFunctionsHosting()`. `ServerlessHostOptions` retains only 6 shared cross-cutting properties. **Breaking change** for consumers accessing nested platform properties.
- **DI naming convention doc fix** -- Removed stale "Known Violations" table and `[Obsolete]` references from `docs/architecture/di-naming-convention.md`.

### Added

- **NServiceBus feature-parity evaluation** -- Comprehensive 10-dimension comparison. Result: parity or superiority across all dimensions. 1 MEDIUM gap (saga timeouts) is tracked for a future release.

### Added

- **MinimalWiring bridge conformance tests** -- A2/A3/A4 bridge shapes (ElasticSearchProjections, DataProcessing, CDC) with bucket classification and isolation/idempotence gates.
- **Security namespace-vs-folder policy doc** -- `docs/architecture/security-namespace-policy.md` documenting when Excalibur.Security folders get sub-namespaces.
- **Builder method naming convention doc** -- `docs/architecture/builder-pattern-convention.md` canonical 4-method connection pattern (ConnectionString, ConnectionStringName, ConnectionFactory, BindConfiguration).
- **Public helper audit** -- evaluated the retained-public class helpers against the Required Public API Checklist.

### Changed

- **A3 DI three-pillar naming** -- `AddDispatchAuthorization` → `AddExcaliburAuthorization`, `AddDispatchAdvancedSagas` → `AddExcaliburAdvancedSagas`, `AddDispatchOrchestration` → `AddExcaliburOrchestration`, `AddDispatchHealthChecks` → `AddExcaliburHealthChecks`. Direct renames, no `[Obsolete]` shims. **Breaking change** for consumers calling old method names.
- **Elastic IndexTemplate SDK-type hide** -- `IndexTemplateConfiguration.Template` (`IndexSettings`) and `Mappings` (`TypeMapping`) replaced with opaque `SettingsJson` and `MappingsJson` (`JsonElement?`). Same for `ComponentTemplateConfiguration`. SDK types confined to `Internal/` adapter layer. **Breaking change** for consumers directly setting `Template`/`Mappings` properties.

### Added

- **SmartEnum\<T\> DDD building block** -- Type-safe enumeration base class in `Excalibur.Domain.Model` with `FromId()`, `FromName()`, `TryFromId()`, `TryFromName()`, `GetAll()`. Supports case-insensitive name lookup, equality by ID, and error messages listing valid values. Replaces raw enums for constrained value sets (OrderStatus, PaymentMethod, etc.).
- **CDC DI forwarding registrations** -- All 7 CDC providers now register forwarding DI entries so consumers can resolve processors via base interfaces (`ICdcProcessor<T>`, `ICdcStreamProcessor<T,TPos>`) in addition to provider-specific marker interfaces.
- **SDK seam interfaces** -- `IStorageClientSeam` (GCP), `IServiceBusSenderSeam`/`IServiceBusReceiverSeam`/`IServiceBusProcessorSeam` (Azure ServiceBus), `IPublisherClientSeam`/`ISubscriberClientSeam` (GCP PubSub), `IArmClientSeam` (Azure ARM). Internal adapter pattern replaces concrete SDK fakes in tests with proper seams. SDK governance fakes reduced from 11 to 4.
- **DataProcessing assembly scanners** -- `AddProcessorsFromAssembly` and `AddRecordHandlersFromAssembly` on `IDataProcessingBuilder`. AOT-annotated; explicit registration alternatives available.

### Changed

- **ExcaliburHeaderNames + Cultures moved** -- Moved from `Excalibur.Domain` to `Excalibur.Application`. These are HTTP infrastructure concerns, not domain model types. Consumer `using` statements must update. A3 consumers use type aliases to avoid namespace collision with `ApplicationContext`.
- **Swashbuckle 6→10 migration** -- `SwaggerGenOptionsExtensions` updated for Microsoft.OpenApi v2 API (`OpenApiSchema` constructor changes). Package reference updated in `Directory.Packages.props`.
- **CdcJobQuartz sample composition** -- Consolidated 3 separate registration calls (1×`AddDispatch` + 2×`AddExcalibur`) into single `AddExcalibur` root with `ScanAssemblies()`, `AddJobs()`, `AddEventSourcing()` chained.

- **CDC ISP two-tier hierarchy** -- New `ICdcProcessor<TEvent>` (batch, 1 method) and `ICdcStreamProcessor<TEvent, TPosition>` (streaming, 3 methods) base interfaces in `Excalibur.Cdc`. All 7 CDC providers (CosmosDB, MongoDB, Postgres, DynamoDB, Firestore, SqlServer, InMemory) converted to marker interfaces inheriting the appropriate base. Compile-time safety: injecting a poll-only provider where streaming is required now fails at compile time. **Breaking change** -- provider interfaces no longer declare methods directly; consumers must code against the base interfaces or the provider marker.
- **DelegatingPersistenceProvider** -- Abstract decorator base class following Microsoft `DelegatingHandler` pattern. All methods virtual, forwarding to `Inner`. Paired with `PersistenceProviderBuilder` (sealed, `ChatClientBuilder` pattern) for fluent `Use()` + `Build()` composition.
- **IRepository\<TEntity, TKey\>** -- Non-event-sourced CRUD repository abstraction in `Excalibur.Domain`: `GetByIdAsync`, `SaveAsync` (upsert), `DeleteAsync`. Distinct from `IEventSourcedRepository<T,TKey>`.
- **DataProcessing assembly scanners** -- `AddProcessorsFromAssembly` and `AddRecordHandlersFromAssembly` extension methods on `IDataProcessingBuilder`. AOT-annotated with `[RequiresUnreferencedCode]`; explicit `AddProcessor<T>` / `AddRecordHandler<THandler,TRecord>` available as AOT-safe alternatives.

### Changed

- **Snapshot.Data byte\[\] → ReadOnlyMemory\<byte\>** -- `ISnapshot.Data` and `Snapshot.Data` changed from `byte[]` to `ReadOnlyMemory<byte>` for improved immutability and zero-copy slicing. `Snapshot.Create()` factory still accepts `byte[]` via implicit conversion. All 8 snapshot store implementations updated. **Breaking change** for custom `ISnapshot` implementations.
- **Serverless host provider cleanup** -- AWS Lambda, Azure Functions, and Google Cloud Functions host providers now consistently emit `LogLevel.Warning` stubs for telemetry options without platform SDK integration. Dead stub methods (`ConfigureXRayTracing`, `ConfigureLambdaMetrics`, `ConfigureGoogleCloudTracing`, `ConfigureGoogleCloudMetrics`) removed.
- **LeaderElectionOptionsValidator sealed** -- Changed from `public class` to `public sealed class`.
- **CDC method renames** -- `ProcessCdcChangesAsync` → `ProcessBatchAsync` (SqlServer), `ProcessChangesAsync` → `ProcessBatchAsync` (InMemory) for unified contract consistency.

### Removed

- **SqlServer-specific ICdcProcessor deleted** -- Replaced by `ISqlServerCdcProcessor` marker interface extending the new generic `ICdcProcessor<T>`.
- **Duplicate CDC provider method declarations** -- 200+ lines of duplicated interface method declarations across 6 CDC providers eliminated by inheritance from base interfaces.

### Removed

- **Authorization RequestProvider layer deleted** -- 36 legacy `RequestProvider` files removed from `Excalibur.Data.SqlServer` and `Excalibur.Data.Postgres`. SQL is now inlined directly into Store implementations (`SqlServerGrantStore`, `SqlServerActivityGroupStore`, `PostgresGrantStore`, `PostgresActivityGroupStore`). The Store pattern (`IGrantStore`, `IActivityGroupStore`) was already the public contract; RequestProviders were never DI-registered or consumer-accessible. ~98 `PublicAPI.Shipped.txt` entries removed. No functional changes.

### Changed

- **MongoDB.Driver 2.x → 3.x migration** -- Upgraded `MongoDB.Driver` from 2.30.0 to 3.8.0 across all 8 shipping MongoDB packages (`Excalibur.Data.MongoDB`, `Excalibur.EventSourcing.MongoDB`, `Excalibur.Cdc.MongoDB`, `Excalibur.Saga.MongoDB`, `Excalibur.Inbox.MongoDB`, `Excalibur.Outbox.MongoDB`, `Excalibur.LeaderElection.MongoDB`, `Excalibur.Compliance`). Key migration changes: `_ownsClient` pattern for `MongoClient` `IDisposable` lifecycle tracking; sync `Indexes.CreateOne()` → async `CreateOneAsync()` in leader election; `Cluster.Description.Servers`/`WireVersionRange` health check → `buildInfo`/`serverStatus` commands. `MongoDbComplianceStore` gains `IDisposable`. **Breaking change** for consumers who subclass sealed `MongoClient`/`MongoDatabase`/`MongoCollection<T>` (unlikely — use interfaces for mocking).

- **DataProcessing: cursor-based paging replaces offset-based paging** -- `IRecordFetcher<T>.FetchBatchAsync` now accepts `string? cursor` (opaque token) instead of `long skip`, returning `CursorFetchResult<TRecord>` with the next cursor. `IDataProcessor.RunAsync` accepts a `string? processedCursor` for crash-safe resume. Dual-cursor tracking separates transient fetch position from durable processed checkpoint. SQL schema adds `FetchCursor`/`ProcessedCursor` columns with `COALESCE` preservation. **Breaking change** — all `DataProcessor<T>` implementations must update their `FetchBatchAsync` override signature.

- **IErasureService ISP split** -- `ExecuteAsync` removed from the public `IErasureService` interface (now 4 methods). Execution is handled internally by `ErasureSchedulerBackgroundService` via new `internal IErasureExecutor`. Consumers submit requests via `RequestErasureAsync` and monitor via `GetStatusAsync`. **Breaking change** if calling `IErasureService.ExecuteAsync` directly (use the background scheduler instead).
- **ISystemLoadMonitor CancellationToken** -- `GetCurrentLoadAsync()` now requires a `CancellationToken` parameter per.NET convention. **Breaking change** for `ISystemLoadMonitor` implementors.

### Fixed

- **DataProcessorDiscovery AOT split (P0)** -- `TryGetRecordType` split into AOT-safe (attribute-only) and `TryGetRecordTypeWithReflection` (fallback). Assembly-scanning DI path uses reflection; all other paths are AOT-compatible. `[RequiresUnreferencedCode]` scoped to reflection-only path.
- **HandlerInvokerRegistry ValueTask support (P1)** -- `CreateInvoker` now handles `ValueTask` and `ValueTask<T>` return types. `TargetInvocationException` unwrapped via `ExceptionDispatchInfo` to preserve stack traces.
- **StaticPipelineGenerator CS0122 (P1)** -- Source generator no longer casts to internal `Dispatcher` class; uses `IDispatcher` interface instead. Namespace filter prevents interceptor recursion.
- **HashiCorpVault DI double-registration (P1)** -- Changed to singleton forwarding pattern: concrete type registered once, both `ICredentialStore` and `IWritableCredentialStore` forwarded to same instance.
- **DataProcessorRegistry DI mismatch (P1)** -- All 4 `AddDataProcessor` overloads + assembly-scanning path now register both concrete type and `IDataProcessor` interface.
- **DefaultOutboxDispatcher sentinel (P2)** -- `GetPendingMessagesAsync` returns `Enumerable.Empty` instead of throwing when no real outbox is configured. Write operations still throw as fail-fast.
- **10 flaky test fixes** -- Timing thresholds increased (500ms→2000ms CTS, 5s→30s background services), `OperationName`-filtered activity assertions, async delegate fixes, per-test Kafka topic isolation, async disposal for ES adapter.
- **ContextFlowMetrics null safety (P0)** -- 13 counter/histogram fields in `ContextFlowMetrics` used `null!` initialization. Added null-conditional operators (`?.`) to prevent `NullReferenceException` if meter instrument creation fails.
- **MongoDbTenantEventStoreResolver MongoClient leak** -- Cached tenant event stores held undisposed `MongoClient` instances (leaking connection pools since MongoDB.Driver 3.x makes `MongoClient` `IDisposable`). Resolver now implements `IAsyncDisposable` with proper `_clientCache` tracking and ordered disposal.

### Documentation

- **The saga package now ships a guarantee contract.** `Excalibur.Saga/ARCHITECTURE.md` states the
  tenant-isolation guarantee in falsifiable terms and names, for each of the seven providers, the seam
  that enforces it — separated by whether the tenant term is applied server-side in the query or
  client-side after the read, because those two forms fail differently and a consumer auditing their
  own deployment needs to know which one they have. Source anchors are aids for a reader checking the
  claim, not a stable contract: re-locate by the described mechanism if a line has moved.

- **CDC SqlServer XML doc improvements** -- Added XML documentation to `ICdcRepository`, `IDatabaseOptions`, and `DataChangeEventProcessor` in `Excalibur.Cdc.SqlServer`. No behavioral changes.

### Security

- **Snappier 1.3.0 → 1.3.1** ([GHSA-pggp-6c3x-2xmx](https://github.com/advisories/GHSA-pggp-6c3x-2xmx)) -- Infinite-loop vulnerability in `SnappyStream` decompression; 15 bytes of malformed framed-format data can freeze a thread. Transitive dependency via MemoryPack affecting 55 packages. Resolved by bumping `Directory.Packages.props`.

### Fixed

- **DataProcessing: ProcessedCursor never persisted** -- The consumer loop passed `null` for `processedCursor` on every checkpoint, so `COALESCE` in SQL always preserved the existing `NULL` value. Introduced internal `PagedRecord` struct that tags the last record per producer page with the page cursor; consumer now persists the cursor at page-boundary checkpoints, enabling correct crash-recovery resume.
- **DataProcessing: DDL CompletedCount INT → BIGINT** -- Column type mismatched the `long` in C# (`DataTaskRequest.CompletedCount`), risking overflow at ~2.1B records. Fixed in docs-site DDL and sample setup script.
- **DataProcessing: invalid filtered index in DDL** -- `WHERE [Attempts] < [MaxAttempts]` uses column-to-column comparison, which SQL Server filtered indexes do not support. Replaced with a covering index keyed on `[CreatedAt]` (the polling query's ORDER BY column).
- **DataProcessing: IAsyncDisposable record disposal** -- Consumer now prefers `IAsyncDisposable.DisposeAsync()` over `IDisposable.Dispose()` for record cleanup, consistent with framework-wide async disposal pattern.
- **Money value object STJ deserialization** -- `Money` had two parameterized constructors with no `[JsonConstructor]`, causing `NotSupportedException` during System.Text.Json deserialization (e.g., in ElasticSearch projections). Added private `[JsonConstructor]` constructor. Also added defensive `[JsonConstructor]` to `Address` to prevent the same issue if a second constructor is ever added.
- **SqlServerIdentityMapStore.CreateConnection() infinite recursion** -- `_connectionFactory?.Invoke() ?? CreateConnection()` called itself when no explicit connection factory was registered (i.e., `ConnectionString()` or `BindConfiguration()` paths), causing `StackOverflowException` on every database operation. Fixed to fall back to `new SqlConnection(_options.ConnectionString)`.
- **Pre-publish audit: 18 runtime bug fixes across 11 packages**
 - **DI forwarding registration** -- `DataProcessingBuilder.AddProcessor<T>()` now registers concrete type so `DataProcessorRegistry` can resolve processors by concrete type (fixes `InvalidOperationException: No service for type`)
 - **SecurityEventLogger hard-cast** -- replaced unsafe `(SecurityEventLogger)sp.GetRequiredService<ISecurityEventLogger>()` with forwarding pattern (fixes `InvalidCastException` when consumers provide custom `ISecurityEventLogger`)
 - **Idempotent DI registrations** -- converted `AddSingleton`/`AddScoped` → `TryAddSingleton`/`TryAddScoped` across Security, Observability, GooglePubSub, Compliance, and Serverless packages to prevent duplicate registrations on repeated calls; `ICredentialStore` uses concrete-type guard instead (multi-registration interface where multiple stores coexist)
 - **Serializer double-dispose** -- added `_disposed` guard to `DispatchJsonSerializer`, `CompositeAotJsonSerializer`, and `AotJsonSerializer` (`ThreadLocal<T>.Dispose()` throws `ObjectDisposedException` on double-dispose)
 - **CreateScope → CreateAsyncScope** -- `ColdStartOptimizerBase` and new `DispatchTestHarness.CreateAsyncScope()` (missed in prior sweep)
 - **IAsyncDisposable** -- added to `MultiRegionKeyProvider` (replaces spin-wait), `LongPollingOptimizer`, `StreamHealthMonitor` (fixes disposal race), `CloudMonitoringExporter` (added `_disposed` guard)
 - **Load balancer thread-safety** -- `WeightedRoundRobinLoadBalancer` counters now use `Interlocked.Increment`; both load balancers add `volatile` to snapshot fields for correct double-checked locking
 - **CachingMiddleware** -- explicit null check on `DeserializeCachedValue` return (was `null!`); swallowed `ICachePolicy` exceptions now logged via `LogWarning`
- **CreateScope → CreateAsyncScope** across 12 framework services -- `DataProcessingHostedService`, `DataProcessor<T>`, `SagaTimeoutDeliveryService`, `QuartzJobAdapter`, `OutboxProcessor`, `InboxProcessor`, `PoisonMessageHandler`, `SnapshotCreationJob`, `ProjectionRebuildJob`, `OutboxProcessorJob`, ElasticSearch/OpenSearch `HostExtensions`, and `JitAccessExpiryService` now use `CreateAsyncScope()` to correctly dispose services implementing `IAsyncDisposable` (fixes `InvalidOperationException` when processors inherit from `DataProcessor<T>`)
- **dependency-review-action@v5 → v4** -- CI security workflow referenced non-existent action version
- **Nullability test fixes** -- `DataProcessingBuilderShould` CS8764 (`DbConnection.ConnectionString` override) and `EphemeralProjectionEngineExtendedShould` CS8620 (FakeItEasy `Returns` type inference)
- **Docusaurus MDX v3 parse errors** -- 4 docs files using `{#custom-id}` heading syntax converted to `<div id="..." />` anchors
- **23 npm vulnerabilities resolved** -- upgraded Docusaurus 3.9→3.10 (added `@docusaurus/faster`), overrode `minimatch@10.2.5` and `serialize-javascript@7.0.5`

### Changed

- **Versioning: GitVersion → MinVer migration** -- Package versioning now uses [MinVer](https://github.com/adamralph/minver) 6.0.0 (Polly pattern) instead of GitVersion. Versions are computed from git tags (`v3.0.0-alpha.N`); commits after a tag auto-increment the pre-release identifier. Local dev defaults to `3.0.0-alpha.0`. Release workflow updated to pass `MinVerVersionOverride` for `workflow_dispatch` builds. `GitVersion.yml` removed. SourceGenerators project carries an explicit MinVer reference (opts out of CPM).
- **Release workflow hardened** -- `release.yml` build step now passes `MinVerVersionOverride` to ensure correct version in both build and pack phases; removed redundant `AssemblyVersion`/`FileVersion`/`InformationalVersion` overrides from `dotnet pack` (MinVer sets all four version properties during build)

### Added

- **`ICdcBuilder.BindProcessingConfiguration(string sectionPath)`** -- allows binding `CdcProcessingOptions` to an `IConfiguration` section (e.g., `appsettings.json`) via the CDC builder fluent API
- **`WithProjectionHealthChecks()`** -- opt-in projection health check registration (previously auto-registered by `UseEventNotification()`)
- **`IProjectionRebuildService.GetStatusAsync<TProjection>()`** -- type-safe per-projection rebuild status query
- **`IProjectionRebuildService.GetAllStatusesAsync()`** -- bulk rebuild status monitoring
- **`PersistencePrerequisiteValidator`** + **`InboxPrerequisiteValidator`** -- fail-loud-at-host-start probes for missing persistence/inbox provider registrations
- **Non-keyed DI forwarding aliases** across 6 subsystems (EventSourcing, LeaderElection, Outbox, Saga, Inbox, Persistence) -- consumers can inject stores directly without `[FromKeyedServices]`

### Changed

- **`ProjectionRebuildService`** narrowed from `public sealed` to `internal sealed` — consumers use `IProjectionRebuildService` interface via DI
- **Projection health checks** are now opt-in via `WithProjectionHealthChecks()` instead of auto-registered — reduces overhead for consumers who don't need health monitoring

- **AddDispatchInstrumentation()** unified OTel entry point -- registers all 18 meters + 26 ActivitySources in one call, with auto-wire via `AddDispatchPipeline()`
- **Excalibur.Dispatch.Analyzers** package with 6 diagnostic rules (DISP101-DISP106): DI namespace enforcement, extension class naming, CancellationToken interface conventions, namespace segment validation, ConfigureAwait enforcement, blocking call detection
- **Templates CI workflow** validating all 8 `dotnet new` templates produce buildable projects
- **DocFX API reference workflow** for automated API documentation generation
- **Coverage threshold enforcement** -- quality gates now fail (not just report) below 65% combined coverage
- ****: dependency-update commit-hygiene policy (patch grouping / one-per-commit minor / rationale-required major) -- governs all subsequent dep-bump sprints
- ** §D7.1**: canonical `Store` / `Provider` / `Manager` / `Operations` domain-role suffix taxonomy formalized with selection rule (naming-test then shape-test) and the- 14-seam precedent table
- **PrerequisiteValidators** (4): `EventSourcingPrerequisiteValidator`, `LeaderElectionPrerequisiteValidator`, `OutboxPrerequisiteValidator`, `SagaPrerequisiteValidator` -- `internal sealed IHostedService` probes that fail loud at host start if the subsystem's required abstraction is missing from the container (actionable error message names subsystem, missing type, and provider registration path)
- **MinimalWiringConformanceTestKit.IgnoredDescriptorPredicates** hook for upstream-SDK non-idempotence scenarios
- **XUnit `CollectionDefinition`** on `Excalibur.Saga.Tests.StateMachine.*` to serialize shared-state tests (fixes under-parallel-load flakiness)
- **Windows AOT publish prerequisites** section in `docs/architecture/aot-compatibility.md` -- documents MSVC Build Tools + Windows 11 SDK requirement
- **`CursorEncoder`** (in `Excalibur.EventSourcing.Abstractions`): typed cursor serialization primitive for cursor-based pagination — encode/decode strongly-typed position tokens with tamper-evident HMAC option. Base-64Url wire format; stable across processes.
- **`ElasticIndexMappingBuilder`** + **`IElasticIndexConfiguration`** (in `Excalibur.Data.ElasticSearch`): fluent builder for ES index mappings with per-field type/analyzer/subfield configuration; decouples projection definitions from raw Elastic SDK mapping DSL.
- **`ElasticSearchCursorHelper`** (in `Excalibur.Data.ElasticSearch`): opinionated cursor helper for ES-backed paginated queries; pairs with `CursorEncoder` for end-to-end cursor pagination.
- **`docs-site/docs/data-access/pagination.md`**: consumer guide for cursor-vs-offset pagination, including ES-specific recipes.
- **`AsyncProjectionProcessingHost`** -- background hosted service for continuous projection processing with cursor tracking, batch processing, and graceful shutdown
- **`SqlServerGlobalStreamQuery`** -- SQL Server implementation for global stream projection queries
- **`docs-site/docs/data-access/data-request.md`**: consumer guide for IDataRequest usage patterns
- **Typed dispatch** -- `IDispatcher.DispatchAsync<TResponse>(IDispatchAction<TResponse>)` overloads that infer `TResponse` from the action parameter type, eliminating explicit dual type arguments at the call site. Includes context-free, explicit-context, and `DispatchChildAsync` variants. Backed by `TypedDispatchDelegateCache` for zero-alloc hot-path dispatch.
- **`DispatchActionExtensionGenerator`** source generator -- emits per-action strongly-typed extension methods when `EnableTypedDispatchExtensions()` is opted in via `DispatchBuilder`.
- **`HandlerRegistrySourceGenerator`** -- source-generated `AddDiscoveredHandlers()` extension for fully AOT-safe handler registration. Zero reflection, replaces `HandlerRegistryBootstrapper` and `HandlerRegistryExtensions`.

### Fixed

- **AOT pre-warm guard**: skip reflection-based `HandlerActivator`/`HandlerInvoker` cache pre-warm when `RuntimeFeature.IsDynamicCodeSupported` is `false`; prevents `PlatformNotSupportedException` in native AOT deployments
- **Flaky CI**: `ErasureSchedulerBackgroundServiceShould.Continue_after_processing_error` timeout increased from 5s to 10s to match peer background-service tests under full-suite CI load

### Changed

- **Projection system rework**: `EventNotificationBroker` enhanced with reflection caching and improved observability; `ProjectionRebuildService` batch rebuild support added; `IProjectionBuilder` simplified; `InMemoryProjectionRegistry` and `InMemoryCursorMapStore` hardened
- **CDC SqlServer decomposition**: monolithic `CdcProcessor` decomposed into focused collaborators (`CdcChangeDetector`, `CdcChangeApplier`, `CdcCheckpointManager`, `CdcRepository`). `DataChangeEvent`/`DataChangeExtensions` hardened, `CdcRecoveryOptions` validation added, dead `DatabaseOptions`/`IDatabaseOptions` removed. PublicAPI baselines updated.
- **DataProcessing quality hardening**: `DataProcessor`/`DataOrchestrationManager` hardened with structured logging, `CancellationToken` propagation, disposal guards. Added `DataProcessingHealthCheck` + `DataProcessingHealthState` health-check infrastructure. Exception types improved with serialization support. Dapper SQL requests updated.
- **AOT suppression baseline refreshed** after source-generator, handler, CDC, and data-processing infrastructure changes
- **.NET 10 dependency bump**: `Microsoft.Extensions.*` and `System.*` packages updated from 10.0.6 → 10.0.7

### Removed

- **Dead projection code removed**: `CursorPageRequest` (relocated to cursor pagination), `DirtyCheckingMode`, `IMultiStreamProjectionBuilder`, `MultiStreamProjectionBuilder` -- superseded by simplified projection builder API
- **5 dead source generators** deleted: `HandlerActivationGenerator`, `HandlerInvocationGenerator`, `MessageFactorySourceGenerator`, `MessageTypeRegistrySourceGenerator`, `ZeroAllocationHandlerInvokerGenerator` — all were unused/superseded by `HandlerRegistrySourceGenerator` and `HandlerInvokerSourceGenerator`
- **Handler infrastructure simplified** (-1,626 lines): extracted `HandlerActivatorRegistry` and `ResultFactoryRegistry` with thread-safe public APIs for AOT source-gen integration; `HandlerInvoker`/`HandlerActivator` internals consolidated

### Changed

- **Money value object: ISO 4217 currency separation**. `Money` constructor now accepts `string currencyCode` (ISO 4217 — "USD", "EUR", "GBP") as the primary identifier. Previous `cultureName` parameter is removed — culture is a display concern handled by `ToString(CultureInfo)`. Follows the pattern used by `java.util.Currency` + `NumberFormat` and NodaMoney. Multi-currency applications can now correctly represent currency identity independent of user locale. Breaking API change; consumers update from `new Money(100, "en-US")` to `new Money(100, "USD")`. `MoneyTypeHandler` and `NullableMoneyTypeHandler` (SqlServer) updated accordingly.
- **Pagination primitives relocated**: `CursorPageRequest`, `CursorPagedResult`, `PageNavigation`, `PagedResult` moved from `Excalibur.Domain` to `Excalibur.EventSourcing.Abstractions` (event-sourcing is the primary consumer). `Excalibur.Domain.PublicAPI.Shipped.txt` drops the four types; `Excalibur.EventSourcing.Abstractions.PublicAPI.Shipped.txt` adds them. Consumer impact: namespace-only `using` change; no type shape edits. Aligns with Dispatch/Excalibur separation — Domain stays focused on aggregate/entity/value-object primitives.
- **Benchmark baseline refreshed to `20260420` epoch** (`benchmarks/baselines/net10.0/dispatch-comparative-20260420/`). BenchmarkDotNet `0.15.8` on.NET SDK `10.0.202` / Runtime `10.0.6`. 16 reports committed across Comparative + WarmPath configurations. Prior `20260302` baseline preserved on disk as superseded (not cited for new claims). Absolute numbers are **not cross-diffable across the BDN 0.15.4 → 0.15.8 epoch boundary**; ratios within each report remain apples-to-apples. See `docs/performance/competitor-benchmarks.md` and `docs/benchmarks/results/current/performance-report.md` for refreshed headline numbers. One row (100-concurrent-commands allocation) is flagged under investigation pending a methodology-matched WarmPath rerun.
- **CS1591 XML documentation** now enforced on all shipping packages at build time; suppression moved to non-shipping code only
- Options validation error messages now include type names and config section guidance
- **Target framework: `.NET 10 only`** -- dropped net8.0 / net9.0 multi-target. Templates, Dockerfiles, docs (compatibility-matrix, deployment, aot-compatibility, cicd-testing-package-pipeline), CONTRIBUTING.md, RELEASE.md, and eng scripts updated accordingly
- **Dep currency sweep**: `Serilog` 3→4, `Microsoft.ApplicationInsights` 2→3 (removed deprecated `EnableEventCounterCollectionModule` + `EnableAdaptiveSampling`), `Google.Cloud.Firestore` 3→4, `Medo.Uuid7` 1→3 (byte-layout contract migration), plus `System.Security.Cryptography.Xml` 10.0.6 CVE pin-forward, Testcontainers, Polly, OpenTelemetry, NBomber, FluentMigrator, and ~24 more
- **Security folder refactor**: 14 root files organized into `/CredentialStores/`, `/EventStores/`, `/Middleware/` with matching sub-namespaces; 3 DI-extension classes consolidated
- **Test project rename**: 11 test projects under `tests/unit/` + `tests/benchmarks/` renamed from `Excalibur.Dispatch.{AuditLogging*,Compliance*}` to `Excalibur.{AuditLogging*,Compliance*}` matching src-side package rename. 172 namespace updates, 12 `InternalsVisibleTo` updates, 4 transitive `PackageReference` updates, cascade across `.sln` / 6 `.slnf` / manifest / governance / AOT baseline
- **IndexTemplateDescriptor.Metadata**: SDK-type hide -- `IReadOnlyDictionary<string, object?>?` → `IReadOnlyDictionary<string, string>?` (projects via `.ToString()` in internal adapter)
- **System.Threading.Lock**: adopted.NET 9+ `Lock` type in `InMemoryApiKeyManager` and `LeastLoadedPlacementStrategy` (IDE0330 compliance, dropped pragma)
- **`field` keyword (C# 13) suggestion enabled** (`IDE0370` severity: `suggestion`)
- **Elastic.Clients.Elasticsearch 8.17 → 9.3.4 migration**. Completes the forward-path blocked by the `Elastic.Transport` pin-back. 101 consumer files migrated across 4 packages (`Excalibur.Data.ElasticSearch`, `Excalibur.AuditLogging.Elasticsearch`, `Excalibur.Inbox.ElasticSearch`, `Excalibur.Outbox.ElasticSearch`). `Elastic.Transport` restored to 0.16.0, `Testcontainers.Elasticsearch` restored to 4.11.0 (pin-back comments removed). All 1578 ES-related tests pass (1560 unit + 18 integration).
- **Security folder/namespace consolidation**. Resolved criterion #4 violation: 6 credential types in `/Configuration/` now match their folder namespace; duplicate credential-store locations consolidated to a single canonical home.
- **NServiceBus added to comparative benchmark suite**. New `NServiceBusComparisonBenchmarks` class mirroring existing MediatR/Wolverine/MassTransit comparison shape. Wired into `eng/run-comparative-benchmarks.ps1`.
- **Comparative benchmark script coverage completed**. `eng/run-comparative-benchmarks.ps1` filter + expected-reports arrays now cover all benchmark classes including `RoutingFirstParityBenchmarks`.
- **Benchmark dependencies refreshed**: MediatR 12.2.0→12.5.0, MassTransit 8.4.1→8.5.9, WolverineFx 5.2.0→5.31.1 (latest pre-commercial versions; benchmark-only, no shipping-package impact). Full 9-class comparative rerun on updated deps. See `docs/performance/competitor-benchmarks.md` for licensing context.

### Fixed

- **Governance: Core package no longer pulls Azure.* transitives.** Removed unused `Microsoft.ApplicationInsights` PackageReference from `Excalibur.Dispatch.csproj` (zero.cs consumers; dead weight). Post- the v3 transitive graph pulled `Azure.Core` + `Azure.Monitor.OpenTelemetry.Exporter` into the Core package, which the `transitive-bloat-report.ps1` governance gate correctly flags as a prohibited provider-SDK intrusion. Telemetry/OTel integration belongs in `Excalibur.Dispatch.Observability`, not Core.
- **Governance: `management/package-map.yaml` restored** with comprehensive categorization of all 170 shipping packages (Abstractions / Core / Framework / Hosting / Provider / Excalibur / Testing / Tool / Metapackage). The `transitive-bloat-report.ps1` governance gate no longer falls back to conservative heuristics; it now deterministically categorizes every project. File was deleted during a 2025 cleanup and had been silently missing since.
- **Elastic SDK seam regression.** CI integration-tests surfaced 12 Elasticsearch conformance/integration failures with `Elastic.Transport.UnexpectedTransportException: The JSON value could not be converted to Elastic.Clients.Elasticsearch.IndexManagement.IndexMappingRecord` plus cascade `NullReferenceException`s. Root cause: `Elastic.Transport` is a pre-1.0 library with breaking minor-version changes;'s 0.10.1→0.16.0 bump combined with `Testcontainers.Elasticsearch` 4.7→4.11 (newer ES server image) produced a client/server JSON schema mismatch for `IndexMappingRecord`. Pinned both back (`Elastic.Transport` → 0.10.1, `Testcontainers.Elasticsearch` → 4.7.0) until the paired `Elastic.Clients.Elasticsearch` 8→9 migration completes (follow-up tracked separately). Both `Directory.Packages.props` pins carry inline comments documenting the revert rationale.
- 154+ CI build errors: broken XML crefs, AOT annotation mismatches, null dereferences in source generators
- TrimmerRoots.xml stale reference to deleted `Messaging.MessageResult`
- Testing and CloudEvents samples registered in governance matrix
- PublicAPI baselines promoted (all Unshipped -> Shipped), `*REMOVED*` entries cleared
- Internal type crefs in public XML docs replaced with `<c>` tags (cross-assembly resolution)
- AOT annotation inheritance on override methods (`IEventSerializer.ResolveType`, `AotJsonEventSerializer`, Jobs.* packages)
- **`DefaultFunctionContext.Items` fresh-dictionary bug** in `Excalibur.Dispatch.Hosting.AzureFunctions` -- `get => new Dictionary<>()` caused silently-lost writes; replaced with stable backing field + regression test
- **Saga flaky test family** (`Excalibur.Saga.Tests.StateMachine.*` under parallel load) resolved via xUnit `CollectionDefinition(DisableParallelization=true)` -- confirmed by 20× shard-08 iterations
- **Governance manifest drift** (pre-existing): removed 3 stale `src/Dispatch/Excalibur.Security*` entries, added 2 missing `src/Excalibur/Excalibur.Security.{Aws,Azure}` entries -- `eng/validate-solution.ps1` now PASSES 342=342=342 projects
- **ProblemDetailsOpenApi NuGet packaging** -- `problem-details.openapi.yaml` was packed as `contentFiles/any/any/`, causing NuGet to inject the YAML file into every consumer project and producing CS build errors. Switched to `EmbeddedResource`; replaced `GetYamlPath()` with `GetYaml()` and `GetYamlStream()` APIs that read from the embedded resource
- **AzureFunctions test-bootstrap types moved out of prod** -- `DefaultFunctionContext`, `DefaultTraceContext`, `DefaultRetryContext`, `DefaultInvocationFeatures` relocated from `src/Dispatch/Excalibur.Dispatch.Hosting.AzureFunctions/` into `tests/unit/Excalibur.Dispatch.Hosting.Serverless.Tests/Bootstrap/` (prod file shrank 329 → 203 LOC)
- **`SamplesOnly.slnf`** missing `samples/05-serverless/AzureFunctions` entry added
- **`Templates` multi-target-template brokenness**: `template.json` advertised net8/net9 Framework choices but csprojs hardcoded net10.0; `--Framework net8.0` silently produced net10.0 output. All 8 templates + 7 Dockerfiles + `eng/test-templates.ps1` + `templates-ci.yml` collapsed to net10.0 only. Also fixed 2 pre-existing JSON syntax errors (trailing comma + duplicate brace) in `excalibur-outbox` and `excalibur-saga` template.json
- **THIRD-PARTY-NOTICES.md regenerated** -- was stale with respect to dep bumps (Medo.Uuid7 1.4.0→3.2.0, Polly 8.6.4→8.6.6, Microsoft.ApplicationInsights 2.23→3.1, Serilog 3.1→4.3, Google.Cloud.Firestore 3.7→4.2, System.Text.Json 10.0.0→10.0.6, ~170 other ripple updates)
- **15 orphan `src/Dispatch/Excalibur.Dispatch.{AuditLogging*,Compliance*}/` directories** removed

## [3.0.0-alpha] - Pre-release Development

The 3.0.0 alpha series represents a complete ground-up redesign of the Excalibur.Dispatch framework. This section captures the cumulative changes across the alpha development cycle.

### Architecture & Core

- **Microsoft-style transport layer**: `ITransportSender` (3 methods), `ITransportReceiver` (3 methods), `ITransportSubscriber` (push-based) -- replacing bloated `ICloudMessagePublisher` (10 methods) and `CloudMessage` (36 properties)
- **TransportMessage** (9 properties) with `DelegatingTransport*` decorator bases and builder pattern (`Use()` + `Build()`)
- **Ultra-local dispatch API** via `IDirectLocalDispatcher` with `ValueTask`/`ValueTask<T>` paths for local success scenarios
- **Precompiled middleware chain pathing** and no-middleware fast path for zero-overhead dispatch routing
- **MessageResult unification**: single `Abstractions.MessageResult` static factory with `Success()`, `Cancelled()`, `Failed()` methods and cached singletons
- **MessageContext** with pooled defaults and lazy `Items` to reduce hot-path allocations
- **Endpoint mapping** aligned to action semantics (`Dispatch*Action` naming) with strict cancellation propagation

### Transport & Messaging

- **Dead Letter Queue** universal support: Kafka (`{topic}.dead-letter`), AWS SQS (native), Azure Service Bus (`$DeadLetterQueue`), RabbitMQ (DLX), Google PubSub
- 8 transport decorators: Telemetry, DeadLetter, Ordering, Retry, and more
- DI registration parity: 3 interfaces x 5 transports = 15 builder registrations
- Transport conformance test kit validating all providers
- `CancellationToken = default` removed from entire codebase (~1,806 instances)
- `ConfigureAwait(false)` added to ~329 await statements

### Quality & Safety (985+ fixes)

- **110 P0 critical fixes**: SQL injection prevention, thread-safety (`volatile`, `ConcurrentBag`, `Interlocked`, bounded `ConcurrentDictionary`), async void elimination, disposal safety (`IAsyncDisposable` + drain pattern)
- **875+ P1/P2 fixes**: resilience, middleware, DI, namespace conventions, AOT annotations
- **Zero open issues milestone** -- 0 P0, 0 P1, 0 P2, 0 P3
- `volatile _disposed` sweep: 202 fields across 152 files
- `[GeneratedRegex]` for SQL validation (AOT-safe)
- HMACSHA256 signing for GDPR data subject hashing

### PII-Safe Telemetry

- `ITelemetrySanitizer` (2 methods), `HashingTelemetrySanitizer` (SHA-256 with bounded cache), `NullTelemetrySanitizer`
- `SetSanitizedErrorStatus` extension for OTel spans
- 7 middleware PII-hardened: Authentication, JwtAuthentication, Authorization, AuditLogging, Logging, TenantIdentity, MetricsLogging
- `SensitiveDataPostConfigureOptions` flows `IncludeRawPii` to all `IncludeSensitiveData` flags
- Consumer migration guide: `docs/guides/pii-telemetry-migration.md`

### Interface & Options Compliance

- **94 interfaces** decomposed to ISP compliance (<=5 methods)
- **69 Options types** split to <=10 properties with sub-options composition
- **ValidateOnStart** for ~100 DI registrations across all packages
- **DataAnnotations** `[Required]`/`[Range]` on ~60 Options classes
- **IValidateOptions<T>** cross-property validators for 15+ Options types
- `IMeterFactory` lifecycle migration: static `new Meter(...)` to DI-managed across 5 subsystems
- 85 backward-compat shims removed
- 13 ElasticSearch `*Settings` renamed to `*Options`

### Event Sourcing & Domain

- Snapshot upgrading: `SnapshotUpgrader<TFrom,TTo>` + `SnapshotVersionManager` (BFS shortest-path)
- GDPR: `DataSubjectHasher`, `IEventStoreErasure`, atomic `InMemoryErasureStore`
- `IEventStore` simplified to 3 methods: `LoadAsync` x2, `AppendAsync`
- `DomainEventBase` abstract record, `DomainException` decoupled from `ApiException`
- `DateTime` to `DateTimeOffset` migration
- IdentityMap feature with CompositeKey validation and MERGE pattern

### Patterns & Infrastructure

- CDC: `CdcProcessor` SRP split into `CdcChangeDetector` + `CdcCheckpointManager` + `CdcChangeApplier`
- `ISagaBuilder` Microsoft-style `AddExcaliburSaga(Action<ISagaBuilder>)` with Use/Build
- `IOutboxStore` ISP: core (5 methods) + `IOutboxStoreAdmin` across 10 providers
- `IRepository` abstraction, `IDomainEventEnricher`, `TelemetryPersistenceProvider`
- `ColdStartOptimizerBase` shared serverless cold start optimization
- `TransactionScopeBase` shared base for ITransactionScope implementations
- `IAuditActorProvider` configurable actor identity for audit logging

### Testing & DX

- `AddDispatchTesting()` DI, `SagaTestFixture`, `AggregateTestFixture`
- Transport test doubles: `InMemoryTransportSender/Receiver/Subscriber`
- `Excalibur.Dispatch.Testing.Shouldly` assertion package
- `TestMeterFactory` helper for unit tests needing functional meters
- 2,413 test files migrated to standardized trait constants
- 35,000+ tests across 80+ test projects

### AOT & Trimming

- `IsAotCompatible` for ~42 packages
- Source generators for compile-time handler resolution
- `[LoggerMessage]` source-gen migration for structured logging
- Explicit generic DI registration for AOT safety

### Analyzers

- **DISP001**: Handler Not Discoverable
- **DISP002**: Missing AutoRegister Attribute
- **DISP003**: Reflection Without AOT Annotation
- **DISP004**: Optimization Hint
- **DISP005**: Handler Should Be Sealed
- **DISP006**: Message Type Missing Dispatch Interface
- **DISP101**: DI Extension Wrong Namespace
- **DISP102**: Extension Class 'I' Prefix
- **DISP103**: CancellationToken Default in Interface
- **DISP104**: '.Core.' Namespace Segment
- **DISP105**: Missing ConfigureAwait(false)
- **DISP106**: Blocking Call in Async Method

### CI/CD & Governance

- 18 GitHub Actions workflows (CI, release, quality gates, governance, performance, AOT validation, CodeQL, secrets, docs)
- 25+ PowerShell/shell quality gate scripts
- 14 CI shards for parallel test execution
- CycloneDX SBOM generation in security and release pipelines
- Public API baseline enforcement (RS0016/RS0017 as errors)
- Solution governance validation with 317/317 project compliance
- Template validation, DocFX API docs generation

### Packaging

- 175+ shipping NuGet packages
- Triple-targeting: net8.0, net9.0, net10.0
- SourceLink, deterministic builds, symbol packages
- Per-package README.md for NuGet.org display
- Multi-license stack: Excalibur 1.0 / AGPL-3.0 / SSPL-1.0 / Apache-2.0
- 8 `dotnet new` templates: dispatch-api, dispatch-minimal-api, dispatch-worker, dispatch-serverless, excalibur-ddd, excalibur-cqrs, excalibur-saga, excalibur-outbox
- 68 sample projects across 13 categories

[Unreleased]: https://github.com/TrigintaFaces/Excalibur/compare/v3.0.0-alpha.85...HEAD
