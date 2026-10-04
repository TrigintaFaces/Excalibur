# Tiered event storage

This document explains the implemented archive/read-through design for contributors. Consumer registration examples are in `docs-site/docs/event-sourcing/providers.md`.

## Storage identity and durable acknowledgement

`IColdEventStore` identifies a stream by the exact tenant partition, aggregate type and aggregate ID. `ColdArchiveBatch` snapshots caller-owned payloads, validates the complete batch and checks version/event identity before providers merge or filter it. Providers must not turn malformed storage into an empty result.

Archival reads raw payloads through `IEventStoreArchiveReader`, with the candidate's explicit tenant and inclusive version ceiling. This is a separate administrative capability from `IEventStoreArchive`: forwarding archive metadata/tombstoning must not implicitly grant payload access. Tiered startup resolves archive, reader and `IEventStoreArchiveScanner` capabilities once from the captured hot store through `GetService`; denied readers and independent replacement registrations fail startup. Custom providers and restricting decorators must implement or mediate the new capability before enabling tiered archival. The read preserves archive/erasure markers and does not fence concurrent erasure.

Candidate selection implements `MaxAge OR Position < MaxPosition`; `RetainRecentCount` always protects the newest N rows by aggregate version and can operate alone. Each candidate ends before the first ineligible payload, erasure marker or unexplained null payload. Archived null-payload markers may be traversed but contribute no pending work. The legacy one-shot discovery query excludes zero-work groups before applying its candidate limit. This repairs the former aggregate-version comparison, AND combination and whole-stream retention rejection; callers relying on those behaviors must reassess policy settings. The former snapshot prerequisite is retired: retained cold history and its durable prefix receipt supply replay safety, while snapshots remain optional. Hosted archival uses bounded stream-key pages through `IEventStoreArchiveScanner`. A round captures policy values, an age cutoff instant and a committed-position horizon. The horizon bounds which stream identities enter that round; policy evaluation still reads each selected stream's entire current history. `BatchSize` bounds examined stream identities, including streams with no eligible work. Database keyset comparison and ordering use the same tenant/ID/type expressions and collations. The next continuation follows the last examined key, not the last successful candidate.

Successful pages with a continuation are processed immediately. The configured archive interval applies before a new round and after a page-fetch failure, rather than between every page. Large rounds can produce sustained database and cold-storage activity; the page size is not a rate limit or a bound on database I/O.

The worker accepts the next continuation after attempting every candidate, even when individual candidates fail. Empty candidate pages can still advance. Fetch failure or cancellation retains the previous continuation, so a partially processed page may repeat safely. Policy reloads take effect at the next round; changing options does not restart an active round. Continuations belong to a single scanner instance and cannot be persisted or passed to another source. With finite valid, stable stream identities and continued successful page retrieval, every horizon stream is eventually examined. This does not guarantee successful cold writes, bounded round duration, or progress across repeated short-lived restarts. Legacy manual discovery remains available but repeated calls have no fairness guarantee. Scanner normalization matches raw reads and tombstoning: null maps to the untenanted sentinel; invalid blank tenant identities fail rather than merge into another stream.

Azure Blob, S3 and GCS default to `ColdArchiveLayout.Legacy`, retaining tenant/ID keys. Full-batch aggregate-type validation prevents another type from being served or merged under that key. Their builders accept `Layout(ColdArchiveLayout.TypedV2)` to select the type-qualified `ColdStorageKey.StreamPath` format. Layout is captured at provider construction; later options changes cannot switch it. Selection does not activate a namespace or migrate any data.

`WriteAsync` returns a durable contiguous prefix starting at version zero. For example, a persisted set `{0, 1, 5}` acknowledges only `1`. An acknowledged upload containing only a suffix still returns `-1`. Empty input returns `-1`; an identical retry returns the existing contiguous prefix. Preserve records above a gap and merge by membership rather than discarding versions below the previous maximum. Conditional-write retries must reread and revalidate the competing archive before merging again.

## Typed archive activation and retained migration history

`IColdEventStoreMigration` is an optional administrative capability of the captured cloud cold-store instance. Do not resolve a second provider or unwrap decorators to obtain it. Ordinary tiered startup does not require this capability. A tenant-confined decorator must deny namespace activation unless it has namespace-wide authorization or exclusively owns that namespace.

`ColdArchiveMigrationCoordinator` requires an external operator fence that drains **all** legacy operations and retries before activation; old binaries must stay fenced afterward. The namespace marker is not a lock: a legacy writer could check absence, pause, and resume its upload after activation. A guard cannot stop that already-running write.

Activation conditionally creates and verifies an immutable `layout-v1.json` marker, scoped to the container or exact bucket prefix. Every typed operation requires a valid marker. Every Legacy operation rejects any marker or per-slot receipt presence, including malformed records. Empty writes still validate storage before returning `-1`. No negative marker cache is permitted. The namespace marker also prevents a fresh typed stream without a legacy source or receipt from appearing absent after a Legacy restart.

For each occupied legacy slot, migration reads the complete source, validates one nonempty unambiguous stream, creates the typed copy without overwriting a conflict, rechecks the source revision and bytes, and publishes an immutable receipt. A receipt binds the namespace, keys, identities, original revision and SHA-256 digest. Source archives, typed archives, receipts and markers must remain retained; bucket/container recreation and external replacement violate the protocol. The digest detects drift, not an attacker able to rewrite all retained evidence.

Typed reads verify the receipt, retained source and complete original baseline before returning data. Later appends and sparse gap filling are allowed only while that baseline remains intact. An occupied legacy slot without a completed receipt fails, including filtered reads and existence checks; absence at the typed key is not migration evidence. An activated namespace can accept a fresh typed stream where no legacy slot exists, or another aggregate type at the same tenant/ID after the occupied slot's migration receipt and retained baseline validate. Conditional writes use the exact revision returned by this validation and repeat the entire validation on every conflict retry. Duplicate-only acknowledgements cannot bypass it.

A failed operation may have persisted a marker, copy or receipt. Retry validates that state and resumes. Activation is namespace-wide but does not prove all streams migrated; a crash after activation can block both Legacy access and unmigrated typed access until migration resumes. Deleting markers or sources is not rollback and is not cold-data erasure support.

Provider boundaries:

- Azure uses the captured container and same-download ETag.
- S3 binds typed migration identity through the captured client's endpoint resolver. Unsupported discovery prevents typed operations/migration, while Legacy presence guards remain usable. Access-point ARNs and multi-region aliases are rejected by this binding path. Routing must remain fixed to one strongly consistent namespace; changing endpoint/address style can invalidate existing identity even when an operator considers two URLs equivalent.
- GCS pins downloads to the observed generation and independently checks stored size and CRC32C before strict decoding. Typed operations require raw compressed bytes. Framework-created clients use `GcsRawStorageClientBuilder`; supplied clients are never modified. Legacy metadata guards do not initialize that raw adapter. Missing-object classification verifies bucket metadata, requiring permission to read it; unavailable or unauthorized buckets fail closed. The store disposes framework-created clients once, while supplied instances remain caller-owned and DI-created factory results remain owned by DI.

## Archival retains the hot row

`EventArchiveService` reads candidates and events from the captured raw hot store. After the cold write succeeds, it calls `TombstoneArchivedEventsUpToVersionAsync` only through the confirmed prefix and candidate boundary. The row retains its event ID, tenant, aggregate identity, version and global position. Its payload is cleared and `ArchivedAt` is set. Archive markers preserve global membership; they are not erasure markers. The service refuses erased events in the selected candidate range and unexplained missing payloads.

`TieredEventStoreDecorator` handles aggregate reads. `TieredGlobalStreamQuery` handles the registered SQL Server/PostgreSQL global feeds. Global hydration preserves the selected page's membership and ordering; it never adds cold-only events. SQL Server's contiguous-prefix filtering occurs before hydration, so an inaccessible archive above a withheld gap cannot block delivery of the visible prefix.

## Freshness and erasure races

`ArchivedEventRevalidator` checks each selected archive marker against `IEventStoreAuthoritativeReader` after fetching cold data. The reader observes committed state during that call, independently of an ambient transaction, old snapshot, cache or replica. The source must be the same primary and durable history as the selected event. Engine role checks reject inappropriate sources but do not prove arbitrary connection-factory routing.

An authoritative `$erased` result returns a normalized event with null payload and metadata. Otherwise the stable locator and cold identity must agree. Missing authoritative state or conflicting stable identity fails the read. A non-erased result additionally requires a matching cold payload; an erased result is normalized before lazy cold-batch validation. Cancellation and cold fetch or parse errors still fail the read. A legacy cold global position of zero is treated as unknown; conflicting nonzero positions are rejected.

Cold data may be cached within a page by the complete tenant/type/ID tuple. The fresh-state check is still performed for each event. A global read publishes its result only after the entire selected page succeeds. A later failure cannot hand a consumer a partial page to checkpoint.

These checks are per-event observations. They do not create a page-wide transaction or prevent an erasure from committing after its observation. They also do not remove cold objects. Keep the tiered erasure guard: enabling event-store erasure while the composed tiered store denies `IEventStoreErasure` fails host startup. An external retention policy does not enable this unsupported combination. Distinguish read suppression from destruction of retained data.

## Dependency-injection composition

`UseTieredStorage` captures the configured hot and cold instances once, wraps keyed `"default"`, and validates the final singleton composition before starting the archive worker. Archive operations resolve the raw hot chain's `IEventStoreArchive` capability through `GetService`, preserving decorator mediation or denial. They must not unwrap a chain to bypass that decision.

SQL Server and PostgreSQL bind their global query and hot store to the same private source identity. Global hydration rejects a replacement hot store that does not preserve that binding. The composition receipt is an internal identity token, not a connection or a proof that an arbitrary custom decorator preserves reads. Registrations must be frozen once the service provider is built.

SQL Server's legacy borrowed-connection factory does not qualify for authoritative reads. Its explicitly owned-primary factory path and connection-string path can create fresh owned connections. PostgreSQL uses the selected data source. Global authoritative readers carry explicit per-row tenant locators; they must not mutate the ambient caller tenant to hydrate an estate-wide page.

## Consumers and regression evidence

Projection hosts, rebuilds and materialized views distinguish positive erasure from an unresolved archive. They must not advance progress past a non-erased event with no usable payload. The materialized-view processor calls builder members through the typed interface, preserving default and explicit implementations. Its per-event persistence uses `StoredEvent.GlobalPosition`; aggregate version is a different coordinate even when a final batch checkpoint happens to mask the difference.

The regression layers have different purposes:

- `TieredGlobalStreamQueryShould` tests page validation, identity, cache scope and per-event revalidation.
- `TieredGlobalProviderWiringShould` tests provider binding, capability denial and composition before database access.
- `TieredGlobalStreamProviderIntegrationShould` uses real SQL Server/PostgreSQL and Azure Blob through Azurite to verify restoration and erasure committed after old bytes were fetched.
- `HydratedProjectionRebuildShould`, `HydratedProjectionHostsShould` and `HydratedMaterializedViewShould` pass real hydration through consumers, checking persisted values, unchanged progress on failure and observable refresh failure.

Hosted tests record checkpoint-attempt snapshots and assert after the host stops. An assertion thrown inside a fake persistence callback can otherwise be swallowed by the host's retry path. Materialized-view failure tests reject every mutation method, not merely the configured fake calls. These tests establish their scoped behavior; they do not certify every provider deployment, failover scenario or exactly-once arrangement.
