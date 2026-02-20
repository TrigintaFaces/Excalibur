// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Decorators;
using Excalibur.EventSourcing.Observability;

namespace Excalibur.EventSourcing.Diagnostics;

/// <summary>
/// Decorates an <see cref="IEventStore"/> with dedicated throughput metrics:
/// <c>events_appended</c> (counter), <c>events_loaded</c> (counter),
/// <c>append_duration</c> (histogram), and <c>load_duration</c> (histogram).
/// </summary>
/// <remarks>
/// <para>
/// This decorator complements <see cref="TelemetryEventStore"/> by providing fine-grained
/// throughput counters that track the actual number of events (not just operations).
/// For example, a single <c>AppendAsync</c> call that appends 5 events will increment
/// <c>events_appended</c> by 5.
/// </para>
/// <para>
/// Can be composed with <see cref="TelemetryEventStore"/> via decorator chaining.
/// </para>
/// </remarks>
public sealed class EventStoreThroughputMetrics : DelegatingEventStore
{
	private readonly Counter<long> _eventsAppendedCounter;
	private readonly Counter<long> _eventsLoadedCounter;
	private readonly Histogram<double> _appendDurationHistogram;
	private readonly Histogram<double> _loadDurationHistogram;
	private readonly string _providerName;
	private readonly TagCardinalityGuard _aggregateTypeGuard;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventStoreThroughputMetrics"/> class.
	/// </summary>
	/// <param name="inner">The inner event store implementation to decorate.</param>
	/// <param name="meter">The meter for recording metrics.</param>
	/// <param name="providerName">The provider name for tagging (e.g., "sqlserver", "inmemory").</param>
	public EventStoreThroughputMetrics(
		IEventStore inner,
		Meter meter,
		string providerName)
		: base(inner)
	{
		ArgumentNullException.ThrowIfNull(meter);
		_providerName = providerName ?? throw new ArgumentNullException(nameof(providerName));
		_aggregateTypeGuard = new TagCardinalityGuard(maxCardinality: 128);

		_eventsAppendedCounter = meter.CreateCounter<long>(
			EventSourcingMetricNames.EventsAppended,
			"{event}",
			"Total number of events appended to event stores");

		_eventsLoadedCounter = meter.CreateCounter<long>(
			EventSourcingMetricNames.EventsLoaded,
			"{event}",
			"Total number of events loaded from event stores");

		_appendDurationHistogram = meter.CreateHistogram<double>(
			EventSourcingMetricNames.AppendDuration,
			"s",
			"Duration of event store append operations in seconds");

		_loadDurationHistogram = meter.CreateHistogram<double>(
			EventSourcingMetricNames.LoadDuration,
			"s",
			"Duration of event store load operations in seconds");
	}

	/// <inheritdoc />
	public override async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var guardedType = _aggregateTypeGuard.Guard(aggregateType);
		var sw = Stopwatch.StartNew();

		var result = await base.LoadAsync(aggregateId, aggregateType, cancellationToken)
			.ConfigureAwait(false);

		sw.Stop();
		RecordLoad(guardedType, result.Count, sw.Elapsed.TotalSeconds);
		return result;
	}

	/// <inheritdoc />
	public override async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		var guardedType = _aggregateTypeGuard.Guard(aggregateType);
		var sw = Stopwatch.StartNew();

		var result = await base.LoadAsync(aggregateId, aggregateType, fromVersion, cancellationToken)
			.ConfigureAwait(false);

		sw.Stop();
		RecordLoad(guardedType, result.Count, sw.Elapsed.TotalSeconds);
		return result;
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

		// Materialize to count events without consuming the enumerable
		var eventList = events as IReadOnlyCollection<IDomainEvent> ?? events.ToList();
		var sw = Stopwatch.StartNew();

		var result = await base.AppendAsync(aggregateId, aggregateType, eventList, expectedVersion, cancellationToken)
			.ConfigureAwait(false);

		sw.Stop();
		RecordAppend(guardedType, eventList.Count, sw.Elapsed.TotalSeconds);
		return result;
	}

	private void RecordLoad(string guardedType, int eventCount, double durationSeconds)
	{
		var tags = new TagList
		{
			{ EventSourcingTags.Provider, _providerName },
			{ EventSourcingTags.AggregateType, guardedType },
		};

		_eventsLoadedCounter.Add(eventCount, tags);
		_loadDurationHistogram.Record(durationSeconds, tags);
	}

	private void RecordAppend(string guardedType, int eventCount, double durationSeconds)
	{
		var tags = new TagList
		{
			{ EventSourcingTags.Provider, _providerName },
			{ EventSourcingTags.AggregateType, guardedType },
		};

		_eventsAppendedCounter.Add(eventCount, tags);
		_appendDurationHistogram.Record(durationSeconds, tags);
	}
}
