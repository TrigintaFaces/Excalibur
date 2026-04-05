// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Histogram-based context flow metrics for recording distribution measurements
/// such as context sizes, pipeline latencies, and serialization timings.
/// </summary>
public interface IContextFlowHistogramMetrics
{
	/// <summary>
	/// Records context size metrics.
	/// </summary>
	/// <param name="stage"> The pipeline stage being measured. </param>
	/// <param name="sizeBytes"> The size of the context payload in bytes. </param>
	void RecordContextSize(string stage, int sizeBytes);

	/// <summary>
	/// Records pipeline stage latency.
	/// </summary>
	/// <param name="stage"> The pipeline stage being measured. </param>
	/// <param name="latencyMs"> The latency in milliseconds. </param>
	void RecordPipelineStageLatency(string stage, long latencyMs);

	/// <summary>
	/// Records serialization latency.
	/// </summary>
	/// <param name="operationType"> The type of serialization operation. </param>
	/// <param name="latencyMs"> The latency in milliseconds. </param>
	void RecordSerializationLatency(string operationType, long latencyMs);

	/// <summary>
	/// Records deserialization latency.
	/// </summary>
	/// <param name="operationType"> The type of deserialization operation. </param>
	/// <param name="latencyMs"> The latency in milliseconds. </param>
	void RecordDeserializationLatency(string operationType, long latencyMs);

	/// <summary>
	/// Records when context size exceeds threshold.
	/// </summary>
	/// <param name="stage"> The pipeline stage where the threshold was exceeded. </param>
	/// <param name="sizeBytes"> The measured size in bytes. </param>
	void RecordContextSizeThresholdExceeded(string stage, int sizeBytes);
}
