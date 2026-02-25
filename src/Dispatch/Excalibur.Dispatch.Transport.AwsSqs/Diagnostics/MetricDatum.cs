// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.AwsSqs;

/// <summary>
/// Represents a single metric data point for export to AWS CloudWatch.
/// </summary>
/// <remarks>
/// <para>
/// Maps to the CloudWatch <c>MetricDatum</c> structure used in <c>PutMetricData</c> API calls.
/// This is a framework-level abstraction that decouples the metrics bridge from the
/// AWS SDK types.
/// </para>
/// </remarks>
/// <param name="MetricName">The name of the metric.</param>
/// <param name="Value">The metric value.</param>
/// <param name="Unit">The unit of the metric (e.g., Count, Milliseconds, Bytes).</param>
/// <param name="Timestamp">The timestamp of the data point.</param>
/// <param name="Dimensions">Optional key-value dimensions for the metric.</param>
public sealed record MetricDatum(
	string MetricName,
	double Value,
	string Unit,
	DateTimeOffset Timestamp,
	IReadOnlyDictionary<string, string>? Dimensions = null);
