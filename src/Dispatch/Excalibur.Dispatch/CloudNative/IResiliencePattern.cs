// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// Interface for patterns that support resilience features like circuit breakers, retries, etc.
/// </summary>
public interface IResiliencePattern : ICloudNativePattern
{
	/// <summary>
	/// Gets current state of the resilience mechanism.
	/// </summary>
	/// <value>
	/// Current state of the resilience mechanism.
	/// </value>
	ResilienceState State { get; }

	/// <summary>
	/// Execute an operation with resilience features.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);

	/// <summary>
	/// Execute an operation with resilience features and a fallback.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task<T> ExecuteAsync<T>(Func<Task<T>> operation, Func<Task<T>> fallback, CancellationToken cancellationToken);

	/// <summary>
	/// Manually reset the resilience state.
	/// </summary>
	void Reset();
}
