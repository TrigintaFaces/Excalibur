// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Decorators;
using Excalibur.EventSourcing.Observability;

namespace Excalibur.EventSourcing.Diagnostics;

/// <summary>
/// Decorates an <see cref="ISnapshotStore"/> with OpenTelemetry metrics and distributed tracing.
/// Records operation counts and durations for all snapshot store operations.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <see cref="TelemetryEventStore"/> pattern:
/// a wrapping decorator that instruments an existing <see cref="ISnapshotStore"/> implementation
/// without requiring modification of the underlying provider.
/// </para>
/// <para>
/// Two metrics are recorded:
/// <list type="bullet">
/// <item><description><c>excalibur.eventsourcing.snapshotstore.operations</c> — Counter of store operations</description></item>
/// <item><description><c>excalibur.eventsourcing.snapshotstore.duration</c> — Histogram of operation durations in seconds</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class TelemetrySnapshotStore : DelegatingSnapshotStore
{
	private readonly Counter<long> _operationsCounter;
	private readonly Histogram<double> _durationHistogram;
	private readonly ActivitySource _activitySource;
	private readonly string _providerName;
	private readonly TagCardinalityGuard _aggregateTypeGuard;
	private readonly TagCardinalityGuard _aggregateIdGuard;
	private readonly TagCardinalityGuard _exceptionTypeGuard;

	/// <summary>
	/// Initializes a new instance of the <see cref="TelemetrySnapshotStore"/> class.
	/// </summary>
	/// <param name="inner">The inner snapshot store implementation to decorate.</param>
	/// <param name="meter">The meter for recording metrics.</param>
	/// <param name="activitySource">The activity source for distributed tracing.</param>
	/// <param name="providerName">The provider name for tagging (e.g., "sqlserver", "inmemory").</param>
	public TelemetrySnapshotStore(
		ISnapshotStore inner,
		Meter meter,
		ActivitySource activitySource,
		string providerName)
		: base(inner)
	{
		ArgumentNullException.ThrowIfNull(meter);
		_activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
		_providerName = providerName ?? throw new ArgumentNullException(nameof(providerName));
		_aggregateTypeGuard = new TagCardinalityGuard(maxCardinality: 128);
		_aggregateIdGuard = new TagCardinalityGuard(maxCardinality: 128);
		_exceptionTypeGuard = new TagCardinalityGuard(maxCardinality: 50);

		_operationsCounter = meter.CreateCounter<long>(
			EventSourcingMetricNames.SnapshotStoreOperations,
			"{operation}",
			"Number of snapshot store operations");

		_durationHistogram = meter.CreateHistogram<double>(
			EventSourcingMetricNames.SnapshotStoreDuration,
			"s",
			"Duration of snapshot store operations in seconds");
	}

	/// <inheritdoc />
	public override async ValueTask<ISnapshot?> GetLatestSnapshotAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var guardedType = _aggregateTypeGuard.Guard(aggregateType);
		var guardedId = _aggregateIdGuard.Guard(aggregateId);
		using var activity = _activitySource.StartActivity(EventSourcingActivities.GetSnapshot);
		SetActivityTags(activity, guardedId, guardedType);

		var startTimestamp = Stopwatch.GetTimestamp();
		try
		{
			var result = await base.GetLatestSnapshotAsync(aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
			RecordSuccess("get_snapshot", guardedType, startTimestamp);
			activity?.SetTag(EventSourcingTags.OperationResult,
				result is not null ? EventSourcingTagValues.Success : EventSourcingTagValues.NotFound);
			return result;
		}
		catch (Exception ex)
		{
			RecordFailure("get_snapshot", guardedType, startTimestamp, activity, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public override async ValueTask SaveSnapshotAsync(
		ISnapshot snapshot,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		var guardedType = _aggregateTypeGuard.Guard(snapshot.AggregateType);
		var guardedId = _aggregateIdGuard.Guard(snapshot.AggregateId);
		using var activity = _activitySource.StartActivity(EventSourcingActivities.SaveSnapshot);
		SetActivityTags(activity, guardedId, guardedType);
		activity?.SetTag(EventSourcingTags.Version, snapshot.Version);

		var startTimestamp = Stopwatch.GetTimestamp();
		try
		{
			await base.SaveSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
			RecordSuccess("save_snapshot", guardedType, startTimestamp);
		}
		catch (Exception ex)
		{
			RecordFailure("save_snapshot", guardedType, startTimestamp, activity, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public override async ValueTask DeleteSnapshotsAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var guardedType = _aggregateTypeGuard.Guard(aggregateType);
		var guardedId = _aggregateIdGuard.Guard(aggregateId);
		using var activity = _activitySource.StartActivity(EventSourcingActivities.DeleteSnapshots);
		SetActivityTags(activity, guardedId, guardedType);

		var startTimestamp = Stopwatch.GetTimestamp();
		try
		{
			await base.DeleteSnapshotsAsync(aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
			RecordSuccess("delete_snapshots", guardedType, startTimestamp);
		}
		catch (Exception ex)
		{
			RecordFailure("delete_snapshots", guardedType, startTimestamp, activity, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public override async ValueTask DeleteSnapshotsOlderThanAsync(
		string aggregateId,
		string aggregateType,
		long olderThanVersion,
		CancellationToken cancellationToken)
	{
		var guardedType = _aggregateTypeGuard.Guard(aggregateType);
		var guardedId = _aggregateIdGuard.Guard(aggregateId);
		using var activity = _activitySource.StartActivity(EventSourcingActivities.DeleteSnapshots);
		SetActivityTags(activity, guardedId, guardedType);
		activity?.SetTag(EventSourcingTags.Version, olderThanVersion);

		var startTimestamp = Stopwatch.GetTimestamp();
		try
		{
			await base.DeleteSnapshotsOlderThanAsync(aggregateId, aggregateType, olderThanVersion, cancellationToken).ConfigureAwait(false);
			RecordSuccess("delete_snapshots_older", guardedType, startTimestamp);
		}
		catch (Exception ex)
		{
			RecordFailure("delete_snapshots_older", guardedType, startTimestamp, activity, ex);
			throw;
		}
	}

	private void RecordSuccess(string operation, string guardedType, long startTimestamp)
	{
		var tags = new TagList
		{
			{ EventSourcingTags.Operation, operation },
			{ EventSourcingTags.Provider, _providerName },
			{ EventSourcingTags.AggregateType, guardedType },
			{ EventSourcingTags.OperationResult, EventSourcingTagValues.Success },
		};

		_operationsCounter.Add(1, tags);
		_durationHistogram.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);
	}

	private void RecordFailure(string operation, string guardedType, long startTimestamp, Activity? activity, Exception ex)
	{
		var guardedExceptionType = _exceptionTypeGuard.Guard(ex.GetType().FullName);
		activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
		activity?.SetTag(EventSourcingTags.ExceptionType, guardedExceptionType);

		var tags = new TagList
		{
			{ EventSourcingTags.Operation, operation },
			{ EventSourcingTags.Provider, _providerName },
			{ EventSourcingTags.AggregateType, guardedType },
			{ EventSourcingTags.OperationResult, EventSourcingTagValues.Failure },
		};

		_operationsCounter.Add(1, tags);
		_durationHistogram.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);
	}

	private void SetActivityTags(Activity? activity, string aggregateId, string guardedType)
	{
		if (activity is not null)
		{
			_ = activity.SetTag(EventSourcingTags.AggregateId, aggregateId);
			_ = activity.SetTag(EventSourcingTags.AggregateType, guardedType);
			_ = activity.SetTag(EventSourcingTags.Provider, _providerName);
		}
	}
}
