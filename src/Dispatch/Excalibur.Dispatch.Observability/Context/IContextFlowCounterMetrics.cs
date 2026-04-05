// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Counter-based context flow metrics for recording discrete events
/// such as snapshots, mutations, errors, validation failures, and boundary transitions.
/// </summary>
public interface IContextFlowCounterMetrics
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
}
