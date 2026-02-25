// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// Metrics for cloud-native patterns.
/// </summary>
public sealed class PatternMetrics
{
	/// <summary>
	/// Gets or sets total number of operations performed.
	/// </summary>
	/// <value>The current <see cref="TotalOperations"/> value.</value>
	public long TotalOperations { get; set; }

	/// <summary>
	/// Gets or sets number of successful operations.
	/// </summary>
	/// <value>The current <see cref="SuccessfulOperations"/> value.</value>
	public long SuccessfulOperations { get; set; }

	/// <summary>
	/// Gets or sets number of failed operations.
	/// </summary>
	/// <value>The current <see cref="FailedOperations"/> value.</value>
	public long FailedOperations { get; set; }

	/// <summary>
	/// Gets or sets average operation duration.
	/// </summary>
	/// <value>The current <see cref="AverageOperationTime"/> value.</value>
	public TimeSpan AverageOperationTime { get; set; }

	/// <summary>
	/// Gets success rate as a percentage (0.0 to 1.0).
	/// </summary>
	/// <value>
	/// Success rate as a percentage (0.0 to 1.0).
	/// </value>
	public double SuccessRate => TotalOperations > 0 ? (double)SuccessfulOperations / TotalOperations : 0;

	/// <summary>
	/// Gets or sets when the pattern was last updated.
	/// </summary>
	/// <value>The current <see cref="LastUpdated"/> value.</value>
	public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets additional custom metrics specific to the pattern.
	/// </summary>
	/// <value>The current <see cref="CustomMetrics"/> value.</value>
	public Dictionary<string, object> CustomMetrics { get; init; } = [];
}
