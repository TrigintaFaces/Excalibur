// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport.GooglePubSub;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Collects metrics for batch message receiving operations.
/// </summary>
public sealed class BatchMetricsCollector : IDisposable
{
	private readonly Meter _meter;
	private volatile bool _disposed;
	private readonly Counter<long> _messagesReceived;
	private readonly Counter<long> _bytesReceived;
	private readonly Counter<long> _batchesReceived;
	private readonly Counter<long> _messagesAcknowledged;
	private readonly Histogram<double> _batchReceiveDuration;
	private readonly Histogram<double> _batchAckDuration;
	private readonly Histogram<long> _batchSize;
	private readonly UpDownCounter<int> _activeBatchProcessors;

	private long _totalMessagesReceived;
	private long _totalBytesReceived;
	private long _totalBatchesReceived;

	/// <summary>
	/// Initializes a new instance of the <see cref="BatchMetricsCollector" /> class.
	/// </summary>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by this class and disposed in Dispose()")]
	public BatchMetricsCollector(string meterName = GooglePubSubTelemetryConstants.MeterName)
		: this(meterFactory: null, meterName)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="BatchMetricsCollector" /> class using an <see cref="IMeterFactory"/> for DI-managed meter lifecycle.
	/// </summary>
	/// <param name="meterFactory"> Optional meter factory for DI-managed meter lifecycle. If null, creates an unmanaged meter. </param>
	/// <param name="meterName"> The meter name. Defaults to <see cref="GooglePubSubTelemetryConstants.MeterName"/>. </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by IMeterFactory or this class and disposed in Dispose()")]
	public BatchMetricsCollector(IMeterFactory? meterFactory, string meterName = GooglePubSubTelemetryConstants.MeterName)
	{
		_meter = meterFactory?.Create(meterName) ?? new Meter(meterName, GooglePubSubTelemetryConstants.Version);

		_messagesReceived = _meter.CreateCounter<long>(
			"pubsub.batch.messages.received",
			"messages",
			"Total number of messages received in batches");

		_bytesReceived = _meter.CreateCounter<long>(
			"pubsub.batch.bytes.received",
			"bytes",
			"Total bytes received in batches");

		_batchesReceived = _meter.CreateCounter<long>(
			"pubsub.batch.count",
			"batches",
			"Total number of batches received");

		_messagesAcknowledged = _meter.CreateCounter<long>(
			"pubsub.batch.messages.acknowledged",
			"messages",
			"Total messages acknowledged");

		_batchReceiveDuration = _meter.CreateHistogram<double>(
			"pubsub.batch.receive.duration",
			"milliseconds",
			"Duration of batch receive operations");

		_batchAckDuration = _meter.CreateHistogram<double>(
			"pubsub.batch.ack.duration",
			"milliseconds",
			"Duration of batch acknowledgment operations");

		_batchSize = _meter.CreateHistogram<long>(
			"pubsub.batch.size",
			"messages",
			"Size of received batches");

		_activeBatchProcessors = _meter.CreateUpDownCounter<int>(
			"pubsub.batch.processors.active",
			"processors",
			"Number of active batch processors");

		// Create observable gauges for totals
		_ = _meter.CreateObservableGauge(
			"pubsub.batch.messages.total",
			() => _totalMessagesReceived,
			"messages",
			"Total messages received since startup");

		_ = _meter.CreateObservableGauge(
			"pubsub.batch.bytes.total",
			() => _totalBytesReceived,
			"bytes",
			"Total bytes received since startup");

		_ = _meter.CreateObservableGauge(
			"pubsub.batch.count.total",
			() => _totalBatchesReceived,
			"batches",
			"Total batches received since startup");
	}

	/// <summary>
	/// Records metrics for a received batch.
	/// </summary>
	public void RecordBatchReceived(int messageCount, long totalBytes, double durationMs,
		Dictionary<string, object>? tags = null)
	{
		var tagList = CreateTagList(tags);

		_messagesReceived.Add(messageCount, tagList);
		_bytesReceived.Add(totalBytes, tagList);
		_batchesReceived.Add(1, tagList);
		_batchSize.Record(messageCount, tagList);
		_batchReceiveDuration.Record(durationMs, tagList);

		_totalMessagesReceived += messageCount;
		_totalBytesReceived += totalBytes;
		_totalBatchesReceived++;
	}

	/// <summary>
	/// Records metrics for batch acknowledgment.
	/// </summary>
	public void RecordBatchAcknowledged(int messageCount, double durationMs,
		Dictionary<string, object>? tags = null)
	{
		var tagList = CreateTagList(tags);

		_messagesAcknowledged.Add(messageCount, tagList);
		_batchAckDuration.Record(durationMs, tagList);
	}

	/// <summary>
	/// Increments the active batch processor count.
	/// </summary>
	public void IncrementActiveProcessors(Dictionary<string, object>? tags = null) => _activeBatchProcessors.Add(1, CreateTagList(tags));

	/// <summary>
	/// Decrements the active batch processor count.
	/// </summary>
	public void DecrementActiveProcessors(Dictionary<string, object>? tags = null) => _activeBatchProcessors.Add(-1, CreateTagList(tags));

	/// <summary>
	/// Records a batch processing session.
	/// </summary>
	public IDisposable RecordBatchProcessingSession(Dictionary<string, object>? tags = null)
	{
		IncrementActiveProcessors(tags);
		return new BatchProcessingSession(this, tags);
	}

	/// <summary>
	/// Disposes the metrics collector.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_meter?.Dispose();
		GC.SuppressFinalize(this);
	}

	private static TagList CreateTagList(Dictionary<string, object>? tags)
	{
		var tagList = default(TagList);
		if (tags != null)
		{
			foreach (var kvp in tags)
			{
				tagList.Add(kvp.Key, kvp.Value);
			}
		}

		return tagList;
	}

	/// <summary>
	/// Represents a batch processing session for automatic metric tracking.
	/// </summary>
	private sealed class BatchProcessingSession(BatchMetricsCollector collector, Dictionary<string, object>? tags) : IDisposable
	{
		public void Dispose() => collector.DecrementActiveProcessors(tags);
	}
}
