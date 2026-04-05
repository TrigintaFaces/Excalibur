// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Gauge-based context flow metrics for recording current-state measurements
/// such as preservation success, active context counts, lineage depth, and summaries.
/// </summary>
public interface IContextFlowGaugeMetrics
{
	/// <summary>
	/// Records successful context preservation.
	/// </summary>
	/// <param name="stage"> The pipeline stage where preservation was verified. </param>
	void RecordContextPreservationSuccess(string stage);

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
