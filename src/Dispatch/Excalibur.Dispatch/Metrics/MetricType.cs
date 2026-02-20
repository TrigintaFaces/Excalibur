// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Defines the fundamental metric types supported by the messaging system's telemetry infrastructure.
/// </summary>
/// <remarks>
/// These metric types align with industry standards (Prometheus, OpenTelemetry) and provide comprehensive observability capabilities for
/// different measurement scenarios. Each type serves specific use cases and has distinct characteristics for data collection and analysis.
/// </remarks>
public enum MetricType
{
	/// <summary>
	/// A monotonically increasing counter metric that tracks cumulative values.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Counters are ideal for measuring:
	/// - Total requests processed
	/// - Error counts and failure rates
	/// - Bytes transmitted or operations completed
	/// - Resource usage accumulation
	/// </para>
	/// <para>Counter values never decrease except on application restart, making them perfect for rate calculations and trend analysis.</para>
	/// </remarks>
	Counter = 0,

	/// <summary>
	/// A gauge metric representing a point-in-time measurement that can increase or decrease.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Gauges are ideal for measuring:
	/// - Current memory usage or queue depth
	/// - Active connection counts
	/// - Temperature, CPU utilization, or other variable metrics
	/// - Current pool sizes and resource availability
	/// </para>
	/// <para>Unlike counters, gauge values can fluctuate up and down over time.</para>
	/// </remarks>
	Gauge = 1,

	/// <summary>
	/// A histogram metric that samples observations and groups them into configurable buckets.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Histograms are ideal for measuring:
	/// - Request duration distributions
	/// - Message size distributions
	/// - Response time percentiles (P50, P95, P99)
	/// - Resource utilization patterns
	/// </para>
	/// <para>Histograms provide detailed distribution analysis and support quantile calculations for performance analysis and SLA monitoring.</para>
	/// </remarks>
	Histogram = 2,

	/// <summary>
	/// A summary metric that provides quantile estimates over a sliding time window.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Summaries are ideal for measuring:
	/// - Request latency quantiles with pre-calculated percentiles
	/// - Response size summaries with statistical analysis
	/// - Performance metrics requiring real-time quantile tracking
	/// </para>
	/// <para>Unlike histograms, summaries calculate quantiles client-side and provide more accurate percentile calculations for time-series analysis.</para>
	/// </remarks>
	Summary = 3,
}
