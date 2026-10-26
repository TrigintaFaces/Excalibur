# Excalibur.Data.ElasticSearch — projection error persistence

This document carries two guarantees. The first, and the bulk of what follows, is **projection error
persistence**. The second, at the end, is **projection index identity** — narrow, and separate because
it concerns where a projection writes rather than how a failure is recorded.

Scope: the store that records *why a projection failed*. Not the projections themselves, and not the
read path — this document covers the write that captures a failure so somebody can see it later.

---

## Guarantee

**A projection-error record is persisted at-least-once, and a replay cannot duplicate it.**

Stated so it can be falsified:

- A create is **replayed** until Elasticsearch returns an outcome it actually produces for a create.
  The settled set is closed: `201` (stored), `409` (an earlier attempt of this execution stored it), or
  a conclusive client error (`400`, `401`, `403`, `404`, `405`, `413`, `422`).
- **Every other answer is unknown, not settled.** A `200`, any `5xx`, a `408`, a `429`, an absent
  status, a body that could not be read, or an exception of any kind leaves the record's fate unknown
  and the create is replayed, bounded by the configured attempt budget.
- **A replay cannot create a duplicate.** The document id is a GUID minted by this execution, and the
  write is `op_type=create`, so a committed first attempt answers `409` rather than overwriting.
- **A bulk response is interpreted in two independent halves.** If the response talks about records
  that were not sent — an unknown id, an operation that was not issued, the same id twice — it is
  terminal, because that is evidence about the responder and no replay changes who answered. If it is
  merely *silent* about records that were sent, the whole validated set is replayed, because loss omits
  and cannot invent.
- **Caller cancellation is terminal.** The caller's own token decides that, and nothing downstream can
  override it.

What this does **not** guarantee: that the record is persisted at all. If the attempt budget is
exhausted the failure is reported through the handler's logging and the record is not stored. The
guarantee is about never *silently* concluding a write succeeded or failed on no evidence.

---

## How it is achieved

| property | seam |
|---|---|
| the settled set for a create | `Resilience/ProjectionErrorPersistenceRetry.cs` — `IsSettledCreateStatus`, over `IsConclusiveClientError` |
| unknown ⇒ replay, for a create | `Resilience/ProjectionErrorPersistenceRetry.cs` — `IsUnsettledCreateOutcome` |
| unknown ⇒ replay, for a bulk envelope | `Resilience/ProjectionErrorPersistenceRetry.cs` — `IsUnsettledBulkEnvelope`, called only where the response carried no interpretable item list |
| per-item outcome | `Resilience/ProjectionErrorPersistenceRetry.cs` — `ClassifyBulkItem` (`Stored` / `Rejected` / `Unknown`) |
| attribution before selection | `Projections/ProjectionErrorHandler.cs` — the shape check (ids are ours, operation is `create`, no duplicates within the response) runs *before* any replay subset is chosen |
| idempotent identity | `Projections/ProjectionErrorHandler.cs` — `Id = Guid.NewGuid().ToString()`, written with `CreateRequest` / `BulkCreateOperation` |
| a thrown exception is never classified by type | `Resilience/ProjectionErrorPersistenceRetry.cs` — the retry predicate treats any throw as an uninterpreted outcome |

The classification deliberately asks **"did Elasticsearch answer?"** rather than **"does this failure
look transient?"**. The settled set is small, closed, and fixed by Elasticsearch's create contract; the
set of ways a request can fail is neither, which is why an allowlist of transient faults cannot be
completed and must not be reintroduced.

---

## Evidence

Conformance arms in `Excalibur.Data.ElasticSearch.Tests`,
`ProjectionErrorPersistenceRetryShould`
(`tests/unit/Excalibur.Data.ElasticSearch.Tests/ElasticSearch/Projections/`). Each is mutant-proven: the named
arm goes RED under a one-token change to the property it defends, and nothing else does.

| arm | what it RED-detects |
|---|---|
| `ProjectionErrorPersistenceRetryShould.RecoverDroppedAcknowledgementWithinSameCreateBudget` | a severed acknowledgement treated as settled — covers **both** shapes, a reset before any status and a status whose body never arrives |
| `ProjectionErrorPersistenceRetryShould.ReplayACreateOnlyUntilElasticsearchSettlesIt` | a status wrongly added to or removed from the settled set, with the settled rows as the liveness half so a stored record cannot loop |
| `ProjectionErrorPersistenceRetryShould.SettleA408NeitherWay` | `408` treated as a verdict |
| `ProjectionErrorPersistenceRetryShould.ReplayABulkEnvelopeItCouldNotInterpret` | an uninterpretable bulk envelope treated as settled |
| `ProjectionErrorPersistenceRetryShould.ReplayABulkItemItCouldNotInterpret` | a per-item `5xx`, `408`, or absent status counted a permanent failure |
| `ProjectionErrorPersistenceRetryShould.ReplayAnIncompleteBulkResponseBecauseAbsenceIsNotEvidence` | a response listing fewer items than were sent treated as terminal |
| `ProjectionErrorPersistenceRetryShould.RefuseMalformedBulkAuthority` | the converse — an unattributable response wrongly replayed |
| `ProjectionErrorPersistenceRetryShould.CountAConflictAsStoredBecauseOnlyThisExecutionMintsTheId` | a conflict counted as a failure rather than as storage |
| `ProjectionErrorPersistenceRetryShould.ReplayAThrownFailureWithinTheBudgetThenPropagateIt` | a throw classified by exception type, and an unbounded budget |

---

## Consumer obligations

- **Reads of the projection-error store must tolerate a replayed write.** The guarantee is
  at-least-once; a replay is refused by the document id rather than prevented, so a reader sees one
  record per id and never a partial one.
- **The attempt budget is yours to size.** `MaxIndexAttempts` and `MaxBulkAttempts` bound how long an
  indeterminate outcome is pursued. A budget of 1 disables replay entirely and returns this seam to
  reporting a single attempt's result.
- **Do not place a proxy in front of Elasticsearch that synthesises its own status codes.** A `200`
  from an intermediary on a create is treated as unknown — correctly, since Elasticsearch does not
  answer a create that way — and will be replayed.

---

## Known gaps

- **The record can still be lost when the budget is exhausted**, and the only trace is a log line. The
  seam guarantees no silent *conclusion*, not delivery.
- **No property-based coverage.** The arms above are examples chosen by hand; they sample the status
  space rather than generate it. Treat this seam as unproven against inputs nobody thought of.
- **No model of the replay/attribution interaction.** The bulk path's division into attribution and
  completeness is argued and tested, not checked against a state space.
- **The single-create and bulk paths share a classification vocabulary but not a predicate.** They are
  deliberately separate because `200` settles a healthy bulk envelope and is never a valid create
  answer. Nothing structurally prevents a future edit from collapsing them.

---

# Second guarantee — projection index identity

Scope: which Elasticsearch index a registered projection store writes to, and nothing else. Not the
fold, not the cursor, not the error path above.

---

## Guarantee

**No two projection types can write to the same index on the same cluster without being refused at
start-up.**

Stated so it can be falsified:

- An index name resolves to `{IndexPrefix}-{IndexName ?? simple type name}`, lowercased. The default
  uses the projection's **simple** type name, so two types with one simple name in different
  namespaces resolve to one index.
- Their **options buckets do not collide** — those are keyed on the full type name — so no other part
  of the registration path notices.
- A composition in which two distinct projection types resolve to the **same index on the same
  cluster** is **refused before any fold runs**, naming both types and the index.
- **The cluster is part of the identity.** One index name on two clusters is permitted: the stores
  never address the same Elasticsearch, so neither can overwrite the other.
- **The cluster is compared NORMALISED, so one cluster spelled two ways is one cluster.** A trailing
  slash, host case, surrounding whitespace, and the same address supplied through `NodeUri` on one
  projection and `NodeUris` on another all fold together. A base PATH is significant, because a node
  behind a reverse proxy is distinguished that way. An address that will not parse folds by case
  instead of being dropped, so an unparseable pair still GROUPS — where normalisation is uncertain the
  conservative answer is "these might be the same cluster", because separating risks the silent
  overwrite and grouping only risks a refusal the consumer can act on.
- **The credential is not part of the identity.** The same nodes reached under two different
  credentials are one cluster for this purpose, because two stores there overwrite each other
  regardless of who authenticated.
- **A tenant-sharded store writes to the index its projection was REGISTERED with**, prefixed by the
  shard. The per-shard store is built from the registered options, so a distinct index name given to
  keep two read models apart is not undone one shard at a time.
- The same projection type registered more than once is **one claimant**, not a collision.

What this does **not** guarantee: that two projections writing to one index would actually corrupt
each other. That depends on their document ids overlapping. The refusal is unconditional because the
overlap is unknowable at start-up and the failure it prevents is silent.

---

## How it is achieved

| property | seam |
|---|---|
| the refusal | `Projections/ElasticSearchProjectionIndexIdentityValidator.cs` — `Validate()` |
| the identity it keys on | same file — `(DescribeClusterNodes(options), GetIndexName(options, type.Name))` |
| cluster identity without the credential, normalised | `Projections/ElasticSearchProjectionClientSettings.cs` — `DescribeClusterNodes` over `NormaliseNode`, which `DescribeCluster` composes so the two cannot drift |
| registered settings reaching a shard | `Projections/ElasticSearchProjectionStoreOptions.cs` — `CloneForShard`, called by `Sharding/ElasticSearchTenantProjectionStoreResolver.cs` |
| the contract's declared exception | `Projections/ElasticSearchProjectionIndexIdentityValidator.cs` — resolving options is wrapped, so a consumer's options failure is reported as this contract's `InvalidOperationException` rather than escaping as `OptionsValidationException` |
| the index string, derived once | `Projections/ElasticSearchProjectionIndexConvention.cs` — `GetIndexName`, the same call `ElasticSearchProjectionStore`'s constructors make |
| the registered population | `Projections/ElasticSearchProjectionStoreExtensions.cs` — `ProjectionOptionsNameClaims`, populated by `ClaimOptionsName` on every registration path |
| reachable from a host | `Projections/ElasticSearchProjectionStoreExtensions.cs` — `WireIndexIdentityValidation` registers both `IHostedService` and `IStartupPrerequisiteValidator` |

The check runs where the whole registration set is known. A single registration cannot see its future
sibling, and the resolved index depends on options that configuration and per-projection callbacks have
not finished supplying at registration time.

---

## Evidence

`ElasticSearchProjectionIndexIdentityShould`
(`tests/unit/Excalibur.Data.ElasticSearch.Tests/ElasticSearch/Projections/`), 10 arms, safety paired
with liveness throughout.

| arm | what it RED-detects |
|---|---|
| `RefuseTwoProjectionsResolvingToOneIndex` | the refusal absent entirely; asserts the exception type, so a failure for an unrelated reason does not satisfy it |
| `RefuseACollisionWhenAHostStartsItsServices` | the hosted registration dropped while every type stays present — the half-wired case, proven RED by severing that one registration |
| `RefuseOneIndexNameOnOneClusterReachedUnderDifferentCredentials` | the credential folded into the cluster identity, which would separate two stores that do collide |
| `AllowOneIndexNameOnTwoDifferentClusters` | the cluster dropped from the identity, which would refuse a sound multi-cluster composition |
| `RefuseACollisionWhenOneClusterIsSpelledTwoWays` | the cluster key compared RAW — proven RED on a trailing slash and on host case, the silent direction |
| `RefuseACollisionAcrossTheNodeUriAndNodeUrisForms` | the two node-address forms read as two clusters |
| `AllowTwoClustersDistinguishedOnlyByBasePath` | normalisation folding too far, discarding a significant base path |
| `KeyOnTheIndexTheStoreItselfResolves` | the check and the store deriving the index differently — reads both real stores' own resolved index |
| `TellTheConsumerWhatToTypeAndThatTheExistingIndexNeedsRebuilding` | a message that names a property path but no pasteable line, and says nothing about data already interleaved |
| `ShardedProjectionIndexIdentityShould.CarryTheRegisteredIndexNameIntoThePerShardStore` | the registered index name dropped per shard, which re-creates the collision after this check certified the composition |
| `ShardedProjectionIndexIdentityShould.CarryTheOtherRegisteredSettingsIntoThePerShardStore` | the same mechanism dropping the credential, replica count, refresh interval and initialise flag |
| `ShardedProjectionIndexIdentityShould.DropTheRegisteredNodeListSoTheShardAddressIsUsed` | a retained node list outranking the shard's own address |
| `AllowTwoSameNamedProjectionsGivenDistinctIndexNames` | a guard that refuses everything; also resolves both stores through real DI |
| `AllowSameNamedProjectionsSeparatedByPrefix` | a check keyed on the type name rather than the resolved index |
| `AllowOneProjectionTakingTheConventionalDefault` | a guard that refuses the common single-projection case |
| `AllowTheSameProjectionRegisteredTwice` | idempotent registration read as two owners |
| `AllowProjectionsWithDistinctSimpleNames` | over-broad grouping |
| `StartHostedServicesForADistinctlyNamedComposition` | a hosted check that throws on a legitimate composition |

---

## Consumer obligations

- **Give each read model its own index name when two share a simple type name.** Set
  `Index.IndexName`, or separate them with `Index.IndexPrefix`. The refusal names both types.
- **An index name is live coordination state.** Changing one points the store at a different index and
  orphans whatever is in the old one; it is a migration, not a settings change.
- **A host-less consumer must call `ValidateStartupGates()`** after building the provider. A host that
  starts its services reaches the check without it.

---

## Known gaps

- **A registration-supplied client hides its cluster.** When the client comes from a factory rather
  than from node addresses, the node set is unknown and all such stores group together, so the check is
  conservative there: it may refuse two stores that in fact address different clusters. The refusal says
  so explicitly rather than implying the clusters matched, and the remedy is the same explicit index
  name, so it stays actionable.
- **Only REGISTERED projection stores are seen.** The population is the types that went through an
  `Add*` entry point. A consumer who constructs `ElasticSearchProjectionStore<T>` directly, registers
  their own `IProjectionStore<T>`, or points a custom `ElasticRepositoryBase<TDocument>` at a
  projection's index — a pattern this package's own documentation shows — is invisible to the check.
  Deliberate sharing is fine; an accidental collision between two such writers gets no help here.
- **The check runs as a hosted service, so another hosted service registered BEFORE the first
  projection store starts first.** A processor that writes on start could therefore write once before
  the refusal. The host-less `ValidateStartupGates()` path starts no hosted services and is unaffected.
- **Options changed after start-up are not re-checked.** The check reads options once. A consumer who
  mutates an index name through a change token afterwards is outside this guarantee.
- **The refusal is unconditional, not evidence of overlap.** Two projections on one index are refused
  whether or not their document ids would ever have collided.
- **No property-based coverage and no model.** The arms above are examples chosen by hand; they sample
  the composition space rather than generate it. This guarantee is verified at the contract-and-arm
  level only — treat it as unproven against compositions nobody thought of.
