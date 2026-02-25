// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Configuration options for graceful degradation.
/// </summary>
/// <remarks>
/// <para>
/// Follows the Microsoft Options pattern (Polly v8 <c>RetryStrategyOptions</c> as reference).
/// Each degradation level is configured as a <see cref="DegradationLevelConfig"/> in the
/// <see cref="Levels"/> list, replacing the previous 21-property flat design.
/// </para>
/// </remarks>
public sealed class GracefulDegradationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether automatic level adjustment is enabled.
	/// </summary>
	/// <value><see langword="true"/> to allow the service to adjust degradation levels automatically; otherwise, <see langword="false"/>.</value>
	public bool EnableAutoAdjustment { get; set; } = true;

	/// <summary>
	/// Gets or sets the interval for health checks.
	/// </summary>
	/// <value>The cadence between health evaluation cycles. Defaults to 30 seconds.</value>
	public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the minimum duration to maintain a degradation level.
	/// </summary>
	/// <value>The minimum time a degradation level remains active before reevaluation. Defaults to one minute.</value>
	public TimeSpan MinimumLevelDuration { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets the degradation level configurations.
	/// </summary>
	/// <value>The ordered list of degradation level configurations from least to most severe.</value>
	public List<DegradationLevelConfig> Levels { get; set; } = DefaultLevels();

	/// <summary>
	/// Gets the priority threshold for a given degradation level.
	/// Returns 0 if the level is not configured.
	/// </summary>
	internal int GetPriorityThreshold(DegradationLevel level) =>
		Levels.FirstOrDefault(l => string.Equals(l.Name, level.ToString(), StringComparison.OrdinalIgnoreCase))
			?.PriorityThreshold ?? 0;

	/// <summary>
	/// Gets the error rate threshold for a given degradation level.
	/// Returns 1.0 (never triggers) if the level is not configured.
	/// </summary>
	internal double GetErrorRateThreshold(DegradationLevel level) =>
		Levels.FirstOrDefault(l => string.Equals(l.Name, level.ToString(), StringComparison.OrdinalIgnoreCase))
			?.ErrorRateThreshold ?? 1.0;

	/// <summary>
	/// Gets the CPU threshold for a given degradation level.
	/// Returns 100.0 (never triggers) if the level is not configured.
	/// </summary>
	internal double GetCpuThreshold(DegradationLevel level) =>
		Levels.FirstOrDefault(l => string.Equals(l.Name, level.ToString(), StringComparison.OrdinalIgnoreCase))
			?.CpuThreshold ?? 100.0;

	/// <summary>
	/// Gets the memory threshold for a given degradation level.
	/// Returns 100.0 (never triggers) if the level is not configured.
	/// </summary>
	internal double GetMemoryThreshold(DegradationLevel level) =>
		Levels.FirstOrDefault(l => string.Equals(l.Name, level.ToString(), StringComparison.OrdinalIgnoreCase))
			?.MemoryThreshold ?? 100.0;

	private static List<DegradationLevelConfig> DefaultLevels() =>
	[
		new("Minor", 10, 0.01, 60, 60),
		new("Moderate", 30, 0.05, 70, 70),
		new("Major", 50, 0.10, 80, 80),
		new("Severe", 70, 0.25, 90, 90),
		new("Emergency", 100, 0.50, 95, 95),
	];
}

/// <summary>
/// Configuration for a single degradation level.
/// </summary>
/// <param name="Name">The degradation level name (e.g., "Minor", "Moderate").</param>
/// <param name="PriorityThreshold">Operations below this priority are rejected at this level.</param>
/// <param name="ErrorRateThreshold">Error rate (0.0-1.0) that triggers this level.</param>
/// <param name="CpuThreshold">CPU usage percentage (0-100) that triggers this level.</param>
/// <param name="MemoryThreshold">Memory usage percentage (0-100) that triggers this level.</param>
public record DegradationLevelConfig(
	string Name,
	int PriorityThreshold,
	double ErrorRateThreshold,
	double CpuThreshold,
	double MemoryThreshold);
