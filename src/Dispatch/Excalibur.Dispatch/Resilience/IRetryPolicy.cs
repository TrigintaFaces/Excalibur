// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Zero-dependency retry policy interface for executing operations with retry logic.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides a minimal abstraction for retry policies that does not depend
/// on any external libraries like Polly. It allows the core Dispatch package to remain
/// dependency-free while still supporting advanced retry implementations via optional packages.
/// </para>
/// <para>
/// Default implementations include:
/// <list type="bullet">
///   <item><see cref="DefaultRetryPolicy"/> - Uses <see cref="IBackoffCalculator"/> for retry delays</item>
///   <item><see cref="NoOpRetryPolicy"/> - Pass-through policy that doesn't retry</item>
/// </list>
/// </para>
/// <para>
/// For Polly-based implementations, use the <c>Excalibur.Dispatch.Resilience.Polly</c> package.
/// </para>
/// </remarks>
public interface IRetryPolicy
{
	/// <summary>
	/// Executes an async operation with retry logic.
	/// </summary>
	/// <typeparam name="TResult">The return type of the operation.</typeparam>
	/// <param name="action">The operation to execute.</param>
	/// <param name="cancellationToken">Token to cancel the operation.</param>
	/// <returns>The result of the operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
	/// <exception cref="Exception">The last exception thrown after all retry attempts are exhausted.</exception>
	Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> action,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes an async operation with retry logic.
	/// </summary>
	/// <param name="action">The operation to execute.</param>
	/// <param name="cancellationToken">Token to cancel the operation.</param>
	/// <returns>A task representing the async operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
	/// <exception cref="Exception">The last exception thrown after all retry attempts are exhausted.</exception>
	Task ExecuteAsync(
		Func<CancellationToken, Task> action,
		CancellationToken cancellationToken);
}
