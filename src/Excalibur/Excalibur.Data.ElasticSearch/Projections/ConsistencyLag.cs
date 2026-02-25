// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents the consistency lag between write and read models.
/// </summary>
public sealed class ConsistencyLag
{
	/// <summary>
	/// Gets the projection type.
	/// </summary>
	/// <value>
	/// The projection type.
	/// </value>
	public required string ProjectionType { get; init; }

	/// <summary>
	/// Gets the current lag in milliseconds.
	/// </summary>
	/// <value>
	/// The current lag in milliseconds.
	/// </value>
	public required double CurrentLagMs { get; init; }

	/// <summary>
	/// Gets the average lag over the measurement period.
	/// </summary>
	/// <value>
	/// The average lag over the measurement period.
	/// </value>
	public required double AverageLagMs { get; init; }

	/// <summary>
	/// Gets the maximum lag observed.
	/// </summary>
	/// <value>
	/// The maximum lag observed.
	/// </value>
	public required double MaxLagMs { get; init; }

	/// <summary>
	/// Gets the minimum lag observed.
	/// </summary>
	/// <value>
	/// The minimum lag observed.
	/// </value>
	public required double MinLagMs { get; init; }

	/// <summary>
	/// Gets the 95th percentile lag.
	/// </summary>
	/// <value>
	/// The 95th percentile lag.
	/// </value>
	public double P95LagMs { get; init; }

	/// <summary>
	/// Gets the 99th percentile lag.
	/// </summary>
	/// <value>
	/// The 99th percentile lag.
	/// </value>
	public double P99LagMs { get; init; }

	/// <summary>
	/// Gets the number of events pending projection.
	/// </summary>
	/// <value>
	/// The number of events pending projection.
	/// </value>
	public long PendingEvents { get; init; }

	/// <summary>
	/// Gets the timestamp of the last processed event.
	/// </summary>
	/// <value>
	/// The timestamp of the last processed event.
	/// </value>
	public DateTimeOffset? LastProcessedEventTime { get; init; }

	/// <summary>
	/// Gets a value indicating whether the lag is within acceptable limits.
	/// </summary>
	/// <value>
	/// A value indicating whether the lag is within acceptable limits.
	/// </value>
	public bool IsWithinSLA { get; init; }
}
