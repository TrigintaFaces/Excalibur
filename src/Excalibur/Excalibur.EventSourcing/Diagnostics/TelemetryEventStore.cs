// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Diagnostics;
using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Decorators;
using Excalibur.EventSourcing.Observability;

namespace Excalibur.EventSourcing.Diagnostics;

/// <summary>
/// Decorates an <see cref="IEventStore"/> with OpenTelemetry metrics and distributed tracing.
/// Records operation counts and durations for all event store operations.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <see cref="Excalibur.LeaderElection.Diagnostics.TelemetryLeaderElection"/> pattern:
/// a wrapping decorator that instruments an existing <see cref="IEventStore"/> implementation
/// without requiring modification of the underlying provider.
/// </para>
/// <para>
/// Two metrics are recorded:
/// <list type="bullet">
/// <item><description><c>excalibur.eventsourcing.eventstore.operations</c> — Counter of store operations</description></item>
/// <item><description><c>excalibur.eventsourcing.eventstore.duration</c> — Histogram of operation durations in seconds</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class TelemetryEventStore : DelegatingEventStore
{
	private readonly Counter<long> _operationsCounter;
	private readonly Histogram<double> _durationHistogram;
	private readonly ActivitySource _activitySource;
	private readonly string _providerName;
	private readonly TagCardinalityGuard _aggregateTypeGuard;
	private readonly TagCardinalityGuard _aggregateIdGuard;
	private readonly TagCardinalityGuard _exceptionTypeGuard;

	/// <summary>
	/// Initializes a new instance of the <see cref="TelemetryEventStore"/> class.
	/// </summary>
	/// <param name="inner">The inner event store implementation to decorate.</param>
	/// <param name="meter">The meter for recording metrics.</param>
	/// <param name="activitySource">The activity source for distributed tracing.</param>
	/// <param name="providerName">The provider name for tagging (e.g., "sqlserver", "inmemory").</param>
	public TelemetryEventStore(
		IEventStore inner,
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
			EventSourcingMetricNames.EventStoreOperations,
			"{operation}",
			"Number of event store operations");

		_durationHistogram = meter.CreateHistogram<double>(
			EventSourcingMetricNames.EventStoreDuration,
			"s",
			"Duration of event store operations in seconds");
	}

	/// <inheritdoc />
	public override async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var guardedType = _aggregateTypeGuard.Guard(aggregateType);
		var guardedId = _aggregateIdGuard.Guard(aggregateId);
		using var activity = _activitySource.StartActivity(EventSourcingActivities.Load);
		SetActivityTags(activity, guardedId, guardedType);

		var sw = ValueStopwatch.StartNew();
		try
		{
			var result = await base.LoadAsync(aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
			RecordSuccess("load", guardedType, sw);
			activity?.SetTag(EventSourcingTags.EventCount, result.Count);
			return result;
		}
		catch (Exception ex)
		{
			RecordFailure("load", guardedType, sw, activity, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public override async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		var guardedType = _aggregateTypeGuard.Guard(aggregateType);
		var guardedId = _aggregateIdGuard.Guard(aggregateId);
		using var activity = _activitySource.StartActivity(EventSourcingActivities.Load);
		SetActivityTags(activity, guardedId, guardedType);
		activity?.SetTag(EventSourcingTags.FromVersion, fromVersion);

		var sw = ValueStopwatch.StartNew();
		try
		{
			var result = await base.LoadAsync(aggregateId, aggregateType, fromVersion, cancellationToken).ConfigureAwait(false);
			RecordSuccess("load", guardedType, sw);
			activity?.SetTag(EventSourcingTags.EventCount, result.Count);
			return result;
		}
		catch (Exception ex)
		{
			RecordFailure("load", guardedType, sw, activity, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public override async ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		var guardedType = _aggregateTypeGuard.Guard(aggregateType);
		var guardedId = _aggregateIdGuard.Guard(aggregateId);
		using var activity = _activitySource.StartActivity(EventSourcingActivities.Append);
		SetActivityTags(activity, guardedId, guardedType);
		activity?.SetTag(EventSourcingTags.ExpectedVersion, expectedVersion);

		var sw = ValueStopwatch.StartNew();
		try
		{
			var result = await base.AppendAsync(aggregateId, aggregateType, events, expectedVersion, cancellationToken).ConfigureAwait(false);
			RecordSuccess("append", guardedType, sw);
			return result;
		}
		catch (Exception ex)
		{
			RecordFailure("append", guardedType, sw, activity, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public override async ValueTask<IReadOnlyList<StoredEvent>> GetUndispatchedEventsAsync(
		int batchSize,
		CancellationToken cancellationToken)
	{
		using var activity = _activitySource.StartActivity(EventSourcingActivities.GetUndispatched);
		activity?.SetTag(EventSourcingTags.BatchSize, batchSize);
		activity?.SetTag(EventSourcingTags.Provider, _providerName);

		var sw = ValueStopwatch.StartNew();
		try
		{
			var result = await base.GetUndispatchedEventsAsync(batchSize, cancellationToken).ConfigureAwait(false);
			RecordSuccess("get_undispatched", null, sw);
			activity?.SetTag(EventSourcingTags.EventCount, result.Count);
			return result;
		}
		catch (Exception ex)
		{
			RecordFailure("get_undispatched", null, sw, activity, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public override async ValueTask MarkEventAsDispatchedAsync(
		string eventId,
		CancellationToken cancellationToken)
	{
		using var activity = _activitySource.StartActivity(EventSourcingActivities.MarkDispatched);
		// EventId is high-cardinality (unique per event) — not guarded on activity spans
		// because traces benefit from unique identifiers for correlation.
		// Only metric tags must be guarded; EventId is NOT used in metric TagLists.
		activity?.SetTag(EventSourcingTags.EventId, eventId);
		activity?.SetTag(EventSourcingTags.Provider, _providerName);

		var sw = ValueStopwatch.StartNew();
		try
		{
			await base.MarkEventAsDispatchedAsync(eventId, cancellationToken).ConfigureAwait(false);
			RecordSuccess("mark_dispatched", null, sw);
		}
		catch (Exception ex)
		{
			RecordFailure("mark_dispatched", null, sw, activity, ex);
			throw;
		}
	}

	private void RecordSuccess(string operation, string? aggregateType, ValueStopwatch sw)
	{
		var tags = new TagList
		{
			{ EventSourcingTags.Operation, operation },
			{ EventSourcingTags.Provider, _providerName },
			{ EventSourcingTags.OperationResult, EventSourcingTagValues.Success },
		};

		if (aggregateType is not null)
		{
			tags.Add(EventSourcingTags.AggregateType, aggregateType);
		}

		_operationsCounter.Add(1, tags);
		_durationHistogram.Record(sw.Elapsed.TotalSeconds, tags);
	}

	private void RecordFailure(string operation, string? aggregateType, ValueStopwatch sw, Activity? activity, Exception ex)
	{
		var guardedExceptionType = _exceptionTypeGuard.Guard(ex.GetType().FullName);
		activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
		activity?.SetTag(EventSourcingTags.ExceptionType, guardedExceptionType);

		var tags = new TagList
		{
			{ EventSourcingTags.Operation, operation },
			{ EventSourcingTags.Provider, _providerName },
			{ EventSourcingTags.OperationResult, EventSourcingTagValues.Failure },
		};

		if (aggregateType is not null)
		{
			tags.Add(EventSourcingTags.AggregateType, aggregateType);
		}

		_operationsCounter.Add(1, tags);
		_durationHistogram.Record(sw.Elapsed.TotalSeconds, tags);
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
