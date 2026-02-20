// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Degradation metrics for monitoring.
/// </summary>
public sealed class DegradationMetrics
{
	/// <summary>
	/// Gets the current degradation level.
	/// </summary>
	/// <value>The active <see cref="DegradationLevel"/> applied to the pipeline.</value>
	public DegradationLevel CurrentLevel { get; init; }

	/// <summary>
	/// Gets the timestamp of the last level change.
	/// </summary>
	/// <value>The UTC timestamp when the level most recently changed.</value>
	public DateTimeOffset LastLevelChange { get; init; }

	/// <summary>
	/// Gets the reason for the last level change.
	/// </summary>
	/// <value>A human-readable explanation describing why the level changed.</value>
	public string LastChangeReason { get; init; } = string.Empty;

	/// <summary>
	/// Gets the operation statistics by operation name.
	/// </summary>
	/// <value>A dictionary mapping each operation name to its aggregated statistics.</value>
	public IReadOnlyDictionary<string, OperationStatistics> OperationStatistics { get; init; } = new Dictionary<string, OperationStatistics>(StringComparer.Ordinal);

	/// <summary>
	/// Gets the current health metrics.
	/// </summary>
	/// <value>The underlying system health metrics associated with the degradation state.</value>
	public HealthMetrics HealthMetrics { get; init; } = new();

	/// <summary>
	/// Gets the total number of operations across all operation types.
	/// </summary>
	/// <value>The cumulative number of primary or fallback executions performed.</value>
	public long TotalOperations { get; init; }

	/// <summary>
	/// Gets the total number of fallback executions across all operations.
	/// </summary>
	/// <value>The total number of times a fallback operation executed instead of the primary path.</value>
	public long TotalFallbacks { get; init; }

	/// <summary>
	/// Gets the overall success rate (0.0-1.0) across all operations.
	/// </summary>
	/// <value>The proportion of successful operations represented as a value between 0.0 and 1.0.</value>
	public double SuccessRate { get; init; }
}
