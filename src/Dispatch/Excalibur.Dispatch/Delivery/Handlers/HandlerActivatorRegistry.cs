// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// AOT-safe registry for handler context setters.
/// Source generators register typed context setters at module load time via <see cref="RegisterContextSetter{T}"/>.
/// The framework's <see cref="HandlerActivator"/> and <see cref="AotHandlerActivator"/> consult this registry to inject <see cref="IMessageContext"/>
/// without reflection or expression compilation.
/// </summary>
public static class HandlerActivatorRegistry
{
	private static readonly ConcurrentDictionary<Type, ContextSetterSlot> _contextSetters = new();

	/// <summary>
	/// Registers a typed context setter for a handler type.
	/// Called by source-generated module initializers.
	/// </summary>
	/// <remarks>
	/// A registration completed before a dispatch begins is visible to that dispatch, including warmed and
	/// frozen activation plans. An overlapping dispatch may observe either registration. Handlers implementing
	/// <see cref="IMessageContextAware"/> retain interface-based context injection in preference to this registry.
	/// </remarks>
	/// <typeparam name="THandler">The handler type.</typeparam>
	/// <param name="setter">A delegate that sets the <see cref="IMessageContext"/> on the handler.</param>
	public static void RegisterContextSetter<THandler>(Action<THandler, IMessageContext> setter) where THandler : class
	{
		ArgumentNullException.ThrowIfNull(setter);
		GetContextSetterSlot(typeof(THandler)).Set((handler, context) => setter((THandler)handler, context));
	}

	/// <summary>
	/// Tries to get a registered context setter for the specified handler type.
	/// </summary>
	/// <param name="handlerType">The handler type.</param>
	/// <param name="setter">The context setter delegate, if registered.</param>
	/// <returns><c>true</c> if a context setter was found; otherwise, <c>false</c>.</returns>
	public static bool TryGetContextSetter(Type handlerType, out Action<object, IMessageContext>? setter)
	{
		ArgumentNullException.ThrowIfNull(handlerType);
		setter = _contextSetters.TryGetValue(handlerType, out var slot) ? slot.Current : null;
		return setter is not null;
	}

	/// <summary>
	/// Returns whether any context setter is registered for the specified handler type.
	/// </summary>
	/// <param name="handlerType">The handler type.</param>
	/// <returns><c>true</c> if a context setter is registered.</returns>
	public static bool HasContextSetter(Type handlerType)
	{
		ArgumentNullException.ThrowIfNull(handlerType);
		return _contextSetters.TryGetValue(handlerType, out var slot) && slot.HasSetter;
	}
	// Slots are never replaced: plans built before registration observe subsequent publication.
	internal static ContextSetterSlot GetContextSetterSlot(Type handlerType) =>
		_contextSetters.GetOrAdd(handlerType, static _ => new ContextSetterSlot());

	internal sealed class ContextSetterSlot
	{
		private Action<object, IMessageContext>? _current;
		internal Action<object, IMessageContext>? Current => Volatile.Read(ref _current);
		internal bool HasSetter => Current is not null;
		internal void Set(Action<object, IMessageContext> setter) => Volatile.Write(ref _current, setter);
	}
}
