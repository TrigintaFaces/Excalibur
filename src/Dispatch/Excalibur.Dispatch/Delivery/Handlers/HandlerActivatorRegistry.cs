// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// AOT-safe registry for handler context setters.
/// Source generators register typed context setters at module load time via <see cref="RegisterContextSetter{T}"/>.
/// The framework's <see cref="HandlerActivator"/> consults this registry to inject <see cref="IMessageContext"/>
/// without reflection or expression compilation.
/// </summary>
public static class HandlerActivatorRegistry
{
	private static readonly ConcurrentDictionary<Type, Action<object, IMessageContext>> _contextSetters = new();

	/// <summary>
	/// Registers a typed context setter for a handler type.
	/// Called by source-generated module initializers.
	/// </summary>
	/// <typeparam name="THandler">The handler type.</typeparam>
	/// <param name="setter">A delegate that sets the <see cref="IMessageContext"/> on the handler.</param>
	public static void RegisterContextSetter<THandler>(Action<THandler, IMessageContext> setter) where THandler : class
	{
		ArgumentNullException.ThrowIfNull(setter);
		_contextSetters[typeof(THandler)] = (handler, context) => setter((THandler)handler, context);
	}

	/// <summary>
	/// Tries to get a registered context setter for the specified handler type.
	/// </summary>
	/// <param name="handlerType">The handler type.</param>
	/// <param name="setter">The context setter delegate, if registered.</param>
	/// <returns><c>true</c> if a context setter was found; otherwise, <c>false</c>.</returns>
	public static bool TryGetContextSetter(Type handlerType, out Action<object, IMessageContext>? setter)
	{
		return _contextSetters.TryGetValue(handlerType, out setter);
	}

	/// <summary>
	/// Returns whether any context setter is registered for the specified handler type.
	/// </summary>
	/// <param name="handlerType">The handler type.</param>
	/// <returns><c>true</c> if a context setter is registered.</returns>
	public static bool HasContextSetter(Type handlerType)
	{
		return _contextSetters.ContainsKey(handlerType);
	}
}
