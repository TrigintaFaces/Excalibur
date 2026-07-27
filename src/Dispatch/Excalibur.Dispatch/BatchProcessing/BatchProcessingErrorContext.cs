// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.BatchProcessing;

/// <summary>
/// Carries the details of a failed batch to an <c>OnBatchError</c> callback so that a batch-processing
/// failure is observable to the caller rather than swallowed.
/// </summary>
/// <remarks>
/// Modelled on the Polly resilience-event argument pattern (a struct context passed to a
/// <see cref="Func{T, TResult}"/> callback), so additional context can be added without breaking the
/// callback signature. The processor continues running after a faulted batch; a fault never tears down
/// the pipeline.
/// </remarks>
/// <typeparam name="T">The item type processed in the batch.</typeparam>
internal readonly struct BatchProcessingErrorContext<T>
	where T : class
{
	/// <summary>
	/// Initializes a new instance of the <see cref="BatchProcessingErrorContext{T}"/> struct.
	/// </summary>
	/// <param name="batch">The batch whose processing delegate threw.</param>
	/// <param name="exception">The exception thrown by the batch-processing delegate.</param>
	/// <param name="isShutdown">Whether the fault occurred during a shutdown/drain flush.</param>
	public BatchProcessingErrorContext(IReadOnlyList<T> batch, Exception exception, bool isShutdown)
	{
		Batch = batch;
		Exception = exception;
		IsShutdown = isShutdown;
	}

	/// <summary>
	/// Gets the batch whose processing delegate threw.
	/// </summary>
	/// <value>The failed batch of items.</value>
	public IReadOnlyList<T> Batch { get; }

	/// <summary>
	/// Gets the exception thrown by the batch-processing delegate.
	/// </summary>
	/// <value>The exception that faulted the batch.</value>
	public Exception Exception { get; }

	/// <summary>
	/// Gets a value indicating whether the fault occurred during a shutdown/drain flush.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the fault occurred while draining during shutdown; otherwise,
	/// <see langword="false"/> for a fault during live processing.
	/// </value>
	public bool IsShutdown { get; }
}
