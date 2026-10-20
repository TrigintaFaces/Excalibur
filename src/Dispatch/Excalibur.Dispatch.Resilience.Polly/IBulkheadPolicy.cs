// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Bulkhead isolation policy to prevent resource exhaustion.
/// </summary>
public interface IBulkheadPolicy
{
	/// <summary>
	/// Gets a value indicating whether the bulkhead has available capacity.
	/// </summary>
	/// <value><see langword="true"/> when capacity remains; otherwise, <see langword="false"/>.</value>
	bool HasCapacity { get; }

	/// <summary>
	/// Executes an operation with bulkhead isolation.
	/// </summary>
	/// <typeparam name="T">The type returned by the asynchronous <paramref name="operation"/>.</typeparam>
	/// <param name="operation">The delegate to execute within the bulkhead constraints.</param>
	/// <param name="cancellationToken">A token used to observe cancellation requests.</param>
	/// <returns>A task that completes with the asynchronous result produced by the operation.</returns>
	Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);

	/// <summary>Executes an operation that observes both caller cancellation and its execution timeout.</summary>
	/// <typeparam name="T">The result type.</typeparam>
	/// <param name="operation">The operation, which must observe the supplied strategy token.</param>
	/// <param name="cancellationToken">The caller cancellation token.</param>
	/// <returns>The operation result.</returns>
	/// <exception cref="NotSupportedException">The implementation does not support token-aware execution.</exception>
	/// <remarks>Existing implementations retain the original overload and may implement this capability explicitly.</remarks>
	Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
		throw new NotSupportedException("This bulkhead implementation does not support token-aware execution.");

	/// <summary>
	/// Gets the current bulkhead metrics.
	/// </summary>
	/// <returns>The current <see cref="BulkheadMetrics"/> snapshot.</returns>
	BulkheadMetrics GetMetrics();
}
