// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Represents a metrics recording interface for serialization performance and telemetry.
/// </summary>
/// <remarks>
/// This interface provides a contract for collecting and recording metrics related to serialization operations, including counter metrics
/// for events and gauge metrics for measurements. Implementations can provide integration with various telemetry systems like Application
/// Insights, Prometheus, or custom solutions.
/// </remarks>
[Obsolete("Use System.Diagnostics.Metrics.Meter with named Counter<T>/Histogram<T> instruments instead. " +
	"This interface duplicates System.Diagnostics.Metrics without added value. " +
	"See https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics for migration guidance. " +
	"This interface will be removed in a future release.")]
public interface IMetrics
{
	/// <summary>
	/// Records a counter metric that represents a cumulative value over time.
	/// </summary>
	/// <param name="name"> The metric name identifying the counter. Should follow naming conventions (e.g., 'serialization.operations.count'). </param>
	/// <param name="value"> The counter value to record. Must be non-negative for meaningful metrics. </param>
	/// <param name="tags"> Optional key-value pairs for metric dimensions and filtering (e.g., operation type, status). </param>
	/// <remarks>
	/// Counter metrics are typically used for:
	/// - Tracking serialization/deserialization operation counts
	/// - Recording error counts by type
	/// - Measuring throughput over time The metric name should be descriptive and follow your organization's naming conventions.
	/// </remarks>
	void RecordCounter(string name, long value, params KeyValuePair<string, object?>[] tags);

	/// <summary>
	/// Records a gauge metric that represents a point-in-time measurement value.
	/// </summary>
	/// <param name="name"> The metric name identifying the gauge. Should follow naming conventions (e.g., 'serialization.pool.size.current'). </param>
	/// <param name="value"> The gauge value to record. Can be any numeric measurement relevant to the metric. </param>
	/// <param name="tags"> Optional key-value pairs for metric dimensions and filtering (e.g., pool type, component). </param>
	/// <remarks>
	/// Gauge metrics are typically used for:
	/// - Tracking pool sizes and utilization rates
	/// - Recording serialization latency measurements
	/// - Monitoring memory usage and performance indicators Unlike counters, gauge values can go up and down over time.
	/// </remarks>
	void RecordGauge(string name, double value, params KeyValuePair<string, object?>[] tags);
}
