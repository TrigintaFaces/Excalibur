// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Provides a base implementation for pooled middleware that can be reused to reduce allocations.
/// </summary>
/// <remarks>
/// This abstract class enables middleware to participate in object pooling for high-performance scenarios. Instances are rented from a
/// pool, used for processing, reset, and returned. This pattern significantly reduces garbage collection pressure in high-throughput
/// pipelines. Derived classes must implement the Reset method to clear state between uses.
/// </remarks>
public abstract class PooledDispatchMiddleware : DispatchMiddlewareBase, IPooledObject
{
	private volatile bool _isInUse;
	private volatile bool _canBePooled = true;

	/// <inheritdoc />
	public bool CanBePooled => _canBePooled && !_isInUse;

	/// <summary>
	/// Gets or sets a value indicating whether this middleware instance is currently in use.
	/// </summary>
	/// <value>
	/// A value indicating whether this middleware instance is currently in use.
	/// </value>
	protected bool IsInUse
	{
		get => _isInUse;
		set => _isInUse = value;
	}

	/// <inheritdoc />
	public virtual void Reset()
	{
		_isInUse = false;
		ResetState();
	}

	/// <summary>
	/// Marks this middleware instance as unsuitable for pooling.
	/// </summary>
	/// <remarks>
	/// Call this method if the middleware enters a state where it cannot be safely reused, such as holding unmanaged resources or being in
	/// an error state.
	/// </remarks>
	protected void MarkAsUnpoolable() => _canBePooled = false;

	/// <summary>
	/// Called before processing to acquire the middleware from the pool.
	/// </summary>
	protected virtual void OnRent() => _isInUse = true;

	/// <summary>
	/// Called after processing to return the middleware to the pool.
	/// </summary>
	protected virtual void OnReturn() => Reset();

	/// <summary>
	/// Resets the middleware state for reuse.
	/// </summary>
	/// <remarks>
	/// Derived classes must implement this method to clear all state, reset fields to defaults, and prepare for the next use. This method
	/// should be idempotent and should not throw exceptions.
	/// </remarks>
	protected abstract void ResetState();

	/// <inheritdoc />
	protected override async ValueTask<IMessageResult> ProcessAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		OnRent();
		try
		{
			return await base.ProcessAsync(message, context, nextDelegate, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			OnReturn();
		}
	}
}
