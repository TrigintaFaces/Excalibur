---
title: Restoring Inline with durable ordered processing
sidebar_label: Inline projection migration
description: Use Inline in one host or several while retaining positioned writes, shared Elasticsearch clients, and durable recovery.
---

# Inline projection migration

`.Inline()` is available again for mutable and immutable projections. It completes read models through
the committed global position before a successful save returns and automatically registers background
recovery. An API, a Jobs application, or both can run the same configuration independently.

```csharp
es.UseSqlServer(sql => sql.ConnectionString(connectionString));
es.AddProjection<OrderSummary>(p => p.Inline()
    .When<OrderPlaced>((view, e) => view.Total += e.Amount));
```

Register the projection store as usual. With connection-string SQL Server and options-based
Elasticsearch, no separate projection processor, checkpoint registration, or subscription scope is
needed. SQL checkpoint tables still need deployment through the provider migrations. For a non-default
SQL schema, deploy the checkpoint tables in that same schema; the shipped script defaults to `dbo`.

## What changed underneath

The old Inline path applied only the locally saved events with no global position. Concurrent saves
could lose a fold, and a partially updated row could become impossible to advance safely. The restored
path reads the complete ordered prefix, folds each affected document once per page, writes state and
position conditionally, and advances the checkpoint only after the configured visibility barrier.
It shares this machinery with Async but has a separate cursor, so an unrelated Async handler does not
run inside an Inline save.

The Elasticsearch client remains a keyed singleton, shared by scoped stores. The historical per-save
client creation bug exhausted connection pools and could surface as a canceled PUT with no HTTP
response. Restoring Inline does not restore that lifetime. Cancellation alone still does not identify
its cause; inspect the underlying transport and workload when diagnosing an incident.

## Migrate an existing destination deliberately

- Keep a positioned destination only when its projection definition and physical schema are compatible. Inline starts its own
  checkpoint and skips document prefixes already recorded there. First startup may scan historical
  events while establishing the new cursor.
- SQL Server and PostgreSQL projection tables created by earlier alphas with incompatible key widths
  or tenant collation need fresh provisioning and replay. The current tenant column is limited to 64 characters; SQL Server
  also uses a 350-character projection ID and binary tenant collation. The shipped `CREATE` scripts
  and schema generators do not alter an existing table. Stop all projection writers, provision a new
  destination with the current schema, and rebuild it from the durable event stream before cutover.
  Preserve the event store; do not clear its events or retry previously committed commands.
- Rebuild legacy numberless or unplaceable rows before enabling processing. Do not assign them a
  guessed position or retry the original command to repair them.
- Stop old processors before changing a projection from Async to Inline. Changing the workset under an
  existing subscription name is rejected. Use a new subscription name/revision and a deliberate
  destination cutover if the projection semantics change. A new cursor alone cannot repair changed
  projection semantics in an existing document.
- Cooperating API and Jobs hosts must register the same Inline projection set, revision, source, and
  destination configuration. Different endpoint lists fail closed even if operators know they reach
  one cluster. A rebuilt/replaced database or cluster requires a coordinated checkpoint reset/cutover;
  endpoint names do not identify physical data incarnations.

Inline uses `Inline:` plus `GlobalStreamProjectionOptions.ProjectionName` as its checkpoint name.
For custom event readers, externally supplied Elasticsearch clients, or tenant routers, register the
source-affine durable checkpoint store and configure a shared `SubscriptionScope` explicitly. Do not
replace the provider's reader independently of its checkpoint binding.

## Destination unavailable after commit

Check `SaveResult.Projections`. `ProjectionFreshness.BoundExpired` means the events are committed and the
read model is behind; the same `SaveResult` carries the version and the session token. Nothing is thrown,
so there is no exception to catch and no retry policy to disarm.
Return a committed/pending response with a read URL and session token if appropriate for your API.
Do not ask the caller to resubmit the command. Recovery resumes automatically in a running host or on
restart; `IProjectionSessionReader` waits on the Inline cursor for an Inline projection.

The foreground wait is bounded by `EventNotificationOptions.PostCommitTimeout`. Caller cancellation
cannot cancel the post-commit work. A provider that ignores cancellation must still eventually finish;
the framework retains its operation scope and lease until then. Poison handlers need repair rather
than unlimited automatic command retries. Notification handlers retain their independent failure policy.

For Elasticsearch search-after-save, configure `Index.EnsureSearchVisibilityOnProjectionCheckpoint`.
A successful document PUT alone does not promise that an Elasticsearch search can see the document.

## Keep Async when it fits

`.Async()` remains a separate option. Use `EnableProjectionProcessing()` for a background host, or
`AddProjectionProcessor()` for an application that explicitly drives processing. Both may live in a
single project; a second project is never required. Async saves return a session token without waiting
for projection completion, and token-aware reads can wait when the caller needs read-your-writes.

## AOT

Use explicit generic registrations, explicit aggregate event application, and generated event JSON
metadata. Projection event registrations also bind typed notification dispatch; the registered event
path needs no runtime generic construction. Assembly scanning and unregistered runtime event types
retain their existing dynamic-code requirements. Provider serialization must be configured separately;
the native orchestration probe does not certify SQL Server or Elasticsearch client AOT support.

For the SQL Server → Elasticsearch path, configure the same generated event metadata with
`AddAotEventSerializer(context)` and `UseSqlServer(sql => sql.EventTypeInfoResolver(context))`, and set
`ElasticSearchProjectionStoreOptions.SourceTypeInfoResolver` to generated read-model metadata. These
are distinct serialization boundaries. Positioned document envelopes receive framework metadata
automatically, and the Elasticsearch client remains shared.

The real-provider native harness is `tests/harnesses/SqlElasticInlineAotProbe`. Its diagnostic executable
exercises real SQL/Elasticsearch services, but strict native publishing still reports dependency
diagnostics. **Do not treat this path as release-certified for native AOT until that strict gate passes.**
The probe README records the tested platform/authentication and the remaining blockers.
