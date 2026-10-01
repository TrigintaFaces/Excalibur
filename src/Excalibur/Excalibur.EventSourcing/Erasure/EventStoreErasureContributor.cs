// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Erasure;

/// <summary>
/// Integrates GDPR erasure with event sourcing by tombstoning events and deleting snapshots
/// for erased aggregates.
/// </summary>
/// <remarks>
/// <para>
/// This contributor is invoked by <see cref="IErasureService"/> during erasure execution.
/// It delegates to <see cref="IEventStoreErasure"/> for event tombstoning and
/// <see cref="ISnapshotStore"/> for snapshot deletion.
/// </para>
/// <para>
/// The contributor requires an <see cref="IAggregateDataSubjectMapping"/> to resolve
/// which aggregate IDs belong to a data subject. Without this mapping, the contributor
/// cannot determine which aggregates to erase.
/// </para>
/// </remarks>
public sealed partial class EventStoreErasureContributor : IErasureContributor
{
	private readonly IEventStoreErasure _eventStoreErasure;
	private readonly ISnapshotStore? _snapshotStore;
	private readonly IAggregateDataSubjectMapping _mapping;
	private readonly ILogger<EventStoreErasureContributor> _logger;
	private readonly IReadOnlySet<DataStoreKind> _coveredStoreKinds;
	private readonly IServiceProvider? _serviceProvider;
	private readonly IErasureRetentionRegistry? _retentions;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventStoreErasureContributor"/> class.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>There is one constructor, and the reason is that the alternatives were unsafe by default.</b> Two
	/// earlier overloads omitted the retention registry, so a consumer taking the shortest one got a
	/// completed erasure, a certificate saying <c>Completed</c>, and a legally-required record destroyed,
	/// with no signal at any tier. Documenting a fail-open does not make it fail closed, so they are gone
	/// rather than deprecated.
	/// </para>
	/// <para>
	/// A deployment that declares no retention passes <see langword="null"/> for
	/// <paramref name="retentions"/>. That is correct, and it is now a state the caller chooses rather than
	/// one they fall into by picking the overload with fewer parameters.
	/// </para>
	/// </remarks>
	/// <param name="eventStoreErasure">The event store erasure interface for tombstoning events.</param>
	/// <param name="mapping">The mapping service that resolves data subject IDs to aggregate IDs.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="snapshotStore">Optional snapshot store for deleting associated snapshots.</param>
	/// <param name="serviceProvider">
	/// Resolves the projection registry and, through each registration's pre-bound clear delegate, the
	/// projection store and recovery service — so an erasure also reaches the read models that already
	/// materialized the subject. Null leaves projections untouched.
	/// </param>
	/// <param name="retentions">
	/// The declared erasure retentions, or <see langword="null"/> when the deployment declares none. An
	/// aggregate type named there is left whole — no tombstone, no snapshot deletion, no read-model clear —
	/// and is reported on the result so the erasure record names it with its basis, justification and period.
	/// </param>
	public EventStoreErasureContributor(
		IEventStoreErasure eventStoreErasure,
		IAggregateDataSubjectMapping mapping,
		ILogger<EventStoreErasureContributor> logger,
		ISnapshotStore? snapshotStore,
		IServiceProvider? serviceProvider,
		IErasureRetentionRegistry? retentions)
	{
		_eventStoreErasure = eventStoreErasure ?? throw new ArgumentNullException(nameof(eventStoreErasure));
		_mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_snapshotStore = snapshotStore;
		_serviceProvider = serviceProvider;
		_retentions = retentions;

		// Declare coverage so the erasure gate can mark event-store (and, when a snapshot store is wired,
		// snapshot) locations as Covered. Snapshot is only declared when this contributor can actually
		// delete snapshots — declaring it without a store would falsely "cover" a store it cannot erase.
		_coveredStoreKinds = _snapshotStore is not null
			? new HashSet<DataStoreKind> { DataStoreKind.EventStore, DataStoreKind.Snapshot }
			: new HashSet<DataStoreKind> { DataStoreKind.EventStore };
	}

	/// <inheritdoc/>
	public string Name => "EventStore";

	/// <inheritdoc/>
	public IReadOnlySet<DataStoreKind> CoveredStoreKinds => _coveredStoreKinds;

	/// <inheritdoc/>
	public async Task<ErasureContributorResult> EraseAsync(
		ErasureContributorContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		LogErasureStarting(context.RequestId, context.DataSubjectIdHash);

		// A SELECTIVE request asks for specific data categories only. This contributor has exactly one
		// action -- tombstone every event of an aggregate -- so it cannot express that restriction, and the
		// two ways of ignoring it are both wrong: tombstoning anyway destroys categories the request did not
		// ask about, and reporting success erases nothing while claiming it did.
		//
		// It refuses instead, and the refusal is safe BECAUSE OF THE ORDER THE SERVICE RUNS THINGS IN. The
		// data subject's own key is queued for destruction unconditionally and destroyed BEFORE any
		// contributor is invoked, so by the time this runs the subject's annotated personal fields are
		// already unrecoverable. This pass is ADDITIONAL destruction, justified only by catching what the
		// key destruction did not reach. Declining it therefore withholds extra destruction; it does not
		// withhold the erasure.
		//
		// Failed rather than Succeeded, so the coverage gate leaves the obligation outstanding and the
		// request cannot report Completed. An operator reading this knows the categories they named were
		// not separately honoured here, rather than discovering it from a certificate that says otherwise.
		if (context.Scope == ErasureScope.Selective)
		{
			return ErasureContributorResult.Failed(
				"This erasure names specific data categories, and event-store erasure is whole-aggregate: it "
				+ "tombstones every event of every aggregate mapped to the data subject and cannot restrict "
				+ "itself to named categories. No event was tombstoned. The data subject's own encryption key "
				+ "was already destroyed before contributors ran, so personal fields annotated for per-subject "
				+ "encryption are unrecoverable regardless of this step. To erase at category granularity, "
				+ "annotate those fields for per-subject encryption so key destruction reaches them, or register "
				+ "a contributor declaring event-store coverage that can honour the category restriction.");
		}

		// Resolve aggregate IDs for this data subject
		var aggregateReferences = await _mapping.GetAggregatesForDataSubjectAsync(
			context.DataSubjectIdHash,
			context.TenantId,
			cancellationToken).ConfigureAwait(false);

		if (aggregateReferences.Count == 0)
		{
			LogNoAggregatesFound(context.RequestId, context.DataSubjectIdHash);
			return ErasureContributorResult.Succeeded(0);
		}

		LogAggregatesResolved(context.RequestId, aggregateReferences.Count);

		// Erasure runs from a background scheduler with no ambient tenant. Establish the data subject's
		// tenant scope explicitly so every event-store call below (IsErased/EraseEvents) applies the tenant
		// discriminator for the correct tenant. The event store reads this ambient tenant per query.
		//
		// A null tenant here does NOT widen the erase across the shard: an unresolved ambient tenant fails
		// closed at TenantScope.FromContext, so a store call would raise TenantRequiredException rather than
		// emit a predicate-less statement. The term is deliberately passed raw rather than folded onto the
		// untenanted sentinel: this one is the erasure REQUEST's tenant, not a value read back off a stored
		// row, so a missing value means "the request named no tenant" -- undecided, not untenanted -- and
		// failing closed is the correct answer to it.
		using var tenantScope = TenantContextHolder.BeginScope(context.TenantId);

		var totalErased = 0;

		// Aggregates this pass actually acted on, as distinct from events it removed. A retry over an
		// already-tombstoned aggregate removes no events and has still acted; a retained aggregate is
		// neither. That distinction is what the discharge claim at the end of this method rests on.
		var aggregatesActedOn = 0;
		var errors = new List<string>();
		var retained = new List<ErasureException>();
		var reportedTypes = new HashSet<string>(StringComparer.Ordinal);

		foreach (var reference in aggregateReferences)
		{
			// RETENTION IS CHECKED HERE, BEFORE ANYTHING IS DESTROYED, and the placement is the whole
			// mechanism rather than a convenience. The three destructive steps below -- the snapshot
			// delete, the tombstone and the read-model clear -- all act on THIS reference, so skipping
			// the reference skips all three together and no ordering argument is needed between them.
			//
			// THE UNIT IS THE AGGREGATE TYPE, WHOLE, and that is a legal fact rather than a
			// simplification. An obligation to keep a record attaches to the RECORD: a statute requiring
			// sales records to be kept does not require the buyer and permit deleting the salesperson,
			// because a partly-erased record is a MUTATED record and a mutated record has no evidentiary
			// value -- which is the entire reason it was being kept. So a retained type is never
			// tombstoned, for anyone, and every data subject named inside it is covered while the
			// obligation is in force.
			//
			// The event store needs no change and is told nothing about any of this. It is asked which
			// aggregate to erase and has no opinion about which ones it should be asked for.
			if (_retentions?.TryGetRetention(
					context.TenantId, reference.AggregateType, out var retention) == true)
			{
				// Reported once per TYPE rather than once per aggregate. The retention is a statement
				// about the type, so N entries for N instances would repeat one fact N times on a signed
				// record without adding a claim.
				//
				// THE REQUESTING SUBJECT IS NAMED IN THE REASON, not left to be inferred from the
				// category. This entry is read by a data subject who asked to be erased and was not, and
				// a justification written about the record ("sales records are kept for six years under
				// the tax code") does not by itself tell them that THEIR personal data is what persists
				// there. Article 15 entitles them to be told, so the disclosure is stated rather than
				// implied, and the basis and period travel beside it on the same entry.
				//
				// THE ENTRY STATES THE ACT THIS PASS PERFORMED, NEVER THE OUTCOME FOR THE DATA. That
				// distinction is the whole correction, and it survived one round of fixing already.
				//
				// It first read "was not erased and lawfully persists there for the period stated". The
				// second half was a legal over-claim and was dropped. "Was not erased" then remained --
				// and it is the SAME defect in fewer words, because it is a claim about the ERASURE
				// rather than about this contributor. The erasure destroyed the data subject's own key
				// handle before any contributor ran, and for fields written BEFORE the retention was
				// declared that handle is the one protecting them. So the record can survive with its
				// personal fields unreadable, and "personal data was not erased" would be false about
				// exactly the subject reading it.
				//
				// What this pass DID establish, and all it establishes: the mapping named this aggregate
				// type for this data subject, and this pass did not tombstone it. Whether the fields
				// inside it still decrypt is a fact about key handles that this seam cannot reach -- the
				// derivation is internal to the compliance package, which does not name this assembly as
				// a friend, and no key provider is injected here. It is checkable one layer up, in the
				// service that stamps the handle onto this entry; it is not checkable here, so here it
				// is not claimed.
				if (reportedTypes.Add(reference.AggregateType))
				{
					retained.Add(new ErasureException
					{
						Basis = retention.Basis,
						DataCategory = reference.AggregateType,
						Reason =
							$"This erasure did not tombstone '{reference.AggregateType}', an aggregate "
							+ "type this data subject's data is mapped to. The record is retained under "
							+ "the obligation stated. "
							+ retention.Justification,

						// THE DECLARED PERIOD IS NOT COPIED, and its absence is the honest claim. The
						// declaration states one duration for every record of this aggregate type, and the
						// instant it runs from is a fact about the individual record -- a warranty term runs
						// from delivery, not from the order that created the aggregate. A duration on a signed
						// certificate with no instant beside it invites the reader to anchor it on the
						// certificate's own date, which restarts the clock at the moment the data subject asked
						// to be erased and extends the retention past the end the law gives it.
						//
						// What the entry does state is the ground, the category, the justification, and (stamped
						// one layer up) the handle whose destruction ends the retention.
					});
				}

				LogAggregateRetained(
					reference.AggregateId,
					reference.AggregateType,
					context.RequestId,
					retention.Justification);

				continue;
			}

			aggregatesActedOn++;

			try
			{
				// Whether the events are ALREADY tombstoned is reported, never acted on. It answers
				// "are the event rows erased", and the invariant this loop owes is "is the erasure
				// COMPLETE" -- a different predicate over a wider state. Skipping the rest of the loop
				// on it is what made an interrupted erasure PERMANENT: the events are tombstoned, so a
				// retry declared the aggregate done and never re-attempted the snapshot or the read
				// models, while the retry's own log line read as success. Every step below is
				// idempotent, so re-running them costs a round trip and buys back the only thing that
				// can finish an erasure that was interrupted.
				var alreadyErased = await _eventStoreErasure.IsErasedAsync(
					reference.AggregateId,
					reference.AggregateType,
					cancellationToken).ConfigureAwait(false);

				if (alreadyErased)
				{
					LogAggregateAlreadyErased(reference.AggregateId, reference.AggregateType, context.RequestId);
				}

				// THE SNAPSHOT GOES FIRST, and the ordering is the correctness argument rather than a
				// preference. Nothing spans these two stores transactionally -- the snapshot store is a
				// separate store and frequently a separate database -- so one of the two orders has to
				// survive a fault between them, and only this one does.
				//
				// Tombstone-then-delete fails UNSAFE. A snapshot at count N over events 0..N-1 makes the
				// aggregate load compute fromVersion = N-1 and fetch ZERO event rows, so the tombstone
				// check never sees a row and the erased sentinel never returns: the load hands back the
				// FULL PRE-ERASURE STATE from the snapshot, silently, for as long as the snapshot lives.
				//
				// Delete-then-tombstone fails SAFE. A fault after this call leaves a not-yet-erased
				// aggregate with no snapshot, which costs a slower rehydrate from its own events and
				// loses nothing: a snapshot is derived state with no independent value. The subject's
				// data is still there to erase, and the retry above now reaches this point again.
				if (_snapshotStore is not null)
				{
					await _snapshotStore.DeleteSnapshotsAsync(
						reference.AggregateId,
						reference.AggregateType,
						cancellationToken).ConfigureAwait(false);

					LogSnapshotsDeleted(reference.AggregateId, reference.AggregateType, context.RequestId);
				}

				// Erase events (tombstone)
				var erasedCount = await _eventStoreErasure.EraseEventsAsync(
					reference.AggregateId,
					reference.AggregateType,
					context.RequestId,
					cancellationToken).ConfigureAwait(false);

				totalErased += erasedCount;

				// PROPAGATE THE ERASURE TO THE READ MODELS, here and not elsewhere, because the ORDERING is
				// the correctness argument rather than a convenience.
				//
				// It must run AFTER the tombstone: a replay invoked before it would fold the live payload
				// straight back into the row it was meant to clear. Being inside this loop makes that
				// ordering structural — the same step that destroyed the payload is the one that notifies —
				// instead of an inferred contributor ordering nobody can see at the call site.
				//
				// The subject's own key was destroyed before any contributor ran, and that is IRRELEVANT to
				// this call rather than a constraint on it: every event of this aggregate now carries a null
				// payload, and both the decrypting store decorator and the replay skip a null payload before
				// any key is consulted. Nothing here needs to read what the key protected.
				await ClearProjectionsForAggregateAsync(reference, errors, cancellationToken).ConfigureAwait(false);

				LogAggregateErased(reference.AggregateId, reference.AggregateType, erasedCount, context.RequestId);
			}
			catch (Exception ex)
			{
				errors.Add($"Failed to erase aggregate {reference.AggregateType}/{reference.AggregateId}: {ex.Message}");
				LogAggregateErasureFailed(reference.AggregateId, reference.AggregateType, context.RequestId, ex);
			}
		}

		if (errors.Count > 0)
		{
			// The retentions travel on the FAILURE path too. What was kept is a fact about the store;
			// whether an unrelated aggregate's read model was briefly unreachable is a fact about the run,
			// and letting the second erase the first would make the partial certificate -- the one a
			// controller reconciles by hand -- the only one that never names what survived.
			return ErasureContributorResult.Failed(
				$"Partial erasure: {totalErased} events erased, {errors.Count} failures. First error: {errors[0]}",
				retained);
		}

		LogErasureCompleted(context.RequestId, totalErased, aggregateReferences.Count);

		// Name the declared pairs this contributor erased. The service already filtered them to the store
		// kinds this contributor covers, so every pair here is one the erasure above acted on. Reporting
		// success WITHOUT naming them discharges nothing, which leaves every registered obligation
		// outstanding and the erasure incompletable.
		//
		// The retentions are carried out alongside them, and that half is not optional. A certificate
		// reporting a clean completion over data deliberately kept is indistinguishable from one over data
		// that was destroyed -- the silence is what makes the retention undetectable from outside, and
		// undetectable is the discriminator that makes a defect of this class catastrophic rather than
		// merely wrong.
		// ACTING ON NO AGGREGATE DISCHARGES NOTHING, and the case is reachable: a subject whose every
		// aggregate is of a retained type. Naming the declared pairs there would assert that this
		// contributor erased the subject's data at each of them -- the one claim a certificate rests on --
		// and it would be false. The sibling case at the top of this method, no aggregates at all, already
		// reports success while discharging nothing for the same reason: silence fails closed.
		//
		// The discriminator is AGGREGATES ACTED ON, never the event count. A retry over an aggregate whose
		// events are already tombstoned erases zero events and has still done the work -- it re-ran the
		// snapshot delete and the read-model clear, which is the whole reason the loop no longer skips it.
		// Keying on the count would report that retry as a failure and leave an interrupted erasure with no
		// way to close.
		return aggregatesActedOn == 0
			? ErasureContributorResult.Failed(
				"No aggregate was erased: every aggregate mapped to this data subject is of a type under a "
				+ "declared erasure retention. The event store still holds the subject lawfully, so this "
				+ "contributor discharges no registered obligation.",
				retained)
			: ErasureContributorResult.Succeeded(totalErased, context.DeclaredLocations, retained);
	}

	/// <summary>
	/// Clears one erased aggregate's contribution from every projection that can be cleared per aggregate.
	/// </summary>
	/// <param name="reference">The aggregate whose events were just tombstoned.</param>
	/// <param name="errors">Accumulates a failure per projection, so one store fault does not hide others.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <remarks>
	/// <para>
	/// A registration carries a clear delegate only when its projection id IS the aggregate id. A keyed
	/// projection has none, because replaying one aggregate cannot produce a correct row for a key many
	/// aggregates feed; those are NAMED on the certificate by the projection gap report, which reads the
	/// same property this does, so the report cannot claim a coverage this loop did not perform.
	/// </para>
	/// <para>
	/// <b>Every registration is attempted and failures accumulate.</b> One projection's store fault must
	/// not leave the others still holding the subject's data, so nothing here throws out of the loop.
	/// </para>
	/// <para>
	/// <b>A failure is recorded rather than swallowed, and it is not the "data is gone" case.</b> After
	/// the tombstone there is no way for this to fail BECAUSE the payload is gone — the replay folds
	/// nothing and writes the empty state, which is precisely the cleared outcome. So a failure here
	/// means a read model may still hold the subject and the framework cannot establish otherwise, which
	/// must reach the certificate as a failure rather than a log line.
	/// </para>
	/// </remarks>
	private async Task ClearProjectionsForAggregateAsync(
		AggregateReference reference,
		List<string> errors,
		CancellationToken cancellationToken)
	{
		// No projection registry means no read model to notify: a host that composed event-store erasure
		// without projections has nothing to clear.
		if (_serviceProvider?.GetService(typeof(IProjectionRegistry)) is not IProjectionRegistry registry)
		{
			return;
		}

		foreach (var registration in registry.GetAll())
		{
			if (registration.ClearForAggregate is null)
			{
				continue;
			}

			try
			{
				await registration.ClearForAggregate(
						_serviceProvider,
						reference.AggregateId,
						reference.AggregateType,
						cancellationToken)
					.ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				errors.Add(
					$"Failed to clear projection '{registration.ProjectionType.Name}' for erased aggregate "
					+ $"{reference.AggregateType}/{reference.AggregateId}: {ex.Message}. That read model may "
					+ "still hold this data subject's material; replay it with "
					+ "IProjectionRecovery.ReapplyAsync for this aggregate.");
			}
		}
	}

	[LoggerMessage(
		Diagnostics.EventSourcingEventId.ErasureContributorStarting,
		LogLevel.Information,
		"Event store erasure starting for request {RequestId}, data subject hash {DataSubjectIdHash}")]
	private partial void LogErasureStarting(Guid requestId, string dataSubjectIdHash);

	[LoggerMessage(
		Diagnostics.EventSourcingEventId.ErasureNoAggregatesFound,
		LogLevel.Information,
		"No aggregates found for data subject hash {DataSubjectIdHash} in erasure request {RequestId}")]
	private partial void LogNoAggregatesFound(Guid requestId, string dataSubjectIdHash);

	[LoggerMessage(
		Diagnostics.EventSourcingEventId.ErasureAggregatesResolved,
		LogLevel.Information,
		"Resolved {AggregateCount} aggregates for erasure request {RequestId}")]
	private partial void LogAggregatesResolved(Guid requestId, int aggregateCount);

	[LoggerMessage(
		Diagnostics.EventSourcingEventId.ErasureAggregateAlreadyErased,
		LogLevel.Debug,
		"Aggregate {AggregateId} ({AggregateType}) already erased, skipping for request {RequestId}")]
	private partial void LogAggregateAlreadyErased(string aggregateId, string aggregateType, Guid requestId);

	[LoggerMessage(
		Diagnostics.EventSourcingEventId.ErasureAggregateRetained,
		LogLevel.Information,
		"Aggregate {AggregateId} ({AggregateType}) left intact for request {RequestId} under a declared "
		+ "retention: {Justification}")]
	private partial void LogAggregateRetained(
		string aggregateId, string aggregateType, Guid requestId, string justification);

	[LoggerMessage(
		Diagnostics.EventSourcingEventId.ErasureAggregateCompleted,
		LogLevel.Information,
		"Erased {EventCount} events for aggregate {AggregateId} ({AggregateType}), request {RequestId}")]
	private partial void LogAggregateErased(string aggregateId, string aggregateType, int eventCount, Guid requestId);

	[LoggerMessage(
		Diagnostics.EventSourcingEventId.ErasureSnapshotsDeleted,
		LogLevel.Information,
		"Deleted snapshots for aggregate {AggregateId} ({AggregateType}), request {RequestId}")]
	private partial void LogSnapshotsDeleted(string aggregateId, string aggregateType, Guid requestId);

	[LoggerMessage(
		Diagnostics.EventSourcingEventId.ErasureAggregateFailed,
		LogLevel.Error,
		"Failed to erase aggregate {AggregateId} ({AggregateType}) for request {RequestId}")]
	private partial void LogAggregateErasureFailed(string aggregateId, string aggregateType, Guid requestId, Exception exception);

	[LoggerMessage(
		Diagnostics.EventSourcingEventId.ErasureContributorCompleted,
		LogLevel.Information,
		"Event store erasure completed for request {RequestId}: {TotalEventsErased} events erased across {AggregateCount} aggregates")]
	private partial void LogErasureCompleted(Guid requestId, int totalEventsErased, int aggregateCount);
}
