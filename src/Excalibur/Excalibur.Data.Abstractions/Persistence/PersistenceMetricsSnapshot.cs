// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Represents a snapshot of persistence metrics at a point in time.
/// </summary>
public sealed class PersistenceMetricsSnapshot
{
	/// <summary>
	/// Gets or sets the total number of queries executed.
	/// </summary>
	/// <value>The current <see cref="TotalQueries"/> value.</value>
	public long TotalQueries { get; set; }

	/// <summary>
	/// Gets or sets the total number of commands executed.
	/// </summary>
	/// <value>The current <see cref="TotalCommands"/> value.</value>
	public long TotalCommands { get; set; }

	/// <summary>
	/// Gets or sets the total number of errors.
	/// </summary>
	/// <value>The current <see cref="TotalErrors"/> value.</value>
	public long TotalErrors { get; set; }

	/// <summary>
	/// Gets or sets the number of cache hits.
	/// </summary>
	/// <value>The current <see cref="CacheHits"/> value.</value>
	public long CacheHits { get; set; }

	/// <summary>
	/// Gets or sets the number of cache misses.
	/// </summary>
	/// <value>The current <see cref="CacheMisses"/> value.</value>
	public long CacheMisses { get; set; }

	/// <summary>
	/// Gets or sets the average query duration in milliseconds.
	/// </summary>
	/// <value>The current <see cref="AverageQueryDurationMs"/> value.</value>
	public double AverageQueryDurationMs { get; set; }

	/// <summary>
	/// Gets or sets the average command duration in milliseconds.
	/// </summary>
	/// <value>The current <see cref="AverageCommandDurationMs"/> value.</value>
	public double AverageCommandDurationMs { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the snapshot.
	/// </summary>
	/// <value>The current <see cref="Timestamp"/> value.</value>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets custom metrics.
	/// </summary>
	/// <value>The current <see cref="CustomMetrics"/> value.</value>
	public Dictionary<string, long> CustomMetrics { get; } = [];
}
