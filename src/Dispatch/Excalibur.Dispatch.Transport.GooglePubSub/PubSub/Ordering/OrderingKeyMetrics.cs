// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Metrics;
using Excalibur.Dispatch.Transport.GooglePubSub;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Provides metrics collection specifically for ordering key operations.
/// </summary>
public sealed class OrderingKeyMetrics : IDisposable
{
	private readonly Meter _meter;
	private readonly Counter<long> _messagesInSequence;
	private readonly Counter<long> _messagesOutOfSequence;
	private readonly Counter<long> _orderingKeysCreated;
	private readonly Counter<long> _orderingKeysFailed;
	private readonly Counter<long> _orderingKeysReset;
	private readonly System.Diagnostics.Metrics.Histogram<long> _sequenceGapSize;
	private readonly ObservableGauge<int> _activeOrderingKeys;
	private readonly ObservableGauge<int> _failedOrderingKeys;
	private readonly RateCounter _inSequenceCount;
	private readonly RateCounter _outOfSequenceCount;
	private readonly ValueHistogram _gapSizeHistogram;
	private int _activeKeys;
	private int _failedKeys;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderingKeyMetrics" /> class.
	/// </summary>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by this class and disposed in Dispose()")]
	public OrderingKeyMetrics()
		: this(meterFactory: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderingKeyMetrics" /> class using an <see cref="IMeterFactory"/> for DI-managed meter lifecycle.
	/// </summary>
	/// <param name="meterFactory"> Optional meter factory for DI-managed meter lifecycle. If null, creates an unmanaged meter. </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by IMeterFactory or this class and disposed in Dispose()")]
	public OrderingKeyMetrics(IMeterFactory? meterFactory)
	{
		_meter = meterFactory?.Create(GooglePubSubTelemetryConstants.MeterName) ?? new Meter(GooglePubSubTelemetryConstants.MeterName, GooglePubSubTelemetryConstants.Version);

		// Create counters
		_messagesInSequence = _meter.CreateCounter<long>(
			"pubsub.ordering.messages_in_sequence",
			"messages",
			"Messages received in sequence");

		_messagesOutOfSequence = _meter.CreateCounter<long>(
			"pubsub.ordering.messages_out_of_sequence",
			"messages",
			"Messages received out of sequence");

		_orderingKeysCreated = _meter.CreateCounter<long>(
			"pubsub.ordering.keys_created",
			"keys",
			"Ordering keys created");

		_orderingKeysFailed = _meter.CreateCounter<long>(
			"pubsub.ordering.keys_failed",
			"keys",
			"Ordering keys that entered failed state");

		_orderingKeysReset = _meter.CreateCounter<long>(
			"pubsub.ordering.keys_reset",
			"keys",
			"Failed ordering keys that were reset");

		// Create histogram
		_sequenceGapSize = _meter.CreateHistogram<long>(
			"pubsub.ordering.sequence_gap_size",
			"messages",
			"Size of sequence gaps for out-of-order messages");

		// Create observable gauges
		_activeOrderingKeys = _meter.CreateObservableGauge(
			"pubsub.ordering.active_keys",
			() => _activeKeys,
			"keys",
			"Number of active ordering keys");

		_failedOrderingKeys = _meter.CreateObservableGauge(
			"pubsub.ordering.failed_keys",
			() => _failedKeys,
			"keys",
			"Number of failed ordering keys");

		// High-performance counters
		_inSequenceCount = new RateCounter();
		_outOfSequenceCount = new RateCounter();
		_gapSizeHistogram = new ValueHistogram();
	}

	/// <summary>
	/// Records a message received in sequence.
	/// </summary>
	public void RecordInSequenceMessage()
	{
		_ = _inSequenceCount.Increment();
		_messagesInSequence.Add(1);
	}

	/// <summary>
	/// Records a message received out of sequence.
	/// </summary>
	/// <param name="gapSize"> The size of the sequence gap. </param>
	public void RecordOutOfSequenceMessage(long gapSize)
	{
		_ = _outOfSequenceCount.Increment();
		_messagesOutOfSequence.Add(1);

		if (gapSize > 0)
		{
			_gapSizeHistogram.Record(gapSize);
			_sequenceGapSize.Record(gapSize);
		}
	}

	/// <summary>
	/// Records creation of a new ordering key.
	/// </summary>
	public void RecordOrderingKeyCreated() => _orderingKeysCreated.Add(1);

	/// <summary>
	/// Records an ordering key entering failed state.
	/// </summary>
	public void RecordOrderingKeyFailed() => _orderingKeysFailed.Add(1);

	/// <summary>
	/// Records an ordering key being reset.
	/// </summary>
	public void RecordOrderingKeyReset() => _orderingKeysReset.Add(1);

	/// <summary>
	/// Updates the count of active and failed ordering keys.
	/// </summary>
	/// <param name="activeKeys"> Number of active keys. </param>
	/// <param name="failedKeys"> Number of failed keys. </param>
	public void UpdateKeyStateCounts(int activeKeys, int failedKeys)
	{
		_activeKeys = activeKeys;
		_failedKeys = failedKeys;
	}

	/// <summary>
	/// Gets ordering performance statistics.
	/// </summary>
	/// <returns> Performance statistics. </returns>
	public OrderingPerformanceStatistics GetStatistics()
	{
		var gapSnapshot = _gapSizeHistogram.GetSnapshot();

		return new OrderingPerformanceStatistics
		{
			TotalInSequence = _inSequenceCount.Value,
			TotalOutOfSequence = _outOfSequenceCount.Value,
			SequenceRatio = _inSequenceCount.Value > 0
				? (double)_inSequenceCount.Value / (_inSequenceCount.Value + _outOfSequenceCount.Value) * 100
				: 100.0,
			AverageGapSize = gapSnapshot.Mean,
			P95GapSize = gapSnapshot.P95,
			MaxGapSize = gapSnapshot.Max,
		};
	}

	/// <inheritdoc />
	public void Dispose() => _meter?.Dispose();
}
