// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Excalibur.EventSourcing.Abstractions;

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

	/// <summary>
	/// Initializes a new instance of the <see cref="EventStoreErasureContributor"/> class.
	/// </summary>
	/// <param name="eventStoreErasure">The event store erasure interface for tombstoning events.</param>
	/// <param name="mapping">The mapping service that resolves data subject IDs to aggregate IDs.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="snapshotStore">Optional snapshot store for deleting associated snapshots.</param>
	public EventStoreErasureContributor(
		IEventStoreErasure eventStoreErasure,
		IAggregateDataSubjectMapping mapping,
		ILogger<EventStoreErasureContributor> logger,
		ISnapshotStore? snapshotStore = null)
	{
		_eventStoreErasure = eventStoreErasure ?? throw new ArgumentNullException(nameof(eventStoreErasure));
		_mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_snapshotStore = snapshotStore;
	}

	/// <inheritdoc/>
	public string Name => "EventStore";

	/// <inheritdoc/>
	public async Task<ErasureContributorResult> EraseAsync(
		ErasureContributorContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		LogErasureStarting(context.RequestId, context.DataSubjectIdHash);

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

		var totalErased = 0;
		var errors = new List<string>();

		foreach (var reference in aggregateReferences)
		{
			try
			{
				// Check if already erased
				var alreadyErased = await _eventStoreErasure.IsErasedAsync(
					reference.AggregateId,
					reference.AggregateType,
					cancellationToken).ConfigureAwait(false);

				if (alreadyErased)
				{
					LogAggregateAlreadyErased(reference.AggregateId, reference.AggregateType, context.RequestId);
					continue;
				}

				// Erase events (tombstone)
				var erasedCount = await _eventStoreErasure.EraseEventsAsync(
					reference.AggregateId,
					reference.AggregateType,
					context.RequestId,
					cancellationToken).ConfigureAwait(false);

				totalErased += erasedCount;

				// Delete snapshots if snapshot store is available
				if (_snapshotStore is not null)
				{
					await _snapshotStore.DeleteSnapshotsAsync(
						reference.AggregateId,
						reference.AggregateType,
						cancellationToken).ConfigureAwait(false);

					LogSnapshotsDeleted(reference.AggregateId, reference.AggregateType, context.RequestId);
				}

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
			return ErasureContributorResult.Failed(
				$"Partial erasure: {totalErased} events erased, {errors.Count} failures. First error: {errors[0]}");
		}

		LogErasureCompleted(context.RequestId, totalErased, aggregateReferences.Count);
		return ErasureContributorResult.Succeeded(totalErased);
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
