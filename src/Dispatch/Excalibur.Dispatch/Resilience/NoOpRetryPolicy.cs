// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// No-op retry policy that executes operations directly without any retry logic.
/// </summary>
/// <remarks>
/// <para>
/// This is a singleton implementation that provides pass-through behavior for scenarios
/// where retry logic is disabled or not needed. It replaces the previous
/// <c>NullRetryPolicyFactory</c> which required Polly's <c>Policy.NoOpAsync()</c>.
/// </para>
/// <para>
/// Use <see cref="Instance"/> to access the singleton instance:
/// <code>
/// var policy = NoOpRetryPolicy.Instance;
/// await policy.ExecuteAsync(async ct => await DoWorkAsync(ct), cancellationToken);
/// </code>
/// </para>
/// </remarks>
public sealed class NoOpRetryPolicy : IRetryPolicy
{
	/// <summary>
	/// Prevents external instantiation to enforce singleton pattern.
	/// </summary>
	private NoOpRetryPolicy()
	{
	}

	/// <summary>
	/// Gets the singleton instance of the no-op retry policy.
	/// </summary>
	public static NoOpRetryPolicy Instance { get; } = new();

	/// <inheritdoc />
	/// <remarks>
	/// Executes the action directly without any retry logic.
	/// Exceptions are propagated immediately without retry attempts.
	/// </remarks>
	public Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> action,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		return action(cancellationToken);
	}

	/// <inheritdoc />
	/// <remarks>
	/// Executes the action directly without any retry logic.
	/// Exceptions are propagated immediately without retry attempts.
	/// </remarks>
	public Task ExecuteAsync(
		Func<CancellationToken, Task> action,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		return action(cancellationToken);
	}
}
