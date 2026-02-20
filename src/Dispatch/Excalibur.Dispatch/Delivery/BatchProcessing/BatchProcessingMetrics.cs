// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Microsoft.ApplicationInsights;

namespace Excalibur.Dispatch.Delivery.BatchProcessing;

/// <summary>
/// Provides metrics collection for batch processing operations.
/// </summary>
public sealed class BatchProcessingMetrics : IDisposable
{
	private readonly Meter _meter;
	private readonly TelemetryClient? _telemetryClient;

	/// <summary>
	/// Counters.
	/// </summary>
	private readonly Counter<long> _messagesProcessedCounter;

	private readonly Counter<long> _batchesProcessedCounter;
	private readonly Counter<long> _failedMessagesCounter;

	/// <summary>
	/// Histograms.
	/// </summary>
	private readonly Histogram<double> _batchSizeHistogram;

	private readonly Histogram<double> _batchDurationHistogram;
	private readonly Histogram<double> _throughputHistogram;

	// Gauges - used by meter callbacks for observability
	// R0.8: Remove unread private members - these are used by meter callbacks
#pragma warning disable IDE0052
	private readonly ObservableGauge<int> _currentBatchSizeGauge;
	private readonly ObservableGauge<double> _successRateGauge;
#pragma warning restore IDE0052

	private int _currentBatchSize;
	private double _currentSuccessRate;

	/// <summary>
	/// Initializes a new instance of the <see cref="BatchProcessingMetrics" /> class.
	/// </summary>
	/// <param name="meterName"> The name for the metrics meter. </param>
	/// <param name="telemetryClient"> Optional Application Insights telemetry client. </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by this class and disposed in Dispose()")]
	public BatchProcessingMetrics(string meterName, TelemetryClient? telemetryClient = null)
		: this(meterFactory: null, meterName, telemetryClient)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="BatchProcessingMetrics" /> class using an <see cref="IMeterFactory"/> for DI-managed meter lifecycle.
	/// </summary>
	/// <param name="meterFactory"> Optional meter factory for DI-managed meter lifecycle. If null, creates an unmanaged meter. </param>
	/// <param name="meterName"> The name for the metrics meter. </param>
	/// <param name="telemetryClient"> Optional Application Insights telemetry client. </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by IMeterFactory or this class and disposed in Dispose()")]
	public BatchProcessingMetrics(IMeterFactory? meterFactory, string meterName, TelemetryClient? telemetryClient = null)
	{
		_meter = meterFactory?.Create(meterName) ?? new Meter(meterName, "1.0.0");
		_telemetryClient = telemetryClient;

		// Initialize counters
		_messagesProcessedCounter = _meter.CreateCounter<long>(
			"dispatch.batch.messages.processed",
			"messages",
			"Total number of messages processed in batches");

		_batchesProcessedCounter = _meter.CreateCounter<long>(
			"dispatch.batch.batches.processed",
			"batches",
			"Total number of batches processed");

		_failedMessagesCounter = _meter.CreateCounter<long>(
			"dispatch.batch.messages.failed",
			"messages",
			"Total number of messages that failed processing");

		// Initialize histograms
		_batchSizeHistogram = _meter.CreateHistogram<double>(
			"dispatch.batch.size",
			"messages",
			"Distribution of batch sizes");

		_batchDurationHistogram = _meter.CreateHistogram<double>(
			"dispatch.batch.processing.duration",
			"seconds",
			"Time taken to process batches");

		_throughputHistogram = _meter.CreateHistogram<double>(
			"dispatch.batch.throughput",
			"messages/second",
			"Messages processed per second");

		// Initialize gauges
		_currentBatchSizeGauge = _meter.CreateObservableGauge(
			"dispatch.batch.current_size",
			() => _currentBatchSize,
			"messages",
			"Current batch size being used");

		_successRateGauge = _meter.CreateObservableGauge(
			"dispatch.batch.success_rate",
			() => _currentSuccessRate,
			"ratio",
			"Current success rate for batch processing");
	}

	/// <summary>
	/// Records metrics for a completed batch.
	/// </summary>
	public void RecordBatchCompleted(
		int batchSize,
		int successfulCount,
		int failedCount,
		TimeSpan duration,
		Dictionary<string, object?>? tags = null)
	{
		var totalProcessed = successfulCount + failedCount;
		var successRate = totalProcessed > 0 ? (double)successfulCount / totalProcessed : 0;
		var throughput = duration.TotalSeconds > 0 ? totalProcessed / duration.TotalSeconds : 0;

		// Update counters
		var tagList = tags?.Select(static kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value)).ToArray()
					  ?? [];
		_messagesProcessedCounter.Add(successfulCount, tagList);
		_failedMessagesCounter.Add(failedCount, tagList);
		_batchesProcessedCounter.Add(1, tagList);

		// Update histograms
		_batchSizeHistogram.Record(batchSize, tagList);
		_batchDurationHistogram.Record(duration.TotalSeconds, tagList);
		_throughputHistogram.Record(throughput, tagList);

		// Update current values for gauges
		_currentBatchSize = batchSize;
		_currentSuccessRate = successRate;

		// Send to Application Insights if available
		_telemetryClient?.TrackMetric("BatchProcessing.BatchSize", batchSize);
		_telemetryClient?.TrackMetric("BatchProcessing.SuccessRate", successRate);
		_telemetryClient?.TrackMetric("BatchProcessing.Throughput", throughput);
		_telemetryClient?.TrackMetric("BatchProcessing.Duration", duration.TotalMilliseconds);

		if (tags != null)
		{
			var properties = tags.ToDictionary(
				static kvp => kvp.Key,
				static kvp => kvp.Value?.ToString() ?? string.Empty, StringComparer.Ordinal);

			_telemetryClient?.TrackEvent("BatchProcessingCompleted", properties);
		}
	}

	/// <summary>
	/// Records a batch processing failure.
	/// </summary>
	public void RecordBatchFailure(Exception exception, Dictionary<string, object?>? tags = null) =>
		_telemetryClient?.TrackException(exception, tags?.ToDictionary(
			static kvp => kvp.Key,
			static kvp => kvp.Value?.ToString() ?? string.Empty));

	/// <summary>
	/// Disposes of the metrics resources.
	/// </summary>
	public void Dispose() => _meter?.Dispose();
}
