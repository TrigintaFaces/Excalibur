// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Represents a snapshot of performance metrics at a point in time.
/// </summary>
public sealed record PerformanceSnapshot
{
	/// <summary>
	/// Gets the middleware execution statistics.
	/// </summary>
	/// <value> The middleware metrics keyed by middleware name. </value>
	public required IReadOnlyDictionary<string, ComponentMetrics> MiddlewareMetrics { get; init; }

	/// <summary>
	/// Gets the pipeline execution statistics.
	/// </summary>
	/// <value> The aggregated pipeline metrics captured in the snapshot. </value>
	public required PipelineMetrics PipelineMetrics { get; init; }

	/// <summary>
	/// Gets the batch processing statistics.
	/// </summary>
	/// <value> The batch processing metrics keyed by processor type. </value>
	public required IReadOnlyDictionary<string, BatchProcessingMetrics> BatchMetrics { get; init; }

	/// <summary>
	/// Gets the handler registry performance statistics.
	/// </summary>
	/// <value> The metrics describing handler registry activity. </value>
	public required HandlerRegistryMetrics HandlerMetrics { get; init; }

	/// <summary>
	/// Gets the queue operation performance statistics.
	/// </summary>
	/// <value> The queue metrics keyed by queue name. </value>
	public required IReadOnlyDictionary<string, QueueMetrics> QueueMetrics { get; init; }

	/// <summary>
	/// Gets the timestamp when the snapshot was taken.
	/// </summary>
	/// <value> The timestamp associated with the snapshot. </value>
	public required DateTimeOffset Timestamp { get; init; }
}
