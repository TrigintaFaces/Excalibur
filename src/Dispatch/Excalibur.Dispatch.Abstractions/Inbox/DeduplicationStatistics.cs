// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides statistics about the in-memory deduplicator performance and usage.
/// </summary>
public sealed class DeduplicationStatistics
{
	/// <summary>
	/// Gets the current number of message IDs being tracked in memory.
	/// </summary>
	/// <value> The number of message identifiers currently tracked. </value>
	public int TrackedMessageCount { get; init; }

	/// <summary>
	/// Gets the total number of duplicate detection checks performed.
	/// </summary>
	/// <value> The number of duplicate detection checks performed. </value>
	public long TotalChecks { get; init; }

	/// <summary>
	/// Gets the number of messages identified as duplicates.
	/// </summary>
	/// <value> The number of detected duplicate messages. </value>
	public long DuplicatesDetected { get; init; }

	/// <summary>
	/// Gets the estimated memory usage in bytes.
	/// </summary>
	/// <remarks>
	/// This is an approximation based on tracked message IDs and overhead. Actual memory usage may vary based on .NET runtime optimizations.
	/// </remarks>
	/// <value> The estimated memory usage in bytes. </value>
	public long EstimatedMemoryUsageBytes { get; init; }

	/// <summary>
	/// Gets the duplicate detection hit ratio as a percentage.
	/// </summary>
	/// <value> Percentage of checks that identified duplicates, 0-100. </value>
	public double DuplicateHitRatio => TotalChecks > 0 ? DuplicatesDetected * 100.0 / TotalChecks : 0.0;

	/// <summary>
	/// Gets the timestamp when these statistics were captured.
	/// </summary>
	/// <value> The timestamp when statistics were captured. </value>
	public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public override string ToString() =>
		$"DeduplicationStats: {TrackedMessageCount} tracked, {DuplicatesDetected}/{TotalChecks} duplicates ({DuplicateHitRatio:F2}%), ~{EstimatedMemoryUsageBytes:N0} bytes";
}
