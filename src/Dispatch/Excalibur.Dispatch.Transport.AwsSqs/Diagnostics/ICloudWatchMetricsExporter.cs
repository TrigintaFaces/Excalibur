// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.AwsSqs;

/// <summary>
/// Exports metric data points to AWS CloudWatch.
/// </summary>
/// <remarks>
/// <para>
/// Bridges OpenTelemetry metrics to AWS CloudWatch by batching metric data points
/// and publishing them via the CloudWatch <c>PutMetricData</c> API.
/// </para>
/// <para>
/// This interface follows the Microsoft pattern of single-method focused interfaces
/// (similar to <c>IHealthCheck</c> with 1 method). The export operation is the
/// sole responsibility; buffering and scheduling are handled by the hosting
/// infrastructure.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var metrics = new List&lt;MetricDatum&gt;
/// {
///     new("MessagesSent", 42, "Count", DateTimeOffset.UtcNow),
///     new("ProcessingLatency", 150.5, "Milliseconds", DateTimeOffset.UtcNow),
/// };
/// await exporter.ExportAsync(metrics, cancellationToken);
/// </code>
/// </example>
public interface ICloudWatchMetricsExporter
{
	/// <summary>
	/// Exports a batch of metric data points to AWS CloudWatch.
	/// </summary>
	/// <param name="metrics">The metric data points to export.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> representing the asynchronous export operation.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="metrics"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// CloudWatch has a limit of 1000 metric data points per <c>PutMetricData</c> call.
	/// Implementations should handle batching internally if the collection exceeds this limit.
	/// </para>
	/// </remarks>
	Task ExportAsync(IReadOnlyList<MetricDatum> metrics, CancellationToken cancellationToken);
}
