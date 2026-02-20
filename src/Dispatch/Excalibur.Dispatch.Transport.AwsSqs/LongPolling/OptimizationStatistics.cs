// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Optimization statistics for a queue.
/// </summary>
public sealed class OptimizationStatistics
{
	/// <summary>
	/// Gets or sets the queue URL.
	/// </summary>
	/// <value>
	/// The queue URL.
	/// </value>
	public Uri? QueueUrl { get; set; }

	/// <summary>
	/// Gets or sets the total messages received.
	/// </summary>
	/// <value>
	/// The total messages received.
	/// </value>
	public long TotalMessages { get; set; }

	/// <summary>
	/// Gets or sets the API calls saved.
	/// </summary>
	/// <value>
	/// The API calls saved.
	/// </value>
	public long ApiCallsSaved { get; set; }

	/// <summary>
	/// Gets or sets the efficiency score.
	/// </summary>
	/// <value>
	/// The efficiency score.
	/// </value>
	public double EfficiencyScore { get; set; }

	/// <summary>
	/// Gets or sets the average message latency.
	/// </summary>
	/// <value>
	/// The average message latency.
	/// </value>
	public TimeSpan AverageLatency { get; set; }

	/// <summary>
	/// Gets or sets the empty receive rate.
	/// </summary>
	/// <value>
	/// The empty receive rate.
	/// </value>
	public double EmptyReceiveRate { get; set; }

	/// <summary>
	/// Gets or sets when the statistics were last updated.
	/// </summary>
	/// <value>
	/// When the statistics were last updated.
	/// </value>
	public DateTimeOffset LastUpdated { get; set; }
}
