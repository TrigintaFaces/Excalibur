# Excalibur.Data.ElasticSearch — projection error persistence

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
`ElasticSearch/Projections/ProjectionErrorPersistenceRetryShould`. Each is mutant-proven: the named
arm goes RED under a one-token change to the property it defends, and nothing else does.

| arm | what it RED-detects |
|---|---|
| `RecoverDroppedAcknowledgementWithinSameCreateBudget` | a severed acknowledgement treated as settled — covers **both** shapes, a reset before any status and a status whose body never arrives |
| `ReplayACreateOnlyUntilElasticsearchSettlesIt` | a status wrongly added to or removed from the settled set, with the settled rows as the liveness half so a stored record cannot loop |
| `SettleA408NeitherWay` | `408` treated as a verdict |
| `ReplayABulkEnvelopeItCouldNotInterpret` | an uninterpretable bulk envelope treated as settled |
| `ReplayABulkItemItCouldNotInterpret` | a per-item `5xx`, `408`, or absent status counted a permanent failure |
| `ReplayAnIncompleteBulkResponseBecauseAbsenceIsNotEvidence` | a response listing fewer items than were sent treated as terminal |
| `RefuseMalformedBulkAuthority` | the converse — an unattributable response wrongly replayed |
| `CountAConflictAsStoredBecauseOnlyThisExecutionMintsTheId` | a conflict counted as a failure rather than as storage |
| `ReplayAThrownFailureWithinTheBudgetThenPropagateIt` | a throw classified by exception type, and an unbounded budget |

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
