// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Configures metrics collection for Elasticsearch operations.
/// </summary>
public sealed class MetricsOptions
{
	/// <summary>
	/// Gets a value indicating whether metrics collection is enabled.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether metrics are collected. Defaults to <c> true </c>. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to include operation duration metrics.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to track operation durations. Defaults to <c> true </c>. </value>
	public bool IncludeDuration { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to include success/failure rate metrics.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to track success and failure rates. Defaults to <c> true </c>. </value>
	public bool IncludeSuccessFailureRates { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to include circuit breaker state metrics.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to track circuit breaker states. Defaults to <c> true </c>. </value>
	public bool IncludeCircuitBreakerState { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to include retry attempt metrics.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to track retry attempts. Defaults to <c> true </c>. </value>
	public bool IncludeRetryAttempts { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to include document count metrics.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to track document counts in operations. Defaults to <c> false </c>. </value>
	public bool IncludeDocumentCounts { get; init; }

	/// <summary>
	/// Gets the histogram bucket configuration for duration metrics.
	/// </summary>
	/// <value> Array of bucket boundaries for duration histograms in milliseconds. </value>
	public double[] DurationHistogramBuckets { get; init; } = [1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000];
}
