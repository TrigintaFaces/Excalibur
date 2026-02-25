// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Observability.Diagnostics;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Metrics collection component for context flow observability, tracking context field preservation rate, mutation frequency, size metrics,
/// and cross-boundary success rate.
/// </summary>
public sealed class ContextFlowMetrics : IContextFlowMetrics, IDisposable
{
	private readonly Meter _meter;

	private readonly TagCardinalityGuard _fieldNameGuard = new();
	private Counter<long> _contextSnapshotCounter = null!;

	private Counter<long> _contextMutationCounter = null!;
	private Counter<long> _contextErrorCounter = null!;
	private Counter<long> _contextValidationFailureCounter = null!;
	private Counter<long> _crossBoundaryTransitionCounter = null!;
	private Counter<long> _contextPreservationSuccessCounter = null!;
	private Counter<long> _contextFieldLossCounter = null!;
	private Counter<long> _contextSizeThresholdExceededCounter = null!;

	private Histogram<double> _contextSizeHistogram = null!;

	private Histogram<double> _contextFieldCountHistogram = null!;
	private Histogram<double> _pipelineStageLatencyHistogram = null!;
	private Histogram<double> _contextSerializationLatencyHistogram = null!;
	private Histogram<double> _contextDeserializationLatencyHistogram = null!;

	private long _totalContextsProcessed;

	private long _contextsPreservedSuccessfully;
	private long _activeContexts;
	private long _maxLineageDepth;

	/// <summary>
	/// Initializes a new instance of the <see cref="ContextFlowMetrics" /> class.
	/// </summary>
	/// <param name="options"> Configuration options for context observability. </param>
	public ContextFlowMetrics(IOptions<ContextObservabilityOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_meter = new Meter(ContextObservabilityTelemetryConstants.MeterName, "1.0.0");

		InitializeCounters();
		InitializeHistograms();
		InitializeGauges();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ContextFlowMetrics" /> class using an <see cref="IMeterFactory"/>.
	/// </summary>
	/// <param name="meterFactory"> The meter factory for DI-managed meter lifecycle. </param>
	/// <param name="options"> Configuration options for context observability. </param>
	public ContextFlowMetrics(IMeterFactory meterFactory, IOptions<ContextObservabilityOptions> options)
	{
		ArgumentNullException.ThrowIfNull(meterFactory);
		ArgumentNullException.ThrowIfNull(options);
		_meter = meterFactory.Create(ContextObservabilityTelemetryConstants.MeterName);

		InitializeCounters();
		InitializeHistograms();
		InitializeGauges();
	}

	/// <summary>
	/// Records a context snapshot event.
	/// </summary>
	/// <param name="stage"> The pipeline stage where the snapshot was taken. </param>
	/// <param name="fieldCount"> Number of fields in the context. </param>
	/// <param name="sizeBytes"> Size of the context in bytes. </param>
	public void RecordContextSnapshot(string stage, int fieldCount, int sizeBytes)
	{
		var tags = new TagList { { "stage", stage } };

		_contextSnapshotCounter.Add(1, tags);
		_contextFieldCountHistogram.Record(fieldCount, tags);
		_contextSizeHistogram.Record(sizeBytes, tags);

		Interlocked.Increment(ref _totalContextsProcessed);
	}

	/// <summary>
	/// Records a context mutation event.
	/// </summary>
	/// <param name="changeType"> Type of change detected. </param>
	/// <param name="fieldName"> Name of the field that changed. </param>
	/// <param name="stage"> Pipeline stage where the mutation occurred. </param>
	public void RecordContextMutation(ContextChangeType changeType, string fieldName, string stage)
	{
		ArgumentNullException.ThrowIfNull(fieldName);
		ArgumentNullException.ThrowIfNull(stage);

		var tags = new TagList
		{
			{ "change_type", changeType.ToString() },
			{ "field", _fieldNameGuard.Guard(TruncateFieldName(fieldName)) },
			{ "stage", stage }
		};

		_contextMutationCounter.Add(1, tags);

		if (changeType == ContextChangeType.Removed)
		{
			_contextFieldLossCounter.Add(1, tags);
		}
	}

	/// <summary>
	/// Records a context error event.
	/// </summary>
	/// <param name="errorType"> Type of error that occurred. </param>
	/// <param name="stage"> Pipeline stage where the error occurred. </param>
	public void RecordContextError(string errorType, string stage)
	{
		var tags = new TagList { { "error_type", errorType }, { "stage", stage } };

		_contextErrorCounter.Add(1, tags);
	}

	/// <summary>
	/// Records a context validation failure.
	/// </summary>
	/// <param name="failureReason"> Reason for the validation failure. </param>
	public void RecordContextValidationFailure(string failureReason)
	{
		ArgumentNullException.ThrowIfNull(failureReason);

		var tags = new TagList { { "reason", TruncateFieldName(failureReason) } };

		_contextValidationFailureCounter.Add(1, tags);
	}

	/// <summary>
	/// Records a cross-boundary transition event.
	/// </summary>
	/// <param name="serviceBoundary"> The service boundary identifier. </param>
	/// <param name="contextPreserved"> Whether context was preserved across the boundary. </param>
	public void RecordCrossBoundaryTransition(string serviceBoundary, bool contextPreserved)
	{
		var tags = new TagList { { "service", serviceBoundary }, { "preserved", contextPreserved.ToString() } };

		_crossBoundaryTransitionCounter.Add(1, tags);

		if (contextPreserved)
		{
			Interlocked.Increment(ref _contextsPreservedSuccessfully);
		}
	}

	/// <summary>
	/// Records successful context preservation.
	/// </summary>
	/// <param name="stage"> The pipeline stage. </param>
	public void RecordContextPreservationSuccess(string stage)
	{
		var tags = new TagList { { "stage", stage } };

		_contextPreservationSuccessCounter.Add(1, tags);

		Interlocked.Increment(ref _contextsPreservedSuccessfully);
	}

	/// <summary>
	/// Records when context size exceeds threshold.
	/// </summary>
	/// <param name="stage"> The pipeline stage. </param>
	/// <param name="sizeBytes"> The actual size in bytes. </param>
	public void RecordContextSizeThresholdExceeded(string stage, int sizeBytes)
	{
		var tags = new TagList { { "stage", stage } };

		_contextSizeThresholdExceededCounter.Add(1, tags);
		_contextSizeHistogram.Record(sizeBytes, tags);
	}

	/// <summary>
	/// Records context size metrics.
	/// </summary>
	/// <param name="stage"> The pipeline stage. </param>
	/// <param name="sizeBytes"> Size in bytes. </param>
	public void RecordContextSize(string stage, int sizeBytes)
	{
		var tags = new TagList { { "stage", stage } };

		_contextSizeHistogram.Record(sizeBytes, tags);
	}

	/// <summary>
	/// Records pipeline stage latency.
	/// </summary>
	/// <param name="stage"> The pipeline stage. </param>
	/// <param name="latencyMs"> Latency in milliseconds. </param>
	public void RecordPipelineStageLatency(string stage, long latencyMs)
	{
		var tags = new TagList { { "stage", stage } };

		_pipelineStageLatencyHistogram.Record(latencyMs, tags);
	}

	/// <summary>
	/// Records context serialization latency.
	/// </summary>
	/// <param name="operationType"> Type of serialization operation. </param>
	/// <param name="latencyMs"> Latency in milliseconds. </param>
	public void RecordSerializationLatency(string operationType, long latencyMs)
	{
		var tags = new TagList { { "operation", operationType } };

		_contextSerializationLatencyHistogram.Record(latencyMs, tags);
	}

	/// <summary>
	/// Records context deserialization latency.
	/// </summary>
	/// <param name="operationType"> Type of deserialization operation. </param>
	/// <param name="latencyMs"> Latency in milliseconds. </param>
	public void RecordDeserializationLatency(string operationType, long latencyMs)
	{
		var tags = new TagList { { "operation", operationType } };

		_contextDeserializationLatencyHistogram.Record(latencyMs, tags);
	}

	/// <summary>
	/// Updates the count of active contexts.
	/// </summary>
	/// <param name="delta"> Change in active context count (positive or negative). </param>
	public void UpdateActiveContextCount(int delta)
	{
		var newValue = Interlocked.Add(ref _activeContexts, delta);
		if (newValue < 0)
		{
			// CAS loop to floor at zero
			Interlocked.CompareExchange(ref _activeContexts, 0, newValue);
		}
	}

	/// <summary>
	/// Updates the maximum observed lineage depth.
	/// </summary>
	/// <param name="depth"> The observed lineage depth. </param>
	public void UpdateLineageDepth(int depth)
	{
		// Lock-free max update via CAS loop
		long currentMax;
		do
		{
			currentMax = Interlocked.Read(ref _maxLineageDepth);
			if (depth <= currentMax)
			{
				return;
			}
		} while (Interlocked.CompareExchange(ref _maxLineageDepth, depth, currentMax) != currentMax);
	}

	/// <summary>
	/// Gets current metrics summary.
	/// </summary>
	/// <returns> A summary of current metrics. </returns>
	public ContextMetricsSummary GetMetricsSummary()
	{
		var total = Interlocked.Read(ref _totalContextsProcessed);
		var preserved = Interlocked.Read(ref _contextsPreservedSuccessfully);
		var active = Interlocked.Read(ref _activeContexts);
		var maxDepth = Interlocked.Read(ref _maxLineageDepth);

		return new ContextMetricsSummary
		{
			TotalContextsProcessed = total,
			ContextsPreservedSuccessfully = preserved,
			PreservationRate = total == 0 ? 1.0 : (double)preserved / total,
			ActiveContexts = active,
			MaxLineageDepth = maxDepth,
			Timestamp = DateTimeOffset.UtcNow,
		};
	}

	/// <summary>
	/// Disposes the metrics resources.
	/// </summary>
	public void Dispose() => _meter?.Dispose();

	private static string TruncateFieldName(string fieldName)
	{
		// Truncate field names to prevent high cardinality issues
		const int maxLength = 50;
		if (fieldName.Length <= maxLength)
		{
			return fieldName;
		}

		return $"{fieldName.AsSpan(0, maxLength)}...";
	}

	private void InitializeCounters()
	{
		_contextSnapshotCounter = _meter.CreateCounter<long>(
			"dispatch.context.flow.snapshots",
			"snapshots",
			"Total number of context snapshots taken");

		_contextMutationCounter = _meter.CreateCounter<long>(
			"dispatch.context.flow.mutations",
			"mutations",
			"Total number of context mutations detected");

		_contextErrorCounter = _meter.CreateCounter<long>(
			"dispatch.context.flow.errors",
			"errors",
			"Total number of context flow errors");

		_contextValidationFailureCounter = _meter.CreateCounter<long>(
			"dispatch.context.flow.validation_failures",
			"failures",
			"Total number of context validation failures");

		_crossBoundaryTransitionCounter = _meter.CreateCounter<long>(
			"dispatch.context.flow.cross_boundary_transitions",
			"transitions",
			"Total number of cross-boundary transitions");

		_contextPreservationSuccessCounter = _meter.CreateCounter<long>(
			"dispatch.context.flow.preservation_success",
			"successes",
			"Total number of successful context preservations");

		_contextFieldLossCounter = _meter.CreateCounter<long>(
			"dispatch.context.flow.field_loss",
			"losses",
			"Total number of context field losses detected");

		_contextSizeThresholdExceededCounter = _meter.CreateCounter<long>(
			"dispatch.context.flow.size_threshold_exceeded",
			"exceeds",
			"Number of times context size exceeded threshold");
	}

	private void InitializeHistograms()
	{
		_contextSizeHistogram = _meter.CreateHistogram<double>(
			"dispatch.context.flow.size_bytes",
			"bytes",
			"Distribution of context sizes in bytes");

		_contextFieldCountHistogram = _meter.CreateHistogram<double>(
			"dispatch.context.flow.field_count",
			"fields",
			"Distribution of context field counts");

		_pipelineStageLatencyHistogram = _meter.CreateHistogram<double>(
			"dispatch.context.flow.stage_latency_ms",
			"milliseconds",
			"Latency of pipeline stages in milliseconds");

		_contextSerializationLatencyHistogram = _meter.CreateHistogram<double>(
			"dispatch.context.flow.serialization_latency_ms",
			"milliseconds",
			"Context serialization latency in milliseconds");

		_contextDeserializationLatencyHistogram = _meter.CreateHistogram<double>(
			"dispatch.context.flow.deserialization_latency_ms",
			"milliseconds",
			"Context deserialization latency in milliseconds");
	}

	private void InitializeGauges()
	{
		_ = _meter.CreateObservableGauge(
			"dispatch.context.flow.preservation_rate",
			CalculatePreservationRate,
			"ratio",
			"Current context preservation rate (0-1)");

		_ = _meter.CreateObservableGauge(
			"dispatch.context.flow.active_contexts",
			() => _activeContexts,
			"contexts",
			"Number of currently active contexts");

		_ = _meter.CreateObservableGauge(
			"dispatch.context.flow.lineage_depth",
			() => _maxLineageDepth,
			"depth",
			"Maximum observed context lineage depth");
	}

	private double CalculatePreservationRate()
	{
		var total = Interlocked.Read(ref _totalContextsProcessed);
		if (total == 0)
		{
			return 1.0;
		}

		return (double)Interlocked.Read(ref _contextsPreservedSuccessfully) / total;
	}
}
