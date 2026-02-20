// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents consistency metrics for a projection type.
/// </summary>
public sealed class ConsistencyMetrics
{
	/// <summary>
	/// Gets the projection type.
	/// </summary>
	/// <value>
	/// The projection type.
	/// </value>
	public required string ProjectionType { get; init; }

	/// <summary>
	/// Gets the measurement period start time.
	/// </summary>
	/// <value>
	/// The measurement period start time.
	/// </value>
	public required DateTime PeriodStart { get; init; }

	/// <summary>
	/// Gets the measurement period end time.
	/// </summary>
	/// <value>
	/// The measurement period end time.
	/// </value>
	public required DateTime PeriodEnd { get; init; }

	/// <summary>
	/// Gets the total number of events processed.
	/// </summary>
	/// <value>
	/// The total number of events processed.
	/// </value>
	public required long TotalEventsProcessed { get; init; }

	/// <summary>
	/// Gets the average processing time per event.
	/// </summary>
	/// <value>
	/// The average processing time per event.
	/// </value>
	public required double AverageProcessingTimeMs { get; init; }

	/// <summary>
	/// Gets the throughput in events per second.
	/// </summary>
	/// <value>
	/// The throughput in events per second.
	/// </value>
	public required double EventsPerSecond { get; init; }

	/// <summary>
	/// Gets the percentage of events processed within SLA.
	/// </summary>
	/// <value>
	/// The percentage of events processed within SLA.
	/// </value>
	public required double SLACompliancePercentage { get; init; }

	/// <summary>
	/// Gets the number of consistency violations.
	/// </summary>
	/// <value>
	/// The number of consistency violations.
	/// </value>
	public long ConsistencyViolations { get; init; }

	/// <summary>
	/// Gets the lag distribution histogram.
	/// </summary>
	/// <value>
	/// The lag distribution histogram.
	/// </value>
	public IDictionary<string, long>? LagDistribution { get; init; }
}
