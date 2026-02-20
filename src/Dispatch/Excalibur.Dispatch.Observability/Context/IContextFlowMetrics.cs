// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Interface for context flow metrics operations.
/// </summary>
public interface IContextFlowMetrics
{
	/// <summary>
	/// Records a context snapshot event.
	/// </summary>
	/// <param name="stage"> The pipeline stage where the snapshot occurred. </param>
	/// <param name="fieldCount"> The number of fields present in the context. </param>
	/// <param name="sizeBytes"> The size of the context payload in bytes. </param>
	void RecordContextSnapshot(string stage, int fieldCount, int sizeBytes);

	/// <summary>
	/// Records a context mutation event.
	/// </summary>
	/// <param name="changeType"> The type of change applied to the context. </param>
	/// <param name="fieldName"> The name of the field that changed. </param>
	/// <param name="stage"> The pipeline stage where the change occurred. </param>
	void RecordContextMutation(ContextChangeType changeType, string fieldName, string stage);

	/// <summary>
	/// Records a context error event.
	/// </summary>
	/// <param name="errorType"> The classification of the error. </param>
	/// <param name="stage"> The pipeline stage at which the error happened. </param>
	void RecordContextError(string errorType, string stage);

	/// <summary>
	/// Records a context validation failure.
	/// </summary>
	/// <param name="failureReason"> The reason validation failed. </param>
	void RecordContextValidationFailure(string failureReason);

	/// <summary>
	/// Records a cross-boundary transition.
	/// </summary>
	/// <param name="serviceBoundary"> The boundary that was crossed. </param>
	/// <param name="contextPreserved"> Whether the context was preserved across the boundary. </param>
	void RecordCrossBoundaryTransition(string serviceBoundary, bool contextPreserved);

	/// <summary>
	/// Records successful context preservation.
	/// </summary>
	/// <param name="stage"> The pipeline stage where preservation was verified. </param>
	void RecordContextPreservationSuccess(string stage);

	/// <summary>
	/// Records when context size exceeds threshold.
	/// </summary>
	/// <param name="stage"> The pipeline stage where the threshold was exceeded. </param>
	/// <param name="sizeBytes"> The measured size in bytes. </param>
	void RecordContextSizeThresholdExceeded(string stage, int sizeBytes);

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
	/// Updates active context count.
	/// </summary>
	/// <param name="delta"> The delta applied to the active count. </param>
	void UpdateActiveContextCount(int delta);

	/// <summary>
	/// Updates maximum lineage depth.
	/// </summary>
	/// <param name="depth"> The lineage depth to record. </param>
	void UpdateLineageDepth(int depth);

	/// <summary>
	/// Gets current metrics summary.
	/// </summary>
	/// <returns> The current metrics snapshot. </returns>
	ContextMetricsSummary GetMetricsSummary();
}
