// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Performance metrics from a dry run.
/// </summary>
public sealed class DryRunPerformanceMetrics
{
	/// <summary>
	/// Gets the average processing time per document.
	/// </summary>
	/// <value>
	/// The average processing time per document.
	/// </value>
	public required double AverageProcessingTimeMs { get; init; }

	/// <summary>
	/// Gets the estimated total migration time.
	/// </summary>
	/// <value>
	/// The estimated total migration time.
	/// </value>
	public required TimeSpan EstimatedTotalTime { get; init; }

	/// <summary>
	/// Gets the estimated throughput.
	/// </summary>
	/// <value>
	/// The estimated throughput.
	/// </value>
	public required double DocumentsPerSecond { get; init; }
}
