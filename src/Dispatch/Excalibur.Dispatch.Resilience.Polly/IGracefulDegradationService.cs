// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Graceful degradation framework with multiple service levels.
/// </summary>
public interface IGracefulDegradationService
{
	/// <summary>
	/// Gets the current degradation level.
	/// </summary>
	/// <value>The active degradation level for the service.</value>
	DegradationLevel CurrentLevel { get; }

	/// <summary>
	/// Executes an operation with graceful degradation.
	/// </summary>
	/// <typeparam name="T">The type returned by the primary or fallback operations.</typeparam>
	/// <param name="context">The degradation context describing fallback strategies.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes with the operation result.</returns>
	Task<T> ExecuteWithDegradationAsync<T>(
		DegradationContext<T> context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Manually sets the degradation level.
	/// </summary>
	/// <param name="level">The level to apply.</param>
	/// <param name="reason">The reason for the manual override.</param>
	void SetLevel(DegradationLevel level, string reason);

	/// <summary>
	/// Gets degradation metrics.
	/// </summary>
	/// <returns>The current degradation metrics.</returns>
	DegradationMetrics GetMetrics();
}
