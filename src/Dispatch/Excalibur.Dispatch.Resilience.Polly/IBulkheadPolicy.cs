// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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

	/// <summary>
	/// Gets the current bulkhead metrics.
	/// </summary>
	/// <returns>The current <see cref="BulkheadMetrics"/> snapshot.</returns>
	BulkheadMetrics GetMetrics();
}
