// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Composite interface for all context flow metrics operations.
/// </summary>
/// <remarks>
/// This interface composes three focused sub-interfaces following ISP:
/// <list type="bullet">
///   <item><description><see cref="IContextFlowCounterMetrics"/> - Counter-based event metrics (snapshots, mutations, errors, validation, boundaries).</description></item>
///   <item><description><see cref="IContextFlowHistogramMetrics"/> - Histogram-based distribution metrics (sizes, latencies, thresholds).</description></item>
///   <item><description><see cref="IContextFlowGaugeMetrics"/> - Gauge-based current-state metrics (preservation, active counts, lineage, summaries).</description></item>
/// </list>
/// Consumers that need only a subset of metrics operations should depend on the specific sub-interface.
/// </remarks>
public interface IContextFlowMetrics : IContextFlowCounterMetrics, IContextFlowHistogramMetrics, IContextFlowGaugeMetrics
{
}
