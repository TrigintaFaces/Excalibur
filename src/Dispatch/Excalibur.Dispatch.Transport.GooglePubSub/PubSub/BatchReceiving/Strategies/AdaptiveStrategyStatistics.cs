// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Statistics for the adaptive batching strategy.
/// </summary>
public sealed class AdaptiveStrategyStatistics
{
	/// <summary>
	/// Gets or sets the current batch size.
	/// </summary>
	/// <value>
	/// The current batch size.
	/// </value>
	public int CurrentBatchSize { get; set; }

	/// <summary>
	/// Gets or sets the average processing time.
	/// </summary>
	/// <value>
	/// The average processing time.
	/// </value>
	public double AverageProcessingTime { get; set; }

	/// <summary>
	/// Gets or sets the average throughput.
	/// </summary>
	/// <value>
	/// The average throughput.
	/// </value>
	public double AverageThroughput { get; set; }

	/// <summary>
	/// Gets or sets the current aggressiveness factor.
	/// </summary>
	/// <value>
	/// The current aggressiveness factor.
	/// </value>
	public double Aggressiveness { get; set; }

	/// <summary>
	/// Gets or sets the number of stable iterations.
	/// </summary>
	/// <value>
	/// The number of stable iterations.
	/// </value>
	public int StableIterations { get; set; }

	/// <summary>
	/// Gets or sets recent batch results.
	/// </summary>
	/// <value>
	/// Recent batch results.
	/// </value>
	public List<BatchResult> RecentResults { get; set; } = [];
}
