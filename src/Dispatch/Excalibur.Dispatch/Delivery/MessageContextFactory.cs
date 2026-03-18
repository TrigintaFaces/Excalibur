// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Default implementation of message context factory with thread-local context recycling.
/// </summary>
/// <remarks>
/// <para>
/// Uses a single-element per-thread cache to recycle <see cref="MessageContext"/> instances.
/// When <see cref="Return"/> is called, the context is reset and cached for the next
/// <see cref="CreateContext()"/> call on the same thread. This eliminates ~200B per dispatch
/// for the common single-dispatch-per-thread pattern without the complexity of a full pool.
/// </para>
/// <para>
/// <strong>Thread-safety invariant:</strong> Recycling is safe because <c>[ThreadStatic]</c>
/// guarantees each thread has its own cached instance, and <see cref="Return"/> is only called
/// after the dispatch completes fully on the synchronous fast path. On the async path, contexts
/// follow normal allocation/GC. This means no async continuation can hold a stale reference to
/// a recycled context — the entire dispatch lifecycle (create → use → reset → cache) executes
/// on a single thread with no escaping references.
/// </para>
/// </remarks>
public sealed class MessageContextFactory(IServiceProvider serviceProvider) : IMessageContextFactory
{
	[ThreadStatic] private static MessageContext? s_cachedContext;

	/// <summary>
	/// Creates a new message context instance, recycling a previously returned context when available.
	/// </summary>
	/// <returns> A new or recycled message context. </returns>
	public IMessageContext CreateContext()
	{
		var context = s_cachedContext;
		if (context is not null)
		{
			s_cachedContext = null;
			context.Initialize(serviceProvider);
			return context;
		}

		var fresh = new MessageContext();
		fresh.Initialize(serviceProvider);
		return fresh;
	}

	/// <summary>
	/// Creates a new message context instance with the specified properties.
	/// </summary>
	/// <param name="properties"> Optional properties to initialize the context with. </param>
	/// <returns> A new message context with the specified properties. </returns>
	public IMessageContext CreateContext(IDictionary<string, object>? properties)
	{
		var context = s_cachedContext;
		if (context is null)
		{
			context = new MessageContext();
		}
		else
		{
			s_cachedContext = null;
		}

		context.Initialize(serviceProvider);

		// Copy properties to the context if needed
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
	public IMessageContext CreateChildContext(IMessageContext parent)
	{
		ArgumentNullException.ThrowIfNull(parent);
		return parent.CreateChildContext();
	}

	/// <inheritdoc />
	/// <remarks>
	/// Recycles the context into a per-thread cache for reuse by the next <see cref="CreateContext()"/> call.
	/// Only one context is cached per thread; additional returned contexts are left for GC.
	/// </remarks>
	public void Return(IMessageContext context)
	{
		if (context is MessageContext mc)
		{
			mc.Reset();
			s_cachedContext = mc;
		}
	}
}
