// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.ZeroAlloc;

/// <summary>
/// A message context factory that uses pooling for high-throughput scenarios.
/// Rents contexts from the pool instead of allocating new ones.
/// </summary>
/// <remarks>
/// This factory should be used when <see cref="ZeroAllocConfigurationExtensions.UseZeroAllocation"/> is enabled.
/// Contexts obtained from this factory should be returned to the pool after use via
/// <see cref="IMessageContextPool.ReturnToPool"/> for optimal memory efficiency.
/// </remarks>
public sealed class PooledMessageContextFactory(IMessageContextPool pool) : IMessageContextFactory
{
	private readonly IMessageContextPool _pool = pool ?? throw new ArgumentNullException(nameof(pool));

	/// <summary>
	/// Gets the underlying pool for returning contexts after use.
	/// </summary>
	public IMessageContextPool Pool => _pool;

	/// <inheritdoc />
	/// <remarks>
	/// Returns a pooled context. The caller is responsible for returning the context
	/// to the pool via <see cref="IMessageContextPool.ReturnToPool"/> after use.
	/// </remarks>
	public IMessageContext CreateContext()
	{
		// Rent from pool without message - caller will set it during dispatch
		return _pool.Rent();
	}

	/// <inheritdoc />
	/// <remarks>
	/// Returns a pooled context with the specified properties. The caller is responsible
	/// for returning the context to the pool via <see cref="IMessageContextPool.ReturnToPool"/> after use.
	/// </remarks>
	public IMessageContext CreateContext(IDictionary<string, object>? properties)
	{
		var context = _pool.Rent();

		if (properties != null)
		{
			foreach (var kvp in properties)
			{
				context.Items[kvp.Key] = kvp.Value;
			}
		}

		return context;
	}

	/// <inheritdoc />
	/// <remarks>
	/// Creates a pooled child context with cross-cutting identifiers propagated from the parent.
	/// The caller is responsible for returning the context to the pool after use.
	/// </remarks>
	public IMessageContext CreateChildContext(IMessageContext parent)
	{
		ArgumentNullException.ThrowIfNull(parent);

		// For child contexts, use the parent's CreateChildContext method
		// This preserves the proper propagation of cross-cutting identifiers
		return parent.CreateChildContext();
	}

	/// <inheritdoc />
	/// <remarks>
	/// Returns the context to the pool for reuse. The context is reset before being returned.
	/// This should always be called in a finally block to ensure contexts are returned even if
	/// processing fails.
	/// </remarks>
	public void Return(IMessageContext context)
	{
		_pool.ReturnToPool(context);
	}
}
