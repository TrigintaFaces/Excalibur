// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Metrics;
using Excalibur.Dispatch.Transport.GooglePubSub;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Default implementation of Google Pub/Sub metrics collection.
/// </summary>
public sealed class GooglePubSubMetrics : IGooglePubSubMetrics, IDisposable
{
	private readonly Meter _meter;
	private readonly Counter<long> _messagesEnqueued;
	private readonly Counter<long> _messagesDequeued;
	private readonly Counter<long> _messagesProcessed;
	private readonly Counter<long> _messagesFailed;
	private readonly Counter<long> _batchesCreated;
	private readonly Counter<long> _batchesCompleted;
	private readonly Counter<long> _connectionsCreated;
	private readonly Counter<long> _connectionsClosed;
	private readonly System.Diagnostics.Metrics.Histogram<double> _queueTime;
	private readonly System.Diagnostics.Metrics.Histogram<double> _processingTime;
	private readonly System.Diagnostics.Metrics.Histogram<long> _batchSize;
	private readonly System.Diagnostics.Metrics.Histogram<double> _batchDuration;
	private readonly ObservableGauge<int> _flowControlPermits;
	private readonly ObservableGauge<long> _flowControlBytes;
	private readonly RateCounter _enqueuedCount;
	private readonly RateCounter _processedCount;
	private readonly RateCounter _failedCount;
	private readonly ValueHistogram _queueTimeHistogram;
	private readonly ValueHistogram _processingTimeHistogram;
	private int _lastPermits;
	private long _lastBytes;

	/// <summary>
	/// Initializes a new instance of the <see cref="GooglePubSubMetrics" /> class.
	/// </summary>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by this class and disposed in Dispose()")]
	public GooglePubSubMetrics()
		: this(meterFactory: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="GooglePubSubMetrics" /> class using an <see cref="IMeterFactory"/> for DI-managed meter lifecycle.
	/// </summary>
	/// <param name="meterFactory"> Optional meter factory for DI-managed meter lifecycle. If null, creates an unmanaged meter. </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by IMeterFactory or this class and disposed in Dispose()")]
	public GooglePubSubMetrics(IMeterFactory? meterFactory)
	{
		_meter = meterFactory?.Create(GooglePubSubTelemetryConstants.MeterName) ?? new Meter(GooglePubSubTelemetryConstants.MeterName, GooglePubSubTelemetryConstants.Version);

		// Create counters
		_messagesEnqueued = _meter.CreateCounter<long>("pubsub.messages.enqueued", "messages", "Messages enqueued for processing");
		_messagesDequeued = _meter.CreateCounter<long>("pubsub.messages.dequeued", "messages", "Messages dequeued for processing");
		_messagesProcessed = _meter.CreateCounter<long>("pubsub.messages.processed", "messages", "Messages successfully processed");
		_messagesFailed = _meter.CreateCounter<long>("pubsub.messages.failed", "messages", "Messages that failed processing");
		_batchesCreated = _meter.CreateCounter<long>("pubsub.batches.created", "batches", "Batches created");
		_batchesCompleted = _meter.CreateCounter<long>("pubsub.batches.completed", "batches", "Batches completed");
		_connectionsCreated = _meter.CreateCounter<long>("pubsub.connections.created", "connections", "Connections created");
		_connectionsClosed = _meter.CreateCounter<long>("pubsub.connections.closed", "connections", "Connections closed");

		// Create histograms
		_queueTime = _meter.CreateHistogram<double>("pubsub.message.queue_time", "milliseconds", "Time messages spend in queue");
		_processingTime = _meter.CreateHistogram<double>("pubsub.message.processing_time", "milliseconds", "Message processing time");
		_batchSize = _meter.CreateHistogram<long>("pubsub.batch.size", "messages", "Batch sizes");
		_batchDuration = _meter.CreateHistogram<double>("pubsub.batch.duration", "milliseconds", "Batch processing duration");

		// Create observable gauges
		_flowControlPermits = _meter.CreateObservableGauge("pubsub.flow_control.permits", () => _lastPermits, "permits",
			"Available flow control permits");
		_flowControlBytes =
			_meter.CreateObservableGauge("pubsub.flow_control.bytes", () => _lastBytes, "bytes", "Available flow control bytes");

		// High-performance counters for hot paths
		_enqueuedCount = new RateCounter();
		_processedCount = new RateCounter();
		_failedCount = new RateCounter();
		_queueTimeHistogram = new ValueHistogram();
		_processingTimeHistogram = new ValueHistogram();
	}

	/// <inheritdoc />
	public void MessageEnqueued()
	{
		_ = _enqueuedCount.Increment();
		_messagesEnqueued.Add(1);
	}

	/// <inheritdoc />
	public void MessageDequeued(TimeSpan queueTime)
	{
		var milliseconds = queueTime.TotalMilliseconds;
		_queueTimeHistogram.Record((long)milliseconds);
		_messagesDequeued.Add(1);
		_queueTime.Record(milliseconds);
	}

	/// <inheritdoc />
	public void MessageProcessed(TimeSpan duration)
	{
		var milliseconds = duration.TotalMilliseconds;
		_ = _processedCount.Increment();
		_processingTimeHistogram.Record((long)milliseconds);
		_messagesProcessed.Add(1);
		_processingTime.Record(milliseconds);
	}

	/// <inheritdoc />
	public void MessageFailed()
	{
		_ = _failedCount.Increment();
		_messagesFailed.Add(1);
	}

	/// <inheritdoc />
	public void BatchCreated(int size)
	{
		_batchesCreated.Add(1);
		_batchSize.Record(size);
	}

	/// <inheritdoc />
	public void BatchCompleted(int size, TimeSpan duration)
	{
		_batchesCompleted.Add(1);
		_batchSize.Record(size);
		_batchDuration.Record(duration.TotalMilliseconds);
	}

	/// <inheritdoc />
	public void ConnectionCreated() => _connectionsCreated.Add(1);

	/// <inheritdoc />
	public void ConnectionClosed() => _connectionsClosed.Add(1);

	/// <inheritdoc />
	public void RecordFlowControl(int permits, int bytes)
	{
		_lastPermits = permits;
		_lastBytes = bytes;
	}

	/// <summary>
	/// Gets performance statistics.
	/// </summary>
	/// <returns> Performance statistics snapshot. </returns>
	public PerformanceStatistics GetStatistics()
	{
		var queueSnapshot = _queueTimeHistogram.GetSnapshot();
		var processingSnapshot = _processingTimeHistogram.GetSnapshot();

		return new PerformanceStatistics
		{
			MessagesEnqueued = _enqueuedCount.Value,
			MessagesProcessed = _processedCount.Value,
			MessagesFailed = _failedCount.Value,
			AverageQueueTime = TimeSpan.FromMilliseconds(queueSnapshot.Mean),
			P95QueueTime = TimeSpan.FromMilliseconds(queueSnapshot.P95),
			AverageProcessingTime = TimeSpan.FromMilliseconds(processingSnapshot.Mean),
			P95ProcessingTime = TimeSpan.FromMilliseconds(processingSnapshot.P95),
		};
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_meter.Dispose();
		GC.SuppressFinalize(this);
	}
}
