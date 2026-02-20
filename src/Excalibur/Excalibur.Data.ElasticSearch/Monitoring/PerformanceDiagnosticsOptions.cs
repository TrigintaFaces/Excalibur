// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Configures performance diagnostics and slow operation detection.
/// </summary>
public sealed class PerformanceDiagnosticsOptions
{
	/// <summary>
	/// Gets a value indicating whether performance diagnostics are enabled.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether performance monitoring is active. Defaults to <c> true </c>. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the threshold for slow operation detection.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the slow operation threshold. Defaults to 5 seconds. </value>
	public TimeSpan SlowOperationThreshold { get; init; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets a value indicating whether to track memory usage during operations.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to monitor memory usage. Defaults to <c> false </c>. </value>
	public bool TrackMemoryUsage { get; init; }

	/// <summary>
	/// Gets a value indicating whether to analyze query performance.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to analyze query execution statistics. Defaults to <c> false </c>. </value>
	public bool AnalyzeQueryPerformance { get; init; }

	/// <summary>
	/// Gets the sampling rate for performance analysis (0.0 to 1.0).
	/// </summary>
	/// <value> A <see cref="double" /> representing the sampling percentage. Defaults to 0.01 (1%). </value>
	public double SamplingRate { get; init; } = 0.01;
}
